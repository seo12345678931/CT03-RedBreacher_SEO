using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class GachaResultSlot : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("iconImage")]
    private Image _iconImage;

    [SerializeField, FormerlySerializedAs("nameText")]
    private TMP_Text _nameText;

    [SerializeField, FormerlySerializedAs("stateText")]
    private TMP_Text _stateText;

    [SerializeField, FormerlySerializedAs("levelText")]
    private TMP_Text _levelText;

    [SerializeField, FormerlySerializedAs("progressText")]
    private TMP_Text _progressText;

    [SerializeField]
    private Image _progressImage;

    [Header("Rarity")]
    [Tooltip("비워두면 슬롯 루트의 Image(프레임)를 등급 색으로 칠한다.")]
    [SerializeField] private Image _rarityFrame;
    [SerializeField] private TMP_Text _rarityText;
    [SerializeField] private bool _tintNameByRarity = true;
    [Tooltip("등급별 등장 팝 연출 사용 여부.")]
    [SerializeField] private bool _playRevealPop = true;

    private Image _rootImage;
    private Tween _revealTween;

    private void Awake()
    {
        _rootImage = GetComponent<Image>();
    }

    public void Setup(GachaDrawResult result)
    {
        if (result == null || result.grantResult == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = result.Icon;
            _iconImage.enabled = result.Icon != null;
        }

        ApplyRarity(result.Rarity);
        PlayRevealPop(result.Rarity);

        SetText(_nameText, result.DisplayName);
        SetText(_stateText, result.grantResult.isNew ? "신규!" : "<color=#ffffff>중복</color>");
        SetText(_levelText, $"Lv.{result.grantResult.currentLevel}");
        SetText(
            _progressText,
            result.grantResult.requiredDuplicates > 0
                ? $"{result.grantResult.duplicateProgress} / {result.grantResult.requiredDuplicates}"
                : result.grantResult.coreCrystalReward > 0
                    ? $"MAX +{result.grantResult.coreCrystalReward}"
                    : "MAX");

        if (_progressImage != null)
        {
            _progressImage.fillAmount = result.grantResult.requiredDuplicates > 0
                ? (float)result.grantResult.duplicateProgress / result.grantResult.requiredDuplicates
                : 1f;
        }
    }

    private void ApplyRarity(Rarity rarity)
    {
        Color color = RarityVisuals.GetColor(rarity);

        // 전용 프레임이 지정돼 있으면 그것을, 없으면 슬롯 루트의 Image(프레임)를 등급 색으로 칠한다.
        Image frame = _rarityFrame != null ? _rarityFrame : _rootImage;
        if (frame != null)
        {
            frame.color = color;
        }

        if (_rarityText != null)
        {
            _rarityText.text = RarityVisuals.GetLabel(rarity);
            _rarityText.color = color;
        }

        if (_tintNameByRarity && _nameText != null)
        {
            _nameText.color = color;
        }
    }

    private void PlayRevealPop(Rarity rarity)
    {
        if (!_playRevealPop)
        {
            return;
        }

        _revealTween?.Kill();

        // 등급이 높을수록 더 크게 튀어나오게 해서 전설/영웅이 더 화려하게 보이도록 한다.
        float overshoot = rarity switch
        {
            Rarity.Legendary => 1.7f,
            Rarity.Epic => 1.2f,
            Rarity.Rare => 0.9f,
            _ => 0.5f
        };

        transform.localScale = Vector3.one * 0.7f;
        _revealTween = transform
            .DOScale(1f, 0.35f)
            .SetEase(Ease.OutBack, overshoot)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        _revealTween?.Kill();
        _revealTween = null;
        transform.localScale = Vector3.one;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
