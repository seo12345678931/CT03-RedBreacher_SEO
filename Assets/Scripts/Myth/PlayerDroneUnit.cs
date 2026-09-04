using UnityEngine;

public class PlayerDroneUnit : MonoBehaviour
{
    private PlayerController player;
    private DroneConfig config;
    private Transform muzzle;
    private CombatHealth currentTarget;
    private AssemblyFactory assemblyFactory;
    private AudioSource fireSource;
    private int slotIndex;
    private int slotCount;
    private float nextAttackTime;
    private Vector3 followVelocity;

    public void Initialize(PlayerController owner, DroneConfig droneConfig, int index, int count, AudioSource droneFireSource)
    {
        player = owner;
        config = droneConfig;
        fireSource = droneFireSource;
        slotIndex = index;
        slotCount = Mathf.Max(1, count);
        muzzle = FindMuzzle(transform);
        assemblyFactory = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.AssemblyFactory
            : FindFirstObjectByType<AssemblyFactory>(FindObjectsInactive.Include);

        // 스폰 직후 곧바로 대형 슬롯에 위치시킨다. 이걸 안 하면 FollowPlayerSlot의 SmoothDamp가
        // 프리팹 스폰 지점(부모/원점)에서 대형까지 미끄러져 들어와, 드론이 잠깐 이상한 곳에서 튀어나온 것처럼 보인다.
        if (player != null && config != null)
        {
            transform.position = GetSlotPosition();
            followVelocity = Vector3.zero;
        }

        CombatPlane.ClampTransform(transform);

        // 드론 외형과 동일한 실루엣 그림자를 생성한다.
        SpriteShapeShadow.Ensure(gameObject);
    }

    private void Update()
    {
        if (player == null || config == null || player.Health == null || player.Health.IsDead)
        {
            return;
        }

        FollowPlayerSlot();

        if (!HasValidTarget())
        {
            currentTarget = FindClosestTarget();
        }

        Vector3 targetDirection = currentTarget != null && IsTargetInRange(currentTarget)
            ? CombatPlane.Direction(transform.position, currentTarget.transform.position)
            : Vector3.zero;

        if (targetDirection.sqrMagnitude > 0f)
        {
            // 사거리 안에 적이 있으면 적을 조준하고 사격한다.
            CombatPlane.RotateZOnlyToward(transform, targetDirection, config.RotationSpeed * Time.deltaTime);
            if (IsFacing(targetDirection) && Time.time >= nextAttackTime)
            {
                // 타겟 방향이 아니라 실제 기수 방향으로 발사한다(탱크 터렛과 동일).
                // 타겟 방향으로 쏘면 정렬이 끝나기 전(스폰 직후 등) 탄이 기수에서
                // 게이트 각도만큼 꺾여 나가 이상한 곳으로 튀는 것처럼 보인다.
                Vector3 noseDirection = CombatPlane.DirectionFromZRotation(transform);
                Fire(noseDirection.sqrMagnitude > 0f ? noseDirection : targetDirection);
                nextAttackTime = Time.time + GetAttackInterval();
            }
            return;
        }

        // 적이 없으면 플레이어처럼 자신이 이동하는 방향을 바라본다.
        FaceMovementDirection();
    }

    private void FaceMovementDirection()
    {
        // SmoothDamp가 산출한 추적 속도를 이동 방향으로 사용한다. 거의 멈춰 있으면 현재 방향을 유지한다.
        if (followVelocity.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector3 moveDirection = CombatPlane.ProjectDirection(followVelocity);
        if (moveDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        CombatPlane.RotateZOnlyToward(transform, moveDirection, config.RotationSpeed * Time.deltaTime);
    }

    private void FollowPlayerSlot()
    {
        Vector3 targetPosition = GetSlotPosition();
        CoreCharger coreCharger = GetCoreCharger();
        float followSpeedBonus = coreCharger != null ? coreCharger.DroneFollowSpeedBonus : 0f;

        // SmoothDamp로 관성 있는 추적을 만들어 별개 유닛처럼 트레일링하게 한다.
        // FollowSpeed 보너스가 높을수록 지연 시간을 줄여 더 빠르게 따라온다.
        float responsiveness = Mathf.Max(0.01f, config.FollowSpeed + followSpeedBonus);
        float smoothTime = config.FollowSmoothTime * (config.FollowSpeed / responsiveness);
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, smoothTime);
        transform.position = CombatPlane.WithFixedY(smoothed);
    }

    private Vector3 GetSlotPosition()
    {
        float angleDegrees;
        if (slotCount == 1)
        {
            // 드론이 1개일 때는 플레이어의 바로 뒤(180도)에 배치
            angleDegrees = 180f;
        }
        else
        {
            // 드론이 2개 이상일 때는 플레이어의 좌측 양옆(90도)부터 우측 양옆(270도)까지 뒤쪽 반원에 걸쳐 고르게 분배
            float progress = (float)slotIndex / (slotCount - 1);
            angleDegrees = Mathf.Lerp(90f, 270f, progress);
        }

        // 플레이어 회전과 무관하게 월드 기준 고정 방향으로 배치하여 별개의 유닛처럼 움직이게 한다.
        float finalAngle = angleDegrees * Mathf.Deg2Rad;

        // X/Z 평면 기준 오프셋 계산 (Z는 정면, X는 오른쪽)
        Vector3 offset = new Vector3(Mathf.Sin(finalAngle), 0f, Mathf.Cos(finalAngle)) * config.FollowRadius;
        return CombatPlane.WithFixedY(player.transform.position + offset);
    }

    private bool HasValidTarget()
    {
        return currentTarget != null && !currentTarget.IsDead && IsTargetInRange(currentTarget);
    }

    private CombatHealth FindClosestTarget()
    {
        CombatHealth closestTarget = null;
        float closestDistanceSqr = float.PositiveInfinity;

        // 드론은 플레이어와 별도로 가장 가까운 적을 탐색한다. (전 씬 스캔 대신 적 레지스트리 순회)
        var enemies = EnemyController.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            closestTarget = SelectCloserTarget(enemy.Health, closestTarget, ref closestDistanceSqr);
        }

        return closestTarget;
    }

