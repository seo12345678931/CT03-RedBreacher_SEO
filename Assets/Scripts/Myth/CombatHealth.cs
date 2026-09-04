using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public readonly struct CombatDamageEvent
{
    public CombatDamageEvent(
        CombatHealth target,
        float requestedDamage,
        float appliedDamage,
        float previousHealth,
        float currentHealth,
        bool isCritical,
        bool isFatal,
        Vector3 hitPosition)
    {
        Target = target;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        IsCritical = isCritical;
        IsFatal = isFatal;
        HitPosition = hitPosition;
    }

    public CombatHealth Target { get; }
    public float RequestedDamage { get; }
    public float AppliedDamage { get; }
    public float PreviousHealth { get; }
    public float CurrentHealth { get; }
    public bool IsCritical { get; }
    public bool IsFatal { get; }
    public Vector3 HitPosition { get; }
}

public class CombatHealth : MonoBehaviour
{
    private const string BossDeathAudioSourceName = "Audio Source_SFX BossDeath";

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private bool autoAddDamageFeedback = true;

    [Header("Auto Regen")]
    [SerializeField] private bool enableAutoRegen = true;
    [SerializeField] private bool playerOnlyRegen = true;
    [SerializeField] private float regenDelayAfterHit = 3f;
    [SerializeField] private float regenPercentPerSecond = 0.01f;
    [SerializeField] private float regenFlatPerSecond;

    [Header("Damage Number")]
    [SerializeField] private bool showDamageNumber = true;
    [SerializeField] private TMP_FontAsset damageNumberFont;

    // 전역 기본 데미지 폰트. CombatHealth가 런타임 AddComponent로 붙는 적(Enemy02~10 등)은
    // damageNumberFont를 인스펙터로 배선할 수 없으므로, 씬에서 한 번 주입해 공용 기본값으로 쓴다.
    // 개별 프리팹의 damageNumberFont가 설정되어 있으면 그쪽이 우선한다.
    public static TMP_FontAsset GlobalDamageNumberFont;

