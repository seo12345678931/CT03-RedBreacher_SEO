using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>튜토리얼 진행도(PlayerPrefs)를 초기화하는 에디터 메뉴 — 재테스트용.</summary>
public static class TutorialDebug
{
    private const string StepIndexKey = "Tutorial.StepIndex";
    private const string CompletedKey = "Tutorial.Completed";
    private const string BaseStepIndexKey = "Tutorial.BasePopup.StepIndex";
    private const string BaseCompletedKey = "Tutorial.BasePopup.Completed";
    private const string CoreChargerStepIndexKey = "Tutorial.CoreCharger.StepIndex";
    private const string CoreChargerCompletedKey = "Tutorial.CoreCharger.Completed";
    private const string InventoryStepIndexKey = "Tutorial.Inventory.StepIndex";
    private const string InventoryCompletedKey = "Tutorial.Inventory.Completed";
    private const string BossEncounterStepIndexKey = "Tutorial.BossEncounter.StepIndex";
    private const string BossEncounterCompletedKey = "Tutorial.BossEncounter.Completed";
    private const string TutorialFolder = "Assets/Resources/Tutorial";
    private const string ConfigAssetPath = TutorialFolder + "/TutorialConfig.asset";
    private const string OverlayPrefabPath = TutorialFolder + "/TutorialOverlay.prefab";

    [MenuItem("Tools/Tutorial/Reset Tutorial Progress")]
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(StepIndexKey);
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.DeleteKey(BaseStepIndexKey);
        PlayerPrefs.DeleteKey(BaseCompletedKey);
        PlayerPrefs.DeleteKey(CoreChargerStepIndexKey);
        PlayerPrefs.DeleteKey(CoreChargerCompletedKey);
        PlayerPrefs.DeleteKey(BossEncounterStepIndexKey);
        PlayerPrefs.DeleteKey(BossEncounterCompletedKey);
        PlayerPrefs.DeleteKey(InventoryStepIndexKey);
        PlayerPrefs.DeleteKey(InventoryCompletedKey);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] 진행도를 초기화했습니다. 다음 재생 시 온보딩이 처음부터 시작됩니다.");
    }

    [MenuItem("Tools/Tutorial/Create Overlay Prefab")]
    public static void CreateOverlayPrefab()
    {
        Directory.CreateDirectory(TutorialFolder);
        AssetDatabase.Refresh();

        TutorialConfig config = AssetDatabase.LoadAssetAtPath<TutorialConfig>(ConfigAssetPath);
        TMP_FontAsset bodyFont = GetConfigBodyFont(config);

        GameObject root = new GameObject(
            "TutorialOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(TutorialOverlay));
        RectTransform rootRect = (RectTransform)root.transform;
        SetAnchors(rootRect, 0f, 0f, 1f, 1f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        RectTransform topQuad = MakeDim("Dim_Top", rootRect);
        RectTransform bottomQuad = MakeDim("Dim_Bottom", rootRect);
        RectTransform leftQuad = MakeDim("Dim_Left", rootRect);
        RectTransform rightQuad = MakeDim("Dim_Right", rootRect);
        Image fullDim = MakeDim("Dim_Full", rootRect).GetComponent<Image>();
        SetAnchors(fullDim.rectTransform, 0f, 0f, 1f, 1f);

        RectTransform bubble = MakeBubble(rootRect);
        TMP_Text bubbleText = MakeText("Body", bubble, bodyFont, 40f, TextAlignmentOptions.Left);
        RectTransform bodyRect = (RectTransform)bubbleText.transform;
        SetAnchors(bodyRect, 0f, 0f, 1f, 1f);
        bodyRect.offsetMin = new Vector2(36f, 30f);
        bodyRect.offsetMax = new Vector2(-36f, -22f);

        TMP_Text tapHintText = MakeText("TapHint", bubble, bodyFont, 24f, TextAlignmentOptions.Center);
        tapHintText.text = "탭하여 계속";
        tapHintText.color = new Color(1f, 1f, 1f, 0.6f);
        RectTransform hintRect = (RectTransform)tapHintText.transform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 8f);
        hintRect.sizeDelta = new Vector2(420f, 32f);

        TutorialOverlay overlay = root.GetComponent<TutorialOverlay>();
        BindOverlayFields(overlay, rootRect, bodyFont, topQuad, bottomQuad, leftQuad, rightQuad, fullDim, bubble, bubbleText, tapHintText.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OverlayPrefabPath);
        Object.DestroyImmediate(root);

        TutorialOverlay prefabOverlay = prefab != null ? prefab.GetComponent<TutorialOverlay>() : null;
        AssignOverlayPrefab(config, prefabOverlay);
        AssetDatabase.SaveAssets();

        Selection.activeObject = prefab;
        Debug.Log("[Tutorial] TutorialOverlay 프리팹을 생성하고 TutorialConfig에 연결했습니다.");
    }

    private static RectTransform MakeDim(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true; // 강조 구멍 외 영역 입력을 막는다.
        return (RectTransform)go.transform;
    }

    private static RectTransform MakeBubble(Transform parent)
    {
        GameObject go = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.08f, 0.10f);
        rect.anchorMax = new Vector2(0.92f, 0.26f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        image.raycastTarget = true;
        return rect;
    }

    private static TMP_Text MakeText(string objectName, Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(22f, fontSize);
        text.fontSizeMax = fontSize + 4f;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_FontAsset GetConfigBodyFont(TutorialConfig config)
    {
        if (config == null)
        {
            return null;
        }

        SerializedObject serializedConfig = new SerializedObject(config);
        return serializedConfig.FindProperty("bodyFont").objectReferenceValue as TMP_FontAsset;
    }

    private static void AssignOverlayPrefab(TutorialConfig config, TutorialOverlay overlay)
    {
        if (config == null || overlay == null)
        {
            return;
        }

        SerializedObject serializedConfig = new SerializedObject(config);
        serializedConfig.FindProperty("overlayPrefab").objectReferenceValue = overlay;
        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
    }

    private static void BindOverlayFields(
        TutorialOverlay overlay,
        RectTransform root,
        TMP_FontAsset font,
        RectTransform topQuad,
        RectTransform bottomQuad,
        RectTransform leftQuad,
        RectTransform rightQuad,
        Image fullDim,
        RectTransform bubble,
        TMP_Text bubbleText,
        GameObject tapHint)
    {
        SerializedObject serializedOverlay = new SerializedObject(overlay);
        serializedOverlay.FindProperty("root").objectReferenceValue = root;
        serializedOverlay.FindProperty("font").objectReferenceValue = font;
        serializedOverlay.FindProperty("topQuad").objectReferenceValue = topQuad;
        serializedOverlay.FindProperty("bottomQuad").objectReferenceValue = bottomQuad;
        serializedOverlay.FindProperty("leftQuad").objectReferenceValue = leftQuad;
        serializedOverlay.FindProperty("rightQuad").objectReferenceValue = rightQuad;
        serializedOverlay.FindProperty("fullDim").objectReferenceValue = fullDim;
        serializedOverlay.FindProperty("bubble").objectReferenceValue = bubble;
        serializedOverlay.FindProperty("bubbleText").objectReferenceValue = bubbleText;
        serializedOverlay.FindProperty("tapHint").objectReferenceValue = tapHint;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
