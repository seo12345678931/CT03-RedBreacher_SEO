using System.Collections.Generic;
using UnityEngine;

public enum PlayerSkillType
{
    Bombardment,
    AutoTurret,
    MissileTurret,
    StealthBomber,
    AttackHelicopter
}

[System.Serializable]
public class PlayerSkillLevelSkinStyle
{
    [SerializeField, Min(1)] private int collectionLevel = 1;
    [SerializeField] private SkinEditor_SkillLevel.BodyPaint bodyPaint = SkinEditor_SkillLevel.BodyPaint.None;
    [SerializeField] private SkinEditor_SkillLevel.ChevronMark chevronMark = SkinEditor_SkillLevel.ChevronMark.None;

    public int CollectionLevel => Mathf.Max(1, collectionLevel);
    public SkinEditor_SkillLevel.BodyPaint BodyPaint => bodyPaint;
    public SkinEditor_SkillLevel.ChevronMark ChevronMark => chevronMark;
}

[CreateAssetMenu(menuName = "Myth/Combat/Player Skill Config")]
public class PlayerSkillConfig : ScriptableObject, IDuplicateLevelConfig
{
    [Header("Identity")]
    [SerializeField] private string id = "skill_default";
    [SerializeField] private string displayName = "자동 스킬";
    [SerializeField] private Sprite icon;
    [SerializeField] private Rarity rarity = Rarity.Common;
    [SerializeField] private PlayerSkillType skillType;
    [TextArea(4, 8)]
    [SerializeField] private string description;

    [Header("Auto Cast")]
    [SerializeField] private float cooldown = 10f;
    [SerializeField] private float castRange = 8f;
    [SerializeField] private int minimumEnemyCount = 1;

    [Header("Damage")]
    [SerializeField] private float attackPowerMultiplier = 2f;
    [SerializeField] private float flatDamage;
    [SerializeField] private bool canCritical;
    [SerializeField] private float effectRadius = 3f;
    [SerializeField] private int maxTargets = 20;
    [SerializeField] private float knockbackForce;

    [Header("Collection Level")]
    [SerializeField] private int maxLevel = 10;
    [SerializeField] private float damagePercentPerLevel = 0.1f;
    [SerializeField] private float cooldownReductionPercentPerLevel = 0.03f;
    [SerializeField] private float maxCooldownReductionPercent = 0.5f;
    [SerializeField] private int countIncreaseLevelInterval = 4;
    [SerializeField] private int bombCountIncreasePerInterval = 1;
    [SerializeField] private int turretCountIncreasePerInterval = 1;
    [SerializeField] private int maxLevelDuplicateCoreCrystalReward = 1;

    [Header("Collection Level Skin")]
    [SerializeField] private List<PlayerSkillLevelSkinStyle> levelSkinStyles = new List<PlayerSkillLevelSkinStyle>();

    [Header("Bombardment")]
    [SerializeField] private float impactDelay = 0.5f;
    [SerializeField] private GameObject warningEffectPrefab;
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private float effectCleanupDelay = 2f;
    [SerializeField] private GameObject _airplanePrefab;
    [SerializeField] private float _airplaneSpeed = 15f;
    [SerializeField] private float _airplaneSpawnOffset = 15f;
    [SerializeField] private float _airplaneHeight = 6f;
    [SerializeField] private GameObject _bombProjectilePrefab;
    [SerializeField] private float _bombEffectScale = 1f;
    [SerializeField] private int _bombCount = 1;
    [SerializeField] private float _bombInterval = 0.15f;
    [SerializeField] private float _screenShakeDuration = 0.22f;
    [SerializeField] private float _screenShakeStrength = 0.4f;
    [SerializeField] private float _screenShakeFrequency = 1.5f;
    [SerializeField] private AudioClip _airplaneTakeOffSfx;
    [SerializeField] private AudioClip[] impactSfx;

    [Header("Auto Turret")]
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private ProjectileConfig turretProjectileConfig;
    [SerializeField] private float turretDuration = 8f;
    [SerializeField] private float turretAttackInterval = 0.8f;
    [SerializeField] private float turretAttackRange = 6f;
    [SerializeField] private float turretRotationSpeed = 360f;
    [SerializeField] private float turretPlacementDistance = 1.5f;
    [SerializeField] private string turretFirePointName = "FirePoint";
    [SerializeField] private AudioClip turretFireSfx;

