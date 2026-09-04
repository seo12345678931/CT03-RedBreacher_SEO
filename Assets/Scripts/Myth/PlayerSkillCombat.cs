using System.Collections.Generic;
using UnityEngine;

public static class PlayerSkillCombat
{
    public static float CalculateDamage(PlayerController player, PlayerSkillConfig config)
    {
        return CalculateDamage(player, config, out _);
    }

    public static float CalculateDamage(
        PlayerController player,
        PlayerSkillConfig config,
        out bool isCritical)
    {
        isCritical = false;
        if (player == null || config == null)
        {
            return 0f;
        }

        int level = Mathf.Max(1, player.GetSkillLevel(config));
        float levelMultiplier = DuplicateLevelProgression.GetLevelMultiplier(
            level,
            config.DamagePercentPerLevel);
        float damage = (player.TotalAttackDamage * config.AttackPowerMultiplier + config.FlatDamage)
            * levelMultiplier;
        isCritical = config.CanCritical && Random.value < Mathf.Clamp01(player.CritChance);
        if (isCritical)
        {
            damage *= Mathf.Max(1f, player.CritMultiplier);
        }

        return damage;
    }

    public static List<CombatHealth> FindEnemies(Vector3 center, float radius)
    {
        List<CombatHealth> results = new List<CombatHealth>();
        IReadOnlyList<EnemyController> enemies = EnemyController.Active;
        float radiusSqr = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemy = enemies[i];
            CombatHealth health = enemy != null ? enemy.Health : null;
            if (health == null || health.IsDead)
            {
                continue;
            }

            if (CombatPlane.DistanceSqr(center, enemy.transform.position) <= radiusSqr)
            {
                results.Add(health);
            }
        }

        return results;
    }

    public static CombatHealth FindClosestEnemy(Vector3 center, float radius)
    {
        List<CombatHealth> enemies = FindEnemies(center, radius);
        CombatHealth closest = null;
        float closestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < enemies.Count; i++)
        {
            float distanceSqr = CombatPlane.DistanceSqr(center, enemies[i].transform.position);
            if (distanceSqr >= closestDistanceSqr)
            {
                continue;
            }

            closest = enemies[i];
            closestDistanceSqr = distanceSqr;
        }

        return closest;
    }

    public static bool TryFindBestAreaCenter(
        Vector3 origin,
        float castRange,
        float effectRadius,
        int minimumEnemyCount,
        out Vector3 areaCenter)
    {
        List<CombatHealth> candidates = FindEnemies(origin, castRange);
        int bestCount = 0;
        areaCenter = CombatPlane.WithFixedY(origin);

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 candidatePosition = candidates[i].transform.position;
            int nearbyCount = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (CombatPlane.DistanceSqr(candidatePosition, candidates[j].transform.position)
                    <= effectRadius * effectRadius)
                {
                    nearbyCount++;
                }
            }

            if (nearbyCount > bestCount)
            {
                bestCount = nearbyCount;
                areaCenter = CombatPlane.WithFixedY(candidatePosition);
            }
        }

        return bestCount >= Mathf.Max(1, minimumEnemyCount);
    }

    public static int ApplyAreaDamage(
        PlayerController player,
        Vector3 center,
        float radius,
        float damage,
        int maxTargets,
        float knockbackForce,
        bool isCritical = false)
    {
        List<CombatHealth> targets = FindEnemies(center, radius);
        targets.Sort((left, right) =>
            CombatPlane.DistanceSqr(center, left.transform.position)
                .CompareTo(CombatPlane.DistanceSqr(center, right.transform.position)));

        int hitCount = 0;
        int targetLimit = Mathf.Max(1, maxTargets);
        for (int i = 0; i < targets.Count && hitCount < targetLimit; i++)
        {
            CombatHealth target = targets[i];
            EnemyController enemy = target.GetComponentInParent<EnemyController>();
            if (enemy == null)
            {
                continue;
            }

            target.TakeDamage(damage, isCritical);
            if (knockbackForce > 0f)
            {
                Vector3 knockbackDirection = CombatPlane.Direction(center, target.transform.position);
                enemy.ApplyKnockback(knockbackDirection, knockbackForce);
            }

            CombatRewardService.GrantIfKilled(player, target);
            hitCount++;
        }

        return hitCount;
    }
}
