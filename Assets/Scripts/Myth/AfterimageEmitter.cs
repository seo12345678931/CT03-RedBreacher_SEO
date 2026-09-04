using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회피/대시처럼 빠르게 이동할 때 본체 스프라이트의 반투명 복제본(잔상)을 남겨 속도감을 준다.
/// 매 프레임 <see cref="Emit"/>을 호출해도 설정된 간격으로만 잔상이 생성되도록 내부에서 조절한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AfterimageEmitter : MonoBehaviour
{
    [Header("잔상 연출")]
    [SerializeField] private bool enableAfterimage = true;
    [Tooltip("잔상을 남기는 최소 시간 간격(초). 작을수록 잔상이 촘촘해집니다.")]
    [SerializeField] private float interval = 0.025f;
    [Tooltip("잔상 하나가 사라지기까지 걸리는 시간(초).")]
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private Color color = new Color(0.45f, 0.9f, 1f, 0.6f);
    [Tooltip("잔상을 본체보다 뒤에 그리기 위한 정렬 순서 보정값.")]
    [SerializeField] private int sortingOffset = -1;

    private float lastEmitTime = float.NegativeInfinity;
    private readonly List<GameObject> activeAfterimages = new List<GameObject>();

    public bool IsEnabled => enableAfterimage;

    /// <summary>
    /// 잔상을 하나 남긴다. force가 false면 설정된 간격을 지나지 않은 호출은 무시되므로
    /// 이동 중 매 프레임 호출해도 안전하다. 대시 시작 프레임에는 force=true로 즉시 한 장을 남기면 좋다.
    /// </summary>
    public void Emit(bool force = false)
    {
        if (!enableAfterimage)
        {
            return;
        }

        if (!force && Time.time - lastEmitTime < Mathf.Max(0.005f, interval))
        {
            return;
        }

        lastEmitTime = Time.time;
        SpawnAfterimage();
    }

    private void OnDisable()
    {
        ClearAfterimages();
    }

    private void SpawnAfterimage()
    {
        SpriteRenderer[] sources = GatherBodyRenderers();
        if (sources.Length == 0)
        {
            return;
        }

        GameObject ghostRoot = new GameObject("Afterimage");
        ghostRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);

        List<SpriteRenderer> ghostRenderers = new List<SpriteRenderer>(sources.Length);
        for (int i = 0; i < sources.Length; i++)
        {
            SpriteRenderer source = sources[i];
            if (source == null || !source.enabled || source.sprite == null)
            {
                continue;
            }

            GameObject part = new GameObject("Ghost Part");
            Transform partTransform = part.transform;
            partTransform.SetParent(ghostRoot.transform, false);
            partTransform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            partTransform.localScale = source.transform.lossyScale;

            SpriteRenderer ghost = part.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.sharedMaterial = source.sharedMaterial;
            ghost.sortingLayerID = source.sortingLayerID;
            ghost.sortingOrder = source.sortingOrder + sortingOffset;
            ghost.color = color;
            ghostRenderers.Add(ghost);
        }

        if (ghostRenderers.Count == 0)
        {
            Destroy(ghostRoot);
            return;
        }

        activeAfterimages.Add(ghostRoot);
        StartCoroutine(FadeAfterimage(ghostRoot, ghostRenderers.ToArray()));
    }

    private IEnumerator FadeAfterimage(GameObject ghostRoot, SpriteRenderer[] renderers)
    {
        float fadeDuration = Mathf.Max(0.05f, lifetime);
        float startAlpha = color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration && ghostRoot != null)
        {
            elapsed += Time.deltaTime;
            float alpha = startAlpha * (1f - Mathf.Clamp01(elapsed / fadeDuration));
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color rendererColor = renderers[i].color;
                rendererColor.a = alpha;
                renderers[i].color = rendererColor;
            }

            yield return null;
        }

        activeAfterimages.Remove(ghostRoot);
        if (ghostRoot != null)
        {
            Destroy(ghostRoot);
        }
    }

    private void ClearAfterimages()
    {
        for (int i = 0; i < activeAfterimages.Count; i++)
        {
            if (activeAfterimages[i] != null)
            {
                Destroy(activeAfterimages[i]);
            }
        }

        activeAfterimages.Clear();
    }

    private SpriteRenderer[] GatherBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> valid = new List<SpriteRenderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name != "__SpriteShapeShadow")
            {
                valid.Add(renderers[i]);
            }
        }

        return valid.ToArray();
    }
}
