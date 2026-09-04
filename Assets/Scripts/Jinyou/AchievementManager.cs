using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public enum AchievementProgressType
{
    PlayerLevel,
    EnemyKill,
    StageClear,
    WeaponCollect,
    DroneCollect,
    BossClear,
    BossDifficultyClear,
    FacilityUpgrade,
    WeaponUpgrade,
    DroneUpgrade
}

[DisallowMultipleComponent]
public class AchievementManager : MonoBehaviour
{
    [Serializable]
    public class AchievementEntry
    {
        [SerializeField, FormerlySerializedAs("id")] private string _id;
        [SerializeField, FormerlySerializedAs("progressType")] private AchievementProgressType _progressType;
        [SerializeField, FormerlySerializedAs("title")] private string _title;
        [TextArea]
        [SerializeField, FormerlySerializedAs("description")] private string _description;
        [SerializeField, FormerlySerializedAs("iconSprite")] private Sprite _iconSprite;
        [Min(1)]
        [SerializeField, FormerlySerializedAs("targetAmount")] private int _targetAmount = 1;
        [SerializeField, FormerlySerializedAs("nextTargetAmounts")] private List<int> _nextTargetAmounts = new List<int>();
        [Min(0)]
        [SerializeField, FormerlySerializedAs("repeatRequirementAmount")] private int _repeatRequirementAmount = 1;
        [Min(1)]
        [SerializeField, FormerlySerializedAs("progressAmountPerEvent")] private int _progressAmountPerEvent = 1;
        [SerializeField, FormerlySerializedAs("rewardCurrency")] private CurrencyType _rewardCurrency = CurrencyType.CoreCrystals;
        [Min(0)]
        [SerializeField, FormerlySerializedAs("rewardAmount")] private int _rewardAmount;
        [SerializeField, FormerlySerializedAs("currentAmount")] private int _currentAmount;
        [SerializeField, FormerlySerializedAs("completed")] private bool _completed;
        [SerializeField, FormerlySerializedAs("completedCount")] private int _completedCount;

        public string Id => _id;
        public AchievementProgressType ProgressType => _progressType;
        public string Title => _title;
        public string Description => _description;
        public Sprite IconSprite => _iconSprite;
        public int TargetAmount => Mathf.Max(1, _targetAmount);
        public IReadOnlyList<int> NextTargetAmounts => _nextTargetAmounts;
        public int RepeatRequirementAmount => Mathf.Max(0, _repeatRequirementAmount);
        public int ProgressAmountPerEvent => Mathf.Max(1, _progressAmountPerEvent);
        public CurrencyType RewardCurrency => _rewardCurrency;
        public int RewardAmount => Mathf.Max(0, _rewardAmount);
        public int CurrentAmount => Mathf.Max(0, _currentAmount);
        public bool Completed => _completed;
        public int CompletedCount => Mathf.Max(0, _completedCount);
        public int PreviousTargetAmount => CompletedCount == 0
            ? 0
            : GetRequiredAmountForCompletion(CompletedCount - 1);
        public int NextTargetAmount => GetRequiredAmountForCompletion(CompletedCount);
        public float Progress01
        {
            get
            {
                int span = Mathf.Max(1, NextTargetAmount - PreviousTargetAmount);
                return Mathf.Clamp01((float)(CurrentAmount - PreviousTargetAmount) / span);
            }
        }

        public AchievementEntry(
            string id,
            AchievementProgressType progressType,
            string title,
            string description,
            int targetAmount,
            int progressAmountPerEvent = 1,
            int repeatRequirementAmount = 1,
            CurrencyType rewardCurrency = CurrencyType.CoreCrystals,
            int rewardAmount = 0)
        {
            this._id = id;
            this._progressType = progressType;
            this._title = title;
            this._description = description;
            this._targetAmount = Mathf.Max(1, targetAmount);
            this._progressAmountPerEvent = Mathf.Max(1, progressAmountPerEvent);
            this._repeatRequirementAmount = Mathf.Max(0, repeatRequirementAmount);
            this._rewardCurrency = rewardCurrency;
            this._rewardAmount = Mathf.Max(0, rewardAmount);
        }

