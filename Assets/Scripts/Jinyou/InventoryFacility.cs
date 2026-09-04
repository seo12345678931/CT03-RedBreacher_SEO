using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class InventoryFacility : MonoBehaviour
{
    private const string EquipmentPartsKey = "InventoryFacility.EquipmentParts";
    private const string CollectionProgressKey = "InventoryFacility.CollectionProgress";
    private const string EquipmentAutoSellEnabledKey = "InventoryFacility.EquipmentAutoSell.Enabled";
    private const string EquipmentAutoSellMaxRarityKey = "InventoryFacility.EquipmentAutoSell.MaxRarity";
    private const bool DefaultEquipmentAutoSellEnabled = false;
    private const EquipmentPartRarity DefaultEquipmentAutoSellMaxRarity = EquipmentPartRarity.Common;

    [System.Serializable]
    private class EquipmentPartSaveData
    {
        public List<EquipmentPartInstance> parts = new List<EquipmentPartInstance>();
    }

    [System.Serializable]
    public class CollectionProgress
    {
        public string configId;
        [Min(1)] public int level = 1;
        [Min(0)] public int duplicateProgress;
    }

    [System.Serializable]
    public class CollectionGrantResult
    {
        public bool success;
        public bool isNew;
        public string configId;
        public int previousLevel;
        public int currentLevel;
        public int duplicateProgress;
        public int requiredDuplicates;
        public int coreCrystalReward;
    }

    [System.Serializable]
    private class CollectionProgressSaveData
    {
        public List<CollectionProgress> weapons = new List<CollectionProgress>();
        public List<CollectionProgress> skills = new List<CollectionProgress>();
        public List<string> drones = new List<string>();
    }

    [System.Serializable]
    public class WeaponStack
    {
        public ProjectileConfig weaponConfig;
        [Min(1)] public int quantity = 1;

        public int DuplicateCount => Mathf.Max(0, quantity - 1);
    }

    [Header("Weapons")]
    [SerializeField] private List<ProjectileConfig> weaponConfigs = new List<ProjectileConfig>();
    [SerializeField] private List<WeaponStack> weaponStacks = new List<WeaponStack>();

    [Header("Skills")]
    [SerializeField] private List<PlayerSkillConfig> skillConfigs = new List<PlayerSkillConfig>();

    [Header("Units")]
    [SerializeField] private List<PlayerUnitConfig> unitConfigs = new List<PlayerUnitConfig>();

    [Header("Equipment Parts")]
    [SerializeField] private List<EquipmentPartConfig> equipmentPartConfigs = new List<EquipmentPartConfig>();
    [SerializeField] private List<EquipmentPartInstance> equipmentParts = new List<EquipmentPartInstance>();
    [SerializeField] private bool autoSellEquipmentPartsEnabled;
    [SerializeField] private EquipmentPartRarity autoSellMaxRarity = DefaultEquipmentAutoSellMaxRarity;
    [SerializeField] private bool saveEquipmentPartsToPlayerPrefs = true;
    [SerializeField] private bool saveCollectionProgressToPlayerPrefs = true;

    [Header("Equipment Part Drop Visual")]
    [SerializeField] private bool playEquipmentPartDropVisual = true;
    [SerializeField] private Transform equipmentPartCollectTarget;
    [SerializeField] private float partDropPopDuration = 0.25f;
    [SerializeField] private float partDropHoldDuration = 0.35f;
    [SerializeField] private float partDropCollectDuration = 0.45f;
    [SerializeField] private float partDropPopDistance = 0.8f;
    [SerializeField] private float partDropVisualSize = 0.7f;
    [SerializeField] private int partDropSortingOrder = 20;

    [Header("Equipment Part Drop Debug")]
    [SerializeField] private bool forceEquipmentPartDrop;

    [Header("Events")]
    public UnityEvent OnInventoryChanged = new UnityEvent();
    public UnityEvent OnCollectionProgressChanged = new UnityEvent();
    public UnityEvent<string, int> OnWeaponLevelChanged = new UnityEvent<string, int>();
    public UnityEvent<string, int> OnSkillLevelChanged = new UnityEvent<string, int>();
    public UnityEvent OnEquipmentPartsChanged = new UnityEvent();
    public UnityEvent<bool> OnEquipmentAutoSellChanged = new UnityEvent<bool>();

    private bool equipmentPartsInitialized;
    private bool collectionProgressInitialized;
    private bool autoSellSettingsInitialized;
    private List<CollectionProgress> weaponProgress = new List<CollectionProgress>();
    private List<CollectionProgress> skillProgress = new List<CollectionProgress>();
    private List<string> ownedDroneIds = new List<string>();
    private readonly List<PlayerSkillConfig> ownedSkillConfigs = new List<PlayerSkillConfig>();

    public IReadOnlyList<ProjectileConfig> WeaponConfigs
    {
        get
        {
            EnsureCollectionProgressInitialized();
            return weaponConfigs;
        }
    }
    public IReadOnlyList<WeaponStack> WeaponStacks => weaponStacks;
    public IReadOnlyList<PlayerSkillConfig> SkillConfigs
    {
        get
        {
            EnsureCollectionProgressInitialized();
            return ownedSkillConfigs;
        }
    }
    public IReadOnlyList<PlayerUnitConfig> UnitConfigs => unitConfigs;
    public IReadOnlyList<string> OwnedDroneIds
    {
        get
        {
            EnsureCollectionProgressInitialized();
            return ownedDroneIds;
        }
    }
    public IReadOnlyList<EquipmentPartConfig> EquipmentPartConfigs
    {
        get
        {
            EnsureEquipmentPartsInitialized();
            return equipmentPartConfigs;
        }
    }
    public IReadOnlyList<EquipmentPartInstance> EquipmentParts
    {
        get
        {
            EnsureEquipmentPartsInitialized();
            return equipmentParts;
        }
    }
    public bool ForceEquipmentPartDrop => forceEquipmentPartDrop;
    public bool AutoSellEquipmentPartsEnabled
    {
        get
        {
            EnsureAutoSellSettingsInitialized();
            return autoSellEquipmentPartsEnabled;
        }
    }
    public EquipmentPartRarity AutoSellMaxRarity
    {
        get
        {
            EnsureAutoSellSettingsInitialized();
            return autoSellMaxRarity;
        }
    }

    private void Awake()
    {
        EnsureAutoSellSettingsInitialized();
        EnsureCollectionProgressInitialized();
        EnsureEquipmentPartsInitialized();
    }

    public static InventoryFacility FindAny()
    {
        // 기지 UI가 비활성 상태여도 파츠 보상 저장소를 찾을 수 있어야 한다.
        return FindFirstObjectByType<InventoryFacility>(FindObjectsInactive.Include);
    }

    public JinyouInventorySaveData CaptureState()
    {
        EnsureAutoSellSettingsInitialized();
        EnsureCollectionProgressInitialized();
        EnsureEquipmentPartsInitialized();
        return new JinyouInventorySaveData
        {
            weapons = CloneProgressList(weaponProgress),
            skills = CloneProgressList(skillProgress),
            drones = new List<string>(ownedDroneIds),
            equipmentParts = new List<EquipmentPartInstance>(equipmentParts),
            equipmentAutoSellEnabled = autoSellEquipmentPartsEnabled,
            equipmentAutoSellMaxRarity = autoSellMaxRarity
        };
    }

    public void RestoreState(JinyouInventorySaveData data)
    {
        if (data == null)
        {
            return;
        }

        collectionProgressInitialized = true;
        equipmentPartsInitialized = true;
        weaponProgress = CloneProgressList(data.weapons);
        skillProgress = CloneProgressList(data.skills);
        ownedDroneIds = data.drones != null ? new List<string>(data.drones) : new List<string>();
        equipmentParts = data.equipmentParts != null
            ? new List<EquipmentPartInstance>(data.equipmentParts)
            : new List<EquipmentPartInstance>();
        autoSellSettingsInitialized = true;
        autoSellEquipmentPartsEnabled = data.equipmentAutoSellEnabled;
        autoSellMaxRarity = ClampAutoSellMaxRarity(data.equipmentAutoSellMaxRarity);

        NormalizeCollectionProgress();
        NormalizeEquipmentParts();
        SyncWeaponConfigsFromProgress();
        SyncSkillConfigsFromProgress();
        NotifyCollectionChanged();
        OnEquipmentPartsChanged.Invoke();
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveCollectionProgressToPlayerPrefs = enabled;
        saveEquipmentPartsToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(CollectionProgressKey);
        PlayerPrefs.DeleteKey(EquipmentPartsKey);
        PlayerPrefs.DeleteKey(EquipmentAutoSellEnabledKey);
        PlayerPrefs.DeleteKey(EquipmentAutoSellMaxRarityKey);
        autoSellSettingsInitialized = false;
        autoSellEquipmentPartsEnabled = DefaultEquipmentAutoSellEnabled;
        autoSellMaxRarity = DefaultEquipmentAutoSellMaxRarity;
        PlayerPrefs.Save();
    }

    public void SetEquipmentAutoSellEnabled(bool enabled)
    {
        EnsureAutoSellSettingsInitialized();
        if (autoSellEquipmentPartsEnabled == enabled)
        {
            OnEquipmentAutoSellChanged.Invoke(autoSellEquipmentPartsEnabled);
            return;
        }

        autoSellEquipmentPartsEnabled = enabled;
        SaveAutoSellSettings();
        OnEquipmentAutoSellChanged.Invoke(autoSellEquipmentPartsEnabled);
        OnInventoryChanged.Invoke();
    }

    public void SetEquipmentAutoSellMaxRarity(EquipmentPartRarity maxRarity)
    {
        EnsureAutoSellSettingsInitialized();
        EquipmentPartRarity clampedRarity = ClampAutoSellMaxRarity(maxRarity);
        if (autoSellMaxRarity == clampedRarity)
        {
            OnEquipmentAutoSellChanged.Invoke(autoSellEquipmentPartsEnabled);
            return;
        }

        autoSellMaxRarity = clampedRarity;
        SaveAutoSellSettings();
        OnEquipmentAutoSellChanged.Invoke(autoSellEquipmentPartsEnabled);
        OnInventoryChanged.Invoke();
    }

    private static List<CollectionProgress> CloneProgressList(List<CollectionProgress> source)
    {
        List<CollectionProgress> result = new List<CollectionProgress>();
        if (source == null)
        {
            return result;
        }

        foreach (CollectionProgress progress in source)
        {
            if (progress != null)
            {
                result.Add(new CollectionProgress
                {
                    configId = progress.configId,
                    level = progress.level,
                    duplicateProgress = progress.duplicateProgress
                });
            }
        }

        return result;
    }

    public bool ContainsWeapon(ProjectileConfig weaponConfig)
    {
        EnsureCollectionProgressInitialized();
        return weaponConfig != null && FindProgress(weaponProgress, weaponConfig.Id) != null;
    }

    public bool ContainsSkill(PlayerSkillConfig skillConfig)
    {
        EnsureCollectionProgressInitialized();
        return skillConfig != null && FindProgress(skillProgress, skillConfig.Id) != null;
    }

    public bool ContainsUnit(PlayerUnitConfig unitConfig)
    {
        return unitConfig != null && unitConfigs.Contains(unitConfig);
    }

    public bool ContainsDrone(DroneConfig droneConfig)
    {
        EnsureCollectionProgressInitialized();
        return droneConfig != null && ownedDroneIds.Contains(droneConfig.Id);
    }

    public bool AddDrone(DroneConfig droneConfig)
    {
        return RegisterDrone(droneConfig, true);
    }

    public bool RegisterInitialDrone(DroneConfig droneConfig)
    {
        return RegisterDrone(droneConfig, false);
    }

    public bool AddWeapon(ProjectileConfig weaponConfig)
    {
        return AddWeapon(weaponConfig, 1);
    }

    public bool AddWeapon(ProjectileConfig weaponConfig, int quantity)
    {
        return GrantWeapon(weaponConfig, quantity).success;
    }

    public CollectionGrantResult GrantWeapon(ProjectileConfig weaponConfig, int quantity = 1)
    {
        EnsureCollectionProgressInitialized();
        if (weaponConfig == null || quantity <= 0)
        {
            return new CollectionGrantResult();
        }

        bool isNew = !ContainsWeapon(weaponConfig);
        RegisterWeaponConfig(weaponConfig);
        int previousLevel = GetWeaponLevel(weaponConfig);
        int coreCrystalReward = AddCopies(
            weaponProgress,
            weaponConfig,
            quantity);
        int currentLevel = GetWeaponLevel(weaponConfig);
        SaveCollectionProgress();
        NotifyCollectionChanged();
        if (currentLevel != previousLevel)
        {
            OnWeaponLevelChanged.Invoke(weaponConfig.Id, currentLevel);
        }

        if (isNew)
        {
            // 중복 무기가 아닌 최초 획득만 수집 업적에 반영한다.
            AchievementManager.ReportWeaponCollected();
        }

        MainGuideMissionManager.ReportWeaponCollected(quantity);

        return BuildGrantResult(
            weaponConfig,
            isNew,
            previousLevel,
            currentLevel,
            GetDuplicateProgress(weaponConfig),
            GetRequiredDuplicates(weaponConfig),
            coreCrystalReward);
    }

    public bool AddSkill(PlayerSkillConfig skillConfig, int quantity = 1)
    {
        return GrantSkill(skillConfig, quantity).success;
    }

    public CollectionGrantResult GrantSkill(PlayerSkillConfig skillConfig, int quantity = 1)
    {
        EnsureCollectionProgressInitialized();
        if (skillConfig == null || quantity <= 0)
        {
            return new CollectionGrantResult();
        }

        bool isNew = !ContainsSkill(skillConfig);
        RegisterSkillConfig(skillConfig);
        int previousLevel = GetSkillLevel(skillConfig);
        int coreCrystalReward = AddCopies(
            skillProgress,
            skillConfig,
            quantity);
        int currentLevel = GetSkillLevel(skillConfig);
        SyncSkillConfigsFromProgress();
        SaveCollectionProgress();
        NotifyCollectionChanged();
        if (currentLevel != previousLevel)
        {
            OnSkillLevelChanged.Invoke(skillConfig.Id, currentLevel);
        }

        return BuildGrantResult(
            skillConfig,
            isNew,
            previousLevel,
            currentLevel,
            GetDuplicateProgress(skillConfig),
            GetRequiredDuplicates(skillConfig),
            coreCrystalReward);
    }

    public void RegisterInitialWeapon(ProjectileConfig weaponConfig)
    {
        EnsureCollectionProgressInitialized();
        if (weaponConfig == null)
        {
            return;
        }

        RegisterWeaponConfig(weaponConfig);
        if (FindProgress(weaponProgress, weaponConfig.Id) == null)
        {
            weaponProgress.Add(CreateProgress(weaponConfig.Id));
            SaveCollectionProgress();
            NotifyCollectionChanged();
        }
    }

    public void RegisterInitialSkills(IReadOnlyList<PlayerSkillConfig> skills)
    {
        EnsureCollectionProgressInitialized();
        if (skills == null)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < skills.Count; i++)
        {
            PlayerSkillConfig skill = skills[i];
            if (skill == null)
            {
                continue;
            }

            RegisterSkillConfig(skill);
            if (FindProgress(skillProgress, skill.Id) == null)
            {
                skillProgress.Add(CreateProgress(skill.Id));
                changed = true;
            }
        }

        SyncSkillConfigsFromProgress();
        if (changed)
        {
            SaveCollectionProgress();
            NotifyCollectionChanged();
        }
    }

    public int GetWeaponLevel(ProjectileConfig weaponConfig)
    {
        EnsureCollectionProgressInitialized();
        if (weaponConfig == null)
        {
            return 0;
        }

        int level = GetLevel(weaponProgress, weaponConfig.Id);
        return level > 0 ? Mathf.Min(level, weaponConfig.MaxLevel) : 0;
    }

    public int GetSkillLevel(PlayerSkillConfig skillConfig)
    {
        EnsureCollectionProgressInitialized();
        if (skillConfig == null)
        {
            return 0;
        }

        int level = GetLevel(skillProgress, skillConfig.Id);
        return level > 0 ? Mathf.Min(level, skillConfig.MaxLevel) : 0;
    }

    public int GetDuplicateProgress(ProjectileConfig weaponConfig)
    {
        EnsureCollectionProgressInitialized();
        return weaponConfig != null && GetWeaponLevel(weaponConfig) < weaponConfig.MaxLevel
            ? GetDuplicateProgress(weaponProgress, weaponConfig.Id)
            : 0;
    }

    public int GetDuplicateProgress(PlayerSkillConfig skillConfig)
    {
        EnsureCollectionProgressInitialized();
        return skillConfig != null && GetSkillLevel(skillConfig) < skillConfig.MaxLevel
            ? GetDuplicateProgress(skillProgress, skillConfig.Id)
            : 0;
    }

    public int GetRequiredDuplicates(ProjectileConfig weaponConfig)
    {
        EnsureCollectionProgressInitialized();
        return weaponConfig != null ? GetRequiredDuplicates(GetWeaponLevel(weaponConfig), weaponConfig.MaxLevel) : 0;
    }

    public int GetRequiredDuplicates(PlayerSkillConfig skillConfig)
    {
        EnsureCollectionProgressInitialized();
        return skillConfig != null ? GetRequiredDuplicates(GetSkillLevel(skillConfig), skillConfig.MaxLevel) : 0;
    }

    public bool AddUnit(PlayerUnitConfig unitConfig)
    {
        if (unitConfig == null || unitConfigs.Contains(unitConfig))
        {
            return false;
        }

        unitConfigs.Add(unitConfig);
        OnInventoryChanged.Invoke();
        return true;
    }

    public bool ReplaceUnit(PlayerUnitConfig currentUnit, PlayerUnitConfig nextUnit)
    {
        if (currentUnit == null || nextUnit == null || currentUnit == nextUnit)
        {
            return false;
        }

        int currentIndex = unitConfigs.IndexOf(currentUnit);
        if (currentIndex < 0)
        {
            return false;
        }

        if (unitConfigs.Contains(nextUnit))
        {
            unitConfigs.RemoveAt(currentIndex);
        }
        else
        {
            unitConfigs[currentIndex] = nextUnit;
        }

        OnInventoryChanged.Invoke();
        return true;
    }

    public bool RemoveWeapon(ProjectileConfig weaponConfig)
    {
        return RemoveWeapon(weaponConfig, 1);
    }

    public bool RemoveWeapon(ProjectileConfig weaponConfig, int quantity)
    {
        EnsureCollectionProgressInitialized();
        if (weaponConfig == null || quantity <= 0)
        {
            return false;
        }

        int ownedCopies = GetWeaponQuantity(weaponConfig);
        if (ownedCopies <= 0 || quantity < ownedCopies)
        {
            return false;
        }

        // 레벨 일부를 역산하지 않고 전체 보유 제거만 지원한다.
        weaponProgress.RemoveAll(progress => progress != null && progress.configId == weaponConfig.Id);
        weaponConfigs.Remove(weaponConfig);
        weaponStacks.RemoveAll(stack => stack != null && stack.weaponConfig == weaponConfig);
        SaveCollectionProgress();
        NotifyCollectionChanged();
        return true;
    }

    public int GetWeaponQuantity(ProjectileConfig weaponConfig)
    {
        int level = GetWeaponLevel(weaponConfig);
        if (level <= 0)
        {
            return 0;
        }

        return 1 + (level - 1) * level / 2 + GetDuplicateProgress(weaponConfig);
    }

    public int GetWeaponDuplicateCount(ProjectileConfig weaponConfig)
    {
        return GetDuplicateProgress(weaponConfig);
    }

    public bool RemoveUnit(PlayerUnitConfig unitConfig)
    {
        if (unitConfig == null || !unitConfigs.Remove(unitConfig))
        {
            return false;
        }

        OnInventoryChanged.Invoke();
        return true;
    }

    public bool AddEquipmentPart(EquipmentPartInstance part)
    {
        EnsureEquipmentPartsInitialized();
        if (part == null)
        {
            return false;
        }

        equipmentParts ??= new List<EquipmentPartInstance>();
        if (string.IsNullOrWhiteSpace(part.instanceId))
        {
            part.instanceId = System.Guid.NewGuid().ToString("N");
        }

        if (FindEquipmentPart(part.instanceId) != null)
        {
            return false;
        }

        part.subStats ??= new List<EquipmentSubStat>();
        // 새로 보관되는 파츠는 인벤토리에서 확인하기 전까지 신규로 표시한다.
        part.isNew = true;
        equipmentParts.Add(part);
        SaveEquipmentParts();
        OnEquipmentPartsChanged.Invoke();
        OnInventoryChanged.Invoke();
        return true;
    }

    public void MarkEquipmentPartSeen(string instanceId)
    {
        EquipmentPartInstance part = FindEquipmentPart(instanceId);
        if (part == null || !part.isNew)
        {
            return;
        }

        // 신규 뱃지는 해당 파츠를 한 번 확인(선택)하면 해제하고 저장만 한다(목록 리빌드는 하지 않음).
        part.isNew = false;
        SaveEquipmentParts();
    }

    public bool AcquireEquipmentPart(
        EquipmentPartInstance part,
        PlayerEquipmentPartLoadout loadout,
        PlayerCurrencyWallet wallet,
        out int autoSaleCredits)
    {
        autoSaleCredits = 0;
        if (part == null)
        {
            return false;
        }

        if (!AutoSellEquipmentPartsEnabled)
        {
            return AddEquipmentPart(part);
        }

        if (part.rarity > AutoSellMaxRarity)
        {
            return AddEquipmentPart(part);
        }

        if (wallet == null)
        {
            return AddEquipmentPart(part);
        }

        // 같은 슬롯의 장착 파츠보다 낮은 등급은 보관하지 않고 즉시 판매한다.
        autoSaleCredits = Mathf.Max(0, part.salePrice);
        wallet.AddCredits(autoSaleCredits);
        return true;
    }

    public EquipmentPartInstance FindEquipmentPart(string instanceId)
    {
        EnsureEquipmentPartsInitialized();
        if (string.IsNullOrWhiteSpace(instanceId) || equipmentParts == null)
        {
            return null;
        }

        return equipmentParts.Find(part => part != null && part.instanceId == instanceId);
    }

    public EquipmentPartConfig ResolveEquipmentPartConfig(string configId)
    {
        EnsureEquipmentPartsInitialized();
        return equipmentPartConfigs.Find(config => config != null && config.Id == configId);
    }

    public void PlayEquipmentPartDropVisual(
        EquipmentPartConfig config,
        EquipmentPartInstance part,
        Vector3 dropPosition)
    {
        if (!playEquipmentPartDropVisual || part == null)
        {
            return;
        }

        // 보상은 즉시 지급하고 드롭 위치에는 수집 피드백만 풀링해서 보여준다.
        EquipmentPartDropVisual.Play(
            config != null ? config.Icon : null,
            part.rarity,
            dropPosition,
            equipmentPartCollectTarget,
            partDropPopDuration,
            partDropHoldDuration,
            partDropCollectDuration,
            partDropPopDistance,
            partDropVisualSize,
            partDropSortingOrder);
    }

    public bool RemoveEquipmentPart(string instanceId, PlayerEquipmentPartLoadout loadout = null)
    {
        EquipmentPartInstance part = FindEquipmentPart(instanceId);
        loadout ??= FindFirstObjectByType<PlayerEquipmentPartLoadout>();
        if (part == null || (loadout != null && loadout.IsEquipped(instanceId)))
        {
            return false;
        }

        equipmentParts.Remove(part);
        SaveEquipmentParts();
        OnEquipmentPartsChanged.Invoke();
        OnInventoryChanged.Invoke();
        return true;
    }

    public bool TrySellEquipmentPart(
        string instanceId,
        PlayerEquipmentPartLoadout loadout,
        ICurrencyWallet wallet)
    {
        EquipmentPartInstance part = FindEquipmentPart(instanceId);
        if (part == null || wallet == null || (loadout != null && loadout.IsEquipped(instanceId)))
        {
            return false;
        }

        if (!RemoveEquipmentPart(instanceId, loadout))
        {
            return false;
        }

        wallet?.Add(CurrencyType.Credits, Mathf.Max(0, part.salePrice));
        return true;
    }

    private void OnValidate()
    {
        NormalizeWeaponStacks();
        RemoveNullAndDuplicateUnits();
        EnsureEquipmentPartConfigs(false);
        skillConfigs ??= new List<PlayerSkillConfig>();
        skillConfigs.RemoveAll(config => config == null);
#if UNITY_EDITOR
        if (skillConfigs.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                "t:PlayerSkillConfig",
                new[] { "Assets/SO/Balance/Skills" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                PlayerSkillConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerSkillConfig>(path);
                if (config != null && !skillConfigs.Contains(config))
                {
                    skillConfigs.Add(config);
                }
            }
        }
#endif
    }

    private WeaponStack FindWeaponStack(ProjectileConfig weaponConfig)
    {
        return weaponStacks.Find(item => item != null && item.weaponConfig == weaponConfig);
    }

    private WeaponStack GetOrCreateWeaponStack(ProjectileConfig weaponConfig)
    {
        WeaponStack stack = FindWeaponStack(weaponConfig);
        if (stack != null)
        {
            return stack;
        }

        stack = new WeaponStack { weaponConfig = weaponConfig, quantity = 0 };
        weaponStacks.Add(stack);
        return stack;
    }

    private void NormalizeWeaponStacks()
    {
        weaponStacks ??= new List<WeaponStack>();

        foreach (ProjectileConfig weaponConfig in weaponConfigs)
        {
            if (weaponConfig == null || FindWeaponStack(weaponConfig) != null)
            {
                continue;
            }

            weaponStacks.Add(new WeaponStack { weaponConfig = weaponConfig, quantity = 1 });
        }

        HashSet<ProjectileConfig> seen = new HashSet<ProjectileConfig>();
        for (int i = weaponStacks.Count - 1; i >= 0; i--)
        {
            WeaponStack stack = weaponStacks[i];
            if (stack == null || stack.weaponConfig == null || !seen.Add(stack.weaponConfig))
            {
                weaponStacks.RemoveAt(i);
                continue;
            }

            stack.quantity = Mathf.Max(1, stack.quantity);
        }

        SyncWeaponConfigsFromStacks();
    }

    private void SyncWeaponConfigsFromStacks()
    {
        weaponConfigs.Clear();
        foreach (WeaponStack stack in weaponStacks)
        {
            if (stack != null && stack.weaponConfig != null && !weaponConfigs.Contains(stack.weaponConfig))
            {
                weaponConfigs.Add(stack.weaponConfig);
            }
        }

        for (int i = weaponConfigs.Count - 1; i >= 0; i--)
        {
            if (weaponConfigs[i] == null)
            {
                weaponConfigs.RemoveAt(i);
            }
        }
    }

    private void EnsureCollectionProgressInitialized()
    {
        if (collectionProgressInitialized)
        {
            return;
        }

        collectionProgressInitialized = true;
        NormalizeWeaponStacks();
        skillConfigs ??= new List<PlayerSkillConfig>();
        skillConfigs.RemoveAll(config => config == null);

        string json = PlayerPrefs.GetString(CollectionProgressKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            CollectionProgressSaveData saveData = JsonUtility.FromJson<CollectionProgressSaveData>(json);
            weaponProgress = saveData?.weapons ?? new List<CollectionProgress>();
            skillProgress = saveData?.skills ?? new List<CollectionProgress>();
            ownedDroneIds = saveData?.drones ?? new List<string>();
            NormalizeCollectionProgress();
            SyncWeaponConfigsFromProgress();
            SyncSkillConfigsFromProgress();
            return;
        }

        // 기존 Inspector 수량은 최초 보유와 중복 진행도로 한 번만 이관한다.
        weaponProgress = new List<CollectionProgress>();
        skillProgress = new List<CollectionProgress>();
        ownedDroneIds = new List<string>();
        for (int i = 0; i < weaponStacks.Count; i++)
        {
            WeaponStack stack = weaponStacks[i];
            if (stack?.weaponConfig == null)
            {
                continue;
            }

            AddCopies(
                weaponProgress,
                stack.weaponConfig,
                Mathf.Max(1, stack.quantity),
                false);
        }

        SaveCollectionProgress();
        SyncWeaponConfigsFromProgress();
        SyncSkillConfigsFromProgress();
    }

    private bool RegisterDrone(DroneConfig droneConfig, bool reportCollection)
    {
        EnsureCollectionProgressInitialized();
        if (droneConfig == null)
        {
            return false;
        }

        // 드론 ID를 기준으로 최초 해금만 저장하고 수집 업적에 반영한다.
        if (ownedDroneIds.Contains(droneConfig.Id))
        {
            if (reportCollection)
            {
                MainGuideMissionManager.ReportDroneCollected();
            }

            return false;
        }

        ownedDroneIds.Add(droneConfig.Id);
        SaveCollectionProgress();
        NotifyCollectionChanged();
        if (reportCollection)
        {
            AchievementManager.ReportDroneCollected();
            MainGuideMissionManager.ReportDroneCollected();
        }

        return true;
    }

    private int AddCopies(
        List<CollectionProgress> progressList,
        IDuplicateLevelConfig config,
        int quantity,
        bool grantMaxLevelReward = true)
    {
        CollectionProgress progress = FindProgress(progressList, config.Id);
        int remainingCopies = quantity;
        if (progress == null)
        {
            progress = CreateProgress(config.Id);
            progressList.Add(progress);
            remainingCopies--;
        }

        // 공통 계산기가 레벨 진행 후 최대 레벨 초과 중복 수량을 반환한다.
        int maxLevelDuplicates = DuplicateLevelProgression.AddDuplicates(
            config,
            ref progress.level,
            ref progress.duplicateProgress,
            remainingCopies);
        if (grantMaxLevelReward)
        {
            return GrantMaxLevelDuplicateReward(
                config.MaxLevelDuplicateCoreCrystalReward,
                maxLevelDuplicates);
        }

        return 0;
    }

    private int GrantMaxLevelDuplicateReward(int rewardPerCopy, int copyCount)
    {
        int reward = Mathf.Max(0, rewardPerCopy) * Mathf.Max(0, copyCount);
        if (reward <= 0)
        {
            return 0;
        }

        PlayerCurrencyWallet wallet = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.CurrencyWallet
            : FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
        wallet?.AddCoreCrystals(reward);
        return wallet != null ? reward : 0;
    }

    private static CollectionGrantResult BuildGrantResult(
        IDuplicateLevelConfig config,
        bool isNew,
        int previousLevel,
        int currentLevel,
        int duplicateProgress,
        int requiredDuplicates,
        int coreCrystalReward)
    {
        return new CollectionGrantResult
        {
            success = config != null,
            isNew = isNew,
            configId = config?.Id,
            previousLevel = previousLevel,
            currentLevel = currentLevel,
            duplicateProgress = duplicateProgress,
            requiredDuplicates = requiredDuplicates,
            coreCrystalReward = coreCrystalReward
        };
    }

    private void RegisterWeaponConfig(ProjectileConfig weaponConfig)
    {
        if (!weaponConfigs.Contains(weaponConfig))
        {
            weaponConfigs.Add(weaponConfig);
        }
    }

    private void RegisterSkillConfig(PlayerSkillConfig skillConfig)
    {
        if (!skillConfigs.Contains(skillConfig))
        {
            skillConfigs.Add(skillConfig);
        }
    }

    private void SyncWeaponConfigsFromProgress()
    {
        foreach (CollectionProgress progress in weaponProgress)
        {
            ProjectileConfig config = ResolveLoadedConfig<ProjectileConfig>(progress.configId);
            if (config != null && !weaponConfigs.Contains(config))
            {
                weaponConfigs.Add(config);
            }
        }

        weaponConfigs.RemoveAll(config => config == null);
        for (int i = weaponConfigs.Count - 1; i >= 0; i--)
        {
            if (FindProgress(weaponProgress, weaponConfigs[i].Id) == null)
            {
                weaponConfigs.RemoveAt(i);
            }
        }
    }

    private void SyncSkillConfigsFromProgress()
    {
        foreach (CollectionProgress progress in skillProgress)
        {
            PlayerSkillConfig config = ResolveLoadedConfig<PlayerSkillConfig>(progress.configId);
            if (config != null && !skillConfigs.Contains(config))
            {
                skillConfigs.Add(config);
            }
        }

        ownedSkillConfigs.Clear();
        for (int i = 0; i < skillConfigs.Count; i++)
        {
            PlayerSkillConfig config = skillConfigs[i];
            if (config != null
                && FindProgress(skillProgress, config.Id) != null
                && !ownedSkillConfigs.Contains(config))
            {
                ownedSkillConfigs.Add(config);
            }
        }
    }

    private static T ResolveLoadedConfig<T>(string configId) where T : ScriptableObject, IDuplicateLevelConfig
    {
        if (string.IsNullOrWhiteSpace(configId))
        {
            return null;
        }

        T[] configs = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] != null && configs[i].Id == configId)
            {
                return configs[i];
            }
        }

        return null;
    }

    private void NormalizeCollectionProgress()
    {
        NormalizeProgressList(weaponProgress);
        NormalizeProgressList(skillProgress);
        ownedDroneIds ??= new List<string>();
        HashSet<string> droneIds = new HashSet<string>();
        for (int i = ownedDroneIds.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(ownedDroneIds[i]) || !droneIds.Add(ownedDroneIds[i]))
            {
                ownedDroneIds.RemoveAt(i);
            }
        }
    }

    private static void NormalizeProgressList(List<CollectionProgress> progressList)
    {
        progressList ??= new List<CollectionProgress>();
        HashSet<string> ids = new HashSet<string>();
        for (int i = progressList.Count - 1; i >= 0; i--)
        {
            CollectionProgress progress = progressList[i];
            if (progress == null || string.IsNullOrWhiteSpace(progress.configId) || !ids.Add(progress.configId))
            {
                progressList.RemoveAt(i);
                continue;
            }

            progress.level = Mathf.Max(1, progress.level);
            progress.duplicateProgress = Mathf.Max(0, progress.duplicateProgress);
        }
    }

    private static CollectionProgress CreateProgress(string configId)
    {
        return new CollectionProgress
        {
            configId = configId,
            level = 1,
            duplicateProgress = 0
        };
    }

    private static CollectionProgress FindProgress(List<CollectionProgress> progressList, string configId)
    {
        return progressList?.Find(item => item != null && item.configId == configId);
    }

    private static int GetLevel(List<CollectionProgress> progressList, string configId)
    {
        CollectionProgress progress = FindProgress(progressList, configId);
        return progress != null ? Mathf.Max(1, progress.level) : 0;
    }

    private static int GetDuplicateProgress(List<CollectionProgress> progressList, string configId)
    {
        CollectionProgress progress = FindProgress(progressList, configId);
        return progress != null ? Mathf.Max(0, progress.duplicateProgress) : 0;
    }

    private static int GetRequiredDuplicates(int level, int maxLevel)
    {
        return DuplicateLevelProgression.GetRequiredDuplicates(level, maxLevel);
    }

    private void SaveCollectionProgress()
    {
        if (!saveCollectionProgressToPlayerPrefs)
        {
            return;
        }

        CollectionProgressSaveData saveData = new CollectionProgressSaveData
        {
            weapons = weaponProgress,
            skills = skillProgress,
            drones = ownedDroneIds
        };
        PlayerPrefs.SetString(CollectionProgressKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    private void NotifyCollectionChanged()
    {
        AchievementManager.ReportWeaponCollected();
        OnCollectionProgressChanged.Invoke();
        OnInventoryChanged.Invoke();
    }

    private void RemoveNullAndDuplicateUnits()
    {
        HashSet<PlayerUnitConfig> seen = new HashSet<PlayerUnitConfig>();
        for (int i = unitConfigs.Count - 1; i >= 0; i--)
        {
            PlayerUnitConfig item = unitConfigs[i];
            if (item == null || !seen.Add(item))
            {
                unitConfigs.RemoveAt(i);
            }
        }
    }

    private void EnsureEquipmentPartConfigs(bool allowRuntimeDefaults = true)
    {
        equipmentPartConfigs ??= new List<EquipmentPartConfig>();
        equipmentPartConfigs.RemoveAll(config => config == null);

#if UNITY_EDITOR
        if (equipmentPartConfigs.Count == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                "t:EquipmentPartConfig",
                new[] { "Assets/SO/Balance/EquipmentParts" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                EquipmentPartConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentPartConfig>(path);
                if (config != null && !equipmentPartConfigs.Contains(config))
                {
                    equipmentPartConfigs.Add(config);
                }
            }
        }
#endif

        if (equipmentPartConfigs.Count > 0 || !allowRuntimeDefaults)
        {
            return;
        }

        // 별도 SO 연결 전에도 드롭 테스트가 가능하도록 기본 3종을 런타임에 보강한다.
        equipmentPartConfigs.Add(CreateRuntimeConfig("part_armor_default", "전술 장갑", EquipmentPartSlot.Armor, 0.05f, 0.1f, 0.2f, 0.3f));
        equipmentPartConfigs.Add(CreateRuntimeConfig("part_engine_default", "고속 엔진", EquipmentPartSlot.Engine, 0.03f, 0.06f, 0.12f, 0.18f));
        equipmentPartConfigs.Add(CreateRuntimeConfig("part_chip_default", "화력 칩", EquipmentPartSlot.Chip, 0.05f, 0.1f, 0.2f, 0.3f));
    }

    private void EnsureEquipmentPartsInitialized()
    {
        if (equipmentPartsInitialized)
        {
            return;
        }

        equipmentPartsInitialized = true;
        EnsureEquipmentPartConfigs();
        LoadEquipmentParts();
    }

    private static EquipmentPartConfig CreateRuntimeConfig(
        string id,
        string displayName,
        EquipmentPartSlot slot,
        float commonValue,
        float rareValue,
        float epicValue,
        float legendaryValue)
    {
        EquipmentPartConfig config = ScriptableObject.CreateInstance<EquipmentPartConfig>();
        config.ConfigureRuntimeDefaults(id, displayName, slot, commonValue, rareValue, epicValue, legendaryValue);
        return config;
    }

    private void LoadEquipmentParts()
    {
        equipmentParts ??= new List<EquipmentPartInstance>();
        if (!saveEquipmentPartsToPlayerPrefs)
        {
            NormalizeEquipmentParts();
            return;
        }

        string json = PlayerPrefs.GetString(EquipmentPartsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            EquipmentPartSaveData saveData = JsonUtility.FromJson<EquipmentPartSaveData>(json);
            equipmentParts = saveData?.parts ?? new List<EquipmentPartInstance>();
        }

        NormalizeEquipmentParts();
    }

    private void EnsureAutoSellSettingsInitialized()
    {
        if (autoSellSettingsInitialized)
        {
            return;
        }

        autoSellSettingsInitialized = true;
        if (PlayerPrefs.HasKey(EquipmentAutoSellEnabledKey))
        {
            autoSellEquipmentPartsEnabled = PlayerPrefs.GetInt(EquipmentAutoSellEnabledKey, 0) != 0;
        }
        else
        {
            autoSellEquipmentPartsEnabled = DefaultEquipmentAutoSellEnabled;
        }

        if (PlayerPrefs.HasKey(EquipmentAutoSellMaxRarityKey))
        {
            autoSellMaxRarity = ClampAutoSellMaxRarity(PlayerPrefs.GetInt(
                EquipmentAutoSellMaxRarityKey,
                (int)DefaultEquipmentAutoSellMaxRarity));
        }
        else
        {
            autoSellMaxRarity = DefaultEquipmentAutoSellMaxRarity;
        }
    }

    private void SaveAutoSellSettings()
    {
        if (!saveEquipmentPartsToPlayerPrefs)
        {
            return;
        }

        PlayerPrefs.SetInt(EquipmentAutoSellEnabledKey, autoSellEquipmentPartsEnabled ? 1 : 0);
        PlayerPrefs.SetInt(EquipmentAutoSellMaxRarityKey, (int)autoSellMaxRarity);
        PlayerPrefs.Save();
    }

    private static EquipmentPartRarity ClampAutoSellMaxRarity(int value)
    {
        if (value < (int)EquipmentPartRarity.Common)
        {
            return EquipmentPartRarity.Common;
        }

        if (value > (int)EquipmentPartRarity.Epic)
        {
            return EquipmentPartRarity.Epic;
        }

        return (EquipmentPartRarity)value;
    }

    private static EquipmentPartRarity ClampAutoSellMaxRarity(EquipmentPartRarity value)
    {
        return ClampAutoSellMaxRarity((int)value);
    }

    private void NormalizeEquipmentParts()
    {
        equipmentParts ??= new List<EquipmentPartInstance>();
        HashSet<string> instanceIds = new HashSet<string>();
        for (int i = equipmentParts.Count - 1; i >= 0; i--)
        {
            EquipmentPartInstance part = equipmentParts[i];
            if (part == null)
            {
                equipmentParts.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(part.instanceId) || !instanceIds.Add(part.instanceId))
            {
                part.instanceId = System.Guid.NewGuid().ToString("N");
                instanceIds.Add(part.instanceId);
            }

            part.level = Mathf.Max(1, part.level);
            part.mainStatValue = Mathf.Max(0f, part.mainStatValue);
            part.salePrice = Mathf.Max(0, part.salePrice);
            part.subStats ??= new List<EquipmentSubStat>();
        }
    }

    private void SaveEquipmentParts()
    {
        if (!saveEquipmentPartsToPlayerPrefs)
        {
            return;
        }

        EquipmentPartSaveData saveData = new EquipmentPartSaveData { parts = equipmentParts };
        PlayerPrefs.SetString(EquipmentPartsKey, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }
}
