using UnityEngine;

public static class CombatRewardService
{
    public static void GrantIfKilled(PlayerController player, CombatHealth target)
    {
        if (player == null || target == null)
        {
            return;
        }

        EnemyController enemy = target.GetComponentInParent<EnemyController>();
        if (enemy == null || !target.TryClaimDeathReward())
        {
            return;
        }

        // 일반 공격, 드론, 스킬 처치가 동일한 보상 경로를 사용한다.
        AchievementManager.ReportEnemyKilled();
        MainGuideMissionManager.ReportEnemyKilled();
        TutorialManager.Report(TutorialEventType.EnemyKilled);
        player.Progression?.AddExperience(enemy.ExperienceReward);
        GrantCurrency(player, enemy);
        TryGrantEquipmentPart(player, enemy, enemy is BossEnemyController);
    }

    private static void GrantCurrency(PlayerController player, EnemyController enemy)
    {
        PlayerCurrencyWallet wallet = player.GetComponent<PlayerCurrencyWallet>();
        if (wallet == null && BaseCampManager.Instance != null)
        {
            wallet = BaseCampManager.Instance.CurrencyWallet;
        }

        if (wallet == null)
        {
            wallet = Object.FindFirstObjectByType<PlayerCurrencyWallet>();
        }

        if (wallet == null)
        {
            return;
        }

        wallet.Add(CurrencyType.Credits, enemy.CreditReward);
        // 코어 크리스탈은 보스 처치 시에만 지급한다.
        if (enemy is BossEnemyController)
        {
            wallet.Add(CurrencyType.CoreCrystals, enemy.CoreCrystalReward);
        }
    }

    private static void TryGrantEquipmentPart(PlayerController player, EnemyController enemy, bool killedBoss)
    {
        InventoryFacility inventory = BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : InventoryFacility.FindAny();
        if (inventory == null)
        {
            Debug.LogWarning("[파츠 드롭] InventoryFacility를 찾지 못해 드롭을 처리할 수 없습니다.", enemy.gameObject);
            return;
        }

        bool dropSucceeded = inventory.ForceEquipmentPartDrop || Random.value < enemy.PartDropChance;
        if (!dropSucceeded)
        {
            return;
        }

        if (inventory.EquipmentPartConfigs.Count == 0)
        {
            Debug.LogWarning("[파츠 드롭] EquipmentPartConfig 목록이 비어 있습니다.", enemy.gameObject);
            return;
        }

        EquipmentPartConfig config = inventory.EquipmentPartConfigs[
            Random.Range(0, inventory.EquipmentPartConfigs.Count)];
        // 드롭되는 파츠 레벨은 현재 플레이어 레벨을 따른다.
        int dropLevel = player != null && player.Progression != null ? player.Progression.Level : 1;
        EquipmentPartInstance part = EquipmentPartGenerator.Create(
            config,
            EquipmentPartGenerator.RollRarity(dropLevel, killedBoss),
            dropLevel);
        PlayerEquipmentPartLoadout loadout = player != null
            ? player.EquipmentPartLoadout
            : null;
        PlayerCurrencyWallet wallet = player != null
            ? player.GetComponent<PlayerCurrencyWallet>()
            : null;
        if (wallet == null && BaseCampManager.Instance != null)
        {
            wallet = BaseCampManager.Instance.CurrencyWallet;
        }

        if (!inventory.AcquireEquipmentPart(part, loadout, wallet, out int autoSaleCredits))
        {
            return;
        }

        inventory.PlayEquipmentPartDropVisual(config, part, enemy.transform.position);
        if (autoSaleCredits > 0)
        {
            Debug.Log(
                $"[파츠 자동 판매] {config.DisplayName} / {GetRarityName(part.rarity)} / "
                + $"{autoSaleCredits} 크레딧 획득",
                enemy.gameObject);
            return;
        }

        Debug.Log(
            $"[파츠 드롭] {config.DisplayName} / Lv.{part.level} / {GetRarityName(part.rarity)} / "
            + $"{GetSlotName(part.slot)} / 주옵 {part.GetScaledMainValue() * 100f:0.##}% / "
            + $"보유 {inventory.EquipmentParts.Count}개",
            enemy.gameObject);
    }

    private static string GetRarityName(EquipmentPartRarity rarity)
    {
        return rarity switch
        {
            EquipmentPartRarity.Rare => "희귀",
            EquipmentPartRarity.Epic => "영웅",
            EquipmentPartRarity.Legendary => "전설",
            _ => "일반"
        };
    }

    private static string GetSlotName(EquipmentPartSlot slot)
    {
        return slot switch
        {
            EquipmentPartSlot.Armor => "장갑",
            EquipmentPartSlot.Engine => "엔진",
            _ => "칩"
        };
    }
}
