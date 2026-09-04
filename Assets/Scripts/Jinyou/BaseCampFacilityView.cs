using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BaseCampFacilityView : MonoBehaviour, IPointerClickHandler
{
    public enum FacilityType
    {
        CommandCenter,
        CreditRefinery,
        AssemblyFactory,
        CoreCharger,
        TraitPointFacility,
        SkillHanger,
        BossTracker
    }

    [Header("Facility")]
    [SerializeField] private FacilityType facilityType;
    [SerializeField] private BaseCampManager baseCampManager;
    [SerializeField] private GameObject facilityPanel;
    [SerializeField] private Button facilityButton;

    [Header("Visual")]
    [SerializeField] private Image facilityImage;
    [SerializeField] private Sprite[] levelSprites;

    [Header("Events")]
    public UnityEvent OnSelected = new UnityEvent();

    private int lastSelectFrame = -1;

    public string FacilityId => GetFacilityId();
    public Sprite FacilitySprite => facilityImage != null ? facilityImage.sprite : null;
    public int CurrentLevel => GetCurrentLevel();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeFacilityEvents();
        facilityButton?.onClick.AddListener(SelectFacility);
        SyncView();
    }

    // 이미지 레벨별 출력 초기화 순서 재정렬
    private void Start()
    {
        ResolveReferences();
        SubscribeFacilityEvents();
        SyncView();
    }

    private void OnDisable()
    {
        UnsubscribeFacilityEvents();
        facilityButton?.onClick.RemoveListener(SelectFacility);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SelectFacility();
    }

    public void SelectFacility()
    {
        if (lastSelectFrame == Time.frameCount)
        {
            return;
        }

        lastSelectFrame = Time.frameCount;
        ResolveReferences();

        if (!IsUnlocked())
        {
            return;
        }

        if (baseCampManager != null)
        {
            baseCampManager.OpenPanel(facilityPanel);
        }
        else if (facilityPanel != null)
        {
            facilityPanel.SetActive(true);
        }

        OnSelected.Invoke();
    }

    public void Configure(
        FacilityType type,
        BaseCampManager manager,
        GameObject panel,
        Image targetImage,
        Sprite[] sprites)
    {
        if (isActiveAndEnabled)
        {
            UnsubscribeFacilityEvents();
        }

        facilityType = type;
        baseCampManager = manager;
        facilityPanel = panel;
        facilityImage = targetImage;
        levelSprites = sprites;

        if (isActiveAndEnabled)
        {
            SubscribeFacilityEvents();
        }

        SyncView();
    }

    public void SyncView()
    {
        UpdateVisual();
        UpdateInteractable();
    }

    public void UpdateVisual()
    {
        if (facilityImage == null || levelSprites == null || levelSprites.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(GetCurrentLevel() - 1, 0, levelSprites.Length - 1);
        facilityImage.sprite = levelSprites[index];
        facilityImage.color = IsUnlocked() ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        UpdateInteractable();
    }

    private int GetCurrentLevel()
    {
        ResolveReferences();

        return facilityType switch
        {
            FacilityType.CommandCenter => baseCampManager?.CommandCenter?.Level ?? 1,
            FacilityType.CreditRefinery => baseCampManager?.CreditRefinery?.Level ?? 1,
            FacilityType.AssemblyFactory => baseCampManager?.AssemblyFactory?.Level ?? 1,
            FacilityType.CoreCharger => baseCampManager?.CoreCharger?.Level ?? 1,
            FacilityType.TraitPointFacility => 1,
            FacilityType.SkillHanger => baseCampManager?.SkillHanger?.Level ?? 1,
            FacilityType.BossTracker => baseCampManager?.CommandCenter?.Level ?? 1,
            _ => 1
        };
    }

    private bool IsUnlocked()
    {
        ResolveReferences();

        if (facilityType == FacilityType.CommandCenter)
        {
            return true;
        }

        CommandCenter researchLab = baseCampManager != null ? baseCampManager.CommandCenter : null;
        if (researchLab == null)
        {
            return true;
        }

        return researchLab.IsFacilityUnlocked(GetFacilityId());
    }

    private string GetFacilityId()
    {
        return facilityType switch
        {
            FacilityType.CreditRefinery => "energy_refinery",
            FacilityType.AssemblyFactory => "assembly_factory",
            FacilityType.CoreCharger => "core_charger",
            FacilityType.TraitPointFacility => "trait_point_facility",
            FacilityType.SkillHanger => "skill_hanger",
            FacilityType.BossTracker => "boss_tracker",
            _ => string.Empty
        };
    }

    private void UpdateInteractable()
    {
        if (facilityButton != null)
        {
            facilityButton.interactable = IsUnlocked();
        }
    }

    private void SubscribeFacilityEvents()
    {
        UnsubscribeFacilityEvents();
        ResolveReferences();

        switch (facilityType)
        {
            case FacilityType.CommandCenter:
                if (baseCampManager?.CommandCenter != null)
                {
                    baseCampManager.CommandCenter.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CommandCenter.OnUpgradeStarted.AddListener(SyncView);
                    baseCampManager.CommandCenter.OnUpgradeCompleted.AddListener(SyncView);
                }
                break;
            case FacilityType.CreditRefinery:
                SubscribeResearchLabUnlockEvents();
                if (baseCampManager?.CreditRefinery != null)
                {
                    baseCampManager.CreditRefinery.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CreditRefinery.OnUpgradeStarted.AddListener(SyncView);
                    baseCampManager.CreditRefinery.OnUpgradeCompleted.AddListener(SyncView);
                }
                break;
            case FacilityType.AssemblyFactory:
                SubscribeResearchLabUnlockEvents();
                if (baseCampManager?.AssemblyFactory != null)
                {
                    baseCampManager.AssemblyFactory.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.AssemblyFactory.OnUpgradeStarted.AddListener(SyncView);
                    baseCampManager.AssemblyFactory.OnUpgradeCompleted.AddListener(SyncView);
                }
                break;
            case FacilityType.CoreCharger:
                SubscribeResearchLabUnlockEvents();
                if (baseCampManager?.CoreCharger != null)
                {
                    baseCampManager.CoreCharger.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CoreCharger.OnUpgradeStarted.AddListener(SyncView);
                    baseCampManager.CoreCharger.OnUpgradeCompleted.AddListener(SyncView);
                }
                break;
            case FacilityType.TraitPointFacility:
                SubscribeResearchLabUnlockEvents();
                break;
            case FacilityType.SkillHanger:
                SubscribeResearchLabUnlockEvents();
                if (baseCampManager?.SkillHanger != null)
                {
                    baseCampManager.SkillHanger.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.SkillHanger.OnUpgradeStarted.AddListener(SyncView);
                    baseCampManager.SkillHanger.OnUpgradeCompleted.AddListener(SyncView);
                }
                break;
            case FacilityType.BossTracker:
                SubscribeResearchLabUnlockEvents();
                break;
        }
    }

    private void UnsubscribeFacilityEvents()
    {
        if (baseCampManager == null)
        {
            return;
        }

        if (baseCampManager.CommandCenter != null)
        {
            baseCampManager.CommandCenter.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.CommandCenter.OnUpgradeStarted.RemoveListener(SyncView);
            baseCampManager.CommandCenter.OnUpgradeCompleted.RemoveListener(SyncView);
        }

        if (baseCampManager.CreditRefinery != null)
        {
            baseCampManager.CreditRefinery.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.CreditRefinery.OnUpgradeStarted.RemoveListener(SyncView);
            baseCampManager.CreditRefinery.OnUpgradeCompleted.RemoveListener(SyncView);
        }

        if (baseCampManager.AssemblyFactory != null)
        {
            baseCampManager.AssemblyFactory.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.AssemblyFactory.OnUpgradeStarted.RemoveListener(SyncView);
            baseCampManager.AssemblyFactory.OnUpgradeCompleted.RemoveListener(SyncView);
        }

        if (baseCampManager.CoreCharger != null)
        {
            baseCampManager.CoreCharger.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.CoreCharger.OnUpgradeStarted.RemoveListener(SyncView);
            baseCampManager.CoreCharger.OnUpgradeCompleted.RemoveListener(SyncView);
        }

        if (baseCampManager.SkillHanger != null)
        {
            baseCampManager.SkillHanger.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.SkillHanger.OnUpgradeStarted.RemoveListener(SyncView);
            baseCampManager.SkillHanger.OnUpgradeCompleted.RemoveListener(SyncView);
        }
    }

    private void SubscribeResearchLabUnlockEvents()
    {
        if (baseCampManager?.CommandCenter == null)
        {
            return;
        }

        baseCampManager.CommandCenter.OnLevelChanged.AddListener(HandleLevelChanged);
        baseCampManager.CommandCenter.OnUpgradeCompleted.AddListener(SyncView);
    }

    private void HandleLevelChanged(int level)
    {
        SyncView();
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        baseCampManager?.ConnectFacilities();
        facilityImage ??= ResolveFacilityImage();
        facilityButton ??= GetComponent<Button>() ?? GetComponentInChildren<Button>();
    }

    // 매니저를 통해 찾았지만 참고가 비어있는 경우를 대비해 ConnectFacilities()를 한 번 호출
    private Image ResolveFacilityImage()
    {
        Image directImage = GetComponent<Image>();
        Image[] childImages = GetComponentsInChildren<Image>(true);

        foreach (Image image in childImages)
        {
            if (image != null && ContainsLevelSprite(image.sprite))
            {
                return image;
            }
        }

        foreach (Image image in childImages)
        {
            if (image != null && image != directImage && image.sprite != null)
            {
                return image;
            }
        }

        return directImage != null ? directImage : childImages.Length > 0 ? childImages[0] : null;
    }

    private bool ContainsLevelSprite(Sprite sprite)
    {
        if (sprite == null || levelSprites == null)
        {
            return false;
        }

        foreach (Sprite levelSprite in levelSprites)
        {
            if (sprite == levelSprite)
            {
                return true;
            }
        }

        return false;
    }

}
