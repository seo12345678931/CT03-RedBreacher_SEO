using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum GuideConditionType
{
    EnemyKill,
    PlayerLevel,
    StageClear,
    WeaponCollect,
    DroneCollect,
    CollectCredits,
    UpgradeFacility,
    EnhanceWeapon,
    EnhanceUnit,
    EnhanceDrone,
    UseBossTicket,
    DrawWeaponGacha,
    DrawSkillGacha,
    ClaimOfflineReward,
    BossDefeat
}

[CreateAssetMenu(menuName = "Myth/Guide/Guide Mission Config")]
public class GuideMissionConfig : ScriptableObject
{
    private const string CsvResourcePath = "Guide/guide_missions";
    private const string AssetResourcePath = "Guide/GuideMissionConfig";

    [Serializable]
    public class GuideStepData
    {
        public int order = 1;
        public string id;
        public GuideConditionType conditionType = GuideConditionType.EnemyKill;
        public string conditionTarget;
        public string title;
        [TextArea]
        public string description;
        public int targetAmount = 1;
        public CurrencyType rewardCurrency = CurrencyType.Credits;
        public int rewardAmount;
    }

    [SerializeField] private List<GuideStepData> steps = new List<GuideStepData>();

    private static GuideMissionConfig current;

    public IReadOnlyList<GuideStepData> Steps => steps;

    public static GuideMissionConfig Current
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

    // 절대값(레벨 도달 등) 조건은 누적이 아니라 현재값으로 평가한다.
    public static bool IsAbsoluteCondition(GuideConditionType conditionType)
    {
        return conditionType == GuideConditionType.PlayerLevel
            || conditionType == GuideConditionType.StageClear
            || conditionType == GuideConditionType.DroneCollect
            || conditionType == GuideConditionType.UpgradeFacility
            || conditionType == GuideConditionType.EnhanceUnit;
    }

    public void ReplaceSteps(List<GuideStepData> importedSteps)
    {
        steps = SortByOrder(importedSteps ?? new List<GuideStepData>());
    }

    public static GuideMissionConfig CreateFromCsv(string csv)
    {
        GuideMissionConfig config = CreateInstance<GuideMissionConfig>();
        config.steps = SortByOrder(ParseSteps(csv));
        return config;
    }

    private static GuideMissionConfig LoadFromResources()
    {
        TextAsset preferredCsv = Resources.Load<TextAsset>(CsvResourcePath);
        if (preferredCsv != null)
        {
            return CreateFromCsv(preferredCsv.text);
        }

        GuideMissionConfig asset = Resources.Load<GuideMissionConfig>(AssetResourcePath);
        if (asset != null)
        {
            return asset;
        }

        TextAsset csv = Resources.Load<TextAsset>(CsvResourcePath);
        if (csv == null)
        {
            Debug.LogError("가이드 미션 CSV를 찾을 수 없습니다. Assets/Resources/Guide 경로를 확인하세요.");
            return null;
        }

        // SO 에셋을 아직 임포트하지 않은 경우에도 동일한 CSV를 런타임에서 사용한다.
        return CreateFromCsv(csv.text);
    }

    private static List<GuideStepData> SortByOrder(List<GuideStepData> source)
    {
        source.Sort((a, b) =>
        {
            int left = a != null ? a.order : int.MaxValue;
            int right = b != null ? b.order : int.MaxValue;
            return left.CompareTo(right);
        });
        return source;
    }

    private static List<GuideStepData> ParseSteps(string csv)
    {
        List<GuideStepData> result = new List<GuideStepData>();
        foreach (Dictionary<string, string> row in ParseCsv(csv))
        {
            result.Add(new GuideStepData
            {
                order = GetInt(row, "order", result.Count + 1),
                id = Get(row, "id"),
                conditionType = GetEnum(row, "conditionType", GuideConditionType.EnemyKill),
                conditionTarget = Get(row, "conditionTarget"),
                title = Get(row, "title"),
                description = Get(row, "description"),
                targetAmount = Mathf.Max(1, GetInt(row, "targetAmount", 1)),
                rewardCurrency = GetEnum(row, "rewardCurrency", CurrencyType.Credits),
                rewardAmount = Mathf.Max(0, GetInt(row, "rewardAmount"))
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

    private static T GetEnum<T>(Dictionary<string, string> row, string key, T defaultValue) where T : struct
    {
        return Enum.TryParse(Get(row, key), true, out T value) ? value : defaultValue;
    }
}
