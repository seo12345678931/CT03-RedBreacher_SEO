using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BaseCampManager : MonoBehaviour
{
    public static BaseCampManager Instance { get; private set; }

    [Header("Facilities")]
    [SerializeField] private CommandCenter commandCenter;
    [SerializeField] private CreditRefinery creditRefinery;
    [SerializeField] private AssemblyFactory assemblyFactory;
    [SerializeField] private CoreCharger coreCharger;
    [SerializeField] private SkillHangerFacility skillHanger;
    [SerializeField] private InventoryFacility inventory;
    [SerializeField] private DailyMissionManager dailyMissionManager;
    [SerializeField] private MainGuideMissionManager mainGuideMissionManager;
    [SerializeField] private bool autoFindFacilities = true;

    [Header("Facility Panels")]
    [SerializeField] private GameObject[] facilityPanels;
    [SerializeField] private bool closePanelsOnStart = true;
    [SerializeField] private bool animateFacilityPanels = true;

    [Header("Player State")]
    [SerializeField] private int commanderLevel = 1;
    [SerializeField] private int credits = 500;
    [SerializeField] private PlayerCurrencyWallet currencyWallet;
    [SerializeField] private PlayerProgression playerProgression;

    // 타이틀 프리워밍 등 외부에서 동일 키를 참조하기 위한 기본 통합 세이브 키.
    public const string DefaultUnifiedSaveKey = "Jinyou.SaveData";

    [Header("Unified Save")]
    [SerializeField] private bool useUnifiedSave = true;
    [SerializeField] private bool autoSaveUnifiedState = true;
    [SerializeField] private string unifiedSaveKey = DefaultUnifiedSaveKey;

    // 구키→계정키 이관을 기기당 최초 1회만 수행하기 위한 가드 키.
    private const string LegacyMigrationFlagKey = "Jinyou.SaveData.LegacyMigrated";

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel;
    [SerializeField] private Rect debugPanelRect = new Rect(16f, 16f, 280f, 220f);

    [Header("Events")]
    public UnityEvent<int> OnCreditsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCoreCrystalsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCommanderLevelChanged = new UnityEvent<int>();
    public UnityEvent<JinyouOfflineRewardSaveData> OnOfflineRewardsClaimed = new UnityEvent<JinyouOfflineRewardSaveData>();

    // 통합 세이브(클라우드 동기화 포함) 로드가 끝난 시점을 알린다.
    // 알림 배지 기준선처럼 "복원 완료 후" 잡아야 하는 초기화에 사용한다.
    public event System.Action UnifiedSaveLoaded;
    public bool IsUnifiedSaveLoaded { get; private set; }

    private PlayerCurrencyWallet registeredCurrencyWallet;
    private bool unifiedSaveReady;
    private bool isRestoringUnifiedSave;
    private int loadedUnifiedSaveVersion;
    private bool confirmPlayerPrefsReset;
    private bool skipNextBackupRotation;
    private bool quitFlushCompleted;
    // 초당 발생하는 크레딧 틱 등 잦은 저장 이벤트를 디스크 플러시로 매번 처리하지 않도록
    // 더티 플래그로 코얼레스하고, Update에서 이 간격마다만 실제 저장한다(일시정지/종료 시 즉시 플러시).
    private const float UnifiedSaveDebounceSeconds = 3f;
    private bool unifiedSaveDirty;
    private float nextUnifiedSaveFlushTime;
    private JinyouOfflineRewardSaveData lastOfflineReward = new JinyouOfflineRewardSaveData();

    public CommandCenter CommandCenter => commandCenter;
    public CreditRefinery CreditRefinery => creditRefinery;
    public AssemblyFactory AssemblyFactory => assemblyFactory;
    public CoreCharger CoreCharger => coreCharger;
    public SkillHangerFacility SkillHanger => ResolveSkillHanger();
    public InventoryFacility Inventory => ResolveInventory();
    public int CommanderLevel => ResolveCommanderLevel();
    public int Credits => CurrencyWallet.Credits;
    public int CoreCrystals => CurrencyWallet.CoreCrystals;
    public PlayerCurrencyWallet CurrencyWallet => EnsureCurrencyWallet();
    public PlayerProgression PlayerProgression => ResolvePlayerProgression();
    public JinyouOfflineRewardSaveData LastOfflineReward => lastOfflineReward;
    // 로그인 시 계정(UID)별 키로 분리한다(baseKey_UID). 비로그인/Auth 미준비 시 기본 키 그대로.
    // 이렇게 해야 한 기기에서 여러 계정이 같은 로컬 세이브를 공유하지 않는다.
    private string ResolvedSaveKey
    {
        get
        {
            FirebaseAuthManager auth = FirebaseAuthManager.Instance;
            return auth != null ? auth.ScopedSaveKey(unifiedSaveKey) : unifiedSaveKey;
        }
    }

    private string UnifiedSaveBackupKey => $"{ResolvedSaveKey}.Backup";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCurrencyWallet();
        ConnectFacilities();
        EnsureDailyMissionManager();
        EnsureMainGuideMissionManager();

        // 종료 직전 마지막 저장의 클라우드 업로드가 완료될 때까지 잠깐(최대 2초) 종료를 보류한다.
        // (OnApplicationQuit의 fire-and-forget 업로드는 프로세스 종료로 유실될 수 있음)
        Application.wantsToQuit += HandleWantsToQuit;
    }

    private async void Start()
    {
        ConnectFacilities();
        // 로컬 로드 전에 클라우드와 동기화(로그인 상태에서만). 비로그인/오프라인이면 즉시 통과.
        await SyncFromCloudBeforeLoad();
        if (this == null)
        {
            return; // 동기화 대기 중 씬/오브젝트가 파괴되었으면 중단.
        }

        LoadUnifiedGame();
        if (loadedUnifiedSaveVersion < 3)
        {
            ApplyEquippedLoadoutAtBoot();
        }
        ConfigureUnifiedPersistence();

        // 신규 계정(통합 세이브 없음) 첫 부팅: UID 스코프가 없는 전역 키에서 Awake 때 읽힌
        // 이전 계정의 Myth 진행도(레벨/스탯/라운드)가 첫 저장으로 굳어지기 전에 기본값으로 되돌린다.
        if (useUnifiedSave && loadedUnifiedSaveVersion <= 0 && HasLegacyMythGlobalKeys())
        {
            ResetMythProgressionForFreshAccount();
        }

        // 계정 전환/신규 계정 대응: 부트스트랩은 로그인 전(다른 계정 키)에 판단하므로,
        // 동기화가 끝난 지금 현재 계정 키 기준으로 미완료 튜토리얼이 있으면 매니저를 생성한다.
        TutorialManager.EnsureBootstrappedForCurrentAccount();

        SubscribeUnifiedSaveEvents();
        unifiedSaveReady = true;
        SaveUnifiedGame();
        ClearLegacyStandaloneData();
        if (loadedUnifiedSaveVersion >= 3)
        {
            ApplyEquippedLoadoutAtBoot();
        }

        // 클라우드 동기화 + 로컬 복원이 모두 끝난 시점. 이후 구독자들이 안전하게 초기 상태를 잡는다.
        IsUnifiedSaveLoaded = true;
        UnifiedSaveLoaded?.Invoke();

        if (closePanelsOnStart)
        {
            CloseAllPanels();
        }
    }

    private void Update()
    {
        TickInactiveFacilities(Time.deltaTime);
        FlushUnifiedSaveIfDue();
    }

    // 디바운스된 더티 저장을 간격이 지난 시점에 한 번 실제 저장으로 밀어낸다.
    private void FlushUnifiedSaveIfDue()
    {
        if (!unifiedSaveDirty || Time.unscaledTime < nextUnifiedSaveFlushTime)
        {
            return;
        }

        FlushUnifiedSaveNow();
    }

    // 대기 중인 더티 저장을 즉시 반영한다(일시정지/종료 등 Update가 멈추는 경로 포함).
    private void FlushUnifiedSaveNow()
    {
        unifiedSaveDirty = false;
        SaveUnifiedGame();
    }

    // 저장된 장착 무기/드론을 부팅 시 적용한다. 로드아웃 패널이 닫힌 팝업 안에 있어
    // 패널을 한 번 열기 전까지 적용되지 않던 문제를 막는다(현재 유닛은 CoreCharger가 복원).
    private void ApplyEquippedLoadoutAtBoot()
    {
        PlayerLoadoutSelectionPanel loadout =
            FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        if (loadout != null)
        {
            loadout.ApplySavedLoadout();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // 클라우드 동기화/복원이 끝나기 전(unifiedSaveReady=false)에는 모든 시스템이 기본값 상태다.
        // 이때 저장하면 기본값이 진짜 세이브를 덮어쓰고 최신 타임스탬프로 클라우드까지 오염시키므로 스킵한다.
        if (pauseStatus && unifiedSaveReady)
        {
            FlushUnifiedSaveNow(); // 대기 중인 더티 저장까지 즉시 반영.
            CloudSaveService.Instance.FlushNow(); // 일시정지 중엔 Update가 멈추므로 즉시 업로드.
        }
    }

    private void OnApplicationQuit()
    {
        if (!unifiedSaveReady)
        {
            return; // 복원 전 종료: 기본값으로 세이브를 덮어쓰지 않는다.
        }

        FlushUnifiedSaveNow();
        CloudSaveService.Instance.FlushNow();
    }

    // 종료 요청 시 마지막 상태를 저장하고, 업로드가 남아 있으면 완료(최대 2초)까지 종료를 보류한 뒤 다시 종료한다.
    private bool HandleWantsToQuit()
    {
        if (quitFlushCompleted || !unifiedSaveReady)
        {
            return true; // 이미 플러시했거나(재진입), 복원 전이라 저장할 것이 없다.
        }

        FlushUnifiedSaveNow(); // 마지막 상태 캡처(RequestPush로 업로드 예약).
        if (!CloudSaveService.Instance.HasPendingUploads)
        {
            return true;
        }

        FlushThenQuitAsync();
        return false;
    }

    private async void FlushThenQuitAsync()
    {
        try
        {
            System.Threading.Tasks.Task timeout = System.Threading.Tasks.Task.Delay(2000);
            // 진행 중 푸시가 있으면 대기 키가 남을 수 있어, 시간 안에서 반복 플러시한다.
            while (CloudSaveService.Instance.HasPendingUploads && !timeout.IsCompleted)
            {
                await System.Threading.Tasks.Task.WhenAny(
                    CloudSaveService.Instance.FlushNowAsync(), timeout);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Cloud] 종료 전 업로드 실패(로컬 저장은 완료됨): {exception.Message}");
        }

        quitFlushCompleted = true;
        Application.Quit();
    }

    // 로그인 상태면 클라우드 세이브를 내려받아 로컬과 최신본을 맞춘다. 실패해도 로컬로 계속 진행.
    private async System.Threading.Tasks.Task SyncFromCloudBeforeLoad()
    {
        if (!useUnifiedSave || string.IsNullOrWhiteSpace(unifiedSaveKey))
        {
            return;
        }

        MigrateLegacySaveKeyIfNeeded();

        try
        {
            await CloudSaveService.Instance.EnsureSyncedAsync(ResolvedSaveKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Cloud] 동기화 실패(로컬로 진행): {exception.Message}", this);
        }
    }

    // 구버전(스코프 없는 단일 키)에서 계정별 키로 1회만 이관한다.
    // 업데이트 전 단일 사용자 기기의 로컬 진행도가 첫 로그인 시 유실되지 않게 한다.
    // 최초 로그인 부팅에서 한 번 실행한 뒤 플래그로 잠가, 이후 게스트/타계정 데이터가 다시 섞이지 않게 한다.
    private void MigrateLegacySaveKeyIfNeeded()
    {
        if (PlayerPrefs.HasKey(LegacyMigrationFlagKey))
        {
            return; // 이미 1회 이관 완료.
        }

        string scopedKey = ResolvedSaveKey;
        if (scopedKey == unifiedSaveKey)
        {
            return; // 비로그인 상태: 스코프 키 == 기본 키. 로그인 후 다시 시도(플래그 미설정).
        }

        // 계정 데이터가 아직 없고, 옮길 구 데이터가 있을 때만 복사한다.
        if (!PlayerPrefs.HasKey(scopedKey) && PlayerPrefs.HasKey(unifiedSaveKey))
        {
            PlayerPrefs.SetString(scopedKey, PlayerPrefs.GetString(unifiedSaveKey, string.Empty));

            string legacyBackupKey = $"{unifiedSaveKey}.Backup";
            if (PlayerPrefs.HasKey(legacyBackupKey))
            {
                PlayerPrefs.SetString($"{scopedKey}.Backup", PlayerPrefs.GetString(legacyBackupKey, string.Empty));
            }

            PlayerPrefs.DeleteKey(unifiedSaveKey);
            PlayerPrefs.DeleteKey(legacyBackupKey);
            Debug.Log($"[Cloud] 구 세이브 키를 계정 키로 이관: {unifiedSaveKey} → {scopedKey}");
        }

        PlayerPrefs.SetString(LegacyMigrationFlagKey, "1");
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= HandleWantsToQuit;

        if (registeredCurrencyWallet != null)
        {
            registeredCurrencyWallet.OnCreditsChanged.RemoveListener(HandleCreditsChanged);
            registeredCurrencyWallet.OnCoreCrystalsChanged.RemoveListener(HandleCoreCrystalsChanged);
            registeredCurrencyWallet = null;
        }

        UnsubscribeUnifiedSaveEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ContextMenu("Connect Facilities")]
    public void ConnectFacilities()
    {
        if (!autoFindFacilities)
        {
            return;
        }

        commandCenter ??= FindFirstObjectByType<CommandCenter>(FindObjectsInactive.Include);
        creditRefinery ??= FindFirstObjectByType<CreditRefinery>(FindObjectsInactive.Include);
        assemblyFactory ??= FindFirstObjectByType<AssemblyFactory>(FindObjectsInactive.Include);
        coreCharger ??= FindFirstObjectByType<CoreCharger>(FindObjectsInactive.Include);
        ResolveSkillHanger();
        inventory ??= InventoryFacility.FindAny();
    }

    public void CollectRefineryCredits()
    {
        if (creditRefinery != null)
        {
            int collectedCredits = creditRefinery.CollectCredits();
            AddCredits(collectedCredits);
            DailyMissionManager.ReportCreditsCollected(collectedCredits);
            MainGuideMissionManager.ReportCreditsCollected(collectedCredits);
        }
    }

    public void UpgradeResearchLab()
    {
        TrySpendAndUpgrade(commandCenter);
    }

    public void UpgradeEnergyRefinery()
    {
        TrySpendAndUpgrade(creditRefinery);
    }

    public void UpgradeAssemblyFactory()
    {
        TrySpendAndUpgrade(assemblyFactory);
    }

    public void UpgradeCoreCharger()
    {
        TrySpendAndUpgrade(coreCharger);
    }

    public void UpgradeSkillHanger()
    {
        TrySpendAndUpgrade(skillHanger);
    }

    public void SelectAssemblyMenu(string menuId)
    {
        assemblyFactory?.TrySelectMenu(menuId);
    }

    public void SelectAssemblyWeapon(int weaponIndex)
    {
        if (assemblyFactory != null && assemblyFactory.TrySelectWeapon(weaponIndex))
        {
            SaveUnifiedGameIfReady();
        }
    }

    public void SelectAssemblyWeapon(ProjectileConfig weaponConfig)
    {
        if (assemblyFactory != null && assemblyFactory.TrySelectWeapon(weaponConfig))
        {
            SaveUnifiedGameIfReady();
        }
    }

    public void SelectAssemblyDrone(DroneConfig droneConfig)
    {
        if (assemblyFactory != null && assemblyFactory.TrySelectDrone(droneConfig))
        {
            SaveUnifiedGameIfReady();
        }
    }

    public void EnhanceAssemblyWeapon()
    {
        if (assemblyFactory == null)
        {
            return;
        }

        int availableCredits = Credits;
        if (assemblyFactory.TryEnhanceSelectedWeapon(ref availableCredits))
        {
            SetCreditsForFacility(availableCredits);
            DailyMissionManager.ReportWeaponEnhanced();
            MainGuideMissionManager.ReportWeaponEnhanced();
        }
    }

    public void EnhanceAssemblyDrone()
    {
        if (assemblyFactory == null)
        {
            return;
        }

        int availableCredits = Credits;
        if (assemblyFactory.TryEnhanceSelectedDrone(ref availableCredits))
        {
            SetCreditsForFacility(availableCredits);
            DailyMissionManager.ReportDroneEnhanced();
            MainGuideMissionManager.ReportDroneEnhanced();
            SaveUnifiedGameIfReady();
        }
    }

    public void EnhanceCoreUnit()
    {
        ConvertSelectedCoreUnit();
    }

    public void ConvertSelectedCoreUnit()
    {
        if (coreCharger == null)
        {
            return;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        int playerLevel = PlayerProgression != null ? PlayerProgression.Level : commanderLevel;
        if (coreCharger.TryConvertCurrentUnit(Inventory, player, playerLevel, CurrencyWallet))
        {
            DailyMissionManager.ReportUnitEnhanced();
            MainGuideMissionManager.ReportUnitEnhanced();
            SaveUnifiedGameIfReady();
        }
    }

    public void UseBossTicket()
    {
        commandCenter?.TryUseBossTicket();
    }

    public void OpenPanel(GameObject panel)
    {
        CloseAllPanels();

        if (panel != null)
        {
            OpenFacilityPanel(panel);
        }
    }

    public void CloseAllPanels()
    {
        foreach (GameObject panel in facilityPanels)
        {
            if (panel != null)
            {
                // 다른 패널은 즉시 닫는다. 트윈 정리/상태 복원은 PanelTweenTransition.OnDisable이 처리한다.
                panel.SetActive(false);
            }
        }
    }

    public void ClosePanel(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        if (!animateFacilityPanels || !panel.activeInHierarchy)
        {
            panel.SetActive(false);
            return;
        }

        // 닫기 연출 후 비활성화까지 PanelTweenTransition이 담당한다.
        PanelTweenTransition.EnsureOn(panel).Close();
    }

    private void OpenFacilityPanel(GameObject panel)
    {
        if (animateFacilityPanels)
        {
            // 비활성 상태에서 컴포넌트를 붙여두면 SetActive 시 OnEnable이 열기 연출을 재생한다.
            // 패널 트랜지션·ExitButton 바인딩의 단일 출처는 PanelTweenTransition이다.
            PanelTweenTransition.EnsureOn(panel);
        }

        panel.SetActive(true);
    }

    public void AddCredits(int amount)
    {
        CurrencyWallet.AddCredits(amount);
    }

    public void AddCoreCrystals(int amount)
    {
        CurrencyWallet.AddCoreCrystals(amount);
    }

    public void AddCommanderLevel(int amount)
    {
        SetCommanderLevel(CommanderLevel + Mathf.Max(0, amount));
    }

    public void SetCreditsForFacility(int value)
    {
        SetCredits(value);
    }

    public void SetCommanderLevel(int value)
    {
        commanderLevel = Mathf.Max(1, value);
        OnCommanderLevelChanged.Invoke(CommanderLevel);
        SaveUnifiedGameIfReady();
    }

    [ContextMenu("Save Unified Game")]
    public void SaveUnifiedGame()
    {
        if (!useUnifiedSave
            || isRestoringUnifiedSave
            || string.IsNullOrWhiteSpace(unifiedSaveKey))
        {
            return;
        }

        // 준비된 시설 상태를 하나의 JSON으로 저장해 시스템 간 시점을 맞춘다.
        string key = ResolvedSaveKey;
        JinyouSaveData data = CaptureUnifiedSaveData();
        if (!skipNextBackupRotation && PlayerPrefs.HasKey(key))
        {
            string previousJson = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrWhiteSpace(previousJson))
            {
                PlayerPrefs.SetString(UnifiedSaveBackupKey, previousJson);
            }
        }

        skipNextBackupRotation = false;
        PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        CloudSaveService.Instance.RequestPush(key); // 디바운스 후 클라우드 업로드(비로그인 시 no-op).
    }

    [ContextMenu("Load Unified Game")]
    public void LoadUnifiedGame()
    {
        string key = ResolvedSaveKey;
        if (!useUnifiedSave
            || string.IsNullOrWhiteSpace(unifiedSaveKey)
            || !PlayerPrefs.HasKey(key))
        {
            return;
        }

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            JinyouSaveData data = JsonUtility.FromJson<JinyouSaveData>(json);
            loadedUnifiedSaveVersion = data != null ? data.version : 0;
            RestoreUnifiedSaveData(data);
        }
        catch (Exception exception)
        {
            // JSON 파싱 실패(ArgumentException)뿐 아니라 복원 도중의 NRE 등 모든 예외를 여기서 봉인한다.
            // 그러지 않으면 async void Start()로 예외가 전파돼 이후 저장 구독/unifiedSaveReady 활성화가
            // 통째로 스킵되고, 그 세션 동안 자동 저장·클라우드 푸시가 전부 죽는다.
            Debug.LogWarning($"통합 저장 데이터를 읽지 못했습니다: {exception.Message}", this);
            string backupJson = PlayerPrefs.GetString(UnifiedSaveBackupKey, string.Empty);
            if (string.IsNullOrWhiteSpace(backupJson))
            {
                return;
            }

            try
            {
                JinyouSaveData backupData = JsonUtility.FromJson<JinyouSaveData>(backupJson);
                loadedUnifiedSaveVersion = backupData != null ? backupData.version : 0;
                RestoreUnifiedSaveData(backupData);
                skipNextBackupRotation = true;
                Debug.LogWarning("통합 저장 데이터가 손상되어 백업 데이터로 복구했습니다.", this);
            }
            catch (Exception backupException)
            {
                Debug.LogWarning($"통합 백업 데이터도 읽지 못했습니다: {backupException.Message}", this);
            }
        }
    }

    [ContextMenu("Delete Unified Save")]
    public void DeleteUnifiedSave()
    {
        if (string.IsNullOrWhiteSpace(unifiedSaveKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(ResolvedSaveKey);
        PlayerPrefs.DeleteKey(UnifiedSaveBackupKey);
        PlayerPrefs.Save();
    }

    private void SetCredits(int value)
    {
        CurrencyWallet.SetCredits(value);
    }

    private void TickInactiveFacilities(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (commandCenter != null && !commandCenter.isActiveAndEnabled)
        {
            // Base Popup이 꺼져 시설 Update가 멈춰도 기지 시간은 계속 흐르게 한다.
            commandCenter.AdvanceUpgradeOffline(deltaTime);
            commandCenter.ProduceBossTicketsOffline(deltaTime);
        }

        if (creditRefinery != null && !creditRefinery.isActiveAndEnabled)
        {
            // 정제소 UI 패널이 꺼져도 저장 생산/업그레이드 타이머는 계속 진행한다.
            creditRefinery.Produce(deltaTime);
            creditRefinery.AdvanceUpgradeOffline(deltaTime);
        }

        if (assemblyFactory != null && !assemblyFactory.isActiveAndEnabled)
        {
            assemblyFactory.AdvanceUpgradeOffline(deltaTime);
        }

        if (coreCharger != null && !coreCharger.isActiveAndEnabled)
        {
            coreCharger.AdvanceUpgradeOffline(deltaTime);
        }

        if (skillHanger != null && !skillHanger.isActiveAndEnabled)
        {
            skillHanger.AdvanceUpgradeOffline(deltaTime);
        }
    }

    private void TrySpendAndUpgrade(IBaseCampFacility facility)
    {
        if (facility == null)
        {
            return;
        }

        int availableCredits = Credits;
        int effectiveCommanderLevel = CommanderLevel;
        int researchLabLevel = commandCenter != null ? commandCenter.Level : 1;
        int upgradeCost = facility.UpgradeCost;

        if (!facility.CanStartUpgrade(availableCredits, effectiveCommanderLevel, researchLabLevel))
        {
            return;
        }

        // 시설은 타이머만 시작하고, 실제 재화 차감은 wallet 한 곳에서 처리한다.
        int simulatedCredits = availableCredits;
        if (CurrencyWallet.CanSpend(CurrencyType.Credits, upgradeCost)
            && facility.TryStartUpgrade(ref simulatedCredits, effectiveCommanderLevel, researchLabLevel))
        {
            CurrencyWallet.TrySpend(CurrencyType.Credits, upgradeCost);
            DailyMissionManager.ReportFacilityUpgraded();
            MainGuideMissionManager.ReportFacilityUpgraded();
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugPanel)
        {
            debugPanelRect.height = Mathf.Max(debugPanelRect.height, 260f);
            debugPanelRect = GUILayout.Window(GetInstanceID(), debugPanelRect, DrawDebugPanel, "Base Camp");
        }
#endif
    }

    private void DrawDebugPanel(int windowId)
    {
        GUILayout.Label($"Commander Lv. {CommanderLevel}");
        GUILayout.Label($"Credits: {Credits}");
        GUILayout.Label($"Core Crystals: {CoreCrystals}");

        if (GUILayout.Button("+ Level")) AddCommanderLevel(1);
        if (GUILayout.Button("+ 1000 Credits")) AddCredits(1000);
        if (GUILayout.Button("+ 100 Core Crystals")) AddCoreCrystals(100);
        if (GUILayout.Button("Collect Refinery")) CollectRefineryCredits();
        if (GUILayout.Button("Upgrade Research")) UpgradeResearchLab();
        if (GUILayout.Button("Upgrade Refinery")) UpgradeEnergyRefinery();
        if (GUILayout.Button("Upgrade Assembly")) UpgradeAssemblyFactory();
        if (GUILayout.Button("Upgrade Core")) UpgradeCoreCharger();
        if (GUILayout.Button("Reset Level/Stage")) ResetLevelAndStageDebug();
        if (GUILayout.Button(confirmPlayerPrefsReset
                ? "Confirm Reset All Save"
                : "Reset All PlayerPrefs"))
        {
            if (confirmPlayerPrefsReset)
            {
                ResetAllPlayerPrefsDebug();
            }
            else
            {
                confirmPlayerPrefsReset = true;
            }
        }

        GUI.DragWindow();
    }

    private void ResetLevelAndStageDebug()
    {
        ResetPlayerProgressionDebug();
        ResetStageProgressDebug();
    }

    private void ResetPlayerProgressionDebug()
    {
        PlayerProgression progression = FindFirstObjectByType<PlayerProgression>();
        if (progression != null)
        {
            progression.ResetProgression();
        }

        PlayerStatAllocator statAllocator = FindFirstObjectByType<PlayerStatAllocator>();
        if (statAllocator != null)
        {
            statAllocator.ResetAllocations();
        }
    }

    private void ResetStageProgressDebug()
    {
        EnemySpawnManager spawnManager = FindFirstObjectByType<EnemySpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.ResetStageProgress();
        }
    }

    public void ConfirmResetAllSave()
    {
        ResetAllPlayerPrefsDebug();
    }

    [ContextMenu("Debug/Reset All PlayerPrefs")]
    private void ResetAllPlayerPrefsDebug()
    {
        // 모든 저장 키를 제거한 뒤 씬을 다시 로드해 Inspector 초기값으로 테스트한다.
        unifiedSaveReady = false;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private PlayerCurrencyWallet EnsureCurrencyWallet()
    {
        if (currencyWallet != null)
        {
            RegisterCurrencyWalletEvents();
            return currencyWallet;
        }

        currencyWallet = GetComponent<PlayerCurrencyWallet>();
        if (currencyWallet == null)
        {
            currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>();
        }

        if (currencyWallet == null)
        {
            currencyWallet = gameObject.AddComponent<PlayerCurrencyWallet>();
        }

        RegisterCurrencyWalletEvents();
        return currencyWallet;
    }

    private PlayerProgression ResolvePlayerProgression()
    {
        if (playerProgression != null)
        {
            return playerProgression;
        }

        playerProgression = FindFirstObjectByType<PlayerProgression>();
        return playerProgression;
    }

    private SkillHangerFacility ResolveSkillHanger()
    {
        if (skillHanger != null)
        {
            return skillHanger;
        }

        skillHanger = FindFirstObjectByType<SkillHangerFacility>(FindObjectsInactive.Include);
        // 프리팹에 독립 시설 컴포넌트가 아직 없으면 저장/업그레이드용 런타임 시설을 생성한다.
        skillHanger ??= gameObject.GetComponent<SkillHangerFacility>() ?? gameObject.AddComponent<SkillHangerFacility>();
        return skillHanger;
    }

    private int ResolveCommanderLevel()
    {
        PlayerProgression progression = ResolvePlayerProgression();
        if (progression != null)
        {
            // 지휘관 레벨은 전투 플레이어 레벨을 기준으로 시설 해금/업그레이드 조건에 사용한다.
            return Mathf.Max(1, progression.Level);
        }

        return Mathf.Max(1, commanderLevel);
    }

    private InventoryFacility ResolveInventory()
    {
        if (inventory != null)
        {
            return inventory;
        }

        inventory = InventoryFacility.FindAny();
        return inventory;
    }

    private DailyMissionManager EnsureDailyMissionManager()
    {
        dailyMissionManager ??= DailyMissionManager.Instance
            ?? FindFirstObjectByType<DailyMissionManager>();
        if (dailyMissionManager == null)
        {
            // 일일 미션이 없는 전투 씬에서도 동일한 저장 데이터를 유지한다.
            dailyMissionManager = gameObject.AddComponent<DailyMissionManager>();
        }

        return dailyMissionManager;
    }

    private MainGuideMissionManager EnsureMainGuideMissionManager()
    {
        mainGuideMissionManager ??= MainGuideMissionManager.Instance
            ?? FindFirstObjectByType<MainGuideMissionManager>();
        if (mainGuideMissionManager == null)
        {
            // 가이드 미션이 없는 전투 씬에서도 동일한 저장 데이터를 유지한다.
            mainGuideMissionManager = gameObject.AddComponent<MainGuideMissionManager>();
        }

        return mainGuideMissionManager;
    }

    private void RegisterCurrencyWalletEvents()
    {
        if (currencyWallet == null)
        {
            return;
        }

        if (registeredCurrencyWallet == currencyWallet)
        {
            return;
        }

        if (registeredCurrencyWallet != null)
        {
            registeredCurrencyWallet.OnCreditsChanged.RemoveListener(HandleCreditsChanged);
            registeredCurrencyWallet.OnCoreCrystalsChanged.RemoveListener(HandleCoreCrystalsChanged);
        }

        currencyWallet.OnCreditsChanged.RemoveListener(HandleCreditsChanged);
        currencyWallet.OnCoreCrystalsChanged.RemoveListener(HandleCoreCrystalsChanged);
        currencyWallet.OnCreditsChanged.AddListener(HandleCreditsChanged);
        currencyWallet.OnCoreCrystalsChanged.AddListener(HandleCoreCrystalsChanged);
        registeredCurrencyWallet = currencyWallet;
        HandleCreditsChanged(currencyWallet.Credits);
        HandleCoreCrystalsChanged(currencyWallet.CoreCrystals);
    }

    private void ConfigureUnifiedPersistence()
    {
        if (!useUnifiedSave)
        {
            return;
        }

        // 개별 저장을 통합 저장으로 한 번 마이그레이션한 뒤 중복 키를 제거한다.
        EnsureCurrencyWallet().SetStandaloneSaveEnabled(false, false);
        assemblyFactory?.SetStandaloneSaveEnabled(false, false);
        EnsureDailyMissionManager().SetStandaloneSaveEnabled(false, false);
        EnsureMainGuideMissionManager().SetStandaloneSaveEnabled(false, false);
        Inventory?.SetStandaloneSaveEnabled(false, false);

        PlayerLoadoutSelectionPanel playerLoadout =
            FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        playerLoadout?.SetStandaloneSaveEnabled(false, false);

        PlayerEquipmentPartLoadout equipmentLoadout =
            FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
        equipmentLoadout?.SetStandaloneSaveEnabled(false, false);
        FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include)
            ?.SetStandaloneLoadoutSaveEnabled(false, false);

        AchievementManager achievementManager = AchievementManager.Instance
            ?? FindFirstObjectByType<AchievementManager>();
        achievementManager?.SetStandaloneSaveEnabled(false, false);

        // Myth 진행도(레벨/스탯/라운드)는 키가 UID 스코프가 아니라, 개별 저장을 켜두면
        // 계정 전환 시 이전 계정의 진행도가 새 계정으로 샌다. 통합 세이브로 일원화한다.
        FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, false);
        FindFirstObjectByType<PlayerStatAllocator>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, false);
        FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, false);
    }

    public void RequestUnifiedSave()
    {
        SaveUnifiedGameIfReady();
    }

    // 계정 전환 누수 대상인 전역 Myth 키가 기기에 남아 있는지 확인한다.
    // (SetStandaloneSaveEnabled(false, true) 정리가 한 번 돌면 사라져 이후엔 false)
    private static bool HasLegacyMythGlobalKeys()
    {
        return PlayerPrefs.HasKey("PlayerProgression.Level")
            || PlayerPrefs.HasKey("PlayerStatAllocator.AttackLevel")
            || PlayerPrefs.HasKey("EnemySpawnManager.CurrentRound");
    }

    private void ResetMythProgressionForFreshAccount()
    {
        FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include)?.ResetProgression();
        FindFirstObjectByType<PlayerStatAllocator>(FindObjectsInactive.Include)?.ResetAllocations();
        FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include)?.ResetStageProgress();
        Debug.Log("[Save] 신규 계정: 전역 키에 남아 있던 이전 계정의 Myth 진행도를 기본값으로 초기화했습니다.", this);
    }

    private void ClearLegacyStandaloneData()
    {
        if (!useUnifiedSave || !PlayerPrefs.HasKey(ResolvedSaveKey))
        {
            return;
        }

        EnsureCurrencyWallet().SetStandaloneSaveEnabled(false, true);
        assemblyFactory?.SetStandaloneSaveEnabled(false, true);
        EnsureDailyMissionManager().SetStandaloneSaveEnabled(false, true);
        EnsureMainGuideMissionManager().SetStandaloneSaveEnabled(false, true);
        Inventory?.SetStandaloneSaveEnabled(false, true);

        PlayerLoadoutSelectionPanel playerLoadout =
            FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        playerLoadout?.SetStandaloneSaveEnabled(false, true);

        PlayerEquipmentPartLoadout equipmentLoadout =
            FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
        equipmentLoadout?.SetStandaloneSaveEnabled(false, true);
        FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include)
            ?.SetStandaloneLoadoutSaveEnabled(false, true);

        AchievementManager achievementManager = AchievementManager.Instance
            ?? FindFirstObjectByType<AchievementManager>();
        achievementManager?.SetStandaloneSaveEnabled(false, true);

        // Myth 진행도 전역 키도 통합 세이브 확정 후 제거해 계정 간 오염을 차단한다.
        // (현재 메모리 상태는 이미 위의 SaveUnifiedGame으로 통합 세이브에 반영됨)
        FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, true);
        FindFirstObjectByType<PlayerStatAllocator>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, true);
        FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include)
            ?.SetStandaloneSaveEnabled(false, true);
    }

    private void HandleCreditsChanged(int value)
    {
        // 기존 기지 UI 이벤트를 유지하면서 실제 값은 wallet이 관리한다.
        credits = value;
        OnCreditsChanged.Invoke(value);
        SaveUnifiedGameIfReady();
    }

    private void HandleCoreCrystalsChanged(int value)
    {
        OnCoreCrystalsChanged.Invoke(value);
        SaveUnifiedGameIfReady();
    }

    private JinyouSaveData CaptureUnifiedSaveData()
    {
        PlayerCurrencyWallet wallet = EnsureCurrencyWallet();
        AchievementManager achievementManager = AchievementManager.Instance
            ?? FindFirstObjectByType<AchievementManager>();
        DailyMissionManager resolvedDailyMissionManager = EnsureDailyMissionManager();
        MainGuideMissionManager resolvedGuideMissionManager = EnsureMainGuideMissionManager();
        PlayerLoadoutSelectionPanel playerLoadout =
            FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        PlayerEquipmentPartLoadout equipmentLoadout =
            FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        JinyouPlayerLoadoutSaveData playerLoadoutData = player != null
            ? player.CaptureLoadoutState()
            : playerLoadout != null
                ? playerLoadout.CaptureState()
                : new JinyouPlayerLoadoutSaveData();
        if (playerLoadout != null)
        {
            playerLoadoutData.droneId = playerLoadout.CaptureState().droneId;
        }

        return new JinyouSaveData
        {
            version = 4,
            lastSavedUnixTime = GetCurrentUnixTime(),
            commanderLevel = CommanderLevel,
            mainBuildingLevel = commandCenter != null ? commandCenter.Level : 1,
            credits = wallet != null ? wallet.Credits : credits,
            coreCrystals = wallet != null ? wallet.CoreCrystals : 0,
            lastOfflineReward = lastOfflineReward,
            researchLab = commandCenter != null ? commandCenter.CaptureState() : new JinyouCommandCenterSaveData(),
            energyRefinery = creditRefinery != null ? creditRefinery.CaptureState() : new JinyouEnergyRefinerySaveData(),
            assemblyFactory = assemblyFactory != null ? assemblyFactory.CaptureState() : new JinyouAssemblyFactorySaveData(),
            coreCharger = coreCharger != null ? coreCharger.CaptureState() : new JinyouCoreChargerSaveData(),
            skillHanger = skillHanger != null ? skillHanger.CaptureState() : new JinyouSkillHangerSaveData(),
            achievements = achievementManager != null ? achievementManager.CaptureState() : new JinyouAchievementSaveData(),
            dailyMissions = resolvedDailyMissionManager.CaptureState(),
            guideMissions = resolvedGuideMissionManager.CaptureState(),
            inventory = Inventory != null ? Inventory.CaptureState() : new JinyouInventorySaveData(),
            playerLoadout = playerLoadoutData,
            equipmentLoadout = equipmentLoadout != null
                ? equipmentLoadout.CaptureState()
                : new JinyouEquipmentLoadoutSaveData(),
            myth = CaptureMythSaveData()
        };
    }

    // Myth 전투 진행도(레벨/스탯/라운드)를 통합 세이브에 모은다. 컴포넌트가 없으면 captured=false로 남아 복원 시 건너뛴다.
    private JinyouMythSaveData CaptureMythSaveData()
    {
        JinyouMythSaveData myth = new JinyouMythSaveData();

        PlayerProgression progression =
            FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include);
        if (progression != null)
        {
            myth.progression = progression.CaptureSaveData();
        }

        PlayerStatAllocator statAllocator =
            FindFirstObjectByType<PlayerStatAllocator>(FindObjectsInactive.Include);
        if (statAllocator != null)
        {
            myth.statAllocator = statAllocator.CaptureSaveData();
        }

        EnemySpawnManager enemySpawn =
            FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
        if (enemySpawn != null)
        {
            myth.enemySpawn = enemySpawn.CaptureSaveData();
        }

        myth.tutorial = TutorialManager.CaptureSaveData(); // 인스턴스 없어도 PlayerPrefs에서 직접 캡처.

        return myth;
    }

    private void RestoreUnifiedSaveData(JinyouSaveData data)
    {
        if (data == null)
        {
            return;
        }

        isRestoringUnifiedSave = true;
        try
        {
            // 재화와 시설을 먼저 복원한 뒤 업적과 일일 미션을 복원한다.
            commanderLevel = Mathf.Max(1, data.commanderLevel);
            data.researchLab ??= new JinyouCommandCenterSaveData();
            data.researchLab.level = data.version >= 2 && data.mainBuildingLevel > 0
                ? data.mainBuildingLevel
                : Mathf.Max(1, data.researchLab.level);
            PlayerCurrencyWallet wallet = EnsureCurrencyWallet();
            wallet?.SetCredits(data.credits);
            wallet?.SetCoreCrystals(data.coreCrystals);
            commandCenter?.RestoreState(data.researchLab);
            creditRefinery?.RestoreState(data.energyRefinery);
            assemblyFactory?.RestoreState(data.assemblyFactory);
            coreCharger?.RestoreState(data.coreCharger);
            data.skillHanger ??= new JinyouSkillHangerSaveData();
            skillHanger?.RestoreState(data.skillHanger);
            coreCharger?.ApplyCompletedConversions(Inventory, FindFirstObjectByType<PlayerController>());
            AchievementManager achievementManager = AchievementManager.Instance
                ?? FindFirstObjectByType<AchievementManager>();
            achievementManager?.RestoreState(data.achievements);
            EnsureDailyMissionManager().RestoreState(data.dailyMissions);
            EnsureMainGuideMissionManager().RestoreState(data.guideMissions);

            if (data.version >= 3)
            {
                Inventory?.RestoreState(data.inventory);

                PlayerController player =
                    FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                player?.RestoreLoadoutState(data.playerLoadout);

                PlayerLoadoutSelectionPanel playerLoadout =
                    FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
                playerLoadout?.RestoreState(data.playerLoadout);

                PlayerEquipmentPartLoadout equipmentLoadout =
                    FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
                equipmentLoadout?.RestoreState(data.equipmentLoadout);
            }

            // Myth 전투 진행도는 version 4부터. 구버전 세이브는 건너뛰어 기존 로컬 진행도를 보존한다.
            if (data.version >= 4 && data.myth != null)
            {
                FindFirstObjectByType<PlayerProgression>(FindObjectsInactive.Include)
                    ?.RestoreSaveData(data.myth.progression);
                FindFirstObjectByType<PlayerStatAllocator>(FindObjectsInactive.Include)
                    ?.RestoreSaveData(data.myth.statAllocator);
                FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include)
                    ?.RestoreSaveData(data.myth.enemySpawn);
                TutorialManager.RestoreSaveData(data.myth.tutorial);
            }

            ApplyOfflineRewards(data.lastSavedUnixTime);
            OnCommanderLevelChanged.Invoke(CommanderLevel);
        }
        finally
        {
            isRestoringUnifiedSave = false;
        }
    }

    private void ApplyOfflineRewards(long lastSavedUnixTime)
    {
        lastOfflineReward = new JinyouOfflineRewardSaveData();
        if (lastSavedUnixTime <= 0 || commandCenter == null)
        {
            return;
        }

        float elapsedSeconds = Mathf.Max(0f, GetCurrentUnixTime() - lastSavedUnixTime);
        if (elapsedSeconds <= 0f)
        {
            return;
        }

        // 건설 시간은 전체 미접속 시간을 반영하고 생산 보상은 시설별 한도를 적용한다.
        commandCenter.AdvanceUpgradeOffline(elapsedSeconds);
        creditRefinery?.AdvanceUpgradeOffline(elapsedSeconds);
        assemblyFactory?.AdvanceUpgradeOffline(elapsedSeconds);
        coreCharger?.AdvanceUpgradeOffline(elapsedSeconds);
        skillHanger?.AdvanceUpgradeOffline(elapsedSeconds);

        float maxOfflineSeconds = Mathf.Max(0f, commandCenter.OfflineRewardLimitHours) * 3600f;
        float appliedSeconds = Mathf.Min(elapsedSeconds, maxOfflineSeconds);
        int storedCreditsBefore = creditRefinery != null ? creditRefinery.StoredCredits : 0;
        creditRefinery?.Produce(appliedSeconds);
        int storedCreditsAfter = creditRefinery != null ? creditRefinery.StoredCredits : storedCreditsBefore;
        float ticketOfflineSeconds = Mathf.Min(
            elapsedSeconds,
            Mathf.Max(0f, commandCenter.TicketOfflineLimitHours) * 3600f);

        lastOfflineReward = new JinyouOfflineRewardSaveData
        {
            elapsedSeconds = elapsedSeconds,
            appliedSeconds = appliedSeconds,
            refineryCreditsAdded = Mathf.Max(0, storedCreditsAfter - storedCreditsBefore),
            bossTicketsAdded = commandCenter.ProduceBossTicketsOffline(ticketOfflineSeconds)
        };

        if (lastOfflineReward.HasReward)
        {
            OnOfflineRewardsClaimed.Invoke(lastOfflineReward);
            DailyMissionManager.ReportOfflineRewardClaimed();
            MainGuideMissionManager.ReportOfflineRewardClaimed();
        }
    }

    private void SubscribeUnifiedSaveEvents()
    {
        OnCommanderLevelChanged.AddListener(HandleUnifiedSaveEvent);
        commandCenter?.OnLevelChanged.AddListener(HandleUnifiedSaveEvent);
        commandCenter?.OnBossTicketsChanged.AddListener(HandleUnifiedSaveEvent);
        creditRefinery?.OnLevelChanged.AddListener(HandleUnifiedSaveEvent);
        creditRefinery?.OnCreditsChanged.AddListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnLevelChanged.AddListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnMenuSelected.AddListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnMenuUnlocked.AddListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnWeaponEnhanced.AddListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnDroneEnhanced.AddListener(HandleUnifiedSaveEvent);
        coreCharger?.OnLevelChanged.AddListener(HandleUnifiedSaveEvent);
        coreCharger?.OnUnitEnhanced.AddListener(HandleUnifiedSaveEvent);
        skillHanger?.OnLevelChanged.AddListener(HandleUnifiedSaveEvent);
        skillHanger?.OnUpgradeCompleted.AddListener(HandleUnifiedSaveEvent);
        Inventory?.OnInventoryChanged.AddListener(HandleUnifiedSaveEvent);
        PlayerEquipmentPartLoadout equipmentLoadout =
            FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
        equipmentLoadout?.OnLoadoutChanged.AddListener(HandleUnifiedSaveEvent);
        AchievementManager achievementManager = AchievementManager.Instance
            ?? FindFirstObjectByType<AchievementManager>();
        achievementManager?.OnAchievementsChanged.AddListener(HandleUnifiedSaveEvent);
        achievementManager?.OnAchievementCompleted.AddListener(HandleUnifiedSaveEvent);

        DailyMissionManager resolvedDailyMissionManager = EnsureDailyMissionManager();
        resolvedDailyMissionManager.OnDailyMissionsChanged.AddListener(HandleUnifiedSaveEvent);
        resolvedDailyMissionManager.OnDailyMissionCompleted.AddListener(HandleUnifiedSaveEvent);
        resolvedDailyMissionManager.OnDailyMissionRewardClaimed.AddListener(HandleUnifiedSaveEvent);

        MainGuideMissionManager resolvedGuideMissionManager = EnsureMainGuideMissionManager();
        resolvedGuideMissionManager.OnGuideMissionsChanged.AddListener(HandleUnifiedSaveEvent);
        resolvedGuideMissionManager.OnGuideStepCompleted.AddListener(HandleUnifiedSaveEvent);
        resolvedGuideMissionManager.OnGuideStepClaimed.AddListener(HandleUnifiedSaveEvent);
    }

    private void UnsubscribeUnifiedSaveEvents()
    {
        OnCommanderLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        commandCenter?.OnLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        commandCenter?.OnBossTicketsChanged.RemoveListener(HandleUnifiedSaveEvent);
        creditRefinery?.OnLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        creditRefinery?.OnCreditsChanged.RemoveListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnMenuSelected.RemoveListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnMenuUnlocked.RemoveListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnWeaponEnhanced.RemoveListener(HandleUnifiedSaveEvent);
        assemblyFactory?.OnDroneEnhanced.RemoveListener(HandleUnifiedSaveEvent);
        coreCharger?.OnLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        coreCharger?.OnUnitEnhanced.RemoveListener(HandleUnifiedSaveEvent);
        skillHanger?.OnLevelChanged.RemoveListener(HandleUnifiedSaveEvent);
        skillHanger?.OnUpgradeCompleted.RemoveListener(HandleUnifiedSaveEvent);
        Inventory?.OnInventoryChanged.RemoveListener(HandleUnifiedSaveEvent);
        PlayerEquipmentPartLoadout equipmentLoadout =
            FindFirstObjectByType<PlayerEquipmentPartLoadout>(FindObjectsInactive.Include);
        equipmentLoadout?.OnLoadoutChanged.RemoveListener(HandleUnifiedSaveEvent);
        AchievementManager achievementManager = AchievementManager.Instance
            ?? FindFirstObjectByType<AchievementManager>();
        achievementManager?.OnAchievementsChanged.RemoveListener(HandleUnifiedSaveEvent);
        achievementManager?.OnAchievementCompleted.RemoveListener(HandleUnifiedSaveEvent);

        if (dailyMissionManager != null)
        {
            dailyMissionManager.OnDailyMissionsChanged.RemoveListener(HandleUnifiedSaveEvent);
            dailyMissionManager.OnDailyMissionCompleted.RemoveListener(HandleUnifiedSaveEvent);
            dailyMissionManager.OnDailyMissionRewardClaimed.RemoveListener(HandleUnifiedSaveEvent);
        }

        if (mainGuideMissionManager != null)
        {
            mainGuideMissionManager.OnGuideMissionsChanged.RemoveListener(HandleUnifiedSaveEvent);
            mainGuideMissionManager.OnGuideStepCompleted.RemoveListener(HandleUnifiedSaveEvent);
            mainGuideMissionManager.OnGuideStepClaimed.RemoveListener(HandleUnifiedSaveEvent);
        }
    }

    private void HandleUnifiedSaveEvent()
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(int value)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(string value)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(ProjectileConfig weaponConfig, int level)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(PlayerUnitConfig unitConfig, int level)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(DroneConfig droneConfig, int level)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(AchievementManager.AchievementEntry achievement)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(DailyMissionManager.DailyMissionEntry mission)
    {
        SaveUnifiedGameIfReady();
    }

    private void HandleUnifiedSaveEvent(GuideMissionConfig.GuideStepData guideStep)
    {
        SaveUnifiedGameIfReady();
    }

    private void SaveUnifiedGameIfReady()
    {
        if (!autoSaveUnifiedState || !unifiedSaveReady)
        {
            return;
        }

        // 즉시 저장하지 않고 더티로 표시만 한다. clean→dirty 전환 시에만 마감 시각을 잡아,
        // 크레딧처럼 연속으로 이벤트가 쏟아져도 마감 시각이 계속 밀려나지 않고 간격마다 한 번씩 저장된다.
        if (!unifiedSaveDirty)
        {
            unifiedSaveDirty = true;
            nextUnifiedSaveFlushTime = Time.unscaledTime + UnifiedSaveDebounceSeconds;
        }
    }

    private static long GetCurrentUnixTime()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

