using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentPartSlot
{
    Armor,
    Engine,
    Chip
}

public enum EquipmentPartRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum EquipmentStatType
{
    AttackPercent,
    HealthPercent,
    AttackSpeedPercent,
    CritChance,
    CritDamage
}

[CreateAssetMenu(menuName = "Myth/Equipment/Part Config")]
public class EquipmentPartConfig : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id = "part_default";
    [SerializeField] private string displayName = "기본 파츠";
    [SerializeField] private Sprite icon;
    [SerializeField] private EquipmentPartSlot slot;

    [Header("Main Stat")]
    [SerializeField] private float commonMainValue = 0.05f;
    [SerializeField] private float rareMainValue = 0.1f;
    [SerializeField] private float epicMainValue = 0.2f;
    [SerializeField] private float legendaryMainValue = 0.3f;

    [Header("Sale Price")]
    [SerializeField] private int commonSalePrice = 50;
    [SerializeField] private int rareSalePrice = 200;
    [SerializeField] private int epicSalePrice = 800;
    [SerializeField] private int legendarySalePrice = 2400;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public EquipmentPartSlot Slot => slot;
    public EquipmentStatType MainStatType => GetMainStatType(slot);
    public float CommonMainValue => commonMainValue;
    public float RareMainValue => rareMainValue;
    public float EpicMainValue => epicMainValue;
    public float LegendaryMainValue => legendaryMainValue;
    public int CommonSalePrice => commonSalePrice;
    public int RareSalePrice => rareSalePrice;
    public int EpicSalePrice => epicSalePrice;
    public int LegendarySalePrice => legendarySalePrice;

    public float GetMainValue(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => Mathf.Max(0f, rareMainValue),
            EquipmentPartRarity.Epic => Mathf.Max(0f, epicMainValue),
            EquipmentPartRarity.Legendary => Mathf.Max(0f, legendaryMainValue),
            _ => Mathf.Max(0f, commonMainValue)
        };
    }

    public int GetSalePrice(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => Mathf.Max(0, rareSalePrice),
            EquipmentPartRarity.Epic => Mathf.Max(0, epicSalePrice),
            EquipmentPartRarity.Legendary => Mathf.Max(0, legendarySalePrice),
            _ => Mathf.Max(0, commonSalePrice)
        };
    }

    public static EquipmentStatType GetMainStatType(EquipmentPartSlot targetSlot)
    {
        return targetSlot switch
        {
            EquipmentPartSlot.Armor => EquipmentStatType.HealthPercent,
            EquipmentPartSlot.Engine => EquipmentStatType.AttackSpeedPercent,
            _ => EquipmentStatType.AttackPercent
        };
    }

    public void ConfigureRuntimeDefaults(
        string configId,
        string configDisplayName,
        EquipmentPartSlot configSlot,
        float commonValue,
        float rareValue,
        float epicValue,
        float legendaryValue)
    {
        id = configId;
        displayName = configDisplayName;
        slot = configSlot;
        commonMainValue = commonValue;
        rareMainValue = rareValue;
        epicMainValue = epicValue;
        legendaryMainValue = legendaryValue;
        commonSalePrice = 50;
        rareSalePrice = 200;
        epicSalePrice = 800;
        legendarySalePrice = 2400;
    }
}

[Serializable]
public class EquipmentSubStat
{
    public EquipmentStatType statType;
    public float value;
}

[Serializable]
public class EquipmentPartInstance
{
    public string instanceId;
    public string configId;
    public EquipmentPartSlot slot;
    public EquipmentPartRarity rarity;
    public int level = 1;
    public EquipmentStatType mainStatType;
    public float mainStatValue;
    public int salePrice;
    public List<EquipmentSubStat> subStats = new List<EquipmentSubStat>();
    // 획득 후 아직 인벤토리에서 확인하지 않은 신규 파츠 여부.
    public bool isNew;

    // 드롭 시점의 레벨에 따라 적용/표시되는 최종 수치를 계산한다.
    public float LevelMultiplier => EquipmentPartLeveling.GetMultiplier(level);
    public float GetScaledMainValue() => Mathf.Max(0f, mainStatValue) * LevelMultiplier;
    public float GetScaledSubStatValue(EquipmentSubStat subStat)
        => subStat != null ? Mathf.Max(0f, subStat.value) * LevelMultiplier : 0f;
}

public static class EquipmentPartLeveling
{
    // 레벨 1을 기준으로 레벨이 오를 때마다 기본 수치가 이 비율만큼 추가로 붙는다(레벨당 +5%).
    public const float GrowthPerLevel = 0.05f;

    public static float GetMultiplier(int level)
    {
        return 1f + Mathf.Max(0, level - 1) * GrowthPerLevel;
    }
}