    // 전역 데미지 폰트 크기. 0 이하면 개별 프리팹의 damageNumberFontSize를 그대로 쓴다.
    // 프리팹마다 직렬화된 크기가 제각각이라, 전투 전체의 데미지 숫자 크기를 한 곳에서 조절하기 위한 값.
    public static float GlobalDamageNumberFontSize;
    [SerializeField] private Color enemyDamageColor = Color.white;
    [SerializeField] private Color playerDamageColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float damageNumberFontSize = 4f;
    [SerializeField] private float criticalSizeMultiplier = 1.35f;
    [SerializeField] private float damageNumberDuration = 0.8f;
    [SerializeField] private float damageNumberRiseDistance = 0.8f;
    [SerializeField] private float damageNumberRandomSpread = 0.25f;
    [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private int damageNumberSortingOrder = 50;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource hitAudioSource;
    [Tooltip("같은 피격음이 이 시간(초) 안에 다시 나지 않도록 막는다(동시 다중 피격 시 소리 겹침 방지).")]
    [SerializeField] private float hitSfxMinInterval = 0.05f;

    private float currentHealth;
    private float lastDamageTime = float.NegativeInfinity;
    private float invulnerableUntil;
    private bool debugInvulnerable;
    private bool isDead;
    private bool deathRewardClaimed;
    private EnemyController enemyController;
    private bool isPlayer;
    private static AudioSource bossDeathAudioSource;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsInvulnerable => debugInvulnerable || Time.time < invulnerableUntil;
    public bool IsDebugInvulnerable => debugInvulnerable;
    public event System.Action<float> OnDamageBlockedByInvulnerability;
    public event System.Action<CombatDamageEvent> OnDamaged;
    public event System.Action<CombatDamageEvent> OnCriticalDamaged;
    public event System.Action<CombatDamageEvent> OnDied;

    private void Awake()
    {
        currentHealth = maxHealth;
        enemyController = GetComponent<EnemyController>();
        isPlayer = GetComponent<PlayerController>() != null;
        EnsureDamageFeedback();
    }

    private void Update()
    {
        ApplyAutoRegen();
    }

    public void Initialize(float newMaxHealth)
    {
        // SO나 스폰 매니저에서 체력 값을 주입할 때 사용한다.
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = maxHealth;
        isDead = false;
        deathRewardClaimed = false;
        invulnerableUntil = 0f;
    }

    public void SetMaxHealth(float newMaxHealth, bool preserveCurrentRatio)
    {
        float previousMaxHealth = Mathf.Max(1f, maxHealth);
        float healthRatio = Mathf.Clamp01(currentHealth / previousMaxHealth);
        maxHealth = Mathf.Max(1f, newMaxHealth);

        // 장비 교체 시 회복 악용을 막기 위해 현재 체력 비율을 유지한다.
        currentHealth = preserveCurrentRatio
            ? maxHealth * healthRatio
            : Mathf.Min(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage, bool isCritical = false)
    {
        if (isDead || damage <= 0f)
        {
            return;
        }

        if (debugInvulnerable)
        {
            return;
        }

        if (Time.time < invulnerableUntil)
        {
            OnDamageBlockedByInvulnerability?.Invoke(damage);
            return;
        }

        // 체력 감소 후 사망 여부를 즉시 판정한다.
        lastDamageTime = Time.time;
        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        CombatDamageEvent damageEvent = new CombatDamageEvent(
            this,
            damage,
            previousHealth - currentHealth,
            previousHealth,
            currentHealth,
            isCritical,
            currentHealth <= 0f,
            transform.position);
        PlayHitSound();

        // 타격감 전용 연출은 이 이벤트를 구독해서 데미지 계산/사망 판정과 분리한다.
        OnDamaged?.Invoke(damageEvent);
        if (isCritical)
        {
            OnCriticalDamaged?.Invoke(damageEvent);
        }

        // 데미지 숫자는 부수 연출이므로, 표시 실패가 데미지/사망 처리나 공격자 로직을 깨지 않게 격리한다.
        // 막타도 남은 체력이 아닌 공격이 계산한 원래 피해량을 표시한다.
        try
        {
            ShowDamageNumber(damage, isCritical);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }

        if (currentHealth <= 0f)
        {
            Die(damageEvent);
        }
    }

    // 플레이어의 무기 및 스킬에 피격할 때마다 사운드 출력
    private void PlayHitSound()
    {
        if (hitAudioSource == null)
        {
            return;
        }

        enemyController ??= GetComponent<EnemyController>();
        AudioClip[] hitSounds = enemyController != null && enemyController.CurrentConfig != null
            ? enemyController.CurrentConfig.HitSounds
            : null;
        if (hitSounds == null || hitSounds.Length == 0)
        {
            return;
        }

        int validCount = 0;
        for (int i = 0; i < hitSounds.Length; i++)
        {
            if (hitSounds[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, validCount);
        for (int i = 0; i < hitSounds.Length; i++)
        {
            AudioClip clip = hitSounds[i];
            if (clip == null)
            {
                continue;
            }

            if (selectedIndex-- == 0)
            {
                // 같은 피격음이 짧은 시간에 겹치지 않도록 스로틀을 거쳐 재생한다.
                SfxThrottle.PlayOneShot(hitAudioSource, clip, hitSfxMinInterval);
                return;
            }
        }
    }

    public void SetTemporaryInvulnerability(float duration)
    {
        // 회피 중 연속 피격을 막기 위해 기존 무적 시간보다 길 때만 갱신한다.
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0f, duration));
    }

    public void SetDebugInvulnerable(bool enabled)
    {
        // 디버그 무적은 회피 무적 이벤트와 분리해 퍼펙트 회피가 오발동하지 않게 한다.
        debugInvulnerable = enabled;
    }

    private void ShowDamageNumber(float damage, bool isCritical)
    {
        if (!showDamageNumber || damage <= 0f)
        {
            return;
        }

        Color color = isCritical
            ? criticalDamageColor
            : isPlayer ? playerDamageColor : enemyDamageColor;
        // 전역 크기가 지정되면 개별값 대신 전역값을 사용한다(전투 전체 데미지 숫자 크기 통일).
        float resolvedFontSize = GlobalDamageNumberFontSize > 0f
            ? GlobalDamageNumberFontSize
            : damageNumberFontSize;
        DamageNumberVisual.Play(
            damage,
            isCritical,
            transform.position + damageNumberOffset,
            damageNumberFont,
            color,
            resolvedFontSize,
            criticalSizeMultiplier,
            damageNumberDuration,
            damageNumberRiseDistance,
            damageNumberRandomSpread,
            damageNumberSortingOrder);
    }

    public bool TryClaimDeathReward()
    {
        if (!isDead || deathRewardClaimed)
        {
            return false;
        }

        // 같은 적에게 여러 투사체가 겹쳐도 처치 보상은 한 번만 지급한다.
        deathRewardClaimed = true;
        return true;
    }

    private void ApplyAutoRegen()
    {
        if (!enableAutoRegen || isDead || currentHealth >= maxHealth)
        {
            return;
        }

        if (playerOnlyRegen && !isPlayer)
        {
            return;
        }

        if (Time.time < lastDamageTime + Mathf.Max(0f, regenDelayAfterHit))
        {
            return;
        }

        float regenPerSecond = maxHealth * Mathf.Max(0f, regenPercentPerSecond) + Mathf.Max(0f, regenFlatPerSecond);
        if (regenPerSecond <= 0f)
        {
            return;
        }

        // 피격 후 일정 시간이 지나면 최대 체력 기준 비율 회복을 적용한다.
        currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);
    }

    private void Die(CombatDamageEvent damageEvent)
    {
        isDead = true;
        OnDied?.Invoke(damageEvent);

        if (isPlayer)
        {
            Debug.Log($"{name} 사망: 플레이어 자동 전투를 중지합니다.");
            return;
        }

        // 적 사망 연출은 제거 직전에 월드 이펙트로 분리해 재생한다.
        PlayBossDeathSound();
        GetComponent<EnemyController>()?.PlayDeathExplosionEffect();

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }

    private void PlayBossDeathSound()
    {
        BossEnemyController bossController = GetComponent<BossEnemyController>();
        AudioClip[] clips = bossController != null && bossController.BossConfig != null
            ? bossController.BossConfig.DeathSoundsSfx
            : null;
        AudioClip clip = PickRandomClip(clips);
        if (clip == null)
        {
            return;
        }

        AudioSource source = ResolveBossDeathAudioSource();
        if (source != null)
        {
            source.PlayOneShot(clip);
        }
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
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

        int selectedIndex = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            if (selectedIndex-- == 0)
            {
                return clip;
            }
        }

        return null;
    }

