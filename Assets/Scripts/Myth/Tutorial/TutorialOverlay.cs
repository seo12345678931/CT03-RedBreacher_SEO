using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼용 최상단 오버레이 프리팹 컨트롤러.
/// - 화면을 어둡히되 강조 타깃 영역만 구멍을 남긴다(상/하/좌/우 4개 검정 쿼드, 셰이더/마스크 불필요).
/// - 구멍 외 영역은 raycastTarget=true로 입력 차단 → 강조한 버튼만 눌리게 된다.
/// - 말풍선(TMP) + 선택적 "탭하여 계속" 힌트.
/// 타깃 위치는 매 프레임 다시 계산해 레이아웃이 늦게 잡히거나 움직여도 따라간다.
/// </summary>
[DisallowMultipleComponent]
public class TutorialOverlay : MonoBehaviour
{
    private const float HolePaddingNormalized = 0.012f;

    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_FontAsset font;

    [Header("Dim")]
    [SerializeField] private RectTransform topQuad;
    [SerializeField] private RectTransform bottomQuad;
    [SerializeField] private RectTransform leftQuad;
    [SerializeField] private RectTransform rightQuad;
    [SerializeField] private Image fullDim;

    [Header("Bubble")]
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private GameObject tapHint;

    [Header("Touch Icon Objects")] 
    [Tooltip("특정 튜토리얼 구간이 나올 때 터치입력을 아이콘을 표시하는 UI 오브젝트")] 
    [SerializeField] private GameObject touchDragIcon;
    
    [Header("Bubble Layout")]
    [SerializeField, Range(0.2f, 1f)] private float bubbleWidthNormalized = 0.84f;
    [SerializeField, Range(0f, 0.4f)] private float bubbleMarginXNormalized = 0.08f;
    [SerializeField, Range(0f, 0.5f)] private float bubbleBottomYNormalized = 0.10f;
    [SerializeField, Range(0.5f, 1f)] private float bubbleTopYNormalized = 0.90f;
    [SerializeField] private bool usePreferredBubbleHeight = true;
    [SerializeField, Min(40f)] private float fixedBubbleHeight = 130f;
    [SerializeField, Min(40f)] private float minBubbleHeight = 96f;
    [SerializeField, Min(40f)] private float maxBubbleHeight = 220f;
    [SerializeField, Min(0f)] private float bubbleVerticalPadding = 68f;

    [Header("Target Click Pulse")]
    [SerializeField] private bool enableTargetClickPulse = true;
    [SerializeField] private Color targetClickPulseColor = new Color(1f, 1f, 1f, 0.32f);
    [Tooltip("깜빡거리는 속도. 값이 높을수록 더 빨라짐.")]
    [SerializeField, Min(0.1f)] private float targetClickPulseSpeed = 2.4f;
    [SerializeField, Range(0f, 1f)] private float targetClickPulseMinAlpha = 0.08f;
    [SerializeField, Range(0f, 1f)] private float targetClickPulseMaxAlpha = 0.34f;

    private RectTransform highlightTarget;
    private bool tapArmed;
    private float tapReadyTime;
    private Action tapCallback;
    private TutorialAdvanceType activeAdvanceType;
    private bool showTouchDragGuide;
    private RectTransform targetClickPulseRect;
    private Image targetClickPulseImage;

    private readonly Vector3[] cornerBuffer = new Vector3[4];

    private void Awake()
    {
        BindPrefabReferences();
        ApplyFont();
        gameObject.SetActive(false);
    }

    private void Reset()
    {
        BindPrefabReferences();
        ApplyFont();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BindPrefabReferences();
        ApplyFont();
    }
#endif

    public void Configure(TMP_FontAsset bodyFont)
    {
        if (bodyFont != null)
        {
            font = bodyFont;
        }

        BindPrefabReferences();
        ApplyFont();
        gameObject.SetActive(false);
    }

