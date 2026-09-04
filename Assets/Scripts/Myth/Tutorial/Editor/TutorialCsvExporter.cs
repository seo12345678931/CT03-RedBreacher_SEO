#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TutorialCsvExporter
{
    private const string SoPath = "Assets/Resources/Tutorial/TutorialConfig.asset";
    private const string CsvPath = "Assets/Resources/Tutorial/tutorial_steps.csv";

    // TutorialCsvImporter가 파싱하는 것과 동일한 컬럼 순서/이름.
    private static readonly string[] Headers =
    {
        "order",
        "id",
        "bodyText",
        "highlightTargetName",
        "advanceType",
        "eventType",
        "targetAmount",
        "targetId"
    };

    [MenuItem("Tools/Balance/SO to CSV/Tutorial Steps")]
    public static void Export()
    {
        TutorialConfig asset = AssetDatabase.LoadAssetAtPath<TutorialConfig>(SoPath);
        if (asset == null)
        {
            Debug.LogError($"튜토리얼 SO를 찾을 수 없습니다: {SoPath}");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(string.Join(",", Headers));
        builder.Append('\n');

        IReadOnlyList<TutorialConfig.TutorialStep> steps = asset.Steps;
        for (int i = 0; i < steps.Count; i++)
        {
            TutorialConfig.TutorialStep step = steps[i];
            if (step == null)
            {
                continue;
            }

            List<string> fields = new List<string>
            {
                step.order.ToString(CultureInfo.InvariantCulture),
                step.id,
                step.bodyText,
                step.highlightTargetName,
                step.advanceType.ToString(),
                step.eventType.ToString(),
                step.targetAmount.ToString(CultureInfo.InvariantCulture),
                step.targetId
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
        Debug.Log($"튜토리얼 SO CSV 내보내기 완료: {CsvPath} (스텝 {steps.Count}개)");
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;

        // 콤마/따옴표/줄바꿈이 있으면 따옴표로 감싸고 내부 따옴표는 "" 로 이스케이프.
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
