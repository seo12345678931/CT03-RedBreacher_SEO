using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentPartDropVisual : MonoBehaviour
{
    private static readonly Queue<EquipmentPartDropVisual> Pool = new Queue<EquipmentPartDropVisual>();
    private static Transform poolRoot;
    private static Sprite fallbackSprite;

    private SpriteRenderer spriteRenderer;
    private Coroutine animationRoutine;

    public static void Play(
        Sprite icon,
        EquipmentPartRarity rarity,
        Vector3 dropPosition,
        Transform collectTarget,
        float popDuration,
        float holdDuration,
        float collectDuration,
        float popDistance,
        float visualSize,
        int sortingOrder)
    {
        EquipmentPartDropVisual visual = Get();
        visual.Begin(
            icon,
            rarity,
            dropPosition,
            collectTarget,
            popDuration,
            holdDuration,
            collectDuration,
            popDistance,
            visualSize,
            sortingOrder);
    }

    private static EquipmentPartDropVisual Get()
    {
        EnsurePoolRoot();

        // 이전 재생 세션에서 파괴된(도메인 리로드 비활성 시 정적 풀에 남는) 항목은 건너뛴다.
        EquipmentPartDropVisual visual = null;
        while (Pool.Count > 0)
        {
            EquipmentPartDropVisual candidate = Pool.Dequeue();
            if (candidate != null)
            {
                visual = candidate;
                break;
            }
        }

        if (visual == null)
        {
            visual = Create();
        }

        visual.transform.SetParent(poolRoot, false);
        visual.gameObject.SetActive(true);
        return visual;
    }

    private static EquipmentPartDropVisual Create()
    {
        GameObject visualObject = new GameObject("Equipment Part Drop Visual");
        visualObject.transform.SetParent(poolRoot, false);
        EquipmentPartDropVisual visual = visualObject.AddComponent<EquipmentPartDropVisual>();
        visual.spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualObject.SetActive(false);
        return visual;
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null)
        {
            return;
        }

        // poolRoot가 없거나 재생 종료로 파괴된 경우, 풀에 남은 파괴된 참조도 함께 비운다.
        Pool.Clear();
        GameObject rootObject = new GameObject("EquipmentPartDropVisualPool");
        DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
    }

    private void Begin(
        Sprite icon,
        EquipmentPartRarity rarity,
        Vector3 dropPosition,
        Transform collectTarget,
        float popDuration,
        float holdDuration,
        float collectDuration,
        float popDistance,
        float visualSize,
        int sortingOrder)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        StopAnimation();
        spriteRenderer.sprite = icon != null ? icon : GetFallbackSprite();
        spriteRenderer.color = GetRarityColor(rarity);
        spriteRenderer.sortingOrder = sortingOrder;

        transform.position = CombatPlane.WithFixedY(dropPosition);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = Vector3.zero;

        animationRoutine = StartCoroutine(Animate(
            collectTarget,
            Mathf.Max(0.01f, popDuration),
            Mathf.Max(0f, holdDuration),
            Mathf.Max(0.01f, collectDuration),
            Mathf.Max(0f, popDistance),
            Mathf.Max(0.01f, visualSize)));
    }

    private IEnumerator Animate(
        Transform collectTarget,
        float popDuration,
        float holdDuration,
        float collectDuration,
        float popDistance,
        float visualSize)
    {
        Vector3 startPosition = transform.position;
        Vector3 popDirection = GetPopDirection();
        Vector3 poppedPosition = CombatPlane.WithFixedY(startPosition + popDirection * popDistance);
        Vector3 targetScale = GetTargetScale(visualSize);

        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = CombatPlane.WithFixedY(Vector3.Lerp(startPosition, poppedPosition, eased));
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased);
            yield return null;
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        Vector3 collectStart = transform.position;
        elapsed = 0f;
        while (elapsed < collectDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / collectDuration);
            float eased = t * t * (3f - 2f * t);
            Vector3 targetPosition = GetCollectTargetPosition(collectTarget, collectStart);

            transform.position = CombatPlane.WithFixedY(Vector3.Lerp(collectStart, targetPosition, eased));
            transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, eased);
            Color color = spriteRenderer.color;
            color.a = 1f - eased;
            spriteRenderer.color = color;
            yield return null;
        }

        animationRoutine = null;
        Release();
    }

    private Vector3 GetCollectTargetPosition(Transform collectTarget, Vector3 fallbackPosition)
    {
        if (collectTarget == null)
        {
            return fallbackPosition;
        }

        if (collectTarget is RectTransform rectTransform)
        {
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace && Camera.main != null)
            {
                // Screen Space UI 목표는 메인 카메라의 전투 평면 위치로 변환한다.
                Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera
                    ? canvas.worldCamera
                    : null;
                Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, rectTransform.position);
                Ray ray = Camera.main.ScreenPointToRay(screenPosition);
                Plane combatPlane = new Plane(Vector3.up, CombatPlane.WithFixedY(fallbackPosition));
                if (combatPlane.Raycast(ray, out float distance))
                {
                    return CombatPlane.WithFixedY(ray.GetPoint(distance));
                }
            }
        }

        return CombatPlane.WithFixedY(collectTarget.position);
    }

    private Vector3 GetPopDirection()
    {
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude <= 0f)
        {
            direction = Vector2.up;
        }

        return new Vector3(direction.x, 0f, direction.y);
    }

    private Vector3 GetTargetScale(float visualSize)
    {
        Sprite sprite = spriteRenderer.sprite;
        float spriteSize = sprite != null
            ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)
            : 1f;
        float scale = visualSize / Mathf.Max(0.01f, spriteSize);
        return Vector3.one * scale;
    }

    private void Release()
    {
        StopAnimation();
        spriteRenderer.sprite = null;
        transform.SetParent(poolRoot, false);
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }

    private void StopAnimation()
    {
        if (animationRoutine == null)
        {
            return;
        }

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private static Color GetRarityColor(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => new Color(0.35f, 0.8f, 1f, 1f),
            EquipmentPartRarity.Epic => new Color(1f, 0.45f, 0.9f, 1f),
            EquipmentPartRarity.Legendary => new Color(1f, 0.72f, 0.18f, 1f),
            _ => Color.white
        };
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = CombatVisualFactory.CreateCircleSprite(Color.white);
        }

        return fallbackSprite;
    }
}