    [Header("Missile Turret")]
    [SerializeField] private GameObject missileTurretPrefab;
    [SerializeField] private GameObject missileProjectilePrefab;
    [SerializeField] private float missileTurretDuration = 8f;
    [SerializeField] private float missileTurretAttackInterval = 1.2f;
    [SerializeField] private float missileTurretAttackRange = 7f;
    [SerializeField] private float missileTurretRotationSpeed = 240f;
    [SerializeField] private float missileTurretPlacementDistance = 1.8f;
    [SerializeField] private float missileExplosionRadius = 2.2f;
    [SerializeField] private float missileProjectileSpeed = 12f;
    [SerializeField] private string missileTurretFirePointName = "FirePoint";
    [SerializeField] private GameObject missileImpactEffectPrefab;
    [SerializeField] private AudioClip missileTurretFireSfx;

    [Header("Stealth Bomber")]
    [SerializeField] private GameObject stealthBomberPrefab;
    [SerializeField] private float stealthBomberSpeed = 24f;
    [SerializeField] private float stealthBomberTravelDistance = 18f;
    [SerializeField] private float stealthBomberHeight = 6f;
    [SerializeField] private int stealthBomberRows = 1;
    [SerializeField] private int stealthBomberColumns = 3;
    [SerializeField] private float stealthBomberSpacing = 2.2f;
    [SerializeField] private float stealthBomberBombInterval = 0.12f;
    [SerializeField] private float stealthBomberExplosionRadius = 2.5f;
    [SerializeField] private Sprite stealthBomberShadowSprite; // 비우면 절차적 그림자 사용
    [SerializeField] private float stealthBomberShadowSize = 7f;
    [SerializeField] private float stealthBomberShadowDarkness = 0.55f;

