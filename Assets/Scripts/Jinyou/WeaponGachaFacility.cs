using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum GachaCategory
{
    Weapon,
    Skill
}

[Serializable]
public class GachaDrawResult
{
    public GachaCategory category;
    public ProjectileConfig weaponConfig;
    public PlayerSkillConfig skillConfig;
    public InventoryFacility.CollectionGrantResult grantResult;

    public string DisplayName => category == GachaCategory.Weapon
        ? weaponConfig != null ? weaponConfig.DisplayName : string.Empty
        : skillConfig != null ? skillConfig.DisplayName : string.Empty;
    public Sprite Icon => category == GachaCategory.Weapon
        ? weaponConfig != null ? weaponConfig.Icon : null
        : skillConfig != null ? skillConfig.Icon : null;
    public Rarity Rarity => category == GachaCategory.Weapon
        ? weaponConfig != null ? weaponConfig.Rarity : Rarity.Common
        : skillConfig != null ? skillConfig.Rarity : Rarity.Common;
}

public class WeaponGachaFacility : MonoBehaviour
{
    [Serializable]
    public class WeaponGachaEntry
    {
        public ProjectileConfig weaponConfig;
        [Min(0f)] public float weight = 1f;
        public bool enabled = true;
    }

    [Serializable]
    public class SkillGachaEntry
    {
        public PlayerSkillConfig skillConfig;
        [Min(0f)] public float weight = 1f;
        public bool enabled = true;
    }

    [Serializable]
    public class RarityOdds
    {
        public Rarity rarity;
        [Min(0f)] public float percent;
    }

    private struct RarityCandidate
    {
        public UnityEngine.Object config;
        public float weight;
        public Rarity rarity;
    }

    [Header("Gacha Pools")]
    [SerializeField] private GachaPoolConfig weaponPool;
    [SerializeField] private GachaPoolConfig skillPool;

    [Header("Core Crystal Cost")]
    [SerializeField] private int weaponDrawCost = 10;
    [SerializeField] private int skillDrawCost = 10;
    [SerializeField] private int multiDrawCount = 10;

    [Header("Rarity Odds (%)")]
    [SerializeField] private List<RarityOdds> weaponRarityOdds = new List<RarityOdds>
    {
        new RarityOdds { rarity = Rarity.Common, percent = 60f },
        new RarityOdds { rarity = Rarity.Rare, percent = 27f },
        new RarityOdds { rarity = Rarity.Epic, percent = 10f },
        new RarityOdds { rarity = Rarity.Legendary, percent = 3f }
    };
    [SerializeField] private List<RarityOdds> skillRarityOdds = new List<RarityOdds>
    {
        new RarityOdds { rarity = Rarity.Common, percent = 50f },
        new RarityOdds { rarity = Rarity.Rare, percent = 30f },
        new RarityOdds { rarity = Rarity.Epic, percent = 15f },
        new RarityOdds { rarity = Rarity.Legendary, percent = 5f }
    };
    [Tooltip("멀티(10연차) 뽑기에서 희귀 이상이 하나도 없으면 마지막 1개를 희귀 이상으로 보장한다.")]
    [SerializeField] private bool guaranteeRareOnMultiDraw = true;

    [Header("Legacy Weapon Table")]
    [SerializeField] private List<WeaponGachaEntry> drawTable = new List<WeaponGachaEntry>();

    [Header("References")]
    [SerializeField] private InventoryFacility inventory;
    [SerializeField] private PlayerCurrencyWallet currencyWallet;

    [Header("Events")]
    public UnityEvent<ProjectileConfig> OnWeaponDrawn = new UnityEvent<ProjectileConfig>();
    public UnityEvent<PlayerSkillConfig> OnSkillDrawn = new UnityEvent<PlayerSkillConfig>();
    public UnityEvent<GachaDrawResult> OnResultDrawn = new UnityEvent<GachaDrawResult>();
    public UnityEvent OnDrawCompleted = new UnityEvent();

    private readonly List<ProjectileConfig> lastDrawResults = new List<ProjectileConfig>();
    private readonly List<GachaDrawResult> lastResults = new List<GachaDrawResult>();

