using UnityEngine;

public class MissileTurretSkill : MonoBehaviour
{
    private static readonly Quaternion SkillSpawnRotation = Quaternion.Euler(90f, 0f, 0f);

    private PlayerController owner;
    private PlayerSkillConfig config;
    private Transform firePoint;
    private AudioSource fireAudioSource;
    private CombatHealth currentTarget;
    private float expireTime;
    private float nextAttackTime;
    private float nextTargetSearchTime;
    private float aimAngleDeg;

    public static bool Spawn(PlayerController player, PlayerSkillConfig skillConfig, Vector3 targetPosition)
    {
        if (player == null
            || skillConfig == null
            || skillConfig.MissileTurretPrefab == null
            || skillConfig.MissileProjectileConfig == null)
        {
            return false;
        }

        Vector3 placementDirection = CombatPlane.Direction(player.transform.position, targetPosition);
        if (placementDirection.sqrMagnitude <= 0f)
        {
            placementDirection = CombatPlane.DirectionFromYRotation(player.transform);
        }

        int turretCount = skillConfig.GetTurretCount(player.GetSkillLevel(skillConfig));
        bool spawnedAny = false;
        for (int i = 0; i < turretCount; i++)
        {
            Vector3 placementPosition = GetPlacementPosition(
                player,
                skillConfig,
                placementDirection,
                i,
                turretCount);
            GameObject turretObject = Instantiate(skillConfig.MissileTurretPrefab, placementPosition, SkillSpawnRotation);
            turretObject.transform.position = placementPosition;
            turretObject.transform.rotation = SkillSpawnRotation;

            MissileTurretSkill turret = turretObject.GetComponent<MissileTurretSkill>();
            if (turret == null)
            {
                turret = turretObject.AddComponent<MissileTurretSkill>();
            }

            turret.Initialize(player, skillConfig);
            spawnedAny = true;
        }

        return spawnedAny;
    }

    private static Vector3 GetPlacementPosition(
        PlayerController player,
        PlayerSkillConfig skillConfig,
        Vector3 placementDirection,
        int index,
        int count)
    {
        if (count <= 1)
        {
            return CombatPlane.WithFixedY(
                player.transform.position + placementDirection * skillConfig.MissileTurretPlacementDistance);
        }

        float spreadAngle = Mathf.Min(120f, 30f * (count - 1));
        float offsetAngle = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, index / (float)(count - 1));
        Vector3 rotatedDirection = Quaternion.Euler(0f, offsetAngle, 0f) * placementDirection;
        return CombatPlane.WithFixedY(
            player.transform.position + rotatedDirection * skillConfig.MissileTurretPlacementDistance);
    }

    private void Initialize(PlayerController player, PlayerSkillConfig skillConfig)
    {
        owner = player;
        config = skillConfig;
        expireTime = Time.time + config.MissileTurretDuration;
        nextAttackTime = Time.time;
        firePoint = FindChildByName(transform, config.MissileTurretFirePointName);
        if (firePoint == null)
        {
            firePoint = transform;
        }

        fireAudioSource = ResolveFireAudioSource();
    }

    private void Update()
    {
        CombatPlane.ClampTransform(transform);
        if (owner == null
            || owner.Health == null
            || owner.Health.IsDead
            || config == null
            || Time.time >= expireTime)
        {
            Destroy(gameObject);
            return;
        }

        if (!HasValidTarget())
        {
            currentTarget = null;
            if (Time.time >= nextTargetSearchTime)
            {
                currentTarget = PlayerSkillCombat.FindClosestEnemy(transform.position, config.MissileTurretAttackRange);
                nextTargetSearchTime = currentTarget == null ? Time.time + 0.2f : 0f;
            }
        }

        if (currentTarget == null)
        {
            return;
        }

        RotateToTarget(currentTarget.transform.position);
        if (Time.time < nextAttackTime)
        {
            return;
        }

        Fire(currentTarget);
        nextAttackTime = Time.time + config.MissileTurretAttackInterval;
    }

    private bool HasValidTarget()
    {
        return currentTarget != null
            && !currentTarget.IsDead
            && CombatPlane.DistanceSqr(transform.position, currentTarget.transform.position)
                <= config.MissileTurretAttackRange * config.MissileTurretAttackRange;
    }

    private void RotateToTarget(Vector3 targetPosition)
    {
        Vector3 direction = CombatPlane.Direction(transform.position, targetPosition);
        if (direction.sqrMagnitude <= 0f)
        {
            return;
        }

        float targetAngle = CombatPlane.DirectionToZAngle(direction);
        aimAngleDeg = Mathf.MoveTowardsAngle(
            aimAngleDeg,
            targetAngle,
            config.MissileTurretRotationSpeed * Time.deltaTime);
        transform.rotation = SkillSpawnRotation * Quaternion.Euler(0f, 0f, aimAngleDeg);
    }

    private void Fire(CombatHealth target)
    {
        if (target == null)
        {
            return;
        }

        // 미사일 터렛도 일반 터렛처럼 PlayerProjectile을 사용하고, 충돌 시 범위 피해만 적용한다.
        Vector3 startPosition = firePoint == transform
            ? CombatPlane.WithFixedY(transform.position)
            : CombatPlane.PositionFromZPlaneChild(transform, firePoint, CombatPlane.Direction(transform.position, target.transform.position));
        Vector3 fireDirection = CombatPlane.Direction(startPosition, target.transform.position);
        if (fireDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        PlayFireSfx();

        ProjectileConfig projectileConfig = config.MissileProjectileConfig;
        float damage = PlayerSkillCombat.CalculateDamage(owner, config, out bool isCritical);
        PlayerProjectile projectile = CombatObjectPool.GetProjectile();
        projectile.transform.position = startPosition;
        projectile.Configure(projectileConfig);
        projectile.ConfigureRuntimeStats(projectileConfig.CollisionRadius, config.KnockbackForce);
        projectile.ConfigureRuntimeAreaStats(config.MissileExplosionRadius, 1f, config.MaxTargets);
        projectile.Launch(
            fireDirection,
            damage,
            projectileConfig.Speed,
            projectileConfig.Lifetime,
            owner.Health,
            isCritical);
    }

    private void PlayFireSfx()
    {
        AudioClip clip = config != null ? config.MissileTurretFireSfx : null;
        if (clip == null)
        {
            return;
        }

        fireAudioSource ??= ResolveFireAudioSource();
        if (fireAudioSource != null)
        {
            fireAudioSource.PlayOneShot(clip);
        }
    }

    private AudioSource ResolveFireAudioSource()
    {
        Transform sourceTransform = FindChildByName(transform, "Audio Source Fire");
        if (sourceTransform != null && sourceTransform.TryGetComponent(out AudioSource source))
        {
            return source;
        }

        // Audio Source Fire라는 이름이 안 맞는 경우를 대비
        return GetComponentInChildren<AudioSource>(true);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