    [Header("Attack Helicopter")]
    [SerializeField] private GameObject attackHelicopterPrefab;
    [SerializeField] private ProjectileConfig helicopterProjectileConfig; // 헬기 공격 투사체(터렛과 동일 방식)
    [SerializeField] private GameObject helicopterRocketPrefab;           // 오프닝 예고 로켓(지정 지점 착탄)
    [SerializeField] private float attackHelicopterDuration = 15f;
    [SerializeField] private float attackHelicopterAttackInterval = 0.8f;
    [SerializeField] private float attackHelicopterAttackRange = 7f;
    [SerializeField] private float attackHelicopterMoveSpeed = 5f;
    [SerializeField] private float attackHelicopterFollowOffset = 2.5f;
    [SerializeField] private float attackHelicopterMaxDistanceFromPlayer = 6f; // 이 반경 밖으로는 안 나감
    [SerializeField] private int helicopterOpeningRocketCount = 2;
    [SerializeField] private float helicopterRocketRadius = 2f;
    [SerializeField] private float helicopterRocketSpeed = 16f;
    [SerializeField] private string helicopterRotorName = "Propeller"; // 회전시킬 자식 이름(비우면 비활성)
    [SerializeField] private float helicopterRotorSpeed = 900f;
    [SerializeField] private AudioClip helicopterRocketLunchSfx;
    [SerializeField] private AudioClip firstRocketLunchSfx;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Rarity Rarity => rarity;
    public PlayerSkillType SkillType => skillType;
    public string Description => description;
    public float Cooldown => Mathf.Max(0.1f, cooldown);
    public float CastRange => Mathf.Max(0f, castRange);
    public int MinimumEnemyCount => Mathf.Max(1, minimumEnemyCount);
    public float AttackPowerMultiplier => Mathf.Max(0f, attackPowerMultiplier);
    public float FlatDamage => Mathf.Max(0f, flatDamage);
    public bool CanCritical => canCritical;
    public float EffectRadius => Mathf.Max(0f, effectRadius);
    public int MaxTargets => Mathf.Max(1, maxTargets);
    public float KnockbackForce => Mathf.Max(0f, knockbackForce);
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public float DamagePercentPerLevel => Mathf.Max(0f, damagePercentPerLevel);
    public float CooldownReductionPercentPerLevel => Mathf.Max(0f, cooldownReductionPercentPerLevel);
    public float MaxCooldownReductionPercent => Mathf.Clamp(maxCooldownReductionPercent, 0f, 0.95f);
    public int CountIncreaseLevelInterval => Mathf.Max(1, countIncreaseLevelInterval);
    public int BombCountIncreasePerInterval => Mathf.Max(0, bombCountIncreasePerInterval);
    public int TurretCountIncreasePerInterval => Mathf.Max(0, turretCountIncreasePerInterval);
    public int MaxLevelDuplicateCoreCrystalReward => Mathf.Max(0, maxLevelDuplicateCoreCrystalReward);
    public IReadOnlyList<PlayerSkillLevelSkinStyle> LevelSkinStyles => levelSkinStyles;
    public float ImpactDelay => Mathf.Max(0f, impactDelay);
    public GameObject WarningEffectPrefab => warningEffectPrefab;
    public GameObject ImpactEffectPrefab => impactEffectPrefab;
    public AudioClip[] ImpactSfx => impactSfx;
    public float EffectCleanupDelay => Mathf.Max(0f, effectCleanupDelay);
    public GameObject AirplanePrefab => _airplanePrefab;
    public float AirplaneSpeed => Mathf.Max(0.1f, _airplaneSpeed);
    public float AirplaneSpawnOffset => Mathf.Max(0f, _airplaneSpawnOffset);
    public float AirplaneHeight => _airplaneHeight;
    public GameObject BombProjectilePrefab => _bombProjectilePrefab;
    public float BombEffectScale => Mathf.Max(0.01f, _bombEffectScale);
    public int BombCount => Mathf.Max(1, _bombCount);
    public float BombInterval => Mathf.Max(0f, _bombInterval);
    public float ScreenShakeDuration => Mathf.Max(0f, _screenShakeDuration);
    public float ScreenShakeStrength => Mathf.Max(0f, _screenShakeStrength);
    public float ScreenShakeFrequency => Mathf.Max(0.01f, _screenShakeFrequency);
    public AudioClip AirplaneTakeOffSfx => _airplaneTakeOffSfx;
    public GameObject TurretPrefab => turretPrefab;
    public ProjectileConfig TurretProjectileConfig => turretProjectileConfig;
    public ProjectileConfig MissileProjectileConfig => turretProjectileConfig;
    public float TurretDuration => Mathf.Max(0.1f, turretDuration);
    public float TurretAttackInterval => Mathf.Max(0.1f, turretAttackInterval);
    public float TurretAttackRange => Mathf.Max(0f, turretAttackRange);
    public float TurretRotationSpeed => Mathf.Max(0f, turretRotationSpeed);
    public float TurretPlacementDistance => Mathf.Max(0f, turretPlacementDistance);
    public string TurretFirePointName => turretFirePointName;
    public AudioClip TurretFireSfx => turretFireSfx;
    public GameObject MissileTurretPrefab => missileTurretPrefab;
    public GameObject MissileProjectilePrefab => missileProjectilePrefab;
    public float MissileTurretDuration => Mathf.Max(0.1f, missileTurretDuration);
    public float MissileTurretAttackInterval => Mathf.Max(0.1f, missileTurretAttackInterval);
    public float MissileTurretAttackRange => Mathf.Max(0f, missileTurretAttackRange);
    public float MissileTurretRotationSpeed => Mathf.Max(0f, missileTurretRotationSpeed);
    public float MissileTurretPlacementDistance => Mathf.Max(0f, missileTurretPlacementDistance);
    public float MissileExplosionRadius => Mathf.Max(0.1f, missileExplosionRadius);
    public float MissileProjectileSpeed => Mathf.Max(0.1f, missileProjectileSpeed);
    public string MissileTurretFirePointName => missileTurretFirePointName;
    public GameObject MissileImpactEffectPrefab => missileImpactEffectPrefab;
    public AudioClip MissileTurretFireSfx => missileTurretFireSfx;
    public GameObject StealthBomberPrefab => stealthBomberPrefab;
    public float StealthBomberSpeed => Mathf.Max(0.1f, stealthBomberSpeed);
    public float StealthBomberTravelDistance => Mathf.Max(1f, stealthBomberTravelDistance);
    public float StealthBomberHeight => stealthBomberHeight;
    public int StealthBomberRows => Mathf.Max(1, stealthBomberRows);
    public int StealthBomberColumns => Mathf.Max(1, stealthBomberColumns);
    public float StealthBomberSpacing => Mathf.Max(0.1f, stealthBomberSpacing);
    public float StealthBomberBombInterval => Mathf.Max(0f, stealthBomberBombInterval);
    public float StealthBomberExplosionRadius => Mathf.Max(0.1f, stealthBomberExplosionRadius);
    public Sprite StealthBomberShadowSprite => stealthBomberShadowSprite;
    public float StealthBomberShadowSize => Mathf.Max(0.1f, stealthBomberShadowSize);
    public float StealthBomberShadowDarkness => Mathf.Clamp01(stealthBomberShadowDarkness);
    public GameObject AttackHelicopterPrefab => attackHelicopterPrefab;
    public ProjectileConfig HelicopterProjectileConfig => helicopterProjectileConfig;
    public GameObject HelicopterRocketPrefab => helicopterRocketPrefab;
    public float AttackHelicopterDuration => Mathf.Max(0.1f, attackHelicopterDuration);
    public float AttackHelicopterAttackInterval => Mathf.Max(0.1f, attackHelicopterAttackInterval);
    public float AttackHelicopterAttackRange => Mathf.Max(0f, attackHelicopterAttackRange);
    public float AttackHelicopterMoveSpeed => Mathf.Max(0.1f, attackHelicopterMoveSpeed);
    public float AttackHelicopterFollowOffset => Mathf.Max(0f, attackHelicopterFollowOffset);
    public float AttackHelicopterMaxDistanceFromPlayer => Mathf.Max(1f, attackHelicopterMaxDistanceFromPlayer);
    public int HelicopterOpeningRocketCount => Mathf.Max(0, helicopterOpeningRocketCount);
    public float HelicopterRocketRadius => Mathf.Max(0.1f, helicopterRocketRadius);
    public float HelicopterRocketSpeed => Mathf.Max(0.1f, helicopterRocketSpeed);
    public string HelicopterRotorName => helicopterRotorName;
    public float HelicopterRotorSpeed => helicopterRotorSpeed;
    public AudioClip HelicopterRocketLunchSfx => helicopterRocketLunchSfx;
    public AudioClip FirstRocketLunchSfx => firstRocketLunchSfx;

