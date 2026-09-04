using UnityEngine;

/// <summary>
/// 스테이지 진행에 맞춰 맵 배경(Plane) 머티리얼을 교체한다.
/// 기본값은 10스테이지마다 다음 배경으로 전환한다. (1~10 → 0번, 11~20 → 1번 ...)
/// </summary>
[DisallowMultipleComponent]
public class MapBackgroundManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawnManager spawnManager;
    [Tooltip("배경을 렌더하는 Plane의 Renderer. 비워두면 같은 오브젝트의 Renderer를 사용한다.")]
    [SerializeField] private Renderer backgroundRenderer;

    [Header("Backgrounds")]
    [Tooltip("배경 머티리얼 목록. stagesPerBackground 스테이지마다 다음 항목으로 교체된다.")]
    [SerializeField] private Material[] backgroundMaterials;
    [Tooltip("몇 스테이지마다 배경을 교체할지 (기본 10).")]
    [SerializeField] private int stagesPerBackground = 10;
    [Tooltip("머티리얼 수를 넘어가면 처음부터 순환할지 여부. 끄면 마지막 머티리얼에 고정된다.")]
    [SerializeField] private bool loopMaterials = true;

    [Header("Transition")]
    [Tooltip("스테이지 전환으로 배경이 바뀔 때 화면 페이드 아웃/인으로 자연스럽게 교체할지 여부.")]
    [SerializeField] private bool useFadeTransition = true;
    [Tooltip("페이드 아웃/인 각각의 지속 시간(초).")]
    [SerializeField] private float fadeDuration = 0.4f;
    [Tooltip("검은 화면을 유지하는 추가 시간(초). 0이면 교체 즉시 페이드 인.")]
    [SerializeField] private float fadeHoldSeconds = 0f;

    private int appliedIndex = -1;
    private bool initialized;

    private void Awake()
    {
        if (spawnManager == null)
        {
            spawnManager = FindFirstObjectByType<EnemySpawnManager>();
        }

        if (backgroundRenderer == null)
        {
            backgroundRenderer = GetComponent<Renderer>();
        }
    }

    private void OnEnable()
    {
        if (spawnManager != null)
        {
            spawnManager.StageChanged += HandleStageChanged;
        }
    }

    private void OnDisable()
    {
        if (spawnManager != null)
        {
            spawnManager.StageChanged -= HandleStageChanged;
        }
    }

    private void Start()
    {
        // 시작 시점의 스테이지(세이브 복원 포함)에 맞춰 초기 배경을 즉시 적용한다(페이드 없음).
        if (spawnManager != null)
        {
            ApplyForStage(spawnManager.CurrentStage, false);
        }

        // 초기화가 끝난 뒤의 스테이지 변경부터 페이드 연출을 적용한다.
        initialized = true;
    }

    private void HandleStageChanged(int stage)
    {
        // 초기화 전(세이브 로드 중)에 들어온 변경은 페이드 없이 즉시 반영해 시작 시 깜빡임을 막는다.
        ApplyForStage(stage, initialized);
    }

    private void ApplyForStage(int stage, bool animate)
    {
        if (backgroundRenderer == null || backgroundMaterials == null || backgroundMaterials.Length == 0)
        {
            return;
        }

        int perBackground = Mathf.Max(1, stagesPerBackground);
        int rawIndex = Mathf.Max(0, stage - 1) / perBackground;
        int index = loopMaterials
            ? rawIndex % backgroundMaterials.Length
            : Mathf.Min(rawIndex, backgroundMaterials.Length - 1);

        if (index == appliedIndex)
        {
            return;
        }

        Material material = backgroundMaterials[index];
        if (material == null)
        {
            return;
        }

        appliedIndex = index;

        if (animate && useFadeTransition && Application.isPlaying)
        {
            // 페이드로 화면이 검어진 순간 머티리얼을 교체해 전환을 감춘다.
            ScreenFader.Instance.FadeTransition(
                () => backgroundRenderer.sharedMaterial = material,
                fadeDuration,
                fadeHoldSeconds);
        }
        else
        {
            backgroundRenderer.sharedMaterial = material;
        }
    }
}
