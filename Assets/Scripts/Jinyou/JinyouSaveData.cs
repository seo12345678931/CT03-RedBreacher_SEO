using System;
using System.Collections.Generic;

[Serializable]
public class JinyouSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public long lastSavedUnixTime;
    public int commanderLevel = 1;
    public int mainBuildingLevel = 1;
    public int credits = 500;
    public int coreCrystals;
    public JinyouOfflineRewardSaveData lastOfflineReward = new JinyouOfflineRewardSaveData();
    public JinyouCommandCenterSaveData researchLab = new JinyouCommandCenterSaveData();
    public JinyouEnergyRefinerySaveData energyRefinery = new JinyouEnergyRefinerySaveData();
    public JinyouAssemblyFactorySaveData assemblyFactory = new JinyouAssemblyFactorySaveData();
    public JinyouCoreChargerSaveData coreCharger = new JinyouCoreChargerSaveData();
    public JinyouSkillHangerSaveData skillHanger = new JinyouSkillHangerSaveData();
    public JinyouAchievementSaveData achievements = new JinyouAchievementSaveData();
    public JinyouDailyMissionSaveData dailyMissions = new JinyouDailyMissionSaveData();
    public JinyouGuideMissionSaveData guideMissions = new JinyouGuideMissionSaveData();
    public JinyouInventorySaveData inventory = new JinyouInventorySaveData();
    public JinyouPlayerLoadoutSaveData playerLoadout = new JinyouPlayerLoadoutSaveData();
    public JinyouEquipmentLoadoutSaveData equipmentLoadout = new JinyouEquipmentLoadoutSaveData();
    public JinyouMythSaveData myth = new JinyouMythSaveData();
}

// ───────── Myth(전투) 진행도. version >= 4부터 사용 ─────────
[Serializable]
public class JinyouMythSaveData
{
    public JinyouPlayerProgressionSaveData progression = new JinyouPlayerProgressionSaveData();
    public JinyouPlayerStatAllocatorSaveData statAllocator = new JinyouPlayerStatAllocatorSaveData();
    public JinyouEnemySpawnSaveData enemySpawn = new JinyouEnemySpawnSaveData();
    public JinyouBossTrackerSaveData bossTracker = new JinyouBossTrackerSaveData();
    public JinyouTutorialSaveData tutorial = new JinyouTutorialSaveData();
}

[Serializable]
public class JinyouTutorialSaveData
{
    public bool captured;
    public bool completed;
    public int stepIndex;
    public bool baseCompleted;
    public int baseStepIndex;
    public bool coreChargerCompleted;
    public int coreChargerStepIndex;
    public bool inventoryCompleted;
    public int inventoryStepIndex;
    public bool bossEncounterCompleted;
    public int bossEncounterStepIndex;
}

[Serializable]
public class JinyouPlayerProgressionSaveData
{
    public bool captured; // 캡처 시점에 컴포넌트가 있었는지. false면 복원 스킵.
    public int level = 1;
    public float currentExperience;
    public float experienceToNextLevel = 100f;
    public int statPoints;
}

[Serializable]
public class JinyouPlayerStatAllocatorSaveData
{
    public bool captured;
    public int attackLevel;
    public int healthLevel;
    public int critChanceLevel;
    public int critMultiplierLevel;
}

[Serializable]
public class JinyouPlayerStatSaveData : JinyouPlayerStatAllocatorSaveData
{
}

[Serializable]
public class JinyouEnemySpawnSaveData
{
    public bool captured;
    public int currentRound = 1;
}

[Serializable]
public class JinyouCombatProgressSaveData : JinyouEnemySpawnSaveData
{
}

[Serializable]
public class JinyouBossTrackerSaveData
{
    public string selectedBossId;
    public string selectedDifficultyId;
    public List<JinyouBossRecordSaveData> records = new List<JinyouBossRecordSaveData>();
}

[Serializable]
public class JinyouBossRecordSaveData
{
    public string bossId;
    public string difficultyId;
    public int attempts;
    public int clears;
    public int failures;
    public float bestTime;
}

