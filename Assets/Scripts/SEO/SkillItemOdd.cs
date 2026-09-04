using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillItemOdd : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text skillItemName;
    [SerializeField] private TMP_Text skillItemOddNum;
    [SerializeField] private Image rarityBackground;

    public void Setup(WeaponGachaFacility.SkillGachaEntry entry, float probabilityPercent)
    {
        PlayerSkillConfig skill = entry != null ? entry.skillConfig : null;
        Sprite skillIcon = skill != null ? skill.Icon : null;

        if (icon != null)
        {
            icon.sprite = skillIcon;
            icon.enabled = skillIcon != null;
            icon.preserveAspect = true;
        }

        SetText(skillItemName, skill != null ? skill.DisplayName : string.Empty);

        SetText(skillItemOddNum, $"{Mathf.Max(0f, probabilityPercent):0.##}%");
        SetRarityBackground(skill != null ? skill.Rarity : Rarity.Common);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
    
    private void SetRarityBackground(Rarity rarity)
    {
        if (rarityBackground == null)
        {
            return;
        }

        Color rarityColor = RarityVisuals.GetColor(rarity);
        rarityColor.a = rarityBackground.color.a;
        rarityBackground.color = rarityColor;
    }
}