    [ContextMenu("Auto Bind Children")]
    private void BindPrefabReferences()
    {
        if (root == null)
        {
            root = transform as RectTransform;
        }

        topQuad ??= FindRect("Dim_Top");
        bottomQuad ??= FindRect("Dim_Bottom");
        leftQuad ??= FindRect("Dim_Left");
        rightQuad ??= FindRect("Dim_Right");
        fullDim ??= FindComponent<Image>("Dim_Full");
        bubble ??= FindRect("Bubble");
        bubbleText ??= FindComponent<TMP_Text>("Bubble/Body");
        tapHint ??= FindObject("Bubble/TapHint");
        touchDragIcon ??= FindObject("TouchDragIcon");
    }

    private void ApplyFont()
    {
        if (font == null)
        {
            return;
        }

        if (bubbleText != null)
        {
            bubbleText.font = font;
        }

        TMP_Text hintText = tapHint != null ? tapHint.GetComponent<TMP_Text>() : null;
        if (hintText != null)
        {
            hintText.font = font;
        }
    }

    private bool HasRequiredReferences()
    {
        return root != null
            && topQuad != null
            && bottomQuad != null
            && leftQuad != null
            && rightQuad != null
            && fullDim != null
            && bubble != null
            && bubbleText != null
            && tapHint != null;
    }

    private RectTransform FindRect(string path)
    {
        Transform found = transform.Find(path);
        return found as RectTransform;
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform found = transform.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    private GameObject FindObject(string path)
    {
        Transform found = transform.Find(path);
        return found != null ? found.gameObject : null;
    }

    /// <summary>스텝을 표시한다. target이 null이면 전체 어둡힘.</summary>
    public void Show(string body, RectTransform target, TutorialAdvanceType advanceType, Action onTap)
    {
        Show(body, target, advanceType, onTap, false);
    }

    public void Show(
        string body,
        RectTransform target,
        TutorialAdvanceType advanceType,
        Action onTap,
        bool showTouchDragIcon)
    {
        BindPrefabReferences();
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[Tutorial] TutorialOverlay 프리팹의 필수 자식 참조가 비어 있습니다.");
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (bubbleText != null)
        {
            bubbleText.text = body ?? string.Empty;
        }

        highlightTarget = target;
        tapCallback = onTap;
        activeAdvanceType = advanceType;
        showTouchDragGuide = showTouchDragIcon;
        tapArmed = advanceType == TutorialAdvanceType.Tap;
        tapReadyTime = Time.unscaledTime + 0.2f; // 직전 입력이 곧장 다음 스텝을 넘기지 않도록.

        if (tapHint != null)
        {
            tapHint.SetActive(advanceType == TutorialAdvanceType.Tap);
        }

        RefreshTouchDragIcon();
        UpdateBubbleAnchor();
        RefreshSpotlight();
    }

    /// <summary>강조 타깃을 교체(레이아웃이 늦게 잡히는 패널 내부 요소 대응).</summary>
    public void SetHighlight(RectTransform target)
    {
        if (highlightTarget == target)
        {
            return;
        }

        highlightTarget = target;
        UpdateBubbleAnchor();
        RefreshSpotlight();
    }

    public void Hide()
    {
        tapArmed = false;
        tapCallback = null;
        activeAdvanceType = TutorialAdvanceType.Tap;
        showTouchDragGuide = false;
        highlightTarget = null;
        SetTouchDragIconActive(false);
        SetTargetClickPulseActive(false);
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        // 타깃이 움직이거나 레이아웃이 갱신될 수 있으므로 매 프레임 구멍을 재계산한다.
        if (highlightTarget != null)
        {
            RefreshSpotlight();
        }

        // 타깃 강조 연출
        UpdateTargetClickPulse();
    }

    private void Update()
    {
        if (!tapArmed || Time.unscaledTime < tapReadyTime)
        {
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            Action callback = tapCallback;
            tapArmed = false;
            callback?.Invoke();
        }
    }

    private void RefreshSpotlight()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        // Unity 컴파일러가 조건식 내부 out 변수를 항상 할당으로 보지 않아 먼저 선언한다.
        Rect hole = default;
        bool hasTarget = highlightTarget != null && TryGetNormalizedRect(highlightTarget, out hole);
        topQuad.gameObject.SetActive(hasTarget);
        bottomQuad.gameObject.SetActive(hasTarget);
        leftQuad.gameObject.SetActive(hasTarget);
        rightQuad.gameObject.SetActive(hasTarget);
        if (fullDim != null)
        {
            fullDim.gameObject.SetActive(!hasTarget);
        }

        if (!hasTarget)
        {
            SetTargetClickPulseActive(false);
            return;
        }

        float xMin = Mathf.Clamp01(hole.xMin - HolePaddingNormalized);
        float yMin = Mathf.Clamp01(hole.yMin - HolePaddingNormalized);
        float xMax = Mathf.Clamp01(hole.xMax + HolePaddingNormalized);
        float yMax = Mathf.Clamp01(hole.yMax + HolePaddingNormalized);

        SetAnchors(topQuad, 0f, yMax, 1f, 1f);
        SetAnchors(bottomQuad, 0f, 0f, 1f, yMin);
        SetAnchors(leftQuad, 0f, yMin, xMin, yMax);
        SetAnchors(rightQuad, xMax, yMin, 1f, yMax);
        RefreshTargetClickPulseRect(xMin, yMin, xMax, yMax);
    }

