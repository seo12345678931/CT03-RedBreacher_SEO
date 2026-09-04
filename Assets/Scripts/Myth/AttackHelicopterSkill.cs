using System.Collections;
using UnityEngine;

public class AttackHelicopterSkill : MonoBehaviour
{
    private const string RocketLaunchAudioSourceName = "Audio Source RocketLunch";
    private const string FirstRocketLaunchAudioSourceName = "Audio Source_SFX SkillRocket";

    // 전투 중 조준 회전 속도(초당 도). 타겟이 바뀔 때 확 도는 대신 헬기처럼 서서히 선회하게 한다.
    private const float TurnSpeedDegreesPerSecond = 200f;

    private PlayerController owner;
    private PlayerSkillConfig config;
    private GameObject helicopterVisual;
    private AudioSource rocketLaunchAudioSource;
    private AudioSource firstRocketLaunchAudioSource;
    private CombatHealth currentTarget;
    private float expireTime;
    private float nextAttackTime;
    private float nextTargetSearchTime;
    private bool leaving;
    private bool entering;
    private Vector3 lastFacing = Vector3.forward;

    public static bool Spawn(PlayerController player, PlayerSkillConfig skillConfig, Vector3 targetPosition)
    {
        if (player == null
            || skillConfig == null
            || skillConfig.AttackHelicopterPrefab == null)
        {
            return false;
        }

        GameObject skillObject = new GameObject($"AttackHelicopter_{skillConfig.Id}");
        AttackHelicopterSkill helicopter = skillObject.AddComponent<AttackHelicopterSkill>();

        helicopter.Initialize(player, skillConfig, targetPosition);
        return true;
    }

    private void Initialize(PlayerController player, PlayerSkillConfig skillConfig, Vector3 targetPosition)
    {
        owner = player;
        config = skillConfig;
        expireTime = float.PositiveInfinity;
        nextAttackTime = Time.time + 0.7f;
        leaving = false;
        transform.position = GetSpawnPosition(owner, config);

        // 헬기가 보이기 전에 목표 주변에 로켓을 먼저 떨어뜨려 지원 등장을 예고한다.
        // 로켓이 실제로 착탄한 뒤 헬기가 등장하도록 비행시간 기준으로 등장 지연을 잡는다.
        float openingTime = 0.35f;
        if (config.HelicopterRocketPrefab != null)
        {
            for (int i = 0; i < config.HelicopterOpeningRocketCount; i++)
            {
                Vector3 impact = CombatPlane.WithFixedY(targetPosition + GetOpeningRocketOffset(i, config.HelicopterOpeningRocketCount));
                float launchDelay = i * 0.15f;
                StartCoroutine(LaunchRocket(transform.position, impact, launchDelay));

                float flightTime = Mathf.Sqrt(CombatPlane.DistanceSqr(transform.position, impact)) / config.HelicopterRocketSpeed;
                openingTime = Mathf.Max(openingTime, launchDelay + flightTime);
            }
        }

        StartCoroutine(SpawnHelicopterAfterOpening(openingTime + 0.1f));
    }

    private void Update()
    {
        if (owner == null || owner.Health == null || owner.Health.IsDead || config == null)
        {
            if (helicopterVisual != null)
            {
                Destroy(helicopterVisual);
            }

            Destroy(gameObject);
            return;
        }

        if (helicopterVisual == null)
        {
            return;
        }

        if (entering)
        {
            // 진입 연출 중에는 EntryRoutine이 이동을 제어한다.
            transform.position = helicopterVisual.transform.position;
            return;
        }

        if (Time.time >= expireTime)
        {
            LeaveAndDestroy();
            return;
        }

        AcquireTarget();
        MoveAndFace();
        TryAttack();
        transform.position = helicopterVisual.transform.position;
    }

    // 플레이어 위치/회전에 종속되지 않고, 스스로 타겟을 찾아 움직이는 독립 유닛처럼 동작한다.
    private void AcquireTarget()
    {
        if (HasValidTarget())
        {
            return;
        }

        currentTarget = null;
        if (Time.time >= nextTargetSearchTime)
        {
            currentTarget = PlayerSkillCombat.FindClosestEnemy(helicopterVisual.transform.position, config.AttackHelicopterAttackRange);
            nextTargetSearchTime = currentTarget == null ? Time.time + 0.2f : 0f;
        }
    }

