using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GachaTweenTransition : MonoBehaviour
{
    [Header("Title Targets")]
    [SerializeField] private TMP_Text weaponTitleText;
    [SerializeField] private TMP_Text skillTitleText;

    [Header("Buttons Group Targets")]
    [SerializeField] private RectTransform weaponButtonsGroup;
    [SerializeField] private RectTransform skillButtonsGroup;

    [Header("Detail Button Targets")]
    [SerializeField] private RectTransform weaponDetailButton;
    [SerializeField] private RectTransform skillDetailButton;

    [Header("GachaBox Targets")]
    [SerializeField] private RectTransform weaponGachaBox;
    [SerializeField] private RectTransform skillGachaBox;
    [SerializeField] private GameObject weaponGachaBoxOpenGlow;
    [SerializeField] private GameObject skillGachaBoxOpenGlow;
    [SerializeField] private Sprite closeBox;
    [SerializeField] private Sprite openBox;

    [Header("GachaResult Target")] 
    [SerializeField] private RectTransform gachaPanelTransform;
    [SerializeField] private Button gachaResultConfirmButton;

    [Header(("GachaOddPanels Target"))] 
    [SerializeField] private RectTransform weaponGachaOddPanel;
    [SerializeField] private RectTransform skillGachaOddPanel;
    
    [Header("Move")]
    [SerializeField] private Vector2 titleTargetPosition = new Vector2(0f, 240f);
    [SerializeField] private Vector2 buttonsGroupTargetPosition = new Vector2(0f, -200f);
    [SerializeField] private Vector2 detailButtonTargetPosition = new Vector2(0f, -800f);
    [SerializeField] private float titleMoveDuration = 1f;
    [SerializeField] private float buttonsGroupMoveDuration = 1f;
    [SerializeField] private float detailButtonMoveDuration = 1f;
    [SerializeField] private Ease moveEase = Ease.InBack;

    [Header("Box Open")]
    [SerializeField] private float boxScaleTo = 1.1f;
    [SerializeField] private float boxScaleDuration = 1f;
    [SerializeField] private float boxScaleDelay = 0.5f;
    [SerializeField] private Ease boxScaleEase = Ease.InBack;

    [Header("Result Panel Open")]
    [SerializeField] private Vector3 resultPanelFromScale = new Vector3(1f, 0f, 1f);
    [SerializeField] private float resultPanelScaleDuration = 1f;
    [SerializeField] private float resultPanelScaleDelay = 0.3f;
    [SerializeField] private Ease resultPanelScaleEase = Ease.Linear;

    [Header("Odd Panel Open")]
    [SerializeField] private Vector3 oddPanelFromLocalPosition = new Vector3(0f, 1700f, 0f);
    [SerializeField] private float oddPanelMoveDuration = 0.5f;
    [SerializeField] private float oddPanelMoveDelay;
    [SerializeField] private Ease oddPanelMoveEase = Ease.OutExpo;

    private RectTransform weaponTitleRect;
    private RectTransform skillTitleRect;
    private Vector2 weaponTitleStartPosition;
    private Vector2 skillTitleStartPosition;
    private Vector2 weaponButtonsGroupStartPosition;
    private Vector2 skillButtonsGroupStartPosition;
    private Vector2 weaponDetailButtonStartPosition;
    private Vector2 skillDetailButtonStartPosition;
    private Vector3 weaponGachaBoxStartScale;
    private Vector3 skillGachaBoxStartScale;
    private Image weaponGachaBoxImage;
    private Image skillGachaBoxImage;
    private Sprite weaponInitialBoxSprite;
    private Sprite skillInitialBoxSprite;
    private Vector3 gachaPanelStartScale;
    private Vector3 weaponGachaOddPanelStartLocalPosition;
    private Vector3 skillGachaOddPanelStartLocalPosition;
    private bool hasWeaponStartPosition;
    private bool hasSkillStartPosition;
    private bool hasWeaponButtonsGroupStartPosition;
    private bool hasSkillButtonsGroupStartPosition;
    private bool hasWeaponDetailButtonStartPosition;
    private bool hasSkillDetailButtonStartPosition;
    private bool hasWeaponGachaBoxStartScale;
    private bool hasSkillGachaBoxStartScale;
    private bool hasGachaPanelStartScale;
    private bool hasWeaponGachaOddPanelStartLocalPosition;
    private bool hasSkillGachaOddPanelStartLocalPosition;
    private bool wasWeaponGachaOddPanelActive;
    private bool wasSkillGachaOddPanelActive;
    // Update 폴링에서 오드패널 참조가 아직 없을 때만 재해석하는 스로틀(프레임당 전체 힙 스캔 방지).
    private const float OddPanelResolveInterval = 0.5f;
    private float nextOddPanelResolveTime;
    private Tween activeTween;
    private Tween resultPanelTween;
    private Tween weaponGachaOddPanelTween;
    private Tween skillGachaOddPanelTween;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheOddPanelActiveStates();
        gachaResultConfirmButton?.onClick.AddListener(ResetAll);
    }

    private void Update()
    {
        PlayOddPanelTweensOnOpen();
    }

    private void OnDisable()
    {
        gachaResultConfirmButton?.onClick.RemoveListener(ResetAll);
        ResetAll();
    }

    public void PlayTitle(GachaCategory category, Action onComplete)
    {
        ResolveReferences();

        RectTransform titleTarget = category == GachaCategory.Skill ? skillTitleRect : weaponTitleRect;
        RectTransform buttonsGroupTarget = category == GachaCategory.Skill ? skillButtonsGroup : weaponButtonsGroup;
        RectTransform detailButtonTarget = category == GachaCategory.Skill ? skillDetailButton : weaponDetailButton;
        RectTransform boxTarget = category == GachaCategory.Skill ? skillGachaBox : weaponGachaBox;
        if (titleTarget == null && buttonsGroupTarget == null && detailButtonTarget == null && boxTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        ResetTitle(category);
        ResetButtonsGroup(category);
        ResetDetailButton(category);
        ResetBox(category);
        activeTween?.Kill(false);
        Sequence sequence = DOTween.Sequence();
        if (titleTarget != null)
        {
            sequence.Join(titleTarget
                .DOAnchorPos(titleTargetPosition, Mathf.Max(0.01f, titleMoveDuration))
                .SetEase(moveEase));
        }

        if (buttonsGroupTarget != null)
        {
            sequence.Join(buttonsGroupTarget
                .DOAnchorPos(buttonsGroupTargetPosition, Mathf.Max(0.01f, buttonsGroupMoveDuration))
                .SetEase(moveEase));
        }

        if (detailButtonTarget != null)
        {
            sequence.Join(detailButtonTarget
                .DOAnchorPos(detailButtonTargetPosition, Mathf.Max(0.01f, detailButtonMoveDuration))
                .SetEase(moveEase));
        }

        if (boxTarget != null)
        {
            sequence
                .AppendInterval(Mathf.Max(0f, boxScaleDelay))
                .Append(boxTarget
                    .DOScale(Vector3.one * Mathf.Max(0.01f, boxScaleTo), Mathf.Max(0.01f, boxScaleDuration))
                    .SetEase(boxScaleEase))
                .AppendCallback(() => SetBoxOpened(category, true));
        }

        activeTween = sequence
            .OnComplete(() =>
            {
                activeTween = null;
                onComplete?.Invoke();
            });
    }

    public void ResetAll()
    {
        activeTween?.Kill(false);
        activeTween = null;
        ResetResultPanel();
        ResetOddPanel(GachaCategory.Weapon);
        ResetOddPanel(GachaCategory.Skill);
        ResetTitle(GachaCategory.Weapon);
        ResetTitle(GachaCategory.Skill);
        ResetButtonsGroup(GachaCategory.Weapon);
        ResetButtonsGroup(GachaCategory.Skill);
        ResetDetailButton(GachaCategory.Weapon);
        ResetDetailButton(GachaCategory.Skill);
        ResetBox(GachaCategory.Weapon);
        ResetBox(GachaCategory.Skill);
    }

    public void ResetTitle(GachaCategory category)
    {
        ResolveReferences();

        if (category == GachaCategory.Skill)
        {
            if (skillTitleRect != null && hasSkillStartPosition)
            {
                skillTitleRect.anchoredPosition = skillTitleStartPosition;
            }

            return;
        }

        if (weaponTitleRect != null && hasWeaponStartPosition)
        {
            weaponTitleRect.anchoredPosition = weaponTitleStartPosition;
        }
    }

    public void ResetButtonsGroup(GachaCategory category)
    {
        ResolveReferences();

        if (category == GachaCategory.Skill)
        {
            if (skillButtonsGroup != null && hasSkillButtonsGroupStartPosition)
            {
                skillButtonsGroup.anchoredPosition = skillButtonsGroupStartPosition;
            }

            return;
        }

        if (weaponButtonsGroup != null && hasWeaponButtonsGroupStartPosition)
        {
            weaponButtonsGroup.anchoredPosition = weaponButtonsGroupStartPosition;
        }
    }

    public void ResetDetailButton(GachaCategory category)
    {
        ResolveReferences();

        if (category == GachaCategory.Skill)
        {
            if (skillDetailButton != null && hasSkillDetailButtonStartPosition)
            {
                skillDetailButton.anchoredPosition = skillDetailButtonStartPosition;
            }

            return;
        }

        if (weaponDetailButton != null && hasWeaponDetailButtonStartPosition)
        {
            weaponDetailButton.anchoredPosition = weaponDetailButtonStartPosition;
        }
    }

    public void ResetBox(GachaCategory category)
    {
        ResolveReferences();

        if (category == GachaCategory.Skill)
        {
            if (skillGachaBox != null && hasSkillGachaBoxStartScale)
            {
                skillGachaBox.localScale = skillGachaBoxStartScale;
            }

            SetBoxOpened(GachaCategory.Skill, false);
            return;
        }

        if (weaponGachaBox != null && hasWeaponGachaBoxStartScale)
        {
            weaponGachaBox.localScale = weaponGachaBoxStartScale;
        }

        SetBoxOpened(GachaCategory.Weapon, false);
    }

    public void PlayResultPanel()
    {
        ResolveReferences();
        if (gachaPanelTransform == null)
        {
            return;
        }

        resultPanelTween?.Kill(false);
        Vector3 targetScale = hasGachaPanelStartScale ? gachaPanelStartScale : gachaPanelTransform.localScale;
        gachaPanelTransform.localScale = resultPanelFromScale;
        resultPanelTween = gachaPanelTransform
            .DOScale(targetScale, Mathf.Max(0.01f, resultPanelScaleDuration))
            .SetDelay(Mathf.Max(0f, resultPanelScaleDelay))
            .SetEase(resultPanelScaleEase)
            .OnComplete(() => resultPanelTween = null);
    }

    public void ResetResultPanel()
    {
        ResolveReferences();
        resultPanelTween?.Kill(false);
        resultPanelTween = null;
        if (gachaPanelTransform != null && hasGachaPanelStartScale)
        {
            gachaPanelTransform.localScale = gachaPanelStartScale;
        }
    }

    public void PlayOddPanel(GachaCategory category)
    {
        ResolveReferences();
        RectTransform target = category == GachaCategory.Skill ? skillGachaOddPanel : weaponGachaOddPanel;
        if (target == null)
        {
            return;
        }

        Vector3 targetLocalPosition = GetOddPanelTargetLocalPosition(category, target);
        Tween tween = category == GachaCategory.Skill ? skillGachaOddPanelTween : weaponGachaOddPanelTween;
        tween?.Kill(false);
        target.localPosition = oddPanelFromLocalPosition;
        tween = target
            .DOLocalMove(targetLocalPosition, Mathf.Max(0.01f, oddPanelMoveDuration))
            .SetDelay(Mathf.Max(0f, oddPanelMoveDelay))
            .SetEase(oddPanelMoveEase)
            .OnComplete(() => ClearOddPanelTween(category));

        if (category == GachaCategory.Skill)
        {
            skillGachaOddPanelTween = tween;
            return;
        }

        weaponGachaOddPanelTween = tween;
    }

    public void ResetOddPanel(GachaCategory category)
    {
        ResolveReferences();
        RectTransform target = category == GachaCategory.Skill ? skillGachaOddPanel : weaponGachaOddPanel;
        if (category == GachaCategory.Skill)
        {
            skillGachaOddPanelTween?.Kill(false);
            skillGachaOddPanelTween = null;
            if (target != null && hasSkillGachaOddPanelStartLocalPosition)
            {
                target.localPosition = skillGachaOddPanelStartLocalPosition;
            }

            return;
        }

        weaponGachaOddPanelTween?.Kill(false);
        weaponGachaOddPanelTween = null;
        if (target != null && hasWeaponGachaOddPanelStartLocalPosition)
        {
            target.localPosition = weaponGachaOddPanelStartLocalPosition;
        }
    }

    private void ResolveReferences()
    {
        weaponTitleText ??= FindTitleText("WeaponGacha_Panel");
        skillTitleText ??= FindTitleText("SkillGacha_Panel");
        weaponButtonsGroup ??= FindChildRectTransform("WeaponGacha_Panel", "Buttons_Group");
        skillButtonsGroup ??= FindChildRectTransform("SkillGacha_Panel", "Buttons_Group");
        weaponDetailButton ??= FindChildRectTransform("WeaponGacha_Panel", "WeaponGachaDetail_Button");
        skillDetailButton ??= FindChildRectTransform("SkillGacha_Panel", "SkillGachaDetail_Button");
        weaponGachaBox ??= FindChildRectTransform("WeaponGacha_Panel", "WeaponBoxImage");
        skillGachaBox ??= FindChildRectTransform("SkillGacha_Panel", "SkillBoxImage");
        weaponGachaBoxOpenGlow ??= FindChildGameObject("WeaponGacha_Panel", "weaponGachaBoxOpenGlow");
        skillGachaBoxOpenGlow ??= FindChildGameObject("SkillGacha_Panel", "skillGachaBoxOpenGlow");
        gachaPanelTransform ??= FindRectTransformByName("GachaResult");
        gachaPanelTransform ??= FindRectTransformByName("GachaResult Panel");
        gachaResultConfirmButton ??= FindChildComponent<Button>("GachaResult Panel", "Close Button");
        weaponGachaOddPanel ??= FindRectTransformByName("WeaponGachaOddDetailPanel");
        skillGachaOddPanel ??= FindRectTransformByName("SkillGachaOddDetailPanel");
        weaponGachaBoxImage ??= weaponGachaBox != null ? weaponGachaBox.GetComponent<Image>() : null;
        skillGachaBoxImage ??= skillGachaBox != null ? skillGachaBox.GetComponent<Image>() : null;

        ResolveTitleRect(weaponTitleText, ref weaponTitleRect, ref weaponTitleStartPosition, ref hasWeaponStartPosition);
        ResolveTitleRect(skillTitleText, ref skillTitleRect, ref skillTitleStartPosition, ref hasSkillStartPosition);
        ResolveRect(
            weaponButtonsGroup,
            ref weaponButtonsGroupStartPosition,
            ref hasWeaponButtonsGroupStartPosition);
        ResolveRect(
            skillButtonsGroup,
            ref skillButtonsGroupStartPosition,
            ref hasSkillButtonsGroupStartPosition);
        ResolveRect(
            weaponDetailButton,
            ref weaponDetailButtonStartPosition,
            ref hasWeaponDetailButtonStartPosition);
        ResolveRect(
            skillDetailButton,
            ref skillDetailButtonStartPosition,
            ref hasSkillDetailButtonStartPosition);
        ResolveScale(
            weaponGachaBox,
            ref weaponGachaBoxStartScale,
            ref hasWeaponGachaBoxStartScale);
        ResolveScale(
            skillGachaBox,
            ref skillGachaBoxStartScale,
            ref hasSkillGachaBoxStartScale);
        ResolveScale(
            gachaPanelTransform,
            ref gachaPanelStartScale,
            ref hasGachaPanelStartScale);
        ResolveLocalPosition(
            weaponGachaOddPanel,
            ref weaponGachaOddPanelStartLocalPosition,
            ref hasWeaponGachaOddPanelStartLocalPosition);
        ResolveLocalPosition(
            skillGachaOddPanel,
            ref skillGachaOddPanelStartLocalPosition,
            ref hasSkillGachaOddPanelStartLocalPosition);
        weaponInitialBoxSprite ??= weaponGachaBoxImage != null ? weaponGachaBoxImage.sprite : null;
        skillInitialBoxSprite ??= skillGachaBoxImage != null ? skillGachaBoxImage.sprite : null;
    }

    private static void ResolveTitleRect(
        TMP_Text titleText,
        ref RectTransform titleRect,
        ref Vector2 startPosition,
        ref bool hasStartPosition)
    {
        if (titleText == null)
        {
            return;
        }

        titleRect ??= titleText.transform as RectTransform;
        if (titleRect != null && !hasStartPosition)
        {
            startPosition = titleRect.anchoredPosition;
            hasStartPosition = true;
        }
    }

    private static void ResolveRect(
        RectTransform target,
        ref Vector2 startPosition,
        ref bool hasStartPosition)
    {
        if (target != null && !hasStartPosition)
        {
            startPosition = target.anchoredPosition;
            hasStartPosition = true;
        }
    }

    private static void ResolveScale(
        RectTransform target,
        ref Vector3 startScale,
        ref bool hasStartScale)
    {
        if (target != null && !hasStartScale)
        {
            startScale = target.localScale;
            hasStartScale = true;
        }
    }

    private static void ResolveLocalPosition(
        RectTransform target,
        ref Vector3 startLocalPosition,
        ref bool hasStartLocalPosition)
    {
        if (target != null && !hasStartLocalPosition)
        {
            startLocalPosition = target.localPosition;
            hasStartLocalPosition = true;
        }
    }

    private void PlayOddPanelTweensOnOpen()
    {
        // Update 폴링은 오드패널 두 개의 활성 전환만 감지한다. 참조가 이미 잡혀 있으면
        // 매 프레임 ResolveReferences(미해결 필드마다 전체 힙 스캔)를 돌 필요가 없다.
        // 미해결일 때만 간격을 두고 재해석해, 씬에 패널이 없을 때의 프레임당 다중 힙 스캔을 막는다.
        if ((weaponGachaOddPanel == null || skillGachaOddPanel == null)
            && Time.unscaledTime >= nextOddPanelResolveTime)
        {
            nextOddPanelResolveTime = Time.unscaledTime + OddPanelResolveInterval;
            ResolveReferences();
        }

        bool isWeaponOddPanelActive = weaponGachaOddPanel != null && weaponGachaOddPanel.gameObject.activeInHierarchy;
        bool isSkillOddPanelActive = skillGachaOddPanel != null && skillGachaOddPanel.gameObject.activeInHierarchy;

        if (isWeaponOddPanelActive && !wasWeaponGachaOddPanelActive)
        {
            PlayOddPanel(GachaCategory.Weapon);
        }

        if (isSkillOddPanelActive && !wasSkillGachaOddPanelActive)
        {
            PlayOddPanel(GachaCategory.Skill);
        }

        wasWeaponGachaOddPanelActive = isWeaponOddPanelActive;
        wasSkillGachaOddPanelActive = isSkillOddPanelActive;
    }

    private void CacheOddPanelActiveStates()
    {
        wasWeaponGachaOddPanelActive = weaponGachaOddPanel != null && weaponGachaOddPanel.gameObject.activeInHierarchy;
        wasSkillGachaOddPanelActive = skillGachaOddPanel != null && skillGachaOddPanel.gameObject.activeInHierarchy;
    }

    private Vector3 GetOddPanelTargetLocalPosition(GachaCategory category, RectTransform fallbackTarget)
    {
        if (category == GachaCategory.Skill)
        {
            return hasSkillGachaOddPanelStartLocalPosition
                ? skillGachaOddPanelStartLocalPosition
                : fallbackTarget.localPosition;
        }

        return hasWeaponGachaOddPanelStartLocalPosition
            ? weaponGachaOddPanelStartLocalPosition
            : fallbackTarget.localPosition;
    }

    private void ClearOddPanelTween(GachaCategory category)
    {
        if (category == GachaCategory.Skill)
        {
            skillGachaOddPanelTween = null;
            return;
        }

        weaponGachaOddPanelTween = null;
    }

    private void SetBoxOpened(GachaCategory category, bool opened)
    {
        if (category == GachaCategory.Skill)
        {
            SetActive(skillGachaBoxOpenGlow, opened);
            SetBoxSprite(skillGachaBoxImage, opened ? openBox : closeBox ?? skillInitialBoxSprite);
            return;
        }

        SetActive(weaponGachaBoxOpenGlow, opened);
        SetBoxSprite(weaponGachaBoxImage, opened ? openBox : closeBox ?? weaponInitialBoxSprite);
    }

    private static void SetBoxSprite(Image image, Sprite sprite)
    {
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static TMP_Text FindTitleText(string panelName)
    {
        Transform panel = FindTransformByName(panelName);
        if (panel == null)
        {
            return null;
        }

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Title")
            {
                return text;
            }
        }

        return null;
    }

    private static RectTransform FindChildRectTransform(string panelName, string childName)
    {
        Transform panel = FindTransformByName(panelName);
        if (panel == null)
        {
            return null;
        }

        foreach (RectTransform rectTransform in panel.GetComponentsInChildren<RectTransform>(true))
        {
            if (rectTransform.name == childName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static GameObject FindChildGameObject(string panelName, string childName)
    {
        Transform child = FindChildTransform(panelName, childName);
        return child != null ? child.gameObject : null;
    }

    private static T FindChildComponent<T>(string panelName, string childName) where T : Component
    {
        Transform child = FindChildTransform(panelName, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChildTransform(string panelName, string childName)
    {
        Transform panel = FindTransformByName(panelName);
        if (panel == null)
        {
            return null;
        }

        foreach (Transform child in panel.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindTransformByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            if (target != null && target.name == objectName && target.gameObject.scene.IsValid())
            {
                return target;
            }
        }

        return null;
    }

    private static RectTransform FindRectTransformByName(string objectName)
    {
        return FindTransformByName(objectName) as RectTransform;
    }
}
