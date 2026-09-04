#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GuideMissionCsvImporter
{
    private const string CsvPath = "Assets/Resources/Guide/guide_missions.csv";
    private const string OutputPath = "Assets/Resources/Guide/GuideMissionConfig.asset";

    [MenuItem("Tools/Balance/CSV to SO/Guide Missions")]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"가이드 미션 CSV 파일을 찾을 수 없습니다: {CsvPath}");
            return;
        }

        GuideMissionConfig parsed = GuideMissionConfig.CreateFromCsv(File.ReadAllText(CsvPath));
        GuideMissionConfig asset = AssetDatabase.LoadAssetAtPath<GuideMissionConfig>(OutputPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GuideMissionConfig>();
            AssetDatabase.CreateAsset(asset, OutputPath);
        }

        asset.ReplaceSteps(new List<GuideMissionConfig.GuideStepData>(parsed.Steps));
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Object.DestroyImmediate(parsed);
        Debug.Log($"가이드 미션 CSV SO 가져오기 완료: {OutputPath}");
    }
}
#endif
