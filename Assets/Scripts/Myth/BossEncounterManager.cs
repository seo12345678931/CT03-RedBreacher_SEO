using System;
using UnityEngine;

public class BossEncounterManager : MonoBehaviour
{
    [Header("Encounter")]
    [SerializeField] private EnemySpawnManager enemySpawnManager;
    [SerializeField] private PlayerController player;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform bossSpawnParent;
    [SerializeField] private BossEncounterHud bossEncounterHud;

    [Header("Player-Level Scaling")]
    [Tooltip("켜면 보스 능력치를 플레이어 레벨에 따라 추가로 스케일링한다(난이도 배수 위에 곱해짐).")]
    [SerializeField] private bool scaleBossByPlayerLevel = true;
    [Tooltip("이 레벨에서 레벨 배수 1.0 기준(이하 레벨은 1.0).")]
    [SerializeField] private int scalingBaseLevel = 1;
    [Tooltip("기준 레벨 초과 1레벨당 체력 증가율.")]
    [SerializeField] private float healthPerLevel = 0.1f;
    [Tooltip("기준 레벨 초과 1레벨당 공격력 증가율.")]
    [SerializeField] private float damagePerLevel = 0.05f;
    [Tooltip("기준 레벨 초과 1레벨당 보상 증가율.")]
    [SerializeField] private float rewardPerLevel = 0.05f;

    private BossEnemyController activeBoss;
    private CombatHealth activeBossHealth;
    private bool encounterActive;

    public bool IsEncounterActive => encounterActive;
    public bool IsBossHudVisible
    {
        get
        {
            ResolveReferences();
            return bossEncounterHud != null && bossEncounterHud.IsBossHudVisible;
        }
    }

    public event Action EncounterStarted;
    public event Action<bool> EncounterEnded;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        UnsubscribeActiveBossHealth();
    }

    private void Update()
    {
        if (!encounterActive)
        {
            return;
        }

        ResolveReferences();
        if (player != null && player.Health != null && player.Health.IsDead)
        {
            HandlePlayerDefeat();
            return;
        }

        if (activeBossHealth == null)
        {
            // 보스가 죽지 않고 외부에서 파괴됨(스테이지 리셋 등) → 승리가 아니라 중단으로 처리한다.
            AbortEncounter();
            return;
        }

        if (activeBossHealth.IsDead)
        {
            HandleBossDefeat();
        }
    }

    public bool CanSummon(BossEnemyConfig config)
    {
        ResolveReferences();
        return !encounterActive
            && enemySpawnManager != null
            && enemySpawnManager.CanStartBossEncounter
            && config != null
            && config.EnemyPrefab != null
            && config.EnemyPrefab.GetComponent<BossEnemyController>() != null;
    }

    public bool TrySummon(BossEnemyConfig config)
    {
        return TrySummon(config, 1f, 1f, 1f, 1f);
    }

    public bool TrySummon(
        BossEnemyConfig config,
        float healthScale,
        float moveSpeedScale,
        float damageScale,
        float rewardScale)
    {
        if (!CanSummon(config) || !enemySpawnManager.PauseForBossEncounter())
        {
            return false;
        }

        Vector3 spawnPosition = bossSpawnPoint != null
            ? CombatPlane.WithFixedY(bossSpawnPoint.position)
            : CombatPlane.WithFixedY(transform.position);
        GameObject bossObject = Instantiate(
            config.EnemyPrefab,
            spawnPosition,
            Quaternion.identity,
            bossSpawnParent);
        activeBoss = bossObject.GetComponent<BossEnemyController>();
        activeBossHealth = bossObject.GetComponent<CombatHealth>();
        if (activeBoss == null || activeBossHealth == null)
        {
            Destroy(bossObject);
            enemySpawnManager.ResumePausedRound();
            return false;
        }

        // 난이도 배수 위에 플레이어 레벨 스케일을 곱해 최종 수치를 만든다.
        // (이동속도는 레벨 스케일 대상에서 제외 — 난이도 배수만 적용)
        // 보스 SO의 전투 수치를 적용하고 기존 보상/타겟 경로에 등록한다.
        activeBoss.InitializeBoss(
            config,
            enemySpawnManager.CurrentStage,
            healthScale * GetPlayerLevelScale(healthPerLevel),
            moveSpeedScale,
            damageScale * GetPlayerLevelScale(damagePerLevel),
            rewardScale * GetPlayerLevelScale(rewardPerLevel));
        activeBossHealth.OnDied += HandleBossDied;
        encounterActive = true;
        bossEncounterHud?.Show(config, activeBossHealth);
        EncounterStarted?.Invoke();
        return true;
    }

    private void HandleBossDied(CombatDamageEvent damageEvent)
    {
        if (!encounterActive)
        {
            return;
        }

        HandleBossDefeat();
    }

    private void HandleBossDefeat()
    {
        encounterActive = false;
        UnsubscribeActiveBossHealth();
        activeBoss = null;
        activeBossHealth = null;
        bossEncounterHud?.Hide();
        enemySpawnManager?.ResumePausedRound();
        MainGuideMissionManager.ReportBossDefeated();
        EncounterEnded?.Invoke(true);
    }

    // 보스가 처치되지 않고 사라진 경우(스테이지 리셋 등 외부 파괴)의 중단 처리.
    // 승리 보상/미션 보고 없이 상태만 정리한다. ResumePausedRound는 bossEncounterActive가
    // 이미 해제된 경우(리셋 경로) 내부 가드로 no-op이라 리셋된 라운드를 되돌리지 않는다.
    private void AbortEncounter()
    {
        encounterActive = false;
        UnsubscribeActiveBossHealth();
        activeBoss = null;
        activeBossHealth = null;
        bossEncounterHud?.Hide();
        enemySpawnManager?.ResumePausedRound();
        EncounterEnded?.Invoke(false);
    }

    private void HandlePlayerDefeat()
    {
        encounterActive = false;
        UnsubscribeActiveBossHealth();
        if (activeBoss != null)
        {
            Destroy(activeBoss.gameObject);
        }

        activeBoss = null;
        activeBossHealth = null;
        bossEncounterHud?.Hide();
        enemySpawnManager?.CancelBossEncounterForPlayerDeath();
        EncounterEnded?.Invoke(false);
    }

    public void ShowResult(string title, string detail, bool success, string clearTimeText = null)
    {
        bossEncounterHud?.ShowResult(title, detail, success, clearTimeText);
    }

    private void UnsubscribeActiveBossHealth()
    {
        if (activeBossHealth != null)
        {
            activeBossHealth.OnDied -= HandleBossDied;
        }
    }

    private void ResolveReferences()
    {
        enemySpawnManager ??= FindFirstObjectByType<EnemySpawnManager>();
        player ??= FindFirstObjectByType<PlayerController>();
        bossEncounterHud ??= FindFirstObjectByType<BossEncounterHud>();
    }

    // 기준 레벨 초과분 1레벨당 perLevel 비율로 선형 증가하는 배수를 돌려준다.
    private float GetPlayerLevelScale(float perLevel)
    {
        if (!scaleBossByPlayerLevel)
        {
            return 1f;
        }

        int over = Mathf.Max(0, GetPlayerLevel() - Mathf.Max(1, scalingBaseLevel));
        return Mathf.Max(0.01f, 1f + perLevel * over);
    }

    private int GetPlayerLevel()
    {
        ResolveReferences();
        return player != null && player.Progression != null ? player.Progression.Level : 1;
    }
}
