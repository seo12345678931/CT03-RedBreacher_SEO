using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CoreCharger : MonoBehaviour, IBaseCampFacility
{
    private const string FacilityId = "core_charger";
    [Serializable]
    public class UnitConversionStage
    {
        [Min(1)] public int requiredPlayerLevel = 5;
        public PlayerUnitConfig currentUnit;
        public PlayerUnitConfig nextUnit;
        [Min(1)] public int requiredCrystalCoreCostUnit = 5;

        public bool IsConfigured => currentUnit != null && nextUnit != null && currentUnit != nextUnit;
    }

    [Serializable]
    public class DroneUnlock
    {
        public DroneConfig droneConfig;
        [Min(1)] public int requiredCoreChargerLevel = 2;
        [Min(1)] public int requiredCrystalCoreCostDrone = 5;
    }

    [Header("Level")]
    [SerializeField] private int level = 1;
    private int maxLevel = 1;

    [Header("Unit SO Conversion")]
    [SerializeField, Min(1)] private int levelsPerConversion = 5;
    [SerializeField] private List<UnitConversionStage> conversionStages = new List<UnitConversionStage>();
    [SerializeField] private List<int> convertedStageIndices = new List<int>();

    [Header("Drone Enhancement")]
    [SerializeField] private List<DroneUnlock> droneUnlocks = new List<DroneUnlock>();

    [Header("Events")]
    public UnityEvent<int> OnLevelChanged = new UnityEvent<int>();
    public UnityEvent<PlayerUnitConfig, int> OnUnitEnhanced = new UnityEvent<PlayerUnitConfig, int>();
    public UnityEvent OnUpgradeStarted = new UnityEvent();
    public UnityEvent OnUpgradeCompleted = new UnityEvent();

    private bool isUpgrading;
    private bool balanceReady;
    private float upgradeRemainingSeconds;
    private float currentUpgradeDurationSeconds;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public int UpgradeCost => GetUpgradeCostForCurrentLevel();
    public int RequiredCommanderLevel => GetCurrentBalance()?.requiredCommanderLevel ?? int.MaxValue;
    public int RequiredResearchLabLevel => GetCurrentBalance()?.requiredCommandCenterLevel ?? int.MaxValue;
    public bool IsUpgrading => isUpgrading;
    public float UpgradeRemainingSeconds => upgradeRemainingSeconds;
    public float CurrentUpgradeDurationSeconds => currentUpgradeDurationSeconds;
    public int CompletedConversionCount => convertedStageIndices.Count;
    public int CurrentStageIndex => FindFirstIncompleteStage();
    public IReadOnlyList<UnitConversionStage> ConversionStages => conversionStages;
    public UnitConversionStage CurrentConversionStage => GetConversionStageAt(CurrentStageIndex);
    public IReadOnlyList<DroneUnlock> DroneUnlocks => droneUnlocks;
    public float DroneAttackDamageBonus => Mathf.Max(0f, GetCurrentBalance()?.droneAttackDamageBonus ?? 0f);
    public float DroneAttackRangeBonus => Mathf.Max(0f, GetCurrentBalance()?.droneAttackRangeBonus ?? 0f);
    public float DroneAttackIntervalReduction => Mathf.Max(0f, GetCurrentBalance()?.droneAttackIntervalReduction ?? 0f);
    public float DroneFollowSpeedBonus => Mathf.Max(0f, GetCurrentBalance()?.droneFollowSpeedBonus ?? 0f);
    public int CurrentUnitConversionCoreCost => GetUnitConversionCoreCost(CurrentConversionStage);

    private void Awake()
    {
        EnsureBalanceInitialized();
        Normalize();
    }

    // 비활성 시설이 Awake 전에 RestoreState되어 maxLevel(기본 1)로 레벨이 클램프되는 문제 방지.
    private void EnsureBalanceInitialized()
    {
        if (balanceReady)
        {
            return;
        }

        BaseCampBalanceConfig config = BaseCampBalanceConfig.Current;
        string error = "기지 밸런스 설정을 찾을 수 없습니다.";
        if (config != null && config.ValidateFacility(FacilityId, out maxLevel, out error))
        {
            balanceReady = true;
        }
        else
        {
            Debug.LogError($"코어 강화소 밸런스 초기화 실패: {error}", this);
        }
    }

    private void Update()
    {
        TickUpgrade(Time.deltaTime);
    }

    public bool IsConversionCompleted(int stageIndex)
    {
        return convertedStageIndices.Contains(stageIndex);
    }

    public bool CanConvertCurrentUnit(
        InventoryFacility inventory,
        PlayerController player,
        int playerLevel,
        PlayerCurrencyWallet wallet = null)
    {
        int stageIndex = CurrentStageIndex;
        UnitConversionStage stage = GetConversionStageAt(stageIndex);
        if (stage == null || !stage.IsConfigured)
        {
            return false;
        }

        bool ownsCurrentUnit = inventory != null && inventory.ContainsUnit(stage.currentUnit);
        bool hasCurrentUnitEquipped = player != null && player.UnitConfig == stage.currentUnit;
        int requiredCoreChargerLevel = GetRequiredCoreChargerLevel(stageIndex);
        return playerLevel >= stage.requiredPlayerLevel
            && level >= requiredCoreChargerLevel
            && (ownsCurrentUnit || hasCurrentUnitEquipped)
            && CanSpendCoreCrystals(wallet, GetUnitConversionCoreCost(stage));
    }

    public bool TryConvertCurrentUnit(
        InventoryFacility inventory,
        PlayerController player,
        int playerLevel,
        PlayerCurrencyWallet wallet = null)
    {
        if (!CanConvertCurrentUnit(inventory, player, playerLevel, wallet))
        {
            return false;
        }

        int stageIndex = CurrentStageIndex;
        UnitConversionStage stage = conversionStages[stageIndex];
        int coreCrystalCost = GetUnitConversionCoreCost(stage);
        if (!TrySpendCoreCrystals(wallet, coreCrystalCost))
        {
            return false;
        }

        bool inventoryChanged = inventory != null && inventory.ReplaceUnit(stage.currentUnit, stage.nextUnit);

        if (!inventoryChanged && inventory != null && !inventory.ContainsUnit(stage.nextUnit))
        {
            inventory.AddUnit(stage.nextUnit);
        }

        // 코어 변환은 현재 전투 유닛을 다음 단계 SO로 즉시 교체해 런타임 스탯/프리팹에 반영한다.
        player?.SetUnitConfig(stage.nextUnit);

        convertedStageIndices.Add(stageIndex);
        Normalize();
        OnUnitEnhanced.Invoke(stage.nextUnit, stageIndex + 1);
        OnLevelChanged.Invoke(Level);
        return true;
    }

    public void ApplyCompletedConversions(InventoryFacility inventory, PlayerController player)
    {
        if (inventory == null)
        {
            return;
        }

        List<int> completedStages = new List<int>(convertedStageIndices);
        completedStages.Sort();

        foreach (int stageIndex in completedStages)
        {
            UnitConversionStage stage = GetConversionStageAt(stageIndex);
            if (stage == null || !stage.IsConfigured)
            {
                continue;
            }

            bool wasEquipped = player != null && player.UnitConfig == stage.currentUnit;
            inventory.ReplaceUnit(stage.currentUnit, stage.nextUnit);
            if (wasEquipped)
            {
                player.SetUnitConfig(stage.nextUnit);
            }
        }
    }

    public JinyouCoreChargerSaveData CaptureState()
    {
        JinyouCoreChargerSaveData data = new JinyouCoreChargerSaveData
        {
            level = level,
            isUpgrading = isUpgrading,
            upgradeRemainingSeconds = upgradeRemainingSeconds,
            currentUpgradeDurationSeconds = currentUpgradeDurationSeconds
        };
        data.convertedStageIndices.AddRange(convertedStageIndices);
        return data;
    }

    public void RestoreState(JinyouCoreChargerSaveData data)
    {
        EnsureBalanceInitialized();
        level = Mathf.Clamp(data?.level ?? 1, 1, maxLevel);
        isUpgrading = data != null && data.isUpgrading;
        upgradeRemainingSeconds = Mathf.Max(0f, data?.upgradeRemainingSeconds ?? 0f);
        currentUpgradeDurationSeconds = Mathf.Max(0f, data?.currentUpgradeDurationSeconds ?? 0f);
        convertedStageIndices = data?.convertedStageIndices != null
            ? new List<int>(data.convertedStageIndices)
            : new List<int>();

        Normalize();
        OnLevelChanged.Invoke(Level);
    }

    public int GetRequiredCoreChargerLevel(int stageIndex)
    {
        return Mathf.Clamp(stageIndex + 2, 2, maxLevel);
    }

    public DroneUnlock GetNextLockedDroneUnlock(InventoryFacility inventory)
    {
        if (droneUnlocks == null)
        {
            return null;
        }

        foreach (DroneUnlock droneUnlock in droneUnlocks)
        {
            if (droneUnlock?.droneConfig == null)
            {
                continue;
            }

            if (inventory == null || !inventory.ContainsDrone(droneUnlock.droneConfig))
            {
                return droneUnlock;
            }
        }

        return null;
    }

    public int GetDroneUnlockCoreCost(DroneUnlock droneUnlock)
    {
        return Mathf.Max(0, droneUnlock?.requiredCrystalCoreCostDrone ?? 0);
    }

    public bool CanUnlockNextDrone(InventoryFacility inventory, PlayerCurrencyWallet wallet = null)
    {
        DroneUnlock nextUnlock = GetNextLockedDroneUnlock(inventory);
        return nextUnlock?.droneConfig != null
            && inventory != null
            && level >= Mathf.Max(1, nextUnlock.requiredCoreChargerLevel)
            && CanSpendCoreCrystals(wallet, GetDroneUnlockCoreCost(nextUnlock));
    }

    public bool TryUnlockNextDrone(InventoryFacility inventory, PlayerCurrencyWallet wallet = null)
    {
        if (!CanUnlockNextDrone(inventory, wallet))
        {
            return false;
        }

        DroneUnlock nextUnlock = GetNextLockedDroneUnlock(inventory);
        if (nextUnlock?.droneConfig == null)
        {
            return false;
        }

        if (!TrySpendCoreCrystals(wallet, GetDroneUnlockCoreCost(nextUnlock)))
        {
            return false;
        }

        return inventory.AddDrone(nextUnlock.droneConfig);
    }

    public bool CanUpgrade(int credits, int commanderLevel)
    {
        return balanceReady
            && !isUpgrading
            && level < maxLevel
            && credits >= UpgradeCost
            && commanderLevel >= RequiredCommanderLevel;
    }

    public int GetLevelLimit(int researchLabLevel)
    {
        return maxLevel;
    }

    public bool CanStartUpgrade(int credits, int commanderLevel, int researchLabLevel)
    {
        return CanUpgrade(credits, commanderLevel)
            && researchLabLevel >= RequiredResearchLabLevel
            && level < GetLevelLimit(researchLabLevel);
    }

    public bool TryStartUpgrade(ref int availableCredits, int commanderLevel, int researchLabLevel)
    {
        if (!CanStartUpgrade(availableCredits, commanderLevel, researchLabLevel))
        {
            return false;
        }

        availableCredits -= UpgradeCost;
        StartUpgradeTimer();
        return true;
    }

    public void Upgrade()
    {
        if (isUpgrading)
        {
            CompleteUpgrade();
            return;
        }

        if (level >= maxLevel)
        {
            return;
        }

        OnUpgradeStarted.Invoke();
        CompleteUpgrade();
    }

    public void AdvanceUpgradeOffline(float elapsedSeconds)
    {
        TickUpgrade(Mathf.Max(0f, elapsedSeconds));
    }

    private UnitConversionStage GetConversionStageAt(int index)
    {
        if (index < 0 || index >= conversionStages.Count)
        {
            return null;
        }

        return conversionStages[index];
    }

    private int FindFirstIncompleteStage()
    {
        for (int i = 0; i < conversionStages.Count; i++)
        {
            if (!convertedStageIndices.Contains(i))
            {
                return i;
            }
        }

        return -1;
    }

    private void StartUpgradeTimer()
    {
        OnUpgradeStarted.Invoke();
        currentUpgradeDurationSeconds = GetUpgradeDurationForCurrentLevel();

        if (currentUpgradeDurationSeconds <= 0f)
        {
            CompleteUpgrade();
            return;
        }

        isUpgrading = true;
        upgradeRemainingSeconds = currentUpgradeDurationSeconds;
    }

    private void TickUpgrade(float deltaTime)
    {
        if (!isUpgrading)
        {
            return;
        }

        upgradeRemainingSeconds -= deltaTime;
        if (upgradeRemainingSeconds <= 0f)
        {
            CompleteUpgrade();
        }
    }

    private void CompleteUpgrade()
    {
        if (level >= maxLevel)
        {
            isUpgrading = false;
            upgradeRemainingSeconds = 0f;
            return;
        }

        isUpgrading = false;
        upgradeRemainingSeconds = 0f;
        currentUpgradeDurationSeconds = 0f;
        level++;
        OnLevelChanged.Invoke(level);
        AchievementManager.ReportFacilityUpgraded();
        OnUpgradeCompleted.Invoke();
    }

    private int GetUpgradeCostForCurrentLevel()
    {
        if (level >= maxLevel)
        {
            return 0;
        }

        return Mathf.Max(0, GetCurrentBalance()?.upgradeCost ?? 0);
    }

    private static int GetUnitConversionCoreCost(UnitConversionStage stage)
    {
        return Mathf.Max(0, stage?.requiredCrystalCoreCostUnit ?? 0);
    }

    private static bool CanSpendCoreCrystals(PlayerCurrencyWallet wallet, int cost)
    {
        cost = Mathf.Max(0, cost);
        return cost <= 0 || (wallet != null && wallet.CanSpend(CurrencyType.CoreCrystals, cost));
    }

    private static bool TrySpendCoreCrystals(PlayerCurrencyWallet wallet, int cost)
    {
        cost = Mathf.Max(0, cost);
        return cost <= 0 || (wallet != null && wallet.TrySpendCoreCrystals(cost));
    }

    private float GetUpgradeDurationForCurrentLevel()
    {
        return Mathf.Max(0f, GetCurrentBalance()?.upgradeSeconds ?? 0f);
    }

    private BaseCampBalanceConfig.FacilityLevelData GetCurrentBalance()
    {
        return BaseCampBalanceConfig.Current?.GetLevel(FacilityId, level);
    }

    private void Normalize()
    {
        level = balanceReady ? Mathf.Clamp(level, 1, maxLevel) : Mathf.Max(1, level);

        levelsPerConversion = Mathf.Max(1, levelsPerConversion);
        conversionStages ??= new List<UnitConversionStage>();
        convertedStageIndices ??= new List<int>();

        for (int i = 0; i < conversionStages.Count; i++)
        {
            UnitConversionStage stage = conversionStages[i];
            if (stage != null)
            {
                stage.requiredPlayerLevel = (i + 1) * levelsPerConversion;
                stage.requiredCrystalCoreCostUnit = Mathf.Max(0, stage.requiredCrystalCoreCostUnit);
            }
        }

        convertedStageIndices.RemoveAll(index => index < 0 || index >= conversionStages.Count);
        convertedStageIndices.Sort();
        for (int i = convertedStageIndices.Count - 1; i > 0; i--)
        {
            if (convertedStageIndices[i] == convertedStageIndices[i - 1])
            {
                convertedStageIndices.RemoveAt(i);
            }
        }

        droneUnlocks ??= new List<DroneUnlock>();
        foreach (DroneUnlock droneUnlock in droneUnlocks)
        {
            if (droneUnlock != null)
            {
                droneUnlock.requiredCoreChargerLevel = Mathf.Max(1, droneUnlock.requiredCoreChargerLevel);
                droneUnlock.requiredCrystalCoreCostDrone = Mathf.Max(0, droneUnlock.requiredCrystalCoreCostDrone);
            }
        }
    }

    private void OnValidate()
    {
        Normalize();
    }
}
