using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SkillHangerPanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerController player;
    [SerializeField] private InventoryFacility inventory;

    [Header("Buttons")]
    [SerializeField] private Button[] skillButtons;
    [SerializeField] private Image[] skillIcons;
    [SerializeField] private Button changeSkillButton;

    [Header("Skill Info SubPanel")]
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private Image skillIconImage;
    [SerializeField] private TMP_Text skillLevelText;
    [SerializeField] private TMP_Text skillCooldownText;
    [SerializeField] private TMP_Text skillDescriptionText;

    [Header("LoadoutPanel")]
    [SerializeField] private SkillLoadoutSelectionPanel skillLoadoutSelectionPanel;

    private PlayerSkillConfig[] slotSkills = Array.Empty<PlayerSkillConfig>();
    private UnityAction[] skillButtonActions = Array.Empty<UnityAction>();
    private PlayerSkillConfig selectedSkill;
    private int selectedSkillSlotIndex = -1;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeInventory();
        SubscribeSkillButtons();
        changeSkillButton?.onClick.AddListener(OpenSkillLoadoutSelectionPanel);
        RefreshSkillSlots();
    }

    private void OnDisable()
    {
        changeSkillButton?.onClick.RemoveListener(OpenSkillLoadoutSelectionPanel);
        UnsubscribeSkillButtons();
        UnsubscribeInventory();
    }
    
    // 플레이어가 소지한 스킬갯수(최대 4개)만큼 동기화한다.
    // 만약 기본스킬을(스킬 1, 2칸) 제외한 다른 스킬이 없다면 3, 4칸 버튼은 비활성화 시키고 아이콘은 아예 보이기 않게 처리.
    private void RefreshSkillSlots()
    {
        ResolveReferences();

        int slotCount = skillButtons != null ? skillButtons.Length : 0;
        EnsureSlotCaches(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            PlayerSkillConfig skill = player != null ? player.GetEquippedSkill(i) : null;
            bool hasSkill = skill != null && (inventory == null || inventory.ContainsSkill(skill));
            slotSkills[i] = hasSkill ? skill : null;

            SetButtonInteractable(skillButtons[i], hasSkill);
            SetSkillIcon(GetSkillIcon(i), hasSkill ? skill.Icon : null);
        }

        if (selectedSkill != null && !IsVisibleSlotSkill(selectedSkill))
        {
            selectedSkill = null;
            selectedSkillSlotIndex = -1;
        }

        RefreshSelectedSkillInfo();
    }

    private void SelectSkillSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotSkills.Length)
        {
            return;
        }

        selectedSkill = slotSkills[slotIndex];
        selectedSkillSlotIndex = selectedSkill != null ? slotIndex : -1;
        RefreshSelectedSkillInfo();
    }

    private void RefreshSelectedSkillInfo()
    {
        if (selectedSkill == null)
        {
            SetButtonInteractable(changeSkillButton, false);
            SetSkillIcon(skillIconImage, null);
            SetText(skillNameText, "스킬이 선택되지 않음.");
            SetText(skillLevelText, "--");
            SetText(skillCooldownText, "--");
            SetText(skillDescriptionText, "우선 교체할 스킬을 선택해야합니다.");
            return;
        }

        int level = GetSkillLevel(selectedSkill);
        SetButtonInteractable(changeSkillButton, true);
        SetSkillIcon(skillIconImage, selectedSkill.Icon);
        SetText(skillNameText, selectedSkill.DisplayName);
        SetText(skillLevelText, $"Lv.{level}/{selectedSkill.MaxLevel}");
        SetText(skillCooldownText, $"{selectedSkill.GetCooldown(level):0.##}초");
        SetText(skillDescriptionText, selectedSkill.Description);
    }

    private void OpenSkillLoadoutSelectionPanel()
    {
        if (selectedSkill == null || selectedSkillSlotIndex < 0)
        {
            return;
        }

        skillLoadoutSelectionPanel?.Open(selectedSkillSlotIndex, RefreshSkillSlots);
    }

    private int GetSkillLevel(PlayerSkillConfig skill)
    {
        if (skill == null)
        {
            return 0;
        }

        int level = inventory != null ? inventory.GetSkillLevel(skill) : player != null ? player.GetSkillLevel(skill) : 1;
        return Mathf.Clamp(level, 1, skill.MaxLevel);
    }

    private bool IsVisibleSlotSkill(PlayerSkillConfig skill)
    {
        for (int i = 0; i < slotSkills.Length; i++)
        {
            if (slotSkills[i] == skill)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        inventory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : InventoryFacility.FindAny();
    }

    private void SubscribeSkillButtons()
    {
        UnsubscribeSkillButtons();

        int slotCount = skillButtons != null ? skillButtons.Length : 0;
        EnsureSlotCaches(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            int slotIndex = i;
            skillButtonActions[i] = () => SelectSkillSlot(slotIndex);
            skillButtons[i]?.onClick.AddListener(skillButtonActions[i]);
        }
    }

    private void UnsubscribeSkillButtons()
    {
        if (skillButtons == null || skillButtonActions == null)
        {
            return;
        }

        int count = Mathf.Min(skillButtons.Length, skillButtonActions.Length);
        for (int i = 0; i < count; i++)
        {
            if (skillButtonActions[i] != null)
            {
                skillButtons[i]?.onClick.RemoveListener(skillButtonActions[i]);
                skillButtonActions[i] = null;
            }
        }
    }

    private void SubscribeInventory()
    {
        inventory?.OnCollectionProgressChanged.AddListener(RefreshSkillSlots);
    }

    private void UnsubscribeInventory()
    {
        inventory?.OnCollectionProgressChanged.RemoveListener(RefreshSkillSlots);
    }

    private void EnsureSlotCaches(int slotCount)
    {
        if (slotSkills.Length != slotCount)
        {
            slotSkills = new PlayerSkillConfig[slotCount];
        }

        if (skillButtonActions.Length != slotCount)
        {
            skillButtonActions = new UnityAction[slotCount];
        }
    }

    // PlayerSkillConfig에 있는 Sprite icon과 동기화
    private Image GetSkillIcon(int index)
    {
        return skillIcons != null && index >= 0 && index < skillIcons.Length
            ? skillIcons[index]
            : null;
    }

    private static void SetSkillIcon(Image target, Sprite icon)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = icon;
        target.enabled = icon != null;
        target.preserveAspect = true;
    }

    private static void SetButtonInteractable(Button target, bool interactable)
    {
        if (target != null)
        {
            target.interactable = interactable;
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
