using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TraitPointFacilityPanel : MonoBehaviour
{
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private PlayerStatAllocator statAllocator;
    [SerializeField] private TMP_Text availablePointText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text maxHealthText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text critMultiplierText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button maxHealthButton;
    [SerializeField] private Button critChanceButton;
    [SerializeField] private Button critMultiplierButton;
    [SerializeField] private Button resetButton;

    private void OnEnable()
    {
        ResolveReferences();
        attackButton?.onClick.AddListener(InvestAttack);
        maxHealthButton?.onClick.AddListener(InvestMaxHealth);
        critChanceButton?.onClick.AddListener(InvestCritChance);
        critMultiplierButton?.onClick.AddListener(InvestCritMultiplier);
        resetButton?.onClick.AddListener(ResetTraits);
        RefreshNow();
    }

    private void OnDisable()
    {
        attackButton?.onClick.RemoveListener(InvestAttack);
        maxHealthButton?.onClick.RemoveListener(InvestMaxHealth);
        critChanceButton?.onClick.RemoveListener(InvestCritChance);
        critMultiplierButton?.onClick.RemoveListener(InvestCritMultiplier);
        resetButton?.onClick.RemoveListener(ResetTraits);
    }

    // 매 프레임 문자열 생성/TMP 갱신을 피하기 위한 주기적 갱신 간격.
    private const float RefreshInterval = 0.25f;
    private float nextRefreshTime;

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        RefreshNow();
    }

    private void RefreshNow()
    {
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    public void InvestAttack()
    {
        ResolveReferences();
        statAllocator?.TryUpgradeAttack();
        Refresh();
    }

    public void InvestMaxHealth()
    {
        ResolveReferences();
        statAllocator?.TryUpgradeHealth();
        Refresh();
    }

    public void InvestCritChance()
    {
        ResolveReferences();
        statAllocator?.TryUpgradeCritChance();
        Refresh();
    }

    public void InvestCritMultiplier()
    {
        ResolveReferences();
        statAllocator?.TryUpgradeCritMultiplier();
        Refresh();
    }

    public void ResetTraits()
    {
        ResolveReferences();
        statAllocator?.ResetAllocationsAndRefund();
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();
        if (statAllocator == null)
        {
            return;
        }

        PlayerProgression progression = statAllocator.GetComponent<PlayerProgression>();
        int availablePoints = progression != null ? progression.StatPoints : 0;
        SetText(availablePointText, $"Trait Points {availablePoints}");
        SetText(attackText, $"Attack Lv.{statAllocator.AttackDisplayLevel}  +{statAllocator.AttackBonusPercent * 100f:0.#}%");
        SetText(maxHealthText, $"Max Health Lv.{statAllocator.HealthDisplayLevel}  +{statAllocator.HealthBonusPercent * 100f:0.#}%");
        SetText(critChanceText, $"Crit Chance Lv.{statAllocator.CritChanceDisplayLevel}  +{statAllocator.CritChanceBonus * 100f:0.#}%");
        SetText(critMultiplierText, $"Crit Multiplier Lv.{statAllocator.CritMultiplierDisplayLevel}  +{statAllocator.CritMultiplierBonus * 100f:0.#}%");
        SetText(summaryText, BuildSummary());

        SetInvestButton(attackButton, availablePoints, statAllocator.CanUpgradeAttack);
        SetInvestButton(maxHealthButton, availablePoints, statAllocator.CanUpgradeHealth);
        SetInvestButton(critChanceButton, availablePoints, statAllocator.CanUpgradeCritChance);
        SetInvestButton(critMultiplierButton, availablePoints, statAllocator.CanUpgradeCritMultiplier);
        if (resetButton != null)
        {
            resetButton.interactable = statAllocator.TotalAllocatedLevels > 0;
        }
    }

    private string BuildSummary()
    {
        return $"Attack +{statAllocator.AttackBonusPercent * 100f:0.#}%\n"
            + $"Max Health +{statAllocator.HealthBonusPercent * 100f:0.#}%\n"
            + $"Crit Chance +{statAllocator.CritChanceBonus * 100f:0.#}%\n"
            + $"Crit Multiplier +{statAllocator.CritMultiplierBonus * 100f:0.#}%";
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        if (statAllocator == null && baseCampManager?.PlayerProgression != null)
        {
            statAllocator = baseCampManager.PlayerProgression.GetComponent<PlayerStatAllocator>();
        }

        statAllocator ??= FindFirstObjectByType<PlayerStatAllocator>();
    }

    private static void SetInvestButton(Button button, int availablePoints, bool canUpgrade)
    {
        if (button != null)
        {
            button.interactable = availablePoints > 0 && canUpgrade;
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
