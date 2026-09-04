using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    private BossEnemyConfig config;
    private Rigidbody body;
    private SphereCollider projectileCollider;
    private GameObject projectileEffectInstance;
    private float damage;
    private float expireTime;
    private float collisionRadius;
    private bool released;
    private int wallLayer;

    private void Awake()
    {
        EnsureComponents();
        wallLayer = LayerMask.NameToLayer("Wall");
    }

    private void Update()
    {
        CombatPlane.ClampTransform(transform);
        CombatPlane.ClampVelocity(body);
        CheckOverlapHit();

        if (!released && Time.time >= expireTime)
        {
            ReturnToPool();
        }
    }

    public void PrepareForReuse()
    {
        released = false;
        EnsureComponents();
    }

    public void Launch(BossEnemyConfig bossConfig, Vector3 direction, float attackDamage)
    {
        if (bossConfig == null)
        {
            ReturnToPool();
            return;
        }

        config = bossConfig;
        damage = Mathf.Max(0f, attackDamage);
        collisionRadius = config.ProjectileCollisionRadius;
        expireTime = Time.time + config.ProjectileLifetime;
        released = false;
        ApplyCollisionRadius();

        Vector3 launchDirection = CombatPlane.ProjectDirection(direction);
        if (launchDirection.sqrMagnitude <= 0f)
        {
            launchDirection = Vector3.forward;
        }

        transform.position = CombatPlane.WithFixedY(transform.position);
        transform.rotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        SpawnProjectileEffect();
        body.isKinematic = false;
        body.linearVelocity = launchDirection * config.ProjectileSpeed;
    }

    public void ResetForPool()
    {
        released = true;
        damage = 0f;
        expireTime = 0f;
        config = null;

        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (projectileEffectInstance != null)
        {
            CombatObjectPool.ReleaseEffect(projectileEffectInstance);
            projectileEffectInstance = null;
        }
    }

    private void EnsureComponents()
    {
        CombatPlane.ClampTransform(transform);
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        projectileCollider = GetComponent<SphereCollider>();
        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<SphereCollider>();
        }

        projectileCollider.isTrigger = true;
        ApplyCollisionRadius();
    }

    private void ApplyCollisionRadius()
    {
        if (projectileCollider != null)
        {
            projectileCollider.radius = Mathf.Max(0.01f, collisionRadius);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    private void CheckOverlapHit()
    {
        if (released)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.01f, collisionRadius));
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryHit(hits[i]))
            {
                return;
            }
        }
    }

    private bool TryHit(Collider hitCollider)
    {
        if (released || hitCollider == null)
        {
            return false;
        }

        if (wallLayer >= 0 && hitCollider.gameObject.layer == wallLayer)
        {
            SpawnHitEffect();
            ReturnToPool();
            return true;
        }

        PlayerController player = hitCollider.GetComponentInParent<PlayerController>();
        if (player == null || player.Health == null || player.Health.IsDead)
        {
            return false;
        }

        // 보스 발사체는 플레이어에게만 피해를 적용한다.
        player.Health.TakeDamage(damage);
        SpawnHitEffect();
        ReturnToPool();
        return true;
    }

    private void SpawnProjectileEffect()
    {
        if (config == null || config.ProjectileEffectPrefab == null)
        {
            return;
        }

        projectileEffectInstance = CombatObjectPool.GetEffect(
            config.ProjectileEffectPrefab,
            transform.position,
            transform.rotation,
            transform);
        projectileEffectInstance.transform.localPosition = Vector3.zero;
        projectileEffectInstance.transform.localRotation = Quaternion.identity;
        DisableExternalMotion(projectileEffectInstance);
    }

    private void SpawnHitEffect()
    {
        if (config == null || config.HitEffectPrefab == null)
        {
            return;
        }

        GameObject hitEffect = CombatObjectPool.GetEffect(
            config.HitEffectPrefab,
            transform.position,
            transform.rotation);
        CombatObjectPool.ReleaseEffect(hitEffect, config.EffectCleanupDelay);
    }

    private static void DisableExternalMotion(GameObject effect)
    {
        MonoBehaviour[] behaviours = effect.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            string typeName = behaviours[i].GetType().Name;
            if (typeName == "ProjectileMoveScript" || typeName == "HS_ProjectileMover")
            {
                behaviours[i].enabled = false;
            }
        }

        Rigidbody[] rigidbodies = effect.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (!rigidbodies[i].isKinematic)
            {
                rigidbodies[i].linearVelocity = Vector3.zero;
                rigidbodies[i].angularVelocity = Vector3.zero;
            }

            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
        }

        Collider[] colliders = effect.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void ReturnToPool()
    {
        if (!released)
        {
            CombatObjectPool.ReleaseBossProjectile(this);
        }
    }
}
