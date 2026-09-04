using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class BaseCampUpgradeStatus
{
    public static string BuildConditionText(
        IBaseCampFacility facility,
        int credits,
        int commanderLevel,
        int researchLabLevel)
    {
        if (facility == null)
        {
            return "Facility not connected";
        }

        if (facility.IsUpgrading)
        {
            return $"Upgrading... {facility.UpgradeRemainingSeconds:0}s left";
        }

        if (researchLabLevel < facility.RequiredResearchLabLevel)
        {
            return $"Research Lab Lv.{facility.RequiredResearchLabLevel} required";
        }

        int levelLimit = facility.GetLevelLimit(researchLabLevel);
        if (facility.Level >= levelLimit && facility.Level < facility.MaxLevel)
        {
            return $"Level cap Lv.{levelLimit}. Upgrade Research Lab";
        }

        if (facility.Level >= facility.MaxLevel)
        {
            return "Max level reached";
        }

        if (credits < facility.UpgradeCost)
        {
            return $"Need {facility.UpgradeCost - credits} more credits";
        }

        if (commanderLevel < facility.RequiredCommanderLevel)
        {
            return $"Commander Lv.{facility.RequiredCommanderLevel} required";
        }

        if (facility.CanStartUpgrade(credits, commanderLevel, researchLabLevel))
        {
            return "Ready to upgrade";
        }

        return "Max level reached";
    }

    public static void SetUpgradeProgress(
        Image progressFill,
        IBaseCampFacility facility,
        ref float observedUpgradeDuration)
    {
        if (progressFill == null)
        {
            return;
        }

        if (facility == null || !facility.IsUpgrading)
        {
            observedUpgradeDuration = 0f;
            progressFill.fillAmount = 0f;
            progressFill.gameObject.SetActive(false);
            return;
        }

        progressFill.gameObject.SetActive(true);
        observedUpgradeDuration = Mathf.Max(observedUpgradeDuration, facility.CurrentUpgradeDurationSeconds);

        if (observedUpgradeDuration <= 0f)
        {
            progressFill.fillAmount = 1f;
            return;
        }

        float remainingRatio = facility.UpgradeRemainingSeconds / observedUpgradeDuration;
        progressFill.fillAmount = Mathf.Clamp01(1f - remainingRatio);
    }
}

public static class BaseCampUpgradeButtonText
{
    public static void Set(
        TMP_Text labelText,
        TMP_Text costText,
        string label,
        int cost,
        bool showCost)
    {
        SetText(labelText, label);
        SetText(costText, showCost ? cost.ToString() : string.Empty);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
