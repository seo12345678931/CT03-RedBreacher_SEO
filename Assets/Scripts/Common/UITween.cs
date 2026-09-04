using DG.Tweening;
using UnityEngine;

/// <summary>
/// UI 전용 DOTween 효과 모음. 각 효과의 단일 출처(single source of truth)로,
/// 스크립트마다 펀치/셰이크/페이드 보일러플레이트를 반복 작성하지 않도록 한다.
///
/// 모든 효과는 UI이므로 SetUpdate(true)로 Time.timeScale의 영향을 받지 않으며,
/// SetTarget으로 대상에 묶어 DOKill 등으로 안전하게 정리할 수 있게 한다.
/// 사용 예: slotRect.Punch();  iconTransform.Punch(0.2f);  group.FadeOut();
/// </summary>
public static class UITween
{
    /// <summary>효과 강도/타이밍의 기본값. 톤을 바꾸려면 여기 한 곳만 수정한다.</summary>
    public static class T
    {
        public const float PunchScale = 0.12f;   // 버튼 "톡" 펀치 기본 강도
        public const float PunchDuration = 0.18f; // 펀치/셰이크 기본 길이
        public const float ShakePixels = 12f;     // 좌우 흔들림 기본 거리(px)
        public const float FadeDuration = 0.16f;  // 페이드 기본 길이

        public const int PunchVibrato = 8;
        public const float PunchElasticity = 0.75f;
        public const int ShakeVibrato = 10;
        public const float ShakeElasticity = 0.6f;
    }

    /// <summary>버튼/아이콘이 "톡" 튀는 펀치 스케일 피드백. 연타 시 누적되지 않도록 진행 중 펀치를 끝상태(원래 스케일)로 정리한 뒤 재생한다.</summary>
    public static Tween Punch(this Transform target, float strength = -1f, float duration = -1f)
    {
        if (target == null)
        {
            return null;
        }

        // 진행 중인 펀치를 끝상태로 완료시켜 원래 스케일을 보장한다(별도 base-scale 캐싱 불필요).
        target.DOComplete();
        float scale = strength < 0f ? T.PunchScale : strength;
        float time = duration < 0f ? T.PunchDuration : duration;
        return target
            .DOPunchScale(Vector3.one * Mathf.Max(0f, scale), Mathf.Max(0.01f, time), T.PunchVibrato, T.PunchElasticity)
            .SetUpdate(true)
            .SetTarget(target);
    }

    /// <summary>잘못된 입력 등을 알리는 좌우 흔들림(앵커 펀치).</summary>
    public static Tween ShakeX(this RectTransform target, float pixels = -1f, float duration = -1f)
    {
        float distance = pixels < 0f ? T.ShakePixels : pixels;
        return target.ShakeX(new Vector2(distance, 0f), duration);
    }

    /// <summary>축을 직접 지정하는 흔들림(앵커 펀치).</summary>
    public static Tween ShakeX(this RectTransform target, Vector2 strength, float duration = -1f)
    {
        if (target == null)
        {
            return null;
        }

        target.DOComplete();
        float time = duration < 0f ? T.PunchDuration : duration;
        return target
            .DOPunchAnchorPos(strength, Mathf.Max(0.01f, time), T.ShakeVibrato, T.ShakeElasticity)
            .SetUpdate(true)
            .SetTarget(target);
    }

    /// <summary>CanvasGroup 페이드 인.</summary>
    public static Tween FadeIn(this CanvasGroup group, float duration = -1f)
    {
        if (group == null)
        {
            return null;
        }

        float time = duration < 0f ? T.FadeDuration : duration;
        return group.DOFade(1f, Mathf.Max(0.01f, time))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(group);
    }

    /// <summary>CanvasGroup 페이드 아웃.</summary>
    public static Tween FadeOut(this CanvasGroup group, float duration = -1f)
    {
        if (group == null)
        {
            return null;
        }

        float time = duration < 0f ? T.FadeDuration : duration;
        return group.DOFade(0f, Mathf.Max(0.01f, time))
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .SetTarget(group);
    }

    /// <summary>대상에 걸린 트윈을 끝상태로 완료 후 제거한다(스케일/위치 드리프트 없이 안전하게 정리).</summary>
    public static void Stop(this Transform target)
    {
        if (target == null)
        {
            return;
        }

        target.DOComplete();
        target.DOKill(false);
    }
}