        public AchievementEntry(AchievementConfig.AchievementDefinition definition)
        {
            _id = definition.id;
            _progressType = definition.progressType;
            _title = definition.title;
            _description = definition.description;
            _iconSprite = definition.iconSprite;
            _targetAmount = Mathf.Max(1, definition.targetAmount);
            _nextTargetAmounts = definition.nextTargetAmounts != null
                ? new List<int>(definition.nextTargetAmounts)
                : new List<int>();
            _repeatRequirementAmount = Mathf.Max(0, definition.repeatRequirementAmount);
            _progressAmountPerEvent = Mathf.Max(1, definition.progressAmountPerEvent);
            _rewardCurrency = definition.rewardCurrency;
            _rewardAmount = Mathf.Max(0, definition.rewardAmount);
            Validate();
        }

        public bool AddProgress(int amount)
        {
            int positiveAmount = Mathf.Max(0, amount);
            if (positiveAmount == 0 || _completed)
            {
                return false;
            }

            int previousAmount = _currentAmount;
            _currentAmount = Mathf.Min(NextTargetAmount, _currentAmount + positiveAmount);
            RefreshCompletionState();
            return previousAmount != _currentAmount;
        }

        public bool SetProgress(int amount)
        {
            if (_completed)
            {
                return false;
            }

            int previousAmount = _currentAmount;
            _currentAmount = Mathf.Clamp(amount, 0, NextTargetAmount);
            RefreshCompletionState();
            return previousAmount != _currentAmount;
        }

        public bool TryClaimReward()
        {
            RefreshCompletionState();
            if (!_completed)
            {
                return false;
            }

            _completedCount++;
            _completed = false;
            if (IsFinalMilestoneClaimed)
            {
                _currentAmount = GetLastConfiguredTargetAmount();
            }

            return true;
        }

        public void RestoreProgress(int amount, int completedMilestones)
        {
            _completedCount = Mathf.Max(0, completedMilestones);
            _currentAmount = Mathf.Clamp(amount, 0, NextTargetAmount);
            RefreshCompletionState();
        }

        public void ResetProgress()
        {
            _currentAmount = 0;
            _completed = false;
            _completedCount = 0;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = _progressType.ToString();
            }

            _targetAmount = Mathf.Max(1, _targetAmount);
            _nextTargetAmounts ??= new List<int>();
            int previousRequirement = _targetAmount;
            for (int i = 0; i < _nextTargetAmounts.Count; i++)
            {
                _nextTargetAmounts[i] = Mathf.Max(previousRequirement + 1, _nextTargetAmounts[i]);
                previousRequirement = _nextTargetAmounts[i];
            }

            _repeatRequirementAmount = Mathf.Max(0, _repeatRequirementAmount);
            _progressAmountPerEvent = Mathf.Max(1, _progressAmountPerEvent);
            _rewardAmount = Mathf.Max(0, _rewardAmount);
            _completedCount = Mathf.Max(0, _completedCount);
            _currentAmount = Mathf.Clamp(_currentAmount, 0, NextTargetAmount);
            RefreshCompletionState();
        }

        private void RefreshCompletionState()
        {
            _completed = !IsFinalMilestoneClaimed && _currentAmount >= NextTargetAmount;
        }

        private bool RepeatEnabled => RepeatRequirementAmount > 0;

        private int ConfiguredMilestoneCount => 1 + (_nextTargetAmounts != null ? _nextTargetAmounts.Count : 0);

        private bool IsFinalMilestoneClaimed => !RepeatEnabled && CompletedCount >= ConfiguredMilestoneCount;

        private int GetRequiredAmountForCompletion(int completionIndex)
        {
            if (completionIndex <= 0)
            {
                return TargetAmount;
            }

            int nextTargetIndex = completionIndex - 1;
            if (_nextTargetAmounts != null && nextTargetIndex < _nextTargetAmounts.Count)
            {
                return Mathf.Max(TargetAmount, _nextTargetAmounts[nextTargetIndex]);
            }

            int lastConfiguredTarget = TargetAmount;
            if (_nextTargetAmounts != null && _nextTargetAmounts.Count > 0)
            {
                lastConfiguredTarget = Mathf.Max(TargetAmount, _nextTargetAmounts[_nextTargetAmounts.Count - 1]);
            }

            if (!RepeatEnabled)
            {
                return lastConfiguredTarget;
            }

            int repeatIndex = completionIndex - (_nextTargetAmounts != null ? _nextTargetAmounts.Count : 0);
            return lastConfiguredTarget + Mathf.Max(0, repeatIndex) * RepeatRequirementAmount;
        }

