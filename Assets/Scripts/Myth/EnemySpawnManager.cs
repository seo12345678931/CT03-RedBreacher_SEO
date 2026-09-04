using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    private const string CurrentRoundKey = "EnemySpawnManager.CurrentRound";

    [Header("Stage")]
    [SerializeField] private int startStage = 1;
    [SerializeField] private int roundsPerStage = 5;
    [SerializeField] private bool saveToPlayerPrefs = true;

    [Header("Round")]
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private int startRound = 1;
    [SerializeField] private int baseEnemyCount = 4;
    [SerializeField] private int enemyCountIncreasePerStage = 2;
    [SerializeField] private int enemyCountIncreasePerRound = 1;
    [Tooltip("라운드당 스폰되는 적 수의 상한. 고스테이지 포위 즉사와 성능 저하를 막는다.")]
    [SerializeField] private int maxEnemyCountPerRound = 55;
    [SerializeField] private float timeBetweenRounds = 2f;

    [Header("Player Death")]
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform playerRespawnPoint;
    [SerializeField] private float restartDelayOnPlayerDeath = 3f;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverCountdownText;

    [Header("Damage Number")]
    [Tooltip("전투 전체의 기본 데미지 숫자 폰트. 런타임에 CombatHealth가 붙는 적들도 이 폰트를 쓴다.")]
    [SerializeField] private TMP_FontAsset damageNumberFont;
    [Tooltip("전투 전체의 데미지 숫자 크기. 0 이하면 각 프리팹의 개별 크기를 그대로 쓴다.")]
    [SerializeField] private float damageNumberFontSize = 3.2f;

    [Header("Enemy Scaling")]
    [SerializeField] private float healthIncreasePerStage = 0.2f;
    [SerializeField] private float damageIncreasePerStage = 0.1f;
    [Tooltip("이 스테이지를 넘어서면 적 데미지 증가율이 아래 값으로 낮아진다.")]
    [SerializeField] private int damageSoftCapStage = 50;
    [SerializeField] private float damageIncreasePerStageAfterCap = 0.05f;
    [SerializeField] private float moveSpeedIncreasePerStage = 0.018f;
    [SerializeField] private float rewardIncreasePerStage = 0.15f;

    [Header("Spawn")]
    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private List<EnemyConfig> enemyConfigs = new List<EnemyConfig>();
    [SerializeField] private List<EnemyConfig> enemyConfigStage2 = new List<EnemyConfig>();
    [SerializeField] private List<EnemyConfig> enemyConfigStage3 = new List<EnemyConfig>();
    [SerializeField] private List<EnemyConfig> enemyConfigFinalStage = new List<EnemyConfig>();
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemySpawnParent;
    [SerializeField] private Transform spawnCenter;
    [SerializeField] private float spawnRadius = 8f;
    [Tooltip("플레이어로부터 이 거리 안쪽에는 적이 스폰되지 않는다(0이면 제한 없음).")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 4f;
    [SerializeField] private float spawnInterval = 0.25f;

    [Header("Spawn Bounds")]
    [SerializeField] private bool clampSpawnToBounds = true;
    [SerializeField] private Transform spawnBoundsCenter;
    [SerializeField] private Vector2 spawnBoundsSize = new Vector2(24f, 24f);
    [SerializeField] private float spawnBoundsPadding = 1f;
    [SerializeField] private LayerMask spawnObstacleMask;
    [SerializeField] private float spawnCollisionRadius = 0.6f;
    [SerializeField] private int spawnPositionAttempts = 12;

    private readonly List<CombatHealth> aliveEnemies = new List<CombatHealth>();
    private Coroutine roundRoutine;
    private Coroutine playerDeathRestartRoutine;
    private Vector3 fallbackPlayerRespawnPosition;
    private Vector3 fallbackSpawnBoundsCenter;
    private int currentStage;
    private int currentRound;
    private int currentRoundInStage;
    private int lastReportedStageClearRound;
    private int pausedRoundForBoss;
    private bool roundsActive;
    private bool spawningRound;
    private bool bossEncounterActive;

    /// <summary>스테이지 번호가 바뀔 때 새 스테이지 값과 함께 발화한다. (배경 교체 등 연출 훅)</summary>
    public event System.Action<int> StageChanged;

    public int CurrentStage => currentStage;
    public int CurrentRound => currentRound;
    public int CurrentRoundInStage => currentRoundInStage;
    public int RoundsPerStage => Mathf.Max(1, roundsPerStage);
    public int AliveEnemyCount => aliveEnemies.Count;
    public bool IsSpawningRound => spawningRound;
    public bool IsBossEncounterActive => bossEncounterActive;
    public bool CanStartBossEncounter => !bossEncounterActive
        && playerDeathRestartRoutine == null
        && (player == null || player.Health == null || !player.Health.IsDead);

    private void Awake()
    {
        // 데미지 숫자 전역 기본 폰트/크기 주입(플레이어/보스 포함 전 CombatHealth에 적용).
        if (damageNumberFont != null)
        {
            CombatHealth.GlobalDamageNumberFont = damageNumberFont;
        }
        CombatHealth.GlobalDamageNumberFontSize = damageNumberFontSize;

        ResolvePlayer();
        fallbackPlayerRespawnPosition = player != null
            ? CombatPlane.WithFixedY(player.transform.position)
            : CombatPlane.WithFixedY(transform.position);
        fallbackSpawnBoundsCenter = spawnBoundsCenter != null
            ? CombatPlane.WithFixedY(spawnBoundsCenter.position)
            : spawnCenter != null
                ? CombatPlane.WithFixedY(spawnCenter.position)
                : CombatPlane.WithFixedY(transform.position);
        SetGameOverPanelActive(false);
        currentStage = Mathf.Max(1, startStage);
        currentRound = LoadCurrentRound();
        RefreshStageRoundState();
    }

    private void Start()
    {
        if (!startOnAwake)
        {
            return;
        }

        // 클라우드 복원 전에 라운드를 시작하면, 복원이 라운드 진행 중에 도착해
        // 진행 중이던 라운드의 currentRound++가 복원된 라운드를 덮어써 한 라운드가 건너뛰어진다.
        // 복원이 끝날 때까지는 ScreenFader가 화면을 가리고 있어 대기가 보이지 않는다.
        BaseCampManager baseCamp = BaseCampManager.Instance;
        if (baseCamp != null && !baseCamp.IsUnifiedSaveLoaded)
        {
            baseCamp.UnifiedSaveLoaded += HandleUnifiedSaveLoadedForStart;
            return;
        }

        StartRounds();
    }

    private void HandleUnifiedSaveLoadedForStart()
    {
        if (BaseCampManager.Instance != null)
        {
            BaseCampManager.Instance.UnifiedSaveLoaded -= HandleUnifiedSaveLoadedForStart;
        }

        StartRounds();
    }

    private void OnDestroy()
    {
        if (BaseCampManager.Instance != null)
        {
            BaseCampManager.Instance.UnifiedSaveLoaded -= HandleUnifiedSaveLoadedForStart;
        }
    }

    private void Update()
    {
        CleanupAliveEnemies();
        HandlePlayerDeathRestart();

        if (roundsActive && !spawningRound && roundRoutine == null && aliveEnemies.Count == 0)
        {
            ReportStageClearIfNeeded();
            roundRoutine = StartCoroutine(RoundRoutine());
        }
    }

    public void StartRounds()
    {
        if (roundRoutine != null || bossEncounterActive)
        {
            return;
        }

        // 외부 UI나 게임 상태에서 라운드 생성을 명시적으로 시작할 수 있다.
        roundsActive = true;
        roundRoutine = StartCoroutine(RoundRoutine());
    }

    private IEnumerator RoundRoutine()
    {
        spawningRound = true;

        if (currentRound > startRound)
        {
            yield return new WaitForSeconds(timeBetweenRounds);
        }

        RefreshStageRoundState();
        int spawnCount = GetEnemyCountForRound(currentRound);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnEnemy(i);
            yield return new WaitForSeconds(spawnInterval);
        }

        spawningRound = false;
        currentRound++;
        SaveProgress();
        roundRoutine = null;
    }

    public void ResetStageProgress()
    {
        roundsActive = false;

        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        if (playerDeathRestartRoutine != null)
        {
            StopCoroutine(playerDeathRestartRoutine);
            playerDeathRestartRoutine = null;
        }

        spawningRound = false;
        // 보스전 중 리셋되면 보스도 ClearSpawnedEnemies로 파괴된다. 플래그를 함께 해제해야
        // StartRounds가 막히지 않고, BossEncounterManager의 중단 처리(ResumePausedRound)가
        // 리셋된 라운드를 보스 소환 전 라운드로 되돌리지 않는다.
        bossEncounterActive = false;
        ClearSpawnedEnemies();
        RevivePlayer();
        currentRound = Mathf.Max(1, startRound);
        RefreshStageRoundState();
        SaveProgress();

        // 디버그 초기화 직후에도 기존 start 설정에 맞춰 바로 재시작한다.
        if (startOnAwake)
        {
            StartRounds();
        }
    }

    public bool PauseForBossEncounter()
    {
        if (!CanStartBossEncounter)
        {
            return false;
        }

        // 현재 진행 중인 라운드를 저장하고 일반 적 생성을 보스전 동안 중단한다.
        pausedRoundForBoss = spawningRound || aliveEnemies.Count == 0
            ? currentRound
            : Mathf.Max(startRound, currentRound - 1);
        bossEncounterActive = true;
        roundsActive = false;

        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        spawningRound = false;
        ClearSpawnedEnemies();
        return true;
    }

    public void ResumePausedRound()
    {
        if (!bossEncounterActive)
        {
            return;
        }

        // 보스 승리 후 소환 직전 라운드를 처음부터 다시 진행한다.
        bossEncounterActive = false;
        currentRound = Mathf.Max(startRound, pausedRoundForBoss);
        RefreshStageRoundState();
        SaveProgress();
        roundsActive = true;
        StartRounds();
    }

    public void CancelBossEncounterForPlayerDeath()
    {
        // 사망 재시작 루틴이 현재 스테이지의 첫 라운드를 결정하도록 상태만 해제한다.
        bossEncounterActive = false;
        roundsActive = false;
    }

    private int GetEnemyCountForRound(int round)
    {
        int stageOffset = Mathf.Max(0, GetStageForRound(round) - startStage);
        int roundInStageOffset = Mathf.Max(0, GetRoundInStage(round) - 1);
        int count = Mathf.Max(1, baseEnemyCount
            + enemyCountIncreasePerStage * stageOffset
            + enemyCountIncreasePerRound * roundInStageOffset);
        return maxEnemyCountPerRound > 0 ? Mathf.Min(count, maxEnemyCountPerRound) : count;
    }

    private int GetStageForRound(int round)
    {
        int roundOffset = Mathf.Max(0, round - startRound);
        return Mathf.Max(1, startStage + roundOffset / RoundsPerStage);
    }

    private int GetRoundInStage(int round)
    {
        int roundOffset = Mathf.Max(0, round - startRound);
        return roundOffset % RoundsPerStage + 1;
    }

    private int GetFirstRoundForStage(int stage)
    {
        int stageOffset = Mathf.Max(0, stage - startStage);
        return Mathf.Max(1, startRound + stageOffset * RoundsPerStage);
    }

    private void RefreshStageRoundState()
    {
        int previousStage = currentStage;
        currentStage = GetStageForRound(currentRound);
        currentRoundInStage = GetRoundInStage(currentRound);

        if (currentStage != previousStage)
        {
            AchievementManager.ReportStageCleared(currentStage);
            StageChanged?.Invoke(currentStage);
        }
    }

    private void ReportStageClearIfNeeded()
    {
        int clearedRound = currentRound - 1;
        if (clearedRound < startRound || clearedRound == lastReportedStageClearRound)
        {
            return;
        }

        if (GetRoundInStage(clearedRound) != RoundsPerStage)
        {
            return;
        }

        lastReportedStageClearRound = clearedRound;
        AchievementManager.ReportStageCleared(currentStage);
        MainGuideMissionManager.ReportStageCleared();
    }

    private void SpawnEnemy(int index)
    {
        Vector3 spawnPosition = GetSpawnPosition(index);
        EnemyConfig selectedConfig = GetEnemyConfig();
        GameObject prefab = GetEnemyPrefab(selectedConfig);
        GameObject enemyObject = prefab != null
            ? Instantiate(prefab, spawnPosition, Quaternion.identity, enemySpawnParent)
            : CreateFallbackEnemy(spawnPosition);

        // 라운드 적으로 동작하는 데 필요한 전투 컴포넌트를 보강한다.
        EnemyController enemy = enemyObject.GetComponent<EnemyController>();
        if (enemy == null)
        {
            enemy = enemyObject.AddComponent<EnemyController>();
        }

        enemy.Initialize(
            selectedConfig,
            GetEnemyLevel(),
            GetStageScale(healthIncreasePerStage),
            GetStageScale(moveSpeedIncreasePerStage),
            GetDamageStageScale(),
            GetStageScale(rewardIncreasePerStage));

        CombatHealth enemyHealth = enemyObject.GetComponent<CombatHealth>();
        if (enemyHealth == null)
        {
            enemyHealth = enemyObject.AddComponent<CombatHealth>();
        }

        aliveEnemies.Add(enemyHealth);

        // AutoSyncTransforms가 꺼져 있어, 방금 스폰한 콜라이더는 다음 물리 스텝 전까지
        // Physics.OverlapSphere(투사체 히트 판정)에 잡히지 않는다. 즉시 동기화해 스폰 직후
        // 공격이 통과(무시)되는 문제를 막는다.
        Physics.SyncTransforms();
    }

    private EnemyConfig GetEnemyConfig()
    {
        int validConfigCount = CountValidEnemyConfigs(enemyConfigs)
            + (currentStage >= 11 ? CountValidEnemyConfigs(enemyConfigStage2) : 0)
            + (currentStage >= 21 ? CountValidEnemyConfigs(enemyConfigStage3) : 0)
            + (currentStage >= 31 ? CountValidEnemyConfigs(enemyConfigFinalStage) : 0);

        if (validConfigCount > 0)
        {
            // 등록된 적 SO 중 유효한 항목 하나를 스폰마다 무작위로 선택한다.
            // 스테이지 10 이상 넘어갈 때 마다 새로운 적 SO를 추가해 스폰에 포함시킨다.
            int targetIndex = Random.Range(0, validConfigCount);
            if (TryGetEnemyConfigAt(enemyConfigs, ref targetIndex, out EnemyConfig selectedConfig)
                || currentStage >= 11 && TryGetEnemyConfigAt(enemyConfigStage2, ref targetIndex, out selectedConfig)
                || currentStage >= 21 && TryGetEnemyConfigAt(enemyConfigStage3, ref targetIndex, out selectedConfig)
                || currentStage >= 31 && TryGetEnemyConfigAt(enemyConfigFinalStage, ref targetIndex, out selectedConfig))
            {
                return selectedConfig;
            }
        }

        return enemyConfig;
    }

    private static int CountValidEnemyConfigs(List<EnemyConfig> configs)
    {
        if (configs == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < configs.Count; i++)
        {
            if (configs[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    // 각 스테이지 별 적 SO 갯수만큼 스폰에 적용
    private static bool TryGetEnemyConfigAt(List<EnemyConfig> configs, ref int targetIndex, out EnemyConfig selectedConfig)
    {
        selectedConfig = null;
        if (configs == null)
        {
            return false;
        }

        for (int i = 0; i < configs.Count; i++)
        {
            EnemyConfig config = configs[i];
            if (config == null)
            {
                continue;
            }

            if (targetIndex == 0)
            {
                selectedConfig = config;
                return true;
            }

            targetIndex--;
        }

        return false;
    }

    private GameObject GetEnemyPrefab(EnemyConfig selectedConfig)
    {
        if (selectedConfig != null && selectedConfig.EnemyPrefab != null)
        {
            return selectedConfig.EnemyPrefab;
        }

        return enemyPrefab;
    }

    private Vector3 GetSpawnPosition(int index)
    {
        Vector3 center = GetSpawnCenterPosition();
        int attempts = Mathf.Max(1, spawnPositionAttempts);
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = GetSpawnCandidate(center, index, i);
            if (IsSpawnPositionValid(candidate))
            {
                return candidate;
            }
        }

        // 모든 후보가 막혀 있으면 마지막 후보라도 bounds 안쪽으로 제한해서 반환한다.
        return ClampSpawnPositionToBounds(GetSpawnCandidate(center, index, attempts));
    }

    private Vector3 GetSpawnCandidate(Vector3 center, int index, int attempt)
    {
        float angle = (index * 137.5f + currentRound * 29f + attempt * 57f) * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, spawnRadius - attempt * 0.35f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        return ClampSpawnPositionToBounds(center + offset);
    }

    private int GetEnemyLevel()
    {
        // 적 레벨은 현재 스테이지에 맞춰 증가한다.
        return Mathf.Max(1, currentStage);
    }

    private float GetStageScale(float increasePerStage)
    {
        int stageOffset = Mathf.Max(0, currentStage - startStage);
        return Mathf.Max(0.01f, 1f + increasePerStage * stageOffset);
    }

    private float GetDamageStageScale()
    {
        // 소프트캡 스테이지까지는 기본 증가율, 그 이후는 완화된 증가율을 적용해
        // 고스테이지에서 접촉데미지 포위 즉사를 막는다.
        int fullRateStages = Mathf.Max(0, Mathf.Min(currentStage, damageSoftCapStage) - startStage);
        int reducedStages = Mathf.Max(0, currentStage - Mathf.Max(startStage, damageSoftCapStage));
        return Mathf.Max(0.01f, 1f
            + damageIncreasePerStage * fullRateStages
            + damageIncreasePerStageAfterCap * reducedStages);
    }

    private Vector3 GetSpawnCenterPosition()
    {
        if (spawnCenter != null)
        {
            return CombatPlane.WithFixedY(spawnCenter.position);
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            return CombatPlane.WithFixedY(player.transform.position);
        }

        return CombatPlane.WithFixedY(transform.position);
    }

    private bool IsSpawnPositionValid(Vector3 position)
    {
        // 플레이어와 너무 가까운 위치는 후보에서 제외한다(다른 각도로 재시도됨).
        if (!IsFarEnoughFromPlayer(position))
        {
            return false;
        }

        if (spawnObstacleMask.value == 0)
        {
            return true;
        }

        // 벽/장애물 콜라이더와 겹치는 위치는 스폰 후보에서 제외한다.
        return !Physics.CheckSphere(
            CombatPlane.WithFixedY(position),
            Mathf.Max(0.01f, spawnCollisionRadius),
            spawnObstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool IsFarEnoughFromPlayer(Vector3 position)
    {
        float minDistance = Mathf.Max(0f, minSpawnDistanceFromPlayer);
        if (minDistance <= 0f)
        {
            return true;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (player == null)
        {
            return true;
        }

        Vector3 playerPosition = CombatPlane.WithFixedY(player.transform.position);
        return CombatPlane.DistanceSqr(position, playerPosition) >= minDistance * minDistance;
    }

    private Vector3 ClampSpawnPositionToBounds(Vector3 position)
    {
        position = CombatPlane.WithFixedY(position);
        if (!clampSpawnToBounds)
        {
            return position;
        }

        Vector3 center = GetSpawnBoundsCenter();
        Vector2 halfSize = Vector2.Max(Vector2.zero, spawnBoundsSize * 0.5f - Vector2.one * Mathf.Max(0f, spawnBoundsPadding));

        // 벽 콜라이더 안쪽으로 스폰 위치를 제한해 적이 벽 밖에서 생성되지 않게 한다.
        position.x = Mathf.Clamp(position.x, center.x - halfSize.x, center.x + halfSize.x);
        position.z = Mathf.Clamp(position.z, center.z - halfSize.y, center.z + halfSize.y);
        return CombatPlane.WithFixedY(position);
    }

    private Vector3 GetSpawnBoundsCenter()
    {
        if (spawnBoundsCenter != null)
        {
            return CombatPlane.WithFixedY(spawnBoundsCenter.position);
        }

        if (Application.isPlaying)
        {
            // 플레이어가 움직여도 아레나 스폰 경계 중심은 시작 위치에 고정한다.
            return fallbackSpawnBoundsCenter;
        }

        if (spawnCenter != null)
        {
            return CombatPlane.WithFixedY(spawnCenter.position);
        }

        return CombatPlane.WithFixedY(transform.position);
    }

    private GameObject CreateFallbackEnemy(Vector3 spawnPosition)
    {
        GameObject enemyObject = new GameObject($"Round {currentRound} Enemy");
        if (enemySpawnParent != null)
        {
            enemyObject.transform.SetParent(enemySpawnParent);
        }

        enemyObject.transform.position = CombatPlane.WithFixedY(spawnPosition);

        // 프리팹이 없을 때도 라운드 테스트가 가능하도록 임시 표시를 만든다.
        SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CombatVisualFactory.CreateCircleSprite(Color.red);
        renderer.sortingOrder = 1;

        return enemyObject;
    }

    private void CleanupAliveEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null || aliveEnemies[i].IsDead)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    private void HandlePlayerDeathRestart()
    {
        ResolvePlayer();

        if (player == null || player.Health == null || !player.Health.IsDead || playerDeathRestartRoutine != null)
        {
            return;
        }

        SetGameOverPanelActive(true);
        playerDeathRestartRoutine = StartCoroutine(RestartCurrentStageAfterPlayerDeath());
    }

    private IEnumerator RestartCurrentStageAfterPlayerDeath()
    {
        int stageToRestart = Mathf.Max(1, currentStage);
        bossEncounterActive = false;
        roundsActive = false;

        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
        }

        spawningRound = false;
        ClearSpawnedEnemies();

        // 사망 연출/확인 시간을 둔 뒤 현재 스테이지를 처음부터 다시 시작한다.
        float remainingSeconds = Mathf.Max(0f, restartDelayOnPlayerDeath);
        while (remainingSeconds > 0f)
        {
            UpdateGameOverCountdown(remainingSeconds);
            remainingSeconds -= Time.deltaTime;
            yield return null;
        }

        RevivePlayer();
        currentRound = GetFirstRoundForStage(stageToRestart);
        RefreshStageRoundState();
        SaveProgress();
        roundsActive = true;
        playerDeathRestartRoutine = null;
        StartRounds();
    }

    private void RevivePlayer()
    {
        if (player == null || player.Health == null)
        {
            return;
        }

        // 부활 시 아레나 시작 위치로 되돌려 구석 재시작을 막는다.
        player.transform.position = GetPlayerRespawnPosition();
        player.Health.Initialize(player.Health.MaxHealth);
        CombatPlane.ClampTransform(player.transform);
        SetGameOverPanelActive(false);
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }
    }

    private Vector3 GetPlayerRespawnPosition()
    {
        if (playerRespawnPoint != null)
        {
            return CombatPlane.WithFixedY(playerRespawnPoint.position);
        }

        return CombatPlane.WithFixedY(fallbackPlayerRespawnPosition);
    }

    private void SetGameOverPanelActive(bool isActive)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(isActive);
        }

        if (!isActive && gameOverCountdownText != null)
        {
            gameOverCountdownText.text = string.Empty;
        }
    }

    private void UpdateGameOverCountdown(float remainingSeconds)
    {
        if (gameOverCountdownText != null)
        {
            int displaySeconds = Mathf.Max(1, Mathf.CeilToInt(remainingSeconds));
            gameOverCountdownText.text = $"{displaySeconds}초 뒤 다시 시작합니다.";
        }
    }

    private void ClearSpawnedEnemies()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                Destroy(enemies[i].gameObject);
            }
        }

        aliveEnemies.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!clampSpawnToBounds)
        {
            return;
        }

        Vector3 center = GetSpawnBoundsCenter();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, new Vector3(spawnBoundsSize.x, 0.1f, spawnBoundsSize.y));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, spawnCollisionRadius));
    }

    private int LoadCurrentRound()
    {
        int defaultRound = Mathf.Max(1, startRound);
        if (!saveToPlayerPrefs)
        {
            return defaultRound;
        }

        return Mathf.Max(defaultRound, PlayerPrefs.GetInt(CurrentRoundKey, defaultRound));
    }

    private void SaveProgress()
    {
        if (!saveToPlayerPrefs)
        {
            return;
        }

        PlayerPrefs.SetInt(CurrentRoundKey, Mathf.Max(1, currentRound));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 통합 세이브 사용 시 전역 PlayerPrefs 개별 저장을 끈다. 키가 UID 스코프가 아니라서
    /// 계정 전환 시 이전 계정의 라운드 진행이 새 계정으로 새는 것을 막는다.
    /// </summary>
    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(CurrentRoundKey);
        PlayerPrefs.Save();
    }

    /// <summary>클라우드 동기화용 스테이지 진행(라운드) 스냅샷(통합 세이브에 편입).</summary>
    public JinyouEnemySpawnSaveData CaptureSaveData()
    {
        return new JinyouEnemySpawnSaveData
        {
            captured = true,
            currentRound = Mathf.Max(1, currentRound),
        };
    }

    /// <summary>통합 세이브에서 라운드 진행을 복원한다(클라우드 풀 이후 재적용).</summary>
    public void RestoreSaveData(JinyouEnemySpawnSaveData data)
    {
        if (data == null || !data.captured)
        {
            return;
        }

        currentRound = Mathf.Max(1, data.currentRound);
        // 복원 직후 스테이지/라운드 파생 상태를 갱신해 적 스케일링·배경(StageChanged)이 어긋나지 않게 한다.
        RefreshStageRoundState();
        SaveProgress();
    }
}
