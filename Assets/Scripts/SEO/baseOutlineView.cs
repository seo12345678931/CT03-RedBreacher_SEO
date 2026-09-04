using UnityEngine;
using UnityEngine.UI;

public class baseOutlineView : MonoBehaviour
{
    public enum FacilityType
    {
        CommandCenter,
        EnergyRefinery,
        AssemblyFactory,
        CoreCharger,
        TraitPointFacility,
        Inventory,
        WeaponGacha,
        BossTracker
    }
    
    [Header("Facility")]
    [SerializeField] private FacilityType facilityType;
    [SerializeField] private BaseCampManager baseCampManager;

    [Header("Visual")]
    [SerializeField] private Image facilityImage;
    [SerializeField] private Sprite[] levelSprites;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeFacilityEvents();
        UpdateVisual();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeFacilityEvents();
        UpdateVisual();
    }

    private void OnDisable()
    {
        UnsubscribeFacilityEvents();
    }

    public void UpdateVisual()
    {
        if (facilityImage == null || levelSprites == null || levelSprites.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(GetCurrentLevel() - 1, 0, levelSprites.Length - 1);
        facilityImage.sprite = levelSprites[index];
    }

    private int GetCurrentLevel()
    {
        ResolveReferences();

        return facilityType switch
        {
            FacilityType.CommandCenter => baseCampManager?.CommandCenter?.Level ?? 1,
            FacilityType.EnergyRefinery => baseCampManager?.CreditRefinery?.Level ?? 1,
            FacilityType.AssemblyFactory => baseCampManager?.AssemblyFactory?.Level ?? 1,
            FacilityType.CoreCharger => baseCampManager?.CoreCharger?.Level ?? 1,
            FacilityType.TraitPointFacility => 1,
            FacilityType.Inventory => 1,
            FacilityType.WeaponGacha => 1,
            FacilityType.BossTracker => baseCampManager?.CommandCenter?.Level ?? 1,
            _ => 1
        };
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
                    baseCampManager.CommandCenter.OnUpgradeCompleted.AddListener(UpdateVisual);
                }
                break;
            case FacilityType.EnergyRefinery:
                if (baseCampManager?.CreditRefinery != null)
                {
                    baseCampManager.CreditRefinery.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CreditRefinery.OnUpgradeCompleted.AddListener(UpdateVisual);
                }
                break;
            case FacilityType.AssemblyFactory:
                if (baseCampManager?.AssemblyFactory != null)
                {
                    baseCampManager.AssemblyFactory.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.AssemblyFactory.OnUpgradeCompleted.AddListener(UpdateVisual);
                }
                break;
            case FacilityType.CoreCharger:
                if (baseCampManager?.CoreCharger != null)
                {
                    baseCampManager.CoreCharger.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CoreCharger.OnUpgradeCompleted.AddListener(UpdateVisual);
                }
                break;
            case FacilityType.BossTracker:
                if (baseCampManager?.CommandCenter != null)
                {
                    baseCampManager.CommandCenter.OnLevelChanged.AddListener(HandleLevelChanged);
                    baseCampManager.CommandCenter.OnUpgradeCompleted.AddListener(UpdateVisual);
                }
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
            baseCampManager.CommandCenter.OnUpgradeCompleted.RemoveListener(UpdateVisual);
        }

        if (baseCampManager.CreditRefinery != null)
        {
            baseCampManager.CreditRefinery.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.CreditRefinery.OnUpgradeCompleted.RemoveListener(UpdateVisual);
        }

        if (baseCampManager.AssemblyFactory != null)
        {
            baseCampManager.AssemblyFactory.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.AssemblyFactory.OnUpgradeCompleted.RemoveListener(UpdateVisual);
        }

        if (baseCampManager.CoreCharger != null)
        {
            baseCampManager.CoreCharger.OnLevelChanged.RemoveListener(HandleLevelChanged);
            baseCampManager.CoreCharger.OnUpgradeCompleted.RemoveListener(UpdateVisual);
        }
    }

    private void HandleLevelChanged(int level)
    {
        UpdateVisual();
    }

    private void ResolveReferences()
    {
        baseCampManager ??= BaseCampManager.Instance ?? FindFirstObjectByType<BaseCampManager>();
        baseCampManager?.ConnectFacilities();
        facilityImage ??= ResolveFacilityImage();
    }

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
