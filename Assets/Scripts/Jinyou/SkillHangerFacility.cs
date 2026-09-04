using UnityEngine;
using UnityEngine.Events;

public class SkillHangerFacility : MonoBehaviour, IBaseCampFacility
{
    private const string FacilityId = "skill_hanger";

    [Header("Level")]
    [SerializeField] private int level = 1;

    [Header("Events")]
    public UnityEvent<int> OnLevelChanged = new UnityEvent<int>();
    public UnityEvent OnUpgradeStarted = new UnityEvent();
    public UnityEvent OnUpgradeCompleted = new UnityEvent();

    private int maxLevel = 1;
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

    private void Awake()
    {
        EnsureBalanceInitialized();
        level = Mathf.Clamp(level, 1, maxLevel);
    }

    private void Update()
    {
        TickUpgrade(Time.deltaTime);
    }

    // 비활성 패널 복원 전에 maxLevel이 기본값으로 남지 않도록 CSV 밸런스를 먼저 읽는다.
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
            Debug.LogError($"스킬 격납고 밸런스 초기화 실패: {error}", this);
        }
    }

    public JinyouSkillHangerSaveData CaptureState()
    {
        return new JinyouSkillHangerSaveData
        {
            level = level,
            isUpgrading = isUpgrading,
            upgradeRemainingSeconds = upgradeRemainingSeconds,
            currentUpgradeDurationSeconds = currentUpgradeDurationSeconds
        };
    }

    public void RestoreState(JinyouSkillHangerSaveData data)
    {
        EnsureBalanceInitialized();
        level = Mathf.Clamp(data?.level ?? 1, 1, maxLevel);
        isUpgrading = data != null && data.isUpgrading;
        upgradeRemainingSeconds = Mathf.Max(0f, data?.upgradeRemainingSeconds ?? 0f);
        currentUpgradeDurationSeconds = Mathf.Max(0f, data?.currentUpgradeDurationSeconds ?? 0f);
        OnLevelChanged.Invoke(level);
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

    private float GetUpgradeDurationForCurrentLevel()
    {
        return Mathf.Max(0f, GetCurrentBalance()?.upgradeSeconds ?? 0f);
    }

    private int GetUpgradeCostForCurrentLevel()
    {
        if (level >= maxLevel)
        {
            return 0;
        }

        return Mathf.Max(0, GetCurrentBalance()?.upgradeCost ?? 0);
    }

    private BaseCampBalanceConfig.FacilityLevelData GetCurrentBalance()
    {
        EnsureBalanceInitialized();
        return BaseCampBalanceConfig.Current?.GetLevel(FacilityId, level);
    }
}