    private void MoveAndFace()
    {
        Vector3 heliPosition = helicopterVisual.transform.position;
        Vector3 playerPosition = CombatPlane.WithFixedY(owner.transform.position);
        float leash = config.AttackHelicopterMaxDistanceFromPlayer;

        Vector3 destination = heliPosition;
        Vector3 faceDir = Vector3.zero;

        if (currentTarget != null)
        {
            Vector3 toTarget = CombatPlane.Direction(heliPosition, currentTarget.transform.position);
            if (toTarget.sqrMagnitude > 0f)
            {
                // 사거리의 일정 비율까지 접근 후 정지(스탠드오프)하되, 플레이어 범위를 벗어나는 추격은 막는다.
                float engageDistance = config.AttackHelicopterAttackRange * 0.7f;
                float distance = Mathf.Sqrt(CombatPlane.DistanceSqr(heliPosition, currentTarget.transform.position));
                Vector3 standoff = distance > engageDistance
                    ? CombatPlane.WithFixedY(currentTarget.transform.position - toTarget * engageDistance)
                    : heliPosition;
                destination = ClampToRadius(standoff, playerPosition, leash);
                faceDir = toTarget;
            }
        }
        else if (CombatPlane.DistanceSqr(heliPosition, playerPosition) > (leash * 0.8f) * (leash * 0.8f))
        {
            // 타겟이 없고 플레이어와 멀어졌으면 범위 안으로 복귀한다.
            destination = playerPosition;
            faceDir = CombatPlane.Direction(heliPosition, playerPosition);
        }

        helicopterVisual.transform.position = Vector3.MoveTowards(
            heliPosition,
            destination,
            config.AttackHelicopterMoveSpeed * Time.deltaTime);

        // 어떤 경우에도 플레이어 기준 leash 밖으로 나가지 않게 최종 제한(플레이어가 빨리 멀어져도 가장자리에 붙어 따라감).
        helicopterVisual.transform.position = ClampToRadius(
            helicopterVisual.transform.position, playerPosition, leash);

        if (faceDir.sqrMagnitude > 0f)
        {
            lastFacing = faceDir;
            // 즉시 스냅 대신 회전 속도 제한을 둬 자연스럽게 선회시킨다.
            helicopterVisual.transform.rotation = Quaternion.RotateTowards(
                helicopterVisual.transform.rotation,
                FaceRotation(faceDir),
                TurnSpeedDegreesPerSecond * Time.deltaTime);
        }
    }

    private static Vector3 ClampToRadius(Vector3 position, Vector3 center, float radius)
    {
        Vector3 offset = position - center;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance > radius && distance > 0.0001f)
        {
            position = center + offset * (radius / distance);
        }

