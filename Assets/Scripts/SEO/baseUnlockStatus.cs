using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class baseUnlockStatus : MonoBehaviour
{
    [SerializeField] private Image baseIcon;
    [SerializeField] private Image lockStateIcon;
    [SerializeField] private TMP_Text baseNameText;
    [SerializeField] private TMP_Text baseLevelText;

    private BaseCampFacilityView facilityView;
    private CommandCenter.FacilityUnlock facilityUnlock;

    public string FacilityId => facilityUnlock != null ? facilityUnlock.facilityId : string.Empty;

    public void Configure(
        BaseCampFacilityView view,
        CommandCenter.FacilityUnlock unlock)
    {
        facilityView = view;
        facilityUnlock = unlock;
        facilityView?.SyncView();
        Refresh();
    }

    public void Refresh()
    {
        facilityView?.SyncView();

        Sprite facilitySprite = facilityView != null ? facilityView.FacilitySprite : null;
        if (baseIcon != null)
        {
            baseIcon.sprite = facilitySprite;
            baseIcon.enabled = facilitySprite != null;
            baseIcon.preserveAspect = true;
        }

        if (lockStateIcon != null)
        {
            lockStateIcon.gameObject.SetActive(facilityUnlock != null && !facilityUnlock.unlocked);
        }

        SetText(baseNameText, facilityUnlock != null
            ? facilityUnlock.displayName
            : string.Empty);
        SetText(baseLevelText, facilityUnlock != null && facilityUnlock.unlocked
            ? $"Lv.{GetCurrentFacilityLevel()}"
            : string.Empty);
    }

    private int GetCurrentFacilityLevel()
    {
        return facilityView != null
            ? facilityView.CurrentLevel
            : facilityUnlock != null ? facilityUnlock.requiredLabLevel : 1;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