    private static AudioSource ResolveBossDeathAudioSource()
    {
        if (bossDeathAudioSource != null)
        {
            return bossDeathAudioSource;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        Transform sourceTransform = FindChildByName(
            player != null ? player.transform : null,
            BossDeathAudioSourceName);
        if (sourceTransform != null && sourceTransform.TryGetComponent(out AudioSource source))
        {
            bossDeathAudioSource = source;
        }

        return bossDeathAudioSource;
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
            Transform result = FindChildByName(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void EnsureDamageFeedback()
    {
        if (!autoAddDamageFeedback || GetComponent<CombatDamageFeedback>() != null)
        {
            return;
        }

        // 프리팹 수정 없이 CombatHealth가 있는 대상에 피격 연출 컴포넌트를 자동 부착한다.
        gameObject.AddComponent<CombatDamageFeedback>();
    }
}

[DisallowMultipleComponent]
public class CombatDamageFeedback : MonoBehaviour
{
    private const string FlashOverlayName = "__DamageFlashOverlay";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material flashOverlayMaterial;
    private static bool flashOverlayMaterialSearched;

    [Header("Hit Flash")]
    [SerializeField] private bool enableHitFlash = true;
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private Color criticalFlashColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float flashDuration = 0.08f;

    [Header("Hit Squash")]
    [SerializeField] private bool enableHitSquash = true;
    [SerializeField] private float squashPunch = 0.06f;
    [SerializeField] private float criticalSquashPunch = 0.13f;
    [SerializeField] private float squashDuration = 0.14f;

    [Header("Camera Shake")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private bool shakeOnlyOnCriticalOrFatal = true;
    [SerializeField] private float normalShakeDuration = 0.06f;
    [SerializeField] private float normalShakeStrength = 0.18f;
    [SerializeField] private float normalShakeFrequency = 18f;
    [SerializeField] private float criticalShakeDuration = 0.1f;
    [SerializeField] private float criticalShakeStrength = 0.42f;
    [SerializeField] private float criticalShakeFrequency = 24f;
    [SerializeField] private float shakeInterval = 0.05f;

    [Header("Hit Stop")]
    [SerializeField] private bool enableCriticalHitStop = true;
    [SerializeField] private float criticalHitStopDuration = 0.035f;
    [SerializeField] private float fatalHitStopDuration = 0.055f;

    private CombatHealth health;
    private readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private readonly List<Color> spriteBaseColors = new List<Color>();
    private readonly List<SpriteRenderer> spriteFlashOverlays = new List<SpriteRenderer>();
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Color> rendererBaseColors = new List<Color>();
    private MaterialPropertyBlock propertyBlock;
    private Tween flashTween;
    private Tween squashTween;
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleCaptured;
    private float nextShakeTime;
    private bool allowsImpactFeedback;
    private bool isBoss;
    private bool flashTargetsReady;
    private static bool hitStopActive;
    private static float previousTimeScale = 1f;

    private void Awake()
    {
        // MaterialPropertyBlock은 생성자/필드 초기화에서 만들 수 없어 Awake에서 생성한다.
        propertyBlock = new MaterialPropertyBlock();

        // 일반 적 다수가 동시에 피격될 때 연출이 과해지지 않도록 보스/플레이어만 강한 피격 연출을 허용한다.
        isBoss = GetComponent<BossEnemyController>() != null;
        allowsImpactFeedback = GetComponent<PlayerController>() != null || isBoss;
    }

    private void OnEnable()
    {
        health = GetComponent<CombatHealth>();
        if (health == null)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
        health.OnCriticalDamaged += HandleCriticalDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnCriticalDamaged -= HandleCriticalDamaged;
        }

        StopFlash();
        StopSquash();
    }

    private void HandleDamaged(CombatDamageEvent damageEvent)
    {
        PlayFlash(damageEvent.IsCritical ? criticalFlashColor : hitFlashColor);

        // 보스는 펀치 대신 피격마다 스크린 셰이크로 묵직함을 준다.
        if (!isBoss)
        {
            PlaySquash(damageEvent.IsCritical);
        }

        PlayShake(damageEvent.IsCritical || damageEvent.IsFatal, force: isBoss);
    }

    private void HandleCriticalDamaged(CombatDamageEvent damageEvent)
    {
        // 히트스탑은 보스전에서만 작동시킨다. (일반 웨이브에서는 화면 정지가 과하게 느껴짐)
        if (!enableCriticalHitStop || !allowsImpactFeedback || !BossEnemyController.IsBossBattleActive)
        {
            return;
        }

        float duration = damageEvent.IsFatal ? fatalHitStopDuration : criticalHitStopDuration;
        PlayHitStop(duration);
    }

    private void PlayFlash(Color color)
    {
        if (!enableHitFlash)
        {
            return;
        }

        EnsureFlashTargets();
        StopFlash();
        float strength = 1f;
        flashTween = DOVirtual.Float(1f, 0f, Mathf.Max(0.01f, flashDuration), value =>
            {
                strength = value;
                ApplyFlash(color, strength);
            })
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(ClearFlash);
    }

    private void PlaySquash(bool critical)
    {
        if (!enableHitSquash)
        {
            return;
        }

        // 첫 피격 시점에 원래 스케일을 확정한다. (스폰 직후 스케일 보정이 끝난 뒤)
        if (!baseScaleCaptured)
        {
            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }

        // 연속 피격으로 스케일이 누적되지 않도록 원래 크기로 되돌린 뒤 다시 펀치한다.
        squashTween?.Kill(false);
        transform.localScale = baseScale;

        float punch = critical ? criticalSquashPunch : squashPunch;
        squashTween = transform.DOPunchScale(baseScale * punch, Mathf.Max(0.01f, squashDuration), 8, 0.8f)
            .SetUpdate(true)
            .OnComplete(() => transform.localScale = baseScale);
    }

    private void StopSquash()
    {
        squashTween?.Kill(false);
        squashTween = null;
        if (baseScaleCaptured)
        {
            transform.localScale = baseScale;
        }
    }

    private void PlayShake(bool critical, bool force = false)
    {
        if (!enableCameraShake || !allowsImpactFeedback || (shakeOnlyOnCriticalOrFatal && !critical && !force) || Time.unscaledTime < nextShakeTime)
        {
            return;
        }

        nextShakeTime = Time.unscaledTime + Mathf.Max(0f, shakeInterval);
        CombatCameraShake.Play(
            critical ? criticalShakeDuration : normalShakeDuration,
            critical ? criticalShakeStrength : normalShakeStrength,
            critical ? criticalShakeFrequency : normalShakeFrequency);
    }

    private static void PlayHitStop(float duration)
    {
        if (duration <= 0f || hitStopActive)
        {
            return;
        }

        hitStopActive = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        DOVirtual.DelayedCall(duration, () =>
            {
                Time.timeScale = previousTimeScale;
                hitStopActive = false;
            })
            .SetUpdate(true);
    }

    private void CacheRenderers()
    {
        spriteRenderers.Clear();
        spriteBaseColors.Clear();
        List<SpriteRenderer> foundSpriteRenderers = new List<SpriteRenderer>();
        GetComponentsInChildren(true, foundSpriteRenderers);
        for (int i = 0; i < foundSpriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = foundSpriteRenderers[i];
            if (spriteRenderer == null || spriteRenderer.gameObject.name == FlashOverlayName)
            {
                continue;
            }

            spriteRenderers.Add(spriteRenderer);
            spriteBaseColors.Add(spriteRenderer.color);
        }

        renderers.Clear();
        rendererBaseColors.Clear();
        GetComponentsInChildren(true, renderers);
        for (int i = renderers.Count - 1; i >= 0; i--)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || targetRenderer is SpriteRenderer)
            {
                renderers.RemoveAt(i);
                continue;
            }

            rendererBaseColors.Insert(0, ResolveRendererColor(targetRenderer));
        }
    }

    private void EnsureFlashTargets()
    {
        if (flashTargetsReady)
        {
            return;
        }

        // SpriteShapeShadow가 Awake 이후 머티리얼을 바꾸므로, 첫 피격 시점에 렌더러를 확정한다.
        CacheRenderers();
        CreateSpriteFlashOverlays();
        flashTargetsReady = true;
    }

    private void CreateSpriteFlashOverlays()
    {
        spriteFlashOverlays.Clear();
        Material overlayMaterial = GetFlashOverlayMaterial();
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer source = spriteRenderers[i];
            if (source == null)
            {
                spriteFlashOverlays.Add(null);
                continue;
            }

            GameObject overlayObject = new GameObject(FlashOverlayName);
            overlayObject.transform.SetParent(source.transform, false);
            SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.enabled = false;
            if (overlayMaterial != null)
            {
                overlay.sharedMaterial = overlayMaterial;
            }

            spriteFlashOverlays.Add(overlay);
        }
    }

