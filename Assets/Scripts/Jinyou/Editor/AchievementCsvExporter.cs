#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AchievementCsvExporter
{
    private const string SoPath = "Assets/Resources/Achievement/AchievementConfig.asset";
    private const string CsvPath = "Assets/Resources/Achievement/achievements.csv";

    // AchievementConfig.CreateFromCsv가 읽는 컬럼 순서와 동일하게 유지한다.
    private static readonly string[] Headers =
    {
        "id",
        "progressType",
        "title",
        "description",
        "targetAmount",
        "nextTargetAmounts",
        "repeatRequirementAmount",
        "progressAmountPerEvent",
        "rewardCurrency",
        "rewardAmount"
    };

    [MenuItem("Tools/Balance/SO to CSV/Achievements")]
    public static void Export()
    {
        AchievementConfig asset = AssetDatabase.LoadAssetAtPath<AchievementConfig>(SoPath);
        if (asset == null)
        {
            Debug.LogError($"Achievement SO를 찾을 수 없습니다: {SoPath}");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(string.Join(",", Headers));
        builder.Append('\n');

        IReadOnlyList<AchievementConfig.AchievementDefinition> achievements = asset.Achievements;
        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementConfig.AchievementDefinition achievement = achievements[i];
            if (achievement == null)
            {
                continue;
            }

            List<string> fields = new List<string>
            {
                achievement.id,
                achievement.progressType.ToString(),
                achievement.title,
                achievement.description,
                Mathf.Max(1, achievement.targetAmount).ToString(CultureInfo.InvariantCulture),
                FormatIntList(achievement.nextTargetAmounts),
                Mathf.Max(1, achievement.repeatRequirementAmount).ToString(CultureInfo.InvariantCulture),
                Mathf.Max(1, achievement.progressAmountPerEvent).ToString(CultureInfo.InvariantCulture),
                achievement.rewardCurrency.ToString(),
                Mathf.Max(0, achievement.rewardAmount).ToString(CultureInfo.InvariantCulture)
            };

            for (int j = 0; j < fields.Count; j++)
            {
                if (j > 0)
                {
                    builder.Append(',');
                }

                builder.Append(Escape(fields[j]));
            }

            builder.Append('\n');
        }

        File.WriteAllText(CsvPath, builder.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"Achievement SO CSV 내보내기 완료: {CsvPath} ({achievements.Count}개)");
    }

    private static string FormatIntList(IReadOnlyList<int> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;

        bool needsQuote = value.IndexOf(',') >= 0
            || value.IndexOf('"') >= 0
            || value.IndexOf('\n') >= 0
            || value.IndexOf('\r') >= 0;

        if (!needsQuote)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
#endif
