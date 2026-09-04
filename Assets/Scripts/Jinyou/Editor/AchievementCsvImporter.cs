#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AchievementCsvImporter
{
    private const string CsvPath = "Assets/Resources/Achievement/achievements.csv";
    private const string OutputPath = "Assets/Resources/Achievement/AchievementConfig.asset";

    [MenuItem("Tools/Balance/CSV to SO/Achievements")]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"업적 CSV 파일을 찾을 수 없습니다: {CsvPath}");
            return;
        }

        AchievementConfig parsed = AchievementConfig.CreateFromCsv(File.ReadAllText(CsvPath));
        AchievementConfig asset = AssetDatabase.LoadAssetAtPath<AchievementConfig>(OutputPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<AchievementConfig>();
            AssetDatabase.CreateAsset(asset, OutputPath);
        }

        Dictionary<string, Sprite> iconsById = BuildIconMap(asset);
        List<AchievementConfig.AchievementDefinition> imported =
            new List<AchievementConfig.AchievementDefinition>(parsed.Achievements);
        for (int i = 0; i < imported.Count; i++)
        {
            AchievementConfig.AchievementDefinition definition = imported[i];
            if (definition != null
                && !string.IsNullOrWhiteSpace(definition.id)
                && iconsById.TryGetValue(definition.id, out Sprite icon))
            {
                // CSV 임포트 시 수동으로 연결해둔 업적 아이콘은 유지한다.
                definition.iconSprite = icon;
            }
        }

        asset.ReplaceAchievements(imported);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Object.DestroyImmediate(parsed);
        Debug.Log($"업적 CSV SO 가져오기 완료: {OutputPath}");
    }

    private static Dictionary<string, Sprite> BuildIconMap(AchievementConfig asset)
    {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
        if (asset == null)
        {
            return result;
        }

        foreach (AchievementConfig.AchievementDefinition definition in asset.Achievements)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.id) || definition.iconSprite == null)
            {
                continue;
            }

            result[definition.id] = definition.iconSprite;
        }

        return result;
    }
}
#endif