[Serializable]
public class JinyouInventorySaveData
{
    public List<InventoryFacility.CollectionProgress> weapons = new List<InventoryFacility.CollectionProgress>();
    public List<InventoryFacility.CollectionProgress> skills = new List<InventoryFacility.CollectionProgress>();
    public List<string> drones = new List<string>();
    public List<EquipmentPartInstance> equipmentParts = new List<EquipmentPartInstance>();
    public bool equipmentAutoSellEnabled;
    public EquipmentPartRarity equipmentAutoSellMaxRarity = EquipmentPartRarity.Rare;
}

[Serializable]
public class JinyouPlayerLoadoutSaveData
{
    public string unitId;
    public string weaponId;
    public string droneId;
    public List<string> skillIds = new List<string>();
}

[Serializable]
public class JinyouEquipmentLoadoutSaveData
{
    public string armorInstanceId;
    public string engineInstanceId;
    public string chipInstanceId;
}

[Serializable]
public class JinyouOfflineRewardSaveData
{
    public float elapsedSeconds;
    public float appliedSeconds;
    public int refineryCreditsAdded;
    public int bossTicketsAdded;

    public bool HasReward => refineryCreditsAdded > 0 || bossTicketsAdded > 0;
}

[Serializable]
public class JinyouCommandCenterSaveData
{
    public int level = 1;
    public int bossTickets;
    public float bossTicketProductionSeconds;
    public bool isUpgrading;
    public float upgradeRemainingSeconds;
    public float currentUpgradeDurationSeconds;
}

[Serializable]
public class JinyouEnergyRefinerySaveData
{
    public int level = 1;
    public int storedCredits;
    public bool isUpgrading;
    public float upgradeRemainingSeconds;
    public float currentUpgradeDurationSeconds;
}

[Serializable]
public class JinyouAssemblyFactorySaveData
{
    public int level = 1;
    public string selectedMenuId;
    public int selectedWeaponIndex;
    public string selectedWeaponId;
    public string selectedDroneId;
    public bool isUpgrading;
    public float upgradeRemainingSeconds;
    public float currentUpgradeDurationSeconds;
    public List<JinyouMenuSaveData> menus = new List<JinyouMenuSaveData>();
    public List<int> weaponEnhanceLevels = new List<int>();
    public List<JinyouEnhancementLevelSaveData> weaponEnhancements = new List<JinyouEnhancementLevelSaveData>();
    public List<JinyouEnhancementLevelSaveData> droneEnhancements = new List<JinyouEnhancementLevelSaveData>();
}

[Serializable]
public class JinyouEnhancementLevelSaveData
{
    public string configId;
    public int level;
}

[Serializable]
public class JinyouMenuSaveData
{
    public string menuId;
    public bool unlocked;
}

[Serializable]
public class JinyouCoreChargerSaveData
{
    public int level = 1;
    public bool isUpgrading;
    public float upgradeRemainingSeconds;
    public float currentUpgradeDurationSeconds;
    public List<int> convertedStageIndices = new List<int>();
}

[Serializable]
public class JinyouSkillHangerSaveData
{
    public int level = 1;
    public bool isUpgrading;
    public float upgradeRemainingSeconds;
    public float currentUpgradeDurationSeconds;
}

[Serializable]
public class JinyouAchievementSaveData
{
    public List<JinyouAchievementEntrySaveData> entries = new List<JinyouAchievementEntrySaveData>();
}

[Serializable]
public class JinyouAchievementEntrySaveData
{
    public string id;
    public int currentAmount;
    public int completedCount;
}

[Serializable]
public class JinyouDailyMissionSaveData
{
    public string dateKey;
    public List<JinyouDailyMissionEntrySaveData> entries = new List<JinyouDailyMissionEntrySaveData>();
}

[Serializable]
public class JinyouDailyMissionEntrySaveData
{
    public string id;
    public int currentAmount;
    public bool rewardClaimed;
}

[Serializable]
public class JinyouGuideMissionSaveData
{
    // 순차형 가이드 미션은 활성 단계 인덱스와 해당 단계 진행도만 저장한다.
    public int currentIndex;
    public int currentAmount;
    public int bossTicketGrantedStepIndex = -1;
}
