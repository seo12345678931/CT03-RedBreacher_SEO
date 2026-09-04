using UnityEngine;

/// <summary>
/// 무기/스킬/장비가 공유하는 아이템 등급. 멤버 순서/값은 EquipmentPartRarity와 동일하게 맞춰
/// 추후 장비까지 한 체계로 통일하기 쉽게 한다. (Common=0, Rare=1, Epic=2, Legendary=3)
/// </summary>
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// 등급별 표시 색/라벨을 한 곳에서 관리해 결과창·확률표기 UI가 일관되게 보이도록 한다.
/// </summary>
public static class RarityVisuals
{
    public static string GetLabel(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Rare => "희귀",
            Rarity.Epic => "영웅",
            Rarity.Legendary => "전설",
            _ => "일반"
        };
    }

    public static Color GetColor(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Rare => new Color(0.30f, 0.66f, 1f),       // 파랑
            Rarity.Epic => new Color(0.72f, 0.40f, 1f),       // 보라
            Rarity.Legendary => new Color(1f, 0.72f, 0.20f),  // 금빛
            _ => new Color(0.78f, 0.80f, 0.84f)               // 회백(일반)
        };
    }

    public static string GetColorHex(Rarity rarity)
    {
        return ColorUtility.ToHtmlStringRGB(GetColor(rarity));
    }
}
