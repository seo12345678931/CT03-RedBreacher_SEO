using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class InventoryPanel : MonoBehaviour
{
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button weaponTabButton;
    [SerializeField] private Button unitTabButton;
    [SerializeField] private GameObject weaponWindow;
    [SerializeField] private GameObject unitWindow;
    [SerializeField] private RectTransform weaponContentRoot;
    [SerializeField] private RectTransform unitContentRoot;
    [SerializeField] private Button weaponButtonPrefab;
    [SerializeField] private Button unitButtonPrefab;
    [SerializeField] private TMP_Text weaponListText;
    [SerializeField] private TMP_Text unitListText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private bool showBothWindows = true;

    private InventoryFacility inventory;
    private UnityAction<ProjectileConfig> weaponSelectedCallback;
    private UnityAction<PlayerUnitConfig> unitSelectedCallback;
    private Func<ProjectileConfig, bool> weaponSelectablePredicate;
    private Func<PlayerUnitConfig, bool> unitSelectablePredicate;
    private readonly List<Button> spawnedWeaponButtons = new List<Button>();
    private readonly List<Button> spawnedUnitButtons = new List<Button>();
    private PanelTweenTransition panelTransition;

    private void OnEnable()
    {
        ResolveReferences();
        EnsurePanelTransition();
        closeButton?.onClick.AddListener(ClosePanel);
        weaponTabButton?.onClick.AddListener(ShowWeaponWindow);
        unitTabButton?.onClick.AddListener(ShowUnitWindow);
        SubscribeEvents();
        ApplyWindowVisibility();
        RebuildSelectionButtons();
        RefreshNow();
    }

    private void OnDisable()
    {
        closeButton?.onClick.RemoveListener(ClosePanel);
        weaponTabButton?.onClick.RemoveListener(ShowWeaponWindow);
        unitTabButton?.onClick.RemoveListener(ShowUnitWindow);
        UnsubscribeEvents();
        ClearGeneratedButtons();
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
        Button close,
        TMP_Text weapons,
        TMP_Text units,
        TMP_Text summary)
    {
        UnsubscribeEvents();
        baseCampManager = manager;
        closeButton = close;
        weaponListText = weapons;
        unitListText = units;
        summaryText = summary;
        ResolveReferences();
        SubscribeEvents();
        Refresh();
    }

    public void ShowWeaponWindow()
    {
        showBothWindows = false;
        SetActive(weaponWindow, true);
        SetActive(unitWindow, false);
    }

    public void ShowUnitWindow()
    {
        showBothWindows = false;
        SetActive(weaponWindow, false);
        SetActive(unitWindow, true);
    }

    public void ShowBothWindows()
    {
        showBothWindows = true;
        ApplyWindowVisibility();
    }

    public void OpenWeaponSelectMode(
        UnityAction<ProjectileConfig> onSelected,
        Func<ProjectileConfig, bool> canSelect = null)
    {
        weaponSelectedCallback = onSelected;
        unitSelectedCallback = null;
        weaponSelectablePredicate = canSelect;
        unitSelectablePredicate = null;
        gameObject.SetActive(true);
        ShowWeaponWindow();
        RebuildSelectionButtons();
        Refresh();
    }

    public void OpenUnitSelectMode(
        UnityAction<PlayerUnitConfig> onSelected,
        Func<PlayerUnitConfig, bool> canSelect = null)
    {
        weaponSelectedCallback = null;
        unitSelectedCallback = onSelected;
        weaponSelectablePredicate = null;
        unitSelectablePredicate = canSelect;
        gameObject.SetActive(true);
        ShowUnitWindow();
        RebuildSelectionButtons();
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (inventory == null)
        {
            SetText(summaryText, "Inventory not connected");
            SetText(weaponListText, string.Empty);
            SetText(unitListText, string.Empty);
            return;
        }

        SetText(summaryText, $"Weapons {inventory.WeaponConfigs.Count} / Units {inventory.UnitConfigs.Count}");
        SetText(weaponListText, BuildWeaponList());
        SetText(unitListText, BuildUnitList());
    }

    private string BuildWeaponList()
    {
        if (inventory.WeaponConfigs.Count == 0)
        {
            return "No Weapons";
        }

        StringBuilder text = new StringBuilder();
        for (int i = 0; i < inventory.WeaponConfigs.Count; i++)
        {
            ProjectileConfig weapon = inventory.WeaponConfigs[i];
            if (weapon == null)
            {
                continue;
            }

            string selectable = IsWeaponSelectable(weapon) ? string.Empty : " (Unavailable)";
            text.Append(i + 1);
            text.Append(". ");
            text.Append(BuildWeaponInfoText(weapon, selectable));
            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    private string BuildUnitList()
    {
        if (inventory.UnitConfigs.Count == 0)
        {
            return "No Units";
        }

        string text = string.Empty;
        for (int i = 0; i < inventory.UnitConfigs.Count; i++)
        {
            PlayerUnitConfig unit = inventory.UnitConfigs[i];
            if (unit == null)
            {
                continue;
            }

            string selectable = IsUnitSelectable(unit) ? string.Empty : " (Unavailable)";
            text += $"{i + 1}. {unit.DisplayName} ({unit.Id}){selectable}\n";
        }

        return text.TrimEnd();
    }

    private void ClosePanel()
    {
        ClearSelectionMode();
        EnsurePanelTransition();
        if (panelTransition != null)
        {
            panelTransition.Close();
            return;
        }

        gameObject.SetActive(false);
    }

    private void RebuildSelectionButtons()
    {
        ClearGeneratedButtons();
        ResolveReferences();

        if (inventory == null)
        {
            return;
        }

        RebuildWeaponButtons();
        RebuildUnitButtons();
    }

    private void RebuildWeaponButtons()
    {
        if (weaponContentRoot == null || weaponButtonPrefab == null)
        {
            return;
        }

        foreach (ProjectileConfig weapon in inventory.WeaponConfigs)
        {
            if (weapon == null)
            {
                continue;
            }

            Button button = Instantiate(weaponButtonPrefab, weaponContentRoot);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = BuildWeaponInfoText(weapon, IsWeaponSelectable(weapon) ? string.Empty : " (Unavailable)");
            }

            ProjectileConfig capturedWeapon = weapon;
            button.interactable = weaponSelectedCallback != null && IsWeaponSelectable(capturedWeapon);
            button.onClick.AddListener(() => SelectWeapon(capturedWeapon));
            spawnedWeaponButtons.Add(button);
        }
    }

    private void RebuildUnitButtons()
    {
        if (unitContentRoot == null || unitButtonPrefab == null)
        {
            return;
        }

        foreach (PlayerUnitConfig unit in inventory.UnitConfigs)
        {
            if (unit == null)
            {
                continue;
            }

            Button button = Instantiate(unitButtonPrefab, unitContentRoot);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"{unit.DisplayName} ({unit.Id})";
            }

            PlayerUnitConfig capturedUnit = unit;
            button.interactable = unitSelectedCallback != null && IsUnitSelectable(capturedUnit);
            button.onClick.AddListener(() => SelectUnit(capturedUnit));
            spawnedUnitButtons.Add(button);
        }
    }

    private void SelectWeapon(ProjectileConfig weapon)
    {
        if (weaponSelectedCallback == null)
        {
            return;
        }

        if (!IsWeaponSelectable(weapon))
        {
            return;
        }

        weaponSelectedCallback?.Invoke(weapon);
        ClosePanel();
    }

    private void SelectUnit(PlayerUnitConfig unit)
    {
        if (unitSelectedCallback == null)
        {
            return;
        }

        if (!IsUnitSelectable(unit))
        {
            return;
        }

        unitSelectedCallback?.Invoke(unit);
        ClosePanel();
    }

    private bool IsWeaponSelectable(ProjectileConfig weapon)
    {
        return weapon != null && (weaponSelectablePredicate == null || weaponSelectablePredicate(weapon));
    }

    private bool IsUnitSelectable(PlayerUnitConfig unit)
    {
        return unit != null && (unitSelectablePredicate == null || unitSelectablePredicate(unit));
    }

    private string BuildWeaponInfoText(ProjectileConfig weapon, string suffix)
    {
        int level = inventory != null ? inventory.GetWeaponLevel(weapon) : 0;
        int duplicateCount = inventory != null ? inventory.GetDuplicateProgress(weapon) : 0;
        int requiredDuplicates = inventory != null ? inventory.GetRequiredDuplicates(weapon) : 0;
        string muzzlePrefix = string.IsNullOrWhiteSpace(weapon.MuzzleNamePrefix) ? "None" : weapon.MuzzleNamePrefix;
        string progress = requiredDuplicates > 0
            ? $"{duplicateCount}/{requiredDuplicates}"
            : "MAX";

        return $"{weapon.DisplayName} ({weapon.Id}) Lv.{level} / Dup {progress}{suffix}\n"
            + $"Damage {FormatNumber(weapon.AttackDamage)} / Speed {FormatNumber(weapon.Speed)} / Life {FormatNumber(weapon.Lifetime)}\n"
            + $"Radius {FormatNumber(weapon.CollisionRadius)} / Knockback {FormatNumber(weapon.KnockbackForce)}\n"
            + $"Muzzle {weapon.MultiMuzzleFireMode} x{weapon.MaxBurstMuzzleCount} / Prefix {muzzlePrefix}\n"
            + $"Projectile {GetObjectName(weapon.ProjectileEffectPrefab)} / Hit {GetObjectName(weapon.HitEffectPrefab)} / Flash {GetObjectName(weapon.FireFlashEffectPrefab)}";
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }

    private static string GetObjectName(UnityEngine.Object target)
    {
        return target != null ? target.name : "None";
    }

    private void ClearGeneratedButtons()
    {
        foreach (Button button in spawnedWeaponButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        foreach (Button button in spawnedUnitButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        spawnedWeaponButtons.Clear();
        spawnedUnitButtons.Clear();
    }

    private void ClearSelectionMode()
    {
        weaponSelectedCallback = null;
        unitSelectedCallback = null;
        weaponSelectablePredicate = null;
        unitSelectablePredicate = null;
    }

    private void ApplyWindowVisibility()
    {
        if (showBothWindows)
        {
            SetActive(weaponWindow, true);
            SetActive(unitWindow, true);
            return;
        }

        bool weaponActive = weaponWindow == null || weaponWindow.activeSelf;
        SetActive(weaponWindow, weaponActive);
        SetActive(unitWindow, !weaponActive);
    }

    private void SubscribeEvents()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged.AddListener(HandleInventoryChanged);
        }
    }

    private void UnsubscribeEvents()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged.RemoveListener(HandleInventoryChanged);
        }
    }

    private void HandleInventoryChanged()
    {
        RebuildSelectionButtons();
        Refresh();
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        inventory = baseCampManager != null ? baseCampManager.Inventory : FindFirstObjectByType<InventoryFacility>();
    }

    private void EnsurePanelTransition()
    {
        // 인벤토리/기체 패널은 코드에서 열고 닫으므로 공통 패널 연출 컴포넌트를 보장한다.
        panelTransition ??= GetComponent<PanelTweenTransition>();
        if (panelTransition == null)
        {
            panelTransition = gameObject.AddComponent<PanelTweenTransition>();
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }
}
