using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 최상단 고정 가이드 미션 바. <see cref="MainGuideMissionManager"/>의 활성 단계를 표시하고
/// 수령 버튼으로 보상을 클레임한다. 직렬화 참조가 비어 있으면 자식 이름으로 자동 탐색한다.
/// 프리팹은 Tools/Guide/Build Guide Mission UI Prefab 으로 생성한다.
/// </summary>
[DisallowMultipleComponent]
public class MainGuideMissionPanel : MonoBehaviour
{
    [Header("References (비우면 자식 이름으로 자동 탐색)")]
    [SerializeField] private MainGuideMissionManager guideManager;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private Button claimButton;
    [SerializeField] private GameObject claimReadyBadge;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardAmountText;

    [Header("Reward Icons (재화별 스프라이트, 인스펙터에서 지정)")]
    [SerializeField] private Sprite creditsIcon;
    [SerializeField] private Sprite coreCrystalsIcon;

    [Header("Behaviour")]
    [SerializeField] private bool hideWhenAllCompleted = true;

    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // 매니저가 BaseCampManager에 의해 늦게 생성되는 경우를 대비해 구독을 재시도한다.
        if (!subscribed)
        {
            TrySubscribe();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed)
        {
            return;
        }

        ResolveReferences();
        if (guideManager == null)
        {
            return;
        }

        guideManager.OnGuideMissionsChanged.AddListener(Refresh);
        subscribed = true;

        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(HandleClaimClicked);
            claimButton.onClick.AddListener(HandleClaimClicked);
        }

        Refresh();
    }

    private void Unsubscribe()
    {
        if (guideManager != null)
        {
            guideManager.OnGuideMissionsChanged.RemoveListener(Refresh);
        }

        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(HandleClaimClicked);
        }

        subscribed = false;
    }

    private void HandleClaimClicked()
    {
        if (guideManager == null)
        {
            return;
        }

        GuideMissionConfig.GuideStepData step = guideManager.CurrentStep;
        // 도착 지점과 같은 기준(시각적 중심)으로 시작 위치를 잡아 pivot 차이로 어긋나지 않게 한다.
        Vector3 sourcePosition = rewardIcon != null
            ? RewardFlyAnimator.GetRectWorldCenter(rewardIcon.rectTransform)
            : (claimButton != null
                ? RewardFlyAnimator.GetRectWorldCenter(claimButton.transform as RectTransform)
                : transform.position);

        if (guideManager.TryClaimCurrent() && step != null && step.rewardAmount > 0)
        {
            RewardFlyAnimator.Instance.PlayReward(sourcePosition, step.rewardCurrency, step.rewardAmount, 64f);
        }
    }

    public void Refresh()
    {
        ResolveReferences();

        bool hasActiveStep = guideManager != null && !guideManager.AllCompleted;
        if (contentRoot != null && hideWhenAllCompleted)
        {
            contentRoot.SetActive(hasActiveStep);
        }

        if (!hasActiveStep)
        {
            if (claimReadyBadge != null)
            {
                claimReadyBadge.SetActive(false);
            }

            return;
        }

        GuideMissionConfig.GuideStepData step = guideManager.CurrentStep;
        bool completed = guideManager.IsCurrentCompleted;

        if (titleText != null)
        {
            titleText.text = step.title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = step.description;
        }

        if (progressText != null)
        {
            progressText.text = $"{guideManager.CurrentAmount} / {guideManager.CurrentTargetAmount}";
        }

        if (progressFill != null)
        {
            progressFill.fillAmount = guideManager.CurrentProgress01;
        }

        if (rewardIcon != null)
        {
            Sprite sprite = ResolveRewardSprite(step.rewardCurrency);
            rewardIcon.sprite = sprite;
            rewardIcon.enabled = sprite != null;
        }

        if (rewardAmountText != null)
        {
            rewardAmountText.text = step.rewardAmount > 0
                ? $"x{step.rewardAmount}"
                : string.Empty;
        }

        if (claimButton != null)
        {
            claimButton.interactable = completed;
        }

        if (claimReadyBadge != null)
        {
            claimReadyBadge.SetActive(completed);
        }
    }

    private void ResolveReferences()
    {
        guideManager ??= MainGuideMissionManager.Instance ?? FindFirstObjectByType<MainGuideMissionManager>();
        contentRoot ??= gameObject;
        titleText ??= FindChildComponent<TMP_Text>(transform, "GuideTitle_Txt");
        descriptionText ??= FindChildComponent<TMP_Text>(transform, "GuideDesc_Txt");
        progressText ??= FindChildComponent<TMP_Text>(transform, "GuideProgress_Num");
        progressFill ??= FindChildComponent<Image>(transform, "GuideProgress_Fill");
        if (claimButton == null)
        {
            Transform claim = FindDeepChild(transform, "GuideClaim_Btn");
            claimButton = claim != null ? claim.GetComponent<Button>() : null;
        }

        claimReadyBadge ??= FindDeepChild(transform, "GuideClaim_Ready")?.gameObject;
        rewardIcon ??= FindChildComponent<Image>(transform, "GuideReward_Icon");
        rewardAmountText ??= FindChildComponent<TMP_Text>(transform, "GuideReward_Num");
    }

    private Sprite ResolveRewardSprite(CurrencyType currency)
    {
        return currency switch
        {
            CurrencyType.CoreCrystals => coreCrystalsIcon,
            _ => creditsIcon,
        };
    }

    private static T FindChildComponent<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindDeepChild(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindDeepChild(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
