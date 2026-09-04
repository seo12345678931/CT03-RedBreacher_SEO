using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 화면 최상단에 노출되는 순차형 메인 가이드 미션을 관리한다.
/// 한 번에 한 단계만 활성화되며, 완료 후 수동 클레임 시 다음 단계로 진행한다.
/// 미션 정의는 CSV → <see cref="GuideMissionConfig"/> SO에서 로드한다.
/// </summary>
[DisallowMultipleComponent]
public class MainGuideMissionManager : MonoBehaviour
{
    public static MainGuideMissionManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool saveToPlayerPrefs = true;
    [SerializeField] private string saveKey = "MainGuideMissionManager.SaveData";

    [Header("Events")]
    public UnityEvent OnGuideMissionsChanged = new UnityEvent();
    public UnityEvent<GuideMissionConfig.GuideStepData> OnGuideStepCompleted = new UnityEvent<GuideMissionConfig.GuideStepData>();
    public UnityEvent<GuideMissionConfig.GuideStepData> OnGuideStepClaimed = new UnityEvent<GuideMissionConfig.GuideStepData>();
    public UnityEvent OnAllGuideMissionsCompleted = new UnityEvent();

    private readonly List<GuideMissionConfig.GuideStepData> steps = new List<GuideMissionConfig.GuideStepData>();
    private readonly Dictionary<GuideConditionType, int> latestAbsoluteValues = new Dictionary<GuideConditionType, int>();

    private int currentIndex;
    private int currentAmount;
    private int bossTicketGrantedStepIndex = -1;

    public IReadOnlyList<GuideMissionConfig.GuideStepData> Steps => steps;
    public int CurrentIndex => currentIndex;
    public int CurrentAmount => Mathf.Max(0, currentAmount);
    public bool AllCompleted => currentIndex >= steps.Count;

    public GuideMissionConfig.GuideStepData CurrentStep =>
        AllCompleted ? null : steps[currentIndex];

    public int CurrentTargetAmount =>
        GetEffectiveTargetAmount(CurrentStep);

    public bool IsCurrentCompleted =>
        CurrentStep != null && CurrentAmount >= CurrentTargetAmount;

    public float CurrentProgress01 =>
        CurrentStep != null ? Mathf.Clamp01((float)CurrentAmount / CurrentTargetAmount) : 1f;

