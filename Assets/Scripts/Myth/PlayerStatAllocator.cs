using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerProgression))]
public class PlayerStatAllocator : MonoBehaviour
{
    private const string AttackLevelKey = "PlayerStatAllocator.AttackLevel";
    private const string HealthLevelKey = "PlayerStatAllocator.HealthLevel";
    private const string CritChanceLevelKey = "PlayerStatAllocator.CritChanceLevel";
    private const string CritMultiplierLevelKey = "PlayerStatAllocator.CritMultiplierLevel";
    private const string LegacyAttackPointKey = "TraitPointFacility.AttackPoints";
    private const string LegacyMaxHealthPointKey = "TraitPointFacility.MaxHealthPoints";
    private const string LegacyCritChancePointKey = "TraitPointFacility.CritChancePoints";
    private const string LegacyCritMultiplierPointKey = "TraitPointFacility.CritMultiplierPoints";

    [Header("Attack")]
    [SerializeField] private float attackPercentPerLevel = 0.02f;
    [SerializeField] private int maxAttackLevel = 100;

    [Header("Health")]
    [SerializeField] private float healthPercentPerLevel = 0.03f;
    [SerializeField] private int maxHealthLevel = 100;

    [Header("Critical")]
    [SerializeField] private float critChancePerLevel = 0.01f;
    [SerializeField] private float maxCritChance = 0.5f;
    [SerializeField] private float critMultiplierPerLevel = 0.05f;
    [SerializeField] private float maxCritMultiplier = 3f;

    [Header("Save")]
    [SerializeField] private bool saveToPlayerPrefs = true;

    [SerializeField] private int attackLevel;
    [SerializeField] private int healthLevel;
    [SerializeField] private int critChanceLevel;
    [SerializeField] private int critMultiplierLevel;

    private PlayerProgression progression;
    private PlayerController playerController;

    public int AttackLevel => attackLevel;
    public int HealthLevel => healthLevel;
    public int CritChanceLevel => critChanceLevel;
    public int CritMultiplierLevel => critMultiplierLevel;
    public int AttackDisplayLevel => attackLevel + 1;
    public int HealthDisplayLevel => healthLevel + 1;
    public int CritChanceDisplayLevel => critChanceLevel + 1;
    public int CritMultiplierDisplayLevel => critMultiplierLevel + 1;
    public float AttackPercentPerLevel => Mathf.Max(0f, attackPercentPerLevel);
    public float HealthPercentPerLevel => Mathf.Max(0f, healthPercentPerLevel);
    public float CritChancePerLevel => Mathf.Max(0f, critChancePerLevel);
    public float CritMultiplierPerLevel => Mathf.Max(0f, critMultiplierPerLevel);
    public float AttackBonusPercent => attackLevel * AttackPercentPerLevel;
    public float HealthBonusPercent => healthLevel * HealthPercentPerLevel;
    public float CritChanceBonus => critChanceLevel * CritChancePerLevel;
    public float CritMultiplierBonus => critMultiplierLevel * CritMultiplierPerLevel;
    public float AttackMultiplier => 1f + AttackBonusPercent;
    public float HealthMultiplier => 1f + HealthBonusPercent;
    public float MaxCritChance => Mathf.Clamp01(maxCritChance);
    public float MaxCritMultiplier => Mathf.Max(1f, maxCritMultiplier);
    public int TotalAllocatedLevels => attackLevel + healthLevel + critChanceLevel + critMultiplierLevel;
    public bool CanUpgradeAttack => attackLevel < Mathf.Max(0, maxAttackLevel);
    public bool CanUpgradeHealth => healthLevel < Mathf.Max(0, maxHealthLevel);
    public bool CanUpgradeCritChance => playerController == null || playerController.CritChance < MaxCritChance;
    public bool CanUpgradeCritMultiplier => playerController == null || playerController.CritMultiplier < MaxCritMultiplier;

