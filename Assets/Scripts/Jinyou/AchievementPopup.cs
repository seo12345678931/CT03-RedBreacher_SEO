using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AchievementPopup : MonoBehaviour
{
    [SerializeField] private AchievementManager achievementManager;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject achievementItemTemplate;
    [SerializeField] private AudioClip btnAudio;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private PanelTweenTransition panelTransition;
    private AudioSource audioSource;

    private void Awake()
    {
        ResolveReferences();
        EnsurePanelTransition();
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsurePanelTransition();
        EnsureAudioSource();
        if (achievementManager != null)
        {
            achievementManager.OnAchievementsChanged.AddListener(Rebuild);
        }

        Rebuild();
    }

    private void OnDisable()
    {
        if (achievementManager != null)
        {
            achievementManager.OnAchievementsChanged.RemoveListener(Rebuild);
        }
    }

    public void Rebuild()
    {
        ResolveReferences();
        ClearSpawnedItems();

        if (achievementManager == null || contentRoot == null || achievementItemTemplate == null)
        {
            return;
        }

        bool templateIsSceneChild = achievementItemTemplate.transform.IsChildOf(contentRoot);
        if (templateIsSceneChild)
        {
            achievementItemTemplate.SetActive(false);
        }
        IReadOnlyList<AchievementManager.AchievementEntry> achievements = achievementManager.Achievements;
        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementManager.AchievementEntry achievement = achievements[i];
            if (achievement == null)
            {
                continue;
            }

            GameObject item = Instantiate(achievementItemTemplate, contentRoot);
            item.SetActive(true);
            BindItem(item, achievement);
            spawnedItems.Add(item);
        }
    }

    private void BindItem(GameObject item, AchievementManager.AchievementEntry achievement)
    {
        SetText(item, "AchName_Txt", achievement.Title);
        SetText(item, "AchDesc_Txt", string.Format(achievement.Description, achievement.NextTargetAmount));
        SetText(item, "Progress_Num", $"{achievement.CurrentAmount} / {achievement.NextTargetAmount}");
        SetText(item, "Count", achievement.RewardAmount.ToString());

        Image achievementIcon = FindChildComponent<Image>(item.transform, "Ach_Icon");
        if (achievementIcon != null && achievement.IconSprite != null)
        {
            achievementIcon.sprite = achievement.IconSprite;
        }

        Image progressFill = FindChildComponent<Image>(item.transform, "Progress_Fill");
        if (progressFill != null)
        {
            progressFill.fillAmount = achievement.Progress01;
        }

        Image rewardClaim = FindChildComponent<Image>(item.transform, "Reward_Claim");
        if (rewardClaim != null)
        {
            rewardClaim.enabled = achievement.Completed;

            // 프리팹 수정 없이 보상 이미지를 수령 버튼으로 사용한다.
            Button claimButton = rewardClaim.GetComponent<Button>();
            if (claimButton == null)
            {
                claimButton = rewardClaim.gameObject.AddComponent<Button>();
            }

            claimButton.targetGraphic = rewardClaim;
            claimButton.interactable = achievement.Completed;

            RectTransform claimRect = rewardClaim.rectTransform;
            string achievementId = achievement.Id;
            CurrencyType rewardCurrency = achievement.RewardCurrency;
            int rewardAmount = achievement.RewardAmount;
            claimButton.onClick.AddListener(() =>
            {
                PlayButtonAudio();
                // 클레임 성공 시 Rebuild로 이 아이템이 파괴되므로 위치를 먼저 캡처한다.
                // 도착 지점과 같은 기준(시각적 중심)으로 잡아야 pivot이 중앙이 아니어도 시작이 어긋나지 않는다.
                Vector3 sourcePosition = RewardFlyAnimator.GetRectWorldCenter(claimRect);
                float iconSize = claimRect != null ? claimRect.rect.width : 56f;
                if (achievementManager.TryClaimReward(achievementId))
                {
                    RewardFlyAnimator.Instance.PlayReward(sourcePosition, rewardCurrency, rewardAmount, iconSize);
                }
            });
        }

        GameObject alertIcon = FindDeepChild(item.transform, "alert")?.gameObject;
        if (alertIcon != null)
        {
            // 보상 수령 가능한 완료 상태일 때만 알림 아이콘을 노출한다.
            alertIcon.SetActive(achievement.Completed);
        }
    }

    private void ClearSpawnedItems()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i]);
            }
        }

        spawnedItems.Clear();
    }

    private void ResolveReferences()
    {
        achievementManager ??= AchievementManager.Instance ?? FindFirstObjectByType<AchievementManager>();
        contentRoot ??= FindDeepChild(transform, "Content");
        achievementItemTemplate ??= FindDeepChild(transform, "Achievement_Button")?.gameObject;
    }

    private void EnsurePanelTransition()
    {
        // 도전과제 팝업도 prefab 수정 없이 공통 열림/닫힘 연출을 적용한다.
        panelTransition ??= GetComponent<PanelTweenTransition>();
        if (panelTransition == null)
        {
            panelTransition = gameObject.AddComponent<PanelTweenTransition>();
        }
    }

    private void EnsureAudioSource()
    {
        audioSource ??= GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void PlayButtonAudio()
    {
        if (btnAudio == null)
        {
            return;
        }

        EnsureAudioSource();
        audioSource.PlayOneShot(btnAudio);
    }

    private static void SetText(GameObject root, string childName, string value)
    {
        TMP_Text target = FindChildComponent<TMP_Text>(root.transform, childName);
        if (target != null)
        {
            target.text = value;
        }
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