    private void ApplyFlash(Color flashColor, float strength)
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.color = Color.Lerp(spriteBaseColors[i], flashColor, strength);
            ApplySpriteFlashOverlay(i, spriteRenderer, flashColor, strength);
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Color color = Color.Lerp(rendererBaseColors[i], flashColor, strength);
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void StopFlash()
    {
        flashTween?.Kill(false);
        flashTween = null;
        ClearFlash();
    }

    private void ClearFlash()
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = spriteBaseColors[i];
            }

            if (i < spriteFlashOverlays.Count && spriteFlashOverlays[i] != null)
            {
                spriteFlashOverlays[i].enabled = false;
                spriteFlashOverlays[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.SetPropertyBlock(null);
        }
    }

    private static Color ResolveRendererColor(Renderer targetRenderer)
    {
        Material material = targetRenderer.sharedMaterial;
        if (material == null)
        {
            return Color.white;
        }

        if (material.HasProperty(BaseColorId))
        {
            return material.GetColor(BaseColorId);
        }

        return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
    }

    private static Material GetFlashOverlayMaterial()
    {
        if (!flashOverlayMaterialSearched)
        {
            flashOverlayMaterialSearched = true;
            Shader shader = Shader.Find("GUI/Text Shader");
            if (shader != null)
            {
                flashOverlayMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        return flashOverlayMaterial;
    }

    private void ApplySpriteFlashOverlay(int index, SpriteRenderer source, Color flashColor, float strength)
    {
        if (index >= spriteFlashOverlays.Count || spriteFlashOverlays[index] == null)
        {
            return;
        }

        SpriteRenderer overlay = spriteFlashOverlays[index];
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + 1;
        overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Clamp01(strength));
        overlay.enabled = source.enabled && source.sprite != null && strength > 0.01f;
    }
}

