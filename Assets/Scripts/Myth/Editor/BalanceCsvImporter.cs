#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BalanceCsvImporter
{
    private const string UnitCsvPath = "Assets/Data/Balance/units.csv";
    private const string WeaponCsvPath = "Assets/Data/Balance/weapons.csv";
    private const string SkillCsvPath = "Assets/Data/Balance/skills.csv";
    private const string EnemyCsvPath = "Assets/Data/Balance/enemies.csv";
    private const string DroneCsvPath = "Assets/Data/Balance/drones.csv";
    private const string EquipmentPartCsvPath = "Assets/Data/Balance/equipment_parts.csv";
    private const string GachaWeaponCsvPath = "Assets/Data/Balance/gacha_weapons.csv";
    private const string GachaSkillCsvPath = "Assets/Data/Balance/gacha_skills.csv";

    private const string UnitOutputPath = "Assets/SO/Balance/Units";
    private const string WeaponOutputPath = "Assets/SO/Balance/Weapons";
    private const string SkillOutputPath = "Assets/SO/Balance/Skills";
    private const string EnemyOutputPath = "Assets/SO/Balance/Enemies";
    private const string DroneOutputPath = "Assets/SO/Balance/Drones";
    private const string EquipmentPartOutputPath = "Assets/SO/Balance/EquipmentParts";
    private const string GachaOutputPath = "Assets/SO/Balance/Gacha";

    [MenuItem("Tools/Balance/CSV to SO/All")]
    public static void ImportAll()
    {
        EnsureOutputFolders();
        ImportWeapons();
        ImportSkills();
        ImportUnits();
        ImportEnemies();
        ImportDrones();
        ImportEquipmentParts();
        ImportGachaWeapons();
        ImportGachaSkills();
        BaseCampBalanceCsvImporter.Import();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("밸런스 CSV 임포트 완료");
    }

    [MenuItem("Tools/Balance/SO to CSV/All")]
    public static void ExportAll()
    {
        ExportUnits();
        ExportWeapons();
        ExportSkills();
        ExportEnemies();
        ExportDrones();
        ExportEquipmentParts();
        ExportGachaWeapons();
        ExportGachaSkills();
        AssetDatabase.Refresh();
        Debug.Log("밸런스 SO CSV 내보내기 완료");
    }

    [MenuItem("Tools/Balance/CSV to SO/Units")]
    public static void ImportUnits()
    {
        foreach (Dictionary<string, string> row in ReadCsv(UnitCsvPath))
        {
            string id = GetRequired(row, "id", UnitCsvPath);
            PlayerUnitConfig config = LoadOrCreate<PlayerUnitConfig>(UnitOutputPath, id, "Unit");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "unitPrefab", GetAsset<GameObject>(row, "unitPrefab"));
            SetFloat(serializedObject, "maxHealth", row, "maxHealth");
            SetFloat(serializedObject, "critChance", row, "critChance");
            SetFloat(serializedObject, "critMultiplier", row, "critMultiplier");
            SetFloat(serializedObject, "attackRange", row, "attackRange");
            SetFloat(serializedObject, "attackDamage", row, "attackDamage");
            SetFloat(serializedObject, "attackInterval", row, "attackInterval");
            SetFloat(serializedObject, "moveSpeed", row, "moveSpeed");
            SetFloat(serializedObject, "rotationSpeed", row, "rotationSpeed");
            SetFloat(serializedObject, "fireAngleTolerance", row, "fireAngleTolerance");
            SetFloat(serializedObject, "repositionDistance", row, "repositionDistance");
            SetFloat(serializedObject, "repositionCooldown", row, "repositionCooldown");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Weapons")]
    public static void ImportWeapons()
    {
        foreach (Dictionary<string, string> row in ReadCsv(WeaponCsvPath))
        {
            string id = GetRequired(row, "id", WeaponCsvPath);
            ProjectileConfig config = LoadOrCreate<ProjectileConfig>(WeaponOutputPath, id, "Weapon");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "icon", GetAsset<Sprite>(row, "icon"));
            SetEnum(serializedObject, "rarity", row, "rarity");
            SetFloat(serializedObject, "attackDamage", row, "attackDamage");
            SetEnum(serializedObject, "attackType", row, "attackType");
            SetFloat(serializedObject, "areaRadius", row, "areaRadius");
            SetFloat(serializedObject, "areaDamageMultiplier", row, "areaDamageMultiplier");
            SetInt(serializedObject, "maxAreaTargets", row, "maxAreaTargets");
            SetInt(serializedObject, "maxPierceTargets", row, "maxPierceTargets");
            SetInt(serializedObject, "maxLevel", row, "maxLevel");
            SetFloat(serializedObject, "damagePercentPerLevel", row, "damagePercentPerLevel");
            SetInt(serializedObject, "maxLevelDuplicateCoreCrystalReward", row, "maxLevelDuplicateCoreCrystalReward");
            SetFloat(serializedObject, "speed", row, "speed");
            SetFloat(serializedObject, "lifetime", row, "lifetime");
            SetFloat(serializedObject, "collisionRadius", row, "collisionRadius");
            SetFloat(serializedObject, "knockbackForce", row, "knockbackForce");
            SetEnum(serializedObject, "multiMuzzleFireMode", row, "multiMuzzleFireMode");
            SetInt(serializedObject, "maxBurstMuzzleCount", row, "maxBurstMuzzleCount");
            SetString(serializedObject, "muzzleNamePrefix", Get(row, "muzzleNamePrefix"));
            SetObject(serializedObject, "fireFlashEffectPrefab", GetAsset<GameObject>(row, "fireFlashEffect"));
            SetObject(serializedObject, "projectileEffectPrefab", GetAsset<GameObject>(row, "projectileEffect"));
            SetObject(serializedObject, "hitEffectPrefab", GetAsset<GameObject>(row, "hitEffect"));
            SetFloat(serializedObject, "effectCleanupDelay", row, "effectCleanupDelay");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Skills")]
    public static void ImportSkills()
    {
        EnsureFolder(SkillOutputPath);
        foreach (Dictionary<string, string> row in ReadCsv(SkillCsvPath))
        {
            string id = GetRequired(row, "id", SkillCsvPath);
            PlayerSkillConfig config = LoadOrCreate<PlayerSkillConfig>(SkillOutputPath, id, "Skill");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "icon", GetAsset<Sprite>(row, "icon"));
            SetEnum(serializedObject, "rarity", row, "rarity");
            SetEnum(serializedObject, "skillType", row, "skillType");
            SetFloat(serializedObject, "cooldown", row, "cooldown");
            SetFloat(serializedObject, "castRange", row, "castRange");
            SetInt(serializedObject, "minimumEnemyCount", row, "minimumEnemyCount");
            SetFloat(serializedObject, "attackPowerMultiplier", row, "attackPowerMultiplier");
            SetFloat(serializedObject, "flatDamage", row, "flatDamage");
            SetBool(serializedObject, "canCritical", row, "canCritical");
            SetFloat(serializedObject, "effectRadius", row, "effectRadius");
            SetInt(serializedObject, "maxTargets", row, "maxTargets");
            SetFloat(serializedObject, "knockbackForce", row, "knockbackForce");
            SetInt(serializedObject, "maxLevel", row, "maxLevel");
            SetFloat(serializedObject, "damagePercentPerLevel", row, "damagePercentPerLevel");
            SetFloat(serializedObject, "cooldownReductionPercentPerLevel", row, "cooldownReductionPercentPerLevel");
            SetFloat(serializedObject, "maxCooldownReductionPercent", row, "maxCooldownReductionPercent");
            SetInt(serializedObject, "countIncreaseLevelInterval", row, "countIncreaseLevelInterval");
            SetInt(serializedObject, "bombCountIncreasePerInterval", row, "bombCountIncreasePerInterval");
            SetInt(serializedObject, "turretCountIncreasePerInterval", row, "turretCountIncreasePerInterval");
            SetInt(serializedObject, "maxLevelDuplicateCoreCrystalReward", row, "maxLevelDuplicateCoreCrystalReward");
            SetFloat(serializedObject, "impactDelay", row, "impactDelay");
            SetObject(serializedObject, "warningEffectPrefab", GetAsset<GameObject>(row, "warningEffect"));
            SetObject(serializedObject, "impactEffectPrefab", GetAsset<GameObject>(row, "impactEffect"));
            SetFloat(serializedObject, "effectCleanupDelay", row, "effectCleanupDelay");
            SetObject(serializedObject, "_airplanePrefab", GetAsset<GameObject>(row, "airplanePrefab"));
            SetFloat(serializedObject, "_airplaneSpeed", row, "airplaneSpeed");
            SetFloat(serializedObject, "_airplaneSpawnOffset", row, "airplaneSpawnOffset");
            SetFloat(serializedObject, "_airplaneHeight", row, "airplaneHeight");
            SetObject(serializedObject, "_bombProjectilePrefab", GetAsset<GameObject>(row, "bombProjectilePrefab"));
            SetFloat(serializedObject, "_bombEffectScale", row, "bombEffectScale");
            SetInt(serializedObject, "_bombCount", row, "bombCount");
            SetFloat(serializedObject, "_bombInterval", row, "bombInterval");
            SetFloat(serializedObject, "_screenShakeDuration", row, "screenShakeDuration");
            SetFloat(serializedObject, "_screenShakeStrength", row, "screenShakeStrength");
            SetFloat(serializedObject, "_screenShakeFrequency", row, "screenShakeFrequency");
            SetObject(serializedObject, "turretPrefab", GetAsset<GameObject>(row, "turretPrefab"));
            SetObject(serializedObject, "turretProjectileConfig", GetAsset<ProjectileConfig>(row, "turretProjectileConfig"));
            SetFloat(serializedObject, "turretDuration", row, "turretDuration");
            SetFloat(serializedObject, "turretAttackInterval", row, "turretAttackInterval");
            SetFloat(serializedObject, "turretAttackRange", row, "turretAttackRange");
            SetFloat(serializedObject, "turretRotationSpeed", row, "turretRotationSpeed");
            SetFloat(serializedObject, "turretPlacementDistance", row, "turretPlacementDistance");
            SetString(serializedObject, "turretFirePointName", Get(row, "turretFirePointName"));
            SetObject(serializedObject, "missileTurretPrefab", GetAsset<GameObject>(row, "missileTurretPrefab"));
            SetObject(serializedObject, "missileProjectilePrefab", GetAsset<GameObject>(row, "missileProjectilePrefab"));
            SetFloat(serializedObject, "missileTurretDuration", row, "missileTurretDuration");
            SetFloat(serializedObject, "missileTurretAttackInterval", row, "missileTurretAttackInterval");
            SetFloat(serializedObject, "missileTurretAttackRange", row, "missileTurretAttackRange");
            SetFloat(serializedObject, "missileTurretRotationSpeed", row, "missileTurretRotationSpeed");
            SetFloat(serializedObject, "missileTurretPlacementDistance", row, "missileTurretPlacementDistance");
            SetFloat(serializedObject, "missileExplosionRadius", row, "missileExplosionRadius");
            SetFloat(serializedObject, "missileProjectileSpeed", row, "missileProjectileSpeed");
            SetString(serializedObject, "missileTurretFirePointName", Get(row, "missileTurretFirePointName"));
            SetObject(serializedObject, "stealthBomberPrefab", GetAsset<GameObject>(row, "stealthBomberPrefab"));
            SetFloat(serializedObject, "stealthBomberSpeed", row, "stealthBomberSpeed");
            SetFloat(serializedObject, "stealthBomberTravelDistance", row, "stealthBomberTravelDistance");
            SetFloat(serializedObject, "stealthBomberHeight", row, "stealthBomberHeight");
            SetInt(serializedObject, "stealthBomberRows", row, "stealthBomberRows");
            SetInt(serializedObject, "stealthBomberColumns", row, "stealthBomberColumns");
            SetFloat(serializedObject, "stealthBomberSpacing", row, "stealthBomberSpacing");
            SetFloat(serializedObject, "stealthBomberBombInterval", row, "stealthBomberBombInterval");
            SetFloat(serializedObject, "stealthBomberExplosionRadius", row, "stealthBomberExplosionRadius");
            SetObject(serializedObject, "attackHelicopterPrefab", GetAsset<GameObject>(row, "attackHelicopterPrefab"));
            SetObject(serializedObject, "helicopterRocketPrefab", GetAsset<GameObject>(row, "helicopterRocketPrefab"));
            SetFloat(serializedObject, "attackHelicopterDuration", row, "attackHelicopterDuration");
            SetFloat(serializedObject, "attackHelicopterAttackInterval", row, "attackHelicopterAttackInterval");
            SetFloat(serializedObject, "attackHelicopterAttackRange", row, "attackHelicopterAttackRange");
            SetFloat(serializedObject, "attackHelicopterMoveSpeed", row, "attackHelicopterMoveSpeed");
            SetFloat(serializedObject, "attackHelicopterFollowOffset", row, "attackHelicopterFollowOffset");
            SetInt(serializedObject, "helicopterOpeningRocketCount", row, "helicopterOpeningRocketCount");
            SetFloat(serializedObject, "helicopterRocketRadius", row, "helicopterRocketRadius");
            SetFloat(serializedObject, "helicopterRocketSpeed", row, "helicopterRocketSpeed");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Enemies")]
    public static void ImportEnemies()
    {
        foreach (Dictionary<string, string> row in ReadCsv(EnemyCsvPath))
        {
            string id = GetRequired(row, "id", EnemyCsvPath);
            EnemyConfig config = LoadOrCreate<EnemyConfig>(EnemyOutputPath, id, "Enemy");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "enemyPrefab", GetAsset<GameObject>(row, "enemyPrefab"));
            SetFloat(serializedObject, "maxHealth", row, "maxHealth");
            SetFloat(serializedObject, "moveSpeed", row, "moveSpeed");
            SetFloat(serializedObject, "stopDistance", row, "stopDistance");
            SetFloat(serializedObject, "contactDamage", row, "contactDamage");
            SetFloat(serializedObject, "contactInterval", row, "contactInterval");
            SetFloat(serializedObject, "experienceReward", row, "experienceReward");
            SetInt(serializedObject, "creditReward", row, "creditReward");
            SetInt(serializedObject, "coreCrystalReward", row, "coreCrystalReward");
            SetFloat(serializedObject, "partDropChance", row, "partDropChance");
            SetObject(serializedObject, "deathExplosionEffectPrefab", GetAsset<GameObject>(row, "deathExplosionEffectPrefab"));
            SetFloat(serializedObject, "deathExplosionCleanupDelay", row, "deathExplosionCleanupDelay");
            SetVector3(
                serializedObject,
                "deathExplosionOffset",
                row,
                "deathExplosionOffsetX",
                "deathExplosionOffsetY",
                "deathExplosionOffsetZ");
            SetFloat(serializedObject, "deathExplosionScale", row, "deathExplosionScale");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Drones")]
    public static void ImportDrones()
    {
        foreach (Dictionary<string, string> row in ReadCsv(DroneCsvPath))
        {
            string id = GetRequired(row, "id", DroneCsvPath);
            DroneConfig config = LoadOrCreate<DroneConfig>(DroneOutputPath, id, "Drone");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "dronePrefab", GetAsset<GameObject>(row, "dronePrefab"));
            SetObject(serializedObject, "projectileConfig", GetAsset<ProjectileConfig>(row, "projectileConfig"));
            SetFloat(serializedObject, "attackDamage", row, "attackDamage");
            SetFloat(serializedObject, "attackRange", row, "attackRange");
            SetFloat(serializedObject, "attackInterval", row, "attackInterval");
            SetFloat(serializedObject, "fireAngleTolerance", row, "fireAngleTolerance");
            SetFloat(serializedObject, "rotationSpeed", row, "rotationSpeed");
            SetInt(serializedObject, "targetMask", row, "targetMask");
            SetFloat(serializedObject, "projectileSpeed", row, "projectileSpeed");
            SetFloat(serializedObject, "projectileLifetime", row, "projectileLifetime");
            SetInt(serializedObject, "droneCount", row, "droneCount");
            SetFloat(serializedObject, "followRadius", row, "followRadius");
            SetFloat(serializedObject, "followSpeed", row, "followSpeed");
            SetFloat(serializedObject, "startAngle", row, "startAngle");
            SetFloat(serializedObject, "angleStep", row, "angleStep");
            SetString(serializedObject, "muzzleNamePrefix", Get(row, "muzzleNamePrefix"));

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Equipment Parts")]
    public static void ImportEquipmentParts()
    {
        EnsureFolder(EquipmentPartOutputPath);
        foreach (Dictionary<string, string> row in ReadCsv(EquipmentPartCsvPath))
        {
            string id = GetRequired(row, "id", EquipmentPartCsvPath);
            EquipmentPartConfig config = LoadOrCreate<EquipmentPartConfig>(
                EquipmentPartOutputPath,
                id,
                "EquipmentPart");
            SerializedObject serializedObject = new SerializedObject(config);

            SetString(serializedObject, "id", id);
            SetString(serializedObject, "displayName", Get(row, "displayName"));
            SetObject(serializedObject, "icon", GetAsset<Sprite>(row, "icon"));
            SetEnum(serializedObject, "slot", row, "slot");
            SetFloat(serializedObject, "commonMainValue", row, "commonMainValue");
            SetFloat(serializedObject, "rareMainValue", row, "rareMainValue");
            SetFloat(serializedObject, "epicMainValue", row, "epicMainValue");
            SetFloat(serializedObject, "legendaryMainValue", row, "legendaryMainValue");
            SetInt(serializedObject, "commonSalePrice", row, "commonSalePrice");
            SetInt(serializedObject, "rareSalePrice", row, "rareSalePrice");
            SetInt(serializedObject, "epicSalePrice", row, "epicSalePrice");
            SetInt(serializedObject, "legendarySalePrice", row, "legendarySalePrice");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }
    }

    [MenuItem("Tools/Balance/CSV to SO/Gacha Weapons")]
    public static void ImportGachaWeapons()
    {
        ImportGachaPool(GachaCategory.Weapon, GachaWeaponCsvPath);
    }

    [MenuItem("Tools/Balance/CSV to SO/Gacha Skills")]
    public static void ImportGachaSkills()
    {
        ImportGachaPool(GachaCategory.Skill, GachaSkillCsvPath);
    }

    [MenuItem("Tools/Balance/SO to CSV/Units")]
    public static void ExportUnits()
    {
        string[] headers =
        {
            "id", "displayName", "unitPrefab", "maxHealth", "critChance", "critMultiplier", "attackRange",
            "attackDamage", "attackInterval", "moveSpeed", "rotationSpeed", "fireAngleTolerance",
            "repositionDistance", "repositionCooldown"
        };

        List<string[]> rows = new List<string[]>();
        foreach (PlayerUnitConfig config in LoadAllAssets<PlayerUnitConfig>(UnitOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.UnitPrefab),
                FormatFloat(config.MaxHealth),
                FormatFloat(config.CritChance),
                FormatFloat(config.CritMultiplier),
                FormatFloat(config.AttackRange),
                FormatFloat(config.AttackDamage),
                FormatFloat(config.AttackInterval),
                FormatFloat(config.MoveSpeed),
                FormatFloat(config.RotationSpeed),
                FormatFloat(config.FireAngleTolerance),
                FormatFloat(config.RepositionDistance),
                FormatFloat(config.RepositionCooldown)
            });
        }

        WriteCsv(UnitCsvPath, headers, rows);
        Debug.Log($"유닛 SO CSV 내보내기 완료: {UnitCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Weapons")]
    public static void ExportWeapons()
    {
        string[] headers =
        {
            "id", "displayName", "icon", "attackDamage", "attackType", "areaRadius", "areaDamageMultiplier",
            "maxAreaTargets", "maxPierceTargets", "maxLevel", "damagePercentPerLevel", "maxLevelDuplicateCoreCrystalReward",
            "speed", "lifetime", "collisionRadius", "knockbackForce",
            "multiMuzzleFireMode", "maxBurstMuzzleCount", "muzzleNamePrefix", "fireFlashEffect",
            "projectileEffect", "hitEffect", "effectCleanupDelay", "rarity"
        };

        List<string[]> rows = new List<string[]>();
        foreach (ProjectileConfig config in LoadAllAssets<ProjectileConfig>(WeaponOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.Icon),
                FormatFloat(config.AttackDamage),
                config.AttackType.ToString(),
                FormatFloat(config.AreaRadius),
                FormatFloat(config.AreaDamageMultiplier),
                config.MaxAreaTargets.ToString(CultureInfo.InvariantCulture),
                config.MaxPierceTargets.ToString(CultureInfo.InvariantCulture),
                config.MaxLevel.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.DamagePercentPerLevel),
                config.MaxLevelDuplicateCoreCrystalReward.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.Speed),
                FormatFloat(config.Lifetime),
                FormatFloat(config.CollisionRadius),
                FormatFloat(config.KnockbackForce),
                config.MultiMuzzleFireMode.ToString(),
                config.MaxBurstMuzzleCount.ToString(CultureInfo.InvariantCulture),
                config.MuzzleNamePrefix,
                GetAssetPath(config.FireFlashEffectPrefab),
                GetAssetPath(config.ProjectileEffectPrefab),
                GetAssetPath(config.HitEffectPrefab),
                FormatFloat(config.EffectCleanupDelay),
                config.Rarity.ToString()
            });
        }

        WriteCsv(WeaponCsvPath, headers, rows);
        Debug.Log($"무기 SO CSV 내보내기 완료: {WeaponCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Skills")]
    public static void ExportSkills()
    {
        string[] headers =
        {
            "id", "displayName", "icon", "skillType", "cooldown", "castRange", "minimumEnemyCount",
            "attackPowerMultiplier", "flatDamage", "canCritical", "effectRadius", "maxTargets", "knockbackForce",
            "maxLevel", "damagePercentPerLevel", "cooldownReductionPercentPerLevel", "maxCooldownReductionPercent",
            "countIncreaseLevelInterval", "bombCountIncreasePerInterval", "turretCountIncreasePerInterval",
            "maxLevelDuplicateCoreCrystalReward",
            "impactDelay", "warningEffect", "impactEffect", "effectCleanupDelay",
            "airplanePrefab", "airplaneSpeed", "airplaneSpawnOffset", "airplaneHeight",
            "bombProjectilePrefab", "bombEffectScale", "bombCount", "bombInterval",
            "screenShakeDuration", "screenShakeStrength", "screenShakeFrequency",
            "turretPrefab", "turretProjectileConfig", "turretDuration", "turretAttackInterval",
            "turretAttackRange", "turretRotationSpeed", "turretPlacementDistance", "turretFirePointName",
            "missileTurretPrefab", "missileProjectilePrefab", "missileTurretDuration",
            "missileTurretAttackInterval", "missileTurretAttackRange", "missileTurretRotationSpeed",
            "missileTurretPlacementDistance", "missileExplosionRadius", "missileProjectileSpeed",
            "missileTurretFirePointName",
            "stealthBomberPrefab", "stealthBomberSpeed", "stealthBomberTravelDistance", "stealthBomberHeight",
            "stealthBomberRows", "stealthBomberColumns", "stealthBomberSpacing",
            "stealthBomberBombInterval", "stealthBomberExplosionRadius",
            "attackHelicopterPrefab", "helicopterRocketPrefab", "attackHelicopterDuration",
            "attackHelicopterAttackInterval", "attackHelicopterAttackRange", "attackHelicopterMoveSpeed",
            "attackHelicopterFollowOffset", "helicopterOpeningRocketCount", "helicopterRocketRadius",
            "helicopterRocketSpeed", "rarity"
        };

        List<string[]> rows = new List<string[]>();
        foreach (PlayerSkillConfig config in LoadAllAssets<PlayerSkillConfig>(SkillOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.Icon),
                config.SkillType.ToString(),
                FormatFloat(config.Cooldown),
                FormatFloat(config.CastRange),
                config.MinimumEnemyCount.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.AttackPowerMultiplier),
                FormatFloat(config.FlatDamage),
                config.CanCritical.ToString(),
                FormatFloat(config.EffectRadius),
                config.MaxTargets.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.KnockbackForce),
                config.MaxLevel.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.DamagePercentPerLevel),
                FormatFloat(config.CooldownReductionPercentPerLevel),
                FormatFloat(config.MaxCooldownReductionPercent),
                config.CountIncreaseLevelInterval.ToString(CultureInfo.InvariantCulture),
                config.BombCountIncreasePerInterval.ToString(CultureInfo.InvariantCulture),
                config.TurretCountIncreasePerInterval.ToString(CultureInfo.InvariantCulture),
                config.MaxLevelDuplicateCoreCrystalReward.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.ImpactDelay),
                GetAssetPath(config.WarningEffectPrefab),
                GetAssetPath(config.ImpactEffectPrefab),
                FormatFloat(config.EffectCleanupDelay),
                GetAssetPath(config.AirplanePrefab),
                FormatFloat(config.AirplaneSpeed),
                FormatFloat(config.AirplaneSpawnOffset),
                FormatFloat(config.AirplaneHeight),
                GetAssetPath(config.BombProjectilePrefab),
                FormatFloat(config.BombEffectScale),
                config.BombCount.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.BombInterval),
                FormatFloat(config.ScreenShakeDuration),
                FormatFloat(config.ScreenShakeStrength),
                FormatFloat(config.ScreenShakeFrequency),
                GetAssetPath(config.TurretPrefab),
                GetAssetPath(config.TurretProjectileConfig),
                FormatFloat(config.TurretDuration),
                FormatFloat(config.TurretAttackInterval),
                FormatFloat(config.TurretAttackRange),
                FormatFloat(config.TurretRotationSpeed),
                FormatFloat(config.TurretPlacementDistance),
                config.TurretFirePointName,
                GetAssetPath(config.MissileTurretPrefab),
                GetAssetPath(config.MissileProjectilePrefab),
                FormatFloat(config.MissileTurretDuration),
                FormatFloat(config.MissileTurretAttackInterval),
                FormatFloat(config.MissileTurretAttackRange),
                FormatFloat(config.MissileTurretRotationSpeed),
                FormatFloat(config.MissileTurretPlacementDistance),
                FormatFloat(config.MissileExplosionRadius),
                FormatFloat(config.MissileProjectileSpeed),
                config.MissileTurretFirePointName,
                GetAssetPath(config.StealthBomberPrefab),
                FormatFloat(config.StealthBomberSpeed),
                FormatFloat(config.StealthBomberTravelDistance),
                FormatFloat(config.StealthBomberHeight),
                config.StealthBomberRows.ToString(CultureInfo.InvariantCulture),
                config.StealthBomberColumns.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.StealthBomberSpacing),
                FormatFloat(config.StealthBomberBombInterval),
                FormatFloat(config.StealthBomberExplosionRadius),
                GetAssetPath(config.AttackHelicopterPrefab),
                GetAssetPath(config.HelicopterRocketPrefab),
                FormatFloat(config.AttackHelicopterDuration),
                FormatFloat(config.AttackHelicopterAttackInterval),
                FormatFloat(config.AttackHelicopterAttackRange),
                FormatFloat(config.AttackHelicopterMoveSpeed),
                FormatFloat(config.AttackHelicopterFollowOffset),
                config.HelicopterOpeningRocketCount.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.HelicopterRocketRadius),
                FormatFloat(config.HelicopterRocketSpeed),
                config.Rarity.ToString()
            });
        }

        WriteCsv(SkillCsvPath, headers, rows);
        Debug.Log($"스킬 SO CSV 내보내기 완료: {SkillCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Enemies")]
    public static void ExportEnemies()
    {
        string[] headers =
        {
            "id", "displayName", "enemyPrefab", "maxHealth", "moveSpeed", "stopDistance", "contactDamage",
            "contactInterval", "experienceReward", "creditReward", "coreCrystalReward", "partDropChance",
            "deathExplosionEffectPrefab", "deathExplosionCleanupDelay", "deathExplosionOffsetX",
            "deathExplosionOffsetY", "deathExplosionOffsetZ", "deathExplosionScale"
        };

        List<string[]> rows = new List<string[]>();
        foreach (EnemyConfig config in LoadAllAssets<EnemyConfig>(EnemyOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.EnemyPrefab),
                FormatFloat(config.MaxHealth),
                FormatFloat(config.MoveSpeed),
                FormatFloat(config.StopDistance),
                FormatFloat(config.ContactDamage),
                FormatFloat(config.ContactInterval),
                FormatFloat(config.ExperienceReward),
                config.CreditReward.ToString(CultureInfo.InvariantCulture),
                config.CoreCrystalReward.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.PartDropChance),
                GetAssetPath(config.DeathExplosionEffectPrefab),
                FormatFloat(config.DeathExplosionCleanupDelay),
                FormatFloat(config.DeathExplosionOffset.x),
                FormatFloat(config.DeathExplosionOffset.y),
                FormatFloat(config.DeathExplosionOffset.z),
                FormatFloat(config.DeathExplosionScale)
            });
        }

        WriteCsv(EnemyCsvPath, headers, rows);
        Debug.Log($"적 SO CSV 내보내기 완료: {EnemyCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Drones")]
    public static void ExportDrones()
    {
        string[] headers =
        {
            "id", "displayName", "dronePrefab", "projectileConfig", "attackDamage", "attackRange", "attackInterval",
            "fireAngleTolerance", "rotationSpeed", "targetMask", "projectileSpeed", "projectileLifetime",
            "droneCount", "followRadius", "followSpeed", "startAngle", "angleStep", "muzzleNamePrefix"
        };

        List<string[]> rows = new List<string[]>();
        foreach (DroneConfig config in LoadAllAssets<DroneConfig>(DroneOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.DronePrefab),
                GetAssetPath(config.ProjectileConfig),
                FormatFloat(config.AttackDamage),
                FormatFloat(config.AttackRange),
                FormatFloat(config.AttackInterval),
                FormatFloat(config.FireAngleTolerance),
                FormatFloat(config.RotationSpeed),
                config.TargetMask.value.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.ProjectileSpeed),
                FormatFloat(config.ProjectileLifetime),
                config.DroneCount.ToString(CultureInfo.InvariantCulture),
                FormatFloat(config.FollowRadius),
                FormatFloat(config.FollowSpeed),
                FormatFloat(config.StartAngle),
                FormatFloat(config.AngleStep),
                config.MuzzleNamePrefix
            });
        }

        WriteCsv(DroneCsvPath, headers, rows);
        Debug.Log($"드론 SO CSV 내보내기 완료: {DroneCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Equipment Parts")]
    public static void ExportEquipmentParts()
    {
        string[] headers =
        {
            "id", "displayName", "icon", "slot", "commonMainValue", "rareMainValue", "epicMainValue",
            "legendaryMainValue", "commonSalePrice", "rareSalePrice", "epicSalePrice", "legendarySalePrice"
        };

        List<string[]> rows = new List<string[]>();
        foreach (EquipmentPartConfig config in LoadAllAssets<EquipmentPartConfig>(EquipmentPartOutputPath))
        {
            rows.Add(new[]
            {
                config.Id,
                config.DisplayName,
                GetAssetPath(config.Icon),
                config.Slot.ToString(),
                FormatFloat(config.CommonMainValue),
                FormatFloat(config.RareMainValue),
                FormatFloat(config.EpicMainValue),
                FormatFloat(config.LegendaryMainValue),
                config.CommonSalePrice.ToString(CultureInfo.InvariantCulture),
                config.RareSalePrice.ToString(CultureInfo.InvariantCulture),
                config.EpicSalePrice.ToString(CultureInfo.InvariantCulture),
                config.LegendarySalePrice.ToString(CultureInfo.InvariantCulture)
            });
        }

        WriteCsv(EquipmentPartCsvPath, headers, rows);
        Debug.Log($"장비 파츠 SO CSV 내보내기 완료: {EquipmentPartCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Gacha Weapons")]
    public static void ExportGachaWeapons()
    {
        GachaPoolConfig pool = LoadGachaPool(GachaCategory.Weapon);
        if (pool == null)
        {
            Debug.LogWarning("무기 뽑기 풀 SO가 없어 CSV 내보내기를 취소합니다.");
            return;
        }

        List<string[]> rows = new List<string[]>();
        foreach (WeaponGachaFacility.WeaponGachaEntry entry in pool.WeaponEntries)
        {
            if (entry?.weaponConfig == null)
            {
                continue;
            }

            rows.Add(new[]
            {
                entry.weaponConfig.Id,
                FormatFloat(entry.weight),
                entry.enabled.ToString()
            });
        }

        WriteCsv(GachaWeaponCsvPath, new[] { "configId", "weight", "enabled" }, rows);
        Debug.Log($"무기 뽑기 SO CSV 내보내기 완료: {GachaWeaponCsvPath}");
    }

    [MenuItem("Tools/Balance/SO to CSV/Gacha Skills")]
    public static void ExportGachaSkills()
    {
        GachaPoolConfig pool = LoadGachaPool(GachaCategory.Skill);
        if (pool == null)
        {
            Debug.LogWarning("스킬 뽑기 풀 SO가 없어 CSV 내보내기를 취소합니다.");
            return;
        }

        List<string[]> rows = new List<string[]>();
        foreach (WeaponGachaFacility.SkillGachaEntry entry in pool.SkillEntries)
        {
            if (entry?.skillConfig == null)
            {
                continue;
            }

            rows.Add(new[]
            {
                entry.skillConfig.Id,
                FormatFloat(entry.weight),
                entry.enabled.ToString()
            });
        }

        WriteCsv(GachaSkillCsvPath, new[] { "configId", "weight", "enabled" }, rows);
        Debug.Log($"스킬 뽑기 SO CSV 내보내기 완료: {GachaSkillCsvPath}");
    }

    private static void ImportGachaPool(GachaCategory category, string csvPath)
    {
        EnsureFolder(GachaOutputPath);
        GachaPoolConfig pool = LoadOrCreate<GachaPoolConfig>(
            GachaOutputPath,
            category == GachaCategory.Weapon ? "Weapons" : "Skills",
            "GachaPool");
        SerializedObject serializedObject = new SerializedObject(pool);
        SerializedProperty categoryProperty = serializedObject.FindProperty("category");
        if (categoryProperty != null)
        {
            categoryProperty.enumValueIndex = (int)category;
        }

        SerializedProperty entriesProperty = serializedObject.FindProperty(
            category == GachaCategory.Weapon ? "weaponEntries" : "skillEntries");
        if (entriesProperty == null)
        {
            return;
        }

        entriesProperty.ClearArray();
        HashSet<string> importedIds = new HashSet<string>();
        foreach (Dictionary<string, string> row in ReadCsv(csvPath))
        {
            string configId = Get(row, "configId");
            if (string.IsNullOrWhiteSpace(configId))
            {
                Debug.LogWarning($"{csvPath}: configId가 비어 있어 행을 제외합니다.");
                continue;
            }

            if (importedIds.Contains(configId))
            {
                Debug.LogWarning($"{csvPath}: 중복 configId를 제외합니다. {configId}");
                continue;
            }

            if (!float.TryParse(
                    Get(row, "weight"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float weight)
                || weight <= 0f)
            {
                Debug.LogWarning($"{csvPath}: 가중치가 0 이하이거나 잘못되어 제외합니다. {configId}");
                continue;
            }

            UnityEngine.Object config = category == GachaCategory.Weapon
                ? FindWeaponById(configId)
                : FindSkillById(configId);
            if (config == null)
            {
                Debug.LogWarning($"{csvPath}: Config ID를 찾지 못해 제외합니다. {configId}");
                continue;
            }

            importedIds.Add(configId);
            int index = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(
                    category == GachaCategory.Weapon ? "weaponConfig" : "skillConfig")
                .objectReferenceValue = config;
            entry.FindPropertyRelative("weight").floatValue = weight;
            entry.FindPropertyRelative("enabled").boolValue = ParseBool(Get(row, "enabled"), true);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();
        Debug.Log($"뽑기 CSV SO 가져오기 완료: {csvPath}");
    }

    private static ProjectileConfig FindWeaponById(string configId)
    {
        foreach (ProjectileConfig config in LoadAllAssets<ProjectileConfig>(WeaponOutputPath))
        {
            if (config.Id == configId)
            {
                return config;
            }
        }

        return null;
    }

    private static PlayerSkillConfig FindSkillById(string configId)
    {
        foreach (PlayerSkillConfig config in LoadAllAssets<PlayerSkillConfig>(SkillOutputPath))
        {
            if (config.Id == configId)
            {
                return config;
            }
        }

        return null;
    }

    private static GachaPoolConfig LoadGachaPool(GachaCategory category)
    {
        string id = category == GachaCategory.Weapon ? "Weapons" : "Skills";
        return AssetDatabase.LoadAssetAtPath<GachaPoolConfig>(
            $"{GachaOutputPath}/GachaPool_{id}.asset");
    }

    private static T LoadOrCreate<T>(string outputPath, string id, string prefix) where T : ScriptableObject
    {
        string assetPath = $"{outputPath}/{prefix}_{SanitizeFileName(id)}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static void EnsureOutputFolders()
    {
        EnsureFolder("Assets/SO");
        EnsureFolder("Assets/SO/Balance");
        EnsureFolder(UnitOutputPath);
        EnsureFolder(WeaponOutputPath);
        EnsureFolder(SkillOutputPath);
        EnsureFolder(EnemyOutputPath);
        EnsureFolder(DroneOutputPath);
        EnsureFolder(EquipmentPartOutputPath);
        EnsureFolder(GachaOutputPath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folder);
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"CSV 파일을 찾을 수 없습니다: {path}");
            yield break;
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length <= 1)
        {
            yield break;
        }

        List<string> headers = ParseCsvLine(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            List<string> values = ParseCsvLine(lines[i]);
            Dictionary<string, string> row = new Dictionary<string, string>();
            for (int j = 0; j < headers.Count; j++)
            {
                string value = j < values.Count ? values[j] : string.Empty;
                row[headers[j]] = value;
            }

        yield return row;
        }
    }

    private static IEnumerable<T> LoadAllAssets<T>(string folderPath) where T : ScriptableObject
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning($"SO 폴더를 찾을 수 없습니다: {folderPath}");
            yield break;
        }

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
        List<T> assets = new List<T>();
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        assets.Sort((left, right) => string.Compare(GetSortId(left), GetSortId(right), StringComparison.Ordinal));
        foreach (T asset in assets)
        {
            yield return asset;
        }
    }

    private static void WriteCsv(string path, string[] headers, List<string[]> rows)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers));
        for (int i = 0; i < rows.Count; i++)
        {
            string[] row = rows[i];
            for (int j = 0; j < row.Length; j++)
            {
                if (j > 0)
                {
                    builder.Append(',');
                }

                builder.Append(EscapeCsv(row[j]));
            }

            builder.AppendLine();
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string GetAssetPath(UnityEngine.Object asset)
    {
        return asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
    }

    private static string GetSortId(ScriptableObject asset)
    {
        switch (asset)
        {
            case PlayerUnitConfig unit:
                return unit.Id;
            case ProjectileConfig weapon:
                return weapon.Id;
            case PlayerSkillConfig skill:
                return skill.Id;
            case EnemyConfig enemy:
                return enemy.Id;
            case DroneConfig drone:
                return drone.Id;
            case EquipmentPartConfig equipmentPart:
                return equipmentPart.Id;
            default:
                return asset.name;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static T GetAsset<T>(Dictionary<string, string> row, string key) where T : UnityEngine.Object
    {
        string path = Get(row, key);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning($"에셋을 찾을 수 없습니다: {key}={path}");
        }

        return asset;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string value) ? value : string.Empty;
    }

    private static string GetRequired(Dictionary<string, string> row, string key, string path)
    {
        string value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{path} CSV에 필수 컬럼 값이 없습니다: {key}");
        }

        return value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, Dictionary<string, string> row, string key)
    {
        string value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            Debug.LogWarning($"숫자 파싱 실패: {key}={value}");
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = parsed;
        }
    }

    private static void SetVector3(
        SerializedObject serializedObject,
        string propertyName,
        Dictionary<string, string> row,
        string xKey,
        string yKey,
        string zKey)
    {
        string xValue = Get(row, xKey);
        string yValue = Get(row, yKey);
        string zValue = Get(row, zKey);
        if (string.IsNullOrWhiteSpace(xValue)
            && string.IsNullOrWhiteSpace(yValue)
            && string.IsNullOrWhiteSpace(zValue))
        {
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        Vector3 current = property.vector3Value;
        property.vector3Value = new Vector3(
            ParseFloatOrCurrent(xValue, current.x, xKey),
            ParseFloatOrCurrent(yValue, current.y, yKey),
            ParseFloatOrCurrent(zValue, current.z, zKey));
    }

    private static float ParseFloatOrCurrent(string value, float currentValue, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return currentValue;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            return parsed;
        }

        Debug.LogWarning($"숫자 파싱 실패: {key}={value}");
        return currentValue;
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, Dictionary<string, string> row, string key)
    {
        string value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            Debug.LogWarning($"정수 파싱 실패: {key}={value}");
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = parsed;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, Dictionary<string, string> row, string key)
    {
        string value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        bool parsed = ParseBool(value, false);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = parsed;
        }
    }

    private static bool ParseBool(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (value == "1")
        {
            return true;
        }

        if (value == "0")
        {
            return false;
        }

        return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    private static void SetEnum(SerializedObject serializedObject, string propertyName, Dictionary<string, string> row, string key)
    {
        string value = Get(row, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        for (int i = 0; i < property.enumNames.Length; i++)
        {
            if (string.Equals(property.enumNames[i], value, StringComparison.OrdinalIgnoreCase))
            {
                property.enumValueIndex = i;
                return;
            }
        }

        Debug.LogWarning($"enum 파싱 실패: {key}={value}");
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c.ToString(), "_");
        }

        return value;
    }
}
#endif
