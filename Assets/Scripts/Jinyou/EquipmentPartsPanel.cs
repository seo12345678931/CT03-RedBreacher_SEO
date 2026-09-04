using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentPartsPanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InventoryFacility inventory;
    [SerializeField] private PlayerEquipmentPartLoadout loadout;
    [SerializeField] private PlayerCurrencyWallet currencyWallet;

    [Header("Part List")]
    [SerializeField] private RectTransform partContentRoot;
    [SerializeField] private Button partButtonPrefab;
    [SerializeField] private Color commonFrameColor = Color.white;
    [SerializeField] private Color rareFrameColor = new Color(0.25f, 0.55f, 1f, 1f);
    [SerializeField] private Color epicFrameColor = new Color(0.75f, 0.3f, 1f, 1f);
    [SerializeField] private Color legendaryFrameColor = new Color(1f, 0.72f, 0.18f, 1f);
    [SerializeField, Min(1)] private int partButtonsPerFrame = 40;

    [Header("Equipped Slots")]
    [SerializeField] private TMP_Text armorSlotText;
    [SerializeField] private TMP_Text engineSlotText;
    [SerializeField] private TMP_Text chipSlotText;

    [Header("Selected Part")]
    [SerializeField] private TMP_Text partNameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text mainStatText;
    [SerializeField] private TMP_Text subStatText;
    [SerializeField] private TMP_Text salePriceText;
    [SerializeField] private Image partIcon;
    [SerializeField] private TMP_Text beforeSelectText;
    [SerializeField] private TMP_Text mainStatTitle;
    [SerializeField] private TMP_Text subStatTitle;

    [Header("Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button sellButton;

    [Header("Filter")]
    [SerializeField] private Button armorFilterButton;
    [SerializeField] private Button engineFilterButton;
    [SerializeField] private Button chipFilterButton;
    [SerializeField] private Button allFilterButton;

    [Header("AutoSellSettings")] 
    [SerializeField] private Button autoSellActiveButton;
    [SerializeField] private Button autoSellDeActiveButton;
    [SerializeField] private Button commonAutoSellButton;
    [SerializeField] private Button rareAutoSellButton;
    [SerializeField] private Button epicAutoSellButton;
    [SerializeField] private TMP_Text autoSellWarningText;
    [SerializeField] private TMP_Text epicAutoSellButtonText;

    private readonly List<Button> spawnedButtons = new List<Button>();
    private readonly Dictionary<string, GameObject> newBadges = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> selectIcons = new Dictionary<string, GameObject>();
    private string selectedInstanceId;
    private PanelTweenTransition panelTransition;
    private Coroutine partListBuildCoroutine;

    // null이면 전체 표시, 값이 있으면 해당 슬롯 파츠만 표시한다.
    private EquipmentPartSlot? slotFilter;

    private void OnEnable()
    {
        ResolveReferences();
        EnsurePanelTransition();
        SubscribeEvents();
        equipButton?.onClick.AddListener(EquipSelected);
        unequipButton?.onClick.AddListener(UnequipSelected);
        sellButton?.onClick.AddListener(SellSelected);
        armorFilterButton?.onClick.AddListener(FilterByArmor);
        engineFilterButton?.onClick.AddListener(FilterByEngine);
        chipFilterButton?.onClick.AddListener(FilterByChip);
        allFilterButton?.onClick.AddListener(ShowAllParts);
        autoSellActiveButton?.onClick.AddListener(EnableAutoSell);
        autoSellDeActiveButton?.onClick.AddListener(DisableAutoSell);
        commonAutoSellButton?.onClick.AddListener(SetCommonAutoSell);
        rareAutoSellButton?.onClick.AddListener(SetRareAndBelowAutoSell);
        epicAutoSellButton?.onClick.AddListener(SetEpicAndBelowAutoSell);
        Rebuild();
    }

    private void OnDisable()
    {
        StopPartListBuild();
        UnsubscribeEvents();
        equipButton?.onClick.RemoveListener(EquipSelected);
        unequipButton?.onClick.RemoveListener(UnequipSelected);
        sellButton?.onClick.RemoveListener(SellSelected);
        armorFilterButton?.onClick.RemoveListener(FilterByArmor);
        engineFilterButton?.onClick.RemoveListener(FilterByEngine);
        chipFilterButton?.onClick.RemoveListener(FilterByChip);
        allFilterButton?.onClick.RemoveListener(ShowAllParts);
        autoSellActiveButton?.onClick.RemoveListener(EnableAutoSell);
        autoSellDeActiveButton?.onClick.RemoveListener(DisableAutoSell);
        commonAutoSellButton?.onClick.RemoveListener(SetCommonAutoSell);
        rareAutoSellButton?.onClick.RemoveListener(SetRareAndBelowAutoSell);
        epicAutoSellButton?.onClick.RemoveListener(SetEpicAndBelowAutoSell);
        ClearButtons();
    }

    public void Rebuild()
    {
        ResolveReferences();
        StopPartListBuild();
        ClearButtons();

        if (inventory != null && partContentRoot != null && partButtonPrefab != null)
        {
            List<EquipmentPartInstance> parts = CollectSortedParts();

            if (parts.Count <= partButtonsPerFrame)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    CreatePartButton(parts[i]);
                }
            }
            else
            {
                partListBuildCoroutine = StartCoroutine(CreatePartButtonsBatched(parts));
            }
        }

        RefreshFilterButtons();
        RefreshAutoSellButtons();
        RefreshEquippedSlots();
        RefreshSelectionIcons();
        RefreshDetail();
    }

    private List<EquipmentPartInstance> CollectSortedParts()
    {
        List<EquipmentPartInstance> parts = new List<EquipmentPartInstance>();
        foreach (EquipmentPartInstance part in inventory.EquipmentParts)
        {
            if (part == null)
            {
                continue;
            }

            // 필터가 설정된 경우 해당 슬롯의 파츠만 목록에 표시한다.
            if (slotFilter.HasValue && part.slot != slotFilter.Value)
            {
                continue;
            }

            parts.Add(part);
        }

        // 장착 중 → 희귀도 내림차순 → 레벨 내림차순 → 이름 순으로 정렬한다.
        parts.Sort(ComparePartsForDisplay);
        return parts;
    }

    private int ComparePartsForDisplay(EquipmentPartInstance a, EquipmentPartInstance b)
    {
        bool equippedA = loadout != null && loadout.IsEquipped(a.instanceId);
        bool equippedB = loadout != null && loadout.IsEquipped(b.instanceId);
        if (equippedA != equippedB)
        {
            return equippedA ? -1 : 1;
        }

        if (a.rarity != b.rarity)
        {
            return b.rarity.CompareTo(a.rarity);
        }

        if (a.level != b.level)
        {
            return b.level.CompareTo(a.level);
        }

        return string.Compare(GetSortName(a), GetSortName(b), System.StringComparison.Ordinal);
    }

    private string GetSortName(EquipmentPartInstance part)
    {
        EquipmentPartConfig config = inventory != null ? inventory.ResolveEquipmentPartConfig(part.configId) : null;
        return GetDisplayName(config, part);
    }

    public void FilterByArmor() => SetSlotFilter(EquipmentPartSlot.Armor);

    public void FilterByEngine() => SetSlotFilter(EquipmentPartSlot.Engine);

    public void FilterByChip() => SetSlotFilter(EquipmentPartSlot.Chip);

    public void ShowAllParts() => SetSlotFilter(null);

    private void SetSlotFilter(EquipmentPartSlot? slot)
    {
        // 활성화된 필터 버튼을 다시 누르면 전체 표시로 토글한다.
        slotFilter = slotFilter == slot ? null : slot;
        Rebuild();
    }

    private void RefreshFilterButtons()
    {
        SetFilterButtonActive(armorFilterButton, slotFilter == EquipmentPartSlot.Armor);
        SetFilterButtonActive(engineFilterButton, slotFilter == EquipmentPartSlot.Engine);
        SetFilterButtonActive(chipFilterButton, slotFilter == EquipmentPartSlot.Chip);
        SetFilterButtonActive(allFilterButton, slotFilter == null);
    }

    // 자동판매 기능 활성여부를 확인하는 메소드들
    private void EnableAutoSell()
    {
        inventory?.SetEquipmentAutoSellEnabled(true);
        RefreshAutoSellButtons();
    }

    private void DisableAutoSell()
    {
        inventory?.SetEquipmentAutoSellEnabled(false);
        RefreshAutoSellButtons();
    }

    // 판매 기준 등급을 설정하는 메소드들 일반/일반, 희귀/일반, 희귀, 영웅
    private void SetCommonAutoSell()
    {
        SetAutoSellMaxRarity(EquipmentPartRarity.Common);
    }

    private void SetRareAndBelowAutoSell()
    {
        SetAutoSellMaxRarity(EquipmentPartRarity.Rare);
    }

    private void SetEpicAndBelowAutoSell()
    {
        SetAutoSellMaxRarity(EquipmentPartRarity.Epic);
    }

    private void SetAutoSellMaxRarity(EquipmentPartRarity maxRarity)
    {
        inventory?.SetEquipmentAutoSellMaxRarity(maxRarity);
        RefreshAutoSellButtons();
    }

    // 자동판매 설정창 관련버튼 Refresh
    private void RefreshAutoSellButtons()
    {
        if (autoSellActiveButton == null
            && autoSellDeActiveButton == null
            && commonAutoSellButton == null
            && rareAutoSellButton == null
            && epicAutoSellButton == null
            && autoSellWarningText == null
            && epicAutoSellButtonText == null)
        {
            return;
        }

        bool autoSellEnabled = inventory != null && inventory.AutoSellEquipmentPartsEnabled;
        bool canUseEpicAutoSell = CanUseEpicAutoSell();
        EquipmentPartRarity selectedMaxRarity = inventory != null
            ? inventory.AutoSellMaxRarity
            : EquipmentPartRarity.Rare;
        if (autoSellActiveButton != null)
        {
            autoSellActiveButton.interactable = !autoSellEnabled;
        }

        if (autoSellDeActiveButton != null)
        {
            autoSellDeActiveButton.interactable = autoSellEnabled;
        }

        if (commonAutoSellButton != null)
        {
            commonAutoSellButton.interactable = autoSellEnabled
                && selectedMaxRarity != EquipmentPartRarity.Common;
        }

        if (rareAutoSellButton != null)
        {
            rareAutoSellButton.interactable = autoSellEnabled
                && selectedMaxRarity != EquipmentPartRarity.Rare;
        }

        if (epicAutoSellButton != null)
        {
            epicAutoSellButton.interactable = autoSellEnabled
                && canUseEpicAutoSell
                && selectedMaxRarity != EquipmentPartRarity.Epic;
        }

        RefreshEpicAutoSellButtonText(canUseEpicAutoSell);
        RefreshAutoSellWarningText(autoSellEnabled, selectedMaxRarity);
    }

    private void RefreshAutoSellButtons(bool _)
    {
        RefreshAutoSellButtons();
    }

    private void RefreshAutoSellWarningText(bool autoSellEnabled, EquipmentPartRarity selectedMaxRarity)
    {
        if (autoSellWarningText == null)
        {
            return;
        }

        if (!autoSellEnabled)
        {
            autoSellWarningText.text = string.Empty;
            return;
        }

        autoSellWarningText.text = selectedMaxRarity switch
        {
            EquipmentPartRarity.Common => "일반등급의 장비는 \n<color=#EC6363><b>획득 즉시 자동판매</b></color>됩니다.",
            EquipmentPartRarity.Rare => "일반, 희귀등급의 장비는 \n<color=#EC6363><b>획득 즉시 자동판매</b></color>됩니다.",
            EquipmentPartRarity.Epic => "경고! <color=#EC6363><b>[전설등급]을 제외한 모든 장비는 \n획득 즉시 자동판매</b></color>됩니다.",
            _ => string.Empty
        };
    }

    private void RefreshEpicAutoSellButtonText(bool canUseEpicAutoSell)
    {
        if (epicAutoSellButtonText == null)
        {
            return;
        }

        epicAutoSellButtonText.text = canUseEpicAutoSell
            ? "일반, 희귀, 영웅등급"
            : "전설장비 5개 이상 소유 시 활성";
    }

    private bool CanUseEpicAutoSell()
    {
        return CountOwnedLegendaryParts() >= 5;
    }

    private int CountOwnedLegendaryParts()
    {
        HashSet<string> countedIds = new HashSet<string>();
        int count = 0;

        if (inventory != null)
        {
            foreach (EquipmentPartInstance part in inventory.EquipmentParts)
            {
                if (TryCountLegendaryPart(part, countedIds))
                {
                    count++;
                }
            }
        }

        if (loadout != null)
        {
            if (TryCountLegendaryPart(loadout.GetEquippedPart(EquipmentPartSlot.Armor), countedIds))
            {
                count++;
            }

            if (TryCountLegendaryPart(loadout.GetEquippedPart(EquipmentPartSlot.Engine), countedIds))
            {
                count++;
            }

            if (TryCountLegendaryPart(loadout.GetEquippedPart(EquipmentPartSlot.Chip), countedIds))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryCountLegendaryPart(EquipmentPartInstance part, HashSet<string> countedIds)
    {
        if (part == null || part.rarity != EquipmentPartRarity.Legendary)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(part.instanceId))
        {
            return true;
        }

        return countedIds.Add(part.instanceId);
    }

    private static void SetFilterButtonActive(Button button, bool active)
    {
        // 현재 선택된 필터 버튼은 비활성(눌린 상태)으로 표시해 시각적으로 구분한다.
        if (button != null)
        {
            button.interactable = !active;
        }
    }

    public void EquipSelected()
    {
        EquipmentPartInstance part = GetSelectedPart();
        if (part != null && loadout != null)
        {
            if (loadout.Equip(part))
            {
                // 파츠 장착 성공 여부를 확인하기 위해 게임이벤트를 호출 (튜토리얼)
                TutorialManager.RaiseGameEvent("InventoryPartEquipped");
            }
        }
    }

    public void UnequipSelected()
    {
        EquipmentPartInstance part = GetSelectedPart();
        if (part != null && loadout != null && loadout.IsEquipped(part.instanceId))
        {
            loadout.Unequip(part.slot);
        }
    }

    public void SellSelected()
    {
        EquipmentPartInstance part = GetSelectedPart();
        if (part == null || inventory == null)
        {
            return;
        }

        if (inventory.TrySellEquipmentPart(part.instanceId, loadout, currencyWallet))
        {
            selectedInstanceId = string.Empty;
            RefreshSelectionIcons();
            RefreshDetail();
        }
    }

    private void CreatePartButton(EquipmentPartInstance part)
    {
        if (part == null)
        {
            return;
        }

        Button button = Instantiate(partButtonPrefab, partContentRoot);
        EquipmentPartConfig config = inventory.ResolveEquipmentPartConfig(part.configId);
        Image iconImage = FindChildImage(button.transform, "Icon");
        if (iconImage != null)
        {
            // 파츠 종류별 SO에 지정된 아이콘을 목록 슬롯에 표시한다.
            iconImage.sprite = GetPartsIcon(config);
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        Image frameImage = FindChildImage(button.transform, "Frame");
        if (frameImage != null)
        {
            // 파츠 희귀도에 따라 슬롯 프레임 색상을 구분한다.
            frameImage.color = GetRarityFrameColor(part.rarity);
        }

        // 장착 표시는 텍스트 대신 "Equip_Icon" 오브젝트의 활성/비활성으로 한다(프리팹에 있을 때).
        bool isEquipped = loadout != null && loadout.IsEquipped(part.instanceId);
        Transform equipIcon = FindChildObject(button.transform, "Equip_Icon");
        if (equipIcon != null)
        {
            equipIcon.gameObject.SetActive(isEquipped);
        }

        // 이름/레벨/등급을 분리된 텍스트로 표시한다(프리팹에 NameText/LevelText/RarityText 자식이 있을 때).
        // 셋 다 없으면 기존처럼 첫 TMP_Text에 합본으로 폴백한다.
        TMP_Text nameLabel = FindChildText(button.transform, "NameText");
        TMP_Text levelLabel = FindChildText(button.transform, "LevelText");
        TMP_Text rarityLabel = FindChildText(button.transform, "RarityText");

        if (nameLabel != null || levelLabel != null || rarityLabel != null)
        {
            if (nameLabel != null)
            {
                nameLabel.text = GetDisplayName(config, part);
            }

            if (levelLabel != null)
            {
                levelLabel.text = $"Lv.{part.level}";
            }

            if (rarityLabel != null)
            {
                rarityLabel.text = GetRarityName(part.rarity);
                // 등급 색으로 가시성을 높인다(프레임 색과 동일 기준).
                rarityLabel.color = GetRarityFrameColor(part.rarity);
            }
        }
        else
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"{GetDisplayName(config, part)} / Lv.{part.level} / {GetRarityName(part.rarity)}";
            }
        }

        // 아직 확인하지 않은 신규 파츠에는 "New" 뱃지를 표시한다(프리팹에 "New" 자식이 있을 때만).
        Transform newBadge = FindChildObject(button.transform, "New");
        if (newBadge != null)
        {
            newBadge.gameObject.SetActive(part.isNew);
            if (!string.IsNullOrEmpty(part.instanceId))
            {
                newBadges[part.instanceId] = newBadge.gameObject;
            }
        }

        Transform selectIcon = FindChildObject(button.transform, "Select_Icon");
        if (selectIcon != null)
        {
            bool isSelected = !string.IsNullOrEmpty(part.instanceId)
                && part.instanceId == selectedInstanceId;
            selectIcon.gameObject.SetActive(isSelected);
            if (!string.IsNullOrEmpty(part.instanceId))
            {
                selectIcons[part.instanceId] = selectIcon.gameObject;
            }
        }

        string capturedId = part.instanceId;
        button.onClick.AddListener(() => Select(capturedId));
        spawnedButtons.Add(button);
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        Transform found = FindChildObject(root, childName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChildObject(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildObject(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Image FindChildImage(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root.GetComponent<Image>();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Image image = FindChildImage(root.GetChild(i), childName);
            if (image != null)
            {
                return image;
            }
        }

        return null;
    }

    private Color GetRarityFrameColor(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => rareFrameColor,
            EquipmentPartRarity.Epic => epicFrameColor,
            EquipmentPartRarity.Legendary => legendaryFrameColor,
            _ => commonFrameColor
        };
    }

    private void Select(string instanceId)
    {
        selectedInstanceId = instanceId;

        // 선택한 파츠는 확인 처리하여 신규 뱃지를 해제한다(목록 전체 리빌드 없이 해당 뱃지만 끈다).
        if (inventory != null)
        {
            inventory.MarkEquipmentPartSeen(instanceId);
        }

        if (!string.IsNullOrEmpty(instanceId)
            && newBadges.TryGetValue(instanceId, out GameObject badge)
            && badge != null)
        {
            badge.SetActive(false);
        }

        RefreshSelectionIcons();
        RefreshDetail();
    }

    private IEnumerator CreatePartButtonsBatched(List<EquipmentPartInstance> parts)
    {
        int createdThisFrame = 0;
        int maxPerFrame = Mathf.Max(1, partButtonsPerFrame);

        for (int i = 0; i < parts.Count; i++)
        {
            CreatePartButton(parts[i]);
            createdThisFrame++;

            if (createdThisFrame >= maxPerFrame)
            {
                createdThisFrame = 0;
                RefreshSelectionIcons();
                yield return null;
            }
        }

        partListBuildCoroutine = null;
        RefreshSelectionIcons();
    }

    private void RefreshSelectionIcons()
    {
        foreach (KeyValuePair<string, GameObject> pair in selectIcons)
        {
            if (pair.Value != null)
            {
                pair.Value.SetActive(!string.IsNullOrEmpty(selectedInstanceId)
                    && pair.Key == selectedInstanceId);
            }
        }
    }

    private void RefreshEquippedSlots()
    {
        SetText(armorSlotText, BuildEquippedText(EquipmentPartSlot.Armor));
        SetText(engineSlotText, BuildEquippedText(EquipmentPartSlot.Engine));
        SetText(chipSlotText, BuildEquippedText(EquipmentPartSlot.Chip));
    }

    private string BuildEquippedText(EquipmentPartSlot slot)
    {
        EquipmentPartInstance part = loadout != null ? loadout.GetEquippedPart(slot) : null;
        if (part == null)
        {
            return "미장착";
        }

        EquipmentPartConfig config = inventory != null ? inventory.ResolveEquipmentPartConfig(part.configId) : null;
        return $"{GetDisplayName(config, part)} Lv.{part.level} ({GetRarityName(part.rarity)})";
    }

    private void RefreshDetail()
    {
        EquipmentPartInstance part = GetSelectedPart();
        EquipmentPartConfig config = part != null && inventory != null
            ? inventory.ResolveEquipmentPartConfig(part.configId)
            : null;

        SetText(beforeSelectText, part != null ? string.Empty : "파츠를 선택하세요.");
        SetText(partNameText, part != null ? GetDisplayName(config, part) : string.Empty);
        SetText(rarityText, part != null ? $"/ Lv.{part.level} {GetRarityName(part.rarity)}" : string.Empty);
        SetText(mainStatTitle, part != null ? "주 옵션" : string.Empty);
        SetText(mainStatText, part != null ? FormatStat(part.mainStatType, part.GetScaledMainValue()) : string.Empty);
        SetText(subStatTitle, part != null ? "부가 옵션" : string.Empty);
        SetText(subStatText, part != null ? BuildSubStatText(part) : string.Empty);
        SetText(salePriceText, part != null ? $"판매\n{part.salePrice:N0} 크레딧" : string.Empty);
        SetPartIcon(partIcon, GetPartsIcon(config));

        bool hasSelection = part != null;
        bool equipped = part != null && loadout != null && loadout.IsEquipped(part.instanceId);
        SetButtonVisible(equipButton, hasSelection);
        SetButtonVisible(unequipButton, hasSelection);
        SetButtonVisible(sellButton, hasSelection);

        if (equipButton != null)
        {
            equipButton.interactable = hasSelection && !equipped;
        }

        if (unequipButton != null)
        {
            unequipButton.interactable = equipped;
        }

        if (sellButton != null)
        {
            sellButton.interactable = hasSelection && !equipped;
        }
    }

    private string BuildSubStatText(EquipmentPartInstance part)
    {
        if (part.subStats == null || part.subStats.Count == 0)
        {
            return "부옵 없음";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < part.subStats.Count; i++)
        {
            EquipmentSubStat subStat = part.subStats[i];
            if (subStat == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(FormatStat(subStat.statType, part.GetScaledSubStatValue(subStat)));
        }

        return builder.ToString();
    }

    private EquipmentPartInstance GetSelectedPart()
    {
        return inventory != null ? inventory.FindEquipmentPart(selectedInstanceId) : null;
    }

    private void ResolveReferences()
    {
        inventory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : FindFirstObjectByType<InventoryFacility>();
        loadout ??= FindFirstObjectByType<PlayerEquipmentPartLoadout>();
        currencyWallet ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.CurrencyWallet
            : FindFirstObjectByType<PlayerCurrencyWallet>();
    }

    private void EnsurePanelTransition()
    {
        // 장비 패널의 ExitButton 직접 SetActive 연결을 공통 닫힘 연출로 대체한다.
        panelTransition ??= GetComponent<PanelTweenTransition>();
        if (panelTransition == null)
        {
            panelTransition = gameObject.AddComponent<PanelTweenTransition>();
        }
    }

    private void SubscribeEvents()
    {
        inventory?.OnEquipmentPartsChanged.AddListener(Rebuild);
        inventory?.OnEquipmentAutoSellChanged.AddListener(RefreshAutoSellButtons);
        loadout?.OnLoadoutChanged.AddListener(Rebuild);
    }

    private void UnsubscribeEvents()
    {
        inventory?.OnEquipmentPartsChanged.RemoveListener(Rebuild);
        inventory?.OnEquipmentAutoSellChanged.RemoveListener(RefreshAutoSellButtons);
        loadout?.OnLoadoutChanged.RemoveListener(Rebuild);
    }

    private void StopPartListBuild()
    {
        if (partListBuildCoroutine == null)
        {
            return;
        }

        StopCoroutine(partListBuildCoroutine);
        partListBuildCoroutine = null;
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
        newBadges.Clear();
        selectIcons.Clear();
    }

    private static string GetDisplayName(EquipmentPartConfig config, EquipmentPartInstance part)
    {
        return config != null ? config.DisplayName : part.configId;
    }

    private static Sprite GetPartsIcon(EquipmentPartConfig config)
    {
        return config != null ? config.Icon : null;
    }

    private static string GetRarityName(EquipmentPartRarity rarity)
    {
        string rarityName = rarity switch
        {
            EquipmentPartRarity.Rare => "희귀",
            EquipmentPartRarity.Epic => "영웅",
            EquipmentPartRarity.Legendary => "전설",
            _ => "일반"
        };

        return $"<color=#{GetRarityColorHex(rarity)}>{rarityName}</color>";
    }

    private static string GetRarityColorHex(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => "59CCFF",
            EquipmentPartRarity.Epic => "FF73E6",
            EquipmentPartRarity.Legendary => "FFB82E",
            _ => "FFFFFF"
        };
    }

    private static string FormatStat(EquipmentStatType statType, float value)
    {
        string statName = statType switch
        {
            EquipmentStatType.AttackPercent => "공격력",
            EquipmentStatType.HealthPercent => "최대 체력",
            EquipmentStatType.AttackSpeedPercent => "공격속도",
            EquipmentStatType.CritChance => "치명타 확률",
            _ => "치명타 피해"
        };

        return $"{statName} +{value * 100f:0.##}%";
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetButtonVisible(Button target, bool visible)
    {
        if (target != null && target.gameObject.activeSelf != visible)
        {
            target.gameObject.SetActive(visible);
        }
    }

    private static void SetPartIcon(Image target, Sprite icon)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = icon;
        target.enabled = icon != null;
        target.preserveAspect = true;
    }
}


