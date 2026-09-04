using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CommandCenter : MonoBehaviour, IBaseCampFacility
{
    private const string FacilityId = "command_center";

    [Serializable]
    public class FacilityUnlock
    {
        public string facilityId;
        public string displayName;
        public int requiredLabLevel = 1;
        public bool unlocked;
    }

    [Header("Level")]
    [SerializeField] private int level = 1;
    private int maxLevel = 1;

    [Header("Runtime State")]
    [SerializeField] private int bossTickets = 1;

    [Header("Facility Unlocks")]
    [SerializeField] private List<FacilityUnlock> facilityUnlocks = new List<FacilityUnlock>();

    [Header("Events")]
    public UnityEvent<int> OnLevelChanged = new UnityEvent<int>();
    public UnityEvent<int> OnBossTicketsChanged = new UnityEvent<int>();
    public UnityEvent<string> OnFacilityUnlocked = new UnityEvent<string>();
    public UnityEvent OnUpgradeStarted = new UnityEvent();
    public UnityEvent OnUpgradeCompleted = new UnityEvent();

    private bool isUpgrading;
    private bool balanceReady;
    private int bossTicketCapacity;
    private float upgradeRemainingSeconds;
    private float currentUpgradeDurationSeconds;
    private float bossTicketProductionSeconds;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public int UpgradeCost => level < maxLevel
        ? Mathf.Max(0, GetCurrentBalance()?.upgradeCost ?? 0)
        : 0;
    public int RequiredCommanderLevel => GetRequiredCommanderLevelForCurrentUpgrade();
    public int RequiredResearchLabLevel => 1;
    public float OfflineRewardLimitHours => Mathf.Max(0f, GetCurrentBalance()?.offlineRewardHours ?? 0f);
    public float TicketOfflineLimitHours => Mathf.Max(0f, GetCurrentBalance()?.ticketOfflineLimitHours ?? 0f);
    public float BossTicketChargeSeconds => BossTicketProductionDaySeconds / Mathf.Max(1, BossTicketsProducedPerDay);
    public float BossTicketProductionDaySeconds => Mathf.Max(0f, GetCurrentBalance()?.ticketProductionDaySeconds ?? 0f);
    public int BossTicketsProducedPerDay => Mathf.Max(0, GetCurrentBalance()?.ticketsPerDay ?? 0);
    public float BossTicketProductionProgress => BossTicketChargeSeconds > 0f
        ? Mathf.Clamp01(bossTicketProductionSeconds / BossTicketChargeSeconds)
        : 1f;
    public int BossTicketCapacity => bossTicketCapacity;
    public int BossTickets => bossTickets;
    public bool IsUpgrading => isUpgrading;
    public float UpgradeRemainingSeconds => upgradeRemainingSeconds;
    public float CurrentUpgradeDurationSeconds => currentUpgradeDurationSeconds;
    public IReadOnlyList<FacilityUnlock> FacilityUnlocks => facilityUnlocks;

    public int GetBossTicketCapacityForLevel(int targetLevel)
    {
        EnsureBalanceInitialized();
        return Mathf.Max(0, GetBalanceForLevel(targetLevel)?.ticketCapacity ?? 0);
    }

    public int GetBossTicketsProducedPerDayForLevel(int targetLevel)
    {
        EnsureBalanceInitialized();
        return Mathf.Max(0, GetBalanceForLevel(targetLevel)?.ticketsPerDay ?? 0);
    }

    private void Awake()
    {
        EnsureBalanceInitialized();
        if (balanceReady)
        {
            level = Mathf.Clamp(level, 1, maxLevel);
            ApplyLevelBalance();
            NormalizeFacilityUnlocks();
        }
    }

    // maxLevel 등 밸런스 값을 설정에서 1회 로드한다. 비활성 시설(닫힌 팝업 내부)이
    // BaseCampManager.Start에서 RestoreState될 때 Awake가 아직 실행되지 않아 maxLevel이
    // 기본값(1)로 남아 저장 레벨이 1로 클램프되는 문제를 방지한다.
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
            Debug.LogError($"사령부 밸런스 초기화 실패: {error}", this);
        }
    }

    private void Start()
    {
        RefreshUnlocks();
    }

    private void Update()
    {
        TickUpgrade(Time.deltaTime);
        TickBossTicketProduction(Time.deltaTime);
    }

    public bool IsFacilityUnlocked(string facilityId)
    {
        NormalizeFacilityUnlocks();

        FacilityUnlock facility = facilityUnlocks.Find(item => item.facilityId == facilityId);
        if (facility == null)
        {
            return false;
        }

        facility.unlocked = level >= facility.requiredLabLevel;
        return facility.unlocked;
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

    public bool CanStartUpgrade(int availableCredits, int commanderLevel)
    {
        return CanUpgrade(availableCredits, commanderLevel);
    }

    public bool CanStartUpgrade(int availableCredits, int commanderLevel, int researchLabLevel)
    {
        return CanStartUpgrade(availableCredits, commanderLevel);
    }

    public bool TryStartUpgrade(ref int availableCredits, int commanderLevel)
    {
        return TryStartUpgrade(ref availableCredits, commanderLevel, Level);
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

    public bool TryUseBossTicket()
    {
        if (bossTickets <= 0)
        {
            return false;
        }

        bossTickets--;
        OnBossTicketsChanged.Invoke(bossTickets);
        return true;
    }

    public void RefundBossTicket()
    {
        // 소환 시작에 실패한 경우에만 이미 차감한 티켓을 복구한다.
        AddBossTicket();
    }

    public void AddBossTickets(int amount)
    {
        int positiveAmount = Mathf.Max(0, amount);
        if (positiveAmount == 0)
        {
            return;
        }

        bossTickets = Mathf.Max(0, bossTickets + positiveAmount);
        OnBossTicketsChanged.Invoke(bossTickets);
    }

    public int ProduceBossTicketsOffline(float elapsedSeconds)
    {
        if (!balanceReady || elapsedSeconds <= 0f || bossTickets >= bossTicketCapacity)
        {
            return 0;
        }

        int beforeTickets = bossTickets;
        float chargeSeconds = BossTicketChargeSeconds;
        if (chargeSeconds <= 0f)
        {
            bossTickets = bossTicketCapacity;
            bossTicketProductionSeconds = 0f;
        }
        else
        {
            bossTicketProductionSeconds += elapsedSeconds;
            while (bossTicketProductionSeconds >= chargeSeconds && bossTickets < bossTicketCapacity)
            {
                bossTicketProductionSeconds -= chargeSeconds;
                bossTickets++;
            }

        }

        int addedTickets = bossTickets - beforeTickets;
        if (addedTickets > 0)
        {
            OnBossTicketsChanged.Invoke(bossTickets);
        }

        return addedTickets;
    }

    public JinyouCommandCenterSaveData CaptureState()
    {
        return new JinyouCommandCenterSaveData
        {
            level = level,
            bossTickets = bossTickets,
            bossTicketProductionSeconds = bossTicketProductionSeconds,
            isUpgrading = isUpgrading,
            upgradeRemainingSeconds = upgradeRemainingSeconds,
            currentUpgradeDurationSeconds = currentUpgradeDurationSeconds
        };
    }

    public void RestoreState(JinyouCommandCenterSaveData data)
    {
        if (data == null)
        {
            return;
        }

        EnsureBalanceInitialized();
        level = Mathf.Clamp(data.level, 1, maxLevel);
        ApplyLevelBalance();
        bossTickets = Mathf.Max(0, data.bossTickets);
        bossTicketProductionSeconds = Mathf.Max(0f, data.bossTicketProductionSeconds);
        isUpgrading = data.isUpgrading;
        upgradeRemainingSeconds = Mathf.Max(0f, data.upgradeRemainingSeconds);
        currentUpgradeDurationSeconds = Mathf.Max(0f, data.currentUpgradeDurationSeconds);
        RefreshUnlocks();
        OnLevelChanged.Invoke(level);
        OnBossTicketsChanged.Invoke(bossTickets);
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

    private void TickBossTicketProduction(float deltaTime)
    {
        if (!balanceReady || bossTickets >= bossTicketCapacity)
        {
            return;
        }

        float chargeSeconds = BossTicketChargeSeconds;
        if (chargeSeconds <= 0f)
        {
            AddBossTicket();
            return;
        }

        bossTicketProductionSeconds += deltaTime;

        while (bossTicketProductionSeconds >= chargeSeconds && bossTickets < bossTicketCapacity)
        {
            bossTicketProductionSeconds -= chargeSeconds;
            AddBossTicket();
        }
    }

    private void AddBossTicket()
    {
        int nextTickets = Mathf.Clamp(bossTickets + 1, 0, bossTicketCapacity);
        if (nextTickets == bossTickets)
        {
            return;
        }

        bossTickets = nextTickets;
        OnBossTicketsChanged.Invoke(bossTickets);
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
        ApplyLevelBalance();
        RefreshUnlocks();
        OnLevelChanged.Invoke(level);
        AchievementManager.ReportFacilityUpgraded();
        OnUpgradeCompleted.Invoke();
    }

    private void RefreshUnlocks()
    {
        NormalizeFacilityUnlocks();

        foreach (FacilityUnlock facility in facilityUnlocks)
        {
            bool shouldBeUnlocked = level >= facility.requiredLabLevel;

            if (!facility.unlocked && shouldBeUnlocked)
            {
                facility.unlocked = true;
                OnFacilityUnlocked.Invoke(facility.facilityId);
            }
            else if (facility.unlocked && !shouldBeUnlocked)
            {
                facility.unlocked = false;
            }
        }
    }

    private void NormalizeFacilityUnlocks()
    {
        BaseCampBalanceConfig config = BaseCampBalanceConfig.Current;
        if (config == null)
        {
            return;
        }

        List<FacilityUnlock> previousUnlocks = facilityUnlocks ?? new List<FacilityUnlock>();
        facilityUnlocks = new List<FacilityUnlock>();
        foreach (BaseCampBalanceConfig.FacilityUnlockData unlock in config.Unlocks)
        {
            FacilityUnlock previous = previousUnlocks.Find(item => item.facilityId == unlock.facilityId);
            facilityUnlocks.Add(new FacilityUnlock
            {
                facilityId = unlock.facilityId,
                displayName = unlock.displayName,
                requiredLabLevel = unlock.requiredCommandCenterLevel,
                unlocked = previous != null && previous.unlocked
            });
        }
    }

    private float GetUpgradeDurationForCurrentLevel()
    {
        return Mathf.Max(0f, GetCurrentBalance()?.upgradeSeconds ?? 0f);
    }

    private int GetRequiredCommanderLevelForCurrentUpgrade()
    {
        return GetCurrentBalance()?.requiredCommanderLevel ?? int.MaxValue;
    }

    private BaseCampBalanceConfig.FacilityLevelData GetCurrentBalance()
    {
        return GetBalanceForLevel(level);
    }

    private BaseCampBalanceConfig.FacilityLevelData GetBalanceForLevel(int targetLevel)
    {
        int clampedLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
        return BaseCampBalanceConfig.Current?.GetLevel(FacilityId, clampedLevel);
    }

    private void ApplyLevelBalance()
    {
        BaseCampBalanceConfig.FacilityLevelData balance = GetCurrentBalance();
        if (balance == null)
        {
            return;
        }

        bossTicketCapacity = Mathf.Max(0, balance.ticketCapacity);
        bossTickets = Mathf.Max(0, bossTickets);
    }

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        bossTickets = Mathf.Max(0, bossTickets);
    }
}
