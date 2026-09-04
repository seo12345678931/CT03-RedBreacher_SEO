using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>스텝이 다음으로 넘어가는 조건.</summary>
public enum TutorialAdvanceType
{
    Tap,           // 화면 어디든 탭하면 진행(정보 안내용)
    TargetClicked, // 강조한 버튼을 직접 누르면 진행(액션 유도)
    GameEvent      // 특정 게임 행동을 하면 진행
}

/// <summary><see cref="TutorialAdvanceType.GameEvent"/>일 때 감지할 행동.</summary>
public enum TutorialEventType
{
    None,
    EnemyKilled,        // 적 처치(정적 Report)
    PlayerLevelReached, // 레벨업(정적 Report)
    PartEquipped,       // 파츠 장착(정적 Report)
    BossDodgeUsed,      // 보스전 회피 기동(정적 Report [PlayerBossDodgeController.StartDodge()])
    FacilityUpgraded,   // 시설 강화 완료(시설 OnUpgradeCompleted 구독)
    WeaponEnhanced,     // 무기 강화 완료(AssemblyFactory.OnWeaponEnhanced 구독)
    UnitEnhanced,       // 유닛 강화 완료(CoreCharger.OnUnitEnhanced 구독)
    PanelOpened         // targetId(없으면 highlightTargetName) 오브젝트 활성화 감지
}

/// <summary>
/// 최초 1회 인터랙티브 온보딩 튜토리얼의 스텝 정의(ScriptableObject).
/// <see cref="GuideMissionConfig"/>의 SO 로드 패턴을 따른다 — Resources/Tutorial/TutorialConfig에 배치.
/// </summary>
[CreateAssetMenu(menuName = "Myth/Tutorial/Tutorial Config", fileName = "TutorialConfig")]
public class TutorialConfig : ScriptableObject
{
    [Serializable]
    public class TutorialStep
    {
        public int order = 1;
        public string id;

        [TextArea]
        [Tooltip("말풍선에 표시할 안내문.")]
        public string bodyText;

        [Tooltip("강조(스포트라이트)할 UI 오브젝트 이름. 씬 전체에서 이름으로 검색한다. 비우면 전체 화면 안내(하이라이트 없음).")]
        public string highlightTargetName;

        public TutorialAdvanceType advanceType = TutorialAdvanceType.Tap;

        [Header("advanceType == GameEvent 전용")]
        public TutorialEventType eventType = TutorialEventType.None;

        [Tooltip("이 행동을 몇 번 해야 진행되는지(예: 적 1마리 처치).")]
        public int targetAmount = 1;

        [Tooltip("PanelOpened 감지 대상 오브젝트 이름(비우면 highlightTargetName 사용).")]
        public string targetId;
    }

    [Header("표시")]
    [SerializeField]
    [Tooltip("말풍선 텍스트 폰트(한글: NEXON Lv2 Gothic SDF). 미지정 시 TMP 기본 폰트.")]
    private TMP_FontAsset bodyFont;

    [SerializeField]
    [Tooltip("튜토리얼 오버레이 프리팹. 비우면 Resources/Tutorial/TutorialOverlay를 사용한다.")]
    private TutorialOverlay overlayPrefab;

    [Header("스텝(순서대로 진행)")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    public TMP_FontAsset BodyFont => bodyFont;
    public TutorialOverlay OverlayPrefab => overlayPrefab != null
        ? overlayPrefab
        : Resources.Load<TutorialOverlay>("Tutorial/TutorialOverlay");
    public IReadOnlyList<TutorialStep> Steps => steps;

    private const string CsvResourcePath = "Tutorial/tutorial_steps";
    private const string ResourcePath = "Tutorial/TutorialConfig";
    private static TutorialConfig current;

    /// <summary>Resources/Tutorial/TutorialConfig에서 1회 로드 후 캐시.</summary>
    public static TutorialConfig Current
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

    public void ReplaceSteps(List<TutorialStep> importedSteps)
    {
        steps = SortByOrder(importedSteps ?? new List<TutorialStep>());
    }

    public static TutorialConfig CreateFromCsv(string csv)
    {
        TutorialConfig config = CreateInstance<TutorialConfig>();
        config.steps = SortByOrder(ParseSteps(csv));
        return config;
    }

    private static TutorialConfig LoadFromResources()
    {
        TutorialConfig asset = Resources.Load<TutorialConfig>(ResourcePath);
        if (asset != null)
        {
            return asset;
        }

        TextAsset csv = Resources.Load<TextAsset>(CsvResourcePath);
        if (csv == null)
        {
            Debug.LogError("튜토리얼 CSV를 찾을 수 없습니다. Assets/Resources/Tutorial 경로를 확인하세요.");
            return null;
        }

        // SO 임포트 전에도 CSV만 있으면 튜토리얼 스텝을 읽을 수 있게 한다.
        return CreateFromCsv(csv.text);
    }

    private static List<TutorialStep> SortByOrder(List<TutorialStep> source)
    {
        source.Sort((a, b) =>
        {
            int left = a != null ? a.order : int.MaxValue;
            int right = b != null ? b.order : int.MaxValue;
            return left.CompareTo(right);
        });
        return source;
    }

    private static List<TutorialStep> ParseSteps(string csv)
    {
        List<TutorialStep> result = new List<TutorialStep>();
        foreach (Dictionary<string, string> row in ParseCsv(csv))
        {
            // id 없는 행(끝의 잔여 쉼표 줄 등)은 스텝이 아니다. 이런 행이 들어오면
            // id 접두사 그룹 판정에서 메인 튜토리얼로 분류되어 끝에 빈 스텝이 하나 더 뜬다.
            if (string.IsNullOrWhiteSpace(Get(row, "id")))
            {
                continue;
            }

            result.Add(new TutorialStep
            {
                order = GetInt(row, "order", result.Count + 1),
                id = Get(row, "id"),
                bodyText = Get(row, "bodyText"),
                highlightTargetName = Get(row, "highlightTargetName"),
                advanceType = GetEnum(row, "advanceType", TutorialAdvanceType.Tap),
                eventType = GetEnum(row, "eventType", TutorialEventType.None),
                targetAmount = Mathf.Max(0, GetInt(row, "targetAmount", 1)),
                targetId = Get(row, "targetId")
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