    private void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        playerController = GetComponent<PlayerController>();
        Load();
        MigrateLegacyTraitPoints();
    }

    public void UpgradeAttack()
    {
        TryUpgradeAttack();
    }

    public void UpgradeHealth()
    {
        TryUpgradeHealth();
    }

    public void UpgradeCritChance()
    {
        TryUpgradeCritChance();
    }

    public void UpgradeCritMultiplier()
    {
        TryUpgradeCritMultiplier();
    }

    public bool TryUpgradeAttack()
    {
        return TryUpgrade(ref attackLevel, Mathf.Max(0, maxAttackLevel));
    }

    public bool TryUpgradeHealth()
    {
        return TryUpgrade(ref healthLevel, Mathf.Max(0, maxHealthLevel));
    }

    public bool TryUpgradeCritChance()
    {
        if (playerController != null && playerController.CritChance >= MaxCritChance)
        {
            return false;
        }

        return TryUpgrade(ref critChanceLevel, GetCritChanceMaxLevel());
    }

    public bool TryUpgradeCritMultiplier()
    {
        if (playerController != null && playerController.CritMultiplier >= MaxCritMultiplier)
        {
            return false;
        }

        return TryUpgrade(ref critMultiplierLevel, GetCritMultiplierMaxLevel());
    }

    public float ApplyCritChance(float baseChance)
    {
        return Mathf.Min(MaxCritChance, Mathf.Clamp01(baseChance) + CritChanceBonus);
    }

    public float ApplyCritMultiplier(float baseMultiplier)
    {
        return Mathf.Min(MaxCritMultiplier, Mathf.Max(1f, baseMultiplier) + CritMultiplierBonus);
    }

    public void ResetAllocations()
    {
        // 디버그 진행도 초기화 시 투자한 강화 레벨도 함께 제거한다.
        attackLevel = 0;
        healthLevel = 0;
        critChanceLevel = 0;
        critMultiplierLevel = 0;
        Save();
    }

    public void ResetAllocationsAndRefund()
    {
        int refundPoints = TotalAllocatedLevels;
        if (refundPoints <= 0)
        {
            return;
        }

        // 기지에서 초기화할 때는 투자한 스탯 포인트를 플레이어에게 반환한다.
        ResetAllocations();
        progression ??= GetComponent<PlayerProgression>();
        progression?.AddStatPoints(refundPoints);
    }

    /// <summary>클라우드 동기화용 스탯 강화 레벨 스냅샷(통합 세이브에 편입).</summary>
    public JinyouPlayerStatAllocatorSaveData CaptureSaveData()
    {
        return new JinyouPlayerStatAllocatorSaveData
        {
            captured = true,
            attackLevel = attackLevel,
            healthLevel = healthLevel,
            critChanceLevel = critChanceLevel,
            critMultiplierLevel = critMultiplierLevel,
        };
    }

    /// <summary>통합 세이브에서 스탯 강화 레벨을 복원한다(클라우드 풀 이후 재적용).</summary>
    public void RestoreSaveData(JinyouPlayerStatAllocatorSaveData data)
    {
        if (data == null || !data.captured)
        {
            return;
        }

        attackLevel = Mathf.Clamp(data.attackLevel, 0, Mathf.Max(0, maxAttackLevel));
        healthLevel = Mathf.Clamp(data.healthLevel, 0, Mathf.Max(0, maxHealthLevel));
        critChanceLevel = Mathf.Clamp(data.critChanceLevel, 0, GetCritChanceMaxLevel());
        critMultiplierLevel = Mathf.Clamp(data.critMultiplierLevel, 0, GetCritMultiplierMaxLevel());
        Save();
    }

    private bool TryUpgrade(ref int currentLevel, int maxLevel)
    {
        if (progression == null)
        {
            progression = GetComponent<PlayerProgression>();
        }

        if (currentLevel >= maxLevel || progression == null || !progression.TrySpendStatPoint())
        {
            return false;
        }

        currentLevel++;
        Save();
        return true;
    }

    private int GetCritChanceMaxLevel()
    {
        float amountPerLevel = Mathf.Max(0.0001f, critChancePerLevel);
        return Mathf.Max(0, Mathf.CeilToInt(MaxCritChance / amountPerLevel));
    }

    private int GetCritMultiplierMaxLevel()
    {
        float amountPerLevel = Mathf.Max(0.0001f, critMultiplierPerLevel);
        return Mathf.Max(0, Mathf.CeilToInt((MaxCritMultiplier - 1f) / amountPerLevel));
    }

    private void Load()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        attackLevel = Mathf.Clamp(PlayerPrefs.GetInt(AttackLevelKey, attackLevel), 0, Mathf.Max(0, maxAttackLevel));
        healthLevel = Mathf.Clamp(PlayerPrefs.GetInt(HealthLevelKey, healthLevel), 0, Mathf.Max(0, maxHealthLevel));
        critChanceLevel = Mathf.Clamp(PlayerPrefs.GetInt(CritChanceLevelKey, critChanceLevel), 0, GetCritChanceMaxLevel());
        critMultiplierLevel = Mathf.Clamp(PlayerPrefs.GetInt(CritMultiplierLevelKey, critMultiplierLevel), 0, GetCritMultiplierMaxLevel());
    }

    private void Save()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        PlayerPrefs.SetInt(AttackLevelKey, attackLevel);
        PlayerPrefs.SetInt(HealthLevelKey, healthLevel);
        PlayerPrefs.SetInt(CritChanceLevelKey, critChanceLevel);
        PlayerPrefs.SetInt(CritMultiplierLevelKey, critMultiplierLevel);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 통합 세이브 사용 시 전역 PlayerPrefs 개별 저장을 끈다. 키가 UID 스코프가 아니라서
    /// 계정 전환 시 이전 계정의 스탯이 새 계정으로 새는 것을 막는다.
    /// </summary>
    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(AttackLevelKey);
        PlayerPrefs.DeleteKey(HealthLevelKey);
        PlayerPrefs.DeleteKey(CritChanceLevelKey);
        PlayerPrefs.DeleteKey(CritMultiplierLevelKey);
        PlayerPrefs.Save();
    }

    private void MigrateLegacyTraitPoints()
    {
        int legacyPoints = PlayerPrefs.GetInt(LegacyAttackPointKey, 0)
            + PlayerPrefs.GetInt(LegacyMaxHealthPointKey, 0)
            + PlayerPrefs.GetInt(LegacyCritChancePointKey, 0)
            + PlayerPrefs.GetInt(LegacyCritMultiplierPointKey, 0);
        if (legacyPoints <= 0)
        {
            return;
        }

        // 적용되지 않던 구 특성 투자량은 새 스탯 시스템에서 다시 쓸 수 있도록 환급한다.
        progression?.AddStatPoints(legacyPoints);
        PlayerPrefs.DeleteKey(LegacyAttackPointKey);
        PlayerPrefs.DeleteKey(LegacyMaxHealthPointKey);
        PlayerPrefs.DeleteKey(LegacyCritChancePointKey);
        PlayerPrefs.DeleteKey(LegacyCritMultiplierPointKey);
        PlayerPrefs.Save();
    }
}
