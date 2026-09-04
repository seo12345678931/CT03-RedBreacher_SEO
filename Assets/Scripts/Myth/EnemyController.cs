using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CombatHealth))]
public class EnemyController : MonoBehaviour
{
    // 활성 적 레지스트리. 타깃 탐색이 FindObjectsByType(전 씬 스캔 + 배열 할당) 대신 이 리스트를 순회한다.
    private static readonly List<EnemyController> active = new List<EnemyController>();
    public static IReadOnlyList<EnemyController> Active => active;

    /// <summary>이 적의 CombatHealth(캐싱). 탐색 시마다 GetComponent 비용을 없앤다.</summary>
    public CombatHealth Health { get; private set; }

    // 도메인 리로드 비활성(Enter Play Mode Options) 환경에서도 이전 세션 잔여 항목을 제거한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();

    [SerializeField] private EnemyConfig enemyConfig;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stopDistance = 1.1f;
    [SerializeField] private float contactDamage = 5f;
    [SerializeField] private float contactInterval = 1f;
    [SerializeField] private float knockbackDamping = 10f;

    [Header("Death Effect")]
    [SerializeField] private GameObject deathExplosionEffectPrefab;
    [SerializeField] private float deathExplosionCleanupDelay = 0.6f;
    [SerializeField] private Vector3 deathExplosionOffset;
    [SerializeField] private float deathExplosionScale = 1f;

    private PlayerController targetPlayer;
    private Rigidbody body;
    private float nextContactTime;
    private Vector3 knockbackVelocity;
    private int enemyLevel = 1;
    private float healthMultiplier = 1f;
    private float moveSpeedMultiplier = 1f;
    private float contactDamageMultiplier = 1f;
    private float rewardMultiplier = 1f;

    protected float MoveSpeedValue => (enemyConfig != null ? enemyConfig.MoveSpeed : moveSpeed) * moveSpeedMultiplier;
    protected float StopDistanceValue => enemyConfig != null ? enemyConfig.StopDistance : stopDistance;
    private float ContactDamageValue => (enemyConfig != null ? enemyConfig.ContactDamage : contactDamage) * contactDamageMultiplier;
    private float ContactIntervalValue => enemyConfig != null ? enemyConfig.ContactInterval : contactInterval;
    public int EnemyLevel => enemyLevel;
    public float ExperienceReward => (enemyConfig != null ? enemyConfig.ExperienceReward : 10f) * rewardMultiplier;
    public int CreditReward => Mathf.RoundToInt((enemyConfig != null ? enemyConfig.CreditReward : 10) * rewardMultiplier);
    public int CoreCrystalReward => Mathf.RoundToInt((enemyConfig != null ? enemyConfig.CoreCrystalReward : 0) * rewardMultiplier);
    public float PartDropChance => enemyConfig != null ? enemyConfig.PartDropChance : 0.05f;
    public EnemyConfig CurrentConfig => enemyConfig;

    protected virtual void Awake()
    {
        Health = GetComponent<CombatHealth>();
        if (Health == null)
        {
            Health = gameObject.AddComponent<CombatHealth>();
        }

        EnsureEnemyComponents();

        // 적 스프라이트 모양을 그대로 사용하는 바닥 그림자를 생성한다.
        SpriteShapeShadow.Ensure(gameObject);
    }

    protected virtual void OnEnable()
    {
        if (!active.Contains(this))
        {
            active.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        active.Remove(this);
    }

    protected virtual void Start()
    {
        targetPlayer = FindFirstObjectByType<PlayerController>();
    }

    public void Initialize(EnemyConfig config)
    {
        Initialize(config, 1, 1f, 1f, 1f, 1f);
    }

    public void Initialize(
        EnemyConfig config,
        int level,
        float healthScale,
        float moveSpeedScale,
        float contactDamageScale,
        float rewardScale)
    {
        if (config != null)
        {
            enemyConfig = config;
        }

        enemyLevel = Mathf.Max(1, level);
        healthMultiplier = Mathf.Max(0.01f, healthScale);
        moveSpeedMultiplier = Mathf.Max(0.01f, moveSpeedScale);
        contactDamageMultiplier = Mathf.Max(0.01f, contactDamageScale);
        rewardMultiplier = Mathf.Max(0.01f, rewardScale);

        CombatHealth health = GetComponent<CombatHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<CombatHealth>();
        }

        if (enemyConfig != null)
        {
            health.Initialize(enemyConfig.MaxHealth * healthMultiplier);
        }
    }

    protected virtual void FixedUpdate()
    {
        CombatPlane.ClampTransform(transform);

        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<PlayerController>();
        }

        if (targetPlayer == null || targetPlayer.Health == null || targetPlayer.Health.IsDead)
        {
            StopBody();
            return;
        }

        if (UpdateKnockback())
        {
            return;
        }

        // 플레이어 방향으로 X/Z 평면에서만 천천히 접근한다.
        Vector3 currentPosition = CombatPlane.WithFixedY(transform.position);
        Vector3 targetPosition = CombatPlane.WithFixedY(targetPlayer.transform.position);
        Vector3 direction = CombatPlane.Direction(currentPosition, targetPosition);
        float distance = Vector3.Distance(currentPosition, targetPosition);
        if (direction.sqrMagnitude <= 0f)
        {
            StopBody();
            return;
        }

