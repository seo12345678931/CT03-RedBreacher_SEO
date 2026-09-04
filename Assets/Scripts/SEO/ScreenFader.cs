using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬 전환 시 검은색 페이드 아웃/인 연출을 처리하는 싱글톤.
/// 자체적으로 DontDestroyOnLoad 최상단 오버레이 캔버스를 생성하므로 프리팹/씬 배선이 필요 없다.
/// 사용: ScreenFader.Instance.LoadScene("Myth");
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;

    private CanvasGroup group;
    private bool transitioning;

    public static ScreenFader Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public bool IsTransitioning => transitioning;

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("ScreenFader");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ScreenFader>();
        instance.Build();
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // 모든 UI 위에 덮이도록 최상단.

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject fade = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fade.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)fade.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = fade.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    /// <summary>현재 씬을 페이드 아웃 → 비동기 로드 → 새 씬 페이드 인.</summary>
    public void LoadScene(string sceneName, float fadeDuration = 0.4f)
    {
        if (transitioning || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(Transition(sceneName, Mathf.Max(0.01f, fadeDuration)));
    }

    /// <summary>씬 로드 없이 페이드 아웃 → (검은 화면에서) 액션 실행 → 페이드 인. 배경 교체 등 연출에 사용.</summary>
    public void FadeTransition(System.Action onBlackout, float fadeDuration = 0.4f, float holdSeconds = 0f)
    {
        if (transitioning)
        {
            // 이미 전환 중이면 변경이 누락되지 않도록 액션만 즉시 실행한다.
            onBlackout?.Invoke();
            return;
        }

        StartCoroutine(FadeTransitionRoutine(onBlackout, Mathf.Max(0.01f, fadeDuration), Mathf.Max(0f, holdSeconds)));
    }

    private IEnumerator FadeTransitionRoutine(System.Action onBlackout, float fadeDuration, float holdSeconds)
    {
        transitioning = true;
        group.blocksRaycasts = true;

        yield return Fade(0f, 1f, fadeDuration);

        onBlackout?.Invoke();

        if (holdSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(holdSeconds);
        }

        yield return Fade(1f, 0f, fadeDuration);

        group.blocksRaycasts = false;
        transitioning = false;
    }

    private IEnumerator Transition(string sceneName, float fadeDuration)
    {
        transitioning = true;
        group.blocksRaycasts = true;

        // 화면 페이드아웃과 동시에 현재 씬의 BGM도 페이드아웃(씬 언로드로 뚝 끊기지 않게).
        BGMManager.Instance?.FadeOut(fadeDuration);

        yield return Fade(0f, 1f, fadeDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        // 무거운 씬 로드 직후 첫 프레임은 deltaTime이 비정상적으로 크다(활성화+Awake/Start가 몰림).
        // 그 프레임을 흘려보내 페이드인이 한 번에 끝나는 것을 막는다.
        yield return null;
        yield return null;

        // 계정 세이브(클라우드 동기화 포함) 복원이 끝날 때까지 검은 화면을 유지한다.
        // 그래야 유닛/드론이 기본값에서 저장값으로 바뀌는 과정이 화면에 노출되지 않는다.
        yield return WaitForSceneDataReady(SceneDataReadyTimeout);

        yield return Fade(1f, 0f, fadeDuration);

        group.blocksRaycasts = false;
        transitioning = false;
    }

    // 세이브 복원이 지연되거나 실패해도 무한 대기하지 않도록 하는 안전 상한(초).
    private const float SceneDataReadyTimeout = 12f;

    // BaseCampManager가 있는 씬이면 통합 세이브 복원(IsUnifiedSaveLoaded)이 끝날 때까지 대기한다.
    // 없는 씬(타이틀 등)이면 즉시 통과.
    private IEnumerator WaitForSceneDataReady(float timeout)
    {
        BaseCampManager baseCamp = BaseCampManager.Instance;
        if (baseCamp == null)
        {
            yield break;
        }

        float deadline = Time.unscaledTime + Mathf.Max(0.1f, timeout);
        while (!baseCamp.IsUnifiedSaveLoaded && Time.unscaledTime < deadline)
        {
            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        const float maxStep = 0.05f; // 프레임 끊김(특히 씬 로드 직후 거대 deltaTime)에 페이드가 한 번에 끝나지 않도록 상한.
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, maxStep);
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }
}
