using System.Collections;
using UnityEngine;

public class BombardmentSkill : MonoBehaviour
{
    private static readonly Quaternion SkillSpawnRotation = Quaternion.Euler(90f, 0f, 0f);
    private const string ImpactAudioSourceName = "Audio Source_SFX Impact";

    private PlayerController _owner;
    private PlayerSkillConfig _config;
    private AudioSource _impactAudioSource;
    private Vector3 _impactPosition;
    private int _bombCount;

    public static bool Cast(PlayerController player, PlayerSkillConfig skillConfig, Vector3 targetPosition)
    {
        if (player == null || skillConfig == null)
        {
            return false;
        }

        GameObject skillObject = new GameObject($"Bombardment_{skillConfig.Id}");
        BombardmentSkill skill = skillObject.AddComponent<BombardmentSkill>();
        skill._owner = player;
        skill._config = skillConfig;
        skill._impactPosition = CombatPlane.WithFixedY(targetPosition);
        // 중복 획득으로 오른 스킬 레벨에 따라 실제 투하 폭탄 수를 계산한다.
        skill._bombCount = skillConfig.GetBombCount(player.GetSkillLevel(skillConfig));
        skill.StartCoroutine(skill.Execute());
        return true;
    }

    private IEnumerator Execute()
    {
        // 지면 데칼이나 이펙트가 바닥과 겹쳐서 안 보이는 현상(Z-Fighting)을 방지하기 위해 미세한 높이 오프셋 추가
        Vector3 visualSpawnPosition = _impactPosition + Vector3.up * 0.05f;

        // 1. 경고 장판 생성
        if (_config.WarningEffectPrefab != null)
        {
            GameObject warning = CombatObjectPool.GetEffect(
                _config.WarningEffectPrefab,
                visualSpawnPosition,
                SkillSpawnRotation);
            
            // 경고 장판 스케일 조정
            warning.transform.localScale = Vector3.one * _config.BombEffectScale;

            // 연속 폭격을 고려해 장판의 유효 시간을 계산하여 릴리즈
            float totalDuration = _config.ImpactDelay + (_bombCount - 1) * _config.BombInterval;
            CombatObjectPool.ReleaseEffect(warning, totalDuration);
        }

        // 2. 비행기 연출 생성 및 비행 개시
        GameObject airplane = null;
        if (_config.AirplanePrefab != null)
        {
            Vector3 direction = Vector3.forward;
            Vector3 centerPosition = _owner != null ? _owner.transform.position : _impactPosition;

            Vector3 startPosition = centerPosition - (direction * _config.AirplaneSpawnOffset);
            startPosition.y = _config.AirplaneHeight;

            Vector3 endPosition = centerPosition + (direction * _config.AirplaneSpawnOffset);
            endPosition.y = _config.AirplaneHeight;

            Quaternion airplaneRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            airplane = Instantiate(_config.AirplanePrefab, startPosition, airplaneRotation);
            StartCoroutine(MoveAirplane(airplane, startPosition, endPosition, _config.AirplaneSpeed));
        }

        // 3. 다중 폭격 연출 및 실행 (시차를 두어 순차 투하)
        float bombSpeed = 20f;
        float fallDistance = _config.AirplaneHeight - CombatPlane.FixedY;
        float fallDuration = bombSpeed > 0f ? fallDistance / bombSpeed : 0.3f;
        float delayBeforeFirstFall = Mathf.Max(0f, _config.ImpactDelay - fallDuration);

        for (int i = 0; i < _bombCount; i++)
        {
            Vector3 bombTargetPos = _impactPosition;
            if (i > 0)
            {
                // 첫 발 이외에는 폭격 범위 반경 내의 랜덤 오프셋 위치에 분산하여 폭격을 유도 (집탄율 70% 수준)
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0f, _config.EffectRadius * 0.7f);
                bombTargetPos += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            float triggerDelay = delayBeforeFirstFall + (i * _config.BombInterval);
            StartCoroutine(SpawnSingleBomb(bombTargetPos, triggerDelay, fallDuration));
        }

        // 전체 연출 소요 시간에 맞춰 컴포넌트 라이프타임 유지
        float totalProcessTime = delayBeforeFirstFall + (_bombCount - 1) * _config.BombInterval + fallDuration + 0.5f;
        yield return new WaitForSeconds(totalProcessTime);

        // 비행기의 최대 비행시간 고려해 대기 후 제거
        float maxFlightDuration = _config.AirplaneSpeed > 0f ? (_config.AirplaneSpawnOffset * 2f / _config.AirplaneSpeed) + 0.5f : 3f;
        float remainingFlightTime = Mathf.Max(0f, maxFlightDuration - totalProcessTime);
        if (remainingFlightTime > 0f)
        {
            yield return new WaitForSeconds(remainingFlightTime);
        }

        if (airplane != null)
        {
            Destroy(airplane);
        }
        
        Destroy(gameObject);
    }