    private int GetEffectiveTargetAmount(GuideMissionConfig.GuideStepData step)
    {
        if (step == null)
        {
            return 0;
        }

        int targetAmount = Mathf.Max(1, step.targetAmount);
        if (step.conditionType == GuideConditionType.UpgradeFacility)
        {
            int maxLevel = ResolveFacilityMaxLevel(step.conditionTarget);
            if (maxLevel > 0)
            {
                targetAmount = Mathf.Min(targetAmount, maxLevel);
            }
        }

        return targetAmount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadSteps();
        Load();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadSteps()
    {
        steps.Clear();
        GuideMissionConfig config = GuideMissionConfig.Current;
        if (config?.Steps == null)
        {
            return;
        }

        foreach (GuideMissionConfig.GuideStepData step in config.Steps)
        {
            if (step != null)
            {
                steps.Add(step);
            }
        }
    }

    // ── 정적 보고 훅(기존 Report 발생 지점에서 함께 호출한다) ────────────────
    public static void ReportEnemyKilled(int amount = 1) => Instance?.AddProgress(GuideConditionType.EnemyKill, amount);
    public static void ReportPlayerLevelReached(int level) => Instance?.SetAbsoluteProgress(GuideConditionType.PlayerLevel, level);
    public static void ReportStageCleared(int amount = 1) => Instance?.AddProgress(GuideConditionType.StageClear, amount);
    public static void ReportWeaponCollected(int amount = 1) => Instance?.AddProgress(GuideConditionType.WeaponCollect, amount);
    public static void ReportDroneCollected(int amount = 1) => Instance?.ReportDroneCollectionProgress();
    public static void ReportCreditsCollected(int amount) => Instance?.AddProgress(GuideConditionType.CollectCredits, amount);
    public static void ReportFacilityUpgraded(int amount = 1) => Instance?.PrimeCurrentStep();
    public static void ReportFacilityLevelReached(string facilityId, int level) => Instance?.SetFacilityLevelProgress(facilityId, level);
    public static void ReportWeaponEnhanced(int amount = 1) => Instance?.AddProgress(GuideConditionType.EnhanceWeapon, amount);
    public static void ReportUnitEnhanced(int amount = 1) => Instance?.ReportUnitEnhancementProgress();
    public static void ReportDroneEnhanced(int amount = 1) => Instance?.AddProgress(GuideConditionType.EnhanceDrone, amount);
    public static void ReportBossTicketUsed(int amount = 1) => Instance?.AddProgress(GuideConditionType.UseBossTicket, amount);
    public static void ReportWeaponGachaDrawn(int amount = 1) => Instance?.AddProgress(GuideConditionType.DrawWeaponGacha, amount);
    public static void ReportSkillGachaDrawn(int amount = 1) => Instance?.AddProgress(GuideConditionType.DrawSkillGacha, amount);
    public static void ReportOfflineRewardClaimed(int amount = 1) => Instance?.AddProgress(GuideConditionType.ClaimOfflineReward, amount);
    public static void ReportBossDefeated(int amount = 1) => Instance?.AddProgress(GuideConditionType.BossDefeat, amount);

    private void AddProgress(GuideConditionType conditionType, int amount)
    {
        int positiveAmount = Mathf.Max(0, amount);
        if (positiveAmount == 0)
        {
            return;
        }

        if (GuideMissionConfig.IsAbsoluteCondition(conditionType))
        {
            // 누적이 아닌 조건이 누적 경로로 들어오면 절대값 누계로 환산한다.
            SetAbsoluteProgress(conditionType, GetAbsolute(conditionType) + positiveAmount);
            return;
        }

        GuideMissionConfig.GuideStepData step = CurrentStep;
        if (step == null || step.conditionType != conditionType)
        {
            return;
        }

        ApplyProgress(Mathf.Min(CurrentTargetAmount, currentAmount + positiveAmount));
    }

    private void SetAbsoluteProgress(GuideConditionType conditionType, int value)
    {
        int clampedValue = Mathf.Max(0, value);
        if (latestAbsoluteValues.TryGetValue(conditionType, out int previous) && previous >= clampedValue)
        {
            clampedValue = previous;
        }

        latestAbsoluteValues[conditionType] = clampedValue;

        GuideMissionConfig.GuideStepData step = CurrentStep;
        if (step == null || step.conditionType != conditionType)
        {
            return;
        }

        ApplyProgress(Mathf.Min(CurrentTargetAmount, clampedValue));
    }

    private void ApplyProgress(int newAmount)
    {
        if (newAmount == currentAmount)
        {
            return;
        }

        bool wasCompleted = IsCurrentCompleted;
        currentAmount = Mathf.Max(0, newAmount);
        Save();
        OnGuideMissionsChanged.Invoke();

        if (!wasCompleted && IsCurrentCompleted)
        {
            OnGuideStepCompleted.Invoke(CurrentStep);
        }
    }

    /// <summary>활성 단계가 완료된 경우 보상을 지급하고 다음 단계로 진행한다.</summary>
    public bool TryClaimCurrent()
    {
        if (!IsCurrentCompleted)
        {
            return false;
        }

        GuideMissionConfig.GuideStepData claimedStep = CurrentStep;
        PlayerCurrencyWallet wallet = ResolveCurrencyWallet();
        if (wallet != null && claimedStep.rewardAmount > 0)
        {
            wallet.Add(claimedStep.rewardCurrency, claimedStep.rewardAmount);
        }

        currentIndex++;
        currentAmount = 0;
        PrimeCurrentStep();

        Save();
        OnGuideStepClaimed.Invoke(claimedStep);
        OnGuideMissionsChanged.Invoke();

        if (AllCompleted)
        {
            OnAllGuideMissionsCompleted.Invoke();
        }
        else if (IsCurrentCompleted)
        {
            // 진입 시점에 이미 충족된 단계(예: 이미 도달한 레벨)는 즉시 완료 처리한다.
            OnGuideStepCompleted.Invoke(CurrentStep);
        }

        return true;
    }

    // 새 활성 단계가 절대값 조건이면 이미 도달한 수치로 진행도를 초기화한다.
    private void PrimeCurrentStep()
    {
        GuideMissionConfig.GuideStepData step = CurrentStep;
        if (step == null)
        {
            return;
        }

        GrantBossTicketsForCurrentStepIfNeeded(step);

        if (GuideMissionConfig.IsAbsoluteCondition(step.conditionType))
        {
            if (step.conditionType == GuideConditionType.StageClear)
            {
                latestAbsoluteValues[GuideConditionType.StageClear] = Mathf.Max(
                    GetAbsolute(GuideConditionType.StageClear),
                    ResolveCurrentStage());
            }
            else if (step.conditionType == GuideConditionType.UpgradeFacility)
            {
                SetFacilityLevelProgress(step.conditionTarget, ResolveFacilityLevel(step.conditionTarget));
                return;
            }
            else if (step.conditionType == GuideConditionType.EnhanceUnit)
            {
                latestAbsoluteValues[GuideConditionType.EnhanceUnit] = Mathf.Max(
                    GetAbsolute(GuideConditionType.EnhanceUnit),
                    ResolveUnitEnhancementCount());
            }

            currentAmount = Mathf.Min(Mathf.Max(1, step.targetAmount), GetAbsolute(step.conditionType));
        }
    }

    private void SetFacilityLevelProgress(string facilityId, int level)
    {
        GuideMissionConfig.GuideStepData step = CurrentStep;
        if (step == null || step.conditionType != GuideConditionType.UpgradeFacility)
        {
            return;
        }

        if (!IsMatchingFacilityTarget(step.conditionTarget, facilityId))
        {
            return;
        }

        ApplyProgress(Mathf.Min(CurrentTargetAmount, Mathf.Max(0, level)));
    }

    private int GetAbsolute(GuideConditionType conditionType)
    {
        int value = latestAbsoluteValues.TryGetValue(conditionType, out int storedValue)
            ? storedValue
            : 0;
        if (conditionType == GuideConditionType.StageClear)
        {
            value = Mathf.Max(value, ResolveCurrentStage());
        }
        else if (conditionType == GuideConditionType.UpgradeFacility)
        {
            value = Mathf.Max(value, ResolveFacilityLevel(CurrentStep?.conditionTarget));
        }
        else if (conditionType == GuideConditionType.EnhanceUnit)
        {
            value = Mathf.Max(value, ResolveUnitEnhancementCount());
        }
        else if (conditionType == GuideConditionType.DroneCollect)
        {
            value = Mathf.Max(value, ResolveDroneCollectionCount());
        }

        return value;
    }

    private void ReportDroneCollectionProgress()
    {
        SetAbsoluteProgress(GuideConditionType.DroneCollect, ResolveDroneCollectionCount());
    }

    private void ReportUnitEnhancementProgress()
    {
        SetAbsoluteProgress(GuideConditionType.EnhanceUnit, ResolveUnitEnhancementCount());
    }

    private int ResolveCurrentStage()
    {
        EnemySpawnManager spawnManager = FindFirstObjectByType<EnemySpawnManager>();
        return spawnManager != null ? Mathf.Max(1, spawnManager.CurrentStage) : 0;
    }

    private int ResolveUnitEnhancementCount()
    {
        BaseCampManager baseCamp = BaseCampManager.Instance;
        if (baseCamp == null)
        {
            baseCamp = FindFirstObjectByType<BaseCampManager>(FindObjectsInactive.Include);
        }

        return baseCamp != null && baseCamp.CoreCharger != null
            ? Mathf.Max(0, baseCamp.CoreCharger.CompletedConversionCount)
            : 0;
    }

    private int ResolveDroneCollectionCount()
    {
        InventoryFacility inventory = FindFirstObjectByType<InventoryFacility>(FindObjectsInactive.Include);
        return inventory != null && inventory.OwnedDroneIds != null
            ? Mathf.Max(0, inventory.OwnedDroneIds.Count)
            : 0;
    }

    private int ResolveFacilityLevel(string facilityId)
    {
        BaseCampManager baseCamp = BaseCampManager.Instance;
        if (baseCamp == null)
        {
            baseCamp = FindFirstObjectByType<BaseCampManager>(FindObjectsInactive.Include);
        }

        if (baseCamp == null)
        {
            return 0;
        }

        string normalizedId = NormalizeFacilityId(facilityId);
        return normalizedId switch
        {
            "command_center" => baseCamp.CommandCenter != null ? baseCamp.CommandCenter.Level : 0,
            "energy_refinery" => baseCamp.CreditRefinery != null ? baseCamp.CreditRefinery.Level : 0,
            "assembly_factory" => baseCamp.AssemblyFactory != null ? baseCamp.AssemblyFactory.Level : 0,
            "core_charger" => baseCamp.CoreCharger != null ? baseCamp.CoreCharger.Level : 0,
            "skill_hanger" => baseCamp.SkillHanger != null ? baseCamp.SkillHanger.Level : 0,
            _ => 0
        };
    }

    private int ResolveFacilityMaxLevel(string facilityId)
    {
        string normalizedId = NormalizeFacilityId(facilityId);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return 0;
        }

        BaseCampBalanceConfig config = BaseCampBalanceConfig.Current;
        return config != null ? Mathf.Max(1, config.GetMaxLevel(normalizedId)) : 0;
    }

