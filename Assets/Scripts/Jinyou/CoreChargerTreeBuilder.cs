using UnityEngine;

public class CoreChargerTreeBuilder : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;

    private void OnEnable()
    {
        HideLegacyTree();
    }

    public void RebuildSelectedRoute()
    {
        HideLegacyTree();
    }

    public void RebuildRoute(string routeId)
    {
        HideLegacyTree();
    }

    public void RebuildUnits()
    {
        HideLegacyTree();
    }

    private void HideLegacyTree()
    {
        if (contentRoot != null)
        {
            contentRoot.gameObject.SetActive(false);
        }
    }
}