    // TargetClicked 이벤트일 때 타깃 영역을 펄스로 강조연출
    private void RefreshTargetClickPulseRect(float xMin, float yMin, float xMax, float yMax)
    {
        bool shouldShow = enableTargetClickPulse
            && activeAdvanceType == TutorialAdvanceType.TargetClicked
            && highlightTarget != null;
        SetTargetClickPulseActive(shouldShow);
        if (!shouldShow || targetClickPulseRect == null)
        {
            return;
        }

        SetAnchors(targetClickPulseRect, xMin, yMin, xMax, yMax);
    }

    private void UpdateTargetClickPulse()
    {
        // 타깃 영역이 없거나 튜토리얼이 닫히면 자동으로 비활성
        if (targetClickPulseImage == null || !targetClickPulseImage.gameObject.activeSelf)
        {
            return;
        }

        // unscaledTime => 튜토리얼 매니저에 보스 튜토리얼 전개 시 Time.timeScale = 0 이 되어도 계속 동작
        float pulse01 = (Mathf.Sin(Time.unscaledTime * targetClickPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color color = targetClickPulseColor;
        color.a = Mathf.Lerp(targetClickPulseMinAlpha, targetClickPulseMaxAlpha, pulse01);
        targetClickPulseImage.color = color;
    }

    private void SetTargetClickPulseActive(bool active)
    {
        EnsureTargetClickPulse();
        if (targetClickPulseImage == null)
        {
            return;
        }

        if (targetClickPulseImage.gameObject.activeSelf != active)
        {
            targetClickPulseImage.gameObject.SetActive(active);
        }
    }

    private void EnsureTargetClickPulse()
    {
        if (targetClickPulseImage != null)
        {
            return;
        }

        Transform existing = transform.Find("TargetClickPulse");
        if (existing != null)
        {
            targetClickPulseRect = existing as RectTransform;
            targetClickPulseImage = existing.GetComponent<Image>();
        }
        else
        {
            GameObject pulseObject = new GameObject("TargetClickPulse", typeof(RectTransform), typeof(Image));
            pulseObject.transform.SetParent(root != null ? root : transform, false);
            targetClickPulseRect = pulseObject.transform as RectTransform;
            targetClickPulseImage = pulseObject.GetComponent<Image>();
        }

        if (targetClickPulseImage != null)
        {
            targetClickPulseImage.raycastTarget = false;
            targetClickPulseImage.color = targetClickPulseColor;
            targetClickPulseImage.gameObject.SetActive(false);
        }

        if (bubble != null && targetClickPulseRect != null)
        {
            targetClickPulseRect.SetSiblingIndex(Mathf.Max(0, bubble.GetSiblingIndex()));
        }
    }

    private bool TryGetNormalizedRect(RectTransform target, out Rect normalized)
    {
        normalized = default;
        if (target == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return false;
        }

        Canvas targetCanvas = target.GetComponentInParent<Canvas>();
        Camera cam = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        target.GetWorldCorners(cornerBuffer);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[i]);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }

        // ScreenSpaceOverlay 캔버스는 정규화 좌표(0~1)가 화면 비율과 일치하므로
        // CanvasScaler 스케일 모드와 무관하게 앵커로 바로 매핑된다.
        normalized = Rect.MinMaxRect(
            min.x / Screen.width,
            min.y / Screen.height,
            max.x / Screen.width,
            max.y / Screen.height);
        return normalized.width > 0f && normalized.height > 0f;
    }