    public float GetCooldown(int level)
    {
        // 수집 레벨이 오를수록 쿨타임을 줄이되 0초에 가까워지지 않도록 상한을 둔다.
        float reduction = Mathf.Min(
            MaxCooldownReductionPercent,
            CooldownReductionPercentPerLevel * GetLevelBonusCount(level));
        return Mathf.Max(0.1f, Cooldown * (1f - reduction));
    }

    public int GetBombCount(int level)
    {
        return BombCount + GetCountIncreaseStepCount(level) * BombCountIncreasePerInterval;
    }

    public int GetTurretCount(int level)
    {
        return 1 + GetCountIncreaseStepCount(level) * TurretCountIncreasePerInterval;
    }

    public PlayerSkillLevelSkinStyle GetSkinStyle(int collectionLevel)
    {
        if (levelSkinStyles == null || levelSkinStyles.Count == 0)
        {
            return null;
        }

        int level = Mathf.Clamp(collectionLevel, 1, MaxLevel);
        PlayerSkillLevelSkinStyle bestMatch = null;
        int bestLevel = 0;

        for (int i = 0; i < levelSkinStyles.Count; i++)
        {
            PlayerSkillLevelSkinStyle style = levelSkinStyles[i];
            if (style == null)
            {
                continue;
            }

            int styleLevel = style.CollectionLevel;
            if (styleLevel <= level && styleLevel >= bestLevel)
            {
                bestMatch = style;
                bestLevel = styleLevel;
            }
        }

        return bestMatch;
    }

    private static int GetLevelBonusCount(int level)
    {
        return Mathf.Max(1, level) - 1;
    }

    private int GetCountIncreaseStepCount(int level)
    {
        return GetLevelBonusCount(level) / CountIncreaseLevelInterval;
    }
}
