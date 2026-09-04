using UnityEngine;

/// <summary>
/// 유닛의 SpriteRenderer들에 드롭섀도 셰이더 머티리얼(Custom/SpriteDropShadow)을 적용한다.
/// 셰이더가 그림자 프리패스를 함께 그리므로, 예전처럼 그림자용 SpriteRenderer를 따로 만들거나
/// 매 프레임 동기화할 필요가 없다(추가 GameObject/LateUpdate 제거 → CPU·GC·드로우 절감).
/// </summary>
[DisallowMultipleComponent]
public sealed class SpriteShapeShadow : MonoBehaviour
{
    private const string MaterialResourcePath = "SpriteDropShadow";

    private static Material sharedShadowMaterial;
    private static bool materialLoadAttempted;

    public static SpriteShapeShadow Ensure(GameObject owner)
    {
        SpriteShapeShadow shadow = owner.GetComponent<SpriteShapeShadow>();
        if (shadow == null)
        {
            shadow = owner.AddComponent<SpriteShapeShadow>();
        }

        shadow.Apply();
        return shadow;
    }

    private void OnEnable()
    {
        Apply();
    }

    /// <summary>자식 SpriteRenderer 전체에 드롭섀도 머티리얼을 적용한다. 동적으로 스프라이트가
    /// 추가된 경우 다시 호출하면 된다(평상시 매 프레임 호출 불필요).</summary>
    public void Apply()
    {
        Material material = GetSharedMaterial();
        if (material == null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
            }
        }
    }

    private static Material GetSharedMaterial()
    {
        if (sharedShadowMaterial == null && !materialLoadAttempted)
        {
            materialLoadAttempted = true;
            sharedShadowMaterial = Resources.Load<Material>(MaterialResourcePath);
            if (sharedShadowMaterial == null)
            {
                Debug.LogError($"드롭섀도 머티리얼을 찾을 수 없습니다: Resources/{MaterialResourcePath}");
            }
        }

        return sharedShadowMaterial;
    }
}