    private void UpdateBubbleAnchor()
    {
        if (bubble == null)
        {
            return;
        }

        float heightNormalized = GetBubbleHeightNormalized();
        if (showTouchDragGuide)
        {
            SetBubbleBottom(heightNormalized);
            return;
        }

        // 타깃이 화면 아래쪽이면 말풍선을 위로, 아니면 아래로 둬 겹치지 않게 한다.
        bool targetIsLow = false;
        if (highlightTarget != null && TryGetNormalizedRect(highlightTarget, out Rect holeN))
        {
            targetIsLow = holeN.center.y < 0.5f;
        }

        if (targetIsLow)
        {
            bubble.anchorMin = new Vector2(bubbleMarginXNormalized, bubbleTopYNormalized - heightNormalized);
            bubble.anchorMax = new Vector2(bubbleMarginXNormalized + bubbleWidthNormalized, bubbleTopYNormalized);
        }
        else
        {
            SetBubbleBottom(heightNormalized);
        }

        bubble.offsetMin = Vector2.zero;
        bubble.offsetMax = Vector2.zero;
    }

    private void SetBubbleBottom(float heightNormalized)
    {
        if (bubble == null)
        {
            return;
        }

        bubble.anchorMin = new Vector2(bubbleMarginXNormalized, bubbleBottomYNormalized);
        bubble.anchorMax = new Vector2(
            bubbleMarginXNormalized + bubbleWidthNormalized,
            bubbleBottomYNormalized + heightNormalized);
        bubble.offsetMin = Vector2.zero;
        bubble.offsetMax = Vector2.zero;
    }

    private void RefreshTouchDragIcon()
    {
        if (!showTouchDragGuide)
        {
            SetTouchDragIconActive(false);
            return;
        }

        SetTouchDragIconActive(true);
        RectTransform iconRect = touchDragIcon != null ? touchDragIcon.transform as RectTransform : null;
        if (iconRect == null)
        {
            return;
        }

        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;

        if (bubble != null)
        {
            iconRect.SetSiblingIndex(Mathf.Max(0, bubble.GetSiblingIndex()));
            bubble.SetAsLastSibling();
        }
    }

    private void SetTouchDragIconActive(bool active)
    {
        if (touchDragIcon != null && touchDragIcon.activeSelf != active)
        {
            touchDragIcon.SetActive(active);
        }
    }

    private float GetBubbleHeightNormalized()
    {
        if (Screen.height <= 0)
        {
            return 0.16f;
        }

        // 고정 높이를 선택하면 안내문 길이와 무관하게 인스펙터 지정값을 사용한다.
        if (!usePreferredBubbleHeight)
        {
            return Mathf.Max(40f, fixedBubbleHeight) / Screen.height;
        }

        float preferredHeight = minBubbleHeight;
        if (bubbleText != null)
        {
            // 자동 높이는 텍스트 높이에 패딩을 더한 뒤 min/max 범위로 제한한다.
            preferredHeight = bubbleText.GetPreferredValues(
                bubbleText.text,
                Screen.width * bubbleWidthNormalized,
                0f).y + bubbleVerticalPadding;
        }

        float height = Mathf.Clamp(preferredHeight, minBubbleHeight, Mathf.Max(minBubbleHeight, maxBubbleHeight));
        return height / Screen.height;
    }

    private static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, Mathf.Max(yMin, yMax));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
