using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CommandCenterPanel : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private BaseCampManager baseCampManager;

    [Header("Base CMD")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text levelText;

    [Header("PlayerLevel")]
    [SerializeField] private TMP_Text playerLevelText;
    [SerializeField] private TMP_Text playerEXPText;
    [SerializeField] private Image playerEXPProgressFill;

    [Header("BaseUnlockStatus")]
    [SerializeField] private baseUnlockStatus baseUnlockStatusPrefab;
    [SerializeField] private Transform statusList;

    [Header("Base CMD Upgrade")]
    [SerializeField] private TMP_Text commandNextLevelText;
    [SerializeField] private TMP_Text unlockDetailText;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text upgradeRemainingText;
    [SerializeField] private TMP_Text upgradeCostText;
    [SerializeField] private Image upgradeProgressFill;
    [SerializeField] private Image coinIcon;

    [Header("Visual")]
    [SerializeField] private Image facilityImage;
    [SerializeField] private Sprite[] levelSprites;

    private readonly List<baseUnlockStatus> _baseUnlockStatusList = new List<baseUnlockStatus>();
    private CommandCenter cmdCenter;
    private BossTracker bossTracker;
    private PlayerProgression progression;
    private float observedUpgradeDuration;

    private void OnEnable()
    {
        ResolveReferences();
        upgradeButton?.onClick.AddListener(UpgradeResearchLab);
        RefreshNow();
    }

    private void OnDisable()
    {
        upgradeButton?.onClick.RemoveListener(UpgradeResearchLab);
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

    public void Configure(
        BaseCampManager manager,
        Button upgrade,
        TMP_Text level,
        TMP_Text upgradeLabel,
        TMP_Text unlock,
        Image targetImage,
        Sprite[] sprites)
    {
        baseCampManager = manager;
        upgradeButton = upgrade;
        levelText = level;
        upgradeText = upgradeLabel;
        unlockDetailText = unlock;
        facilityImage = targetImage;
        levelSprites = sprites;
        Refresh();
    }

    private void UpgradeResearchLab()
    {
        baseCampManager?.UpgradeResearchLab();
        Refresh();
    }

    private void Refresh()
    {
        ResolveReferences();
        RefreshPlayerProgression();

        if (cmdCenter == null)
        {
            SetUpgradeRemainingText(upgradeRemainingText, false, 0f);
            SetActive(upgradeText != null ? upgradeText.gameObject : null, true);
            return;
        }

        SetText(levelText, $"Lv.{cmdCenter.Level}");
        SetText(commandNextLevelText, cmdCenter.Level < cmdCenter.MaxLevel
            ? $"사령부 Lv.{cmdCenter.Level + 1}"
            : "최대 레벨");
        UpdateFacilityVisual();
        BaseCampUpgradeButtonText.Set(
            upgradeText,
            upgradeCostText,
            "업그레이드",
            cmdCenter.UpgradeCost,
            !cmdCenter.IsUpgrading && cmdCenter.Level < cmdCenter.MaxLevel);
        SetUpgradeRemainingText(
            upgradeRemainingText,
            cmdCenter.IsUpgrading,
            cmdCenter.UpgradeRemainingSeconds);
        SetActive(upgradeText != null ? upgradeText.gameObject : null, !cmdCenter.IsUpgrading);
        SetActive(coinIcon != null ? coinIcon.gameObject : null, !cmdCenter.IsUpgrading);
        SetText(unlockDetailText, BuildUnlockSummary());
        RefreshBaseUnlockStatuses();

        if (upgradeButton != null && baseCampManager != null)
        {
            int researchLabLevel = cmdCenter.Level;
            upgradeButton.interactable = cmdCenter.CanStartUpgrade(
                baseCampManager.Credits,
                baseCampManager.CommanderLevel,
                researchLabLevel);
        }

        BaseCampUpgradeStatus.SetUpgradeProgress(upgradeProgressFill, cmdCenter, ref observedUpgradeDuration);
    }

    // baseUnlockStatusPrefab을 연결하여 기지명(displayName), 레벨(requiredLabLevel), 언락상태(bool unlocked)를
    // FacilityUnlock에 연결해서 동기화.
    private void RefreshBaseUnlockStatuses()
    {
        if (baseUnlockStatusPrefab == null || statusList == null || cmdCenter == null)
        {
            return;
        }

        if (_baseUnlockStatusList.Count != cmdCenter.FacilityUnlocks.Count)
        {
            RebuildBaseUnlockStatuses();
        }

        BaseCampFacilityView[] facilityViews = FindObjectsByType<BaseCampFacilityView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cmdCenter.FacilityUnlocks.Count && i < _baseUnlockStatusList.Count; i++)
        {
            CommandCenter.FacilityUnlock unlock = cmdCenter.FacilityUnlocks[i];
            if (unlock != null)
            {
                cmdCenter.IsFacilityUnlocked(unlock.facilityId);
            }

            baseUnlockStatus status = _baseUnlockStatusList[i];
            if (status != null)
            {
                BaseCampFacilityView facilityView = FindFacilityView(facilityViews, unlock?.facilityId);
                status.Configure(facilityView, unlock);
            }
        }
    }

    private void RebuildBaseUnlockStatuses()
    {
        foreach (baseUnlockStatus status in _baseUnlockStatusList)
        {
            if (status != null)
            {
                Destroy(status.gameObject);
            }
        }

        _baseUnlockStatusList.Clear();
        BaseCampFacilityView[] facilityViews = FindObjectsByType<BaseCampFacilityView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (CommandCenter.FacilityUnlock unlock in cmdCenter.FacilityUnlocks)
        {
            if (unlock == null)
            {
                continue;
            }

            cmdCenter.IsFacilityUnlocked(unlock.facilityId);
            BaseCampFacilityView facilityView = FindFacilityView(facilityViews, unlock.facilityId);
            baseUnlockStatus status = Instantiate(baseUnlockStatusPrefab, statusList);
            status.Configure(facilityView, unlock);
            _baseUnlockStatusList.Add(status);
        }
    }

    // baseUnlockStatusPrefab에 있는 Base_icon을 BaseCampFacilityView를 통해서 동기화
    private static BaseCampFacilityView FindFacilityView(
        BaseCampFacilityView[] facilityViews,
        string facilityId)
    {
        if (facilityViews == null)
        {
            return null;
        }

        foreach (BaseCampFacilityView facilityView in facilityViews)
        {
            if (facilityView != null && facilityView.FacilityId == facilityId)
            {
                return facilityView;
            }
        }

        return null;
    }

    // 사령부 기지 이미지 전용 비주얼 업그레이드
    private void UpdateFacilityVisual()
    {
        if (facilityImage == null || levelSprites == null || levelSprites.Length == 0 || cmdCenter == null)
        {
            return;
        }

        int index = Mathf.Clamp(cmdCenter.Level - 1, 0, levelSprites.Length - 1);
        facilityImage.sprite = levelSprites[index];
        facilityImage.color = Color.white;
    }

    // 다음 업그레이드 시 해금요소 미리보기
    private string BuildUnlockSummary()
    {
        List<string> summaryLines = new List<string>();
        int nextLevel = cmdCenter.Level + 1;

        foreach (CommandCenter.FacilityUnlock item in cmdCenter.FacilityUnlocks)
        {
            if (item == null || item.unlocked || item.requiredLabLevel > nextLevel)
            {
                continue;
            }

            summaryLines.Add($"{item.displayName} 해금");
        }

        // 관제탑 레벨은 실제로 사령부 레벨과 연결되어 있어 보스전 관련 해금요소를 미리 보여줄 필요가 있음.
        AddBossTicketPreview(summaryLines, nextLevel);
        AddBossDifficultyPreview(summaryLines, nextLevel);

        return summaryLines.Count == 0
            ? "해금 요소 없음"
            : string.Join("\n", summaryLines);
    }
    
    // 보스 티켓 수/ 1일 지급량 변화수치를 미리 보여주는 메소드
    private void AddBossTicketPreview(List<string> summaryLines, int nextLevel)
    {
        int currentCapacity = cmdCenter.BossTicketCapacity;
        int nextCapacity = cmdCenter.GetBossTicketCapacityForLevel(nextLevel);
        if (nextCapacity > currentCapacity)
        {
            summaryLines.Add($"보스 티켓 최대치 <b>{currentCapacity} > {nextCapacity}</b>");
        }

        int currentTicketsPerDay = cmdCenter.BossTicketsProducedPerDay;
        int nextTicketsPerDay = cmdCenter.GetBossTicketsProducedPerDayForLevel(nextLevel);
        if (nextTicketsPerDay > currentTicketsPerDay)
        {
            summaryLines.Add($"보스 티켓 일일 지급량 <b>{currentTicketsPerDay}개 > {nextTicketsPerDay}개</b>");
        }
    }

    // 보스 난이도 해금 정보를 미리 보여주는 메소드
    private void AddBossDifficultyPreview(List<string> summaryLines, int nextLevel)
    {
        if (bossTracker == null)
        {
            return;
        }

        foreach (BossTracker.BossDifficulty difficulty in bossTracker.Difficulties)
        {
            if (difficulty == null
                || difficulty.requiredResearchLabLevel > nextLevel
                || difficulty.requiredResearchLabLevel <= cmdCenter.Level)
            {
                continue;
            }

            string difficultyName = !string.IsNullOrWhiteSpace(difficulty.displayName)
                ? difficulty.displayName
                : difficulty.difficultyId;
            summaryLines.Add($"보스 난이도 해금 <b>{difficultyName}</b>");
        }
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        cmdCenter = baseCampManager != null ? baseCampManager.CommandCenter : null;
        bossTracker ??= FindFirstObjectByType<BossTracker>(FindObjectsInactive.Include);
        progression = baseCampManager != null
            ? baseCampManager.PlayerProgression
            : FindFirstObjectByType<PlayerProgression>();
    }

    private void RefreshPlayerProgression()
    {
        if (progression == null)
        {
            SetText(playerLevelText, string.Empty);
            SetText(playerEXPText, string.Empty);
            SetFill(playerEXPProgressFill, 0f);
            return;
        }

        SetText(playerLevelText, $"Lv.{progression.Level}");
        SetText(playerEXPText,
            $"{progression.CurrentExperience:0} / {progression.ExperienceToNextLevel:0}");
        SetFill(playerEXPProgressFill, progression.ExperienceProgress01);
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

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static void SetUpgradeRemainingText(TMP_Text target, bool isUpgrading, float remainingSeconds)
    {
        SetText(target, isUpgrading ? $"완료까지 {remainingSeconds:0}초" : string.Empty);
        SetActive(target != null ? target.gameObject : null, isUpgrading);
    }
}
