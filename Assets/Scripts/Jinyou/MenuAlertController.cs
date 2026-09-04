using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MenuAlertController : MonoBehaviour
{
    private enum AlertCategory
    {
        Aircraft,
        Equipment,
        Base,
        Achievement,
        Shop
    }

    private const string CollectionSeenKey = "MenuAlert.CollectionSeenSignature";
    private const string EquipmentSeenKey = "MenuAlert.EquipmentSeenSignature";
    private const string BaseSeenKey = "MenuAlert.BaseSeenSignature";

    // "확인함" 기준선을 계정(UID)별로 분리한다. 전역 키를 쓰면 계정 전환 시 배지 상태가 샌다.
    private static string Scoped(string baseKey)
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        return auth != null ? auth.ScopedSaveKey(baseKey) : baseKey;
    }
    private const float RescanInterval = 1f;

    private class AlertBinding
    {
        public AlertCategory category;
        public Button button;
        public GameObject alertIcon;
    }

    private readonly List<AlertBinding> bindings = new List<AlertBinding>();
    private readonly HashSet<Button> wiredButtons = new HashSet<Button>();

    private Transform cachedMenuPanel;
    private BaseCampManager baseCampManager;
    private InventoryFacility inventory;
    private AchievementManager achievementManager;
    private WeaponGachaFacility gachaFacility;
    private InventoryFacility subscribedInventory;
    private AchievementManager subscribedAchievementManager;
    private BaseCampManager subscribedBaseCampManager;
    private float nextRescanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<MenuAlertController>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject host = new GameObject(nameof(MenuAlertController));
        // Bootstrap은 앱 시작(Title 씬)에서 1회만 실행된다. DontDestroyOnLoad가 없으면
        // Title→Myth 전환에서 파괴된 뒤 재생성되지 않아, 빌드에서 알림 배지 시스템이 통째로 죽는다.
        // (TutorialManager가 같은 문제를 겪고 동일하게 고친 전례가 있다.)
        DontDestroyOnLoad(host);
        host.AddComponent<MenuAlertController>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveReferences();
        SubscribeEvents();
        RefreshBindings();
        RefreshAlerts();
    }

    private void Start()
    {
        ResolveReferences();
        // 알림 기준선은 통합 세이브(클라우드 동기화 포함) 복원이 끝난 뒤 잡는다.
        // Start 시점엔 아직 복원 전이라 여기서 잡으면 실기기에서 알림이 영구히 켜진다.
        RefreshBindings();
        RefreshAlerts();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRescanTime)
        {
            return;
        }

        nextRescanTime = Time.unscaledTime + RescanInterval;
        ResolveReferences();
        SubscribeEvents();
        RefreshBindings();
        RefreshAlerts();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribeEvents();
        bindings.Clear();
        wiredButtons.Clear();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();
        SubscribeEvents();
        RefreshBindings();
        RefreshAlerts();
    }

    private void ResolveReferences()
    {
        // DontDestroyOnLoad로 씬을 넘어 살아남으므로, 파괴된 참조(fake-null)는 ??=에 걸리지 않는다.
        // Unity의 == null 비교로 명시적으로 다시 잡는다.
        if (baseCampManager == null)
        {
            baseCampManager = BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>(FindObjectsInactive.Include);
        }

        if (inventory == null)
        {
            inventory = baseCampManager != null ? baseCampManager.Inventory : InventoryFacility.FindAny();
        }

        if (achievementManager == null)
        {
            achievementManager = AchievementManager.Instance ?? FindFirstObjectByType<AchievementManager>(FindObjectsInactive.Include);
        }

        if (gachaFacility == null)
        {
            gachaFacility = FindFirstObjectByType<WeaponGachaFacility>(FindObjectsInactive.Include);
        }
    }

    private void SubscribeEvents()
    {
        if (inventory != null && subscribedInventory != inventory)
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.OnCollectionProgressChanged.RemoveListener(RefreshAlerts);
                subscribedInventory.OnEquipmentPartsChanged.RemoveListener(RefreshAlerts);
                subscribedInventory.OnInventoryChanged.RemoveListener(RefreshAlerts);
            }

            inventory.OnCollectionProgressChanged.AddListener(RefreshAlerts);
            inventory.OnEquipmentPartsChanged.AddListener(RefreshAlerts);
            inventory.OnInventoryChanged.AddListener(RefreshAlerts);
            subscribedInventory = inventory;
        }

        if (achievementManager != null && subscribedAchievementManager != achievementManager)
        {
            if (subscribedAchievementManager != null)
            {
                subscribedAchievementManager.OnAchievementsChanged.RemoveListener(RefreshAlerts);
                subscribedAchievementManager.OnAchievementCompleted.RemoveListener(HandleAchievementCompleted);
            }

            achievementManager.OnAchievementsChanged.AddListener(RefreshAlerts);
            achievementManager.OnAchievementCompleted.AddListener(HandleAchievementCompleted);
            subscribedAchievementManager = achievementManager;
        }

        if (baseCampManager != null && subscribedBaseCampManager != baseCampManager)
        {
            if (subscribedBaseCampManager != null)
            {
                subscribedBaseCampManager.OnCoreCrystalsChanged.RemoveListener(HandleCurrencyChanged);
                subscribedBaseCampManager.UnifiedSaveLoaded -= HandleUnifiedSaveLoaded;
                UnsubscribeFacilityEvents(subscribedBaseCampManager);
            }

            baseCampManager.OnCoreCrystalsChanged.AddListener(HandleCurrencyChanged);
            baseCampManager.UnifiedSaveLoaded += HandleUnifiedSaveLoaded;
            SubscribeFacilityEvents(baseCampManager);
            subscribedBaseCampManager = baseCampManager;

            // 구독 전에 이미 로드가 끝난 경우(이벤트를 놓친 경우)를 대비해 즉시 기준선을 잡는다.
            if (baseCampManager.IsUnifiedSaveLoaded)
            {
                HandleUnifiedSaveLoaded();
            }
        }
    }

    private void HandleUnifiedSaveLoaded()
    {
        ResolveReferences();
        InitializeSeenSignatures();
        RefreshAlerts();
    }

    private void UnsubscribeEvents()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnCollectionProgressChanged.RemoveListener(RefreshAlerts);
            subscribedInventory.OnEquipmentPartsChanged.RemoveListener(RefreshAlerts);
            subscribedInventory.OnInventoryChanged.RemoveListener(RefreshAlerts);
            subscribedInventory = null;
        }

        if (subscribedAchievementManager != null)
        {
            subscribedAchievementManager.OnAchievementsChanged.RemoveListener(RefreshAlerts);
            subscribedAchievementManager.OnAchievementCompleted.RemoveListener(HandleAchievementCompleted);
            subscribedAchievementManager = null;
        }

        if (subscribedBaseCampManager != null)
        {
            subscribedBaseCampManager.OnCoreCrystalsChanged.RemoveListener(HandleCurrencyChanged);
            subscribedBaseCampManager.UnifiedSaveLoaded -= HandleUnifiedSaveLoaded;
            UnsubscribeFacilityEvents(subscribedBaseCampManager);
            subscribedBaseCampManager = null;
        }
    }

    private void SubscribeFacilityEvents(BaseCampManager manager)
    {
        manager.CommandCenter?.OnUpgradeCompleted.AddListener(RefreshAlerts);
        manager.CreditRefinery?.OnUpgradeCompleted.AddListener(RefreshAlerts);
        manager.AssemblyFactory?.OnUpgradeCompleted.AddListener(RefreshAlerts);
        manager.CoreCharger?.OnUpgradeCompleted.AddListener(RefreshAlerts);
    }

    private void UnsubscribeFacilityEvents(BaseCampManager manager)
    {
        manager.CommandCenter?.OnUpgradeCompleted.RemoveListener(RefreshAlerts);
        manager.CreditRefinery?.OnUpgradeCompleted.RemoveListener(RefreshAlerts);
        manager.AssemblyFactory?.OnUpgradeCompleted.RemoveListener(RefreshAlerts);
        manager.CoreCharger?.OnUpgradeCompleted.RemoveListener(RefreshAlerts);
    }

    private void InitializeSeenSignatures()
    {
        InitializeSignature(Scoped(CollectionSeenKey), BuildCollectionSignature());
        InitializeSignature(Scoped(EquipmentSeenKey), BuildEquipmentSignature());
        InitializeSignature(Scoped(BaseSeenKey), BuildBaseSignature());
    }

    private static void InitializeSignature(string key, string signature)
    {
        if (PlayerPrefs.HasKey(key))
        {
            return;
        }

        PlayerPrefs.SetString(key, signature);
        PlayerPrefs.Save();
    }

    private void RefreshBindings()
    {
        // FindMenuButtonsPanel은 씬의 모든 Transform을 순회하므로(1초 주기 Update에서 호출됨)
        // 한 번 찾은 패널은 캐시하고, 파괴(씬 전환)됐을 때만 다시 찾는다.
        if (cachedMenuPanel == null)
        {
            cachedMenuPanel = FindMenuButtonsPanel();
        }

        Transform menuPanel = cachedMenuPanel;
        if (menuPanel == null)
        {
            return;
        }

        BindMenuButton(menuPanel, "Player_Btn", AlertCategory.Aircraft);
        BindMenuButton(menuPanel, "Inventory_Btn", AlertCategory.Equipment);
        BindMenuButton(menuPanel, "Base_Btn", AlertCategory.Base);
        BindMenuButton(menuPanel, "Achievement_Btn", AlertCategory.Achievement);
        BindMenuButton(menuPanel, "Shop_Btn", AlertCategory.Shop);
    }

    private void BindMenuButton(Transform menuPanel, string buttonName, AlertCategory category)
    {
        Transform buttonTransform = FindDirectChild(menuPanel, buttonName);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        Transform alert = buttonTransform != null ? FindChildByName(buttonTransform, "alert") : null;
        if (button == null || alert == null || wiredButtons.Contains(button))
        {
            return;
        }

        // Focus 안의 alert를 그대로 사용해서 프리팹 계층을 런타임에 바꾸지 않는다.
        bindings.Add(new AlertBinding
        {
            category = category,
            button = button,
            alertIcon = alert.gameObject
        });
        wiredButtons.Add(button);

        AlertCategory capturedCategory = category;
        button.onClick.AddListener(() => MarkSeen(capturedCategory));
    }

    private void RefreshAlerts()
    {
        bool aircraftAlert = HasNewToken(Scoped(CollectionSeenKey), BuildCollectionSignature());
        bool equipmentAlert = HasNewToken(Scoped(EquipmentSeenKey), BuildEquipmentSignature());
        bool baseAlert = HasBaseUpgradeCompleted(BuildBaseSignature());
        bool achievementAlert = HasCompletedAchievement();
        bool shopAlert = CanUseShop();

        for (int i = bindings.Count - 1; i >= 0; i--)
        {
            AlertBinding binding = bindings[i];
            if (binding?.button == null || binding.alertIcon == null)
            {
                bindings.RemoveAt(i);
                continue;
            }

            binding.alertIcon.SetActive(binding.category switch
            {
                AlertCategory.Aircraft => aircraftAlert,
                AlertCategory.Equipment => equipmentAlert,
                AlertCategory.Base => baseAlert,
                AlertCategory.Achievement => achievementAlert,
                AlertCategory.Shop => shopAlert,
                _ => false
            });
        }
    }

    private void MarkSeen(AlertCategory category)
    {
        switch (category)
        {
            case AlertCategory.Aircraft:
                PlayerPrefs.SetString(Scoped(CollectionSeenKey), BuildCollectionSignature());
                break;
            case AlertCategory.Equipment:
                PlayerPrefs.SetString(Scoped(EquipmentSeenKey), BuildEquipmentSignature());
                break;
            case AlertCategory.Base:
                PlayerPrefs.SetString(Scoped(BaseSeenKey), BuildBaseSignature());
                break;
        }

        PlayerPrefs.Save();
        RefreshAlerts();
    }

    private string BuildCollectionSignature()
    {
        if (inventory == null)
        {
            return string.Empty;
        }

        List<string> ids = new List<string>();
        IReadOnlyList<ProjectileConfig> weapons = inventory.WeaponConfigs;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null)
            {
                ids.Add("W:" + weapons[i].Id);
            }
        }

        IReadOnlyList<string> drones = inventory.OwnedDroneIds;
        for (int i = 0; i < drones.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(drones[i]))
            {
                ids.Add("D:" + drones[i]);
            }
        }

        return JoinSorted(ids);
    }

    private string BuildEquipmentSignature()
    {
        if (inventory == null)
        {
            return string.Empty;
        }

        List<string> ids = new List<string>();
        IReadOnlyList<EquipmentPartInstance> parts = inventory.EquipmentParts;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null && !string.IsNullOrWhiteSpace(parts[i].instanceId))
            {
                ids.Add(parts[i].instanceId);
            }
        }

        return JoinSorted(ids);
    }

    private string BuildBaseSignature()
    {
        if (baseCampManager == null)
        {
            return string.Empty;
        }

        return string.Join("|",
            baseCampManager.CommandCenter != null ? baseCampManager.CommandCenter.Level.ToString() : "0",
            baseCampManager.CreditRefinery != null ? baseCampManager.CreditRefinery.Level.ToString() : "0",
            baseCampManager.AssemblyFactory != null ? baseCampManager.AssemblyFactory.Level.ToString() : "0",
            baseCampManager.CoreCharger != null ? baseCampManager.CoreCharger.Level.ToString() : "0");
    }

    private bool HasCompletedAchievement()
    {
        if (achievementManager == null)
        {
            return false;
        }

        IReadOnlyList<AchievementManager.AchievementEntry> achievements = achievementManager.Achievements;
        for (int i = 0; i < achievements.Count; i++)
        {
            if (achievements[i] != null && achievements[i].Completed)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanUseShop()
    {
        if (gachaFacility == null)
        {
            return false;
        }

        return gachaFacility.CanDraw(GachaCategory.Weapon, 1)
            || gachaFacility.CanDraw(GachaCategory.Skill, 1);
    }

    private static string JoinSorted(List<string> values)
    {
        values.Sort();
        return string.Join("|", values);
    }

    private static bool HasNewToken(string key, string currentSignature)
    {
        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        HashSet<string> seenTokens = BuildTokenSet(PlayerPrefs.GetString(key, string.Empty));
        string[] currentTokens = SplitSignature(currentSignature);
        for (int i = 0; i < currentTokens.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(currentTokens[i]) && !seenTokens.Contains(currentTokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBaseUpgradeCompleted(string currentSignature)
    {
        string scopedBaseKey = Scoped(BaseSeenKey);
        if (!PlayerPrefs.HasKey(scopedBaseKey))
        {
            return false;
        }

        string[] seen = SplitSignature(PlayerPrefs.GetString(scopedBaseKey, string.Empty));
        string[] current = SplitSignature(currentSignature);
        int count = Mathf.Min(seen.Length, current.Length);
        for (int i = 0; i < count; i++)
        {
            if (int.TryParse(current[i], out int currentLevel)
                && int.TryParse(seen[i], out int seenLevel)
                && currentLevel > seenLevel)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildTokenSet(string signature)
    {
        HashSet<string> tokens = new HashSet<string>();
        string[] values = SplitSignature(signature);
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                tokens.Add(values[i]);
            }
        }

        return tokens;
    }

    private static string[] SplitSignature(string signature)
    {
        return string.IsNullOrWhiteSpace(signature)
            ? System.Array.Empty<string>()
            : signature.Split('|');
    }

    private static void HandleAchievementCompleted(AchievementManager.AchievementEntry achievement)
    {
        FindFirstObjectByType<MenuAlertController>(FindObjectsInactive.Include)?.RefreshAlerts();
    }

    private static void HandleCurrencyChanged(int value)
    {
        FindFirstObjectByType<MenuAlertController>(FindObjectsInactive.Include)?.RefreshAlerts();
    }

    private static Transform FindMenuButtonsPanel()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == "MenuButtons_Panel")
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildByName(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
