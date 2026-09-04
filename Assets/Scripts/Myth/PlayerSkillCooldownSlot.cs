using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSkillCooldownSlot : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private TMP_Text _cooldownText;
    [SerializeField] private TMP_Text _levelText;

    private PlayerAutoSkillController _skillController;
    private PlayerSkillConfig _skillConfig;

    public void Setup(PlayerAutoSkillController controller, PlayerSkillConfig skill)
    {
        _skillController = controller;
        _skillConfig = skill;

        if (skill == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (_iconImage != null)
        {
            _iconImage.sprite = skill.Icon;
            _iconImage.enabled = skill.Icon != null;
        }

        if (_levelText != null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            int level = player != null ? player.GetSkillLevel(skill) : 1;
            _levelText.text = $"Lv.{Mathf.Max(1, level)}";
        }

        UpdateCooldownVisuals();
    }

    private void Update()
    {
        UpdateCooldownVisuals();
    }

    private void UpdateCooldownVisuals()
    {
        if (_skillController == null || _skillConfig == null)
        {
            return;
        }

        float remaining = _skillController.GetRemainingCooldown(_skillConfig);
        float progress = _skillController.GetCooldownProgress01(_skillConfig);
        if (_levelText != null)
        {
            PlayerController player = _skillController.GetComponent<PlayerController>();
            _levelText.text = $"Lv.{Mathf.Max(1, player != null ? player.GetSkillLevel(_skillConfig) : 1)}";
        }

        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = progress;
        }

        if (_cooldownText != null)
        {
            if (remaining > 0f)
            {
                _cooldownText.gameObject.SetActive(true);
                // 1초 이상 남으면 소수점을 버리고 올림 정수로, 1초 미만이면 소수점 첫째자리까지 표시
                _cooldownText.text = remaining >= 1f ? $"{Mathf.CeilToInt(remaining)}" : $"{remaining:F1}";
            }
            else
            {
                _cooldownText.gameObject.SetActive(false);
            }
        }
    }
}
