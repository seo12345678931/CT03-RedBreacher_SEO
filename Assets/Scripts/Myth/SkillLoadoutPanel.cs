using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SkillLoadoutPanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private PlayerController player;
    [SerializeField] private InventoryFacility inventory;
    [SerializeField] private SkillHangerFacility skillHanger;

    [Header("Skill List")]
    [SerializeField] private RectTransform skillContentRoot;
    [SerializeField] private PlayerLoadoutOptionButton skillButtonPrefab;

    [Header("Equipped Slots")]
    [SerializeField] private TMP_Text[] equippedSlotTexts = new TMP_Text[3];
    [SerializeField] private Button[] slotButtons = new Button[4];
    [SerializeField] private RawImage[] slotSkillIcons = new RawImage[4];
    [SerializeField] private GameObject[] slotLockedObjects = new GameObject[4];

    [Header("Detail")]
    [SerializeField] private RawImage detailSkillIcon;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text skillLevelText;
    [SerializeField] private TMP_Text skillCooldownText;
    [SerializeField] private TMP_Text skillDescriptionText;

    [Header("Commands")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button changeSkillButton;
    [SerializeField] private Button unequipSelectedButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text facilityLevelText;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text upgradeCostText;
    [SerializeField] private TMP_Text upgradeConditionText;
    [SerializeField] private TMP_Text upgradeRemainingText;
    [SerializeField] private Image coinIcon;
    [SerializeField] private Image upgradeProgressFill;
    [SerializeField] private PlayerLoadoutSelectionPanel loadoutSelectionPanel;
    [SerializeField] private Button[] equipSlotButtons = new Button[3];
    [SerializeField] private Button[] unequipSlotButtons = new Button[3];

    [Header("Tween Feedback")]
    [SerializeField] private float slotSelectPunchScale = 0.08f;
    [SerializeField] private float slotSelectTweenDuration = 0.16f;
    [SerializeField] private float equipChangedPunchScale = 0.12f;
    [SerializeField] private float equipChangedTweenDuration = 0.18f;
    [SerializeField] private float invalidSlotShakeDuration = 0.18f;
    [SerializeField] private Vector2 invalidSlotShakeStrength = new Vector2(12f, 0f);

    private readonly List<PlayerLoadoutOptionButton> spawnedButtons = new List<PlayerLoadoutOptionButton>();
    private UnityAction[] slotSelectActions = new UnityAction[4];
    private readonly UnityAction[] equipActions = new UnityAction[3];
    private readonly UnityAction[] unequipActions = new UnityAction[3];
    private PlayerSkillConfig selectedSkill;
    private int selectedSlotIndex;
    private SkillHangerFacility subscribedFacility;
    private float observedUpgradeDuration;

    private void OnEnable()
    {
        ResolveReferences();
        SetMainCloseButtonVisible(true);
        SubscribeButtons();
        SubscribeInventory();
        SubscribeFactory();
        Rebuild();
    }

    private void OnDisable()
    {
        SetMainCloseButtonVisible(true);
        UnsubscribeButtons();
        UnsubscribeInventory();
        UnsubscribeFactory();
        KillFeedbackTweens();
        ClearButtons();
    }

    private void Update()
    {
        RefreshUpgradeStatus();
    }

    public void EquipSelectedToSlot(int slotIndex)
    {
        if (player != null && selectedSkill != null && player.EquipSkill(slotIndex, selectedSkill))
        {
            selectedSlotIndex = slotIndex;
            Refresh();
            PlayEquipChangedFeedback(slotIndex);
        }
    }

    public void UnequipSlot(int slotIndex)
    {
        if (player != null && player.UnequipSkill(slotIndex))
        {
            if (selectedSlotIndex == slotIndex)
            {
                selectedSkill = null;
            }

            Refresh();
            PlayEquipChangedFeedback(slotIndex);
        }
    }

    public void UnequipSelectedSlot()
    {
        // 전역 장착 해제 버튼은 현재 선택된 슬롯만 해제한다.
        UnequipSlot(selectedSlotIndex);
    }

    public void OpenSkillSelectionPanel()
    {
        ResolveReferences();
        if (player != null && !player.IsSkillSlotUnlocked(selectedSlotIndex))
        {
            PlayInvalidSlotFeedback(selectedSlotIndex);
            return;
        }

        if (loadoutSelectionPanel == null)
        {
            return;
        }

        SetMainCloseButtonVisible(false);
        loadoutSelectionPanel.OpenSkillsForSelection(
            EquipSelectedSkillToSelectedSlot,
            GetSelectedSlotSkill(),
            () => SetMainCloseButtonVisible(true));
    }

    public void UpgradeSkillHanger()
    {
        ResolveReferences();
        baseCampManager?.UpgradeSkillHanger();
        Refresh();
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return;
        }

        selectedSlotIndex = slotIndex;
        if (player != null && !player.IsSkillSlotUnlocked(selectedSlotIndex))
        {
            Refresh();
            PlayInvalidSlotFeedback(selectedSlotIndex);
            return;
        }

        selectedSkill = GetSelectedSlotSkill();
        Refresh();
        PlaySlotSelectedFeedback(selectedSlotIndex);
    }

    private void Rebuild()
    {
        ClearButtons();
        if (inventory == null)
        {
            Refresh();
            return;
        }

        for (int i = 0; i < inventory.SkillConfigs.Count; i++)
        {
            PlayerSkillConfig skill = inventory.SkillConfigs[i];
            if (skill == null || skillContentRoot == null || skillButtonPrefab == null)
            {
                continue;
            }

            PlayerSkillConfig capturedSkill = skill;
            PlayerLoadoutOptionButton option = Instantiate(skillButtonPrefab, skillContentRoot);
            option.gameObject.SetActive(true);
            option.Bind(
                $"{skill.DisplayName} Lv.{inventory.GetSkillLevel(skill)}",
                "스킬",
                BuildSkillSummary(skill),
                skill == selectedSkill,
                () => SelectSkill(capturedSkill),
                null,
                null,
                null,
                skill,
                GetSkillLevel(skill));
            spawnedButtons.Add(option);
        }

        Refresh();
    }

    private void SelectSkill(PlayerSkillConfig skill)
    {
        selectedSkill = skill;
        Refresh();
    }

    private void EquipSelectedSkillToSelectedSlot(PlayerSkillConfig skill)
    {
        selectedSkill = skill;
        EquipSelectedToSlot(selectedSlotIndex);
    }

    private void Refresh()
    {
        EnsureSelectedSlotIndex();
        RefreshSkillOptionSelection();
        RefreshUpgradeStatus();

        for (int i = 0; i < equippedSlotTexts.Length; i++)
        {
            bool unlocked = player == null || player.IsSkillSlotUnlocked(i);
            PlayerSkillConfig equipped = player != null ? player.GetEquippedSkill(i) : null;
            SetText(
                equippedSlotTexts[i],
                !unlocked
                    ? $"스킬 격납고 Lv.{player.GetSkillSlotRequiredSkillHangerLevel(i)} 해금"
                    : equipped != null
                    ? $"{equipped.DisplayName} Lv.{GetSkillLevel(equipped)}"
                    : "비어 있음");
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool unlocked = player == null || player.IsSkillSlotUnlocked(i);
            PlayerSkillConfig equipped = player != null ? player.GetEquippedSkill(i) : null;
            SetInteractable(slotButtons[i], unlocked);
            SetSkillPreview(slotSkillIcons, i, equipped);
            SetActive(slotLockedObjects, i, !unlocked);
        }

        for (int i = 0; i < equipSlotButtons.Length; i++)
        {
            if (IsSlotButton(equipSlotButtons[i]))
            {
                continue;
            }

            bool unlocked = player == null || player.IsSkillSlotUnlocked(i);
            SetInteractable(equipSlotButtons[i], unlocked && selectedSkill != null);
        }

        for (int i = 0; i < unequipSlotButtons.Length; i++)
        {
            bool unlocked = player == null || player.IsSkillSlotUnlocked(i);
            SetInteractable(unequipSlotButtons[i], unlocked && player != null && player.GetEquippedSkill(i) != null);
        }

        PlayerSkillConfig detailSkill = GetSelectedSlotSkill();
        bool selectedSlotUnlocked = player == null || player.IsSkillSlotUnlocked(selectedSlotIndex);
        SetInteractable(unequipSelectedButton, selectedSlotUnlocked && detailSkill != null);
        SetSkillPreview(detailSkillIcon, detailSkill);
        SetText(
            skillNameText,
            !selectedSlotUnlocked && player != null
                ? $"스킬 격납고 Lv.{player.GetSkillSlotRequiredSkillHangerLevel(selectedSlotIndex)} 해금"
                : detailSkill != null
                ? detailSkill.DisplayName
                : "스킬 없음");
        SetText(skillLevelText, selectedSlotUnlocked && detailSkill != null ? $"Lv.{GetSkillLevel(detailSkill)}" : "--");

        if (!selectedSlotUnlocked || detailSkill == null)
        {
            SetText(skillCooldownText, "--");
            SetText(
                skillDescriptionText,
                selectedSlotUnlocked
                    ? "'교체하기' 버튼을 눌러\n 원하는 스킬을 선택하세요."
                    : "우선 교체할 스킬을 선택하세요.");
            return;
        }

        SetText(skillCooldownText, $"{detailSkill.GetCooldown(GetSkillLevel(detailSkill)):0.##}");
        SetText(skillDescriptionText, detailSkill.Description);
    }

    private int GetSkillLevel(PlayerSkillConfig skill)
    {
        return inventory != null ? Mathf.Max(1, inventory.GetSkillLevel(skill)) : 1;
    }

    private string BuildSkillSummary(PlayerSkillConfig skill)
    {
        if (skill == null || inventory == null)
        {
            return string.Empty;
        }

        int required = inventory.GetRequiredDuplicates(skill);
        return required > 0
            ? $"중복 {inventory.GetDuplicateProgress(skill)} / {required}"
            : "MAX";
    }

    private void RefreshSkillOptionSelection()
    {
        if (inventory == null)
        {
            return;
        }

        int optionIndex = 0;
        for (int i = 0; i < inventory.SkillConfigs.Count && optionIndex < spawnedButtons.Count; i++)
        {
            PlayerSkillConfig skill = inventory.SkillConfigs[i];
            if (skill == null)
            {
                continue;
            }

            spawnedButtons[optionIndex]?.SetSelected(skill == selectedSkill);
            optionIndex++;
        }
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance
            : FindFirstObjectByType<BaseCampManager>(FindObjectsInactive.Include);
        player ??= FindFirstObjectByType<PlayerController>();
        inventory ??= baseCampManager != null
            ? baseCampManager.Inventory
            : InventoryFacility.FindAny();
        skillHanger ??= baseCampManager != null
            ? baseCampManager.SkillHanger
            : FindFirstObjectByType<SkillHangerFacility>(FindObjectsInactive.Include);
        // 스킬 격납고 프리팹의 교체 버튼/선택 패널을 코드에서 한 번만 연결한다.
        changeSkillButton ??= FindChildComponentByName<Button>(transform, "ChangeSkill Button");
        changeSkillButton ??= FindChildComponentByName<Button>(transform, "Equip_Button");
        unequipSelectedButton ??= FindChildComponentByName<Button>(transform, "Unequip_Button");
        unequipSelectedButton ??= FindChildComponentByName<Button>(transform, "Unequip Button");
        upgradeButton ??= FindChildComponentByName<Button>(transform, "Upgrade_Button");
        upgradeButton ??= FindChildComponentByName<Button>(transform, "UpgradeButton");
        upgradeButton ??= FindChildComponentByName<Button>(transform, "Upgrade Button");
        loadoutSelectionPanel ??= FindChildComponentByName<PlayerLoadoutSelectionPanel>(transform, "LoadoutSelectionPanel_Skill");
        loadoutSelectionPanel ??= FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        detailSkillIcon ??= FindChildComponentByName<RawImage>(FindChildTransformByName(transform, "Detail_SkillIcon"), "Skill_Icon");
        detailSkillIcon ??= FindChildComponentByName<RawImage>(transform, "Detail_SkillIcon");
        ResolveSlotButtons();
    }

    private void SubscribeButtons()
    {
        closeButton?.onClick.AddListener(Close);
        changeSkillButton?.onClick.AddListener(OpenSkillSelectionPanel);
        unequipSelectedButton?.onClick.AddListener(UnequipSelectedSlot);
        upgradeButton?.onClick.AddListener(UpgradeSkillHanger);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i;
            slotSelectActions[i] = () => SelectSlot(slotIndex);
            slotButtons[i]?.onClick.AddListener(slotSelectActions[i]);
        }

        for (int i = 0; i < Mathf.Min(equipSlotButtons.Length, equipActions.Length); i++)
        {
            int slotIndex = i;
            equipActions[i] = () => EquipSelectedToSlot(slotIndex);
            equipSlotButtons[i]?.onClick.AddListener(equipActions[i]);
        }

        for (int i = 0; i < Mathf.Min(unequipSlotButtons.Length, unequipActions.Length); i++)
        {
            int slotIndex = i;
            unequipActions[i] = () => UnequipSlot(slotIndex);
            unequipSlotButtons[i]?.onClick.AddListener(unequipActions[i]);
        }
    }

    private void UnsubscribeButtons()
    {
        closeButton?.onClick.RemoveListener(Close);
        changeSkillButton?.onClick.RemoveListener(OpenSkillSelectionPanel);
        unequipSelectedButton?.onClick.RemoveListener(UnequipSelectedSlot);
        upgradeButton?.onClick.RemoveListener(UpgradeSkillHanger);
        for (int i = 0; i < Mathf.Min(slotButtons.Length, slotSelectActions.Length); i++)
        {
            if (slotSelectActions[i] != null)
            {
                slotButtons[i]?.onClick.RemoveListener(slotSelectActions[i]);
            }
        }

        for (int i = 0; i < Mathf.Min(equipSlotButtons.Length, equipActions.Length); i++)
        {
            if (equipActions[i] != null)
            {
                equipSlotButtons[i]?.onClick.RemoveListener(equipActions[i]);
            }
        }

        for (int i = 0; i < Mathf.Min(unequipSlotButtons.Length, unequipActions.Length); i++)
        {
            if (unequipActions[i] != null)
            {
                unequipSlotButtons[i]?.onClick.RemoveListener(unequipActions[i]);
            }
        }
    }

    private void SubscribeInventory()
    {
        inventory?.OnCollectionProgressChanged.AddListener(Rebuild);
    }

    private void UnsubscribeInventory()
    {
        inventory?.OnCollectionProgressChanged.RemoveListener(Rebuild);
    }

    private void SubscribeFactory()
    {
        if (skillHanger == null || subscribedFacility == skillHanger)
        {
            return;
        }

        UnsubscribeFactory();
        subscribedFacility = skillHanger;
        subscribedFacility.OnLevelChanged.AddListener(HandleFactoryLevelChanged);
        subscribedFacility.OnUpgradeStarted.AddListener(HandleFactoryUpgradeStarted);
        subscribedFacility.OnUpgradeCompleted.AddListener(HandleFactoryUpgradeCompleted);
    }

    private void UnsubscribeFactory()
    {
        if (subscribedFacility == null)
        {
            return;
        }

        subscribedFacility.OnLevelChanged.RemoveListener(HandleFactoryLevelChanged);
        subscribedFacility.OnUpgradeStarted.RemoveListener(HandleFactoryUpgradeStarted);
        subscribedFacility.OnUpgradeCompleted.RemoveListener(HandleFactoryUpgradeCompleted);
        subscribedFacility = null;
    }

    private void HandleFactoryLevelChanged(int level)
    {
        EnsureSelectedSlotIndex();
        Refresh();
    }

    private void HandleFactoryUpgradeStarted()
    {
        Refresh();
    }

    private void HandleFactoryUpgradeCompleted()
    {
        EnsureSelectedSlotIndex();
        Refresh();
    }

    private void Close()
    {
        // 기지 시설 패널 닫힘 연출이 적용되도록 BaseCampManager 경로를 우선 사용한다.
        ResolveReferences();
        if (baseCampManager != null)
        {
            baseCampManager.ClosePanel(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }

    private void ClearButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                Destroy(spawnedButtons[i].gameObject);
            }
        }

        spawnedButtons.Clear();
    }

    private void PlaySlotSelectedFeedback(int slotIndex)
    {
        GetSlotRect(slotIndex).Punch(slotSelectPunchScale, slotSelectTweenDuration);
    }

    private void PlayInvalidSlotFeedback(int slotIndex)
    {
        // 잠긴 슬롯은 위치 흔들림으로 클릭 불가 상태를 짧게 알려준다.
        GetSlotRect(slotIndex).ShakeX(invalidSlotShakeStrength, invalidSlotShakeDuration);
    }

    private void PlayEquipChangedFeedback(int slotIndex)
    {
        Transform iconTransform = GetSlotIconTransform(slotIndex);
        if (iconTransform != null && iconTransform.gameObject.activeInHierarchy)
        {
            iconTransform.Punch(equipChangedPunchScale, equipChangedTweenDuration);
            return;
        }

        GetSlotRect(slotIndex).Punch(equipChangedPunchScale, equipChangedTweenDuration);
    }

    private void KillFeedbackTweens()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            GetSlotRect(i).Stop();
            GetSlotIconTransform(i).Stop();
        }
    }

    private RectTransform GetSlotRect(int slotIndex)
    {
        if (slotButtons == null || slotIndex < 0 || slotIndex >= slotButtons.Length || slotButtons[slotIndex] == null)
        {
            return null;
        }

        return slotButtons[slotIndex].transform as RectTransform;
    }

    private Transform GetSlotIconTransform(int slotIndex)
    {
        if (slotSkillIcons == null || slotIndex < 0 || slotIndex >= slotSkillIcons.Length || slotSkillIcons[slotIndex] == null)
        {
            return null;
        }

        return slotSkillIcons[slotIndex].transform;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetInteractable(Button target, bool interactable)
    {
        if (target != null)
        {
            target.interactable = interactable;
        }
    }

    private bool IsSlotButton(Button button)
    {
        if (button == null || slotButtons == null)
        {
            return false;
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == button)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshUpgradeStatus()
    {
        ResolveReferences();
        if (skillHanger == null)
        {
            SetText(facilityLevelText, string.Empty);
            SetText(upgradeConditionText, "스킬 격납고 시설 참조 없음");
            SetUpgradeRemainingText(upgradeRemainingText, false, 0f);
            SetActive(upgradeText != null ? upgradeText.gameObject : null, true);
            SetActive(upgradeCostText != null ? upgradeCostText.gameObject : null, true);
            SetActive(upgradeConditionText != null ? upgradeConditionText.gameObject : null, true);
            SetActive(coinIcon != null ? coinIcon.gameObject : null, false);
            BaseCampUpgradeStatus.SetUpgradeProgress(upgradeProgressFill, null, ref observedUpgradeDuration);
            SetInteractable(upgradeButton, false);
            return;
        }

        SetText(facilityLevelText, $"Lv.{skillHanger.Level}");

        bool canShowCost = !skillHanger.IsUpgrading && skillHanger.Level < skillHanger.MaxLevel;
        BaseCampUpgradeButtonText.Set(
            upgradeText,
            upgradeCostText,
            skillHanger.Level >= skillHanger.MaxLevel ? "최대 레벨" : "업그레이드",
            skillHanger.UpgradeCost,
            canShowCost);
        if (skillHanger.Level >= skillHanger.MaxLevel)
        {
            SetText(upgradeCostText, "--");
        }

        if (baseCampManager != null)
        {
            int researchLabLevel = baseCampManager.CommandCenter != null
                ? baseCampManager.CommandCenter.Level
                : 1;
            string conditionText = BaseCampUpgradeStatus.BuildConditionText(
                skillHanger,
                baseCampManager.Credits,
                baseCampManager.CommanderLevel,
                researchLabLevel);
            SetText(upgradeConditionText, conditionText);
            SetInteractable(upgradeButton, skillHanger.CanStartUpgrade(
                baseCampManager.Credits,
                baseCampManager.CommanderLevel,
                researchLabLevel));
        }
        else
        {
            SetText(upgradeConditionText, "BaseCampManager 참조 없음");
            SetInteractable(upgradeButton, false);
        }

        bool isUpgrading = skillHanger.IsUpgrading;
        SetUpgradeRemainingText(upgradeRemainingText, isUpgrading, skillHanger.UpgradeRemainingSeconds);
        SetActive(upgradeText != null ? upgradeText.gameObject : null, !isUpgrading);
        SetActive(upgradeCostText != null ? upgradeCostText.gameObject : null, !isUpgrading);
        SetActive(upgradeConditionText != null ? upgradeConditionText.gameObject : null, !isUpgrading);
        SetActive(coinIcon != null ? coinIcon.gameObject : null, !isUpgrading);

        BaseCampUpgradeStatus.SetUpgradeProgress(
            upgradeProgressFill,
            skillHanger,
            ref observedUpgradeDuration);
    }

    private void ResolveSlotButtons()
    {
        if (slotLockedObjects == null || slotLockedObjects.Length != slotButtons.Length)
        {
            slotLockedObjects = new GameObject[slotButtons.Length];
        }

        if (slotSkillIcons == null || slotSkillIcons.Length != slotButtons.Length)
        {
            slotSkillIcons = new RawImage[slotButtons.Length];
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            slotButtons[i] ??= FindChildComponentByName<Button>(transform, $"SkillButton_{i + 1}");
            // 슬롯 버튼 안의 SkillIcon 이미지를 현재 장착 스킬 아이콘으로 동기화한다.
            slotSkillIcons[i] ??= FindChildComponentByName<RawImage>(
                slotButtons[i] != null ? slotButtons[i].transform : null,
                "SkillIcon");
            slotLockedObjects[i] ??= FindChildTransformByName(
                slotButtons[i] != null ? slotButtons[i].transform : null,
                "locked")?.gameObject;
        }

        if (slotSelectActions == null || slotSelectActions.Length != slotButtons.Length)
        {
            slotSelectActions = new UnityAction[slotButtons.Length];
        }
    }

    private void EnsureSelectedSlotIndex()
    {
        if (player == null || player.IsSkillSlotUnlocked(selectedSlotIndex))
        {
            return;
        }

        for (int i = 0; i < player.SkillSlotCount; i++)
        {
            if (player.IsSkillSlotUnlocked(i))
            {
                selectedSlotIndex = i;
                return;
            }
        }
    }

    private PlayerSkillConfig GetSelectedSlotSkill()
    {
        return player != null ? player.GetEquippedSkill(selectedSlotIndex) : null;
    }

    private void SetSkillPreview(RawImage[] targets, int index, PlayerSkillConfig skill)
    {
        if (targets == null || index < 0 || index >= targets.Length)
        {
            return;
        }

        SetSkillPreview(targets[index], skill);
    }

    private void SetSkillPreview(RawImage target, PlayerSkillConfig skill)
    {
        if (target == null)
        {
            return;
        }

        GameObject prefab = GetSkillPreviewPrefab(skill);
        if (prefab == null)
        {
            target.texture = null;
            target.color = Color.clear;
            target.enabled = false;
            target.gameObject.SetActive(false);
            return;
        }

        int skillLevel = GetSkillLevel(skill);
        string cacheKey = $"{skill.GetInstanceID()}_Level{skillLevel}";
        RenderTexture preview = UnitPreviewRenderer.Instance.GetPreview(
            prefab,
            cacheKey,
            instance => ApplySkillPreviewSkin(instance, skill, skillLevel));
        target.texture = preview;
        target.color = preview != null ? Color.white : Color.clear;
        target.enabled = preview != null;
        target.gameObject.SetActive(preview != null);
    }

    // 스킬 프리팹을 수집 레벨에 따라 색상을 다르게 표시
    private static void ApplySkillPreviewSkin(GameObject instance, PlayerSkillConfig skill, int skillLevel)
    {
        if (instance == null || skill == null)
        {
            return;
        }

        SkinEditor_SkillLevel[] skinEditors = instance.GetComponentsInChildren<SkinEditor_SkillLevel>(true);
        for (int i = 0; i < skinEditors.Length; i++)
        {
            skinEditors[i]?.SetSkill(skill, skillLevel);
        }
    }

    private static GameObject GetSkillPreviewPrefab(PlayerSkillConfig skill)
    {
        if (skill == null)
        {
            return null;
        }

        return skill.SkillType switch
        {
            PlayerSkillType.Bombardment => skill.AirplanePrefab,
            PlayerSkillType.AutoTurret => skill.TurretPrefab,
            PlayerSkillType.MissileTurret => skill.MissileTurretPrefab,
            PlayerSkillType.StealthBomber => skill.StealthBomberPrefab,
            PlayerSkillType.AttackHelicopter => skill.AttackHelicopterPrefab,
            _ => null
        };
    }

    private static void SetActive(GameObject[] targets, int index, bool active)
    {
        if (targets == null || index < 0 || index >= targets.Length || targets[index] == null)
        {
            return;
        }

        targets[index].SetActive(active);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void SetMainCloseButtonVisible(bool visible)
    {
        SetActive(closeButton != null ? closeButton.gameObject : null, visible);
    }

    private static void SetUpgradeRemainingText(TMP_Text target, bool isUpgrading, float remainingSeconds)
    {
        SetText(target, isUpgrading ? $"완료까지 {remainingSeconds:0}초" : string.Empty);
        SetActive(target != null ? target.gameObject : null, isUpgrading);
    }

    private static Transform FindChildTransformByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == childName)
            {
                return component;
            }
        }

        return null;
    }
}
