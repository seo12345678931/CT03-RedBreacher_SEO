using UnityEngine;

/// <summary>
/// 모바일 기기의 다이나믹 아일랜드, 노치, 펀치홀 영역을 피해 상단 UI를 아래로 내린다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MobileSafeAreaTopPadding : MonoBehaviour
{
    [SerializeField] private float extraTopPadding;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Vector2 baseOffsetMin;
    private Vector2 baseOffsetMax;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastScaleFactor;
    private bool applying;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        baseOffsetMin = rectTransform.offsetMin;
        baseOffsetMax = rectTransform.offsetMax;
        ApplySafeArea();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        if (HasScreenChanged())
        {
            ApplySafeArea();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && !applying)
        {
            ApplySafeArea();
        }
    }

    private bool HasScreenChanged()
    {
        float scaleFactor = GetCanvasScaleFactor();
        return Screen.width != lastScreenWidth ||
               Screen.height != lastScreenHeight ||
               !IsSameRect(Screen.safeArea, lastSafeArea) ||
               !Mathf.Approximately(scaleFactor, lastScaleFactor);
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        float scaleFactor = GetCanvasScaleFactor();
        float topPadding = Mathf.Max(0f, Screen.height - safeArea.yMax) / scaleFactor;

        // RectTransform offset 단위는 CanvasScaler 적용 후의 로컬 단위라 scaleFactor로 변환한다.
        applying = true;
        rectTransform.offsetMin = baseOffsetMin;
        rectTransform.offsetMax = baseOffsetMax - new Vector2(0f, topPadding + extraTopPadding);
        applying = false;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastSafeArea = safeArea;
        lastScaleFactor = scaleFactor;
    }

    private float GetCanvasScaleFactor()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        return rootCanvas != null ? Mathf.Max(0.01f, rootCanvas.scaleFactor) : 1f;
    }

    private static bool IsSameRect(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.width, b.width) &&
               Mathf.Approximately(a.height, b.height);
    }
}