    private CombatHealth SelectCloserTarget(CombatHealth target, CombatHealth currentClosest, ref float closestDistanceSqr)
    {
        if (target == null || target.IsDead || target == player.Health)
        {
            return currentClosest;
        }

        LayerMask targetMask = config.TargetMask;
        if (targetMask.value != 0 && ((1 << target.gameObject.layer) & targetMask.value) == 0)
        {
            return currentClosest;
        }

        float distanceSqr = CombatPlane.DistanceSqr(transform.position, target.transform.position);
        float attackRange = GetAttackRange();
        if (distanceSqr > closestDistanceSqr || distanceSqr > attackRange * attackRange)
        {
            return currentClosest;
        }

        closestDistanceSqr = distanceSqr;
        return target;
    }

    private bool IsTargetInRange(CombatHealth target)
    {
        float attackRange = GetAttackRange();
        return target != null && CombatPlane.DistanceSqr(transform.position, target.transform.position) <= attackRange * attackRange;
    }

    private bool IsFacing(Vector3 direction)
    {
        Vector3 forward = CombatPlane.DirectionFromZRotation(transform);
        return forward.sqrMagnitude > 0f && Vector3.Angle(forward, direction) <= config.FireAngleTolerance;
    }

    private void Fire(Vector3 direction)
    {
        PlayerProjectile projectile = CombatObjectPool.GetProjectile();
        ProjectileConfig projectileConfig = config.ProjectileConfig;
        projectile.transform.position = GetFirePosition(direction);
        projectile.Configure(projectileConfig);
        projectile.Launch(direction, GetDamage(projectileConfig), config.ProjectileSpeed, config.ProjectileLifetime, player.Health);
        PlayFireSfx(projectileConfig);
    }

    // 무기 SO에 있는 sfxFire를 연결해서 오디오 소스를(droneFireSource) 연결해 출력한다.
    private void PlayFireSfx(ProjectileConfig projectileConfig)
    {
        AudioClip clip = projectileConfig != null ? projectileConfig.SfxFire : null;
        if (fireSource == null || clip == null)
        {
            return;
        }

        fireSource.PlayOneShot(clip);
    }

    private float GetDamage(ProjectileConfig projectileConfig)
    {
        float assemblyBonus = assemblyFactory != null
            ? assemblyFactory.GetDroneAttackDamageBonus(config)
            : 0f;
        return config.AttackDamage
            + assemblyBonus
            + (projectileConfig != null ? projectileConfig.AttackDamage : 0f)
            + (GetCoreCharger()?.DroneAttackDamageBonus ?? 0f);
    }

    private float GetAttackRange()
    {
        return Mathf.Max(0f, config.AttackRange + (GetCoreCharger()?.DroneAttackRangeBonus ?? 0f));
    }

    private float GetAttackInterval()
    {
        return Mathf.Max(
            0.01f,
            config.AttackInterval - (GetCoreCharger()?.DroneAttackIntervalReduction ?? 0f));
    }

    private static CoreCharger GetCoreCharger()
    {
        return BaseCampManager.Instance != null ? BaseCampManager.Instance.CoreCharger : null;
    }

    private Vector3 GetFirePosition(Vector3 direction)
    {
        if (muzzle != null)
        {
            return CombatPlane.WithFixedY(muzzle.position);
        }

        return CombatPlane.WithFixedY(transform.position + direction * 0.25f);
    }

    private Transform FindMuzzle(Transform root)
    {
        if (root == null || config == null)
        {
            return null;
        }

        string prefix = string.IsNullOrWhiteSpace(config.MuzzleNamePrefix) ? "FireMuzzle" : config.MuzzleNamePrefix;
        if (root.name.StartsWith(prefix))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindMuzzle(root.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