    public int DrawCost => GetSingleDrawCost(GachaCategory.Weapon);
    public int MultiDrawCount => Mathf.Max(1, multiDrawCount);
    public IReadOnlyList<WeaponGachaEntry> DrawTable => GetWeaponEntries();
    public IReadOnlyList<ProjectileConfig> LastDrawResults => lastDrawResults;
    public IReadOnlyList<GachaDrawResult> LastResults => lastResults;

    private void Awake()
    {
        ResolveReferences();
    }

    public bool CanDraw(GachaCategory category, int count = 1)
    {
        ResolveReferences();
        return inventory != null
            && currencyWallet != null
            && GetValidDrawEntryCount(category) > 0
            && currencyWallet.CanSpend(CurrencyType.CoreCrystals, GetDrawCost(category, count));
    }

    public int GetDrawCost(GachaCategory category, int count)
    {
        return GetSingleDrawCost(category) * Mathf.Max(1, count);
    }

    public bool TryDraw(GachaCategory category, int count = 1)
    {
        count = Mathf.Max(1, count);
        ResolveReferences();
        if (inventory == null || currencyWallet == null)
        {
            return false;
        }

        List<UnityEngine.Object> pickedConfigs = PickConfigs(category, count);
        if (pickedConfigs.Count != count)
        {
            return false;
        }

        int cost = GetDrawCost(category, count);
        if (!currencyWallet.TrySpend(CurrencyType.CoreCrystals, cost))
        {
            return false;
        }

        GrantPickedConfigs(category, pickedConfigs);
        return true;
    }

    public void Draw(GachaCategory category, int count = 1)
    {
        count = Mathf.Max(1, count);
        ResolveReferences();
        if (inventory == null)
        {
            return;
        }

        List<UnityEngine.Object> pickedConfigs = PickConfigs(category, count);
        if (pickedConfigs.Count == count)
        {
            GrantPickedConfigs(category, pickedConfigs);
        }
    }

    public IReadOnlyList<WeaponGachaEntry> GetWeaponEntries()
    {
        return weaponPool != null && weaponPool.Category == GachaCategory.Weapon
            ? weaponPool.WeaponEntries
            : drawTable;
    }

    public IReadOnlyList<SkillGachaEntry> GetSkillEntries()
    {
        return skillPool != null && skillPool.Category == GachaCategory.Skill
            ? skillPool.SkillEntries
            : Array.Empty<SkillGachaEntry>();
    }

    // 기존 무기 전용 호출부 호환용 API다.
    public bool CanDraw(int coreCrystals, int count = 1)
    {
        return ResolveReferences() != null
            && GetValidDrawEntryCount(GachaCategory.Weapon) > 0
            && coreCrystals >= GetDrawCost(count);
    }

    public int GetDrawCost(int count)
    {
        return GetDrawCost(GachaCategory.Weapon, count);
    }

    public bool TryDraw(ref int availableCoreCrystals, int count = 1)
    {
        count = Mathf.Max(1, count);
        if (!CanDraw(availableCoreCrystals, count))
        {
            return false;
        }

        List<UnityEngine.Object> pickedConfigs = PickConfigs(GachaCategory.Weapon, count);
        if (pickedConfigs.Count != count)
        {
            return false;
        }

        availableCoreCrystals -= GetDrawCost(count);
        GrantPickedConfigs(GachaCategory.Weapon, pickedConfigs);
        return true;
    }

    public void Draw(int count = 1)
    {
        Draw(GachaCategory.Weapon, count);
    }

    private int GetSingleDrawCost(GachaCategory category)
    {
        return category == GachaCategory.Weapon
            ? Mathf.Max(0, weaponDrawCost)
            : Mathf.Max(0, skillDrawCost);
    }

