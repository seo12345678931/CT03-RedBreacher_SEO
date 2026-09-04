using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossEncounterHud : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject bossHudPanel;
    [SerializeField] private GameObject guidMissionPanel;
    
    [Header("BoosUI")]
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TMP_Text bossHealthText;
    [SerializeField] private float resultDisplaySeconds = 4f;
    [SerializeField] private TMP_Text bossClearText;
    [SerializeField] private TMP_Text bossClearTimeText;
    [SerializeField] private GameObject bossClearTimeSubPanel;

    private BossEnemyConfig config;
    private CombatHealth bossHealth;
    private float resultHideTime;

    // 보스 체력바를 목표값이 바뀔 때만 DOTween으로 부드럽게 보간한다(플레이어 HUD와 동일 방식).
    private readonly BarAnimator bossHealthSliderAnim = new BarAnimator();
    // 보스가 처음 등장하는 프레임에는 애니메이션 없이 꽉 찬 상태로 스냅한다.
    private bool snapHealthNextRefresh;

    public bool IsBossHudVisible => bossHudPanel != null && bossHudPanel.activeInHierarchy;

    private void Awake()
    {
        Hide();
    }

    private void Update()
    {
        if (resultHideTime > 0f && Time.unscaledTime >= resultHideTime)
        {
            Hide();
        }

        Refresh();
    }

    public void Show(BossEnemyConfig bossConfig, CombatHealth health)
    {
        config = bossConfig;
        bossHealth = health;
        resultHideTime = 0f;
        snapHealthNextRefresh = true;
        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(true);
        }

        if (guidMissionPanel != null)
        {
            guidMissionPanel.SetActive(false);
        }

        SetActive(bossNameText != null ? bossNameText.gameObject : null, true);
        SetBossClearTextVisible(false);
        SetBossClearTimeVisible(false);
        Refresh();
    }

    public void Hide()
    {
        config = null;
        bossHealth = null;
        resultHideTime = 0f;
        snapHealthNextRefresh = true;
        bossHealthSliderAnim.Kill();
        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(false);
        }

        if (guidMissionPanel != null)
        {
            guidMissionPanel.SetActive(true);
        }

        SetActive(bossNameText != null ? bossNameText.gameObject : null, true);
        SetBossClearTextVisible(false);
        SetBossClearTimeVisible(false);
    }

    public void ShowResult(string title, string detail, bool success, string clearTimeText = null)
    {
        config = null;
        bossHealth = null;
        resultHideTime = Time.unscaledTime + Mathf.Max(1f, resultDisplaySeconds);
        if (bossHudPanel != null)
        {
            bossHudPanel.SetActive(true);
        }

        // 별도 결과 프리팹 없이 기존 보스 HUD를 결과 표시에도 사용한다.
        if (guidMissionPanel != null)
        {
            guidMissionPanel.SetActive(false);
        }

        if (bossClearText != null)
        {
            bossClearText.gameObject.SetActive(true);
            bossClearText.text = string.IsNullOrWhiteSpace(detail)
                ? title
                : $"{title}\n{detail}";
            bossClearText.color = success
                ? new Color(0.435f, 0.812f, 0.459f)
                : new Color(1f, 0.35f, 0.35f);
        }

        SetBossClearTime(clearTimeText);

        SetActive(bossNameText != null ? bossNameText.gameObject : null, false);

        if (bossHealthText != null)
        {
            bossHealthText.text = string.Empty;
        }

        if (bossHealthSlider != null)
        {
            bossHealthSlider.gameObject.SetActive(false);
        }
    }

    private void Refresh()
    {
        if (config == null || bossHealth == null)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, bossHealth.MaxHealth);
        float healthRate = Mathf.Clamp01(bossHealth.CurrentHealth / maxHealth);
        if (bossNameText != null)
        {
            bossNameText.text = config.DisplayName;
            bossNameText.color = Color.white;
        }

        if (bossHealthSlider != null)
        {
            bossHealthSlider.gameObject.SetActive(true);
            bossHealthSliderAnim.ApplySlider(bossHealthSlider, healthRate, snapHealthNextRefresh);
        }

        snapHealthNextRefresh = false;

        if (bossHealthText != null)
        {
            bossHealthText.text = $"{bossHealth.CurrentHealth:0} / {maxHealth:0}";
        }
    }

    private void SetBossClearTextVisible(bool visible)
    {
        if (bossClearText != null)
        {
            bossClearText.gameObject.SetActive(visible);
        }
    }

    private void SetBossClearTime(string text)
    {
        bool visible = !string.IsNullOrWhiteSpace(text);
        SetBossClearTimeVisible(visible);
        if (bossClearTimeText != null)
        {
            bossClearTimeText.text = visible ? text : string.Empty;
        }
    }

    private void SetBossClearTimeVisible(bool visible)
    {
        if (bossClearTimeSubPanel != null)
        {
            bossClearTimeSubPanel.SetActive(visible);
        }

        if (bossClearTimeText != null)
        {
            bossClearTimeText.gameObject.SetActive(visible);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void OnDisable()
    {
        bossHealthSliderAnim.Kill();
    }

    // 슬라이더 채움을 목표값이 바뀔 때만 DOTween으로 보간한다(매 프레임 트윈 생성 방지).
    private class BarAnimator
    {
        private const float FillDuration = 0.25f;

        private float target = float.NaN;
        private Tween tween;

        public void ApplySlider(Slider slider, float value, bool immediate = false)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;

            if (immediate)
            {
                target = value;
                tween?.Kill();
                tween = null;
                slider.value = value;
                return;
            }

            if (!Mathf.Approximately(target, value))
            {
                target = value;
                tween?.Kill();
                tween = slider.DOValue(value, FillDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        public void Kill()
        {
            tween?.Kill();
            tween = null;
            target = float.NaN;
        }
    }
}
