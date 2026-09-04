using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillLoadoutSelectionPanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerController player;
    [SerializeField] private InventoryFacility inventory;
    
    [Header("Buttons")]
    [SerializeField] private Button equipButton;
    
    [Header("Panel")]
    [SerializeField] private GameObject selectionRoot;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private SkillLoadoutOptionButton optionButtonPrefab;
    
    [Header("Detail")] 
    [SerializeField] private Image detailIconSkill;
    [SerializeField] private TMP_Text detailSkillNameText;
    [SerializeField] private TMP_Text detailSkillLevelText;
    [SerializeField] private TMP_Text detailSkillCooldownText;
    [SerializeField] private TMP_Text detailSkillDescriptionText;

    private readonly List<SkillLoadoutOptionButton> spawnedOptions = new List<SkillLoadoutOptionButton>();
    private PlayerSkillConfig selectedSkill;
    private int targetSlotIndex = -1;
    private Action onEquipped;

    private void OnEnable()
    {
        equipButton?.onClick.AddListener(EquipSelectedSkill);
    }

    private void OnDisable()
    {
        equipButton?.onClick.RemoveListener(EquipSelectedSkill);
        ClearOptions();
    }

    public void Open()
    {
        Open(-1, null);
    }

    public void Open(int slotIndex, Action equippedCallback)
    {
        ResolveReferences();
        targetSlotIndex = slotIndex;
        onEquipped = equippedCallback;
        selectedSkill = null;
        GameObject target = selectionRoot != null ? selectionRoot : gameObject;
        // 비활성 상태에서 트랜지션을 보장해두면 SetActive 시 열기 연출이 재생된다.
        PanelTweenTransition.EnsureOn(target);
        target.SetActive(true);
        RebuildSkillList();
        RefreshSkillDetail(selectedSkill);
    }

    private void RebuildSkillList()
    {
        ClearOptions();
        if (inventory == null)
        {
            return;
        }

        for (int i = 0; i < inventory.SkillConfigs.Count; i++)
        {
            PlayerSkillConfig skill = inventory.SkillConfigs[i];
            if (skill == null || !inventory.ContainsSkill(skill))
            {
                continue;
            }

            SkillLoadoutOptionButton option = CreateOption();
            if (option == null)
            {
                continue;
            }

            PlayerSkillConfig capturedSkill = skill;
            option.Bind(
                capturedSkill,
                capturedSkill == selectedSkill,
                () => SelectSkill(capturedSkill));
        }
    }

    private SkillLoadoutOptionButton CreateOption()
    {
        if (optionButtonPrefab == null || contentRoot == null)
        {
            return null;
        }

        SkillLoadoutOptionButton option = Instantiate(optionButtonPrefab, contentRoot);
        option.gameObject.SetActive(true);
        spawnedOptions.Add(option);
        return option;
    }

    private void SelectSkill(PlayerSkillConfig skill)
    {
        selectedSkill = skill;
        RefreshSkillDetail(skill);
        RebuildSkillList();
    }

    private void RefreshSkillDetail(PlayerSkillConfig skill)
    {
        if (skill == null)
        {
            SetIcon(detailIconSkill, null);
            SetText(detailSkillNameText, "스킬이 선택되지 않음.");
            SetText(detailSkillLevelText, "--");
            SetText(detailSkillCooldownText, "--");
            SetText(detailSkillDescriptionText, "교체할 스킬을 선택하세요.");
            SetButtonInteractable(equipButton, false);
            return;
        }

        int level = GetSkillLevel(skill);
        SetIcon(detailIconSkill, skill.Icon);
        SetText(detailSkillNameText, skill.DisplayName);
        SetText(detailSkillLevelText, $"Lv.{level}/{skill.MaxLevel}");
        SetText(detailSkillCooldownText, $"{skill.GetCooldown(level):0.##}초");
        SetText(detailSkillDescriptionText, skill.Description);
        SetButtonInteractable(equipButton, targetSlotIndex >= 0);
    }

    private void EquipSelectedSkill()
    {
        ResolveReferences();
        if (player == null || selectedSkill == null || targetSlotIndex < 0)
        {
            return;
        }

        if (!player.EquipSkill(targetSlotIndex, selectedSkill))
        {
            return;
        }

        onEquipped?.Invoke();
        Close();
    }

    public void Close()
    {
        GameObject target = selectionRoot != null ? selectionRoot : gameObject;
        if (!target.activeInHierarchy)
        {
            target.SetActive(false);
            return;
        }

        // 닫기 연출 후 비활성화까지 PanelTweenTransition이 담당한다.
        PanelTweenTransition.EnsureOn(target).Close();
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

    private void ClearOptions()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            if (spawnedOptions[i] != null)
            {
                Destroy(spawnedOptions[i].gameObject);
            }
        }

        spawnedOptions.Clear();
    }

    private void ResolveReferences()
    {
        player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        inventory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : InventoryFacility.FindAny();
    }

    private static void SetIcon(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
        target.gameObject.SetActive(sprite != null);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetButtonInteractable(Button target, bool interactable)
    {
        if (target != null)
        {
            target.interactable = interactable;
        }
    }
}
