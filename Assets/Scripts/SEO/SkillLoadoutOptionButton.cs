using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SkillLoadoutOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private Image skillIcon;
    [SerializeField] private GameObject selectedMark;

    private System.Action onClick;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>() ?? GetComponentInChildren<Button>();
        }
    }

    private void OnEnable()
    {
        button?.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        button?.onClick.RemoveListener(HandleClick);
    }

    public void Bind(PlayerSkillConfig skill, bool selected, System.Action clickAction)
    {
        onClick = clickAction;
        SetText(skillNameText, skill != null ? skill.DisplayName : string.Empty);
        SetIcon(skill != null ? skill.Icon : null);
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

    private void SetIcon(Sprite sprite)
    {
        if (skillIcon == null)
        {
            return;
        }

        skillIcon.sprite = sprite;
        skillIcon.enabled = sprite != null;
        skillIcon.preserveAspect = true;
        skillIcon.gameObject.SetActive(sprite != null);
    }
}