public enum CurrencyType
{
    Credits,
    CoreCrystals
}

public interface ICurrencyWallet
{
    int GetAmount(CurrencyType type);
    void Add(CurrencyType type, int amount);
    bool CanSpend(CurrencyType type, int amount);
    bool TrySpend(CurrencyType type, int amount);
}

public class PlayerCurrencyWallet : MonoBehaviour, ICurrencyWallet
{
    private const string CreditsKey = "PlayerCurrencyWallet.Credits";
    private const string CoreCrystalsKey = "PlayerCurrencyWallet.CoreCrystals";

    [Header("Currency")]
    [SerializeField] private int credits = 500;
    [SerializeField] private int coreCrystals;
    [SerializeField] private bool saveToPlayerPrefs = true;

    [Header("Events")]
    public UnityEvent<int> OnCreditsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCoreCrystalsChanged = new UnityEvent<int>();

    public int Credits => credits;
    public int CoreCrystals => coreCrystals;

    private void Awake()
    {
        Load();
    }

    public void AddCredits(int amount)
    {
        Add(CurrencyType.Credits, amount);
    }

    public bool TrySpendCredits(int amount)
    {
        return TrySpend(CurrencyType.Credits, amount);
    }

    public void AddCoreCrystals(int amount)
    {
        Add(CurrencyType.CoreCrystals, amount);
    }

