using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerDroneIconBinder : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private PlayerDroneController droneController;

    private void Reset()
    {
        targetImage = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        Sync();
    }

    public void Sync()
    {
        if (targetImage == null)
        {
            return;
        }

        droneController ??= FindFirstObjectByType<PlayerDroneController>(FindObjectsInactive.Include);

        DroneConfig config = droneController != null ? droneController.DroneConfig : null;
        GameObject prefab = config != null ? config.DronePrefab : null;
        if (prefab == null)
        {
            return;
        }

        RenderTexture rt = UnitPreviewRenderer.Instance.GetPreview(prefab);
        if (rt != null)
        {
            targetImage.texture = rt;
            targetImage.color = Color.white;
        }
    }
}