    private static bool IsMatchingFacilityTarget(string stepTarget, string reportedTarget)
    {
        string normalizedStepTarget = NormalizeFacilityId(stepTarget);
        return !string.IsNullOrWhiteSpace(normalizedStepTarget)
            && normalizedStepTarget == NormalizeFacilityId(reportedTarget);
    }

    private static string NormalizeFacilityId(string facilityId)
    {
        return string.IsNullOrWhiteSpace(facilityId)
            ? string.Empty
            : facilityId.Trim().ToLowerInvariant();
    }

    public JinyouGuideMissionSaveData CaptureState()
    {
        return new JinyouGuideMissionSaveData
        {
            currentIndex = currentIndex,
            currentAmount = CurrentAmount,
            bossTicketGrantedStepIndex = bossTicketGrantedStepIndex
        };
    }

    public void RestoreState(JinyouGuideMissionSaveData data)
    {
        if (data == null)
        {
            return;
        }

        currentIndex = Mathf.Clamp(data.currentIndex, 0, steps.Count);
        currentAmount = AllCompleted
            ? 0
            : Mathf.Clamp(data.currentAmount, 0, CurrentTargetAmount);
        bossTicketGrantedStepIndex = data.bossTicketGrantedStepIndex;
        PrimeCurrentStep();
        Save();
        OnGuideMissionsChanged.Invoke();
    }

