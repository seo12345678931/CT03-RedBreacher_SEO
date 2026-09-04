using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BaseCampHud : MonoBehaviour
{
    [SerializeField] private BaseCampManager baseCampManager;

    [Header("Base(Name / Level)")]
    [SerializeField] private TMP_Text commandCenterTitleText;
    [SerializeField] private TMP_Text creditRefineryTitleText;
    [SerializeField] private TMP_Text assemblyFactoryTitleText;
    [SerializeField] private TMP_Text bossTrackerTitleText;
    [SerializeField] private TMP_Text coreChargerTitleText;
    [SerializeField] private TMP_Text traitPointTitleText;
    [SerializeField] private TMP_Text skillHangerTitleText;

    [Header("Boss Ticket")]
    [SerializeField] private TMP_Text bossTicketText;

    [Header("Refinery Storage")]
    [SerializeField] private TMP_Text refineryStorageText;
    [SerializeField] private Image refineryStorageFill;
    [SerializeField] private Button collectButton;

    [Header("Controls")]
    [SerializeField] private Button exitButton;

    [Header("BaseUnlockStatus")]
    [SerializeField] private TMP_Text assemblyFactoryUnlockText;
    [SerializeField] private GameObject assemblyFactoryUnlockPanel;
    [SerializeField] private TMP_Text coreChargerUnlockText;
    [SerializeField] private GameObject coreChargerUnlockPanel;

    // 매 프레임 문자열 생성/TMP 갱신을 피하기 위한 주기적 갱신 간격.
    // 정제소 저장량처럼 실시간으로 차오르는 표시만 이 주기로 따라가면 충분하다.
    private const float RefreshInterval = 0.25f;
    private float nextRefreshTime;

    private void OnEnable()
    {
        ResolveReferences();
        collectButton?.onClick.AddListener(CollectCredits);
        EnsureExitButtonVisible();
        RefreshNow();
    }

    private void OnDisable()
    {
        collectButton?.onClick.RemoveListener(CollectCredits);
    }

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
        TMP_Text bossTicket,
        TMP_Text refineryStorage,
        Image refineryFill)
    {
        baseCampManager = manager;
        bossTicketText = bossTicket;
        refineryStorageText = refineryStorage;
        refineryStorageFill = refineryFill;
        RefreshNow();
    }

    private void Refresh()
    {
        ResolveReferences();
        EnsureExitButtonVisible();

        if (baseCampManager == null)
        {
            RefreshFacilityTitleTexts(null, null, null);
            SetText(bossTicketText, "티켓 --/--");
            SetText(refineryStorageText, "--/--");
            SetFill(refineryStorageFill, 0f);
            SetButtonInteractable(collectButton, false);
            RefreshBaseUnlockStatus();
            return;
        }

        CommandCenter researchLab = baseCampManager.CommandCenter;
        CreditRefinery refinery = baseCampManager.CreditRefinery;
        RefreshFacilityTitleTexts(researchLab, refinery, baseCampManager);

        if (researchLab != null)
        {
            SetText(bossTicketText, $"티켓 {researchLab.BossTickets}/{researchLab.BossTicketCapacity}");
        }
        else
        {
            SetText(bossTicketText, "티켓 --/--");
        }

        if (refinery != null)
        {
            float storageRate = refinery.StorageCapacity > 0
                ? (float)refinery.StoredCredits / refinery.StorageCapacity
                : 0f;
            bool isStorageFull = refinery.StorageCapacity > 0 &&
                                 refinery.StoredCredits >= refinery.StorageCapacity;

            SetText(refineryStorageText, isStorageFull
                ? $"가득참 ({refinery.StorageCapacity})"
                : $"{refinery.StoredCredits}/{refinery.StorageCapacity}");
            SetFill(refineryStorageFill, storageRate);
            SetButtonInteractable(collectButton, refinery.StoredCredits > 0);
        }
        else
        {
            SetText(refineryStorageText, "--/--");
            SetFill(refineryStorageFill, 0f);
            SetButtonInteractable(collectButton, false);
        }

        RefreshBaseUnlockStatus();
    }

    private void RefreshFacilityTitleTexts(
        CommandCenter commandCenter,
        CreditRefinery refinery,
        BaseCampManager manager)
    {
        SetFacilityTitle(commandCenterTitleText, "command_center", "사령부", commandCenter != null ? commandCenter.Level : (int?)null);
        SetFacilityTitle(creditRefineryTitleText, "energy_refinery", "자원 정제소", refinery != null ? refinery.Level : (int?)null);
        SetFacilityTitle(assemblyFactoryTitleText, "assembly_factory", "조립 공장", manager?.AssemblyFactory != null ? manager.AssemblyFactory.Level : (int?)null);
        SetFacilityTitle(bossTrackerTitleText, "boss_tracker", "관제탑", commandCenter != null ? commandCenter.Level : (int?)null);
        SetFacilityTitle(coreChargerTitleText, "core_charger", "코어 강화소", manager?.CoreCharger != null ? manager.CoreCharger.Level : (int?)null);
        SetFacilityTitle(traitPointTitleText, "trait_point_facility", "특성 연구소", 1);
        SetFacilityTitle(skillHangerTitleText, "skill_hanger", "스킬 격납고", manager?.SkillHanger != null ? manager.SkillHanger.Level : (int?)null);
        SetTitleVisibleByUnlock(assemblyFactoryTitleText, commandCenter, "assembly_factory");
        SetTitleVisibleByUnlock(coreChargerTitleText, commandCenter, "core_charger");
    }

    private void SetFacilityTitle(TMP_Text target, string facilityId, string fallbackName, int? level)
    {
        string displayName = GetFacilityDisplayName(facilityId, fallbackName);
        SetText(target, level.HasValue
            ? $"{displayName} <color=#EC9A0E>Lv.{level.Value}</color>"
            : $"{displayName} <color=#EC9A0E>Lv. --</color>");
    }

    private string GetFacilityDisplayName(string facilityId, string fallbackName)
    {
        CommandCenter.FacilityUnlock unlock = FindFacilityUnlock(
            baseCampManager != null ? baseCampManager.CommandCenter : null,
            facilityId);
        if (unlock != null && !string.IsNullOrWhiteSpace(unlock.displayName))
        {
            return unlock.displayName;
        }

        BaseCampBalanceConfig.FacilityDefinition definition = BaseCampBalanceConfig.Current?.GetFacility(facilityId);
        return definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : fallbackName;
    }

    private static void SetTitleVisibleByUnlock(TMP_Text target, CommandCenter commandCenter, string facilityId)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(commandCenter != null && commandCenter.IsFacilityUnlocked(facilityId));
    }
    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
    }

    private void EnsureExitButtonVisible()
    {
        if (exitButton != null && !exitButton.gameObject.activeSelf)
        {
            exitButton.gameObject.SetActive(true);
        }
    }


    private void CollectCredits()
    {
        CreditRefinery refinery = baseCampManager != null ? baseCampManager.CreditRefinery : null;
        int collectedCredits = refinery != null ? refinery.StoredCredits : 0;
        Vector3 sourcePosition = collectButton != null
            ? RewardFlyAnimator.GetRectWorldCenter(collectButton.transform as RectTransform)
            : Vector3.zero;

        baseCampManager?.CollectRefineryCredits();

        if (collectedCredits > 0)
        {
            // 기지 HUD의 Collection_Button에서도 수집 크레딧 흡수 연출을 재생한다.
            RewardFlyAnimator.Instance.PlayReward(sourcePosition, CurrencyType.Credits, collectedCredits);
        }

        RefreshNow();
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


    private static void SetButtonInteractable(Button target, bool interactable)
    {
        if (target != null)
        {
            target.interactable = interactable;
        }
    }

    private void RefreshBaseUnlockStatus()
    {
        CommandCenter commandCenter = baseCampManager != null ? baseCampManager.CommandCenter : null;
        SetUnlockStatusText(assemblyFactoryUnlockText, commandCenter, "assembly_factory", assemblyFactoryUnlockPanel);
        SetUnlockStatusText(coreChargerUnlockText, commandCenter, "core_charger", coreChargerUnlockPanel);
    }

    private void SetUnlockStatusText(
        TMP_Text target,
        CommandCenter commandCenter,
        string facilityId,
        GameObject unlockPanel = null)
    {
        if (target == null)
        {
            SetActive(unlockPanel, false);
            return;
        }

        CommandCenter.FacilityUnlock unlock = FindFacilityUnlock(commandCenter, facilityId);
        if (commandCenter == null || unlock == null)
        {
            target.text = string.Empty;
            SetActive(unlockPanel, false);
            return;
        }

        int requiredLevel = unlock.requiredLabLevel;
        bool unlocked = commandCenter.IsFacilityUnlocked(facilityId);

        target.text = unlocked ? string.Empty : $"<color=#ED3724>잠금</color>\n사령부 Lv.{requiredLevel} 필요";
        SetActive(unlockPanel, !unlocked);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private static CommandCenter.FacilityUnlock FindFacilityUnlock(CommandCenter commandCenter, string facilityId)
    {
        if (commandCenter == null)
        {
            return null;
        }

        foreach (CommandCenter.FacilityUnlock item in commandCenter.FacilityUnlocks)
        {
            if (item != null && item.facilityId == facilityId)
            {
                return item;
            }
        }

        return null;
    }
}

