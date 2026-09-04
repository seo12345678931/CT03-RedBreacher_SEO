#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TutorialCsvImporter
{
    private const string CsvPath = "Assets/Resources/Tutorial/tutorial_steps.csv";
    private const string OutputPath = "Assets/Resources/Tutorial/TutorialConfig.asset";

    [MenuItem("Tools/Balance/CSV to SO/Tutorial Steps")]
    public static void Import()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"튜토리얼 CSV 파일을 찾을 수 없습니다: {CsvPath}");
            return;
        }

        TutorialConfig parsed = TutorialConfig.CreateFromCsv(File.ReadAllText(CsvPath));
        TutorialConfig asset = AssetDatabase.LoadAssetAtPath<TutorialConfig>(OutputPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<TutorialConfig>();
            AssetDatabase.CreateAsset(asset, OutputPath);
        }

        // 폰트/오버레이 프리팹 참조는 SO에 남기고 CSV는 스텝 데이터만 갱신한다.
        asset.ReplaceSteps(new List<TutorialConfig.TutorialStep>(parsed.Steps));
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Object.DestroyImmediate(parsed);
        Debug.Log($"튜토리얼 CSV SO 가져오기 완료: {OutputPath}");
    }
}
#endif
