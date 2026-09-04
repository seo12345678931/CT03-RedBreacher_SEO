using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Myth/Gacha/Gacha Pool Config")]
public class GachaPoolConfig : ScriptableObject
{
    [SerializeField] private GachaCategory category;
    [SerializeField] private List<WeaponGachaFacility.WeaponGachaEntry> weaponEntries =
        new List<WeaponGachaFacility.WeaponGachaEntry>();
    [SerializeField] private List<WeaponGachaFacility.SkillGachaEntry> skillEntries =
        new List<WeaponGachaFacility.SkillGachaEntry>();

    public GachaCategory Category => category;
    public IReadOnlyList<WeaponGachaFacility.WeaponGachaEntry> WeaponEntries => weaponEntries;
    public IReadOnlyList<WeaponGachaFacility.SkillGachaEntry> SkillEntries => skillEntries;
}
