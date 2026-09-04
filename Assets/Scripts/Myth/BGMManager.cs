using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public enum BgmContext
{
    Combat,
    Boss,
    Base
}

/// <summary>
/// 전투/보스전/기지 상황에 따라 배경음악을 크로스페이드로 전환한다.
/// 우선순위: 보스 > 기지 > 전투. 보스전은 BossEncounterManager 이벤트로 자동 감지하고,
/// 기지는 <see cref="BgmContextBinder"/>(기지 패널 루트에 부착)로 켜고 끈다.
/// 출력은 BGM 믹서 그룹으로 보내 설정의 BGM 볼륨이 그대로 먹는다.
/// </summary>
[DisallowMultipleComponent]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip combatClip;
    [SerializeField] private AudioClip bossClip;
    [SerializeField] private AudioClip baseClip;

    [Header("Output")]
    [Tooltip("설정의 BGM 볼륨이 먹도록 BGM 믹서 그룹을 지정한다.")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [Tooltip("BGM 기본 볼륨(믹서 볼륨과 별개의 소스 레벨).")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Transition")]
    [SerializeField] private float crossfadeSeconds = 1f;
    [Tooltip("씬 진입 시 BGM 페이드인 시간(초).")]
    [SerializeField] private float sceneFadeInSeconds = 1f;
    [Tooltip("보스전 시작/종료를 자동 감지해 보스 BGM으로 전환한다.")]
    [SerializeField] private bool autoHookBossEncounter = true;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine fadeRoutine;

    private bool bossActive;
    private bool baseActive;
    private BgmContext currentContext;
    private BossEncounterManager hookedBoss;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AudioVolumeSettings.ApplyVolumeToMixer(
            bgmMixerGroup != null ? bgmMixerGroup.audioMixer : null,
            AudioVolumeSettings.BgmMixerParameter,
            AudioVolumeSettings.GetSavedVolume(AudioVolumeSettings.BgmVolumePrefKey));
        sourceA = CreateSource("BGM Source A");
        sourceB = CreateSource("BGM Source B");
        activeSource = sourceA;
    }

    private void Start()
    {
        if (autoHookBossEncounter)
        {
            HookBoss();
            bossActive = hookedBoss != null && hookedBoss.IsEncounterActive;
        }

        currentContext = ResolveContext();
        // 씬 진입 시 뚝 시작하지 않고 페이드인으로 부드럽게 켠다.
        FadeIn(ClipFor(currentContext), sceneFadeInSeconds);
    }

    private void OnDestroy()
    {
        if (hookedBoss != null)
        {
            hookedBoss.EncounterStarted -= HandleBossStarted;
            hookedBoss.EncounterEnded -= HandleBossEnded;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ── 외부/바인더에서 호출하는 컨텍스트 제어 ──────────────────────────────
    public void SetBossActive(bool active)
    {
        if (bossActive == active)
        {
            return;
        }

        bossActive = active;
        Apply();
    }

    public void SetBaseActive(bool active)
    {
        if (baseActive == active)
        {
            return;
        }

        baseActive = active;
        Apply();
    }

    /// <summary>명시적으로 기지 BGM로 전환(버튼 OnClick 등에 직접 연결해도 됨).</summary>
    public void PlayBase() => SetBaseActive(true);

    /// <summary>기지 해제 → 전투(또는 보스) BGM으로 복귀.</summary>
    public void PlayCombat() => SetBaseActive(false);

    /// <summary>씬 전환 등에서 현재 BGM을 페이드아웃하고 정지한다.</summary>
    public void FadeOut(float duration)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOutRoutine(Mathf.Max(0.01f, duration)));
    }

    private void FadeIn(AudioClip clip, float duration)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource to = activeSource != null ? activeSource : sourceA;
        to.clip = clip;
        to.volume = 0f;
        to.Play();
        activeSource = to;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeSourceRoutine(to, volume, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        AudioSource source = activeSource;
        yield return FadeSourceRoutine(source, 0f, duration);
        if (source != null)
        {
            source.Stop();
        }

        fadeRoutine = null;
    }

    private IEnumerator FadeSourceRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration && source != null)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (source != null)
        {
            source.volume = targetVolume;
        }
    }

    // ── 내부 ────────────────────────────────────────────────────────────────
    private void HookBoss()
    {
        hookedBoss = FindFirstObjectByType<BossEncounterManager>(FindObjectsInactive.Include);
        if (hookedBoss != null)
        {
            hookedBoss.EncounterStarted += HandleBossStarted;
            hookedBoss.EncounterEnded += HandleBossEnded;
        }
    }

    private void HandleBossStarted() => SetBossActive(true);
    private void HandleBossEnded(bool _) => SetBossActive(false);

    private BgmContext ResolveContext()
    {
        if (bossActive)
        {
            return BgmContext.Boss;
        }

        if (baseActive)
        {
            return BgmContext.Base;
        }

        return BgmContext.Combat;
    }

    private void Apply()
    {
        BgmContext target = ResolveContext();
        if (target == currentContext)
        {
            return;
        }

        currentContext = target;
        CrossfadeTo(ClipFor(target), false);
    }

    private AudioClip ClipFor(BgmContext ctx)
    {
        return ctx switch
        {
            BgmContext.Boss => bossClip,
            BgmContext.Base => baseClip,
            _ => combatClip
        };
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = bgmMixerGroup;
        source.volume = 0f;
        return source;
    }

    private void CrossfadeTo(AudioClip clip, bool instant)
    {
        if (clip == null)
        {
            return;
        }

        // 이미 같은 곡이 재생 중이면 무시.
        if (activeSource != null && activeSource.clip == clip && activeSource.isPlaying)
        {
            return;
        }

        AudioSource from = activeSource;
        AudioSource to = activeSource == sourceA ? sourceB : sourceA;
        to.clip = clip;
        to.volume = instant ? volume : 0f;
        to.Play();
        activeSource = to;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (instant)
        {
            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            to.volume = volume;
        }
        else
        {
            fadeRoutine = StartCoroutine(Crossfade(from, to));
        }
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to)
    {
        float duration = Mathf.Max(0.01f, crossfadeSeconds);
        float fromStart = from != null ? from.volume : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            if (from != null)
            {
                from.volume = Mathf.Lerp(fromStart, 0f, k);
            }

            to.volume = Mathf.Lerp(0f, volume, k);
            yield return null;
        }

        if (from != null)
        {
            from.Stop();
            from.volume = 0f;
        }

        to.volume = volume;
        fadeRoutine = null;
    }
}
