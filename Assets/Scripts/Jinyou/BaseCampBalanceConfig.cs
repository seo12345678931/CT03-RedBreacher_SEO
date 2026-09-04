using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Myth/Base Camp/Balance Config")]
public class BaseCampBalanceConfig : ScriptableObject
{
    private const string LevelCsvResourcePath = "BaseCamp/base_camp_levels";
    private const string UnlockCsvResourcePath = "BaseCamp/base_camp_unlocks";

    [Serializable]
    public class FacilityDefinition
    {
        public string facilityId;
        public string displayName;
        public Sprite icon;
        public List<Sprite> levelSprites = new List<Sprite>();
    }

    [Serializable]
    public class FacilityLevelData
    {
        public string facilityId;
        public int level = 1;
        public int upgradeCost;
        public float upgradeSeconds;
        public int requiredCommanderLevel = 1;
        public int requiredCommandCenterLevel = 1;
        public float creditsPerMinute;
        public int storageCapacity;
        public float offlineRewardHours;
        public float ticketOfflineLimitHours;
        public float ticketProductionDaySeconds;
        public int ticketsPerDay;
        public int ticketCapacity;
        public int weaponEnhanceLevelCap;
        public int droneEnhanceLevelCap;
        public float droneAttackDamageBonus;
        public float droneAttackRangeBonus;
        public float droneAttackIntervalReduction;
        public float droneFollowSpeedBonus;
    }

    [Serializable]
    public class FacilityUnlockData
    {
        public string facilityId;
        public string displayName;
        public int requiredCommandCenterLevel = 1;
    }

    [SerializeField] private List<FacilityDefinition> facilities = new List<FacilityDefinition>();
    [SerializeField] private List<FacilityLevelData> levels = new List<FacilityLevelData>();
    [SerializeField] private List<FacilityUnlockData> unlocks = new List<FacilityUnlockData>();

    private static BaseCampBalanceConfig current;

    public IReadOnlyList<FacilityDefinition> Facilities => facilities;
    public IReadOnlyList<FacilityLevelData> Levels => levels;
    public IReadOnlyList<FacilityUnlockData> Unlocks => unlocks;

    public static BaseCampBalanceConfig Current
    {
        get
        {
            if (current == null)
            {
                current = LoadFromResources();
            }

            return current;
        }
    }

    public FacilityLevelData GetLevel(string facilityId, int level)
    {
        return levels.Find(item => item != null
            && item.level == level
            && string.Equals(item.facilityId, facilityId, StringComparison.OrdinalIgnoreCase));
    }

    public int GetMaxLevel(string facilityId)
    {
        int result = 1;
        foreach (FacilityLevelData item in levels)
        {
            if (item != null && string.Equals(item.facilityId, facilityId, StringComparison.OrdinalIgnoreCase))
            {
                result = Mathf.Max(result, item.level);
            }
        }

        return result;
    }