public static class EquipmentPartGenerator
{
    private static readonly EquipmentStatType[] SubStatTypes =
    {
        EquipmentStatType.AttackPercent,
        EquipmentStatType.HealthPercent,
        EquipmentStatType.AttackSpeedPercent,
        EquipmentStatType.CritChance,
        EquipmentStatType.CritDamage
    };

    public static EquipmentPartInstance Create(EquipmentPartConfig config, EquipmentPartRarity rarity, int level = 1)
    {
        if (config == null)
        {
            return null;
        }

        EquipmentPartInstance instance = new EquipmentPartInstance
        {
            instanceId = Guid.NewGuid().ToString("N"),
            configId = config.Id,
            slot = config.Slot,
            rarity = rarity,
            level = Mathf.Max(1, level),
            mainStatType = config.MainStatType,
            mainStatValue = config.GetMainValue(rarity),
            salePrice = config.GetSalePrice(rarity)
        };

        // 희귀도에 따라 중복 없는 부옵을 확정해 저장한다. 전설은 보스 처치 보상으로만 롤링된다.
        int subStatCount = rarity switch
        {
            EquipmentPartRarity.Legendary => 3,
            EquipmentPartRarity.Epic => 2,
            EquipmentPartRarity.Rare => 1,
            _ => 0
        };
        List<EquipmentStatType> candidates = new List<EquipmentStatType>(SubStatTypes);
        for (int i = 0; i < subStatCount && candidates.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            EquipmentStatType statType = candidates[index];
            candidates.RemoveAt(index);
            instance.subStats.Add(new EquipmentSubStat
            {
                statType = statType,
                value = RollSubStatValue(statType, rarity)
            });
        }

        return instance;
    }

    // 레벨에 따라 고희귀도 확률이 완만히 증가한다(상한 적용). 필요하면 이 상수만 조정한다.
    private const float BaseEpicChance = 0.05f;
    private const float BaseRareChance = 0.20f;
    private const float EpicChancePerLevel = 0.005f; // 레벨당 +0.5%
    private const float RareChancePerLevel = 0.01f;  // 레벨당 +1%
    private const float MaxEpicChance = 0.25f;
    private const float MaxRareChance = 0.45f;
    private const float BaseLegendaryBossChance = 0.01f;
    private const float LegendaryBossChancePerLevel = 0.0015f; // 보스 처치 시 레벨당 +0.15%
    private const float MaxLegendaryBossChance = 0.08f;

    public static EquipmentPartRarity RollRarity(int level = 1, bool allowLegendary = false)
    {
        int steps = Mathf.Max(0, level - 1);
        float epicChance = Mathf.Min(MaxEpicChance, BaseEpicChance + steps * EpicChancePerLevel);
        float rareChance = Mathf.Min(MaxRareChance, BaseRareChance + steps * RareChancePerLevel);
        float legendaryChance = allowLegendary
            ? Mathf.Min(MaxLegendaryBossChance, BaseLegendaryBossChance + steps * LegendaryBossChancePerLevel)
            : 0f;

        // 전설은 보스 보상 전용이며, 기존 고등급 총량이 과하게 튀지 않도록 영웅 확률 일부를 대체한다.
        if (legendaryChance > 0f)
        {
            epicChance = Mathf.Max(0f, epicChance - legendaryChance);
        }

        float roll = UnityEngine.Random.value;
        if (roll < legendaryChance)
        {
            return EquipmentPartRarity.Legendary;
        }

        if (roll < legendaryChance + epicChance)
        {
            return EquipmentPartRarity.Epic;
        }

        return roll < legendaryChance + epicChance + rareChance ? EquipmentPartRarity.Rare : EquipmentPartRarity.Common;
    }

    private static float RollSubStatValue(EquipmentStatType statType, EquipmentPartRarity rarity)
    {
        bool legendary = rarity == EquipmentPartRarity.Legendary;
        bool epic = rarity == EquipmentPartRarity.Epic;
        return statType switch
        {
            EquipmentStatType.AttackPercent => UnityEngine.Random.Range(legendary ? 0.06f : epic ? 0.04f : 0.02f, legendary ? 0.12f : epic ? 0.08f : 0.05f),
            EquipmentStatType.HealthPercent => UnityEngine.Random.Range(legendary ? 0.06f : epic ? 0.04f : 0.02f, legendary ? 0.12f : epic ? 0.08f : 0.05f),
            EquipmentStatType.AttackSpeedPercent => UnityEngine.Random.Range(legendary ? 0.03f : epic ? 0.02f : 0.01f, legendary ? 0.07f : epic ? 0.05f : 0.03f),
            EquipmentStatType.CritChance => UnityEngine.Random.Range(legendary ? 0.015f : epic ? 0.01f : 0.005f, legendary ? 0.035f : epic ? 0.025f : 0.015f),
            _ => UnityEngine.Random.Range(legendary ? 0.08f : epic ? 0.05f : 0.03f, legendary ? 0.15f : epic ? 0.1f : 0.06f)
        };
    }
}
