using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 같은 효과음이 아주 짧은 시간에 여러 번 겹쳐 재생되는 것을 막는다.
/// 클립별로 마지막 재생 시각을 기록해, minInterval 안에 다시 요청되면 무시한다.
/// 소스(AudioSource)가 여러 개여도(적마다 다른 소스) 클립 기준으로 막으므로,
/// 여러 적이 동시에 맞을 때 피격음이 우르르 쌓이는 문제를 방지한다.
/// </summary>
public static class SfxThrottle
{
    // clip InstanceID → 마지막 재생 시각(unscaled). 클립 종류만큼만 커지므로 사실상 상수 크기.
    private static readonly Dictionary<int, float> lastPlayTimes = new Dictionary<int, float>();

    /// <summary>이 클립을 지금 재생해도 되는지(직전 재생으로부터 minInterval 지났는지) 판정하고, 허용 시 시각을 갱신한다.</summary>
    public static bool TryConsume(AudioClip clip, float minInterval)
    {
        if (clip == null)
        {
            return false;
        }

        if (minInterval <= 0f)
        {
            return true;
        }

        int id = clip.GetInstanceID();
        float now = Time.unscaledTime;
        if (lastPlayTimes.TryGetValue(id, out float last) && now - last < minInterval)
        {
            return false;
        }

        lastPlayTimes[id] = now;
        return true;
    }

    /// <summary>스로틀을 통과할 때만 PlayOneShot으로 재생한다.</summary>
    public static void PlayOneShot(AudioSource source, AudioClip clip, float minInterval, float volume = 1f)
    {
        if (source == null || clip == null)
        {
            return;
        }

        if (TryConsume(clip, minInterval))
        {
            source.PlayOneShot(clip, volume);
        }
    }
}
