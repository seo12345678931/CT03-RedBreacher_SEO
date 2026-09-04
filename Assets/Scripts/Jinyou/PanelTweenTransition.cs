using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PanelTweenTransition : MonoBehaviour
{
    private enum TransitionStyle
    {
        Auto,
        ScaleFade,
        SlideFromRight
    }

    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool bindExitButtons = true;
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.Auto;
    [SerializeField] private float openDuration = 0.16f;
    [SerializeField] private float closeDuration = 0.12f;
    [SerializeField] private float openStartScale = 0.92f;
    [SerializeField] private float closeEndScale = 0.96f;
    [SerializeField] private float slideDistance = 900f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private bool hasBaseAnchoredPosition;
    private bool closing;

    private void Awake()
    {
        ResolveReferences();
        BindExitButtons();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindExitButtons();
        if (playOnEnable)
        {
            PlayOpen();
        }
    }

    private void OnDisable()
    {
        KillTweens();
        closing = false;
    }

    /// <summary>패널에 PanelTweenTransition을 보장(없으면 부착)한다. 트랜지션 부착 패턴의 단일 출처.</summary>
    public static PanelTweenTransition EnsureOn(GameObject panel)
    {
        if (panel == null)
        {
            return null;
        }

        PanelTweenTransition transition = panel.GetComponent<PanelTweenTransition>();
        if (transition == null)
        {
            transition = panel.AddComponent<PanelTweenTransition>();
        }

        return transition;
    }

    public void Close()
    {
        PlayClose(() => gameObject.SetActive(false));
    }

    public void PlayOpen()
    {
        ResolveReferences();
        KillTweens();
        closing = false;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float duration = Mathf.Max(0.01f, openDuration);
        if (ResolveStyle() == TransitionStyle.SlideFromRight && rectTransform != null)
        {
            // 상점/기지처럼 큰 섹션 팝업은 오른쪽에서 밀려 들어오게 연출한다.
            transform.localScale = Vector3.one;
            rectTransform.anchoredPosition = baseAnchoredPosition + Vector2.right * GetSlideDistance();
            rectTransform.DOAnchorPos(baseAnchoredPosition, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetTarget(this);
            canvasGroup.DOFade(1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(this);
            return;
        }

        transform.localScale = Vector3.one * Mathf.Clamp(openStartScale, 0.01f, 1f);
        transform.DOScale(Vector3.one, duration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetTarget(this);
        canvasGroup.DOFade(1f, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void PlayClose(Action onComplete)
    {
        ResolveReferences();
        if (closing)
        {
            return;
        }

        closing = true;
        KillTweens();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float duration = Mathf.Max(0.01f, closeDuration);
        DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(this)
            .Join(BuildCloseTween(duration))
            .Join(canvasGroup.DOFade(0f, duration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                RestoreVisibleState();
                closing = false;
                onComplete?.Invoke();
            });
    }

    private void BindExitButtons()
    {
        if (!bindExitButtons)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.name != "ExitButton" || !HasPersistentSetActiveTarget(button, gameObject))
            {
                continue;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Close);
        }
    }

    private void ResolveReferences()
    {
        canvasGroup ??= GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        rectTransform ??= transform as RectTransform;
        if (rectTransform != null && !hasBaseAnchoredPosition)
        {
            baseAnchoredPosition = rectTransform.anchoredPosition;
            hasBaseAnchoredPosition = true;
        }
    }

    private void KillTweens()
    {
        DOTween.Kill(this, false);
        transform.DOKill(false);
        canvasGroup?.DOKill(false);
        RestoreVisibleState();
    }

    private void RestoreVisibleState()
    {
        transform.localScale = Vector3.one;
        if (rectTransform != null && hasBaseAnchoredPosition)
        {
            rectTransform.anchoredPosition = baseAnchoredPosition;
        }

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private static bool HasPersistentSetActiveTarget(Button button, GameObject targetPanel)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentTarget(i) == targetPanel
                && button.onClick.GetPersistentMethodName(i) == "SetActive")
            {
                return true;
            }
        }

        return false;
    }

    private Tween BuildCloseTween(float duration)
    {
        if (ResolveStyle() == TransitionStyle.SlideFromRight && rectTransform != null)
        {
            return rectTransform
                .DOAnchorPos(baseAnchoredPosition + Vector2.right * GetSlideDistance(), duration)
                .SetEase(Ease.InCubic);
        }

        return transform
            .DOScale(Vector3.one * Mathf.Clamp(closeEndScale, 0.01f, 1f), duration)
            .SetEase(Ease.InQuad);
    }

    private TransitionStyle ResolveStyle()
    {
        if (transitionStyle != TransitionStyle.Auto)
        {
            return transitionStyle;
        }

        return IsSlidePopupName(name) ? TransitionStyle.SlideFromRight : TransitionStyle.ScaleFade;
    }

    private float GetSlideDistance()
    {
        if (rectTransform != null)
        {
            Rect parentRect = rectTransform.parent is RectTransform parentRectTransform
                ? parentRectTransform.rect
                : rectTransform.rect;
            return Mathf.Max(slideDistance, parentRect.width + rectTransform.rect.width);
        }

        return slideDistance;
    }

    private static bool IsSlidePopupName(string popupName)
    {
        return popupName == "Shop_Popup" || popupName == "Base_Popup";
    }
}
