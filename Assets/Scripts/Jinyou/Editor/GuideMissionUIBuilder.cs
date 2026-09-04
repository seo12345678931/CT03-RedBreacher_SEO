#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 고정 가이드 미션 바 프리팹을 코드로 생성한다.
/// Unity API가 GUID/fileID를 직접 해결하므로 손으로 YAML을 쓸 때 생기는 깨짐을 방지한다.
/// </summary>
public static class GuideMissionUIBuilder
{
    private const string FontGuid = "a5e325fc73a121645b8bb045804e62a4"; // NEXON Lv2 Gothic SDF
    private const string OutputDir = "Assets/Prefabs/SEO_UI";
    private const string OutputPath = OutputDir + "/GuideMissionBar.prefab";

    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.92f);
    private static readonly Color AccentColor = new Color(1f, 0.45f, 0.12f, 1f);
    private static readonly Color TrackColor = new Color(0.16f, 0.17f, 0.2f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f, 1f);

    [MenuItem("Tools/Guide/Build Guide Mission UI Prefab")]
    public static void Build()
    {
        TMP_FontAsset font = LoadFont();

        // ── 루트 바 (상단 가로 스트레치 고정) ───────────────────────────────
        GameObject root = CreateUIObject("GuideMissionBar", null);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -20f);
        rootRect.sizeDelta = new Vector2(-40f, 132f);

        Image bg = root.AddComponent<Image>();
        bg.color = PanelColor;
        bg.raycastTarget = true;

        MainGuideMissionPanel panel = root.AddComponent<MainGuideMissionPanel>();

        // ── "가이드 임무" 라벨 + 제목 ────────────────────────────────────────
        TMP_Text title = CreateText("GuideTitle_Txt", root, font, 30, FontStyles.Bold, AccentColor,
            TextAlignmentOptions.TopLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -10f), new Vector2(-360f, 40f));
        title.text = "임무";

        TMP_Text desc = CreateText("GuideDesc_Txt", root, font, 24, FontStyles.Normal, TextColor,
            TextAlignmentOptions.TopLeft);
        SetRect(desc.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -52f), new Vector2(-360f, 34f));
        desc.text = "목표를 달성하라";

        // ── 진행 트랙 + 채움 + 수치 ──────────────────────────────────────────
        GameObject track = CreateUIObject("GuideProgress_BG", root);
        SetRect(track.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 16f), new Vector2(-360f, 26f));
        Image trackImg = track.AddComponent<Image>();
        trackImg.color = TrackColor;

        GameObject fill = CreateUIObject("GuideProgress_Fill", track);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = AccentColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0.4f;
        fillImg.sprite = trackImg.sprite;

        TMP_Text progressNum = CreateText("GuideProgress_Num", track, font, 20, FontStyles.Bold, TextColor,
            TextAlignmentOptions.Center);
        RectTransform numRect = progressNum.rectTransform;
        numRect.anchorMin = Vector2.zero;
        numRect.anchorMax = Vector2.one;
        numRect.offsetMin = Vector2.zero;
        numRect.offsetMax = Vector2.zero;
        progressNum.text = "0 / 0";

        // ── 보상 슬롯 (재화 아이콘+개수, 도전과제 슬롯 형태). 완료 시 탭하여 수령 ──
        GameObject claim = CreateUIObject("GuideClaim_Btn", root);
        SetRect(claim.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(168f, 92f));
        Image claimImg = claim.AddComponent<Image>();
        claimImg.color = AccentColor;
        Button claimButton = claim.AddComponent<Button>();
        claimButton.targetGraphic = claimImg;

        // 보상 재화 아이콘(좌측). 스프라이트는 패널 인스펙터에서 재화별로 지정한다.
        GameObject rewardIconObj = CreateUIObject("GuideReward_Icon", claim);
        SetRect(rewardIconObj.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(56f, 56f));
        Image rewardIconImg = rewardIconObj.AddComponent<Image>();
        rewardIconImg.color = Color.white;
        rewardIconImg.preserveAspect = true;
        rewardIconImg.raycastTarget = false;
        rewardIconImg.enabled = false; // 스프라이트가 지정되기 전까지 숨김

        // 보상 개수(아이콘 오른쪽).
        TMP_Text rewardNum = CreateText("GuideReward_Num", claim, font, 26, FontStyles.Bold, Color.white,
            TextAlignmentOptions.MidlineLeft);
        RectTransform rewardNumRect = rewardNum.rectTransform;
        rewardNumRect.anchorMin = Vector2.zero;
        rewardNumRect.anchorMax = Vector2.one;
        rewardNumRect.offsetMin = new Vector2(74f, 0f);
        rewardNumRect.offsetMax = new Vector2(-10f, 0f);
        rewardNum.text = "x0";

        // 완료 표시 뱃지(점). 비활성 기본값.
        GameObject readyBadge = CreateUIObject("GuideClaim_Ready", claim);
        SetRect(readyBadge.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-2f, -2f), new Vector2(18f, 18f));
        Image readyImg = readyBadge.AddComponent<Image>();
        readyImg.color = new Color(1f, 0.2f, 0.2f, 1f);
        readyBadge.SetActive(false);

        // ── 패널 바인더에 참조 주입 ──────────────────────────────────────────
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("contentRoot").objectReferenceValue = root;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("descriptionText").objectReferenceValue = desc;
        so.FindProperty("progressText").objectReferenceValue = progressNum;
        so.FindProperty("progressFill").objectReferenceValue = fillImg;
        so.FindProperty("claimButton").objectReferenceValue = claimButton;
        so.FindProperty("claimReadyBadge").objectReferenceValue = readyBadge;
        so.FindProperty("rewardIcon").objectReferenceValue = rewardIconImg;
        so.FindProperty("rewardAmountText").objectReferenceValue = rewardNum;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── 프리팹 저장 ──────────────────────────────────────────────────────
        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out bool success);
        Object.DestroyImmediate(root);

        if (success && saved != null)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;
            Debug.Log($"가이드 미션 UI 프리팹 생성 완료: {OutputPath}");
        }
        else
        {
            Debug.LogError($"가이드 미션 UI 프리팹 저장 실패: {OutputPath}");
        }
    }

    private static TMP_FontAsset LoadFont()
    {
        string path = AssetDatabase.GUIDToAssetPath(FontGuid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("NEXON 폰트를 찾지 못해 기본 TMP 폰트를 사용합니다.");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static GameObject CreateUIObject(string name, GameObject parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.GetComponent<RectTransform>().SetParent(parent.GetComponent<RectTransform>(), false);
        }

        return go;
    }

    private static TMP_Text CreateText(
        string name,
        GameObject parent,
        TMP_FontAsset font,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    // 부모 기준 앵커/피벗과 오프셋 위치, 크기를 한 번에 지정한다.
    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
#endif