    private List<UnityEngine.Object> PickConfigs(GachaCategory category, int count)
    {
        List<RarityCandidate> candidates = BuildCandidates(category);
        float[] oddsTable = BuildOddsTable(category);

        List<UnityEngine.Object> results = new List<UnityEngine.Object>(count);
        for (int i = 0; i < count; i++)
        {
            UnityEngine.Object config = PickFromCandidates(candidates, oddsTable, Rarity.Common);
            if (config == null)
            {
                results.Clear();
                return results;
            }

            results.Add(config);
        }

        // 10연차(멀티) 최소 보장: 희귀 이상이 하나도 없으면 마지막 1개를 희귀 이상으로 교체한다.
        if (guaranteeRareOnMultiDraw
            && count >= MultiDrawCount
            && !ContainsRareOrHigher(results, category)
            && HasCandidateOfAtLeast(candidates, Rarity.Rare))
        {
            UnityEngine.Object guaranteed = PickFromCandidates(candidates, oddsTable, Rarity.Rare);
            if (guaranteed != null)
            {
                results[results.Count - 1] = guaranteed;
            }
        }

        return results;
    }

    private void GrantPickedConfigs(GachaCategory category, IReadOnlyList<UnityEngine.Object> configs)
    {
        lastResults.Clear();
        lastDrawResults.Clear();

        for (int i = 0; i < configs.Count; i++)
        {
            GachaDrawResult result = category == GachaCategory.Weapon
                ? GrantWeapon(configs[i] as ProjectileConfig)
                : GrantSkill(configs[i] as PlayerSkillConfig);
            if (result == null)
            {
                continue;
            }

            lastResults.Add(result);
            OnResultDrawn.Invoke(result);
        }

        OnDrawCompleted.Invoke();
    }

    private GachaDrawResult GrantWeapon(ProjectileConfig weapon)
    {
        if (weapon == null)
        {
            return null;
        }

        InventoryFacility.CollectionGrantResult grantResult = inventory.GrantWeapon(weapon);
        if (!grantResult.success)
        {
            return null;
        }

        lastDrawResults.Add(weapon);
        OnWeaponDrawn.Invoke(weapon);
        return new GachaDrawResult
        {
            category = GachaCategory.Weapon,
            weaponConfig = weapon,
            grantResult = grantResult
        };
    }

    private GachaDrawResult GrantSkill(PlayerSkillConfig skill)
    {
        if (skill == null)
        {
            return null;
        }

        InventoryFacility.CollectionGrantResult grantResult = inventory.GrantSkill(skill);
        if (!grantResult.success)
        {
            return null;
        }

        OnSkillDrawn.Invoke(skill);
        return new GachaDrawResult
        {
            category = GachaCategory.Skill,
            skillConfig = skill,
            grantResult = grantResult
        };
    }

    // 풀에 실제 존재하는 등급만 반영해 합이 100이 되도록 정규화한 확률(UI 확률표기용).
    public IReadOnlyList<RarityOdds> GetNormalizedRarityOdds(GachaCategory category)
    {
        List<RarityCandidate> candidates = BuildCandidates(category);
        float[] oddsTable = BuildOddsTable(category);

        bool[] present = new bool[4];
        for (int i = 0; i < candidates.Count; i++)
        {
            present[(int)candidates[i].rarity] = true;
        }

        float total = 0f;
        for (int r = 0; r < present.Length; r++)
        {
            if (present[r])
            {
                total += oddsTable[r];
            }
        }

        List<RarityOdds> result = new List<RarityOdds>();
        for (int r = 0; r < present.Length; r++)
        {
            if (!present[r])
            {
                continue;
            }

            result.Add(new RarityOdds
            {
                rarity = (Rarity)r,
                percent = total > 0f ? oddsTable[r] / total * 100f : 0f
            });
        }

        return result;
    }

    public float GetWeaponEntryProbabilityPercent(WeaponGachaEntry entry)
    {
        if (!IsValid(entry))
        {
            return 0f;
        }

        return GetEntryProbabilityPercent(GachaCategory.Weapon, entry.weaponConfig, entry.weight, entry.weaponConfig.Rarity);
    }

    public float GetSkillEntryProbabilityPercent(SkillGachaEntry entry)
    {
        if (!IsValid(entry))
        {
            return 0f;
        }

        return GetEntryProbabilityPercent(GachaCategory.Skill, entry.skillConfig, entry.weight, entry.skillConfig.Rarity);
    }

