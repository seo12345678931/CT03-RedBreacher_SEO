using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BossSummonButton : MonoBehaviour
{
    [Header("Source")]
    [FormerlySerializedAs("bossDungeon")]
    [SerializeField] private BossTracker bossTracker;

    [Header("UI")]
    [SerializeField] private Button summonButton;
    [SerializeField] private TMP_Text ticketText;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text stateText;

    private string stateMessage = string.Empty;

    private void Awake()
    {
        summonButton ??= GetComponent<Button>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        summonButton?.onClick.AddListener(TrySummonBoss);
        RefreshNow();
    }

    private void OnDisable()
    {
        summonButton?.onClick.RemoveListener(TrySummonBoss);
    }

    // 매 프레임 문자열 생성/TMP 갱신을 피하기 위한 주기적 갱신 간격.
    private const float RefreshInterval = 0.25f;
    private float nextRefreshTime;

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        RefreshNow();
    }

    private void RefreshNow()
    {
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    public void TrySummonBoss()
    {
        ResolveReferences();
        if (bossTracker == null)
        {
            stateMessage = "보스 트래커가 연결되지 않았습니다.";
            Refresh();
            return;
        }

        BossTracker.BossDefinition boss = bossTracker.SelectedBoss;
        BossTracker.BossDifficulty difficulty = bossTracker.SelectedDifficulty;
        if (boss == null || difficulty == null)
        {
            stateMessage = "선택된 보스가 없습니다.";
            Refresh();
            return;
        }

        if (!bossTracker.IsDifficultyUnlocked(difficulty))
        {
            stateMessage = $"{difficulty.displayName} 난이도가 잠겨 있습니다.";
            Refresh();
            return;
        }

        if (bossTracker.TryEnterSelected())
        {
            stateMessage = $"{GetBossName(boss)} - {difficulty.displayName} 소환";
            DailyMissionManager.ReportBossTicketUsed();
            MainGuideMissionManager.ReportBossTicketUsed();
        }
        else
        {
            stateMessage = "티켓 수 또는 보스전 진행 상태를 확인하세요.";
        }

        Refresh();
    }

    private void Refresh()
    {
        ResolveReferences();
        CommandCenter commandCenter = bossTracker != null ? bossTracker.CmdCenter : null;
        BossTracker.BossDefinition boss = bossTracker != null ? bossTracker.SelectedBoss : null;
        BossTracker.BossDifficulty difficulty = bossTracker != null
            ? bossTracker.SelectedDifficulty
            : null;

        if (ticketText != null)
        {
            ticketText.text = commandCenter != null
                ? $"티켓 {commandCenter.BossTickets}/{commandCenter.BossTicketCapacity}"
                : "티켓 --/--";
        }

        if (bossNameText != null)
        {
            bossNameText.text = boss != null && difficulty != null
                ? $"{GetBossName(boss)} [{difficulty.displayName}]"
                : "선택된 보스 없음";
        }

        if (stateText != null)
        {
            stateText.text = stateMessage;
        }

        if (summonButton != null)
        {
            summonButton.interactable = bossTracker != null && bossTracker.CanEnterSelected();
        }
    }

    private static string GetBossName(BossTracker.BossDefinition boss)
    {
        return boss != null ? boss.DisplayName : "보스";
    }

    private void ResolveReferences()
    {
        bossTracker ??= FindFirstObjectByType<BossTracker>();
    }
}
