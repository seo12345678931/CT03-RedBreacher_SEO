using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerLoadoutSelectionPanel : MonoBehaviour
{
    private const string SelectedWeaponKey = "PlayerLoadout.SelectedWeapon";
    private const string SelectedDroneKey = "PlayerLoadout.SelectedDrone";

    private enum LoadoutMode
    {
        Weapon,
        Drone,
        Skill
    }

    [Header("Source")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerDroneController droneController;
    [SerializeField] private ProjectileConfig[] weaponOptions;
    [SerializeField] private DroneConfig[] droneOptions;
    [SerializeField] private PlayerSkillConfig[] skillOptions;

    [Header("Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button closeButton;

    [Header("Panel")]
    [SerializeField] private GameObject selectionRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private PlayerLoadoutOptionButton optionButtonPrefab;
    [SerializeField] private AudioClip optionButtonAudioClip;

    [Header("Detail")] 
    [SerializeField] private Image detailIconWeapon;
    [SerializeField] private RawImage detailIconDroneOfSkill;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailCategoryText;
    [SerializeField] private TMP_Text detailStatsText;

    private readonly List<PlayerLoadoutOptionButton> spawnedOptions = new List<PlayerLoadoutOptionButton>();
    private bool saveToPlayerPrefs = true;
    private LoadoutMode currentMode;
    private ProjectileConfig selectedWeapon;
    private DroneConfig selectedDrone;
    private PlayerSkillConfig selectedSkill;
    private InventoryFacility inventory;
    private AssemblyFactory assemblyFactory;
    private Action<ProjectileConfig> weaponSelectionCallback;
    private Action<DroneConfig> droneSelectionCallback;
    private Action<PlayerSkillConfig> skillSelectionCallback;
    private Action selectionClosedCallback;
    private PanelTweenTransition panelTransition;
    private AudioSource audioSource;


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

        SetPrefabPreview(target, drone != null ? drone.DronePrefab : null, null, null);
    }

    private void SetSkillPreview(RawImage target, PlayerSkillConfig skill)
    {
        if (target == null)
        {
            return;
        }

        int skillLevel = skill != null ? GetCollectionSkillLevel(skill) : 1;
        GameObject prefab = GetSkillPreviewPrefab(skill);
        string cacheKey = skill != null ? $"{skill.GetInstanceID()}_Level{skillLevel}" : null;
        SetPrefabPreview(
            target,
            prefab,
            cacheKey,
            instance => ApplySkillPreviewSkin(instance, skill, skillLevel));
    }

    private static void SetPrefabPreview(
        RawImage target,
        GameObject prefab,
        string cacheKey,
        Action<GameObject> configureInstance)
    {
        if (target == null)
        {
            return;
        }

        if (prefab == null)
        {
            target.texture = null;
            target.color = Color.clear;
            target.gameObject.SetActive(false);
            return;
        }

        RenderTexture preview = string.IsNullOrWhiteSpace(cacheKey)
            ? UnitPreviewRenderer.Instance.GetPreview(prefab)
            : UnitPreviewRenderer.Instance.GetPreview(prefab, cacheKey, configureInstance);
        target.texture = preview;
        target.color = preview != null ? Color.white : Color.clear;
        target.gameObject.SetActive(preview != null);
    }

    private static void ApplySkillPreviewSkin(GameObject instance, PlayerSkillConfig skill, int skillLevel)
    {
        if (instance == null || skill == null)
        {
            return;
        }

        SkinEditor_SkillLevel[] skinEditors = instance.GetComponentsInChildren<SkinEditor_SkillLevel>(true);
        for (int i = 0; i < skinEditors.Length; i++)
        {
            skinEditors[i]?.SetSkill(skill, skillLevel);
        }
    }

    private static GameObject GetSkillPreviewPrefab(PlayerSkillConfig skill)
    {
        if (skill == null)
        {
            return null;
        }

        return skill.SkillType switch
        {
            PlayerSkillType.Bombardment => skill.AirplanePrefab,
            PlayerSkillType.AutoTurret => skill.TurretPrefab,
            PlayerSkillType.MissileTurret => skill.MissileTurretPrefab,
            PlayerSkillType.StealthBomber => skill.StealthBomberPrefab,
            PlayerSkillType.AttackHelicopter => skill.AttackHelicopterPrefab,
            _ => null
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (weaponOptions == null || weaponOptions.Length == 0)
        {
            weaponOptions = LoadAssetsInEditor<ProjectileConfig>("Assets/SO/Balance/Weapons");
        }

        if (droneOptions == null || droneOptions.Length <= 1)
        {
            droneOptions = LoadAssetsInEditor<DroneConfig>("Assets/SO/Balance/Drones");
        }

        if (skillOptions == null || skillOptions.Length == 0)
        {
            skillOptions = LoadAssetsInEditor<PlayerSkillConfig>("Assets/SO/Balance/Skills");
        }
    }
#endif

    private void Awake()
    {
        // selectionRoot가 이 컴포넌트 자신의 GameObject인 구성에서는,
        // Awake가 첫 OpenPanel(SetActive(true)) 직후에 지연 실행되며 패널을 즉시 닫아버려
        // "처음엔 안 열리고 두 번째 클릭에야 열리는" 문제가 생긴다.
        // 패널은 씬/프리팹에서 비활성으로 시작하므로 여기서 따로 끌 필요가 없다.
        ResolveSources();
        ResolveDetailIconReferences();
        EnsureAudioSource();
    }

    private void Start()
    {
        ResolveSources();
        EnsureAudioSource();
        ApplySavedLoadout();
    }

    /// <summary>저장된 장착 무기/드론을 즉시 플레이어에 적용한다. 로드아웃 팝업이 비활성이라
    /// Start가 늦게 실행되는 문제를 피하려고, 부팅 시 외부(BaseCampManager)에서 호출한다.
    /// 비활성 상태에서도 동작한다.</summary>
    public void ApplySavedLoadout()
    {
        ResolveSources();
        if (saveToPlayerPrefs)
        {
            LoadEquippedLoadout();
            return;
        }

        if (selectedWeapon != null)
        {
            player?.SetWeaponConfig(selectedWeapon);
        }

        if (selectedDrone != null)
        {
            droneController?.SetDroneConfig(selectedDrone);
        }
    }

    public JinyouPlayerLoadoutSaveData CaptureState()
    {
        return new JinyouPlayerLoadoutSaveData
        {
            weaponId = selectedWeapon != null
                ? selectedWeapon.Id
                : PlayerPrefs.GetString(SelectedWeaponKey, string.Empty),
            droneId = selectedDrone != null
                ? selectedDrone.Id
                : PlayerPrefs.GetString(SelectedDroneKey, string.Empty)
        };
    }

    public void RestoreState(JinyouPlayerLoadoutSaveData data)
    {
        if (data == null)
        {
            return;
        }

        ResolveSources();
        ApplyLoadoutIds(data.weaponId, data.droneId);
    }

    public void SetStandaloneSaveEnabled(bool enabled, bool clearStoredData)
    {
        saveToPlayerPrefs = enabled;
        if (!clearStoredData)
        {
            return;
        }

        PlayerPrefs.DeleteKey(SelectedWeaponKey);
        PlayerPrefs.DeleteKey(SelectedDroneKey);
        PlayerPrefs.Save();
    }

    private void ApplyLoadoutIds(string weaponId, string droneId)
    {
        ProjectileConfig weapon = FindWeaponById(weaponId);
        if (weapon != null && (inventory == null || inventory.ContainsWeapon(weapon)))
        {
            selectedWeapon = weapon;
            player?.SetWeaponConfig(weapon);
        }

        DroneConfig drone = FindDroneById(droneId);
        if (drone != null && (inventory == null || inventory.ContainsDrone(drone)))
        {
            selectedDrone = drone;
            droneController?.SetDroneConfig(drone);
        }
    }

    private void LoadEquippedLoadout()
    {
        DroneConfig initialDrone = RegisterInitialDrone();

        string savedWeaponId = PlayerPrefs.GetString(SelectedWeaponKey, string.Empty);
        if (!string.IsNullOrEmpty(savedWeaponId))
        {
            ProjectileConfig weapon = FindWeaponById(savedWeaponId);
            if (weapon != null && (inventory == null || inventory.ContainsWeapon(weapon)))
            {
                selectedWeapon = weapon;
                player?.SetWeaponConfig(weapon);
            }
        }

        string savedDroneId = PlayerPrefs.GetString(SelectedDroneKey, string.Empty);
        if (!string.IsNullOrEmpty(savedDroneId))
        {
            DroneConfig drone = FindDroneById(savedDroneId);
            if (drone != null && (inventory == null || inventory.ContainsDrone(drone)))
            {
                selectedDrone = drone;
                droneController?.SetDroneConfig(drone);
                return;
            }
        }

        selectedDrone = initialDrone;
        droneController?.SetDroneConfig(initialDrone);
        if (initialDrone != null)
        {
            SaveSelectedId(SelectedDroneKey, initialDrone.Id);
        }
    }

    private ProjectileConfig FindWeaponById(string id)
    {
        if (weaponOptions == null)
        {
            return null;
        }

        for (int i = 0; i < weaponOptions.Length; i++)
        {
            if (weaponOptions[i] != null && weaponOptions[i].Id == id)
            {
                return weaponOptions[i];
            }
        }

        return null;
    }

    private DroneConfig FindDroneById(string id)
    {
        if (droneOptions == null)
        {
            return null;
        }

        for (int i = 0; i < droneOptions.Length; i++)
        {
            if (droneOptions[i] != null && droneOptions[i].Id == id)
            {
                return droneOptions[i];
            }
        }

        return null;
    }

    private void OnEnable()
    {
        EnsureAudioSource();
        equipButton?.onClick.AddListener(ConfirmSelected);
        closeButton?.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        // NotifySelectionClosed: 첫 번째로 교체하기 버튼을 눌러 인벤토리에 진입하다가 다시 나올 때 기존 closeButton 복구가 누락되고,
        // 이후 정상적으로 작동하는 "처음엔 안 열리고 두 번째 클릭에야 열리는" 문제를 해결하기 위해 호출.
        NotifySelectionClosed();
        equipButton?.onClick.RemoveListener(ConfirmSelected);
        closeButton?.onClick.RemoveListener(Close);
    }

    public void OpenWeapons(Action onClosed = null)
    {
        ResolveSources();
        weaponSelectionCallback = null;
        droneSelectionCallback = null;
        skillSelectionCallback = null;
        selectionClosedCallback = onClosed;
        currentMode = LoadoutMode.Weapon;
        selectedWeapon = player != null ? player.WeaponConfig : null;
        OpenPanel("무기 로드아웃");
        RebuildWeaponList();
        RefreshWeaponDetail(selectedWeapon);
    }

    public void OpenDrones(Action onClosed = null)
    {
        ResolveSources();
        weaponSelectionCallback = null;
        droneSelectionCallback = null;
        skillSelectionCallback = null;
        selectionClosedCallback = onClosed;
        currentMode = LoadoutMode.Drone;
        selectedDrone = droneController != null ? droneController.DroneConfig : null;
        OpenPanel("드론 로드아웃");
        RebuildDroneList();
        RefreshDroneDetail(selectedDrone);
    }

    public void OpenWeaponsForSelection(Action<ProjectileConfig> onSelected)
    {
        ResolveSources();
        weaponSelectionCallback = onSelected;
        droneSelectionCallback = null;
        skillSelectionCallback = null;
        selectionClosedCallback = null;
        currentMode = LoadoutMode.Weapon;
        selectedWeapon = assemblyFactory != null ? assemblyFactory.SelectedWeaponConfig : null;
        OpenPanel("강화하고자 하는 무기를 선택하세요.");
        RebuildWeaponList();
        RefreshWeaponDetail(selectedWeapon);
    }

    public void OpenDronesForSelection(Action<DroneConfig> onSelected)
    {
        ResolveSources();
        droneSelectionCallback = onSelected;
        weaponSelectionCallback = null;
        skillSelectionCallback = null;
        selectionClosedCallback = null;
        currentMode = LoadoutMode.Drone;
        selectedDrone = assemblyFactory != null ? assemblyFactory.SelectedDroneConfig : null;
        OpenPanel("강화하고자 하는 드론을 선택하세요.");
        RebuildDroneList();
        RefreshDroneDetail(selectedDrone);
    }

    public void OpenSkillsForSelection(
        Action<PlayerSkillConfig> onSelected,
        PlayerSkillConfig currentSkill = null,
        Action onClosed = null)
    {
        ResolveSources();
        skillSelectionCallback = onSelected;
        weaponSelectionCallback = null;
        droneSelectionCallback = null;
        selectionClosedCallback = onClosed;
        currentMode = LoadoutMode.Skill;
        selectedSkill = currentSkill;
        OpenPanel("교체할 스킬을 선택하세요.");
        RebuildSkillList();
        RefreshSkillDetail(selectedSkill);
    }

    public void Close()
    {
        NotifySelectionClosed();

        GameObject root = ResolveSelectionRoot();
        if (root != null)
        {
            PanelTweenTransition transition = EnsurePanelTransition(root);
            if (transition != null)
            {
                transition.Close();
                return;
            }

            root.SetActive(false);
        }
    }

    private void NotifySelectionClosed()
    {
        Action callback = selectionClosedCallback;
        selectionClosedCallback = null;
        callback?.Invoke();
    }

    private void OpenPanel(string title)
    {
        GameObject root = ResolveSelectionRoot();
        if (root != null)
        {
            EnsurePanelTransition(root);
            root.SetActive(true);
        }

        SetText(titleText, title);
    }

    private void RebuildWeaponList()
    {
        ClearOptions();
        if (weaponOptions == null)
        {
            return;
        }

        foreach (ProjectileConfig weapon in weaponOptions)
        {
            if (weapon == null || (inventory != null && !inventory.ContainsWeapon(weapon)))
            {
                continue;
            }

            PlayerLoadoutOptionButton option = CreateOption();
            if (option == null)
            {
                continue;
            }

            option.Bind(
                $"{weapon.DisplayName} Lv.{GetFactoryWeaponLevel(weapon)}",
                RarityVisuals.GetLabel(weapon.Rarity),
                $"Lv.{GetFactoryWeaponLevel(weapon)} / 피해량 {GetEnhancedWeaponDamage(weapon):0.##}",
                weapon == selectedWeapon,
                () =>
                {
                    PlayOptionButtonAudio();
                    SelectWeapon(weapon);
                },
                weapon.Icon,
                null,
                RarityVisuals.GetColor(weapon.Rarity));
        }
    }

    private void RebuildDroneList()
    {
        ClearOptions();
        if (droneOptions == null)
        {
            return;
        }

        foreach (DroneConfig drone in droneOptions)
        {
            if (drone == null || (inventory != null && !inventory.ContainsDrone(drone)))
            {
                continue;
            }

            PlayerLoadoutOptionButton option = CreateOption();
            if (option == null)
            {
                continue;
            }

            option.Bind(
                $"{drone.DisplayName} Lv.{GetFactoryDroneLevel(drone)}",
                GetDroneWeaponName(drone),
                $"Lv.{GetFactoryDroneLevel(drone)} / 피해량 {GetEnhancedDroneDamage(drone):0.##}",
                drone == selectedDrone,
                () =>
                {
                    PlayOptionButtonAudio();
                    SelectDrone(drone);
                },
                null,
                drone);
        }
    }

    private void RebuildSkillList()
    {
        ClearOptions();

        IReadOnlyList<PlayerSkillConfig> source = inventory != null ? inventory.SkillConfigs : skillOptions;
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            PlayerSkillConfig skill = source[i];
            if (skill == null)
            {
                continue;
            }

            PlayerLoadoutOptionButton option = CreateOption();
            if (option == null)
            {
                continue;
            }

            option.Bind(
                $"{skill.DisplayName} Lv.{GetCollectionSkillLevel(skill)}",
                RarityVisuals.GetLabel(skill.Rarity),
                BuildSkillSummary(skill),
                skill == selectedSkill,
                () =>
                {
                    PlayOptionButtonAudio();
                    SelectSkill(skill);
                },
                null,
                null,
                RarityVisuals.GetColor(skill.Rarity),
                skill,
                GetCollectionSkillLevel(skill));
        }
    }

    private PlayerLoadoutOptionButton CreateOption()
    {
        if (optionButtonPrefab == null || contentRoot == null)
        {
            return null;
        }

        PlayerLoadoutOptionButton option = Instantiate(optionButtonPrefab, contentRoot);
        option.gameObject.SetActive(true);
        spawnedOptions.Add(option);
        return option;
    }

    private void SelectWeapon(ProjectileConfig weapon)
    {
        selectedWeapon = weapon;
        RefreshWeaponDetail(weapon);
        RebuildWeaponList();
    }

    private void SelectDrone(DroneConfig drone)
    {
        selectedDrone = drone;
        RefreshDroneDetail(drone);
        RebuildDroneList();
    }

    private void SelectSkill(PlayerSkillConfig skill)
    {
        selectedSkill = skill;
        RefreshSkillDetail(skill);
        RebuildSkillList();
    }

    private void ConfirmSelected()
    {
        ResolveSources();
        if (currentMode == LoadoutMode.Weapon)
        {
            if (weaponSelectionCallback != null)
            {
                Action<ProjectileConfig> callback = weaponSelectionCallback;
                weaponSelectionCallback = null;
                callback.Invoke(selectedWeapon);
                Close();
                return;
            }

            player?.SetWeaponConfig(selectedWeapon);
            if (selectedWeapon != null)
            {
                SaveSelectedId(SelectedWeaponKey, selectedWeapon.Id);
            }
            RebuildWeaponList();
            RefreshWeaponDetail(selectedWeapon);
            return;
        }

        if (currentMode == LoadoutMode.Skill)
        {
            if (skillSelectionCallback != null)
            {
                Action<PlayerSkillConfig> callback = skillSelectionCallback;
                skillSelectionCallback = null;
                callback.Invoke(selectedSkill);
                Close();
            }

            return;
        }

        if (droneSelectionCallback != null)
        {
            Action<DroneConfig> callback = droneSelectionCallback;
            droneSelectionCallback = null;
            callback.Invoke(selectedDrone);
            Close();
            return;
        }

        if (selectedDrone != null && (inventory == null || inventory.ContainsDrone(selectedDrone)))
        {
            droneController?.SetDroneConfig(selectedDrone);
            SaveSelectedId(SelectedDroneKey, selectedDrone.Id);
        }
        RebuildDroneList();
        RefreshDroneDetail(selectedDrone);
    }

    private void SaveSelectedId(string key, string value)
    {
        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        BaseCampManager.Instance?.RequestUnifiedSave();
    }

    private void RefreshWeaponDetail(ProjectileConfig weapon)
    {
        ResolveDetailIconReferences();

        int weaponEnhanceLevel = weapon != null ? GetFactoryWeaponLevel(weapon) : 0;
        float weaponBonusDamage = weapon != null ? GetWeaponBonusDamage(weapon) : 0f;
        SetIcon(detailIconWeapon, weapon != null ? weapon.Icon : null);
        SetDroneIcon(detailIconDroneOfSkill, null);
        SetText(detailNameText, weapon != null ? weapon.DisplayName : "무기를 선택하세요.");
        SetText(detailCategoryText, weapon != null ? $"{weapon.WeaponCategory}" : string.Empty);
        SetText(detailStatsText, weapon != null
            ? $"공장강화 Lv. <color=#4AD787>{weaponEnhanceLevel}</color>\n"
                + $"수집강화 Lv. <color=#4AD787>{GetCollectionWeaponLevel(weapon)}</color>\n"
                + "\n"
                + $"피해량: <color=#EC9A0E>{weapon.AttackDamage:0.##}</color> (+ <color=#4AD787>{weaponBonusDamage:0.##}</color>)\n"
                + $"범위피해: <color=#EC9A0E>{weapon.AreaRadius:0.##}</color>\n"
                + $"탄속: <color=#EC9A0E>{weapon.Speed * weapon.Lifetime:0.##}</color>\n"
                + $"저지력: <color=#EC9A0E>{weapon.KnockbackForce:0.##}</color>\n"
            : string.Empty);
    }

    private void RefreshDroneDetail(DroneConfig drone)
    {
        ResolveDetailIconReferences();

        int droneEnhanceLevel = drone != null ? GetFactoryDroneLevel(drone) : 0;
        float droneDamageBonus = droneEnhanceLevel > 0 ? GetFactoryDroneDamageBonus(drone) : 0f;
        SetIcon(detailIconWeapon, null);
        SetDroneIcon(detailIconDroneOfSkill, drone);
        SetText(detailNameText, drone != null ? drone.DisplayName : "드론을 선택하세요.");
        SetText(detailCategoryText, drone != null ? $"장착한 무기: {drone.ProjectileConfig.DisplayName}" : string.Empty);
        SetText(detailStatsText, drone != null
            ? $"공장강화 Lv. <color=#4AD787>{droneEnhanceLevel}</color>\n"
                + "\n"
                + $"드론 개수: <color=#EC9A0E>{drone.DroneCount:0.##}마리</color>\n"
                + $"피해량: <color=#EC9A0E>{drone.AttackDamage:0.##}</color> (+ <color=#4AD787>{droneDamageBonus:0.##}</color>)\n"
                + $"사거리: <color=#EC9A0E>{drone.AttackRange:0.##}</color>\n"
                + $"발사간격: <color=#EC9A0E>{drone.AttackInterval:0.##}</color>"
            : string.Empty);
    }

    private void RefreshSkillDetail(PlayerSkillConfig skill)
    {
        ResolveDetailIconReferences();

        SetIcon(detailIconWeapon, null);
        SetSkillPreview(detailIconDroneOfSkill, skill);
        SetText(detailNameText, skill != null ? skill.DisplayName : "스킬을 선택하세요.");
        SetText(detailCategoryText, skill != null ? GetSkillTypeLabel(skill.SkillType) : string.Empty);
        SetText(detailStatsText, skill != null
            ? $"수집강화 Lv. <color=#4AD787>{GetCollectionSkillLevel(skill)}</color>\n"
                + "\n"
                + $"쿨타임: <color=#EC9A0E>{skill.GetCooldown(GetCollectionSkillLevel(skill)):0.##}</color>\n"
                + $"범위: <color=#EC9A0E>{skill.EffectRadius:0.##}</color>\n"
                + BuildSkillSummary(skill)
            : string.Empty);
    }

    private void ClearOptions()
    {
        for (int i = spawnedOptions.Count - 1; i >= 0; i--)
        {
            if (spawnedOptions[i] != null)
            {
                Destroy(spawnedOptions[i].gameObject);
            }
        }

        spawnedOptions.Clear();
    }

    private void ResolveSources()
    {
        player ??= FindFirstObjectByType<PlayerController>();
        droneController ??= player != null
            ? player.GetComponent<PlayerDroneController>()
            : FindFirstObjectByType<PlayerDroneController>();
        inventory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.Inventory
            : InventoryFacility.FindAny();
        assemblyFactory ??= BaseCampManager.Instance != null
            ? BaseCampManager.Instance.AssemblyFactory
            : FindFirstObjectByType<AssemblyFactory>(FindObjectsInactive.Include);
        ResolveSelectionRoot();
    }

    private GameObject ResolveSelectionRoot()
    {
        // Inspector 미지정 시 이 컴포넌트가 붙은 오브젝트를 패널 루트로 사용한다.
        if (selectionRoot == null)
        {
            selectionRoot = gameObject;
        }

        return selectionRoot;
    }

    private PanelTweenTransition EnsurePanelTransition(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        // 로드아웃 선택 패널도 공통 열림/닫힘 연출을 적용한다.
        if (panelTransition == null || panelTransition.gameObject != root)
        {
            panelTransition = root.GetComponent<PanelTweenTransition>();
            if (panelTransition == null)
            {
                panelTransition = root.AddComponent<PanelTweenTransition>();
            }
        }

        return panelTransition;
    }

    private void ResolveDetailIconReferences()
    {
        if (detailIconWeapon == null)
        {
            Transform detailRoot = FindChildTransformByName(transform, "Detail_WeaponIcon");
            // 상세 프레임의 배경이 아니라 내부 아이콘 슬롯에 무기 스프라이트를 반영한다.
            detailIconWeapon = FindChildComponentByName<Image>(detailRoot, "Icon");
        }

        if (detailIconDroneOfSkill == null)
        {
            detailIconDroneOfSkill = FindChildComponentByName<RawImage>(transform, "detailIconDroneOfSkill");
            detailIconDroneOfSkill ??= FindChildComponentByName<RawImage>(transform, "DetailIconDroneOfSkill");
            detailIconDroneOfSkill ??= FindChildComponentByName<RawImage>(transform, "Detail_DroneIcon");
            detailIconDroneOfSkill ??= FindChildComponentByName<RawImage>(transform, "Detail_SkillIcon");
        }
    }

    private static Transform FindChildTransformByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component.name == childName)
            {
                return component;
            }
        }

        return null;
    }

    private int GetCollectionWeaponLevel(ProjectileConfig weapon)
    {
        return inventory != null ? Mathf.Max(1, inventory.GetWeaponLevel(weapon)) : 1;
    }

    private int GetCollectionSkillLevel(PlayerSkillConfig skill)
    {
        return inventory != null ? Mathf.Max(1, inventory.GetSkillLevel(skill)) : 1;
    }

    private string BuildSkillSummary(PlayerSkillConfig skill)
    {
        if (skill == null || inventory == null)
        {
            return string.Empty;
        }

        int required = inventory.GetRequiredDuplicates(skill);
        return required > 0
            ? $"중복 <color=#EC9A0E>{inventory.GetDuplicateProgress(skill)}</color> / <color=#EC9A0E>{required}</color>"
            : "<color=#4AD787>MAX</color>";
    }

    private DroneConfig RegisterInitialDrone()
    {
        if (droneOptions == null || droneOptions.Length == 0)
        {
            return null;
        }

        DroneConfig initialDrone = FindDroneById("drone_default") ?? droneOptions[0];
        // 기본 드론은 최초 지급이므로 수집 업적에는 포함하지 않는다.
        inventory?.RegisterInitialDrone(initialDrone);
        return initialDrone;
    }

    private int GetFactoryWeaponLevel(ProjectileConfig weapon)
    {
        return assemblyFactory != null ? assemblyFactory.GetWeaponEnhanceLevel(weapon) : 0;
    }

    private int GetFactoryDroneLevel(DroneConfig drone)
    {
        return assemblyFactory != null ? assemblyFactory.GetDroneEnhanceLevel(drone) : 0;
    }

    private float GetEnhancedWeaponDamage(ProjectileConfig weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        return weapon.AttackDamage * GetCollectionWeaponDamageMultiplier(weapon)
            + GetFactoryWeaponDamageBonus(weapon);
    }

    private float GetWeaponBonusDamage(ProjectileConfig weapon)
    {
        return weapon != null
            ? Mathf.Max(0f, GetEnhancedWeaponDamage(weapon) - weapon.AttackDamage)
            : 0f;
    }

    private float GetCollectionWeaponDamageMultiplier(ProjectileConfig weapon)
    {
        if (weapon == null)
        {
            return 1f;
        }

        return DuplicateLevelProgression.GetLevelMultiplier(
            GetCollectionWeaponLevel(weapon),
            weapon.DamagePercentPerLevel);
    }

    private float GetFactoryWeaponDamageBonus(ProjectileConfig weapon)
    {
        return weapon != null && assemblyFactory != null
            ? assemblyFactory.GetWeaponStatBonus(
                weapon,
                AssemblyFactory.WeaponEnhancementStat.AttackDamage)
            : 0f;
    }

    private float GetEnhancedDroneDamage(DroneConfig drone)
    {
        return drone != null
            ? drone.AttackDamage + GetFactoryDroneDamageBonus(drone)
            : 0f;
    }

    private float GetFactoryDroneDamageBonus(DroneConfig drone)
    {
        return drone != null && assemblyFactory != null
            ? assemblyFactory.GetDroneAttackDamageBonus(drone)
            : 0f;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string GetSkillTypeLabel(PlayerSkillType skillType)
    {
        return skillType switch
        {
            PlayerSkillType.Bombardment => "단일 폭격기",
            PlayerSkillType.AutoTurret => "센트리(개틀링)",
            PlayerSkillType.MissileTurret => "센트리(미사일)",
            PlayerSkillType.StealthBomber => "전략 폭격기",
            PlayerSkillType.AttackHelicopter => "화력지원",
            _ => "스킬"
        };
    }

    private static string GetDroneWeaponName(DroneConfig drone)
    {
        return drone != null && drone.ProjectileConfig != null
            ? drone.ProjectileConfig.DisplayName
            : "장착한 무기 없음";
    }

    private void EnsureAudioSource()
    {
        audioSource ??= GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void PlayOptionButtonAudio()
    {
        if (optionButtonAudioClip == null)
        {
            return;
        }

        EnsureAudioSource();
        audioSource.PlayOneShot(optionButtonAudioClip);
    }

#if UNITY_EDITOR
    private static T[] LoadAssetsInEditor<T>(string folder) where T : UnityEngine.Object
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        T[] assets = new T[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            assets[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return assets;
    }
#endif
}