    public bool TrySpendCoreCrystals(int amount)
    {
        return TrySpend(CurrencyType.CoreCrystals, amount);
    }

    public int GetAmount(CurrencyType type)
    {
        switch (type)
        {
            case CurrencyType.Credits:
                return credits;
            case CurrencyType.CoreCrystals:
                return coreCrystals;
            default:
                return 0;
        }
    }

    public void Add(CurrencyType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        switch (type)
        {
            case CurrencyType.Credits:
                SetCredits(credits + amount);
                break;
            case CurrencyType.CoreCrystals:
                SetCoreCrystals(coreCrystals + amount);
                break;
        }
    }

    public bool CanSpend(CurrencyType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        return GetAmount(type) >= amount;
    }

    public bool TrySpend(CurrencyType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        if (!CanSpend(type, amount))
        {
            return false;
        }

        // 재화 종류별 이벤트/저장은 기존 Set 메서드에 위임한다.
        switch (type)
        {
            case CurrencyType.Credits:
                SetCredits(credits - amount);
                break;
            case CurrencyType.CoreCrystals:
                SetCoreCrystals(coreCrystals - amount);
                break;
        }

        return true;
    }

    public void SetCredits(int value)
    {
        credits = Mathf.Max(0, value);
        Save();
        OnCreditsChanged.Invoke(credits);
    }