        if (distance <= StopDistanceValue)
        {
            // 플레이어와 겹치지 않도록 지정 거리에서 멈추고 거리 기반 공격을 처리한다.
            CombatPlane.SetYOnlyRotation(transform, direction);
            TryContactDamage(targetPlayer);
            StopBody();
            return;
        }

        float moveDistance = MoveSpeedValue * Time.fixedDeltaTime;
        float clampedMoveDistance = Mathf.Min(moveDistance, distance - StopDistanceValue);
        transform.position = CombatPlane.WithFixedY(Vector3.MoveTowards(currentPosition, targetPosition, clampedMoveDistance));
        CombatPlane.SetYOnlyRotation(transform, direction);
        StopBody();
    }

    public virtual void ApplyKnockback(Vector3 knockbackDirection, float knockbackForce)
    {
        Vector3 direction = CombatPlane.ProjectDirection(knockbackDirection);
        if (direction.sqrMagnitude <= 0f || knockbackForce <= 0f)
        {
            return;
        }

        // 같은 프레임에 여러 발을 맞으면 더 강한 쪽이 우선 느껴지게 누적한다.
        knockbackVelocity += direction * knockbackForce;
    }

    public void PlayDeathExplosionEffect()
    {
        GameObject effectPrefab = ResolveDeathExplosionEffectPrefab();
        if (effectPrefab == null)
        {
            return;
        }

        // 적 본체가 바로 제거되므로 이펙트는 월드에 독립 생성하고 풀로 회수한다.
        GameObject effect = CombatObjectPool.GetEffect(
            effectPrefab,
            transform.position + ResolveDeathExplosionOffset(effectPrefab),
            transform.rotation);

        if (effect == null)
        {
            return;
        }

        effect.transform.localScale = Vector3.one * ResolveDeathExplosionScale(effectPrefab);
        CombatObjectPool.ReleaseEffect(effect, ResolveDeathExplosionCleanupDelay(effectPrefab));
    }

    private GameObject ResolveDeathExplosionEffectPrefab()
    {
        return enemyConfig != null && enemyConfig.DeathExplosionEffectPrefab != null
            ? enemyConfig.DeathExplosionEffectPrefab
            : deathExplosionEffectPrefab;
    }

    private Vector3 ResolveDeathExplosionOffset(GameObject effectPrefab)
    {
        return enemyConfig != null && effectPrefab == enemyConfig.DeathExplosionEffectPrefab
            ? enemyConfig.DeathExplosionOffset
            : deathExplosionOffset;
    }

    private float ResolveDeathExplosionScale(GameObject effectPrefab)
    {
        return enemyConfig != null && effectPrefab == enemyConfig.DeathExplosionEffectPrefab
            ? enemyConfig.DeathExplosionScale
            : Mathf.Max(0.01f, deathExplosionScale);
    }

    private float ResolveDeathExplosionCleanupDelay(GameObject effectPrefab)
    {
        return enemyConfig != null && effectPrefab == enemyConfig.DeathExplosionEffectPrefab
            ? enemyConfig.DeathExplosionCleanupDelay
            : Mathf.Max(0f, deathExplosionCleanupDelay);
    }

    protected bool UpdateKnockback()
    {
        if (knockbackVelocity.sqrMagnitude <= 0.0001f)
        {
            knockbackVelocity = Vector3.zero;
            return false;
        }

        // 넉백 중에는 추적 이동을 잠시 멈추고 뒤로 밀린다.
        transform.position = CombatPlane.WithFixedY(transform.position + knockbackVelocity * Time.fixedDeltaTime);

        // 선형 감속 대신 지수 감쇠로 초반엔 확 튕기고 빠르게 멎는 "팝 후 정착" 손맛을 준다.
        knockbackVelocity *= Mathf.Exp(-knockbackDamping * Time.fixedDeltaTime);
        if (knockbackVelocity.sqrMagnitude <= 0.0001f)
        {
            knockbackVelocity = Vector3.zero;
        }

        StopBody();
        return true;
    }

    protected void EnsureEnemyComponents()
    {
        // 탑뷰 전투는 X/Z 평면만 쓰고 Y 높이는 0.1로 고정한다.
        CombatPlane.ClampTransform(transform);

        // 테스트용 적 오브젝트도 바로 이동/충돌 가능하도록 보강한다.
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        if (GetComponent<Collider>() == null)
        {
            SphereCollider enemyCollider = gameObject.AddComponent<SphereCollider>();
            // radius는 로컬 값이라 루트 스케일(예: Enemy01은 0.25)이 곱해지면 히트박스가 의도보다
            // 작아져 투사체가 빗나간다. 월드 기준 반경 0.4가 되도록 스케일을 보정한다.
            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            enemyCollider.radius = 0.4f / scale;
        }
    }

    protected void StopBody()
    {
        if (body == null)
        {
            return;
        }

        if (body.isKinematic)
        {
            return;
        }

        // 이동은 Transform 기준으로 처리하므로 동적 Rigidbody일 때만 속도를 제거한다.
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void TryContactDamage(PlayerController player)
    {
        if (player == null || player.Health == null || Time.time < nextContactTime)
        {
            return;
        }

        // 접촉 중에는 쿨타임마다 플레이어에게 피해를 준다.
        player.Health.TakeDamage(ContactDamageValue);
        nextContactTime = Time.time + ContactIntervalValue;
    }

    protected PlayerController ResolveTargetPlayer()
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<PlayerController>();
        }

        return targetPlayer;
    }
}
