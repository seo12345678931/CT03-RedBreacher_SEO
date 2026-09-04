using UnityEngine;

public class AutoTurretSkill : MonoBehaviour
{
    private static readonly Quaternion SkillSpawnRotation = Quaternion.Euler(90f, 0f, 0f);

    private PlayerController owner;
    private PlayerSkillConfig config;
    private Transform firePoint;
    private AudioSource fireAudioSource;
    private float aimAngleDeg;
    private CombatHealth currentTarget;
    private float expireTime;
    private float nextAttackTime;
    private float nextTargetSearchTime;
    private Vector3 _originalScale;
    private float _spawnTime;
    private bool _isExpiring;
    private const float _popDuration = 0.3f;

    public static bool Spawn(
        PlayerController player,
        PlayerSkillConfig skillConfig,
        Vector3 targetPosition)
    {
        if (player == null || skillConfig == null)
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
            GameObject turretObject = skillConfig.TurretPrefab != null
                ? Instantiate(skillConfig.TurretPrefab, placementPosition, SkillSpawnRotation)
                : new GameObject($"AutoTurret_{skillConfig.Id}");
            turretObject.transform.position = placementPosition;
            // 탑뷰 스프라이트가 바닥을 향하도록 생성 시 X축을 90도로 고정한다.
            turretObject.transform.rotation = SkillSpawnRotation;

            AutoTurretSkill turret = turretObject.GetComponent<AutoTurretSkill>();
            if (turret == null)
            {
                turret = turretObject.AddComponent<AutoTurretSkill>();
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
                player.transform.position + placementDirection * skillConfig.TurretPlacementDistance);
        }

        // 여러 포탑은 진행 방향을 기준으로 부채꼴 배치해 서로 겹치지 않게 한다.
        float spreadAngle = Mathf.Min(120f, 30f * (count - 1));
        float offsetAngle = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, index / (float)(count - 1));
        Vector3 rotatedDirection = Quaternion.Euler(0f, offsetAngle, 0f) * placementDirection;
        return CombatPlane.WithFixedY(
            player.transform.position + rotatedDirection * skillConfig.TurretPlacementDistance);
    }

    private void Initialize(PlayerController player, PlayerSkillConfig skillConfig)
    {
        owner = player;
        config = skillConfig;
        expireTime = Time.time + config.TurretDuration;
        nextAttackTime = Time.time;
        firePoint = FindChildByName(transform, config.TurretFirePointName);
        if (firePoint == null)
        {
            firePoint = transform;
        }

        fireAudioSource = ResolveFireAudioSource();

        _originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
        _spawnTime = Time.time;
        _isExpiring = false;
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

        // 1. 팝인 / 팝아웃 스케일 애니메이션 처리
        float elapsed = Time.time - _spawnTime;
        float timeLeft = expireTime - Time.time;

        if (timeLeft <= _popDuration)
        {
            _isExpiring = true;
            float progress = Mathf.Clamp01(timeLeft / _popDuration);
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, progress);
        }
        else if (elapsed < _popDuration)
        {
            float progress = Mathf.Clamp01(elapsed / _popDuration);
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, progress);
        }
        else if (!_isExpiring)
        {
            transform.localScale = _originalScale;
        }

        // 사라지는 중(팝아웃)에는 타겟 추적 및 공격을 중단함
        if (_isExpiring)
        {
            return;
        }

        if (!HasValidTarget())
        {
            currentTarget = null;
            if (Time.time >= nextTargetSearchTime)
            {
                currentTarget = PlayerSkillCombat.FindClosestEnemy(
                    transform.position,
                    config.TurretAttackRange);
                nextTargetSearchTime = currentTarget == null ? Time.time + 0.2f : 0f;
            }
        }

        if (currentTarget == null)
        {
            return;
        }

        // 회전하는 FirePoint가 아니라 터렛 중심을 기준으로 고정된 조준 방향을 계산한다.
        Vector3 direction = CombatPlane.Direction(
            transform.position,
            currentTarget.transform.position);
        if (direction.sqrMagnitude <= 0f)
        {
            return;
        }

        // Turret1 프리팹은 평면 구조(모든 시각 파츠가 X=90 루트의 직속 자식)라 회전시킬 별도 헤드가 없다.
        // X=90 루트를 localEulerAngles로 돌리면 짐벌 락으로 튀므로, 조준 각도를 필드로 추적해
        // 절대 회전(SkillSpawnRotation * Z회전)으로 적용한다. → 루트 아래 모든 시각 파츠가 함께 회전.
        float targetAngle = CombatPlane.DirectionToZAngle(direction);
        aimAngleDeg = Mathf.MoveTowardsAngle(
            aimAngleDeg,
            targetAngle,
            config.TurretRotationSpeed * Time.deltaTime);
        transform.rotation = SkillSpawnRotation * Quaternion.Euler(0f, 0f, aimAngleDeg);

        if (Time.time < nextAttackTime)
        {
            return;
        }

        // 정렬 중에도 발사하되 투사체는 항상 현재 터렛 정면으로 나간다.
        Fire(currentTarget);
        nextAttackTime = Time.time + config.TurretAttackInterval;
    }

    private bool HasValidTarget()
    {
        return currentTarget != null
            && !currentTarget.IsDead
            && CombatPlane.DistanceSqr(transform.position, currentTarget.transform.position)
                <= config.TurretAttackRange * config.TurretAttackRange;
    }

    private void Fire(CombatHealth target)
    {
        PlayFireSfx();

        float damage = PlayerSkillCombat.CalculateDamage(owner, config, out bool isCritical);
        ProjectileConfig projectileConfig = config.TurretProjectileConfig;
        if (projectileConfig == null)
        {
            target.TakeDamage(damage, isCritical);
            CombatRewardService.GrantIfKilled(owner, target);
            return;
        }

        // 발사 방향은 추적 중인 조준 각도에서 직접 구한다(시각 회전과 동일 소스 → 항상 일직선).
        float aimRad = aimAngleDeg * Mathf.Deg2Rad;
        Vector3 fireDirection = new Vector3(-Mathf.Sin(aimRad), 0f, Mathf.Cos(aimRad));
        if (fireDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        PlayerProjectile projectile = CombatObjectPool.GetProjectile();
        // FirePoint 로컬 좌표를 루트 기준으로 실제 X/Z 발사 위치로 변환한다.
        projectile.transform.position = firePoint == transform
            ? CombatPlane.WithFixedY(transform.position)
            : CombatPlane.PositionFromZPlaneChild(transform, firePoint, fireDirection);
        projectile.Configure(projectileConfig);
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
        AudioClip clip = config != null ? config.TurretFireSfx : null;
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

        // Audio Source Fire이라는 이름이 틀려도 여전히 동작하도록 fallback을 넣음.
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
