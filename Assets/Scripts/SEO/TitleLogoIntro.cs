using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 타이틀 진입 시 로고를 DOTween으로 연출하고, 인트로가 끝나기 전까지는 Tap to Start 입력을 잠근다.
/// 인트로 완료 시 입력을 풀고 지정한 오브젝트를 활성화하며 이벤트를 호출한다.
/// </summary>
[DisallowMultipleComponent]
public class TitleLogoIntro : MonoBehaviour
{
    [Header("Logo")]
    [Tooltip("애니메이션할 로고 RectTransform. 비우면 이 오브젝트 자신을 사용.")]
    [SerializeField] private RectTransform logo;
    [Tooltip("페이드에 쓸 CanvasGroup. 비우면 로고에서 찾거나 자동 추가.")]
    [SerializeField] private CanvasGroup logoGroup;

    [Header("Intro Animation")]
    [SerializeField] private float startScale = 0.7f;
    [SerializeField] private float scaleDuration = 0.6f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    [SerializeField] private float fadeInDuration = 0.5f;
    [Tooltip("로고를 보여주고 유지하는 시간(초).")]
    [SerializeField] private float holdDuration = 0.8f;
    [SerializeField] private bool fadeOutAfter = false;
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Header("Activate After Intro")]
    [Tooltip("인트로 동안 탭 입력을 잠그고, 끝나면 풀어줄 Tap to Start.")]
    [SerializeField] private TapToStartGame tapToStart;
    [Tooltip("인트로 완료 시 활성화할 오브젝트(예: Tap to Start 텍스트/그룹).")]
    [SerializeField] private GameObject[] activateOnComplete;
    [SerializeField] private UnityEvent onIntroComplete;

    private Sequence introSequence;

    private void Awake()
    {
        if (logo == null)
        {
            logo = transform as RectTransform;
        }

        if (logoGroup == null && logo != null)
        {
            logoGroup = logo.GetComponent<CanvasGroup>();
            if (logoGroup == null)
            {
                logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 인트로 동안 탭 잠금 + 완료 시 켤 오브젝트는 미리 꺼둔다.
        if (tapToStart != null)
        {
            tapToStart.SetInputLocked(true);
        }

        SetActivateTargets(false);

        // 초기 상태: 투명 + 축소.
        if (logoGroup != null)
        {
            logoGroup.alpha = 0f;
        }

        if (logo != null)
        {
            logo.localScale = Vector3.one * Mathf.Max(0.01f, startScale);
        }
    }

    private void Start()
    {
        introSequence = DOTween.Sequence().SetUpdate(true);

        if (logoGroup != null)
        {
            introSequence.Join(logoGroup.DOFade(1f, Mathf.Max(0.01f, fadeInDuration)));
        }

        if (logo != null)
        {
            introSequence.Join(logo.DOScale(1f, Mathf.Max(0.01f, scaleDuration)).SetEase(scaleEase));
        }

        introSequence.AppendInterval(Mathf.Max(0f, holdDuration));

        if (fadeOutAfter && logoGroup != null)
        {
            introSequence.Append(logoGroup.DOFade(0f, Mathf.Max(0.01f, fadeOutDuration)));
        }

        introSequence.OnComplete(Complete);
    }

    private void OnDisable()
    {
        introSequence?.Kill();
        introSequence = null;
    }

    private void Complete()
    {
        introSequence = null;

        if (tapToStart != null)
        {
            tapToStart.SetInputLocked(false);
        }

        SetActivateTargets(true);
        onIntroComplete?.Invoke();
    }

    private void SetActivateTargets(bool active)
    {
        if (activateOnComplete == null)
        {
            return;
        }

        for (int i = 0; i < activateOnComplete.Length; i++)
        {
            if (activateOnComplete[i] != null)
            {
                activateOnComplete[i].SetActive(active);
            }
        }
    }
}
