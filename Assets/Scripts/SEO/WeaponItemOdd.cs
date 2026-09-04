using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponItemOdd : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text weaponItemName;
    [SerializeField] private TMP_Text weaponItemType;
    [SerializeField] private TMP_Text weaponItemOddNum;
    [SerializeField] private Image rarityBackground;

    public void Setup(WeaponGachaFacility.WeaponGachaEntry entry, float probabilityPercent)
    {
        ProjectileConfig weapon = entry != null ? entry.weaponConfig : null;
        Sprite weaponIcon = weapon != null ? weapon.Icon : null;

        if (icon != null)
        {
            icon.sprite = weaponIcon;
            icon.enabled = weaponIcon != null;
            icon.preserveAspect = true;
        }

        SetText(weaponItemName, weapon != null ? weapon.DisplayName : string.Empty);
        SetText(weaponItemType, weapon != null ? weapon.WeaponCategory : string.Empty);
        SetRarityBackground(weapon != null ? weapon.Rarity : Rarity.Common);

        SetText(weaponItemOddNum, $"{Mathf.Max(0f, probabilityPercent):0.##}%");
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