    /// <summary>
    /// 정규화된 등급 확률 * 같은 등급 내에의 아이템 자체 확률(entry.weight)을 합쳐 최종적으로 확률을 출력한다.
    /// </summary>
    /// <param name="category">종류 (무기/스킬) 참조</param>
    /// <param name="config"></param>
    /// <param name="weight">아이템별 확률 참조</param>
    /// <param name="rarity">등급별 확률 참조</param>
    /// <returns></returns>
    private float GetEntryProbabilityPercent(GachaCategory category, UnityEngine.Object config, float weight, Rarity rarity)
    {
        List<RarityCandidate> candidates = BuildCandidates(category);
        float[] oddsTable = BuildOddsTable(category);

        bool[] present = new bool[4];
        for (int i = 0; i < candidates.Count; i++)
        {
            present[(int)candidates[i].rarity] = true;
        }

        float totalRarityWeight = 0f;
        for (int r = 0; r < present.Length; r++)
        {
            if (present[r])
            {
                totalRarityWeight += oddsTable[r];
            }
        }

        if (totalRarityWeight <= 0f)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(0f, candidates[i].weight);
            }

            return totalWeight > 0f ? Mathf.Max(0f, weight) / totalWeight * 100f : 0f;
        }

        int rarityIndex = (int)rarity;
        if (rarityIndex < 0 || rarityIndex >= oddsTable.Length || !present[rarityIndex])
        {
            return 0f;
        }

        float sameRarityWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].rarity == rarity)
            {
                sameRarityWeight += Mathf.Max(0f, candidates[i].weight);
            }
        }

        if (sameRarityWeight <= 0f)
        {
            return 0f;
        }

        float normalizedRarityPercent = oddsTable[rarityIndex] / totalRarityWeight * 100f;
        float entryPercentInRarity = Mathf.Max(0f, weight) / sameRarityWeight;
        return normalizedRarityPercent * entryPercentInRarity;
    }

    private List<RarityCandidate> BuildCandidates(GachaCategory category)
    {
        List<RarityCandidate> list = new List<RarityCandidate>();
        if (category == GachaCategory.Weapon)
        {
            IReadOnlyList<WeaponGachaEntry> entries = GetWeaponEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                WeaponGachaEntry entry = entries[i];
                if (IsValid(entry))
                {
                    list.Add(new RarityCandidate
                    {
                        config = entry.weaponConfig,
                        weight = entry.weight,
                        rarity = entry.weaponConfig.Rarity
                    });
                }
            }
        }
        else
        {
            IReadOnlyList<SkillGachaEntry> entries = GetSkillEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                SkillGachaEntry entry = entries[i];
                if (IsValid(entry))
                {
                    list.Add(new RarityCandidate
                    {
                        config = entry.skillConfig,
                        weight = entry.weight,
                        rarity = entry.skillConfig.Rarity
                    });
                }
            }
        }

        return list;
    }

    private float[] BuildOddsTable(GachaCategory category)
    {
        float[] table = new float[4];
        List<RarityOdds> odds = category == GachaCategory.Weapon ? weaponRarityOdds : skillRarityOdds;
        if (odds == null)
        {
            return table;
        }

        for (int i = 0; i < odds.Count; i++)
        {
            if (odds[i] == null)
            {
                continue;
            }

            int index = (int)odds[i].rarity;
            if (index >= 0 && index < table.Length)
            {
                table[index] += Mathf.Max(0f, odds[i].percent);
            }
        }

        return table;
    }

    // 1단계: 등급을 확률로 뽑고 → 2단계: 그 등급 안에서 가중치로 아이템을 뽑는다.
    private UnityEngine.Object PickFromCandidates(List<RarityCandidate> candidates, float[] oddsTable, Rarity minRarity)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        bool[] present = new bool[4];
        for (int i = 0; i < candidates.Count; i++)
        {
            if ((int)candidates[i].rarity >= (int)minRarity)
            {
                present[(int)candidates[i].rarity] = true;
            }
        }

        float totalRarityWeight = 0f;
        for (int r = 0; r < present.Length; r++)
        {
            if (present[r])
            {
                totalRarityWeight += oddsTable[r];
            }
        }

        // 등급 확률이 전혀 없으면(미설정) 최소등급 이상에서 바로 가중치로 뽑는다.
        if (totalRarityWeight <= 0f)
        {
            return PickByWeight(candidates, minRarity, null);
        }

        float roll = UnityEngine.Random.Range(0f, totalRarityWeight);
        Rarity chosenRarity = Rarity.Common;
        bool found = false;
        for (int r = 0; r < present.Length; r++)
        {
            if (!present[r])
            {
                continue;
            }

            roll -= oddsTable[r];
            chosenRarity = (Rarity)r;
            if (roll <= 0f)
            {
                found = true;
                break;
            }
        }

        // 부동소수 오차로 못 정했으면 존재하는 가장 높은 등급으로 보정.
        if (!found)
        {
            for (int r = present.Length - 1; r >= 0; r--)
            {
                if (present[r])
                {
                    chosenRarity = (Rarity)r;
                    break;
                }
            }
        }

        return PickByWeight(candidates, minRarity, chosenRarity);
    }

    private static UnityEngine.Object PickByWeight(List<RarityCandidate> candidates, Rarity minRarity, Rarity? rarityFilter)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!MatchesFilter(candidates[i], minRarity, rarityFilter))
            {
                continue;
            }

            total += Mathf.Max(0f, candidates[i].weight);
        }

        if (total <= 0f)
        {
            // 가중치 합이 0이면 조건에 맞는 첫 후보를 반환한다.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (MatchesFilter(candidates[i], minRarity, rarityFilter))
                {
                    return candidates[i].config;
                }
            }

            return null;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!MatchesFilter(candidates[i], minRarity, rarityFilter))
            {
                continue;
            }

            roll -= Mathf.Max(0f, candidates[i].weight);
            if (roll <= 0f)
            {
                return candidates[i].config;
            }
        }

        return null;
    }

    private static bool MatchesFilter(RarityCandidate candidate, Rarity minRarity, Rarity? rarityFilter)
    {
        if ((int)candidate.rarity < (int)minRarity)
        {
            return false;
        }

        return !rarityFilter.HasValue || candidate.rarity == rarityFilter.Value;
    }

    private static bool HasCandidateOfAtLeast(List<RarityCandidate> candidates, Rarity minRarity)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if ((int)candidates[i].rarity >= (int)minRarity)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsRareOrHigher(List<UnityEngine.Object> configs, GachaCategory category)
    {
        for (int i = 0; i < configs.Count; i++)
        {
            if ((int)GetConfigRarity(configs[i], category) >= (int)Rarity.Rare)
            {
                return true;
            }
        }

        return false;
    }

    private static Rarity GetConfigRarity(UnityEngine.Object config, GachaCategory category)
    {
        if (category == GachaCategory.Weapon)
        {
            return config is ProjectileConfig weapon ? weapon.Rarity : Rarity.Common;
        }

        return config is PlayerSkillConfig skill ? skill.Rarity : Rarity.Common;
    }

    private int GetValidDrawEntryCount(GachaCategory category)
    {
        int count = 0;
        if (category == GachaCategory.Weapon)
        {
            IReadOnlyList<WeaponGachaEntry> entries = GetWeaponEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsValid(entries[i]))
                {
                    count++;
                }
            }
        }
        else
        {
            IReadOnlyList<SkillGachaEntry> entries = GetSkillEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsValid(entries[i]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsValid(WeaponGachaEntry entry)
    {
        return entry != null && entry.enabled && entry.weaponConfig != null && entry.weight > 0f;
    }

    private static bool IsValid(SkillGachaEntry entry)
    {
        return entry != null && entry.enabled && entry.skillConfig != null && entry.weight > 0f;
    }

    private InventoryFacility ResolveReferences()
    {
        inventory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : FindFirstObjectByType<InventoryFacility>(FindObjectsInactive.Include);
        currencyWallet ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.CurrencyWallet
            : FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
        return inventory;
    }

    private void OnValidate()
    {
        weaponDrawCost = Mathf.Max(0, weaponDrawCost);
        skillDrawCost = Mathf.Max(0, skillDrawCost);
        multiDrawCount = Mathf.Max(1, multiDrawCount);
        drawTable ??= new List<WeaponGachaEntry>();

        for (int i = 0; i < drawTable.Count; i++)
        {
            WeaponGachaEntry entry = drawTable[i];
            if (entry != null)
            {
                entry.weight = Mathf.Max(0f, entry.weight);
            }
        }
    }
}