    public void SetCoreCrystals(int value)
    {
        coreCrystals = Mathf.Max(0, value);
        Save();
        OnCoreCrystalsChanged.Invoke(coreCrystals);
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(CreditsKey);
        PlayerPrefs.DeleteKey(CoreCrystalsKey);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        credits = Mathf.Max(0, PlayerPrefs.GetInt(CreditsKey, credits));
        coreCrystals = Mathf.Max(0, PlayerPrefs.GetInt(CoreCrystalsKey, coreCrystals));
        OnCreditsChanged.Invoke(credits);
        OnCoreCrystalsChanged.Invoke(coreCrystals);
    }

    private void Save()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        PlayerPrefs.SetInt(CreditsKey, credits);
        PlayerPrefs.SetInt(CoreCrystalsKey, coreCrystals);
        PlayerPrefs.Save();
    }
}

public interface IBaseCampFacility
{
    int Level { get; }
    int MaxLevel { get; }
    int UpgradeCost { get; }
    int RequiredCommanderLevel { get; }
    int RequiredResearchLabLevel { get; }
    bool IsUpgrading { get; }
    float UpgradeRemainingSeconds { get; }
    float CurrentUpgradeDurationSeconds { get; }
    bool CanUpgrade(int credits, int commanderLevel);
    int GetLevelLimit(int researchLabLevel);
    bool CanStartUpgrade(int credits, int commanderLevel, int researchLabLevel);
    bool TryStartUpgrade(ref int availableCredits, int commanderLevel, int researchLabLevel);
    void Upgrade();
}
