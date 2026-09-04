using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Myth/Achievement/Achievement Config")]
public class AchievementConfig : ScriptableObject
{
    private const string CsvResourcePath = "Achievement/achievements";
    private const string AssetResourcePath = "Achievement/AchievementConfig";

    [Serializable]
    public class AchievementDefinition
    {
        public string id;
        public AchievementProgressType progressType = AchievementProgressType.EnemyKill;
        public string title;
        [TextArea]
        public string description;
        public Sprite iconSprite;
        public int targetAmount = 1;
        public List<int> nextTargetAmounts = new List<int>();
        public int repeatRequirementAmount = 1;
        public int progressAmountPerEvent = 1;
        public CurrencyType rewardCurrency = CurrencyType.CoreCrystals;
        public int rewardAmount;
    }

    [SerializeField] private List<AchievementDefinition> achievements = new List<AchievementDefinition>();

    private static AchievementConfig current;

    public IReadOnlyList<AchievementDefinition> Achievements => achievements;

    public static AchievementConfig Current
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

    public void ReplaceAchievements(List<AchievementDefinition> importedAchievements)
    {
        achievements = importedAchievements ?? new List<AchievementDefinition>();
        Validate();
    }

    public static AchievementConfig CreateFromCsv(string csv)
    {
        AchievementConfig config = CreateInstance<AchievementConfig>();
        config.achievements = ParseAchievements(csv);
        config.Validate();
        return config;
    }

    public void Validate()
    {
        achievements ??= new List<AchievementDefinition>();
        foreach (AchievementDefinition achievement in achievements)
        {
            if (achievement == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(achievement.id))
            {
                achievement.id = achievement.progressType.ToString();
            }

            achievement.targetAmount = Mathf.Max(1, achievement.targetAmount);
            achievement.nextTargetAmounts ??= new List<int>();
            int previousRequirement = achievement.targetAmount;
            for (int i = 0; i < achievement.nextTargetAmounts.Count; i++)
            {
                achievement.nextTargetAmounts[i] = Mathf.Max(previousRequirement + 1, achievement.nextTargetAmounts[i]);
                previousRequirement = achievement.nextTargetAmounts[i];
            }

            achievement.repeatRequirementAmount = Mathf.Max(0, achievement.repeatRequirementAmount);
            achievement.progressAmountPerEvent = Mathf.Max(1, achievement.progressAmountPerEvent);
            achievement.rewardAmount = Mathf.Max(0, achievement.rewardAmount);
        }
    }

    private static AchievementConfig LoadFromResources()
    {
        AchievementConfig asset = Resources.Load<AchievementConfig>(AssetResourcePath);
        if (asset != null)
        {
            return asset;
        }

        TextAsset csv = Resources.Load<TextAsset>(CsvResourcePath);
        if (csv == null)
        {
            Debug.LogError("업적 CSV를 찾을 수 없습니다. Assets/Resources/Achievement 경로를 확인하세요.");
            return null;
        }

        // SO 에셋이 아직 없으면 동일 CSV를 런타임에서 읽어 업적 정의로 사용한다.
        return CreateFromCsv(csv.text);
    }

    private static List<AchievementDefinition> ParseAchievements(string csv)
    {
        List<AchievementDefinition> result = new List<AchievementDefinition>();
        foreach (Dictionary<string, string> row in ParseCsv(csv))
        {
            result.Add(new AchievementDefinition
            {
                id = Get(row, "id"),
                progressType = GetEnum(row, "progressType", AchievementProgressType.EnemyKill),
                title = Get(row, "title"),
                description = Get(row, "description"),
                targetAmount = Mathf.Max(1, GetInt(row, "targetAmount", 1)),
                nextTargetAmounts = ParseIntList(Get(row, "nextTargetAmounts")),
                repeatRequirementAmount = Mathf.Max(0, GetInt(row, "repeatRequirementAmount", 1)),
                progressAmountPerEvent = Mathf.Max(1, GetInt(row, "progressAmountPerEvent", 1)),
                rewardCurrency = GetEnum(row, "rewardCurrency", CurrencyType.CoreCrystals),
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

    private static List<int> ParseIntList(string value)
    {
        List<int> result = new List<int>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        string[] parts = value.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
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

    private void OnValidate()
    {
        Validate();
    }
}
