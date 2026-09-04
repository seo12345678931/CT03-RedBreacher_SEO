using System.Collections;
using UnityEngine;

public class StealthBomberSkill : MonoBehaviour
{
    private static readonly Quaternion GroundEffectRotation = Quaternion.Euler(90f, 0f, 0f);
    private const string ImpactAudioSourceName = "Audio Source_SFX Impact";

    private PlayerController owner;
    private PlayerSkillConfig config;
    private AudioSource impactAudioSource;
    private Vector3 targetCenter;
    private Vector3 flightDirection;

    public static bool Cast(PlayerController player, PlayerSkillConfig skillConfig, Vector3 targetPosition)
    {
        if (player == null || skillConfig == null || skillConfig.StealthBomberPrefab == null)
        {
            return false;
        }

        GameObject skillObject = new GameObject($"StealthBomber_{skillConfig.Id}");
        StealthBomberSkill skill = skillObject.AddComponent<StealthBomberSkill>();
        skill.owner = player;
        skill.config = skillConfig;
        skill.targetCenter = CombatPlane.WithFixedY(targetPosition);
        skill.flightDirection = CombatPlane.Direction(player.transform.position, targetPosition);
        if (skill.flightDirection.sqrMagnitude <= 0f)
        {
            skill.flightDirection = CombatPlane.DirectionFromYRotation(player.transform);
        }

        skill.StartCoroutine(skill.Execute());
        return true;
    }

    private IEnumerator Execute()
    {
        Vector3 startPosition = targetCenter - flightDirection * config.StealthBomberTravelDistance;
        Vector3 endPosition = targetCenter + flightDirection * config.StealthBomberTravelDistance;
        startPosition.y = config.StealthBomberHeight;
        endPosition.y = config.StealthBomberHeight;

        GameObject bomber = Instantiate(config.StealthBomberPrefab, startPosition, GetFlightRotation());
        bomber.transform.position = startPosition;
        bomber.transform.rotation = GetFlightRotation();

        // 비행체를 따라다니는 짙은 바닥 그림자(스프라이트 미지정 시 절차적 생성).
        GroundShadowDecal shadow = GroundShadowDecal.Create(
            bomber.transform,
            config.StealthBomberShadowSize,
            config.StealthBomberShadowDarkness,
            -10,
            config.StealthBomberShadowSprite);

        StartCoroutine(MoveBomber(bomber, startPosition, endPosition));

        Vector3[] impactPoints = BuildImpactPoints();
        for (int i = 0; i < impactPoints.Length; i++)
        {
            StartCoroutine(StrikePoint(impactPoints[i], i * config.StealthBomberBombInterval));
        }

        float flightDuration = Vector3.Distance(startPosition, endPosition) / config.StealthBomberSpeed;
        float strikeDuration = Mathf.Max(0f, (impactPoints.Length - 1) * config.StealthBomberBombInterval)
            + config.ImpactDelay
            + 0.2f;
        yield return new WaitForSeconds(Mathf.Max(flightDuration, strikeDuration));

        if (shadow != null)
        {
            Destroy(shadow.gameObject);
        }

        if (bomber != null)
        {
            Destroy(bomber);
        }

        Destroy(gameObject);
    }

    private Vector3[] BuildImpactPoints()
    {
        int rows = config.StealthBomberRows;
        int columns = config.StealthBomberColumns;
        Vector3 right = Vector3.Cross(Vector3.up, flightDirection).normalized;
        if (right.sqrMagnitude <= 0f)
        {
            right = Vector3.right;
        }

        Vector3[] points = new Vector3[rows * columns];
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            float forwardOffset = (row - (rows - 1) * 0.5f) * config.StealthBomberSpacing;
            for (int column = 0; column < columns; column++)
            {
                float sideOffset = (column - (columns - 1) * 0.5f) * config.StealthBomberSpacing;
                points[index++] = CombatPlane.WithFixedY(
                    targetCenter + flightDirection * forwardOffset + right * sideOffset);
            }
        }

        return points;
    }

    private IEnumerator StrikePoint(Vector3 impactPosition, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Vector3 visualPosition = impactPosition + Vector3.up * 0.05f;
        if (config.WarningEffectPrefab != null)
        {
            GameObject warning = CombatObjectPool.GetEffect(config.WarningEffectPrefab, visualPosition, GroundEffectRotation);
            warning.transform.localScale = Vector3.one * config.StealthBomberExplosionRadius;
            CombatObjectPool.ReleaseEffect(warning, config.ImpactDelay);
        }

        yield return new WaitForSeconds(config.ImpactDelay);

        if (owner == null || owner.Health == null || owner.Health.IsDead)
        {
            yield break;
        }

        // 스텔스 폭격기는 여러 착탄 지점에 피해를 나누어 적용해 화면 전체를 과도하게 지우지 않는다.
        float totalDamage = PlayerSkillCombat.CalculateDamage(owner, config, out bool isCritical);
        float damagePerPoint = totalDamage / Mathf.Max(1, config.StealthBomberRows * config.StealthBomberColumns);
        PlayerSkillCombat.ApplyAreaDamage(
            owner,
            impactPosition,
            config.StealthBomberExplosionRadius,
            damagePerPoint,
            config.MaxTargets,
            config.KnockbackForce,
            isCritical);

        CombatCameraShake.Play(
            config.ScreenShakeDuration,
            config.ScreenShakeStrength,
            config.ScreenShakeFrequency);

        if (config.ImpactEffectPrefab != null)
        {
            GameObject impact = CombatObjectPool.GetEffect(config.ImpactEffectPrefab, visualPosition, Quaternion.identity);
            impact.transform.localScale = Vector3.one * config.StealthBomberExplosionRadius;
            CombatObjectPool.ReleaseEffect(impact, config.EffectCleanupDelay);
        }

        PlayImpactSfx();
    }

    private void PlayImpactSfx()
    {
        AudioClip clip = GetRandomImpactSfx();
        if (clip == null)
        {
            return;
        }

        impactAudioSource ??= ResolveImpactAudioSource();
        if (impactAudioSource != null)
        {
            impactAudioSource.PlayOneShot(clip);
        }
    }

    private AudioClip GetRandomImpactSfx()
    {
        AudioClip[] clips = config != null ? config.ImpactSfx : null;
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        int targetIndex = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (targetIndex == 0)
            {
                return clips[i];
            }

            targetIndex--;
        }

        return null;
    }

    private AudioSource ResolveImpactAudioSource()
    {
        Transform sourceRoot = owner != null ? owner.transform : null;
        Transform sourceTransform = FindChildByName(sourceRoot, ImpactAudioSourceName);
        if (sourceTransform != null && sourceTransform.TryGetComponent(out AudioSource source))
        {
            source.spatialBlend = 0f;
            return source;
        }

        return null;
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

    private IEnumerator MoveBomber(GameObject bomber, Vector3 startPosition, Vector3 endPosition)
    {
        if (bomber == null)
        {
            yield break;
        }

        float duration = Vector3.Distance(startPosition, endPosition) / config.StealthBomberSpeed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (bomber == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            bomber.transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    private Quaternion GetFlightRotation()
    {
        return Quaternion.LookRotation(flightDirection, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }
}
