using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면에서 화면 아무 곳이나 탭/클릭하면 게임 씬을 로드한다.
/// EventSystem 없이도 동작하도록 신형 Input System의 Pointer(마우스/터치 공용)를 직접 폴링한다.
/// </summary>
public class TapToStartGame : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Myth";
    [SerializeField] private float startDelay = 0.1f;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Gate")]
    [Tooltip("켜면 Firebase 로그인 완료 전에는 탭을 무시한다.")]
    [SerializeField] private bool requireSignIn = true;

    [Header("Optional - 'Tap to Start' 깜빡임")]
    [SerializeField] private TMP_Text tapText;
    [SerializeField] private float blinkSpeed = 2f;
    [SerializeField] private AudioSource tapToStartSound;
    
    [Header("GameQuit Popup(안드로이드 한정)")]
    [SerializeField] private GameObject gameQuitPopup;

    private bool starting;
    private bool inputLocked;

    /// <summary>로고 인트로 등에서 탭 입력을 잠그고 풀 때 사용한다.</summary>
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
    }

    private void Update()
    {
        BlinkTapText();

        if (starting || inputLocked)
        {
            return;
        }

        if (WasTapped())
        {
            // 버튼(로그아웃 등) 위 탭만 게임 시작에서 제외한다(배경 이미지 등은 무시).
            if (IsPointerOverButton())
            {
                return;
            }

            // 로그인 전이면 탭 무시(그룹이 활성 상태여도 게임으로 안 넘어감).
            if (requireSignIn && !(FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsSignedIn))
            {
                return;
            }

            starting = true;
            if (startDelay > 0f)
            {
                Invoke(nameof(LoadGameScene), startDelay);
            }
            else
            {
                LoadGameScene();
            }
        }
        
        if (gameQuitPopup != null)
        {
            if (WasGameQuitPopupRequested() && !gameQuitPopup.activeSelf)
            {
                gameQuitPopup.SetActive(true);
            }
        }
    }

    private void LoadGameScene()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogWarning("[TapToStart] gameSceneName이 비어 있습니다.");
            return;
        }

        // 페이드 아웃 → 비동기 로드 → 페이드 인으로 전환.
        PlayTapToStartSound();
        ScreenFader.Instance.LoadScene(gameSceneName, fadeDuration);
    }

    private void BlinkTapText()
    {
        if (tapText == null)
        {
            return;
        }

        Color color = tapText.color;
        color.a = Mathf.Lerp(0.35f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI));
        tapText.color = color;
    }

    private static bool WasTapped()
    {
        // Pointer.current는 마우스/터치/펜을 모두 포괄한다(가장 최근 사용 포인터).
        Pointer pointer = Pointer.current;
        return pointer != null && pointer.press.wasPressedThisFrame;
    }

    private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private static bool IsPointerOverButton()
    {
        // 배경/그라데이션 같은 장식 이미지도 raycast 타겟이라 IsPointerOverGameObject()는 화면 전체를 'UI 위'로 본다.
        // 따라서 직접 raycast해서 '실제로 누를 수 있는 Selectable(버튼 등)' 위인지로 판정한다.
        EventSystem eventSystem = EventSystem.current;
        Pointer pointer = Pointer.current;
        if (eventSystem == null || pointer == null)
        {
            return false;
        }

        PointerEventData data = new PointerEventData(eventSystem)
        {
            position = pointer.position.ReadValue(),
        };

        raycastResults.Clear();
        eventSystem.RaycastAll(data, raycastResults);
        foreach (RaycastResult result in raycastResults)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            Selectable selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }
    
    // PC버전(에디터)의 ESC키나 안드로이드 버전의 백버튼을 누르면 게임종료 팝업창을 활성화
    private bool WasGameQuitPopupRequested()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    public void GameQuit()
    {
        Application.Quit();
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    
    private void PlayTapToStartSound()
    {
        if (tapToStartSound == null)
        {
            Debug.LogWarning($"{nameof(TapToStartGame)}: tapToStartSound가 연결되지 않았습니다.", this);
            return;
        }

        tapToStartSound.Play();
    }
}
