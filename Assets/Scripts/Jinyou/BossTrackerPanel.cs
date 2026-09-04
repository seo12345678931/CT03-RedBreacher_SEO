using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BossTrackerPanel : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private BossTracker bossTracker;
    [SerializeField] private PlayerController player;
    [SerializeField] private CombatHealth playerHealth;
    [SerializeField] private PlayerDroneController playerDroneController;

    [Header("Ticket")]
    [SerializeField] private TMP_Text ticketText;
    [SerializeField] private TMP_Text productionText;
    [SerializeField] private Image ticketProgressFill;

    [Header("CommandCenter Level")]
    [SerializeField] private TMP_Text levelText;
    
    [Header("Boss Info")] 
    [SerializeField] private Image bossLockIcon;
    [SerializeField] private TMP_Text bossLockStateText;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text bossHealthText;
    [SerializeField] private TMP_Text bossDifficultyText;
    [SerializeField] private TMP_Text recommendedPowerText;
    [SerializeField] private TMP_Text rangedAttackText;
    [SerializeField] private TMP_Text laserAttackText;
    [SerializeField] private TMP_Text creditRewardText;
    [SerializeField] private TMP_Text coreRewardText;

    [Header("Visual")]
    [SerializeField] private Image bossIcon;

    [Header("Selection Buttons")]
    [SerializeField] private Button previousBossButton;
    [SerializeField] private Button nextBossButton;
    [SerializeField] private Button previousDifficultyButton;
    [SerializeField] private Button nextDifficultyButton;

    private Color defaultBossDifficultyTextColor = Color.white;
    private bool hasDefaultBossDifficultyTextColor;

    private void OnEnable()
    {
        ResolveReferences();
        ResolvePanelWidgets();
        CacheDefaultColors();
        BindButtons();
        if (bossTracker != null)
        {
            bossTracker.SelectionChanged += Refresh;
        }

        RefreshNow();
    }

    private void OnDisable()
    {
        UnbindButtons();
        if (bossTracker != null)
        {
            bossTracker.SelectionChanged -= Refresh;
        }
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

    private void Refresh()
    {
        ResolveReferences();
        CommandCenter commandCenter = baseCampManager != null
            ? baseCampManager.CommandCenter
            : bossTracker != null ? bossTracker.CmdCenter : null;

        if (commandCenter == null)
        {
            SetText(ticketText, "티켓 --/--");
            SetText(productionText, "티켓 생산 정보 없음");
            SetText(levelText, "사령부 Lv. --");
            SetBossInfo(null, null);
            SetBossLockIcon(null, null);
            SetFill(ticketProgressFill, 0f);
            return;
        }

        BossTracker.BossDefinition boss = bossTracker != null ? bossTracker.SelectedBoss : null;
        BossTracker.BossDifficulty difficulty = bossTracker != null
            ? bossTracker.SelectedDifficulty
            : null;

        SetText(ticketText, $"티켓 {commandCenter.BossTickets}/{commandCenter.BossTicketCapacity}");
        SetText(productionText, $"하루 {commandCenter.BossTicketsProducedPerDay}개 지급");
        SetText(levelText, $"사령부 Lv.{commandCenter.Level:0}");
        SetText(bossLockStateText, BossLockState(boss, difficulty));
        SetText(bossDifficultyText, $"({difficulty.displayName})");
        SetDifficultyTextColor(difficulty);
        SetText(recommendedPowerText, BuildRecommendedPowerText(difficulty));
        SetBossInfo(boss, difficulty);
        SetBossLockIcon(boss, difficulty);
        SetFill(ticketProgressFill, commandCenter.BossTicketCapacity > 0
            ? (float)commandCenter.BossTickets / commandCenter.BossTicketCapacity
            : 0f);

        bool hasMultipleBosses = bossTracker != null && bossTracker.Bosses.Count > 1;
        SetInteractable(previousBossButton, hasMultipleBosses);
        SetInteractable(nextBossButton, hasMultipleBosses);
        SetInteractable(previousDifficultyButton, bossTracker != null && bossTracker.Difficulties.Count > 1);
        SetInteractable(nextDifficultyButton, bossTracker != null && bossTracker.Difficulties.Count > 1);
    }
    
    // 해금 정보를 표시하는 UI 로직
    private string BossLockState(BossTracker.BossDefinition boss, BossTracker.BossDifficulty difficulty)
    {
        if (bossTracker == null || difficulty == null)
        {
            return "해금 정보 없음";
        }

        BossEnemyConfig config = bossTracker.ResolveBossConfig(boss, difficulty);
        int requiredStage = config != null ? config.UnlockStage : 0;
        int requiredPlayerLevel = config != null ? config.UnlockPlayerLevel : 0;
        if (bossTracker.IsDifficultyUnlocked(boss, difficulty))
        {
            return "해금됨";
        }

        string state = string.Empty;
        CommandCenter commandCenter = baseCampManager != null
            ? baseCampManager.CommandCenter
            : bossTracker.CmdCenter;
        int commandCenterLevel = commandCenter != null ? commandCenter.Level : 1;
        if (commandCenterLevel < difficulty.requiredResearchLabLevel)
        {
            AppendCondition(ref state, $"사령부 Lv.{difficulty.requiredResearchLabLevel} 필요");
        }

        if (requiredStage > 0 && bossTracker.CurrentStage < requiredStage)
        {
            AppendCondition(ref state, $"스테이지 {requiredStage} 도달");
        }

        if (requiredPlayerLevel > 0 && bossTracker.CommanderLevel < requiredPlayerLevel)
        {
            AppendCondition(ref state, $"지휘관 Lv.{requiredPlayerLevel} 달성");
        }

        return string.IsNullOrWhiteSpace(state) ? "해금 정보 없음" : state;
    }

    private static void AppendCondition(ref string state, string condition)
    {
        if (!string.IsNullOrEmpty(state))
        {
            state += "\n";
        }

        state += condition;
    }

    /// <summary>
    /// 플레이어의 전투력과 보스전 진입 시 권장 전투력을 UI로 비교해가는 함수
    /// </summary>
    /// <param name="difficulty"></param>
    /// <returns></returns>
    private string BuildRecommendedPowerText(BossTracker.BossDifficulty difficulty)
    {
        DroneConfig drone = playerDroneController != null ? playerDroneController.DroneConfig : null;
        string currentPower = PlayerStatusHud.BuildPlayerPowerText(player, playerHealth, drone);
        float recommendedPower = difficulty != null ? difficulty.recommendedPower : 0f;
        int currentPowerValue = ParsePowerValue(currentPower);
        
        // 현재 전투력이 권장 전투력보다 낮으면 빨간색(#FF0000) : 길거나 같으면 초록색 (#0E9A3C)
        string currentPowerColor = currentPowerValue < recommendedPower ? "#FF0000" : "#0E9A3C";

        return $"권장 전투력: <color={currentPowerColor}>{currentPower}</color> / {recommendedPower:N0}";
    }

    private static int ParsePowerValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return int.TryParse(value.Replace(",", string.Empty), out int result)
            ? result
            : 0;
    }
    private void SetBossLockIcon(BossTracker.BossDefinition boss, BossTracker.BossDifficulty difficulty)
    {
        if (bossLockIcon == null)
        {
            return;
        }

        bool unlocked = bossTracker != null && bossTracker.IsDifficultyUnlocked(boss, difficulty);
        bossLockIcon.gameObject.SetActive(!unlocked);
    }

    private void SetDifficultyTextColor(BossTracker.BossDifficulty difficulty)
    {
        if (bossDifficultyText == null)
        {
            return;
        }

        bossDifficultyText.color = difficulty?.difficultyId switch
        {
            "hard" => new Color32(0xFF, 0x81, 0x00, 0xFF),
            "elite" => new Color32(0xFF, 0x00, 0x00, 0xFF),
            _ => defaultBossDifficultyTextColor
        };
    }

    /// <summary>
    /// 보스에 대한 정보
    /// </summary>
    /// <param name="boss"></param>
    /// <param name="difficulty"></param>
    private void SetBossInfo(
        BossTracker.BossDefinition boss,
        BossTracker.BossDifficulty difficulty)
    {
        BossEnemyConfig config = boss != null ? boss.bossConfig : null;
        string displayName = boss != null
            ? boss.DisplayName
            : config != null ? config.DisplayName : "선택된 보스 없음";

        float healthMultiplier = difficulty != null ? difficulty.healthMultiplier : 1f;
        float damageMultiplier = difficulty != null ? difficulty.damageMultiplier : 1f;
        float rewardMultiplier = difficulty != null ? difficulty.rewardMultiplier : 1f;

        SetText(bossNameText, displayName);
        SetText(bossHealthText, config != null
            ? $"{config.MaxHealth * healthMultiplier:0} <color=#ffffff>HP</color>"
            : string.Empty);
        SetText(rangedAttackText, config != null
            ? $"범위 공격 ({config.RangedAttackDamage * damageMultiplier:0})"
            : string.Empty);
        SetText(laserAttackText, config != null
            ? $"레이저 공격 ({config.LaserDamage * damageMultiplier:0})"
            : string.Empty);
        SetText(creditRewardText, config != null
            ? $"{Mathf.RoundToInt(config.CreditReward * rewardMultiplier):N0}"
            : string.Empty);
        SetText(coreRewardText, config != null
            ? $"{Mathf.RoundToInt(config.CoreCrystalReward * rewardMultiplier):N0}"
            : string.Empty);

        if (bossIcon != null)
        {
            Sprite portrait = boss != null
                ? boss.Portrait
                : config != null ? config.Portrait : null;
            bossIcon.sprite = portrait;
            bossIcon.enabled = portrait != null;
            bossIcon.preserveAspect = true;
        }
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        bossTracker ??= FindFirstObjectByType<BossTracker>();
        player ??= FindFirstObjectByType<PlayerController>();

        if (playerHealth == null && player != null)
        {
            player.TryGetComponent(out playerHealth);
        }

        playerDroneController ??= player != null
            ? player.GetComponentInChildren<PlayerDroneController>(true)
            : FindFirstObjectByType<PlayerDroneController>(FindObjectsInactive.Include);
    }

    private void ResolvePanelWidgets()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        Image[] images = GetComponentsInChildren<Image>(true);

        bossNameText ??= FindByName(texts, "BossName_txt");
        bossHealthText ??= FindByName(texts, "Boss_Health_Num");
        rangedAttackText ??= FindByName(texts, "RangedAttack_txt");
        laserAttackText ??= FindByName(texts, "LaserPatten_txt");
        creditRewardText ??= FindByName(texts, "CreditReward_txt");
        coreRewardText ??= FindByName(texts, "CoreReward_txt");
        bossIcon ??= FindByName(images, "Boss_Icon");
    }

    private void CacheDefaultColors()
    {
        if (hasDefaultBossDifficultyTextColor || bossDifficultyText == null)
        {
            return;
        }

        defaultBossDifficultyTextColor = bossDifficultyText.color;
        hasDefaultBossDifficultyTextColor = true;
    }

    private void BindButtons()
    {
        previousBossButton?.onClick.AddListener(SelectPreviousBoss);
        nextBossButton?.onClick.AddListener(SelectNextBoss);
        previousDifficultyButton?.onClick.AddListener(SelectPreviousDifficulty);
        nextDifficultyButton?.onClick.AddListener(SelectNextDifficulty);
    }

    private void UnbindButtons()
    {
        previousBossButton?.onClick.RemoveListener(SelectPreviousBoss);
        nextBossButton?.onClick.RemoveListener(SelectNextBoss);
        previousDifficultyButton?.onClick.RemoveListener(SelectPreviousDifficulty);
        nextDifficultyButton?.onClick.RemoveListener(SelectNextDifficulty);
    }

    private void SelectPreviousBoss()
    {
        bossTracker?.SelectPreviousBoss();
    }

    private void SelectNextBoss()
    {
        bossTracker?.SelectNextBoss();
    }

    private void SelectPreviousDifficulty()
    {
        bossTracker?.SelectPreviousDifficulty();
    }

    private void SelectNextDifficulty()
    {
        bossTracker?.SelectNextDifficulty();
    }

    private static T FindByName<T>(T[] components, string objectName) where T : Component
    {
        foreach (T component in components)
        {
            if (component != null && component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetFill(Image target, float value)
    {
        if (target != null)
        {
            target.fillAmount = Mathf.Clamp01(value);
        }
    }

    private static void SetInteractable(Button target, bool value)
    {
        if (target != null)
        {
            target.interactable = value;
        }
    }
}