        return CombatPlane.WithFixedY(position);
    }

    private void TryAttack()
    {
        if (currentTarget == null || Time.time < nextAttackTime)
        {
            return;
        }

        FireProjectile(currentTarget);
        nextAttackTime = Time.time + config.AttackHelicopterAttackInterval;
    }

    // 헬기 공격도 일반/미사일 터렛처럼 ProjectileConfig 기반 PlayerProjectile을 사용한다.
    private void FireProjectile(CombatHealth target)
    {
        ProjectileConfig projectileConfig = config.HelicopterProjectileConfig;
        if (projectileConfig == null || target == null)
        {
            return;
        }

        Vector3 startPosition = CombatPlane.WithFixedY(helicopterVisual.transform.position);
        Vector3 fireDirection = CombatPlane.Direction(startPosition, target.transform.position);
        if (fireDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        PlayRocketLaunchSfx();

        float damage = PlayerSkillCombat.CalculateDamage(owner, config, out bool isCritical);
        PlayerProjectile projectile = CombatObjectPool.GetProjectile();
        projectile.transform.position = startPosition;
        projectile.Configure(projectileConfig);
        projectile.ConfigureRuntimeStats(projectileConfig.CollisionRadius, config.KnockbackForce);
        projectile.ConfigureRuntimeAreaStats(config.HelicopterRocketRadius, 1f, config.MaxTargets);
        projectile.Launch(
            fireDirection,
            damage,
            projectileConfig.Speed,
            projectileConfig.Lifetime,
            owner.Health,
            isCritical);
    }

    private void PlayRocketLaunchSfx()
    {
        AudioClip clip = config != null ? config.HelicopterRocketLunchSfx : null;
        if (clip == null)
        {
            return;
        }

        rocketLaunchAudioSource ??= ResolveRocketLaunchAudioSource();
        if (rocketLaunchAudioSource != null)
        {
            rocketLaunchAudioSource.PlayOneShot(clip);
        }
    }

    private AudioSource ResolveRocketLaunchAudioSource()
    {
        if (helicopterVisual == null)
        {
            return null;
        }

        Transform sourceTransform = FindChildByName(helicopterVisual.transform, RocketLaunchAudioSourceName);
        if (sourceTransform != null && sourceTransform.TryGetComponent(out AudioSource source))
        {
            return source;
        }

        return helicopterVisual.GetComponentInChildren<AudioSource>(true);
    }

    private void PlayFirstRocketLaunchSfx()
    {
        AudioClip clip = config != null ? config.FirstRocketLunchSfx : null;
        if (clip == null)
        {
            return;
        }

        firstRocketLaunchAudioSource ??= ResolveFirstRocketLaunchAudioSource();
        if (firstRocketLaunchAudioSource != null)
        {
            firstRocketLaunchAudioSource.PlayOneShot(clip);
        }
    }

    private AudioSource ResolveFirstRocketLaunchAudioSource()
    {
        Transform sourceRoot = owner != null ? owner.transform : null;
        Transform sourceTransform = FindChildByName(sourceRoot, FirstRocketLaunchAudioSourceName);
        if (sourceTransform != null && sourceTransform.TryGetComponent(out AudioSource source))
        {
            source.spatialBlend = 0f;
            return source;
        }

        return null;
    }

    private bool HasValidTarget()
    {
        return currentTarget != null
            && !currentTarget.IsDead
            && helicopterVisual != null
            && CombatPlane.DistanceSqr(helicopterVisual.transform.position, currentTarget.transform.position)
                <= config.AttackHelicopterAttackRange * config.AttackHelicopterAttackRange;
    }

    private IEnumerator SpawnHelicopterAfterOpening(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        if (owner == null || owner.Health == null || owner.Health.IsDead || config == null)
        {
            Destroy(gameObject);
            yield break;
        }

        // 화면 밖 먼 곳에서 등장해 운용 위치(플레이어 옆)로 날아 들어온다.
        Vector3 operatingPosition = GetSpawnPosition(owner, config);
        Vector3 playerPosition = CombatPlane.WithFixedY(owner.transform.position);
        Vector3 entryDirection = CombatPlane.Direction(playerPosition, operatingPosition);
        if (entryDirection.sqrMagnitude <= 0f)
        {
            entryDirection = Vector3.right;
        }

        Vector3 entryStart = CombatPlane.WithFixedY(playerPosition + entryDirection * 30f);
        helicopterVisual = Instantiate(config.AttackHelicopterPrefab, entryStart, FaceRotation(-entryDirection));
        transform.position = helicopterVisual.transform.position;
        lastFacing = -entryDirection;
        rocketLaunchAudioSource = ResolveRocketLaunchAudioSource();
        EnsureRotorSpin(helicopterVisual);

        // 진입이 끝난 뒤부터 체류시간을 센다.
        expireTime = float.PositiveInfinity;
        entering = true;
        StartCoroutine(EntryRoutine());
    }

    private IEnumerator EntryRoutine()
    {
        float entrySpeed = Mathf.Max(config.AttackHelicopterMoveSpeed * 2.5f, 14f);
        while (helicopterVisual != null)
        {
            if (owner == null || owner.Health == null || owner.Health.IsDead)
            {
                Destroy(helicopterVisual);
                Destroy(gameObject);
                yield break;
            }

            Vector3 position = helicopterVisual.transform.position;
            Vector3 destination = GetSpawnPosition(owner, config); // 플레이어 이동 반영
            Vector3 direction = CombatPlane.Direction(position, destination);
            if (direction.sqrMagnitude > 0f)
            {
                lastFacing = direction;
                helicopterVisual.transform.rotation = FaceRotation(direction);
            }

            helicopterVisual.transform.position = Vector3.MoveTowards(position, destination, entrySpeed * Time.deltaTime);
            transform.position = helicopterVisual.transform.position;

            if (CombatPlane.DistanceSqr(helicopterVisual.transform.position, destination) <= 0.25f)
            {
                break;
            }

            yield return null;
        }

        entering = false;
        expireTime = Time.time + config.AttackHelicopterDuration;
    }

    private IEnumerator LaunchRocket(Vector3 startPosition, Vector3 impactPosition, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (config == null)
        {
            yield break;
        }

        GameObject rocket = Instantiate(config.HelicopterRocketPrefab, startPosition, Quaternion.identity);
        rocket.transform.position = startPosition;
        PlayFirstRocketLaunchSfx();

        Vector3 direction = CombatPlane.Direction(startPosition, impactPosition);
        if (direction.sqrMagnitude > 0f)
        {
            rocket.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        float distance = Mathf.Sqrt(CombatPlane.DistanceSqr(startPosition, impactPosition));
        float duration = distance / config.HelicopterRocketSpeed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (rocket == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            rocket.transform.position = CombatPlane.WithFixedY(Vector3.Lerp(startPosition, impactPosition, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        if (rocket != null)
        {
            Destroy(rocket);
        }

        if (owner != null && owner.Health != null && !owner.Health.IsDead)
        {
            float damage = PlayerSkillCombat.CalculateDamage(owner, config, out bool isCritical);
            PlayerSkillCombat.ApplyAreaDamage(
                owner,
                impactPosition,
                config.HelicopterRocketRadius,
                damage,
                config.MaxTargets,
                config.KnockbackForce,
                isCritical);
        }
    }

    private void LeaveAndDestroy()
    {
        if (leaving)
        {
            return;
        }

        leaving = true;
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        // 자신이 향하던 방향으로, 화면 밖까지 충분히 멀리 빠르게 날아간 뒤 사라진다(사라짐이 화면에 안 보이게).
        Vector3 forward = lastFacing.sqrMagnitude > 0f ? lastFacing.normalized : Vector3.forward;
        Transform visualTransform = helicopterVisual != null ? helicopterVisual.transform : transform;
        visualTransform.rotation = FaceRotation(forward);

        const float exitTravel = 30f; // 화면 밖까지 충분한 거리
        float exitSpeed = Mathf.Max(config.AttackHelicopterMoveSpeed * 2.5f, 14f);
        float travelled = 0f;
        float safetyTimeout = 4f;
        float elapsed = 0f;

        while (travelled < exitTravel && elapsed < safetyTimeout)
        {
            if (helicopterVisual == null)
            {
                break;
            }

            float step = exitSpeed * Time.deltaTime;
            visualTransform.position = CombatPlane.WithFixedY(visualTransform.position + forward * step);
            transform.position = visualTransform.position;
            travelled += step;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (helicopterVisual != null)
        {
            Destroy(helicopterVisual);
        }

        Destroy(gameObject);
    }

    private static Vector3 GetSpawnPosition(PlayerController player, PlayerSkillConfig skillConfig)
    {
        Vector3 forward = CombatPlane.DirectionFromYRotation(player.transform);
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        if (side.sqrMagnitude <= 0f)
        {
            side = Vector3.right;
        }

        return CombatPlane.WithFixedY(player.transform.position + side * skillConfig.AttackHelicopterFollowOffset);
    }

    private static Quaternion GetHelicopterRotation(PlayerController player)
    {
        Vector3 forward = CombatPlane.DirectionFromYRotation(player.transform);
        if (forward.sqrMagnitude <= 0f)
        {
            forward = Vector3.forward;
        }

        return FaceRotation(forward);
    }

    private static Quaternion FaceRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    private void EnsureRotorSpin(GameObject visual)
    {
        if (visual == null || string.IsNullOrWhiteSpace(config.HelicopterRotorName))
        {
            return;
        }

        Transform rotor = FindChildByName(visual.transform, config.HelicopterRotorName);
        if (rotor == null)
        {
            return;
        }

        RotorSpin spin = rotor.GetComponent<RotorSpin>();
        if (spin == null)
        {
            spin = rotor.gameObject.AddComponent<RotorSpin>();
        }

        spin.Configure(config.HelicopterRotorSpeed);
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

    private static Vector3 GetOpeningRocketOffset(int index, int count)
    {
        if (count <= 1)
        {
            return Vector3.zero;
        }

        float side = index - (count - 1) * 0.5f;
        return new Vector3(side * 1.5f, 0f, 0f);
    }
}
