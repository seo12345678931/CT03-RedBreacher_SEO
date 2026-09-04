using UnityEngine;

/// <summary>
/// 바닥(XZ 평면)에 깔리는 어두운 그림자 데칼. 지정한 Transform의 XZ 위치를 따라다닌다.
/// 스프라이트를 주지 않으면 부드러운 어두운 원형을 절차적으로 생성한다(아트 불필요).
/// 비행체(스텔스 폭격기 등)의 "짙은 그림자 연출"에 사용.
/// </summary>
public class GroundShadowDecal : MonoBehaviour
{
    private static readonly Quaternion GroundRotation = Quaternion.Euler(90f, 0f, 0f);
    private static Sprite cachedSoftCircle;

    private Transform followTarget;

    public static GroundShadowDecal Create(Transform target, float size, float darkness, int sortingOrder, Sprite overrideSprite)
    {
        GameObject go = new GameObject("GroundShadowDecal");
        GroundShadowDecal decal = go.AddComponent<GroundShadowDecal>();

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = overrideSprite != null ? overrideSprite : GetSoftCircle();
        renderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(darkness));
        renderer.sortingOrder = sortingOrder;

        go.transform.rotation = GroundRotation;
        go.transform.localScale = Vector3.one * Mathf.Max(0.1f, size);

        decal.followTarget = target;
        decal.UpdatePosition();
        return decal;
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            return;
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        Vector3 position = CombatPlane.WithFixedY(followTarget.position);
        position.y += 0.02f; // z-fighting 방지
        transform.position = position;
    }

    private static Sprite GetSoftCircle()
    {
        if (cachedSoftCircle != null)
        {
            return cachedSoftCircle;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha; // 가장자리를 부드럽게
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cachedSoftCircle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedSoftCircle;
    }
}
