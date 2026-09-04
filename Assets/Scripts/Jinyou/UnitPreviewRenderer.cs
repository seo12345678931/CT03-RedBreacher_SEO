using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 SpriteRenderer로 구성된 유닛 프리팹(탱크)을 통째로 RenderTexture에 렌더링해
/// UI에서 "프리팹 모습 그대로" 아이콘으로 쓸 수 있게 한다.
/// 화면 밖 전용 스테이지에 프리팹을 복제(스크립트/물리는 제거)하고 톱다운 오쏘 카메라로 1프레임 렌더한다.
/// 결과는 프리팹별로 캐시한다.
/// </summary>
public class UnitPreviewRenderer : MonoBehaviour
{
    private const string PreviewLayerName = "UnitPreview";
    private const int TextureSize = 256;
    private static readonly Vector3 StageOrigin = new Vector3(10000f, 10000f, 10000f);

    private static UnitPreviewRenderer instance;

    private Camera previewCamera;
    private int previewLayer;
    private readonly Dictionary<int, RenderTexture> cache = new Dictionary<int, RenderTexture>();

    public static UnitPreviewRenderer Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("UnitPreviewRenderer");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<UnitPreviewRenderer>();
                instance.Initialize();
            }

            return instance;
        }
    }

    private void Initialize()
    {
        previewLayer = LayerMask.NameToLayer(PreviewLayerName);

        GameObject camObj = new GameObject("PreviewCamera");
        camObj.transform.SetParent(transform, false);
        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 배경
        previewCamera.cullingMask = previewLayer >= 0 ? (1 << previewLayer) : ~0;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 1000f;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = false;
        previewCamera.enabled = false; // 수동 Render만 사용
    }

    // 기존의 프리뷰 호출부
    public RenderTexture GetPreview(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        int key = prefab.GetInstanceID();
        if (cache.TryGetValue(key, out RenderTexture cached) && cached != null)
        {
            return cached;
        }

        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "UnitPreview_" + prefab.name
        };
        rt.Create();
        RenderPrefab(prefab, rt);
        cache[key] = rt;
        return rt;
    }

    // 스킬 프리팹용 프리뷰 호출부 (GetPreview 커스텀한 별도의 오버로드)
    // 스킬에 담긴 색상스킨(SkinEditor_SkillLevel)을 표시하려면
    // 프리팹 + 스킬 + 레벨 조합을 별도 키로 저장하는 커스텀 오버로드가 필요하다.
    public RenderTexture GetPreview(GameObject prefab, string cacheKeySuffix, Action<GameObject> configureInstance)
    {
        if (prefab == null)
        {
            return null;
        }

        string suffix = string.IsNullOrWhiteSpace(cacheKeySuffix) ? "default" : cacheKeySuffix;
        int key = HashCode.Combine(prefab.GetInstanceID(), suffix);
        if (cache.TryGetValue(key, out RenderTexture cached) && cached != null)
        {
            return cached;
        }

        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "UnitPreview_" + prefab.name + "_" + suffix
        };
        rt.Create();
        RenderPrefab(prefab, rt, configureInstance);
        cache[key] = rt;
        return rt;
    }

    private void RenderPrefab(GameObject prefab, RenderTexture target, Action<GameObject> configureInstance = null)
    {
        // 비활성 부모 아래에 복제 → 인스턴스의 Awake/OnEnable이 실행되지 않는다.
        GameObject stage = new GameObject("PreviewStage");
        stage.transform.SetParent(transform, false);
        stage.transform.position = StageOrigin;
        stage.SetActive(false);

        GameObject unit = Instantiate(prefab, stage.transform);
        unit.transform.localPosition = Vector3.zero;
        configureInstance?.Invoke(unit);

        // 렌더에 필요한 SpriteRenderer만 남기고 스크립트/물리는 제거(활성화 전이라 Awake 미실행).
        foreach (MonoBehaviour mb in unit.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null)
            {
                DestroyImmediate(mb);
            }
        }

        foreach (Rigidbody rb in unit.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null)
            {
                DestroyImmediate(rb);
            }
        }

        foreach (Collider col in unit.GetComponentsInChildren<Collider>(true))
        {
            if (col != null)
            {
                DestroyImmediate(col);
            }
        }

        SetLayerRecursively(stage, previewLayer >= 0 ? previewLayer : 0);
        stage.SetActive(true);

        FrameCamera(unit);

        RenderTexture previous = previewCamera.targetTexture;
        previewCamera.targetTexture = target;
        previewCamera.Render();
        previewCamera.targetTexture = previous;

        // Destroy는 프레임 끝까지 지연되므로 다음 프리뷰 렌더에 이전 유닛이 섞이지 않게 즉시 비활성화한다.
        stage.SetActive(false);
        Destroy(stage);
    }

    private void FrameCamera(GameObject unit)
    {
        Bounds bounds;
        Vector3 center = StageOrigin;
        if (TryGetSpriteBounds(unit, out bounds))
        {
            center = bounds.center;
            PreviewFrame frame = CalculatePreviewFrame(bounds);
            float halfExtent = Mathf.Max(frame.horizontalExtent, frame.verticalExtent);
            previewCamera.orthographicSize = Mathf.Max(0.1f, halfExtent * 1.15f);
            previewCamera.transform.position = center + frame.viewDirection * 100f;
            previewCamera.transform.rotation = Quaternion.LookRotation(-frame.viewDirection, frame.upDirection);
            return;
        }
        else
        {
            previewCamera.orthographicSize = 3f;
        }

        previewCamera.transform.position = center + Vector3.up * 100f;
        previewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    }

    private struct PreviewFrame
    {
        public Vector3 viewDirection;
        public Vector3 upDirection;
        public float horizontalExtent;
        public float verticalExtent;
    }

    private static PreviewFrame CalculatePreviewFrame(Bounds bounds)
    {
        Vector3 extents = bounds.extents;

        if (extents.z <= extents.x && extents.z <= extents.y)
        {
            // XY 평면 스프라이트는 정면(+Z)에서 본다.
            return new PreviewFrame
            {
                viewDirection = Vector3.forward,
                upDirection = Vector3.up,
                horizontalExtent = extents.x,
                verticalExtent = extents.y
            };
        }

        if (extents.y <= extents.x && extents.y <= extents.z)
        {
            // XZ 평면 스프라이트는 위(+Y)에서 본다.
            return new PreviewFrame
            {
                viewDirection = Vector3.up,
                upDirection = Vector3.forward,
                horizontalExtent = extents.x,
                verticalExtent = extents.z
            };
        }

        return new PreviewFrame
        {
            viewDirection = Vector3.right,
            upDirection = Vector3.up,
            horizontalExtent = extents.z,
            verticalExtent = extents.y
        };
    }

    private static bool TryGetSpriteBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool initialized = false;
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = sr.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(sr.bounds);
            }
        }

        return initialized;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
