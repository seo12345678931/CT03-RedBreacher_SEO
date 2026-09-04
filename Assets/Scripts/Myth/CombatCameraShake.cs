using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatCameraShake : MonoBehaviour
{
    private static NoiseSettings runtimeNoiseProfile;

    private CinemachineBasicMultiChannelPerlin noise;
    private NoiseSettings previousNoiseProfile;
    private float previousAmplitude;
    private float previousFrequency;
    private float shakeDuration;
    private float shakeTimeRemaining;
    private float shakeStrength;
    private bool isShaking;

    public static void Play(float duration, float strength, float frequency)
    {
        if (duration <= 0f || strength <= 0f)
        {
            return;
        }

        CinemachineCamera targetCamera = FindFirstObjectByType<CinemachineCamera>();
        if (targetCamera == null)
        {
            return;
        }

        CombatCameraShake shake = targetCamera.GetComponent<CombatCameraShake>();
        if (shake == null)
        {
            shake = targetCamera.gameObject.AddComponent<CombatCameraShake>();
        }

        shake.StartShake(duration, strength, frequency);
    }

    private void Update()
    {
        if (!isShaking || noise == null)
        {
            return;
        }

        shakeTimeRemaining -= Time.unscaledDeltaTime;
        if (shakeTimeRemaining <= 0f)
        {
            StopShake();
            return;
        }

        // 폭발 직후 강하게 흔들리고 종료 시점까지 빠르게 감쇠한다.
        float progress = Mathf.Clamp01(shakeTimeRemaining / shakeDuration);
        noise.AmplitudeGain = shakeStrength * progress;
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void StartShake(float duration, float strength, float frequency)
    {
        EnsureNoise();
        if (noise == null)
        {
            return;
        }

        if (!isShaking)
        {
            previousNoiseProfile = noise.NoiseProfile;
            previousAmplitude = noise.AmplitudeGain;
            previousFrequency = noise.FrequencyGain;
        }

        isShaking = true;
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        shakeStrength = Mathf.Max(shakeStrength, strength);
        noise.NoiseProfile = GetRuntimeNoiseProfile();
        noise.AmplitudeGain = shakeStrength;
        noise.FrequencyGain = Mathf.Max(0.01f, frequency);
    }

    private void EnsureNoise()
    {
        if (noise == null)
        {
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        if (noise == null)
        {
            noise = gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
        }
    }

    private void StopShake()
    {
        if (!isShaking || noise == null)
        {
            return;
        }

        noise.NoiseProfile = previousNoiseProfile;
        noise.AmplitudeGain = previousAmplitude;
        noise.FrequencyGain = previousFrequency;
        shakeDuration = 0f;
        shakeTimeRemaining = 0f;
        shakeStrength = 0f;
        isShaking = false;
    }

    private static NoiseSettings GetRuntimeNoiseProfile()
    {
        if (runtimeNoiseProfile != null)
        {
            return runtimeNoiseProfile;
        }

        runtimeNoiseProfile = ScriptableObject.CreateInstance<NoiseSettings>();
        runtimeNoiseProfile.name = "Bombardment Shake Noise (Runtime)";
        runtimeNoiseProfile.hideFlags = HideFlags.HideAndDontSave;
        runtimeNoiseProfile.PositionNoise = new[]
        {
            CreateNoise(1f, 1f, 0.2f)
        };
        runtimeNoiseProfile.OrientationNoise = new[]
        {
            CreateNoise(0.35f, 0.35f, 0.15f)
        };
        return runtimeNoiseProfile;
    }

    private static NoiseSettings.TransformNoiseParams CreateNoise(float x, float y, float z)
    {
        return new NoiseSettings.TransformNoiseParams
        {
            X = CreateNoiseChannel(x),
            Y = CreateNoiseChannel(y),
            Z = CreateNoiseChannel(z)
        };
    }

    private static NoiseSettings.NoiseParams CreateNoiseChannel(float amplitude)
    {
        return new NoiseSettings.NoiseParams
        {
            Frequency = 1f,
            Amplitude = amplitude,
            Constant = false
        };
    }
}
