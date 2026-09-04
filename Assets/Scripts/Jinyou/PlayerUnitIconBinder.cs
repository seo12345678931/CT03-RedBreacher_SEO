using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대상 RawImage를 현재 플레이어 유닛(<see cref="PlayerUnitConfig"/>) 프리팹의
/// "렌더된 모습 그대로"(본체+포탑+포신+트랙 등 전체)와 동기화한다.
/// 활성화(팝업 열림)될 때마다 갱신하므로 유닛을 교체하면 아이콘도 따라간다.
/// 실제 렌더링은 <see cref="UnitPreviewRenderer"/>가 RenderTexture로 처리한다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerUnitIconBinder : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private PlayerController player;

    private GameObject renderedPrefab;

    private void Reset()
    {
        targetImage = GetComponent<RawImage>();
    }

    private void Awake()
    {
        ResolveTarget();
    }

    private void OnEnable()
    {
        Sync();
    }

    private void Update()
    {
        GameObject prefab = ResolveUnitPrefab();
        if (prefab != null && (targetImage == null || targetImage.texture == null || renderedPrefab != prefab))
        {
            // 플레이어 유닛이 늦게 초기화되는 경우를 대비해 필요한 때만 다시 그린다.
            Sync(prefab);
        }
    }

    public void Sync()
    {
        Sync(ResolveUnitPrefab());
    }

    private void Sync(GameObject prefab)
    {
        ResolveTarget();
        if (targetImage == null)
        {
            return;
        }

        if (prefab == null)
        {
            Clear();
            return;
        }

        RenderTexture rt = UnitPreviewRenderer.Instance.GetPreview(prefab);
        if (rt != null)
        {
            targetImage.texture = rt;
            targetImage.color = Color.white;
            renderedPrefab = prefab;
        }
    }

    private void ResolveTarget()
    {
        targetImage ??= GetComponent<RawImage>();
    }

    private GameObject ResolveUnitPrefab()
    {
        player ??= FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        PlayerUnitConfig config = player != null ? player.UnitConfig : null;
        return config != null ? config.UnitPrefab : null;
    }

    private void Clear()
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.texture = null;
        targetImage.color = Color.clear;
        renderedPrefab = null;
    }
}
