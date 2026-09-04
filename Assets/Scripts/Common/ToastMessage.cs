using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드 한 줄(<see cref="Show"/>)로 화면 하단에 잠깐 떴다 사라지는 토스트를 띄운다.
/// 전용 오버레이 캔버스를 런타임에 직접 생성하므로 씬에 별도 UI를 둘 필요가 없다.
/// 기본 TMP 폰트에는 한글 폴백이 없으므로, 한글 메시지는 <paramref name="font"/>로 한글 폰트를 넘겨야 한다.
/// </summary>
public class ToastMessage : MonoBehaviour
{
    private static ToastMessage instance;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private RectTransform panelRect;
    private Coroutine routine;

    /// <summary>
    /// 토스트를 띄운다. 이미 떠 있으면 새 메시지로 교체한다.
    /// <paramref name="anchor"/>는 화면 기준 정규화 위치(0~1, 예: (0.5,0) 하단중앙, (0.5,0.5) 정중앙, (0.5,1) 상단중앙),
    /// <paramref name="anchoredPosition"/>는 그 기준점에서의 픽셀 오프셋(1080x1920 기준).
    /// </summary>
    public static void Show(string message, TMP_FontAsset font = null, float duration = 2f,
        Vector2? anchoredPosition = null, Vector2? anchor = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        EnsureInstance();
        if (font != null)
        {
            instance.label.font = font;
        }

        if (anchor.HasValue)
        {
            instance.panelRect.anchorMin = anchor.Value;
            instance.panelRect.anchorMax = anchor.Value;
            instance.panelRect.pivot = anchor.Value;
        }

        if (anchoredPosition.HasValue)
        {
            instance.panelRect.anchoredPosition = anchoredPosition.Value;
        }

        instance.Play(message, duration);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("[ToastMessage]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ToastMessage>();
        instance.Build();
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // 항상 최상단.

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 260f);
        panelRect.sizeDelta = new Vector2(820f, 130f);

        group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        GameObject text = new GameObject("Label");
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = text.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32f, 16f);
        textRect.offsetMax = new Vector2(-32f, -16f);

        label = text.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 28f;
        label.fontSizeMax = 48f;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private void Play(string message, float duration)
    {
        label.text = message;
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Run(duration));
    }

    private IEnumerator Run(float duration)
    {
        yield return Fade(0f, 1f, 0.2f);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));
        yield return Fade(1f, 0f, 0.35f);
        routine = null;
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }

        group.alpha = to;
    }
}
