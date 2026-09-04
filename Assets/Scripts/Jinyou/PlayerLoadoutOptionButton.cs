using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerLoadoutOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private RawImage droneOfSkillIcon;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private GameObject selectedMark;

    private System.Action onClick;
    private Color rarityTextDefaultColor = Color.white;
    private bool hasRarityTextDefaultColor;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>() ?? GetComponentInChildren<Button>();
        }

        CacheRarityTextDefaultColor();
    }

    private void OnEnable()
    {
        button?.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(HandleClick);
    }

    public void Bind(
        string title,
        string rarityLabel,
        string summary,
        bool selected,
        System.Action clickAction,
        Sprite iconSprite = null,
        DroneConfig droneConfig = null,
        Color? rarityColor = null,
        PlayerSkillConfig skillConfig = null,
        int skillLevel = 1)
    {
        onClick = clickAction;
        SetText(nameText, title);
        SetText(rarityText, rarityLabel);
        SetRarityTextColor(rarityColor);
        SetText(summaryText, summary);
        SetWeaponIcon(iconSprite);
        SetDroneOfSkillIcon(droneConfig, skillConfig, skillLevel);
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark != null)
        {
            selectedMark.SetActive(selected);
        }
    }

    private void HandleClick()
    {
        onClick?.Invoke();
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    // 무기는 스프라이트 이미지로 동기화
    private void CacheRarityTextDefaultColor()
    {
        if (rarityText == null || hasRarityTextDefaultColor)
        {
            return;
        }

        rarityTextDefaultColor = rarityText.color;
        hasRarityTextDefaultColor = true;
    }

    private void SetRarityTextColor(Color? color)
    {
        if (rarityText == null)
        {
            return;
        }

        CacheRarityTextDefaultColor();
        rarityText.color = color ?? rarityTextDefaultColor;
    }

    private void SetWeaponIcon(Sprite sprite)
    {
        if (weaponIcon == null)
        {
            return;
        }

        weaponIcon.sprite = sprite;
        weaponIcon.enabled = sprite != null;
        weaponIcon.preserveAspect = true;
        weaponIcon.gameObject.SetActive(sprite != null);
    }

    // 드론 및 스킬은 프리팹으로 렌더링헤서 이미지 동기화
    private void SetDroneOfSkillIcon(DroneConfig droneConfig, PlayerSkillConfig skillConfig, int skillLevel)
    {
        if (droneOfSkillIcon == null)
        {
            return;
        }

        GameObject prefab = droneConfig != null
            ? droneConfig.DronePrefab
            : GetSkillPreviewPrefab(skillConfig);
        string cacheKey = skillConfig != null
            ? $"{skillConfig.GetInstanceID()}_Level{Mathf.Max(1, skillLevel)}"
            : null;
        if (prefab == null)
        {
            droneOfSkillIcon.texture = null;
            droneOfSkillIcon.color = Color.clear;
            droneOfSkillIcon.gameObject.SetActive(false);
            return;
        }

        RenderTexture preview = skillConfig != null
            ? UnitPreviewRenderer.Instance.GetPreview(
                prefab,
                cacheKey,
                instance => ApplySkillPreviewSkin(instance, skillConfig, skillLevel))
            : UnitPreviewRenderer.Instance.GetPreview(prefab);
        droneOfSkillIcon.texture = preview;
        droneOfSkillIcon.color = preview != null ? Color.white : Color.clear;
        droneOfSkillIcon.gameObject.SetActive(preview != null);
    }

    // 수집레벨에 따른 스킬스킨 적용도 동기화하여 반영한다.
    private static void ApplySkillPreviewSkin(GameObject instance, PlayerSkillConfig skill, int skillLevel)
    {
        if (instance == null || skill == null)
        {
            return;
        }

        SkinEditor_SkillLevel[] skinEditors = instance.GetComponentsInChildren<SkinEditor_SkillLevel>(true);
        for (int i = 0; i < skinEditors.Length; i++)
        {
            skinEditors[i]?.SetSkill(skill, Mathf.Max(1, skillLevel));
        }
    }

    // 스킬 타입별 프리팹 가져오기
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
}