    private IEnumerator SpawnSingleBomb(Vector3 targetPos, float delay, float fallDuration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // 1. 폭탄 투사체 생성 및 낙하
        GameObject bomb = null;
        if (_config.BombProjectilePrefab != null)
        {
            Vector3 bombStartPos = targetPos;
            bombStartPos.y = _config.AirplaneHeight;

            Quaternion bombRotation = Quaternion.Euler(90f, 0f, 0f);
            bomb = Instantiate(_config.BombProjectilePrefab, bombStartPos, bombRotation);
            
            // 폭탄 투사체 스케일링
            bomb.transform.localScale = Vector3.one * _config.BombEffectScale;

            StartCoroutine(FallBomb(bomb, bombStartPos, targetPos, fallDuration));
        }

        // 낙하 시간 동안 대기
        yield return new WaitForSeconds(fallDuration);

        // 2. 폭격 데미지 및 폭발 이펙트 생성
        CombatCameraShake.Play(
            _config.ScreenShakeDuration,
            _config.ScreenShakeStrength,
            _config.ScreenShakeFrequency);

        if (_owner != null && _owner.Health != null && !_owner.Health.IsDead)
        {
            // 폭격 시점의 최종 플레이어 공격력 / 폭격 횟수를 1발당 데미지로 분할 적용
            float damage = PlayerSkillCombat.CalculateDamage(_owner, _config, out bool isCritical);
            float damagePerBomb = damage / Mathf.Max(1, _bombCount);

            PlayerSkillCombat.ApplyAreaDamage(
                _owner,
                targetPos,
                _config.EffectRadius,
                damagePerBomb,
                _config.MaxTargets,
                _config.KnockbackForce,
                isCritical);

            if (_config.ImpactEffectPrefab != null)
            {
                Vector3 visualSpawnPosition = targetPos + Vector3.up * 0.05f;
                GameObject impact = CombatObjectPool.GetEffect(
                    _config.ImpactEffectPrefab,
                    visualSpawnPosition,
                    Quaternion.identity);
                
                // 폭발 이펙트 스케일링
                impact.transform.localScale = Vector3.one * _config.BombEffectScale;

                CombatObjectPool.ReleaseEffect(impact, _config.EffectCleanupDelay);
            }

            PlayImpactSfx();
        }
    }

    private void PlayImpactSfx()
    {
        AudioClip clip = GetRandomImpactSfx();
        if (clip == null)
        {
            return;
        }

        _impactAudioSource ??= ResolveImpactAudioSource();
        if (_impactAudioSource != null)
        {
            _impactAudioSource.PlayOneShot(clip);
        }
    }

    private AudioClip GetRandomImpactSfx()
    {
        AudioClip[] clips = _config != null ? _config.ImpactSfx : null;
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
        Transform sourceRoot = _owner != null ? _owner.transform : null;
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

    private IEnumerator MoveAirplane(GameObject plane, Vector3 start, Vector3 end, float speed)
    {
        if (plane == null || speed <= 0f)
        {
            yield break;
        }

        float distance = Vector3.Distance(start, end);
        float duration = distance / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (plane == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            plane.transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        if (plane != null)
        {
            Destroy(plane);
        }
    }

    private IEnumerator FallBomb(GameObject bomb, Vector3 start, Vector3 end, float duration)
    {
        if (bomb == null || duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (bomb == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            bomb.transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        if (bomb != null)
        {
            Destroy(bomb);
        }
    }
}
