using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class JinyouDataValidator : EditorWindow
{
    private enum Severity { Info, Warning, Error }

    private sealed class Issue
    {
        public Severity severity;
        public string message;
        public UnityEngine.Object context;
    }

    private readonly List<Issue> issues = new List<Issue>();
    private Vector2 scrollPosition;
    private int inspectedAssetCount;

    [MenuItem("Tools/Jinyou/Data Validator")]
    private static void OpenWindow()
    {
        GetWindow<JinyouDataValidator>("Jinyou Data Validator").minSize = new Vector2(620f, 360f);
    }

    [MenuItem("Tools/Jinyou/Validate All Data %#v")]
    private static void ValidateFromMenu()
    {
        JinyouDataValidator window = GetWindow<JinyouDataValidator>("Jinyou Data Validator");
        window.RunValidation();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Jinyou Data Validator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "밸런스 ScriptableObject와 CSV의 ID, 참조, 수치 및 가챠 구성을 검사합니다.",
            MessageType.Info);

        if (GUILayout.Button("Validate All Data", GUILayout.Height(32f)))
        {
            RunValidation();
        }

        int errorCount = issues.Count(issue => issue.severity == Severity.Error);
        int warningCount = issues.Count(issue => issue.severity == Severity.Warning);
        EditorGUILayout.LabelField(
            $"Assets: {inspectedAssetCount}   Errors: {errorCount}   Warnings: {warningCount}");

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (Issue issue in issues)
        {
            EditorGUILayout.BeginHorizontal();
            MessageType messageType = issue.severity == Severity.Error
                ? MessageType.Error
                : issue.severity == Severity.Warning ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(issue.message, messageType);
            if (issue.context != null && GUILayout.Button("Select", GUILayout.Width(56f), GUILayout.Height(38f)))
            {
                Selection.activeObject = issue.context;
                EditorGUIUtility.PingObject(issue.context);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunValidation()
    {
        issues.Clear();
        inspectedAssetCount = 0;

        ValidateIdentityAssets<ProjectileConfig>("Weapon", asset => asset.Id, ValidateWeapon);
        ValidateIdentityAssets<PlayerSkillConfig>("Skill", asset => asset.Id, ValidateSkill);
        ValidateIdentityAssets<DroneConfig>("Drone", asset => asset.Id, ValidateDrone);
        ValidateIdentityAssets<PlayerUnitConfig>("Unit", asset => asset.Id, ValidateUnit);
        ValidateIdentityAssets<EquipmentPartConfig>("Equipment Part", asset => asset.Id, ValidateEquipmentPart);
        ValidateGachaPools();
        ValidateAchievements();
        ValidateGuideMissions();
        ValidateBaseCampBalance();
        ValidateCsvFiles();

        int errors = issues.Count(issue => issue.severity == Severity.Error);
        int warnings = issues.Count(issue => issue.severity == Severity.Warning);
        if (errors == 0 && warnings == 0)
        {
            Add(Severity.Info, $"검증 완료: {inspectedAssetCount}개 에셋에서 문제가 발견되지 않았습니다.");
            Debug.Log($"[Jinyou Validator] 검증 완료: {inspectedAssetCount}개 에셋, 문제 없음.");
        }
        else
        {
            Debug.LogWarning($"[Jinyou Validator] 검증 완료: 오류 {errors}개, 경고 {warnings}개.");
        }

        Repaint();
    }

    private void ValidateIdentityAssets<T>(
        string label,
        Func<T, string> getId,
        Action<T> validateAsset) where T : ScriptableObject
    {
        List<T> assets = LoadAssets<T>();
        inspectedAssetCount += assets.Count;
        Dictionary<string, T> ids = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (T asset in assets)
        {
            string id = getId(asset);
            if (string.IsNullOrWhiteSpace(id))
            {
                Add(Severity.Error, $"{label}: ID가 비어 있습니다.", asset);
            }
            else if (ids.TryGetValue(id, out T existing))
            {
                Add(Severity.Error,
                    $"{label}: 중복 ID '{id}' ({existing.name}, {asset.name})", asset);
            }
            else
            {
                ids.Add(id, asset);
            }

            validateAsset(asset);
        }
    }

    private void ValidateWeapon(ProjectileConfig asset)
    {
        if (string.IsNullOrWhiteSpace(asset.DisplayName)) Add(Severity.Warning, $"Weapon '{asset.Id}': 표시 이름이 비어 있습니다.", asset);
        if (asset.AttackDamage < 0f) Add(Severity.Error, $"Weapon '{asset.Id}': 공격력이 음수입니다.", asset);
        if (asset.Speed <= 0f) Add(Severity.Error, $"Weapon '{asset.Id}': 투사체 속도는 0보다 커야 합니다.", asset);
        if (asset.Lifetime <= 0f) Add(Severity.Error, $"Weapon '{asset.Id}': 투사체 수명은 0보다 커야 합니다.", asset);
        if (asset.MaxLevel < 1) Add(Severity.Error, $"Weapon '{asset.Id}': 최대 레벨이 올바르지 않습니다.", asset);
        if (asset.ProjectileEffectPrefab == null) Add(Severity.Warning, $"Weapon '{asset.Id}': 투사체 이펙트 프리팹이 없습니다.", asset);
    }

    private void ValidateSkill(PlayerSkillConfig asset)
    {
        if (string.IsNullOrWhiteSpace(asset.DisplayName)) Add(Severity.Warning, $"Skill '{asset.Id}': 표시 이름이 비어 있습니다.", asset);
        if (asset.Cooldown <= 0f) Add(Severity.Error, $"Skill '{asset.Id}': 쿨다운은 0보다 커야 합니다.", asset);
        if (asset.MaxLevel < 1) Add(Severity.Error, $"Skill '{asset.Id}': 최대 레벨이 올바르지 않습니다.", asset);
        if (asset.SkillType == PlayerSkillType.Bombardment && asset.BombProjectilePrefab == null)
            Add(Severity.Warning, $"Skill '{asset.Id}': 폭격 투사체 프리팹이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.AutoTurret && asset.TurretPrefab == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 터렛 프리팹이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.MissileTurret && asset.MissileTurretPrefab == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 미사일 터렛 프리팹이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.MissileTurret && asset.MissileProjectileConfig == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 미사일 투사체 설정이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.StealthBomber && asset.StealthBomberPrefab == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 스텔스 폭격기 프리팹이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.AttackHelicopter && asset.AttackHelicopterPrefab == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 공격헬기 프리팹이 없습니다.", asset);
        if (asset.SkillType == PlayerSkillType.AttackHelicopter && asset.HelicopterRocketPrefab == null)
            Add(Severity.Error, $"Skill '{asset.Id}': 헬기 로켓 투사체 프리팹이 없습니다.", asset);
    }

    private void ValidateDrone(DroneConfig asset)
    {
        if (string.IsNullOrWhiteSpace(asset.DisplayName)) Add(Severity.Warning, $"Drone '{asset.Id}': 표시 이름이 비어 있습니다.", asset);
        if (asset.DronePrefab == null) Add(Severity.Error, $"Drone '{asset.Id}': 드론 프리팹이 없습니다.", asset);
        if (asset.ProjectileConfig == null) Add(Severity.Warning, $"Drone '{asset.Id}': 투사체 설정이 없어 fallback 값을 사용합니다.", asset);
        if (asset.AttackInterval <= 0f) Add(Severity.Error, $"Drone '{asset.Id}': 공격 간격은 0보다 커야 합니다.", asset);
    }

    private void ValidateUnit(PlayerUnitConfig asset)
    {
        if (string.IsNullOrWhiteSpace(asset.DisplayName)) Add(Severity.Warning, $"Unit '{asset.Id}': 표시 이름이 비어 있습니다.", asset);
        if (asset.UnitPrefab == null) Add(Severity.Error, $"Unit '{asset.Id}': 유닛 프리팹이 없습니다.", asset);
        if (asset.MaxHealth <= 0f) Add(Severity.Error, $"Unit '{asset.Id}': 최대 체력은 0보다 커야 합니다.", asset);
        if (asset.AttackInterval <= 0f) Add(Severity.Error, $"Unit '{asset.Id}': 공격 간격은 0보다 커야 합니다.", asset);
    }

    private void ValidateEquipmentPart(EquipmentPartConfig asset)
    {
        if (string.IsNullOrWhiteSpace(asset.DisplayName)) Add(Severity.Warning, $"Equipment Part '{asset.Id}': 표시 이름이 비어 있습니다.", asset);
        if (asset.CommonMainValue < 0f || asset.RareMainValue < 0f || asset.EpicMainValue < 0f)
            Add(Severity.Error, $"Equipment Part '{asset.Id}': 주 능력치가 음수입니다.", asset);
        if (asset.CommonMainValue > asset.RareMainValue || asset.RareMainValue > asset.EpicMainValue)
            Add(Severity.Warning, $"Equipment Part '{asset.Id}': 등급이 높아지는데 주 능력치가 증가하지 않습니다.", asset);
        if (asset.CommonSalePrice < 0 || asset.RareSalePrice < 0 || asset.EpicSalePrice < 0)
            Add(Severity.Error, $"Equipment Part '{asset.Id}': 판매 가격이 음수입니다.", asset);
    }

    private void ValidateGachaPools()
    {
        foreach (GachaPoolConfig pool in LoadAssets<GachaPoolConfig>())
        {
            inspectedAssetCount++;
            HashSet<UnityEngine.Object> seen = new HashSet<UnityEngine.Object>();
            float totalWeight = 0f;
            if (pool.Category == GachaCategory.Weapon)
            {
                foreach (WeaponGachaFacility.WeaponGachaEntry entry in pool.WeaponEntries)
                {
                    if (entry == null || entry.weaponConfig == null) Add(Severity.Error, $"Gacha '{pool.name}': 무기 참조가 비어 있습니다.", pool);
                    else if (!seen.Add(entry.weaponConfig)) Add(Severity.Warning, $"Gacha '{pool.name}': '{entry.weaponConfig.Id}'가 중복 등록되었습니다.", pool);
                    if (entry != null && entry.enabled && entry.weight <= 0f) Add(Severity.Error, $"Gacha '{pool.name}': 활성 무기의 가중치는 0보다 커야 합니다.", pool);
                    if (entry != null && entry.enabled) totalWeight += Mathf.Max(0f, entry.weight);
                }
            }
            else
            {
                foreach (WeaponGachaFacility.SkillGachaEntry entry in pool.SkillEntries)
                {
                    if (entry == null || entry.skillConfig == null) Add(Severity.Error, $"Gacha '{pool.name}': 스킬 참조가 비어 있습니다.", pool);
                    else if (!seen.Add(entry.skillConfig)) Add(Severity.Warning, $"Gacha '{pool.name}': '{entry.skillConfig.Id}'가 중복 등록되었습니다.", pool);
                    if (entry != null && entry.enabled && entry.weight <= 0f) Add(Severity.Error, $"Gacha '{pool.name}': 활성 스킬의 가중치는 0보다 커야 합니다.", pool);
                    if (entry != null && entry.enabled) totalWeight += Mathf.Max(0f, entry.weight);
                }
            }

            if (totalWeight <= 0f) Add(Severity.Error, $"Gacha '{pool.name}': 추첨 가능한 항목이 없습니다.", pool);
        }
    }

    private void ValidateAchievements()
    {
        foreach (AchievementConfig config in LoadAssets<AchievementConfig>())
        {
            inspectedAssetCount++;
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AchievementConfig.AchievementDefinition entry in config.Achievements)
            {
                if (entry == null) { Add(Severity.Error, $"Achievement '{config.name}': 빈 항목이 있습니다.", config); continue; }
                if (string.IsNullOrWhiteSpace(entry.id)) Add(Severity.Error, $"Achievement '{config.name}': ID가 비어 있습니다.", config);
                else if (!ids.Add(entry.id)) Add(Severity.Error, $"Achievement '{config.name}': 중복 ID '{entry.id}'.", config);
                if (entry.targetAmount < 1 || entry.progressAmountPerEvent < 1) Add(Severity.Error, $"Achievement '{entry.id}': 진행 수치가 올바르지 않습니다.", config);
                if (entry.rewardAmount < 0) Add(Severity.Error, $"Achievement '{entry.id}': 보상이 음수입니다.", config);
            }
        }
    }

    private void ValidateGuideMissions()
    {
        foreach (GuideMissionConfig config in LoadAssets<GuideMissionConfig>())
        {
            inspectedAssetCount++;
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> orders = new HashSet<int>();
            foreach (GuideMissionConfig.GuideStepData step in config.Steps)
            {
                if (step == null) { Add(Severity.Error, $"Guide '{config.name}': 빈 단계가 있습니다.", config); continue; }
                if (string.IsNullOrWhiteSpace(step.id)) Add(Severity.Error, $"Guide '{config.name}': ID가 비어 있습니다.", config);
                else if (!ids.Add(step.id)) Add(Severity.Error, $"Guide '{config.name}': 중복 ID '{step.id}'.", config);
                if (step.order < 1 || !orders.Add(step.order)) Add(Severity.Error, $"Guide '{step.id}': 순서가 잘못되었거나 중복입니다 ({step.order}).", config);
                if (step.targetAmount < 1 || step.rewardAmount < 0) Add(Severity.Error, $"Guide '{step.id}': 목표 또는 보상 수치가 올바르지 않습니다.", config);
            }
        }
    }

    private void ValidateBaseCampBalance()
    {
        foreach (BaseCampBalanceConfig config in LoadAssets<BaseCampBalanceConfig>())
        {
            inspectedAssetCount++;
            HashSet<string> facilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BaseCampBalanceConfig.FacilityDefinition facility in config.Facilities)
            {
                if (facility == null || string.IsNullOrWhiteSpace(facility.facilityId)) Add(Severity.Error, $"BaseCamp '{config.name}': 시설 ID가 비어 있습니다.", config);
                else if (!facilityIds.Add(facility.facilityId)) Add(Severity.Error, $"BaseCamp '{config.name}': 중복 시설 ID '{facility.facilityId}'.", config);
            }

            HashSet<string> levelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BaseCampBalanceConfig.FacilityLevelData level in config.Levels)
            {
                if (level == null) { Add(Severity.Error, $"BaseCamp '{config.name}': 빈 레벨 항목이 있습니다.", config); continue; }
                string key = $"{level.facilityId}:{level.level}";
                if (!levelKeys.Add(key)) Add(Severity.Error, $"BaseCamp '{config.name}': 중복 레벨 '{key}'.", config);
                if (!facilityIds.Contains(level.facilityId)) Add(Severity.Error, $"BaseCamp '{config.name}': 정의되지 않은 시설 레벨 '{key}'.", config);
                if (level.level < 1 || level.upgradeCost < 0 || level.upgradeSeconds < 0f) Add(Severity.Error, $"BaseCamp '{key}': 레벨/비용/시간 값이 올바르지 않습니다.", config);
            }

            foreach (string facilityId in facilityIds)
            {
                if (!config.ValidateFacility(facilityId, out _, out string error)) Add(Severity.Error, error, config);
            }
        }
    }

    private void ValidateCsvFiles()
    {
        ValidateCsv("Assets/Resources/Achievement/achievements.csv", "id", "progressType", "targetAmount", "rewardAmount");
        ValidateCsv("Assets/Resources/Guide/guide_missions.csv", "order", "id", "conditionType", "targetAmount", "rewardAmount");
        ValidateCsv("Assets/Resources/BaseCamp/base_camp_levels.csv", "facilityId", "level", "upgradeCost", "upgradeSeconds");
        ValidateCsv("Assets/Resources/BaseCamp/base_camp_unlocks.csv", "facilityId", "requiredCommandCenterLevel");
    }

    private void ValidateCsv(string assetPath, params string[] requiredHeaders)
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (asset == null)
        {
            Add(Severity.Error, $"CSV 파일을 찾을 수 없습니다: {assetPath}");
            return;
        }

        inspectedAssetCount++;
        string firstLine = asset.text.Replace("\r", string.Empty).Split('\n').FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            Add(Severity.Error, $"CSV가 비어 있습니다: {assetPath}", asset);
            return;
        }

        HashSet<string> headers = new HashSet<string>(
            firstLine.Split(',').Select(value => value.Trim().Trim('"')),
            StringComparer.OrdinalIgnoreCase);
        foreach (string header in requiredHeaders)
        {
            if (!headers.Contains(header)) Add(Severity.Error, $"CSV '{assetPath}': 필수 헤더 '{header}'가 없습니다.", asset);
        }
    }

    private static List<T> LoadAssets<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToList();
    }

    private void Add(Severity severity, string message, UnityEngine.Object context = null)
    {
        issues.Add(new Issue { severity = severity, message = message, context = context });
        if (severity == Severity.Error) Debug.LogError($"[Jinyou Validator] {message}", context);
        else if (severity == Severity.Warning) Debug.LogWarning($"[Jinyou Validator] {message}", context);
    }
}
