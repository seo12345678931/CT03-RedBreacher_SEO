using System;
using UnityEngine;

public class PlayerProgression : MonoBehaviour
{
    private const string LevelKey = "PlayerProgression.Level";
    private const string CurrentExperienceKey = "PlayerProgression.CurrentExperience";
    private const string ExperienceToNextLevelKey = "PlayerProgression.ExperienceToNextLevel";
    private const string StatPointsKey = "PlayerProgression.StatPoints";

    [Header("Level")]
    [SerializeField] private int level = 1;
    [SerializeField] private float currentExperience;
    [SerializeField] private float experienceToNextLevel = 100f;
    [SerializeField] private float experienceGrowthRate = 1.13f;
    [SerializeField] private int statPoints;
    [SerializeField] private bool saveToPlayerPrefs = true;

    [Header("Auto Growth")]
    [Tooltip("레벨업 시 특성 포인트와 별개로 자동 적용되는 레벨당 체력 증가율.")]
    [SerializeField] private float autoHealthPercentPerLevel = 0.015f;
    [Tooltip("레벨업 시 특성 포인트와 별개로 자동 적용되는 레벨당 공격력 증가율.")]
    [SerializeField] private float autoAttackPercentPerLevel = 0.01f;

    private int initialLevel;
    private float initialExperienceToNextLevel;

    public int Level => level;
    public float CurrentExperience => currentExperience;
    public float ExperienceToNextLevel => experienceToNextLevel;
    public float ExperienceProgress01 => experienceToNextLevel > 0f ? Mathf.Clamp01(currentExperience / experienceToNextLevel) : 0f;
    public int StatPoints => statPoints;
    public float AutoHealthMultiplier => 1f + Mathf.Max(0f, autoHealthPercentPerLevel) * Mathf.Max(0, level - 1);
    public float AutoAttackMultiplier => 1f + Mathf.Max(0f, autoAttackPercentPerLevel) * Mathf.Max(0, level - 1);
    public event Action Changed;

    private void Awake()
    {
        initialLevel = Mathf.Max(1, level);
        initialExperienceToNextLevel = Mathf.Max(1f, experienceToNextLevel);
        Load();
        AchievementManager.ReportPlayerLevelReached(level);
        MainGuideMissionManager.ReportPlayerLevelReached(level);
    }

    public void AddExperience(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        // 적 처치 경험치를 누적하고 필요하면 여러 레벨업도 한 번에 처리한다.
        currentExperience += amount;
        while (currentExperience >= experienceToNextLevel)
        {
            currentExperience -= experienceToNextLevel;
            LevelUp();
        }

        Save();
    }

    public void ResetProgression()
    {
        // 디버그 초기화는 Inspector 기본값 기준으로 레벨/경험치를 되돌린다.
        level = initialLevel;
        currentExperience = 0f;
        experienceToNextLevel = initialExperienceToNextLevel;
        statPoints = 0;
        Save();
    }

    public bool TrySpendStatPoint()
    {
        if (statPoints <= 0)
        {
            return false;
        }

        // 코어 투자에서 사용하는 특성 포인트 차감은 진행도 저장과 함께 처리한다.
        statPoints--;
        Save();
        return true;
    }

    public bool TrySpendStatPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0)
        {
            return true;
        }

        if (statPoints < amount)
        {
            return false;
        }

        statPoints -= amount;
        Save();
        return true;
    }

    public void AddStatPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0)
        {
            return;
        }

        statPoints += amount;
        Save();
    }

    /// <summary>클라우드 동기화용 진행도 스냅샷(통합 세이브에 편입).</summary>
    public JinyouPlayerProgressionSaveData CaptureSaveData()
    {
        return new JinyouPlayerProgressionSaveData
        {
            captured = true,
            level = level,
            currentExperience = currentExperience,
            experienceToNextLevel = experienceToNextLevel,
            statPoints = statPoints,
        };
    }

    /// <summary>통합 세이브에서 진행도를 복원한다(클라우드 풀 이후 재적용).</summary>
    public void RestoreSaveData(JinyouPlayerProgressionSaveData data)
    {
        if (data == null || !data.captured)
        {
            return;
        }

        level = Mathf.Max(1, data.level);
        currentExperience = Mathf.Max(0f, data.currentExperience);
        experienceToNextLevel = Mathf.Max(1f, data.experienceToNextLevel);
        statPoints = Mathf.Max(0, data.statPoints);
        Save();
        AchievementManager.ReportPlayerLevelReached(level);
        MainGuideMissionManager.ReportPlayerLevelReached(level);
    }

    private void LevelUp()
    {
        level++;
        statPoints++;
        experienceToNextLevel = Mathf.Max(1f, experienceToNextLevel * experienceGrowthRate);
        AchievementManager.ReportPlayerLevelReached(level);
        MainGuideMissionManager.ReportPlayerLevelReached(level);
        TutorialManager.Report(TutorialEventType.PlayerLevelReached);

        Debug.Log($"플레이어 레벨업: Lv.{level}, 특성 포인트 {statPoints}");
    }

    private void Load()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        level = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, level));
        currentExperience = Mathf.Max(0f, PlayerPrefs.GetFloat(CurrentExperienceKey, currentExperience));
        experienceToNextLevel = Mathf.Max(1f, PlayerPrefs.GetFloat(ExperienceToNextLevelKey, experienceToNextLevel));
        statPoints = Mathf.Max(0, PlayerPrefs.GetInt(StatPointsKey, statPoints));
    }

    private void Save()
    {
        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetInt(LevelKey, level);
            PlayerPrefs.SetFloat(CurrentExperienceKey, currentExperience);
            PlayerPrefs.SetFloat(ExperienceToNextLevelKey, experienceToNextLevel);
            PlayerPrefs.SetInt(StatPointsKey, statPoints);
            PlayerPrefs.Save();
        }

        Changed?.Invoke();
        BaseCampManager.Instance?.RequestUnifiedSave();
    }

    public JinyouPlayerProgressionSaveData CaptureState()
    {
        return new JinyouPlayerProgressionSaveData
        {
            level = level,
            currentExperience = currentExperience,
            experienceToNextLevel = experienceToNextLevel,
            statPoints = statPoints
        };
    }

    public void RestoreState(JinyouPlayerProgressionSaveData data)
    {
        if (data == null)
        {
            return;
        }

        level = Mathf.Max(1, data.level);
        currentExperience = Mathf.Max(0f, data.currentExperience);
        experienceToNextLevel = Mathf.Max(1f, data.experienceToNextLevel);
        statPoints = Mathf.Max(0, data.statPoints);
        AchievementManager.ReportPlayerLevelReached(level);
        MainGuideMissionManager.ReportPlayerLevelReached(level);
        Changed?.Invoke();
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(LevelKey);
        PlayerPrefs.DeleteKey(CurrentExperienceKey);
        PlayerPrefs.DeleteKey(ExperienceToNextLevelKey);
        PlayerPrefs.DeleteKey(StatPointsKey);
        PlayerPrefs.Save();
    }
}
