using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보상 클레임 시 클릭 위치에서 상단 재화 아이콘으로 여러 개의 아이콘이 분출했다가
/// 포물선을 그리며 빨려들어가는 연출을 재생한다. 아이콘이 도착하는 시점에 재화 숫자가
/// 카운트업되도록 <see cref="PlayerStatusHud"/>에 hold를 건다. DOTween 기반.
/// </summary>
public class RewardFlyAnimator : MonoBehaviour
{
    private const int IconCount = 5;
    private const float TravelDuration = 0.55f;
    private const float StaggerPerIcon = 0.05f;

    private static RewardFlyAnimator instance;

    private RectTransform overlayRoot;
    private RectTransform creditIcon;
    private RectTransform crystalIcon;
    private PlayerStatusHud hud;

    public static RewardFlyAnimator Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("RewardFlyAnimator");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<RewardFlyAnimator>();
            }

            return instance;
        }
    }

    public void PlayReward(Vector3 sourceWorldPosition, CurrencyType currency, int amount, float iconSize = 56f)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!EnsureReferences())
        {
            Debug.LogWarning("[RewardFly] 참조(UI_Canvas_Game/재화 아이콘) 확보 실패 — 연출 재생 불가.");
            return;
        }

        // 팝업(도전과제 등) 위로 항상 보이도록 매 재생마다 오버레이를 최상단으로 올린다.
        overlayRoot.SetAsLastSibling();

        RectTransform target = currency == CurrencyType.CoreCrystals ? crystalIcon : creditIcon;
        if (target == null)
        {
            return;
        }

        Sprite sprite = target.GetComponent<Image>() != null ? target.GetComponent<Image>().sprite : null;

        // 도착 시점에 숫자가 오르도록 비행 시간만큼 표시값을 잡아둔다.
        if (hud != null)
        {
            hud.HoldCurrencyDisplay(currency, TravelDuration);
        }

        PlayBurst(sprite, sourceWorldPosition, target, Mathf.Max(32f, iconSize));
    }

    private void PlayBurst(Sprite sprite, Vector3 startWorld, RectTransform target, float iconSize)
    {
        // pivot이 (0,0.5) 등 중앙이 아니어도 아이콘의 '시각적 중심'으로 빨려들어가도록 rect 중심을 쓴다.
        Vector3 endWorld = target != null ? GetRectWorldCenter(target) : startWorld;

        for (int i = 0; i < IconCount; i++)
        {
            FlyOne(sprite, startWorld, endWorld, iconSize, i);
        }

        if (target != null)
        {
            // 마지막 아이콘이 도착하는 시점에 타깃 아이콘을 펄스시킨다.
            float pulseDelay = (IconCount - 1) * StaggerPerIcon + TravelDuration;
            DOVirtual.DelayedCall(pulseDelay, () => Pulse(target), false).SetUpdate(true);
        }
    }

    private void FlyOne(Sprite sprite, Vector3 startWorld, Vector3 endWorld, float iconSize, int index)
    {
        GameObject go = new GameObject("FlyIcon", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(overlayRoot, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        // 시작(다른 캔버스의 버튼 등)과 끝(재화 아이콘) 좌표를 스크린 경유로 오버레이 로컬로 변환한다.
        // 캔버스 스케일 모드가 달라도 어긋나지 않는다.
        Vector3 start = WorldToOverlayLocal(startWorld);
        Vector3 end = WorldToOverlayLocal(endWorld);

        // 분출: 시작 지점에서 링 형태로 튕겨 나갔다가 타깃으로 모이는 제어점.
        float angle = (index / (float)IconCount) * Mathf.PI * 2f;
        Vector2 burst = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (iconSize * (1.1f + 0.35f * index));
        Vector3 control = start + (Vector3)burst + (end - start) * 0.35f;

        rect.localPosition = start;
        rect.localScale = Vector3.one * 1.3f;

        // 0→1 진행값을 DOTween으로 구동하고, 위치/스케일/알파를 포물선(SmoothStep)으로 평가한다.
        float driver = 0f;
        DOTween.To(() => driver, value =>
            {
                driver = value;
                float e = Mathf.SmoothStep(0f, 1f, value);
                rect.localPosition = Quadratic(start, control, end, e);
                float s = Mathf.Lerp(1.3f, 0.65f, e);
                rect.localScale = new Vector3(s, s, 1f);
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.8f, e));
            }, 1f, TravelDuration)
            .SetEase(Ease.Linear)
            .SetDelay(index * StaggerPerIcon)
            .SetUpdate(true)
            .OnComplete(() => Destroy(go));
    }

    private static void Pulse(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        // 도착 강조: 살짝 부풀었다가 원래 스케일로 복귀하는 펀치 연출.
        target.DOKill();
        target.localScale = Vector3.one;
        target.DOPunchScale(new Vector3(0.25f, 0.25f, 0f), 0.18f, 1, 0.5f).SetUpdate(true);
    }

    /// <summary>
    /// pivot/anchor와 무관하게 사각형의 중심을 월드 좌표로 반환한다.
    /// 호출부가 시작 위치를 잡을 때도 이걸 써야 도착 지점과 같은 기준(시각적 중심)이 되어 어긋나지 않는다.
    /// </summary>
    public static Vector3 GetRectWorldCenter(RectTransform rect)
    {
        return rect != null ? rect.TransformPoint(rect.rect.center) : Vector3.zero;
    }

    private bool EnsureReferences()
    {
        if (overlayRoot != null && creditIcon != null && crystalIcon != null)
        {
            return true;
        }

        // Credits_Panel이 다른 부모(Top UI 등)로 옮겨져도 동작하도록 하드코딩 경로 대신 이름으로 탐색한다.
        Transform creditsPanel = FindInScene("Credits_Panel");
        if (creditsPanel != null)
        {
            if (creditIcon == null)
            {
                creditIcon = creditsPanel.Find("Credit/Icon") as RectTransform;
            }

            if (crystalIcon == null)
            {
                crystalIcon = creditsPanel.Find("Core Crystal/Icon") as RectTransform;
            }
        }

        if (creditIcon == null || crystalIcon == null)
        {
            Debug.LogWarning($"[RewardFly] 재화 아이콘 탐색 실패 (creditsPanel={creditsPanel != null}, "
                + $"credit={creditIcon != null}, crystal={crystalIcon != null}). "
                + "Credits_Panel 하위에 'Credit/Icon', 'Core Crystal/Icon'이 있는지 확인하세요.");
            return false;
        }

        // 연출 오버레이는 재화 아이콘이 속한 (루트)캔버스에 붙인다. Credits_Panel이 Top UI 등
        // 다른 캔버스(스케일 모드/정렬순서가 다름)로 옮겨가도 타깃과 같은 캔버스에 그려져
        // 좌표·렌더 순서가 어긋나지 않는다.
        Canvas iconCanvas = creditIcon.GetComponentInParent<Canvas>();
        RectTransform canvasRect = iconCanvas != null && iconCanvas.rootCanvas != null
            ? iconCanvas.rootCanvas.transform as RectTransform
            : null;
        if (canvasRect == null)
        {
            GameObject fallbackCanvas = GameObject.Find("UI_Canvas_Game");
            canvasRect = fallbackCanvas != null ? fallbackCanvas.transform as RectTransform : null;
        }

        if (canvasRect == null)
        {
            Debug.LogWarning("[RewardFly] 연출을 붙일 캔버스를 찾지 못했습니다.");
            return false;
        }

        if (overlayRoot == null)
        {
            GameObject go = new GameObject("RewardFlyOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlayRoot = (RectTransform)go.transform;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        if (overlayRoot.parent != canvasRect)
        {
            overlayRoot.SetParent(canvasRect, false);
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
            overlayRoot.localScale = Vector3.one;
        }

        overlayRoot.SetAsLastSibling();

        hud = FindFirstObjectByType<PlayerStatusHud>(FindObjectsInactive.Include);

        return overlayRoot != null && creditIcon != null && crystalIcon != null;
    }

    private Vector3 WorldToOverlayLocal(Vector3 world)
    {
        if (overlayRoot == null)
        {
            return Vector3.zero;
        }

        Canvas canvas = overlayRoot.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screen, cam, out Vector2 local);
        return local;
    }

    private static Vector3 Quadratic(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    // 씬 전체에서 이름으로 Transform을 찾는다(활성 우선, 비활성 폴백). 부모 경로에 의존하지 않는다.
    private static Transform FindInScene(string objectName)
    {
        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active.transform;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform tr = all[i];
            if (tr.name == objectName && tr.gameObject.scene.IsValid())
            {
                return tr;
            }
        }

        return null;
    }
}
