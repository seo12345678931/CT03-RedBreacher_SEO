using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 가챠 버튼 → 결과 화면 사이에 영상 연출을 재생한다.
/// 결과는 이미 계산된 뒤라 최고 등급에 따라 다른 영상을 틀 수 있고(전설 전용 연출 등),
/// 영상이 없거나 재생 실패 시에는 즉시 다음 단계(결과)로 넘어가 흐름이 막히지 않게 한다.
///
/// 주의: VideoPlayer가 올라간 GameObject를 SetActive(false)로 끄면 재생/준비가 멈춘다.
/// 그래서 오브젝트를 끄지 않고 CanvasGroup 알파로 숨기고, 전환은 페이드 인/아웃으로 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class GachaVideoPlayer : MonoBehaviour
{
    [Serializable]
    public class RarityClip
    {
        public Rarity rarity;
        public VideoClip clip;
    }

    [Header("References")]
    [Tooltip("영상 오버레이 루트. SetActive로 끄지 않고 CanvasGroup 알파로 숨긴다. (풀스크린 RawImage가 달린 오브젝트)")]
    [SerializeField] private GameObject videoRoot;
    [Tooltip("숨김/페이드에 사용할 CanvasGroup. 비우면 videoRoot에서 찾거나 런타임에 자동 추가한다.")]
    [SerializeField] private CanvasGroup videoGroup;
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("선택: 스킵 버튼.")]
    [SerializeField] private Button skipButton;

    [Header("Clips")]
    [SerializeField] private VideoClip defaultClip;
    [Tooltip("최고 등급이 이 등급 이상일 때 우선 재생할 영상(높은 등급부터 매칭). 비우면 기본 영상 사용.")]
    [SerializeField] private List<RarityClip> rarityClips = new List<RarityClip>();

    [Header("Transition")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Options")]
    [SerializeField] private bool allowSkip = true;
    [Tooltip("재생 시작 후, 종료 이벤트가 안 올 때를 대비한 추가 안전 시간(초).")]
    [SerializeField] private float safetyBufferSeconds = 1.5f;

    private Action onFinished;
    private bool finished;
    private Coroutine safetyRoutine;
    private Tween fadeTween;
    private bool prepareHooked;

    private void Awake()
    {
        if (videoPlayer == null && videoRoot != null)
        {
            videoPlayer = videoRoot.GetComponentInChildren<VideoPlayer>(true);
        }

        EnsureVideoGroup();

        if (videoPlayer != null)
        {
            // 씬 시작 시 멋대로 재생되지 않게 한다(준비는 Play 호출 때 수행).
            videoPlayer.playOnAwake = false;
        }

        HideInstant();
    }

    private void OnEnable()
    {
        if (skipButton != null)
        {
            skipButton.onClick.AddListener(Skip);
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += HandleLoopPoint;
        }
    }

    private void OnDisable()
    {
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleLoopPoint;
            if (prepareHooked)
            {
                videoPlayer.prepareCompleted -= HandlePrepared;
                prepareHooked = false;
            }
        }

        fadeTween?.Kill();
        fadeTween = null;
    }

    /// <summary>이 컴포넌트가 재생할 영상이 하나라도 있는지(없으면 호출부가 영상 단계를 건너뛰면 됨).</summary>
    public bool HasAnyClip
    {
        get
        {
            if (defaultClip != null)
            {
                return true;
            }

            for (int i = 0; i < rarityClips.Count; i++)
            {
                if (rarityClips[i] != null && rarityClips[i].clip != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>최고 등급에 맞는 영상을 페이드 인 → 재생 → 페이드 아웃 후 onComplete를 한 번 호출한다.</summary>
    public void Play(Rarity highestRarity, Action onComplete)
    {
        VideoClip clip = ResolveClip(highestRarity);
        if (videoPlayer == null || clip == null)
        {
            onComplete?.Invoke();
            return;
        }

        onFinished = onComplete;
        finished = false;

        // VideoPlayer는 비활성 GameObject에서 Prepare가 불가능하다.
        // 숨김은 CanvasGroup 알파로 하므로 오브젝트는 항상 켜둔 채 활성화만 보장한다.
        EnsureVideoGroup();
        ActivateForPlayback();
        if (!videoPlayer.isActiveAndEnabled)
        {
            // 부모 계층이 비활성이라 활성화하지 못한 경우: 예외 대신 영상을 건너뛰고 결과로.
            Debug.LogWarning(
                "GachaVideoPlayer: VideoPlayer가 비활성 상태라 영상을 건너뜁니다. " +
                "영상 오버레이는 결과 패널 밑이 아니라 항상 활성인 캔버스에 두세요.",
                this);
            Action skipCallback = onFinished;
            onFinished = null;
            skipCallback?.Invoke();
            return;
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(allowSkip);
        }

        videoPlayer.isLooping = false;
        videoPlayer.clip = clip;
        videoPlayer.Stop();

        // 준비 완료 후 재생해야 첫 프레임 검은 화면/끊김을 막는다.
        if (!prepareHooked)
        {
            videoPlayer.prepareCompleted += HandlePrepared;
            prepareHooked = true;
        }

        videoPlayer.Prepare();

        // 영상이 준비되는 동안 오버레이를 페이드 인해 부드럽게 전환한다.
        FadeIn();
    }

    public void Skip()
    {
        if (allowSkip)
        {
            Finish();
        }
    }

    private void HandlePrepared(VideoPlayer source)
    {
        if (finished)
        {
            return;
        }

        source.Play();

        if (safetyRoutine != null)
        {
            StopCoroutine(safetyRoutine);
        }

        safetyRoutine = StartCoroutine(SafetyFinish(source.clip));
    }

    private VideoClip ResolveClip(Rarity highestRarity)
    {
        VideoClip best = null;
        int bestRarity = -1;
        for (int i = 0; i < rarityClips.Count; i++)
        {
            RarityClip entry = rarityClips[i];
            if (entry == null || entry.clip == null)
            {
                continue;
            }

            if ((int)highestRarity >= (int)entry.rarity && (int)entry.rarity > bestRarity)
            {
                best = entry.clip;
                bestRarity = (int)entry.rarity;
            }
        }

        return best != null ? best : defaultClip;
    }

    private IEnumerator SafetyFinish(VideoClip clip)
    {
        // 재생 시작 시점 기준 길이+버퍼. 종료 이벤트가 안 와도 멈추지 않도록 강제 종료 보장.
        double length = clip != null ? clip.length : 0d;
        float wait = (float)length + Mathf.Max(0f, safetyBufferSeconds);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, wait));
        Finish();
    }

    private void HandleLoopPoint(VideoPlayer source)
    {
        Finish();
    }

    private void Finish()
    {
        if (finished)
        {
            return;
        }

        finished = true;

        if (safetyRoutine != null)
        {
            StopCoroutine(safetyRoutine);
            safetyRoutine = null;
        }

        // 페이드 아웃으로 결과 화면에 부드럽게 넘긴다. 페이드가 끝난 뒤 영상 정지 + 콜백.
        FadeOut(() =>
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }

            Action callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        });
    }

    private void FadeIn()
    {
        if (videoGroup == null)
        {
            // CanvasGroup이 없으면 최후 수단으로 즉시 표시.
            if (videoRoot != null)
            {
                videoRoot.SetActive(true);
            }

            return;
        }

        fadeTween?.Kill();
        videoGroup.blocksRaycasts = true;
        videoGroup.interactable = true;
        videoGroup.alpha = 0f;
        fadeTween = videoGroup
            .DOFade(1f, Mathf.Max(0.01f, fadeInDuration))
            .SetUpdate(true);
    }

    private void FadeOut(Action onComplete)
    {
        if (videoGroup == null)
        {
            if (videoRoot != null)
            {
                videoRoot.SetActive(false);
            }

            onComplete?.Invoke();
            return;
        }

        fadeTween?.Kill();
        fadeTween = videoGroup
            .DOFade(0f, Mathf.Max(0.01f, fadeOutDuration))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                videoGroup.blocksRaycasts = false;
                videoGroup.interactable = false;
                onComplete?.Invoke();
            });
    }

    // 숨김은 알파로 하되, 재생을 위해 오브젝트/컴포넌트는 활성 상태로 만든다.
    private void ActivateForPlayback()
    {
        if (videoRoot != null && !videoRoot.activeSelf)
        {
            videoRoot.SetActive(true);
        }

        if (videoPlayer != null)
        {
            if (!videoPlayer.gameObject.activeSelf)
            {
                videoPlayer.gameObject.SetActive(true);
            }

            videoPlayer.enabled = true;
        }
    }

    private void EnsureVideoGroup()
    {
        if (videoGroup != null)
        {
            return;
        }

        if (videoRoot != null)
        {
            videoGroup = videoRoot.GetComponent<CanvasGroup>();
            if (videoGroup == null)
            {
                videoGroup = videoRoot.AddComponent<CanvasGroup>();
            }
        }
    }

    // 오브젝트를 끄지 않고(=VideoPlayer를 살려둔 채) 즉시 숨긴다(초기화용).
    private void HideInstant()
    {
        fadeTween?.Kill();
        fadeTween = null;

        if (videoGroup != null)
        {
            videoGroup.alpha = 0f;
            videoGroup.blocksRaycasts = false;
            videoGroup.interactable = false;
            return;
        }

        if (videoRoot != null)
        {
            videoRoot.SetActive(false);
        }
    }
}
