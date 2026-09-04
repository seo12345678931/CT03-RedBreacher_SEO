using DG.Tweening;
using UnityEngine;

public class inventoryTweenTransition : MonoBehaviour
{
    [Header("Panel Target")]
    [SerializeField] private RectTransform autoSellSettingsPanel;
    
    [Header("Panel Open")]
    [SerializeField] private Vector3 autoSellPanelFromLocalPosition = new Vector3(0f, 1700f, 0f);
    [SerializeField] private float autoSellPanelMoveDuration = 0.5f;
    [SerializeField] private float autoSellPanelMoveDelay;
    [SerializeField] private Ease autoSellPanelMoveEase = Ease.OutExpo;

    private Vector3 autoSellPanelStartLocalPosition;
    private bool hasAutoSellPanelStartLocalPosition;
    private bool wasAutoSellSettingsPanelActive;
    private Tween autoSellPanelTween;

    private void Awake()
    {
        ResolveReferences();
        CacheAutoSellPanelStartPosition();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheAutoSellPanelStartPosition();
        wasAutoSellSettingsPanelActive = IsAutoSellSettingsPanelActive();

        if (wasAutoSellSettingsPanelActive)
        {
            PlayAutoSellSettingsPanel();
        }
    }

    private void Update()
    {
        ResolveReferences();

        bool isActive = IsAutoSellSettingsPanelActive();
        if (isActive && !wasAutoSellSettingsPanelActive)
        {
            PlayAutoSellSettingsPanel();
        }

        wasAutoSellSettingsPanelActive = isActive;
    }

    private void OnDisable()
    {
        autoSellPanelTween?.Kill(false);
        autoSellPanelTween = null;
    }

    public void PlayAutoSellSettingsPanel()
    {
        ResolveReferences();
        CacheAutoSellPanelStartPosition();
        if (autoSellSettingsPanel == null)
        {
            return;
        }

        Vector3 targetLocalPosition = hasAutoSellPanelStartLocalPosition
            ? autoSellPanelStartLocalPosition
            : autoSellSettingsPanel.localPosition;

        autoSellPanelTween?.Kill(false);
        autoSellSettingsPanel.localPosition = autoSellPanelFromLocalPosition;
        autoSellPanelTween = autoSellSettingsPanel
            .DOLocalMove(targetLocalPosition, Mathf.Max(0.01f, autoSellPanelMoveDuration))
            .SetDelay(Mathf.Max(0f, autoSellPanelMoveDelay))
            .SetEase(autoSellPanelMoveEase)
            .OnComplete(() => autoSellPanelTween = null);
    }

    public void ResetAutoSellSettingsPanel()
    {
        ResolveReferences();
        autoSellPanelTween?.Kill(false);
        autoSellPanelTween = null;

        if (autoSellSettingsPanel != null && hasAutoSellPanelStartLocalPosition)
        {
            autoSellSettingsPanel.localPosition = autoSellPanelStartLocalPosition;
        }
    }

    private void ResolveReferences()
    {
        autoSellSettingsPanel ??= FindRectTransformByName("autoSellSettingsPanel");
        autoSellSettingsPanel ??= FindRectTransformByName("AutoSellSettingsPanel");
        autoSellSettingsPanel ??= FindRectTransformByName("AutoSellSettings Panel");
    }

    private void CacheAutoSellPanelStartPosition()
    {
        if (autoSellSettingsPanel == null || hasAutoSellPanelStartLocalPosition)
        {
            return;
        }

        autoSellPanelStartLocalPosition = autoSellSettingsPanel.localPosition;
        hasAutoSellPanelStartLocalPosition = true;
    }

    private bool IsAutoSellSettingsPanelActive()
    {
        return autoSellSettingsPanel != null && autoSellSettingsPanel.gameObject.activeInHierarchy;
    }

    private static RectTransform FindRectTransformByName(string objectName)
    {
        foreach (RectTransform rectTransform in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (rectTransform.name == objectName)
            {
                return rectTransform;
            }
        }

        return null;
    }
}
