using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AssemblyFactoryPanel : MonoBehaviour
{
    [SerializeField] private BaseCampManager baseCampManager;
    
    [Header("Buttons")]
    [SerializeField] private Button weaponMenuButton;
    [SerializeField] private Button droneMenuButton;
    [SerializeField] private Button weaponEnhanceButton;
    
    [Header("Base Upgrade")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text upgradeRemainingText;
    [SerializeField] private TMP_Text upgradeCostText;
    [SerializeField] private TMP_Text upgradeConditionText;
    [SerializeField] private Image upgradeProgressFill;
    [SerializeField] private Image coinIcon;
    
    [Header("AssemblyFactory Base Texts")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pleasSelectText;
    [SerializeField] private TMP_Text weaponCurrentAttackText;
    [SerializeField] private TMP_Text weaponAttackLevelText;
    [SerializeField] private TMP_Text weaponEnhanceCostText;
    [SerializeField] private GameObject weaponCost;
    
    [Header("Selected")]
    [SerializeField] private TMP_Text selectedWeaponNameText;
    [SerializeField] private TMP_Text selectedWeaponLevelText;
    [SerializeField] private Image selectedWeaponIcon;
    [SerializeField] private RawImage selectedDroneIcon;
    
    [Header("LoadoutSelection")]
    [SerializeField] private PlayerLoadoutSelectionPanel loadoutSelectionPanel;

    private AssemblyFactory assemblyFactory;
    private PlayerLoadoutSelectionPanel loadoutSelectionTemplate;
    private GameObject independentLoadoutPanelObject;
    private float observedUpgradeDuration;

    private void OnEnable()
    {
        ResolveReferences();
        upgradeButton?.onClick.AddListener(UpgradeFactory);
        weaponMenuButton?.onClick.AddListener(OpenWeaponSelection);
        droneMenuButton?.onClick.AddListener(OpenDroneSelection);
        weaponEnhanceButton?.onClick.AddListener(EnhanceSelected);
        RefreshNow();
    }

    private void OnDisable()
    {
        upgradeButton?.onClick.RemoveListener(UpgradeFactory);
        weaponMenuButton?.onClick.RemoveListener(OpenWeaponSelection);
        droneMenuButton?.onClick.RemoveListener(OpenDroneSelection);
        weaponEnhanceButton?.onClick.RemoveListener(EnhanceSelected);
    }

    private void OnDestroy()
    {
        if (independentLoadoutPanelObject != null)
        {
            Destroy(independentLoadoutPanelObject);
        }
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

    public void Configure(
        BaseCampManager manager,
        Button upgrade,
        Button skill,
        Button parts,
        TMP_Text level,
        TMP_Text upgradeLabel)
    {
        baseCampManager = manager;
        upgradeButton = upgrade;
        weaponMenuButton = skill;
        droneMenuButton = parts;
        levelText = level;
        upgradeText = upgradeLabel;
        Refresh();
    }

    public void SelectWeapon(ProjectileConfig weaponConfig)
    {
        baseCampManager?.SelectAssemblyWeapon(weaponConfig);
        Refresh();
    }

    public void SelectDrone(DroneConfig droneConfig)
    {
        baseCampManager?.SelectAssemblyDrone(droneConfig);
        Refresh();
    }

    public void OpenWeaponSelection()
    {
        ResolveReferences();
        if (loadoutSelectionPanel != null)
        {
            loadoutSelectionPanel.OpenWeaponsForSelection(SelectWeapon);
        }
    }

    public void OpenDroneSelection()
    {
        ResolveReferences();
        if (loadoutSelectionPanel != null)
        {
            loadoutSelectionPanel.OpenDronesForSelection(SelectDrone);
        }
    }

    private void EnhanceSelected()
    {
        if (assemblyFactory == null)
        {
            return;
        }

        if (assemblyFactory.SelectedMenuId == "drone")
        {
            baseCampManager?.EnhanceAssemblyDrone();
        }
        else
        {
            baseCampManager?.EnhanceAssemblyWeapon();
        }

        Refresh();
    }

    private void UpgradeFactory()
    {
        baseCampManager?.UpgradeAssemblyFactory();
        Refresh();
    }

    private void Refresh()
    {
        ResolveReferences();
        if (assemblyFactory == null)
        {
            SetActive(coinIcon != null ? coinIcon.gameObject : null, false);
            SetUpgradeRemainingText(upgradeRemainingText, false, 0f);
            SetActive(upgradeText != null ? upgradeText.gameObject : null, true);
            ClearEnhancementStatTexts();
            return;
        }

        bool droneMode = assemblyFactory.SelectedMenuId == "drone";
        RefreshSelectedTargetDisplay(droneMode);
        SetText(levelText, $"Lv.{assemblyFactory.Level}");
        SetText(upgradeText, droneMode
            ? BuildSelectedDroneHeader()
            : BuildSelectedWeaponHeader());
        SetText(pleasSelectText, droneMode ? BuildDroneText() : BuildWeaponText());
        if (baseCampManager != null)
        {
            int researchLabLevel = baseCampManager.CommandCenter != null
                ? baseCampManager.CommandCenter.Level
                : 1;
            SetText(upgradeConditionText, BaseCampUpgradeStatus.BuildConditionText(
                assemblyFactory,
                baseCampManager.Credits,
                baseCampManager.CommanderLevel,
                researchLabLevel));
        }
        
        SetButtonLabel(weaponEnhanceButton, droneMode
            ? BuildDroneEnhanceButtonText()
            : BuildWeaponEnhanceButtonText());
        BaseCampUpgradeButtonText.Set(
            upgradeText,
            upgradeCostText,
            assemblyFactory.Level >= assemblyFactory.MaxLevel
                ? "최대 레벨"
                : "업그레이드",
            assemblyFactory.UpgradeCost,
            !assemblyFactory.IsUpgrading && assemblyFactory.Level < assemblyFactory.MaxLevel);
        SetUpgradeRemainingText(
            upgradeRemainingText,
            assemblyFactory.IsUpgrading,
            assemblyFactory.UpgradeRemainingSeconds);
        SetActive(upgradeText != null ? upgradeText.gameObject : null, !assemblyFactory.IsUpgrading);
        SetActive(coinIcon != null ? coinIcon.gameObject : null, !assemblyFactory.IsUpgrading);

        SetActive(weaponEnhanceButton != null ? weaponEnhanceButton.gameObject : null, true);
        SetActive(upgradeButton != null ? upgradeButton.gameObject : null, true);
        SetActive(weaponMenuButton != null ? weaponMenuButton.gameObject : null, true);
        SetActive(droneMenuButton != null ? droneMenuButton.gameObject : null, true);

        if (weaponMenuButton != null)
        {
            weaponMenuButton.interactable = true;
        }

        if (droneMenuButton != null)
        {
            droneMenuButton.interactable = true;
        }

        if (upgradeButton != null && baseCampManager != null)
        {
            int researchLabLevel = baseCampManager.CommandCenter != null
                ? baseCampManager.CommandCenter.Level
                : 1;
            upgradeButton.interactable = assemblyFactory.CanStartUpgrade(
                baseCampManager.Credits,
                baseCampManager.CommanderLevel,
                researchLabLevel);
        }

        BaseCampUpgradeStatus.SetUpgradeProgress(
            upgradeProgressFill,
            assemblyFactory,
            ref observedUpgradeDuration);

        if (weaponEnhanceButton != null && baseCampManager != null)
        {
            weaponEnhanceButton.interactable = droneMode
                ? assemblyFactory.CanEnhanceSelectedDrone(baseCampManager.Credits)
                : assemblyFactory.CanEnhanceSelectedWeapon(baseCampManager.Credits);
        }
    }

    private void RefreshSelectedTargetDisplay(bool droneMode)
    {
        if (assemblyFactory == null)
        {
            SetText(selectedWeaponNameText, string.Empty);
            SetText(selectedWeaponLevelText, string.Empty);
            SetIcon(selectedWeaponIcon, null);
            SetDroneIcon(selectedDroneIcon, null);
            return;
        }

        if (droneMode)
        {
            AssemblyFactory.DroneEnhancement enhancement = assemblyFactory.SelectedDroneEnhancement;
            DroneConfig drone = enhancement?.droneConfig;
            SetText(selectedWeaponNameText, drone != null ? drone.DisplayName : string.Empty);
            SetText(selectedWeaponLevelText, enhancement != null && drone != null
                ? $"Lv.{enhancement.enhanceLevel}/{enhancement.maxEnhanceLevel}"
                : string.Empty);
            SetIcon(selectedWeaponIcon, null);
            SetDroneIcon(selectedDroneIcon, drone);
            return;
        }

        AssemblyFactory.WeaponEnhancement weaponEnhancement = assemblyFactory.SelectedWeaponEnhancement;
        ProjectileConfig weapon = weaponEnhancement?.weaponConfig;
        SetText(selectedWeaponNameText, weapon != null ? weapon.DisplayName : string.Empty);
        SetText(selectedWeaponLevelText, weaponEnhancement != null && weapon != null
            ? $"Lv.{weaponEnhancement.enhanceLevel}/{weaponEnhancement.MaxEnhanceLevel}"
            : string.Empty);
        SetIcon(selectedWeaponIcon, weapon != null ? weapon.Icon : null);
        SetDroneIcon(selectedDroneIcon, null);
    }

    private string BuildWeaponText()
    {
        AssemblyFactory.WeaponEnhancement enhancement = assemblyFactory.SelectedWeaponEnhancement;
        ProjectileConfig weapon = enhancement?.weaponConfig;
        if (enhancement == null || weapon == null)
        {
            ClearEnhancementStatTexts();
            return "강화할 무기/드론을 선택하세요.";
        }

        SetActive(weaponCost, true);
        float attackBonus = enhancement.GetStatBonus(AssemblyFactory.WeaponEnhancementStat.AttackDamage);
        float currentAttack = weapon.AttackDamage + attackBonus;
        float nextIncrease = GetNextWeaponAttackIncrease(enhancement);
        float nextAttack = currentAttack + nextIncrease;

        SetText(weaponCurrentAttackText, $"피해량: <color=#EC9A0E>{currentAttack:0}</color>");

        if (enhancement.IsMaxLevel)
        {
            SetText(weaponCurrentAttackText, $"피해량: <color=#EC9A0E>{currentAttack:0}</color>");
            SetText(weaponAttackLevelText, "<color=#4AD787>최대치</color>");
            SetText(weaponEnhanceCostText, "None");
            return string.Empty;
        }

        SetText(weaponAttackLevelText, $"{currentAttack:0}  ->  <color=#4AD787>{nextAttack:0}</color>");
        SetText(weaponEnhanceCostText, $"{enhancement.NextEnhanceCost}");
        return string.Empty;
    }

    private string BuildDroneText()
    {
        AssemblyFactory.DroneEnhancement enhancement = assemblyFactory.SelectedDroneEnhancement;
        DroneConfig drone = enhancement?.droneConfig;
        if (enhancement == null || drone == null)
        {
            ClearEnhancementStatTexts();
            return "강화하고자 하는 무기/드론을 선택하세요.";
        }

        SetActive(weaponCost, true);
        float currentAttack = drone.AttackDamage + enhancement.AttackDamageBonus;
        float nextAttack = currentAttack + enhancement.NextAttackDamageIncrease;

        SetText(weaponCurrentAttackText, $"피해량: <color=#EC9A0E>{currentAttack:0.##}</color>");

        if (enhancement.IsMaxLevel)
        {
            SetText(weaponCurrentAttackText, $"피해량: <color=#EC9A0E>{currentAttack:0.##}</color>");
            SetText(weaponAttackLevelText, "<color=#4AD787>최대치</color>");
            SetText(weaponEnhanceCostText, "None");
            return string.Empty;
        }

        SetText(weaponAttackLevelText, $"{currentAttack:0.##}  ->  <color=#4AD787>{nextAttack:0.##}</color>");
        SetText(weaponEnhanceCostText, $"{enhancement.NextEnhanceCost}");
        return string.Empty;
    }

    private string BuildSelectedWeaponHeader()
    {
        ProjectileConfig weapon = assemblyFactory.SelectedWeaponConfig;
        return weapon != null ? $"Enhancing Weapon SO: {weapon.DisplayName}" : "Select a Weapon SO";
    }

    private void ClearEnhancementStatTexts()
    {
        SetText(weaponCurrentAttackText, string.Empty);
        SetText(weaponAttackLevelText, string.Empty);
        SetText(weaponEnhanceCostText, string.Empty);
        SetActive(weaponCost, false);
    }

    private string BuildSelectedDroneHeader()
    {
        DroneConfig drone = assemblyFactory.SelectedDroneConfig;
        return drone != null ? $"Enhancing Drone SO: {drone.DisplayName}" : "Select a Drone SO";
    }

    private string BuildWeaponEnhanceButtonText()
    {
        AssemblyFactory.WeaponEnhancement enhancement = assemblyFactory.SelectedWeaponEnhancement;
        if (enhancement?.weaponConfig == null)
        {
            return "강화하기";
        }

        if (enhancement.IsMaxLevel)
        {
            return $"{enhancement.weaponConfig.DisplayName} 모두 완료됨";
        }

        float increase = GetNextWeaponAttackIncrease(enhancement);
        return $"강화하기 {enhancement.weaponConfig.DisplayName} +{increase:0}";
    }

    private string BuildDroneEnhanceButtonText()
    {
        AssemblyFactory.DroneEnhancement enhancement = assemblyFactory.SelectedDroneEnhancement;
        if (enhancement?.droneConfig == null)
        {
            return "드론 및 무기를 먼저 선택하세요.";
        }

        return enhancement.IsMaxLevel
            ? $"{enhancement.droneConfig.DisplayName} 모두 완료됨"
            : $"강화하기 {enhancement.droneConfig.DisplayName} +{enhancement.NextAttackDamageIncrease:0.##}";
    }

    private static float GetNextWeaponAttackIncrease(AssemblyFactory.WeaponEnhancement enhancement)
    {
        AssemblyFactory.WeaponEnhancementLevel nextLevel =
            enhancement?.GetEnhancementLevel(enhancement.enhanceLevel);
        if (nextLevel?.statIncreases == null)
        {
            return 0f;
        }

        float increase = 0f;
        foreach (AssemblyFactory.WeaponStatIncrease statIncrease in nextLevel.statIncreases)
        {
            if (statIncrease != null
                && statIncrease.stat == AssemblyFactory.WeaponEnhancementStat.AttackDamage)
            {
                increase += statIncrease.amount;
            }
        }

        return increase;
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        assemblyFactory = baseCampManager != null
            ? baseCampManager.AssemblyFactory
            : FindFirstObjectByType<AssemblyFactory>();
        EnsureIndependentLoadoutPanel();
    }

    private void EnsureIndependentLoadoutPanel()
    {
        if (independentLoadoutPanelObject != null && loadoutSelectionPanel != null)
        {
            return;
        }

        loadoutSelectionTemplate ??= loadoutSelectionPanel != null
            ? loadoutSelectionPanel
            : FindFirstObjectByType<PlayerLoadoutSelectionPanel>(FindObjectsInactive.Include);
        if (loadoutSelectionTemplate == null)
        {
            return;
        }

        Canvas rootCanvas = loadoutSelectionTemplate.GetComponentInParent<Canvas>(true);
        Transform parent = rootCanvas != null ? rootCanvas.transform : transform.root;
        independentLoadoutPanelObject = Instantiate(loadoutSelectionTemplate.gameObject, parent, false);
        independentLoadoutPanelObject.name = "AssemblyFactory_LoadoutSelectionPanel";
        independentLoadoutPanelObject.transform.SetAsLastSibling();
        loadoutSelectionPanel = independentLoadoutPanelObject.GetComponent<PlayerLoadoutSelectionPanel>();
        independentLoadoutPanelObject.SetActive(false);
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button != null)
        {
            SetText(button.GetComponentInChildren<TMP_Text>(true), value);
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetIcon(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
        target.gameObject.SetActive(sprite != null);
    }

    private static void SetDroneIcon(RawImage target, DroneConfig drone)
    {
        if (target == null)
        {
            return;
        }

        GameObject prefab = drone != null ? drone.DronePrefab : null;
        if (prefab == null)
        {
            target.texture = null;
            target.color = Color.clear;
            target.gameObject.SetActive(false);
            return;
        }

        RenderTexture preview = UnitPreviewRenderer.Instance.GetPreview(prefab);
        target.texture = preview;
        target.color = preview != null ? Color.white : Color.clear;
        target.gameObject.SetActive(preview != null);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }

    private static void SetUpgradeRemainingText(TMP_Text target, bool isUpgrading, float remainingSeconds)
    {
        SetText(target, isUpgrading ? $"완료까지 {remainingSeconds:0}초" : string.Empty);
        SetActive(target != null ? target.gameObject : null, isUpgrading);
    }
}
