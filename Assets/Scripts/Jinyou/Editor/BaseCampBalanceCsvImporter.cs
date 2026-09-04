#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BaseCampBalanceCsvImporter
{
    private const string LevelCsvPath = "Assets/Resources/BaseCamp/base_camp_levels.csv";
    private const string UnlockCsvPath = "Assets/Resources/BaseCamp/base_camp_unlocks.csv";
    private const string OutputPath = "Assets/Resources/BaseCamp/BaseCampBalanceConfig.asset";

    [MenuItem("Tools/Balance/CSV to SO/Base Camp")]
    public static void Import()
    {
        if (!File.Exists(LevelCsvPath) || !File.Exists(UnlockCsvPath))
        {
            Debug.LogError("기지 밸런스 CSV 파일을 찾을 수 없습니다.");
            return;
        }

        BaseCampBalanceConfig parsed = BaseCampBalanceConfig.CreateFromCsv(
            File.ReadAllText(LevelCsvPath),
            File.ReadAllText(UnlockCsvPath));
        BaseCampBalanceConfig asset = AssetDatabase.LoadAssetAtPath<BaseCampBalanceConfig>(OutputPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BaseCampBalanceConfig>();
            AssetDatabase.CreateAsset(asset, OutputPath);
        }

        // 아이콘과 레벨 스프라이트는 기존 SO 값을 유지하고 CSV 수치만 갱신한다.
        asset.ReplaceBalanceData(
            new System.Collections.Generic.List<BaseCampBalanceConfig.FacilityLevelData>(parsed.Levels),
            new System.Collections.Generic.List<BaseCampBalanceConfig.FacilityUnlockData>(parsed.Unlocks));
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Object.DestroyImmediate(parsed);
        Debug.Log($"기지 밸런스 CSV SO 가져오기 완료: {OutputPath}");
    }
}
#endif
