using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 최초 1회 인터랙티브 온보딩 튜토리얼을 구동한다.
/// 프리팹/씬 배선 없이 <see cref="MenuAlertController"/>처럼 자가 부트스트랩한다.
/// 스텝 정의는 <see cref="TutorialConfig"/>(Resources/Tutorial/TutorialConfig)에서 로드,
/// 진행도는 PlayerPrefs에 저장한다(완료 시 다시 뜨지 않음).
/// </summary>
[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

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
    private const string LegacyMigrationFlagKey = "Tutorial.LegacyMigrated";

    private static readonly string[] AllProgressKeys =
    {
        StepIndexKey, CompletedKey,
        BaseStepIndexKey, BaseCompletedKey,
        CoreChargerStepIndexKey, CoreChargerCompletedKey,
        InventoryStepIndexKey, InventoryCompletedKey,
        BossEncounterStepIndexKey, BossEncounterCompletedKey,
    };

    // 계정별 진행도 키(로그인 시 baseKey_UID, 비로그인 시 baseKey).
    // BaseCampManager.ResolvedSaveKey와 같은 규칙으로, 같은 기기에서 계정을 전환해도
    // 이전 계정의 튜토리얼 완료 상태가 새 계정에 새어 들어가지 않게 한다.
    private static string ScopedKey(string baseKey)
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        return auth != null ? auth.ScopedSaveKey(baseKey) : baseKey;
    }

    private static int GetSavedInt(string baseKey, int fallback)
    {
        return PlayerPrefs.GetInt(ScopedKey(baseKey), fallback);
    }

    private static void SetSavedInt(string baseKey, int value)
    {
        PlayerPrefs.SetInt(ScopedKey(baseKey), value);
    }

    // 구버전(계정 스코프 없는 키)의 진행도를 첫 로그인 시 계정 키로 1회만 이관한다.
    // 이후 게스트/타계정 데이터가 다시 섞이지 않도록 플래그로 잠근다(BaseCampManager와 동일 패턴).
    private static void MigrateLegacyKeysIfNeeded()
    {
        if (PlayerPrefs.HasKey(LegacyMigrationFlagKey))
        {
            return; // 이미 1회 이관 완료.
        }

        string probe = ScopedKey(CompletedKey);
        if (probe == CompletedKey)
        {
            return; // 비로그인 상태: 로그인 후 다시 시도(플래그 미설정).
        }

        bool hasScoped = false;
        bool hasLegacy = false;
        foreach (string key in AllProgressKeys)
        {
            hasScoped |= PlayerPrefs.HasKey(ScopedKey(key));
            hasLegacy |= PlayerPrefs.HasKey(key);
        }

        // 계정 데이터가 아직 없고, 옮길 구 데이터가 있을 때만 복사한다.
        if (!hasScoped && hasLegacy)
        {
            foreach (string key in AllProgressKeys)
            {
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.SetInt(ScopedKey(key), PlayerPrefs.GetInt(key, 0));
                    PlayerPrefs.DeleteKey(key);
                }
            }
        }

        // 이관 완료 플래그는 기기 전역(스코프 없음): 기기당 최초 로그인 1회만 이관한다.
        PlayerPrefs.SetInt(LegacyMigrationFlagKey, 1);
        PlayerPrefs.Save();
    }

    private const string BaseStepIdPrefix = "base_";
    private const string CoreChargerStepIdPrefix = "base_core_charger_";
    private const string InventoryStepIdPrefix = "inventory_";
    private const string BossEncounterStepIdPrefix = "boss_";
    private const string BasePopupName = "Base_Popup";
    private const string InventoryPopupName = "Inventory_Popup";
    private const string CoreChargerFacilityId = "core_charger";
    private const float ResolveInterval = 0.5f;
    // 이름 기반 씬 전체 스캔(FindInScene)의 최소 간격. 타겟을 못 찾는 동안 매 프레임
    // Resources.FindObjectsOfTypeAll 전체 힙 스캔이 돌던 것을 막는다(모바일 프레임 스파이크 방지).
    private const float SceneScanInterval = 0.25f;

    private TutorialConfig config;
    private TutorialOverlay overlay;

    private int stepIndex;
    private bool completed;
    private int baseStepIndex;
    private bool baseCompleted;
    private int coreChargerStepIndex;
    private bool coreChargerCompleted;
    private int inventoryStepIndex;
    private bool inventoryCompleted;
    private int bossEncounterStepIndex;
    private bool bossEncounterCompleted;
    private bool running;
    private bool runningBaseTutorial;
    private bool runningCoreChargerTutorial;
    private bool runningInventoryTutorial;
    private bool runningBossEncounterTutorial;

    private TutorialConfig.TutorialStep activeStep;
    private int eventProgress;
    private RectTransform resolvedTarget;
    private Button armedButton;
    private UnityEngine.Events.UnityAction armedHandler;

    private BaseCampManager subscribedCamp;
    private AssemblyFactory subscribedFactory;
    private CoreCharger subscribedCharger;
    private float nextResolveTime;
    private float nextSceneScanTime;
    private bool allowSceneScan;
    private bool missingOverlayPrefab;
    private bool bossTutorialPausedTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 로그인된 상태라면(에디터 직행 등) 구버전 키를 먼저 계정 키로 이관한 뒤 판정한다.
        MigrateLegacyKeysIfNeeded();

        // 이미 완료했거나 인스턴스가 있으면 생성하지 않는다.
        if (GetSavedInt(CompletedKey, 0) == 1
            && GetSavedInt(BaseCompletedKey, 0) == 1
            && GetSavedInt(CoreChargerCompletedKey, 0) == 1
            && GetSavedInt(InventoryCompletedKey, 0) == 1
            && GetSavedInt(BossEncounterCompletedKey, 0) == 1)
        {
            return;
        }

        if (FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject host = new GameObject(nameof(TutorialManager));
        host.AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Bootstrap은 최초 씬(타이틀) 로드 직후 1회만 실행되므로, 씬 전환에도 파괴되지 않게 유지한다.
        // 그러지 않으면 타이틀에서 생성된 매니저가 게임(기지) 씬 로드 시 사라져 튜토리얼이 영영 뜨지 않는다.
        DontDestroyOnLoad(gameObject);

        config = TutorialConfig.Current;
        completed = GetSavedInt(CompletedKey, 0) == 1;
        stepIndex = Mathf.Max(0, GetSavedInt(StepIndexKey, 0));
        baseCompleted = GetSavedInt(BaseCompletedKey, 0) == 1;
        baseStepIndex = Mathf.Max(0, GetSavedInt(BaseStepIndexKey, 0));
        coreChargerCompleted = GetSavedInt(CoreChargerCompletedKey, 0) == 1;
        coreChargerStepIndex = Mathf.Max(0, GetSavedInt(CoreChargerStepIndexKey, 0));
        inventoryCompleted = GetSavedInt(InventoryCompletedKey, 0) == 1;
        inventoryStepIndex = Mathf.Max(0, GetSavedInt(InventoryStepIndexKey, 0));
        bossEncounterCompleted = GetSavedInt(BossEncounterCompletedKey, 0) == 1;
        bossEncounterStepIndex = Mathf.Max(0, GetSavedInt(BossEncounterStepIndexKey, 0));
    }

    private void OnDestroy()
    {
        RestoreBossTutorialTimeScale();
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (config == null || config.Steps.Count == 0)
        {
            return;
        }

        // 게임(기지) 씬에서만 동작 — BaseCampManager가 떠야 시작한다(타이틀 등에서 안 뜨도록).
        if (BaseCampManager.Instance == null)
        {
            return;
        }
        
        if (Time.unscaledTime >= nextResolveTime)
        {
            nextResolveTime = Time.unscaledTime + ResolveInterval;
            SubscribeEvents();
        }

        // 씬 전체 스캔은 간격을 두고만 허용한다(찾은 타겟 추적 등 싼 경로는 매 프레임 유지).
        // running 여부와 무관하게 계산해, 아래 !running 분기의 IsPanelOpen 전체 스캔도 함께 스로틀한다.
        allowSceneScan = Time.unscaledTime >= nextSceneScanTime;
        if (allowSceneScan)
        {
            nextSceneScanTime = Time.unscaledTime + SceneScanInterval;
        }

        bool baseCampReady = BaseCampManager.Instance != null;
        bool bossEncounterReady = allowSceneScan
            && !bossEncounterCompleted
            && HasBossEncounterTutorialSteps()
            && IsBossEncounterTutorialReady();

        // 일반/기지 튜토리얼은 BaseCampManager가 필요하지만, 보스전 튜토리얼은 전투 씬에서 독립 실행될 수 있다.
        if (!baseCampReady && !bossEncounterReady)
        {
            return;
        }

        if (!running)
        {
            if (bossEncounterReady)
            {
                BeginStep(bossEncounterStepIndex, false, false, false, true);
                return;
            }

            if (!baseCampReady)
            {
                return;
            }

            if (!completed && HasTutorialSteps(false))
            {
                BeginStep(stepIndex, false);
                return;
            }

            if (allowSceneScan && !baseCompleted && HasTutorialSteps(true) && IsPanelOpen(BasePopupName))
            {
                BeginStep(baseStepIndex, true);
                return;
            }

            if (allowSceneScan
                && !coreChargerCompleted
                && HasCoreChargerTutorialSteps()
                && IsCoreChargerTutorialReady())
            {
                BeginStep(coreChargerStepIndex, false, true);
                return;
            }

            if (allowSceneScan
                && !inventoryCompleted
                && HasInventoryTutorialSteps()
                && IsInventoryTutorialReady())
            {
                BeginStep(inventoryStepIndex, false, false, true, false);
            }

            return;
        }

        ResolveActiveTarget();
        PollPanelOpened();
    }

    // ── 진행 ────────────────────────────────────────────────────────────────
    private void BeginStep(int index)
    {
        BeginStep(index, false);
    }

    private void BeginStep(int index, bool baseTutorial)
    {
        BeginStep(index, baseTutorial, false);
    }

    private void BeginStep(int index, bool baseTutorial, bool coreChargerTutorial)
    {
        BeginStep(index, baseTutorial, coreChargerTutorial, false, false);
    }

    private void BeginStep(
        int index,
        bool baseTutorial,
        bool coreChargerTutorial,
        bool inventoryTutorial,
        bool bossEncounterTutorial)
    {
        int stepCount = GetTutorialStepCount(baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial);
        if (index >= stepCount)
        {
            Complete(baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial);
            return;
        }

        runningBaseTutorial = baseTutorial;
        runningCoreChargerTutorial = coreChargerTutorial;
        runningInventoryTutorial = inventoryTutorial;
        runningBossEncounterTutorial = bossEncounterTutorial;
        if (bossEncounterTutorial)
        {
            bossEncounterStepIndex = index;
            PauseForBossTutorial();
        }
        else if (inventoryTutorial)
        {
            inventoryStepIndex = index;
        }
        else if (coreChargerTutorial)
        {
            coreChargerStepIndex = index;
        }
        else if (baseTutorial)
        {
            baseStepIndex = index;
        }
        else
        {
            stepIndex = index;
        }

        activeStep = GetTutorialStep(index, baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial);
        eventProgress = 0;
        resolvedTarget = null;
        ClearArmedButton();

        if (overlay == null)
        {
            overlay = CreateOverlay();
            if (overlay == null)
            {
                missingOverlayPrefab = true;
                return;
            }
        }

        running = true;
        bool showTouchDragIcon = bossEncounterTutorial
            && index == stepCount - 1
            && activeStep != null
            && activeStep.advanceType == TutorialAdvanceType.GameEvent
            && activeStep.eventType == TutorialEventType.BossDodgeUsed;
        overlay.Show(activeStep.bodyText, null, activeStep.advanceType, OnTapAdvance, showTouchDragIcon);
        allowSceneScan = true; // 스텝 시작 시엔 즉시 1회 스캔 허용.
        ResolveActiveTarget();
        Save();
    }

    private TutorialOverlay CreateOverlay()
    {
        TutorialOverlay prefab = config.OverlayPrefab;
        if (prefab == null)
        {
            if (!missingOverlayPrefab)
            {
                Debug.LogWarning("[Tutorial] TutorialOverlay 프리팹을 찾지 못했습니다. TutorialConfig에 지정하거나 Resources/Tutorial/TutorialOverlay.prefab을 생성하세요.");
            }

            return null;
        }

        // 오버레이 UI는 프리팹에서 만들고, 런타임에서는 인스턴스 제어만 한다.
        TutorialOverlay created = Instantiate(prefab, transform, false);
        created.Configure(config.BodyFont);
        return created;
    }

    private void OnTapAdvance()
    {
        if (running && activeStep != null && activeStep.advanceType == TutorialAdvanceType.Tap)
        {
            AdvanceStep();
        }
    }

    private void AdvanceStep()
    {
        bool baseTutorial = runningBaseTutorial;
        bool coreChargerTutorial = runningCoreChargerTutorial;
        bool inventoryTutorial = runningInventoryTutorial;
        bool bossEncounterTutorial = runningBossEncounterTutorial;
        int nextIndex = (bossEncounterTutorial
            ? bossEncounterStepIndex
            : inventoryTutorial
            ? inventoryStepIndex
            : coreChargerTutorial
            ? coreChargerStepIndex
            : baseTutorial ? baseStepIndex : stepIndex) + 1;
        ClearArmedButton();
        running = false;
        BeginStep(nextIndex, baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial);
    }

    private void Complete()
    {
        Complete(false);
    }

    private void Complete(bool baseTutorial)
    {
        Complete(baseTutorial, false);
    }

    private void Complete(bool baseTutorial, bool coreChargerTutorial)
    {
        Complete(baseTutorial, coreChargerTutorial, false, false);
    }

    private void Complete(bool baseTutorial, bool coreChargerTutorial, bool inventoryTutorial, bool bossEncounterTutorial)
    {
        if (bossEncounterTutorial)
        {
            bossEncounterCompleted = true;
        }
        else if (inventoryTutorial)
        {
            inventoryCompleted = true;
        }
        else if (coreChargerTutorial)
        {
            coreChargerCompleted = true;
        }
        else if (baseTutorial)
        {
            baseCompleted = true;
        }
        else
        {
            completed = true;
        }

        running = false;
        runningBaseTutorial = false;
        runningCoreChargerTutorial = false;
        runningInventoryTutorial = false;
        runningBossEncounterTutorial = false;
        activeStep = null;
        ClearArmedButton();
        RestoreBossTutorialTimeScale();
        if (overlay != null)
        {
            overlay.Hide();
        }

        SetSavedInt(
            bossEncounterTutorial
                ? BossEncounterCompletedKey
                : inventoryTutorial
                ? InventoryCompletedKey
                : coreChargerTutorial
                ? CoreChargerCompletedKey
                : baseTutorial ? BaseCompletedKey : CompletedKey,
            1);
        PlayerPrefs.Save();
        if (completed && baseCompleted && coreChargerCompleted && inventoryCompleted && bossEncounterCompleted)
        {
            UnsubscribeEvents();
        }
    }

    // ── 클라우드 동기화(통합 세이브 편입) ──────────────────────────────────────
    // 인스턴스가 없어도 동작하도록 PlayerPrefs를 직접 읽고/쓴다(부트스트랩이 인스턴스 생성을 결정하므로).

    /// <summary>
    /// 로그인/세이브 동기화 이후 현재 계정 키 기준으로 미완료 튜토리얼이 있으면 매니저를 생성한다.
    /// 부트스트랩은 로그인 전(게스트/이전 계정 키)에 실행되므로, 신규 계정처럼 클라우드 복원이
    /// 아예 없는 경우에도 이 호출로 계정에 맞는 튜토리얼이 뜨게 한다. 중복 호출은 무해하다.
    /// </summary>
    public static void EnsureBootstrappedForCurrentAccount()
    {
        Bootstrap();
    }

    /// <summary>현재 튜토리얼 진행 상태 스냅샷.</summary>
    public static JinyouTutorialSaveData CaptureSaveData()
    {
        MigrateLegacyKeysIfNeeded();
        return new JinyouTutorialSaveData
        {
            captured = true,
            completed = GetSavedInt(CompletedKey, 0) == 1,
            stepIndex = Mathf.Max(0, GetSavedInt(StepIndexKey, 0)),
            baseCompleted = GetSavedInt(BaseCompletedKey, 0) == 1,
            baseStepIndex = Mathf.Max(0, GetSavedInt(BaseStepIndexKey, 0)),
            coreChargerCompleted = GetSavedInt(CoreChargerCompletedKey, 0) == 1,
            coreChargerStepIndex = Mathf.Max(0, GetSavedInt(CoreChargerStepIndexKey, 0)),
            inventoryCompleted = GetSavedInt(InventoryCompletedKey, 0) == 1,
            inventoryStepIndex = Mathf.Max(0, GetSavedInt(InventoryStepIndexKey, 0)),
            bossEncounterCompleted = GetSavedInt(BossEncounterCompletedKey, 0) == 1,
            bossEncounterStepIndex = Mathf.Max(0, GetSavedInt(BossEncounterStepIndexKey, 0)),
        };
    }

    /// <summary>통합 세이브에서 튜토리얼 상태를 복원한다. 클라우드가 '완료'면 진행 중이던 튜토리얼도 즉시 종료한다.</summary>
    public static void RestoreSaveData(JinyouTutorialSaveData data)
    {
        MigrateLegacyKeysIfNeeded();
        if (data == null || !data.captured)
        {
            return;
        }

        SetSavedInt(CompletedKey, data.completed ? 1 : 0);
        SetSavedInt(StepIndexKey, Mathf.Max(0, data.stepIndex));
        SetSavedInt(BaseCompletedKey, data.baseCompleted ? 1 : 0);
        SetSavedInt(BaseStepIndexKey, Mathf.Max(0, data.baseStepIndex));
        SetSavedInt(CoreChargerCompletedKey, data.coreChargerCompleted ? 1 : 0);
        SetSavedInt(CoreChargerStepIndexKey, Mathf.Max(0, data.coreChargerStepIndex));
        SetSavedInt(InventoryCompletedKey, data.inventoryCompleted ? 1 : 0);
        SetSavedInt(InventoryStepIndexKey, Mathf.Max(0, data.inventoryStepIndex));
        SetSavedInt(BossEncounterCompletedKey, data.bossEncounterCompleted ? 1 : 0);
        SetSavedInt(BossEncounterStepIndexKey, Mathf.Max(0, data.bossEncounterStepIndex));
        PlayerPrefs.Save();

        TutorialManager live = Instance;
        if (live == null)
        {
            // 부트스트랩(로그인 전, 이전 계정/게스트 키 기준)이 "모두 완료"로 판단해 인스턴스를
            // 만들지 않았을 수 있다. 방금 쓴 계정 키 기준으로 미완료 튜토리얼이 있으면 다시 생성해,
            // 계정 전환 직후에도 앱 재시작 없이 튜토리얼이 뜨게 한다.
            Bootstrap();
            return;
        }

        if (data.completed)
        {
            live.Complete(); // 이번 실행에 떠 있던 튜토리얼을 즉시 종료/숨김.
        }
        else
        {
            live.completed = false;
            if (live.running)
            {
                // 클라우드 복원은 부팅 후 비동기로 도착하므로, 그 전에 로컬(빈/이전 계정) 상태로
                // 이미 시작된 스텝이 있을 수 있다. 그대로 두면 복원된 진행도가 무시되고 이후 저장이
                // 클라우드 진행도를 뒤로 덮어쓴다. 진행 중 스텝을 중단하고 복원된 인덱스를 적용해
                // 다음 프레임 Update에서 올바른 스텝부터 다시 시작하게 한다.
                live.ClearArmedButton();
                live.running = false;
                live.runningBaseTutorial = false;
                live.runningCoreChargerTutorial = false;
                live.runningInventoryTutorial = false;
                live.runningBossEncounterTutorial = false;
                live.activeStep = null;
                live.RestoreBossTutorialTimeScale();
                if (live.overlay != null)
                {
                    live.overlay.Hide();
                }
            }

            live.stepIndex = Mathf.Max(0, data.stepIndex);
        }

        live.baseCompleted = data.baseCompleted;
        live.coreChargerCompleted = data.coreChargerCompleted;
        live.inventoryCompleted = data.inventoryCompleted;
        live.bossEncounterCompleted = data.bossEncounterCompleted;
        live.baseStepIndex = Mathf.Max(0, data.baseStepIndex);
        live.coreChargerStepIndex = Mathf.Max(0, data.coreChargerStepIndex);
        live.inventoryStepIndex = Mathf.Max(0, data.inventoryStepIndex);
        live.bossEncounterStepIndex = Mathf.Max(0, data.bossEncounterStepIndex);
    }

    // 강조 타깃을 이름으로 계속 탐색해 늦게 활성화되는 패널 내부 요소도 따라간다.
    private bool HasTutorialSteps(bool baseTutorial)
    {
        return GetTutorialStepCount(baseTutorial, false, false, false) > 0;
    }

    private bool HasCoreChargerTutorialSteps()
    {
        return GetTutorialStepCount(false, true, false, false) > 0;
    }

    private bool HasInventoryTutorialSteps()
    {
        return GetTutorialStepCount(false, false, true, false) > 0;
    }

    private bool HasBossEncounterTutorialSteps()
    {
        return GetTutorialStepCount(false, false, false, true) > 0;
    }

    private int GetTutorialStepCount(
        bool baseTutorial,
        bool coreChargerTutorial,
        bool inventoryTutorial,
        bool bossEncounterTutorial)
    {
        if (config == null || config.Steps == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < config.Steps.Count; i++)
        {
            if (IsStepInGroup(config.Steps[i], baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial))
            {
                count++;
            }
        }

        return count;
    }

    // 튜토리얼 단계(Step) 가져오기
    private TutorialConfig.TutorialStep GetTutorialStep(
        int index,
        bool baseTutorial,
        bool coreChargerTutorial,
        bool inventoryTutorial,
        bool bossEncounterTutorial)
    {
        if (config == null || config.Steps == null)
        {
            return null;
        }

        int currentIndex = 0;
        for (int i = 0; i < config.Steps.Count; i++)
        {
            TutorialConfig.TutorialStep step = config.Steps[i];
            if (!IsStepInGroup(step, baseTutorial, coreChargerTutorial, inventoryTutorial, bossEncounterTutorial))
            {
                continue;
            }

            if (currentIndex == index)
            {
                return step;
            }

            currentIndex++;
        }

        return null;
    }

    // 튜토리얼 단계(Step) Bool 그룹
    private static bool IsStepInGroup(
        TutorialConfig.TutorialStep step,
        bool baseTutorial,
        bool coreChargerTutorial,
        bool inventoryTutorial,
        bool bossEncounterTutorial)
    {
        if (bossEncounterTutorial)
        {
            return IsBossEncounterTutorialStep(step);
        }

        if (inventoryTutorial)
        {
            return IsInventoryTutorialStep(step);
        }

        if (coreChargerTutorial)
        {
            return IsCoreChargerTutorialStep(step);
        }

        bool isBaseStep = IsBaseTutorialStep(step);
        bool isCoreChargerStep = IsCoreChargerTutorialStep(step);
        bool isInventoryStep = IsInventoryTutorialStep(step);
        bool isBossEncounterStep = IsBossEncounterTutorialStep(step);
        return baseTutorial
            ? isBaseStep && !isCoreChargerStep && !isInventoryStep && !isBossEncounterStep
            : !isBaseStep && !isCoreChargerStep && !isInventoryStep && !isBossEncounterStep;
    }

    // 처음으로 기지 팝업창을 열었을 경우 전개시작 (* 단, 사령부 레벨은 초기레벨(1레벨)로 유지할 것)
    private static bool IsBaseTutorialStep(TutorialConfig.TutorialStep step)
    {
        return step != null
            && !string.IsNullOrWhiteSpace(step.id)
            && step.id.StartsWith(BaseStepIdPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    // 코어차저가 해금되어 처음으로 관련 패널창이 열면 전개
    private static bool IsCoreChargerTutorialStep(TutorialConfig.TutorialStep step)
    {
        return step != null
            && !string.IsNullOrWhiteSpace(step.id)
            && step.id.StartsWith(CoreChargerStepIdPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    // 최소 파츠 1가지 획득 + 처음으로 인벤토리 팝업창을 열었을 경우 전개시작
    private static bool IsInventoryTutorialStep(TutorialConfig.TutorialStep step)
    {
        return step != null
            && !string.IsNullOrWhiteSpace(step.id)
            && step.id.StartsWith(InventoryStepIdPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    // 처음으로 보스전 버튼을 눌러 보스전이 시작되면 전개
    private static bool IsBossEncounterTutorialStep(TutorialConfig.TutorialStep step)
    {
        return step != null
            && !string.IsNullOrWhiteSpace(step.id)
            && step.id.StartsWith(BossEncounterStepIdPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    // 코어차저가 해금된 뒤 Base_Popup을 열면 튜토리얼을 시작한다.
    private static bool IsCoreChargerTutorialReady()
    {
        BaseCampManager camp = BaseCampManager.Instance;
        CommandCenter commandCenter = camp != null
            ? camp.CommandCenter
            : FindFirstObjectByType<CommandCenter>(FindObjectsInactive.Include);
        return commandCenter != null
            && commandCenter.IsFacilityUnlocked(CoreChargerFacilityId)
            && IsPanelOpen(BasePopupName);
    }
    
    // 인벤토리 튜토리얼 조건문
    private static bool IsInventoryTutorialReady()
    {
        InventoryFacility inventory = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : InventoryFacility.FindAny();
        if (inventory == null || inventory.EquipmentParts.Count <= 0)
        {
            return false;
        }

        return IsPanelOpen(InventoryPopupName);
    }

    // 보스전 튜토리얼 조건문
    private static bool IsBossEncounterTutorialReady()
    {
        BossEncounterManager encounterManager =
            FindFirstObjectByType<BossEncounterManager>(FindObjectsInactive.Include);
        return encounterManager != null && encounterManager.IsEncounterActive;
    }

    // 보스전 튜토리얼 전개가 시작되면 timeScale=0으로 바꿔 일시정지한다.
    private void PauseForBossTutorial()
    {
        if (!runningBossEncounterTutorial || bossTutorialPausedTimeScale)
        {
            return;
        }

        Time.timeScale = 0f;
        bossTutorialPausedTimeScale = true;
    }

    // 보스전 튜토리얼이 끝나면 timeScale을 원상복귀하여 일시정지를 푼다.
    private void RestoreBossTutorialTimeScale()
    {
        if (!bossTutorialPausedTimeScale)
        {
            return;
        }

        Time.timeScale = 1f;
        bossTutorialPausedTimeScale = false;
    }

    private static bool IsPanelOpen(string panelName)
    {
        Transform panel = FindInScene(panelName);
        return panel != null && panel.gameObject.activeInHierarchy;
    }
    private void ResolveActiveTarget()
    {
        if (activeStep == null || string.IsNullOrWhiteSpace(activeStep.highlightTargetName))
        {
            return;
        }

        if (resolvedTarget != null && resolvedTarget.gameObject.activeInHierarchy)
        {
            ArmTargetButtonIfNeeded();
            return;
        }

        if (!allowSceneScan)
        {
            return; // 전체 스캔은 SceneScanInterval 간격으로만.
        }

        Transform found = FindInScene(activeStep.highlightTargetName);
        resolvedTarget = found as RectTransform;
        if (resolvedTarget != null)
        {
            overlay?.SetHighlight(resolvedTarget);
            ArmTargetButtonIfNeeded();
        }
    }

    private void ArmTargetButtonIfNeeded()
    {
        if (activeStep == null || activeStep.advanceType != TutorialAdvanceType.TargetClicked)
        {
            return;
        }

        if (armedButton != null || resolvedTarget == null)
        {
            return;
        }

        Button button = resolvedTarget.GetComponent<Button>() ?? resolvedTarget.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            return;
        }

        armedButton = button;
        armedHandler = AdvanceStep;
        // 실제 버튼 동작(패널 열기 등)도 그대로 실행되고, 우리 리스너가 다음 스텝으로 넘긴다.
        armedButton.onClick.AddListener(armedHandler);
    }

    private void ClearArmedButton()
    {
        if (armedButton != null && armedHandler != null)
        {
            armedButton.onClick.RemoveListener(armedHandler);
        }

        armedButton = null;
        armedHandler = null;
    }

    private void PollPanelOpened()
    {
        if (activeStep == null
            || activeStep.advanceType != TutorialAdvanceType.GameEvent
            || activeStep.eventType != TutorialEventType.PanelOpened)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(activeStep.targetId)
            ? activeStep.highlightTargetName
            : activeStep.targetId;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!allowSceneScan)
        {
            return; // 전체 스캔은 SceneScanInterval 간격으로만.
        }

        Transform panel = FindInScene(name);
        if (panel != null && panel.gameObject.activeInHierarchy)
        {
            AdvanceStep();
        }
    }

    // ── 정적 보고 훅(기존 Report* 호출부 옆에서 함께 호출) ─────────────────────
    public static void Report(TutorialEventType eventType, int amount = 1)
    {
        Instance?.HandleEvent(eventType, amount);
    }

    public static void RaiseGameEvent(string eventName, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        if (System.Enum.TryParse(eventName, true, out TutorialEventType eventType))
        {
            Report(eventType, amount);
        }
    }

    private void HandleEvent(TutorialEventType eventType, int amount)
    {
        if (!running
            || activeStep == null
            || activeStep.advanceType != TutorialAdvanceType.GameEvent
            || activeStep.eventType != eventType)
        {
            return;
        }

        eventProgress += Mathf.Max(1, amount);
        if (eventProgress >= Mathf.Max(1, activeStep.targetAmount))
        {
            AdvanceStep();
        }
    }

    // ── 이벤트 구독(소스 수정 없이 시설 강화 완료 등을 감지) ────────────────────
    private void SubscribeEvents()
    {
        BaseCampManager camp = BaseCampManager.Instance;
        if (camp != null && subscribedCamp != camp)
        {
            UnsubscribeFacilities();
            camp.CommandCenter?.OnUpgradeCompleted.AddListener(HandleFacilityUpgraded);
            camp.CreditRefinery?.OnUpgradeCompleted.AddListener(HandleFacilityUpgraded);
            camp.AssemblyFactory?.OnUpgradeCompleted.AddListener(HandleFacilityUpgraded);
            camp.CoreCharger?.OnUpgradeCompleted.AddListener(HandleFacilityUpgraded);

            subscribedFactory = camp.AssemblyFactory;
            subscribedFactory?.OnWeaponEnhanced.AddListener(HandleWeaponEnhanced);
            subscribedCharger = camp.CoreCharger;
            subscribedCharger?.OnUnitEnhanced.AddListener(HandleUnitEnhanced);

            subscribedCamp = camp;
        }
    }

    private void UnsubscribeFacilities()
    {
        if (subscribedCamp != null)
        {
            subscribedCamp.CommandCenter?.OnUpgradeCompleted.RemoveListener(HandleFacilityUpgraded);
            subscribedCamp.CreditRefinery?.OnUpgradeCompleted.RemoveListener(HandleFacilityUpgraded);
            subscribedCamp.AssemblyFactory?.OnUpgradeCompleted.RemoveListener(HandleFacilityUpgraded);
            subscribedCamp.CoreCharger?.OnUpgradeCompleted.RemoveListener(HandleFacilityUpgraded);
        }

        subscribedFactory?.OnWeaponEnhanced.RemoveListener(HandleWeaponEnhanced);
        subscribedCharger?.OnUnitEnhanced.RemoveListener(HandleUnitEnhanced);
        subscribedFactory = null;
        subscribedCharger = null;
    }

    private void UnsubscribeEvents()
    {
        UnsubscribeFacilities();
        subscribedCamp = null;
    }

    private void HandleFacilityUpgraded() => HandleEvent(TutorialEventType.FacilityUpgraded, 1);
    private void HandleWeaponEnhanced(ProjectileConfig weapon, int level) => HandleEvent(TutorialEventType.WeaponEnhanced, 1);
    private void HandleUnitEnhanced(PlayerUnitConfig unit, int level) => HandleEvent(TutorialEventType.UnitEnhanced, 1);

    // 튜토리얼 진행내역 저장
    private void Save()
    {
        if (runningBossEncounterTutorial)
        {
            SetSavedInt(BossEncounterStepIndexKey, bossEncounterStepIndex);
            SetSavedInt(BossEncounterCompletedKey, bossEncounterCompleted ? 1 : 0);
        }
        else if (runningInventoryTutorial)
        {
            SetSavedInt(InventoryStepIndexKey, inventoryStepIndex);
            SetSavedInt(InventoryCompletedKey, inventoryCompleted ? 1 : 0);
        }
        else if (runningCoreChargerTutorial)
        {
            SetSavedInt(CoreChargerStepIndexKey, coreChargerStepIndex);
            SetSavedInt(CoreChargerCompletedKey, coreChargerCompleted ? 1 : 0);
        }
        else if (runningBaseTutorial)
        {
            SetSavedInt(BaseStepIndexKey, baseStepIndex);
            SetSavedInt(BaseCompletedKey, baseCompleted ? 1 : 0);
        }
        else
        {
            SetSavedInt(StepIndexKey, stepIndex);
            SetSavedInt(CompletedKey, completed ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    // 씬 전체에서 이름으로 탐색(활성 우선, 비활성 폴백) — RewardFlyAnimator.FindInScene와 동일 패턴.
    private static Transform FindInScene(string objectName)
    {
        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active.transform;
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform tr = all[i];
            if (tr.name == objectName && tr.gameObject.scene.IsValid())
            {
                return tr;
            }
        }

        return null;
    }
}

