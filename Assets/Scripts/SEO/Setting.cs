using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    [Header("SFX(효과음)")]
    [SerializeField] private AudioMixer SFX_AudioMixer;
    [SerializeField] private Slider SFX;
    
    [Header("BGM(배경음악)")]
    [SerializeField] private AudioMixer BGM_AudioMixer;
    [SerializeField] private Slider Music;

    private void Start()
    {
        if (Music != null)
        {
            Music.onValueChanged.AddListener(SetBGMVolume);
        }

        if (SFX != null)
        {
            SFX.onValueChanged.AddListener(SetSFXVolume);
        }

        ApplySavedSettings();
    }

    // 설정 바로저장
    private void ApplySavedSettings()
    {
        float bgmVolume = AudioVolumeSettings.GetSavedVolume(
            AudioVolumeSettings.BgmVolumePrefKey,
            AudioVolumeSettings.GetMixerNormalizedVolume(BGM_AudioMixer, AudioVolumeSettings.BgmMixerParameter));
        float sfxVolume = AudioVolumeSettings.GetSavedVolume(
            AudioVolumeSettings.SfxVolumePrefKey,
            AudioVolumeSettings.GetMixerNormalizedVolume(SFX_AudioMixer, AudioVolumeSettings.SfxMixerParameter));
        
        if (Music != null)
        {
            Music.SetValueWithoutNotify(bgmVolume);
        }

        if (SFX != null)
        {
            SFX.SetValueWithoutNotify(sfxVolume);
        }
        
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
    }

    public void SetBGMVolume(float volume)
    {
        AudioVolumeSettings.SetSavedVolume(
            BGM_AudioMixer,
            AudioVolumeSettings.BgmMixerParameter,
            AudioVolumeSettings.BgmVolumePrefKey,
            volume);
    }

    public void SetSFXVolume(float volume)
    {
        AudioVolumeSettings.SetSavedVolume(
            SFX_AudioMixer,
            AudioVolumeSettings.SfxMixerParameter,
            AudioVolumeSettings.SfxVolumePrefKey,
            volume);
    }
}

public static class AudioVolumeSettings
{
    public const string BgmVolumePrefKey = "Setting.BGMVolume";
    public const string SfxVolumePrefKey = "Setting.SFXVolume";
    public const string BgmMixerParameter = "BGMVolume";
    public const string SfxMixerParameter = "SFXVolume";
    // 각 믹서는 자신의 볼륨 파라미터 하나만 노출한다. 다른 믹서의 파라미터를 SetFloat/GetFloat 하면
    // "Exposed name does not exist" 에러가 나므로, 믹서 이름으로 담당 파라미터를 구분한다.
    public const string BgmMixerName = "BGMMixer";
    public const string SfxMixerName = "SFXMixer";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedSettingsAfterSceneLoad()
    {
        ApplySavedSettingsToSceneMixers();
    }

    public static void ApplySavedSettingsToSceneMixers()
    {
        float bgmVolume = GetSavedVolume(BgmVolumePrefKey);
        float sfxVolume = GetSavedVolume(SfxVolumePrefKey);

        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        // 씬에 같은 믹서를 쓰는 소스가 여러 개(적/보스 등)이므로 믹서 단위로 한 번만 적용한다.
        HashSet<AudioMixer> appliedMixers = new HashSet<AudioMixer>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioMixer mixer = sources[i] != null && sources[i].outputAudioMixerGroup != null
                ? sources[i].outputAudioMixerGroup.audioMixer
                : null;
            if (mixer == null || !appliedMixers.Add(mixer))
            {
                continue;
            }

            // 믹서가 실제 노출한 파라미터에만 적용한다(다른 파라미터 호출 시 콘솔 에러 발생).
            if (mixer.name == BgmMixerName)
            {
                ApplyVolumeToMixer(mixer, BgmMixerParameter, bgmVolume);
            }
            else if (mixer.name == SfxMixerName)
            {
                ApplyVolumeToMixer(mixer, SfxMixerParameter, sfxVolume);
            }
        }
    }

    public static float GetSavedVolume(string prefKey, float fallback = 1f)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(prefKey, fallback));
    }

    public static void SetSavedVolume(AudioMixer mixer, string parameterName, string prefKey, float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(mixer, parameterName, clampedVolume);
        PlayerPrefs.SetFloat(prefKey, clampedVolume);
        PlayerPrefs.Save();
    }

    public static void ApplyVolumeToMixer(AudioMixer mixer, string parameterName, float volume)
    {
        if (mixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        float dB = Mathf.Log10(Mathf.Max(0.0001f, Mathf.Clamp01(volume))) * 20f;
        mixer.SetFloat(parameterName, dB);
    }

    public static float GetMixerNormalizedVolume(AudioMixer mixer, string parameterName)
    {
        if (mixer != null && mixer.GetFloat(parameterName, out float currentVolume))
        {
            return Mathf.Pow(10f, currentVolume / 20f);
        }

        return 1f;
    }
}