    [ContextMenu("Reset Guide Missions")]
    public void ResetProgress()
    {
        currentIndex = 0;
        currentAmount = 0;
        bossTicketGrantedStepIndex = -1;
        latestAbsoluteValues.Clear();
        Save();
        OnGuideMissionsChanged.Invoke();
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (clearStoredData && !string.IsNullOrWhiteSpace(saveKey))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }
    }

    private PlayerCurrencyWallet ResolveCurrencyWallet()
    {
        if (BaseCampManager.Instance != null)
        {
            return BaseCampManager.Instance.CurrencyWallet;
        }

        return FindFirstObjectByType<PlayerCurrencyWallet>();
    }

    private void GrantBossTicketsForCurrentStepIfNeeded(GuideMissionConfig.GuideStepData step)
    {
        if (step == null
            || bossTicketGrantedStepIndex == currentIndex
            || (step.conditionType != GuideConditionType.UseBossTicket
                && step.conditionType != GuideConditionType.BossDefeat))
        {
            return;
        }

        CommandCenter commandCenter = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.CommandCenter
            : FindFirstObjectByType<CommandCenter>();
        if (commandCenter == null)
        {
            return;
        }

        int neededTickets = Mathf.Max(1, Mathf.Max(1, step.targetAmount) - CurrentAmount);
        commandCenter.AddBossTickets(neededTickets);
        bossTicketGrantedStepIndex = currentIndex;
        Save();
    }

    private void Load()
    {
        if (!saveToPlayerPrefs
            || string.IsNullOrWhiteSpace(saveKey)
            || !PlayerPrefs.HasKey(saveKey))
        {
            return;
        }

        string json = PlayerPrefs.GetString(saveKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            // 통합 저장이 없어도 진행도를 자체 복원한다.
            RestoreState(JsonUtility.FromJson<JinyouGuideMissionSaveData>(json));
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"가이드 미션 저장 데이터를 읽지 못했습니다: {exception.Message}", this);
        }
    }

    private void Save()
    {
        if (!saveToPlayerPrefs || string.IsNullOrWhiteSpace(saveKey))
        {
            return;
        }

        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(CaptureState()));
        PlayerPrefs.Save();
    }
}