    public bool ValidateFacility(string facilityId, out int maxLevel, out string error)
    {
        maxLevel = GetMaxLevel(facilityId);
        for (int currentLevel = 1; currentLevel <= maxLevel; currentLevel++)
        {
            FacilityLevelData balance = GetLevel(facilityId, currentLevel);
            if (balance == null)
            {
                error = $"{facilityId} 시설의 Lv.{currentLevel} CSV 행이 없습니다.";
                return false;
            }

            // CSV 단일 원본에서 필수 값이 빠지면 해당 시설 동작을 중단한다.
            if (balance.requiredCommanderLevel < 1 || balance.requiredCommandCenterLevel < 1)
            {
                error = $"{facilityId} 시설 Lv.{currentLevel}의 요구 레벨이 올바르지 않습니다.";
                return false;
            }

            if (currentLevel < maxLevel && (balance.upgradeCost < 0 || balance.upgradeSeconds < 0f))
            {
                error = $"{facilityId} 시설 Lv.{currentLevel}의 업그레이드 값이 올바르지 않습니다.";
                return false;
            }

            if (string.Equals(facilityId, "command_center", StringComparison.OrdinalIgnoreCase)
                && (balance.ticketProductionDaySeconds <= 0f
                    || balance.ticketsPerDay <= 0
                    || balance.ticketCapacity < 0))
            {
                error = $"사령부 Lv.{currentLevel}의 티켓 설정이 올바르지 않습니다.";
                return false;
            }

            if (string.Equals(facilityId, "energy_refinery", StringComparison.OrdinalIgnoreCase)
                && (balance.creditsPerMinute < 0f || balance.storageCapacity <= 0))
            {
                error = $"자원 정제소 Lv.{currentLevel}의 생산 설정이 올바르지 않습니다.";
                return false;
            }

            if (string.Equals(facilityId, "assembly_factory", StringComparison.OrdinalIgnoreCase)
                && (balance.weaponEnhanceLevelCap <= 0 || balance.droneEnhanceLevelCap <= 0))
            {
                error = $"조립 공장 Lv.{currentLevel}의 강화 상한이 올바르지 않습니다.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public FacilityDefinition GetFacility(string facilityId)
    {
        return facilities.Find(item => item != null
            && string.Equals(item.facilityId, facilityId, StringComparison.OrdinalIgnoreCase));
    }

    public void ReplaceBalanceData(
        List<FacilityLevelData> importedLevels,
        List<FacilityUnlockData> importedUnlocks)
    {
        levels = importedLevels ?? new List<FacilityLevelData>();
        unlocks = importedUnlocks ?? new List<FacilityUnlockData>();
    }

    public static BaseCampBalanceConfig CreateFromCsv(string levelCsv, string unlockCsv)
    {
        BaseCampBalanceConfig config = CreateInstance<BaseCampBalanceConfig>();
        config.levels = ParseLevels(levelCsv);
        config.unlocks = ParseUnlocks(unlockCsv);
        return config;
    }

    private static BaseCampBalanceConfig LoadFromResources()
    {
        TextAsset levelCsv = Resources.Load<TextAsset>(LevelCsvResourcePath);
        TextAsset unlockCsv = Resources.Load<TextAsset>(UnlockCsvResourcePath);
        if (levelCsv != null && unlockCsv != null)
        {
            // 런타임 밸런스의 단일 원본은 CSV로 둔다.
            return CreateFromCsv(levelCsv.text, unlockCsv.text);
        }

        BaseCampBalanceConfig asset = Resources.Load<BaseCampBalanceConfig>("BaseCamp/BaseCampBalanceConfig");
        if (asset != null)
        {
            return asset;
        }

        Debug.LogError("기지 밸런스 CSV를 찾을 수 없습니다. Assets/Resources/BaseCamp 경로를 확인하세요.");
        return null;
    }

    private static List<FacilityLevelData> ParseLevels(string csv)
    {
        List<FacilityLevelData> result = new List<FacilityLevelData>();
        foreach (Dictionary<string, string> row in ParseCsv(csv))
        {
            result.Add(new FacilityLevelData
            {
                facilityId = Get(row, "facilityId"),
                level = GetInt(row, "level", 1),
                upgradeCost = GetInt(row, "upgradeCost"),
                upgradeSeconds = GetFloat(row, "upgradeSeconds"),
                requiredCommanderLevel = GetInt(row, "requiredCommanderLevel", 1),
                requiredCommandCenterLevel = GetInt(row, "requiredCommandCenterLevel", 1),
                creditsPerMinute = GetFloat(row, "creditsPerMinute"),
                storageCapacity = GetInt(row, "storageCapacity"),
                offlineRewardHours = GetFloat(row, "offlineRewardHours"),
                ticketOfflineLimitHours = GetFloat(row, "ticketOfflineLimitHours"),
                ticketProductionDaySeconds = GetFloat(row, "ticketProductionDaySeconds"),
                ticketsPerDay = GetInt(row, "ticketsPerDay"),
                ticketCapacity = GetInt(row, "ticketCapacity"),
                weaponEnhanceLevelCap = GetInt(row, "weaponEnhanceLevelCap"),
                droneEnhanceLevelCap = GetInt(row, "droneEnhanceLevelCap"),
                droneAttackDamageBonus = GetFloat(row, "droneAttackDamageBonus"),
                droneAttackRangeBonus = GetFloat(row, "droneAttackRangeBonus"),
                droneAttackIntervalReduction = GetFloat(row, "droneAttackIntervalReduction"),
                droneFollowSpeedBonus = GetFloat(row, "droneFollowSpeedBonus")
            });
        }

        return result;
    }

    private static List<FacilityUnlockData> ParseUnlocks(string csv)
    {
        List<FacilityUnlockData> result = new List<FacilityUnlockData>();
        foreach (Dictionary<string, string> row in ParseCsv(csv))
        {
            result.Add(new FacilityUnlockData
            {
                facilityId = Get(row, "facilityId"),
                displayName = Get(row, "displayName"),
                requiredCommandCenterLevel = GetInt(row, "requiredCommandCenterLevel", 1)
            });
        }

        return result;
    }

    private static IEnumerable<Dictionary<string, string>> ParseCsv(string csv)
    {
        string[] lines = csv.Replace("\r", string.Empty).Split('\n');
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
            Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < headers.Count; j++)
            {
                row[headers[j]] = j < values.Count ? values[j] : string.Empty;
            }

            yield return row;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder currentValue = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(currentValue.ToString().Trim());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(character);
            }
        }

        values.Add(currentValue.ToString().Trim());
        return values;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string value) ? value : string.Empty;
    }

    private static int GetInt(Dictionary<string, string> row, string key, int defaultValue = 0)
    {
        return int.TryParse(Get(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : defaultValue;
    }

    private static float GetFloat(Dictionary<string, string> row, string key, float defaultValue = 0f)
    {
        return float.TryParse(Get(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : defaultValue;
    }
}
