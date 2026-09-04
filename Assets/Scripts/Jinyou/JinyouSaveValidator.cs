using System;
using System.Collections.Generic;
using UnityEngine;

public static class JinyouSaveValidator
{
    public static bool TryDeserializeAndValidate(string json, out JinyouSaveData data, out string error)
    {
        data = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "save json is empty";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<JinyouSaveData>(json);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }

        return Validate(data, out error);
    }

    public static bool Validate(JinyouSaveData data, out string error)
    {
        error = string.Empty;
        if (data == null)
        {
            error = "save data is null";
            return false;
        }

        if (data.version <= 0 || data.version > JinyouSaveData.CurrentVersion)
        {
            error = $"unsupported save version: {data.version}";
            return false;
        }

        if (data.lastSavedUnixTime <= 0)
        {
            error = "missing last saved timestamp";
            return false;
        }

        if (data.commanderLevel < 1 || data.mainBuildingLevel < 1)
        {
            error = "commander or main building level is invalid";
            return false;
        }

        if (data.credits < 0 || data.coreCrystals < 0)
        {
            error = "currency cannot be negative";
            return false;
        }

        if (!ValidateFacility(data.researchLab, "command center", out error)
            || !ValidateFacility(data.energyRefinery, "energy refinery", out error)
            || !ValidateFacility(data.assemblyFactory, "assembly factory", out error)
            || !ValidateFacility(data.coreCharger, "core charger", out error)
            || !ValidateFacility(data.skillHanger, "skill hanger", out error))
        {
            return false;
        }

        if (data.version >= 3 && !ValidateV3(data, out error))
        {
            return false;
        }

        if (data.version >= 4 && !ValidateV4(data, out error))
        {
            return false;
        }

        return true;
    }

    private static bool ValidateV3(JinyouSaveData data, out string error)
    {
        error = string.Empty;
        if (data.inventory == null || data.playerLoadout == null || data.equipmentLoadout == null)
        {
            error = "v3 save is missing inventory or loadout data";
            return false;
        }

        if (!ValidateCollection(data.inventory.weapons, "weapon", out error)
            || !ValidateCollection(data.inventory.skills, "skill", out error)
            || !ValidateUniqueStrings(data.inventory.drones, "drone", allowEmpty: false, out error)
            || !ValidateEquipmentParts(data, out error)
            || !ValidateSkillIds(data.playerLoadout.skillIds, out error))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(data.playerLoadout.weaponId)
            && !ContainsCollectionId(data.inventory.weapons, data.playerLoadout.weaponId))
        {
            error = $"equipped weapon is not owned: {data.playerLoadout.weaponId}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(data.playerLoadout.droneId)
            && !ContainsString(data.inventory.drones, data.playerLoadout.droneId))
        {
            error = $"equipped drone is not owned: {data.playerLoadout.droneId}";
            return false;
        }

        return true;
    }

    private static bool ValidateV4(JinyouSaveData data, out string error)
    {
        error = string.Empty;
        if (data.myth == null
            || data.myth.progression == null
            || data.myth.statAllocator == null
            || data.myth.enemySpawn == null
            || data.myth.bossTracker == null)
        {
            error = "v4 save is missing runtime progression data";
            return false;
        }

        if (data.myth.progression.level < 1
            || data.myth.progression.currentExperience < 0f
            || data.myth.progression.experienceToNextLevel <= 0f
            || data.myth.progression.statPoints < 0
            || !IsFinite(data.myth.progression.currentExperience)
            || !IsFinite(data.myth.progression.experienceToNextLevel))
        {
            error = "player progression is invalid";
            return false;
        }

        if (data.myth.statAllocator.attackLevel < 0
            || data.myth.statAllocator.healthLevel < 0
            || data.myth.statAllocator.critChanceLevel < 0
            || data.myth.statAllocator.critMultiplierLevel < 0)
        {
            error = "player stat allocation is invalid";
            return false;
        }

        if (data.myth.enemySpawn.currentRound < 1)
        {
            error = "combat round is invalid";
            return false;
        }

        if (data.myth.bossTracker.records == null)
        {
            error = "boss records are missing";
            return false;
        }

        HashSet<string> seen = new HashSet<string>();
        foreach (JinyouBossRecordSaveData record in data.myth.bossTracker.records)
        {
            if (record == null
                || string.IsNullOrWhiteSpace(record.bossId)
                || string.IsNullOrWhiteSpace(record.difficultyId))
            {
                error = "boss record has an empty id";
                return false;
            }

            if (record.attempts < 0 || record.clears < 0 || record.failures < 0 || record.bestTime < 0f || !IsFinite(record.bestTime))
            {
                error = $"boss record has invalid values: {record.bossId}/{record.difficultyId}";
                return false;
            }

            string key = $"{record.bossId}|{record.difficultyId}";
            if (!seen.Add(key))
            {
                error = $"duplicate boss record: {key}";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateFacility(JinyouCommandCenterSaveData data, string name, out string error)
    {
        return data != null
            ? ValidateFacilityTimers(data.level, data.upgradeRemainingSeconds, data.currentUpgradeDurationSeconds, name, out error)
            : MissingFacility(name, out error);
    }

    private static bool ValidateFacility(JinyouEnergyRefinerySaveData data, string name, out string error)
    {
        return data != null
            ? ValidateFacilityTimers(data.level, data.upgradeRemainingSeconds, data.currentUpgradeDurationSeconds, name, out error)
            : MissingFacility(name, out error);
    }

    private static bool ValidateFacility(JinyouAssemblyFactorySaveData data, string name, out string error)
    {
        return data != null
            ? ValidateFacilityTimers(data.level, data.upgradeRemainingSeconds, data.currentUpgradeDurationSeconds, name, out error)
            : MissingFacility(name, out error);
    }

    private static bool ValidateFacility(JinyouCoreChargerSaveData data, string name, out string error)
    {
        return data != null
            ? ValidateFacilityTimers(data.level, data.upgradeRemainingSeconds, data.currentUpgradeDurationSeconds, name, out error)
            : MissingFacility(name, out error);
    }

    private static bool ValidateFacility(JinyouSkillHangerSaveData data, string name, out string error)
    {
        return data != null
            ? ValidateFacilityTimers(data.level, data.upgradeRemainingSeconds, data.currentUpgradeDurationSeconds, name, out error)
            : MissingFacility(name, out error);
    }

    private static bool MissingFacility(string name, out string error)
    {
        error = $"{name} save is missing";
        return false;
    }

    private static bool ValidateFacilityTimers(
        int level,
        float upgradeRemainingSeconds,
        float currentUpgradeDurationSeconds,
        string name,
        out string error)
    {
        error = string.Empty;
        if (level < 1)
        {
            error = $"{name} level is invalid";
            return false;
        }

        if (upgradeRemainingSeconds < 0f
            || currentUpgradeDurationSeconds < 0f
            || !IsFinite(upgradeRemainingSeconds)
            || !IsFinite(currentUpgradeDurationSeconds))
        {
            error = $"{name} upgrade timer is invalid";
            return false;
        }

        return true;
    }

    private static bool ValidateCollection(List<InventoryFacility.CollectionProgress> entries, string label, out string error)
    {
        error = string.Empty;
        if (entries == null)
        {
            error = $"{label} list is missing";
            return false;
        }

        HashSet<string> seen = new HashSet<string>();
        foreach (InventoryFacility.CollectionProgress entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.configId))
            {
                error = $"{label} id is empty";
                return false;
            }

            if (entry.level < 0)
            {
                error = $"{label} level is invalid: {entry.configId}";
                return false;
            }

            if (!seen.Add(entry.configId))
            {
                error = $"duplicate {label} id: {entry.configId}";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateUniqueStrings(List<string> ids, string label, bool allowEmpty, out string error)
    {
        error = string.Empty;
        if (ids == null)
        {
            error = $"{label} list is missing";
            return false;
        }

        HashSet<string> seen = new HashSet<string>();
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (allowEmpty)
                {
                    continue;
                }

                error = $"{label} id is empty";
                return false;
            }

            if (!seen.Add(id))
            {
                error = $"duplicate {label} id: {id}";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateSkillIds(List<string> skillIds, out string error)
    {
        error = string.Empty;
        if (skillIds == null)
        {
            error = "skill loadout list is missing";
            return false;
        }

        return true;
    }

    private static bool ValidateEquipmentParts(JinyouSaveData data, out string error)
    {
        error = string.Empty;
        List<EquipmentPartInstance> parts = data.inventory.equipmentParts;
        if (parts == null)
        {
            error = "equipment part list is missing";
            return false;
        }

        HashSet<string> seen = new HashSet<string>();
        foreach (EquipmentPartInstance part in parts)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.instanceId) || string.IsNullOrWhiteSpace(part.configId))
            {
                error = "equipment part id is empty";
                return false;
            }

            if (part.level < 0)
            {
                error = $"equipment part level is invalid: {part.instanceId}";
                return false;
            }

            if (!seen.Add(part.instanceId))
            {
                error = $"duplicate equipment instance id: {part.instanceId}";
                return false;
            }
        }

        return ContainsEquipment(parts, data.equipmentLoadout.armorInstanceId, EquipmentPartSlot.Armor, out error)
            && ContainsEquipment(parts, data.equipmentLoadout.engineInstanceId, EquipmentPartSlot.Engine, out error)
            && ContainsEquipment(parts, data.equipmentLoadout.chipInstanceId, EquipmentPartSlot.Chip, out error);
    }

    private static bool ContainsEquipment(
        List<EquipmentPartInstance> parts,
        string instanceId,
        EquipmentPartSlot slot,
        out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return true;
        }

        foreach (EquipmentPartInstance part in parts)
        {
            if (part != null && part.instanceId == instanceId)
            {
                if (part.slot != slot)
                {
                    error = $"equipment part slot mismatch: {instanceId}";
                    return false;
                }

                return true;
            }
        }

        error = $"equipped equipment part is not owned: {instanceId}";
        return false;
    }

    private static bool ContainsCollectionId(List<InventoryFacility.CollectionProgress> entries, string id)
    {
        if (entries == null)
        {
            return false;
        }

        foreach (InventoryFacility.CollectionProgress entry in entries)
        {
            if (entry != null && entry.configId == id)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsString(List<string> entries, string id)
    {
        return entries != null && entries.Contains(id);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
