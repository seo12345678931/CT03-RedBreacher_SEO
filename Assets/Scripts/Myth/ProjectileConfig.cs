using UnityEngine;

public interface IDuplicateLevelConfig
{
    string Id { get; }
    int MaxLevel { get; }
    int MaxLevelDuplicateCoreCrystalReward { get; }
}

public static class DuplicateLevelProgression
{
    public static int AddDuplicates(
        IDuplicateLevelConfig config,
        ref int level,
        ref int duplicateProgress,
        int quantity)
    {
        if (config == null || quantity <= 0)
        {
            return 0;
        }

        level = Mathf.Clamp(level, 1, config.MaxLevel);
        duplicateProgress = Mathf.Max(0, duplicateProgress);
        int remainingCopies = quantity;

        while (remainingCopies > 0 && level < config.MaxLevel)
        {
            duplicateProgress++;
            remainingCopies--;

            if (duplicateProgress >= GetRequiredDuplicates(level, config.MaxLevel))
            {
                duplicateProgress -= level;
                level++;
            }
        }

        if (level >= config.MaxLevel)
        {
            duplicateProgress = 0;
        }

        return remainingCopies;
    }

    public static int GetRequiredDuplicates(int level, int maxLevel)
    {
        return level > 0 && level < Mathf.Max(1, maxLevel) ? level : 0;
    }

    public static float GetLevelMultiplier(int level, float percentPerLevel)
    {
        return 1f + Mathf.Max(0f, percentPerLevel) * (Mathf.Max(1, level) - 1);
    }
}

public enum MultiMuzzleFireMode
{
    Single,
    SplitDamage,
    BurstKeepsDps,
    FullPowerBurst
}

public enum WeaponAttackType
{
    SingleTarget,
    Area,
    Piercing
}

[CreateAssetMenu(menuName = "Myth/Combat/Projectile Config")]
public class ProjectileConfig : ScriptableObject, IDuplicateLevelConfig
{
    [Header("Identity")]
    [SerializeField] private string id = "weapon_default";
    [SerializeField] private string displayName = "기본 무기";
    [SerializeField] private Sprite icon;
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Combat")]
    [SerializeField] private float attackDamage = 0f;
    [SerializeField] private WeaponAttackType attackType = WeaponAttackType.SingleTarget;
    [SerializeField] private float areaRadius = 2f;
    [SerializeField] private float areaDamageMultiplier = 0.7f;
    [SerializeField] private int maxAreaTargets = 10;
    [SerializeField] private int maxPierceTargets = 3;

    [Header("Collection Level")]
    [SerializeField] private int maxLevel = 10;
    [SerializeField] private float damagePercentPerLevel = 0.1f;
    [SerializeField] private int maxLevelDuplicateCoreCrystalReward = 1;

    [Header("Projectile")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float knockbackForce = 2f;

    [Header("Muzzle")]
    [SerializeField] private MultiMuzzleFireMode multiMuzzleFireMode = MultiMuzzleFireMode.BurstKeepsDps;
    [SerializeField] private int maxBurstMuzzleCount = 3;
    [SerializeField] private string muzzleNamePrefix = "FireMuzzle";

    [Header("Effects")]
    [SerializeField] private GameObject fireFlashEffectPrefab;
    [SerializeField] private GameObject projectileEffectPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float effectCleanupDelay = 2f;
    
    [Header("SFX")]
    [SerializeField] private AudioClip sfxFire;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Rarity Rarity => rarity;
    public string WeaponCategory => attackType switch
    {
        WeaponAttackType.Area => "폭발탄",
        WeaponAttackType.Piercing => "철갑탄",
        _ => "일반탄"
    };
    public float AttackDamage => attackDamage;
    public WeaponAttackType AttackType => attackType;
    public float AreaRadius => areaRadius;
    public float AreaDamageMultiplier => areaDamageMultiplier;
    public int MaxAreaTargets => maxAreaTargets;
    public int MaxPierceTargets => Mathf.Max(1, maxPierceTargets);
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public float DamagePercentPerLevel => Mathf.Max(0f, damagePercentPerLevel);
    public int MaxLevelDuplicateCoreCrystalReward => Mathf.Max(0, maxLevelDuplicateCoreCrystalReward);
    public float Speed => speed;
    public float Lifetime => lifetime;
    public float CollisionRadius => collisionRadius;
    public float KnockbackForce => knockbackForce;
    public MultiMuzzleFireMode MultiMuzzleFireMode => multiMuzzleFireMode;
    public int MaxBurstMuzzleCount => maxBurstMuzzleCount;
    public string MuzzleNamePrefix => muzzleNamePrefix;
    public GameObject FireFlashEffectPrefab => fireFlashEffectPrefab;
    public GameObject ProjectileEffectPrefab => projectileEffectPrefab;
    public GameObject HitEffectPrefab => hitEffectPrefab;
    public float EffectCleanupDelay => effectCleanupDelay;
    public AudioClip SfxFire => sfxFire;
}
