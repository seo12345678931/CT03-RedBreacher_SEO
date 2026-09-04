using UnityEngine;
using UnityEngine.Events;

public class CreditRefinery : MonoBehaviour, IBaseCampFacility
{
    private const string FacilityId = "energy_refinery";

    [Header("Level")]
    [SerializeField] private int level = 1;
    private int maxLevel = 1;

    [Header("Production")]
    [SerializeField] private int storedCredits;
    [SerializeField] private bool produceWhilePlaying = true;

    [Header("Events")]
    public UnityEvent<int> OnCreditsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnLevelChanged = new UnityEvent<int>();
    public UnityEvent OnUpgradeStarted = new UnityEvent();
    public UnityEvent OnUpgradeCompleted = new UnityEvent();

    private float productionBuffer;
    private bool isUpgrading;
    private bool balanceReady;
    private float upgradeRemainingSeconds;
    private float currentUpgradeDurationSeconds;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public int StoredCredits => storedCredits;
    public int StorageCapacity => GetStorageCapacityForCurrentLevel();
    public int UpgradeCost => GetUpgradeCostForCurrentLevel();
    public int RequiredCommanderLevel => GetCurrentBalance()?.requiredCommanderLevel ?? int.MaxValue;
    public int RequiredResearchLabLevel => GetRequiredResearchLabLevelForCurrentUpgrade();
    public bool IsUpgrading => isUpgrading;
    public float UpgradeRemainingSeconds => upgradeRemainingSeconds;
    public float CurrentUpgradeDurationSeconds => currentUpgradeDurationSeconds;
    public float CreditsPerMinute => GetCreditsPerMinuteForCurrentLevel();
    public bool IsStorageFull => storedCredits >= StorageCapacity;

    private void Awake()
    {
        EnsureBalanceInitialized();
        level = Mathf.Clamp(level, 1, maxLevel);
        storedCredits = Mathf.Clamp(storedCredits, 0, StorageCapacity);
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
            Debug.LogError($"자원 정제소 밸런스 초기화 실패: {error}", this);
        }
    }

    private void Update()
    {
        if (produceWhilePlaying)
        {
            Produce(Time.deltaTime);
        }

        TickUpgrade(Time.deltaTime);
    }

    public void Produce(float deltaTime)
    {
        productionBuffer += CreditsPerMinute / 60f * deltaTime;
        int amount = Mathf.FloorToInt(productionBuffer);

        if (amount <= 0)
        {
            return;
        }

        productionBuffer -= amount;
        storedCredits = Mathf.Clamp(storedCredits + amount, 0, StorageCapacity);
        OnCreditsChanged.Invoke(storedCredits);
    }

    public int CollectCredits()
    {
        int amount = storedCredits;
        storedCredits = 0;
        OnCreditsChanged.Invoke(storedCredits);
        return amount;
    }

    public JinyouEnergyRefinerySaveData CaptureState()
    {
        return new JinyouEnergyRefinerySaveData
        {
            level = level,
            storedCredits = storedCredits,
            isUpgrading = isUpgrading,
            upgradeRemainingSeconds = upgradeRemainingSeconds,
            currentUpgradeDurationSeconds = currentUpgradeDurationSeconds
        };
    }

    public void RestoreState(JinyouEnergyRefinerySaveData data)
    {
        if (data == null)
        {
            return;
        }

        EnsureBalanceInitialized();
        level = Mathf.Clamp(data.level, 1, maxLevel);
        storedCredits = Mathf.Clamp(data.storedCredits, 0, StorageCapacity);
        isUpgrading = data.isUpgrading;
        upgradeRemainingSeconds = Mathf.Max(0f, data.upgradeRemainingSeconds);
        currentUpgradeDurationSeconds = Mathf.Max(0f, data.currentUpgradeDurationSeconds);
        OnLevelChanged.Invoke(level);
        OnCreditsChanged.Invoke(storedCredits);
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

    public bool CanStartUpgrade(int availableCredits, int commanderLevel, int researchLabLevel)
    {
        return CanUpgrade(availableCredits, commanderLevel)
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

    public void CompleteUpgradeImmediately()
    {
        Upgrade();
    }

    public void AdvanceUpgradeOffline(float elapsedSeconds)
    {
        TickUpgrade(Mathf.Max(0f, elapsedSeconds));
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
        storedCredits = Mathf.Clamp(storedCredits, 0, StorageCapacity);
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

    private float GetCreditsPerMinuteForCurrentLevel()
    {
        return Mathf.Max(0f, GetCurrentBalance()?.creditsPerMinute ?? 0f);
    }

    private int GetStorageCapacityForCurrentLevel()
    {
        return Mathf.Max(1, GetCurrentBalance()?.storageCapacity ?? 1);
    }

    private float GetUpgradeDurationForCurrentLevel()
    {
        return Mathf.Max(0f, GetCurrentBalance()?.upgradeSeconds ?? 0f);
    }

    private int GetRequiredResearchLabLevelForCurrentUpgrade()
    {
        return GetCurrentBalance()?.requiredCommandCenterLevel ?? int.MaxValue;
    }

    private BaseCampBalanceConfig.FacilityLevelData GetCurrentBalance()
    {
        return BaseCampBalanceConfig.Current?.GetLevel(FacilityId, level);
    }

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        storedCredits = Mathf.Max(0, storedCredits);
    }
}