        private int GetLastConfiguredTargetAmount()
        {
            int lastConfiguredTarget = TargetAmount;
            if (_nextTargetAmounts != null && _nextTargetAmounts.Count > 0)
            {
                lastConfiguredTarget = Mathf.Max(TargetAmount, _nextTargetAmounts[_nextTargetAmounts.Count - 1]);
            }

            return lastConfiguredTarget;
        }
    }

    private const string CurrentKeySuffix = ".Current";
    private const string CompletedKeySuffix = ".Completed";
    private const string CompletedCountKeySuffix = ".CompletedCount";

    public static AchievementManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField, FormerlySerializedAs("achievementConfig")]
    private AchievementConfig _achievementConfig;

    [SerializeField, FormerlySerializedAs("saveToPlayerPrefs")]
    private bool _saveToPlayerPrefs = true;

    [SerializeField, FormerlySerializedAs("saveKeyPrefix")]
    private string _saveKeyPrefix = "Achievement.";

    [Header("Achievements")]
    [SerializeField, FormerlySerializedAs("achievements")]
    private List<AchievementEntry> _achievements = new List<AchievementEntry>();

    [Header("Events")]
    public UnityEvent OnAchievementsChanged = new UnityEvent();
    public UnityEvent<AchievementEntry> OnAchievementCompleted = new UnityEvent<AchievementEntry>();

    public IReadOnlyList<AchievementEntry> Achievements => _achievements;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadAchievementDefinitions();
        ValidateAchievements();
        Load();
    }

    private void OnDestroy()
    {
        // 파괴 시 정적 Instance를 비워, 씬 전환 후 파괴된 인스턴스에 진행도가 기록돼
        // 조용히 유실되는 것을 막는다(C# null 연산자는 Unity fake-null을 우회하므로 명시적으로 해제).
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ReportPlayerLevelReached(int level)
    {
        Instance?.SetProgress(AchievementProgressType.PlayerLevel, level);
    }

    public static void ReportEnemyKilled(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.EnemyKill, amount);
    }

    public static void ReportStageCleared(int amount = 1)
    {
        Instance?.SetProgress(AchievementProgressType.StageClear, Mathf.Max(1, amount));
    }

    public static void ReportWeaponCollected(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.WeaponCollect, amount);
    }

    public static void ReportDroneCollected(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.DroneCollect, amount);
    }

    public static void ReportBossCleared(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.BossClear, amount);
    }

    public static void ReportBossDifficultyCleared(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.BossDifficultyClear, amount);
    }

    public static void ReportFacilityUpgraded(int amount = 1)
    {
        Instance?.SetFacilityUpgradeProgressToCurrentTotal();
    }

    public static void ReportWeaponUpgraded(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.WeaponUpgrade, amount);
    }

    public static void ReportDroneUpgraded(int amount = 1)
    {
        Instance?.AddProgress(AchievementProgressType.DroneUpgrade, amount);
    }

    [ContextMenu("Reset Achievement Progress")]
    public void ResetAllProgress()
    {
        foreach (AchievementEntry achievement in _achievements)
        {
            achievement?.ResetProgress();
        }

        Save();
        OnAchievementsChanged.Invoke();
    }

    public JinyouAchievementSaveData CaptureState()
    {
        JinyouAchievementSaveData data = new JinyouAchievementSaveData();
        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null)
            {
                continue;
            }

            data.entries.Add(new JinyouAchievementEntrySaveData
            {
                id = achievement.Id,
                currentAmount = achievement.CurrentAmount,
                completedCount = achievement.CompletedCount
            });
        }

        return data;
    }

    public void RestoreState(JinyouAchievementSaveData data)
    {
        if (data?.entries == null)
        {
            return;
        }

        foreach (JinyouAchievementEntrySaveData entryData in data.entries)
        {
            if (entryData == null || string.IsNullOrWhiteSpace(entryData.id))
            {
                continue;
            }

            AchievementEntry achievement = _achievements.Find(item => item != null && item.Id == entryData.id);
            achievement?.RestoreProgress(entryData.currentAmount, entryData.completedCount);
        }

        Save();
        ResyncAbsoluteProgress(AchievementProgressType.FacilityUpgrade);
        OnAchievementsChanged.Invoke();
    }

    public void AddProgress(AchievementProgressType progressType, int eventCount = 1)
    {
        bool changed = false;
        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null || achievement.ProgressType != progressType)
            {
                continue;
            }

            int amount = Mathf.Max(0, eventCount) * achievement.ProgressAmountPerEvent;
            bool wasCompleted = achievement.Completed;
            changed = achievement.AddProgress(amount) || changed;
            if (!wasCompleted && achievement.Completed)
            {
                OnAchievementCompleted.Invoke(achievement);
            }
        }

        NotifyIfChanged(changed);
    }

    public void SetProgress(AchievementProgressType progressType, int amount)
    {
        bool changed = false;
        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null || achievement.ProgressType != progressType)
            {
                continue;
            }

            bool wasCompleted = achievement.Completed;
            changed = achievement.SetProgress(amount) || changed;
            if (!wasCompleted && achievement.Completed)
            {
                OnAchievementCompleted.Invoke(achievement);
            }
        }

        NotifyIfChanged(changed);
    }

    public bool TryClaimReward(string achievementId)
    {
        AchievementEntry achievement = _achievements.Find(item =>
            item != null && item.Id == achievementId);
        if (achievement == null || !achievement.TryClaimReward())
        {
            return false;
        }

        PlayerCurrencyWallet wallet = ResolveCurrencyWallet();
        if (wallet != null && achievement.RewardAmount > 0)
        {
            wallet.Add(achievement.RewardCurrency, achievement.RewardAmount);
        }

        ResyncAbsoluteProgress(achievement.ProgressType);
        Save();
        OnAchievementsChanged.Invoke();
        return true;
    }

    /// <summary>
    /// 예를 들어 실제 지휘관 레벨이 19인데 도전과제가 Lv.9 보상 단계에서 멈춰 있었다면, 보상을 받는 순간
    /// PlayerProgression.Level을 다시 읽어 현재 레벨(19레벨)로 다시 반영한다.
    /// 이후 기존처럼 순차적으로 보상을 받을 수 있게 한다.
    /// </summary>
    /// <param name="progressType">지휘관 레벨 진행도</param>
    private void ResyncAbsoluteProgress(AchievementProgressType progressType)
    {
        switch (progressType)
        {
            case AchievementProgressType.PlayerLevel:
                PlayerProgression progression = FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include);
                if (progression != null)
                {
                    SetProgress(progressType, progression.Level);
                }
                break;
            case AchievementProgressType.StageClear:
                EnemySpawnManager spawnManager = FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
                if (spawnManager != null)
                {
                    SetProgress(progressType, spawnManager.CurrentStage);
                }
                break;
            case AchievementProgressType.FacilityUpgrade:
                SetFacilityUpgradeProgressToCurrentTotal();
                break;
        }
    }

    private void SetFacilityUpgradeProgressToCurrentTotal()
    {
        SetProgress(AchievementProgressType.FacilityUpgrade, ResolveTotalFacilityLevel());
    }

    private static int ResolveTotalFacilityLevel()
    {
        BaseCampManager baseCampManager = BaseCampManager.Instance
            ?? FindFirstObjectByType<BaseCampManager>(FindObjectsInactive.Include);

        if (baseCampManager != null)
        {
            return GetFacilityLevel(baseCampManager.CommandCenter)
                + GetFacilityLevel(baseCampManager.CreditRefinery)
                + GetFacilityLevel(baseCampManager.AssemblyFactory)
                + GetFacilityLevel(baseCampManager.CoreCharger)
                + GetFacilityLevel(baseCampManager.SkillHanger);
        }

        return GetFacilityLevel(FindFirstObjectByType<CommandCenter>(FindObjectsInactive.Include))
            + GetFacilityLevel(FindFirstObjectByType<CreditRefinery>(FindObjectsInactive.Include))
            + GetFacilityLevel(FindFirstObjectByType<AssemblyFactory>(FindObjectsInactive.Include))
            + GetFacilityLevel(FindFirstObjectByType<CoreCharger>(FindObjectsInactive.Include))
            + GetFacilityLevel(FindFirstObjectByType<SkillHangerFacility>(FindObjectsInactive.Include));
    }

    private static int GetFacilityLevel(IBaseCampFacility facility)
    {
        return facility != null ? Mathf.Max(0, facility.Level) : 0;
    }

    private PlayerCurrencyWallet ResolveCurrencyWallet()
    {
        if (BaseCampManager.Instance != null)
        {
            return BaseCampManager.Instance.CurrencyWallet;
        }

        return FindFirstObjectByType<PlayerCurrencyWallet>();
    }

    private void NotifyIfChanged(bool changed)
    {
        if (!changed)
        {
            return;
        }

        Save();
        OnAchievementsChanged.Invoke();
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        _saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null)
            {
                continue;
            }

            PlayerPrefs.DeleteKey(GetCurrentKey(achievement));
            PlayerPrefs.DeleteKey(GetCompletedKey(achievement));
            PlayerPrefs.DeleteKey(GetCompletedCountKey(achievement));
        }

        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!_saveToPlayerPrefs)
        {
            return;
        }

        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null)
            {
                continue;
            }

            achievement.RestoreProgress(
                PlayerPrefs.GetInt(GetCurrentKey(achievement), achievement.CurrentAmount),
                PlayerPrefs.GetInt(GetCompletedCountKey(achievement), achievement.CompletedCount));
        }
    }

    private void Save()
    {
        if (!_saveToPlayerPrefs)
        {
            return;
        }

        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null)
            {
                continue;
            }

            PlayerPrefs.SetInt(GetCurrentKey(achievement), achievement.CurrentAmount);
            PlayerPrefs.SetInt(GetCompletedKey(achievement), achievement.Completed ? 1 : 0);
            PlayerPrefs.SetInt(GetCompletedCountKey(achievement), achievement.CompletedCount);
        }

        PlayerPrefs.Save();
    }

    private string GetCurrentKey(AchievementEntry achievement)
    {
        return _saveKeyPrefix + achievement.Id + CurrentKeySuffix;
    }

    private string GetCompletedKey(AchievementEntry achievement)
    {
        return _saveKeyPrefix + achievement.Id + CompletedKeySuffix;
    }

    private string GetCompletedCountKey(AchievementEntry achievement)
    {
        return _saveKeyPrefix + achievement.Id + CompletedCountKeySuffix;
    }

    private void OnValidate()
    {
        ValidateAchievements();
    }

    private void LoadAchievementDefinitions()
    {
        AchievementConfig config = _achievementConfig != null
            ? _achievementConfig
            : AchievementConfig.Current;
        if (config == null)
        {
            return;
        }

        Dictionary<string, AchievementEntry> previousEntries = new Dictionary<string, AchievementEntry>();
        foreach (AchievementEntry achievement in _achievements)
        {
            if (achievement == null || string.IsNullOrWhiteSpace(achievement.Id))
            {
                continue;
            }

            previousEntries[achievement.Id] = achievement;
        }

        _achievements = new List<AchievementEntry>();
        foreach (AchievementConfig.AchievementDefinition definition in config.Achievements)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.id))
            {
                continue;
            }

            AchievementEntry entry = new AchievementEntry(definition);
            if (previousEntries.TryGetValue(entry.Id, out AchievementEntry previousEntry))
            {
                // CSV/SO 정의로 갈아끼워도 기존 런타임 진행도는 id 기준으로 이어받는다.
                entry.RestoreProgress(previousEntry.CurrentAmount, previousEntry.CompletedCount);
            }

            _achievements.Add(entry);
        }
    }

    private void ValidateAchievements()
    {
        _achievements ??= new List<AchievementEntry>();
        foreach (AchievementEntry achievement in _achievements)
        {
            achievement?.Validate();
        }
    }
}