public class DamageNumberVisual : MonoBehaviour
{
    private static readonly Queue<DamageNumberVisual> Pool = new Queue<DamageNumberVisual>();
    private static Transform poolRoot;

    private TextMeshPro text;
    private Coroutine animationRoutine;
    private Tween animationTween;

    public static void Play(
        float damage,
        bool isCritical,
        Vector3 position,
        TMP_FontAsset font,
        Color color,
        float fontSize,
        float criticalSizeMultiplier,
        float duration,
        float riseDistance,
        float randomSpread,
        int sortingOrder)
    {
        DamageNumberVisual visual = Get();
        visual.Begin(
            damage,
            isCritical,
            position,
            font,
            color,
            fontSize,
            criticalSizeMultiplier,
            duration,
            riseDistance,
            randomSpread,
            sortingOrder);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool()
    {
        // 도메인 리로드 비활성 환경에서 이전 세션의 (파괴된) 풀 항목이 남아 터지는 것을 막는다.
        Pool.Clear();
        poolRoot = null;
    }

    private static DamageNumberVisual Get()
    {
        EnsurePoolRoot();

        // 풀에 남은 항목이 외부에서 파괴됐을 수 있으므로 살아있는 인스턴스를 만날 때까지 건너뛴다.
        DamageNumberVisual visual = null;
        while (Pool.Count > 0)
        {
            DamageNumberVisual candidate = Pool.Dequeue();
            if (candidate != null)
            {
                visual = candidate;
                break;
            }
        }

        if (visual == null)
        {
            visual = Create();
        }

        visual.transform.SetParent(poolRoot, false);
        visual.gameObject.SetActive(true);
        return visual;
    }

    private static DamageNumberVisual Create()
    {
        GameObject visualObject = new GameObject("Damage Number");
        visualObject.transform.SetParent(poolRoot, false);
        DamageNumberVisual visual = visualObject.AddComponent<DamageNumberVisual>();
        visual.text = visualObject.AddComponent<TextMeshPro>();
        visual.text.alignment = TextAlignmentOptions.Center;
        visual.text.textWrappingMode = TextWrappingModes.NoWrap;
        visualObject.SetActive(false);
        return visual;
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("DamageNumberPool");
        DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
    }

    private void Begin(
        float damage,
        bool isCritical,
        Vector3 position,
        TMP_FontAsset font,
        Color color,
        float fontSize,
        float criticalSizeMultiplier,
        float duration,
        float riseDistance,
        float randomSpread,
        int sortingOrder)
    {
        StopAnimation();
        text ??= GetComponent<TextMeshPro>();
        // 우선순위: 개별 설정 폰트 > 전역 기본 폰트 > TMP 기본 폰트
        TMP_FontAsset resolvedFont = font != null ? font : CombatHealth.GlobalDamageNumberFont;
        text.font = resolvedFont != null ? resolvedFont : TMP_Settings.defaultFontAsset;
        text.text = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
        text.color = color;
        text.fontSize = Mathf.Max(0.1f, fontSize)
            * (isCritical ? Mathf.Max(1f, criticalSizeMultiplier) : 1f);
        text.renderer.sortingOrder = sortingOrder;

        Vector2 spread = Random.insideUnitCircle * Mathf.Max(0f, randomSpread);
        transform.position = position + new Vector3(spread.x, 0f, spread.y);
        FaceCamera();
        transform.localScale = Vector3.one;
        Animate(
            Mathf.Max(0.05f, duration),
            Mathf.Max(0f, riseDistance),
            isCritical);
    }

    private void Animate(float duration, float riseDistance, bool isCritical)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.up * riseDistance;
        Color startColor = text.color;
        Vector3 punchScale = Vector3.one * (isCritical ? 0.35f : 0.14f);

        animationTween = DOTween.Sequence()
            .SetTarget(this)
            .Join(transform.DOMove(endPosition, duration).SetEase(Ease.OutQuad))
            .Join(DOVirtual.Float(1f, 0f, duration, alpha =>
            {
                FaceCamera();
                text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }))
            .Join(transform.DOPunchScale(punchScale, Mathf.Min(0.22f, duration), 8, 0.75f))
            .OnComplete(Release);
    }

    private void FaceCamera()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            transform.rotation = activeCamera.transform.rotation;
        }
    }

    private void StopAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        animationTween?.Kill(false);
        animationTween = null;
    }

    private void Release()
    {
        StopAnimation();
        gameObject.SetActive(false);
        transform.SetParent(poolRoot, false);
        Pool.Enqueue(this);
    }
}

