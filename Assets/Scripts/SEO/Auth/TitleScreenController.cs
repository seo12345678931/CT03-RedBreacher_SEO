using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 흐름: (로그인 버튼) → 구글 로그인 → 성공 시 로그인 버튼 숨김 + Tap to Start/로그아웃 버튼 활성화.
/// Tap to Start 그룹에는 <see cref="TapToStartGame"/>가 있어, 활성화된 뒤 탭하면 게임 씬으로 전환된다.
/// 이전 세션에서 이미 로그인돼 있으면(자동 로그인) 바로 Tap to Start 상태로 넘어가고 환영 토스트를 띄운다.
/// 로그아웃 버튼을 누르면 다시 로그인 버튼이 보이는 초기 상태로 돌아간다.
/// </summary>
[DisallowMultipleComponent]
public class TitleScreenController : MonoBehaviour
{
    [Header("Login")]
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private GameObject loginGroup;       // 숨길 로그인 묶음(미지정 시 버튼 오브젝트 사용)

    [Header("Tap to Start")]
    [SerializeField] private GameObject tapToStartGroup;   // 로그인 후 활성화할 'tap' 오브젝트

    [Header("Sign Out")]
    [SerializeField] private Button signOutButton;         // 로그인 상태에서만 보이는 로그아웃 버튼
    [SerializeField] private GameObject signOutGroup;      // 숨길 로그아웃 묶음(미지정 시 버튼 오브젝트 사용)

    [Header("Guest")]
    [Tooltip("게스트(익명) 로그인 버튼. 구글 로그인과 동일하게 로그인 후 이름 표시 + Tap to Start 노출. (Button OnClick에 EnterAsGuest를 직접 연결해도 됨)")]
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private GameObject guestGroup;       // 숨길 게스트 묶음(미지정 시 버튼 오브젝트 사용)

    [Header("Welcome Toast")]
    [Tooltip("토스트용 한글 폰트. 기본 TMP 폰트엔 한글 폴백이 없으므로 지정 권장.")]
    [SerializeField] private TMP_FontAsset toastFont;
    [Tooltip("{0}에 표시 이름이 들어간다.")]
    [SerializeField] private string welcomeFormat = "{0}님, 환영합니다!";
    [SerializeField] private string welcomeFallback = "환영합니다!";
    [Tooltip("토스트 기준 위치(0~1). (0.5,0) 하단중앙 / (0.5,0.5) 정중앙 / (0.5,1) 상단중앙.")]
    [SerializeField] private Vector2 toastAnchor = new Vector2(0.5f, 0f);
    [Tooltip("기준 위치에서의 오프셋(1080x1920 기준 픽셀).")]
    [SerializeField] private Vector2 toastPosition = new Vector2(0f, 260f);
    [SerializeField] private float toastDuration = 2f;

    [Header("State Text")]
    [Tooltip("접속/로그인 진행 상태를 보여줄 텍스트(선택).")]
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private string connectingText = "접속 중...";
    [SerializeField] private string signingInText = "로그인 중...";
    [SerializeField] private string readyText = "";                       // 로그인 대기(필요 시 '로그인이 필요합니다')
    [SerializeField] private string signedInText = "";                    // 환영은 토스트가 담당
    [SerializeField] private string signInFailedText = "로그인 실패. 다시 시도해 주세요.";
    [SerializeField] private string connectFailedText = "접속 실패. 네트워크를 확인해 주세요.";

    private bool busy;
    private bool welcomeShown;
    private bool cloudPrewarmStarted;

    private void Awake()
    {
        // 초기: 로그인/게스트만 보이고 Tap to Start/로그아웃은 숨김.
        SetActiveSafe(tapToStartGroup, false);
        SetActiveSafe(SignOutVisual(), false);
        SetActiveSafe(LoginVisual(), true);
        SetActiveSafe(GuestVisual(), true);

        // 첫 프레임부터 '접속 중...'이 보이도록 초기화.
        if (stateText != null)
        {
            stateText.text = connectingText;
        }
    }

    private void Start()
    {
        if (googleLoginButton != null)
        {
            googleLoginButton.onClick.AddListener(OnLoginClicked);
            googleLoginButton.interactable = false;
        }

        if (signOutButton != null)
        {
            signOutButton.onClick.AddListener(OnSignOutClicked);
        }

        if (guestLoginButton != null)
        {
            // 익명 로그인도 Firebase 준비가 필요하므로 준비 전엔 비활성(HandleAuthState에서 활성화).
            guestLoginButton.onClick.AddListener(EnterAsGuest);
            guestLoginButton.interactable = false;
        }

        // FirebaseAuthManager.Awake는 이 시점 이전에 실행됨(같은 씬). 준비/상태 변화를 구독.
        FirebaseAuthManager manager = FirebaseAuthManager.Instance;
        if (manager != null)
        {
            manager.OnAuthStateChanged += HandleAuthState;
            HandleAuthState(); // 자동 로그인 즉시 반영(준비 전이면 이후 콜백으로)
        }
        else
        {
            Debug.LogWarning("[Title] 씬에 FirebaseAuthManager가 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (googleLoginButton != null)
        {
            googleLoginButton.onClick.RemoveListener(OnLoginClicked);
        }

        if (signOutButton != null)
        {
            signOutButton.onClick.RemoveListener(OnSignOutClicked);
        }

        if (guestLoginButton != null)
        {
            guestLoginButton.onClick.RemoveListener(EnterAsGuest);
        }

        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged -= HandleAuthState;
        }
    }

    private void HandleAuthState()
    {
        UpdateStateText();

        FirebaseAuthManager manager = FirebaseAuthManager.Instance;
        bool signedIn = manager != null && manager.IsSignedIn;

        if (signedIn)
        {
            ShowSignedIn();

            // 느린 클라우드 다운로드를 타이틀에서 미리 시작해, Myth 진입 시 대기(검은 화면)를 최소화한다.
            TryPrewarmCloudSave();

            // 로그인 성공(자동/버튼 무관) 시 1회 환영 토스트.
            if (!welcomeShown)
            {
                welcomeShown = true;
                ShowWelcomeToast(manager.DisplayName);
            }
            return;
        }

        // 비로그인: 로그인 버튼이 보이는 초기 상태로 복귀.
        welcomeShown = false; // 다음 로그인 때 다시 환영하도록 리셋.
        cloudPrewarmStarted = false; // 재로그인(다른 계정) 시 다시 프리워밍.
        ShowSignedOut();
        if (!busy)
        {
            // Firebase 준비 전 터치는 무반응처럼 보이므로, 준비된 뒤에만 버튼을 활성화한다.
            bool ready = manager != null && manager.IsReady;
            if (googleLoginButton != null)
            {
                googleLoginButton.interactable = ready;
            }

            if (guestLoginButton != null)
            {
                guestLoginButton.interactable = ready;
            }
        }
    }

    private async void OnLoginClicked()
    {
        if (busy)
        {
            return;
        }

        FirebaseAuthManager manager = FirebaseAuthManager.Instance;
        if (manager == null || !manager.IsReady)
        {
            Debug.LogWarning("[Title] 인증이 아직 준비되지 않았습니다.");
            HandleAuthState();
            return;
        }

        Debug.Log("[Title] 구글 로그인 버튼 클릭");
        busy = true;
        googleLoginButton.interactable = false;

        bool success = await manager.SignInWithGoogleAsync();

        busy = false;
        if (success)
        {
            // 로그인 성공 UI 반영(환영 토스트는 HandleAuthState에서 1회 처리됨).
            ShowSignedIn();
        }
        else
        {
            googleLoginButton.interactable = true; // 실패/취소 시 재시도 허용
        }
    }

    /// <summary>
    /// Firebase 익명(게스트) 로그인. 구글 로그인과 동일하게 성공 시 이름 표시 + Tap to Start를 노출한다(바로 진입하지 않음).
    /// 익명도 진짜 UID라 Firestore/클라우드 세이브가 동작하고, IsSignedIn=true가 되어 TapToStartGame 게이트도 통과한다.
    /// Button OnClick에 직접 연결하거나 guestLoginButton에 할당하면 된다.
    /// </summary>
    public async void EnterAsGuest()
    {
        if (busy)
        {
            return;
        }

        FirebaseAuthManager manager = FirebaseAuthManager.Instance;
        if (manager == null || !manager.IsReady)
        {
            Debug.LogWarning("[Title] 인증이 아직 준비되지 않았습니다.");
            HandleAuthState();
            return;
        }

        Debug.Log("[Title] 게스트(익명) 로그인 시도");
        busy = true;
        if (guestLoginButton != null)
        {
            guestLoginButton.interactable = false;
        }

        if (googleLoginButton != null)
        {
            googleLoginButton.interactable = false;
        }

        bool success = await manager.SignInAnonymouslyAsync();
        busy = false;

        if (success)
        {
            // 구글 로그인과 동일: 이름 표시(환영 토스트) + Tap to Start 노출. 환영 토스트는 HandleAuthState에서 1회 처리.
            ShowSignedIn();
        }
        else
        {
            HandleAuthState();
        }
    }

    // 로그인 직후, 계정 세이브의 클라우드 다운로드를 미리 시작한다(느린 네트워크 대기를 타이틀로 이동).
    // Myth의 BaseCampManager가 같은 키로 EnsureSyncedAsync를 호출할 때, 이미 동기화됐으면 즉시 통과한다.
    private void TryPrewarmCloudSave()
    {
        if (cloudPrewarmStarted)
        {
            return;
        }

        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        if (auth == null || !auth.IsSignedIn)
        {
            return;
        }

        cloudPrewarmStarted = true;
        string key = auth.ScopedSaveKey(BaseCampManager.DefaultUnifiedSaveKey);
        _ = CloudSaveService.Instance.EnsureSyncedAsync(key);
        Debug.Log("[Title] 계정 세이브 프리워밍 시작");
    }

    private void OnSignOutClicked()
    {
        if (busy)
        {
            return;
        }

        // SignOut → OnAuthStateChanged → HandleAuthState에서 로그인 UI로 복귀.
        FirebaseAuthManager.Instance?.SignOut();
    }

    private void ShowSignedIn()
    {
        SetActiveSafe(LoginVisual(), false);
        SetActiveSafe(GuestVisual(), false);
        SetActiveSafe(tapToStartGroup, true);
        SetActiveSafe(SignOutVisual(), true);
    }

    private void ShowSignedOut()
    {
        SetActiveSafe(tapToStartGroup, false);
        SetActiveSafe(SignOutVisual(), false);
        SetActiveSafe(LoginVisual(), true);
        SetActiveSafe(GuestVisual(), true);
    }

    private void UpdateStateText()
    {
        if (stateText == null)
        {
            return;
        }

        FirebaseAuthManager manager = FirebaseAuthManager.Instance;
        if (manager == null)
        {
            stateText.text = connectingText;
            return;
        }

        switch (manager.State)
        {
            case FirebaseAuthManager.ConnectionState.Connecting:
                stateText.text = connectingText;
                break;
            case FirebaseAuthManager.ConnectionState.SigningIn:
                stateText.text = signingInText;
                break;
            case FirebaseAuthManager.ConnectionState.Ready:
                stateText.text = readyText;
                break;
            case FirebaseAuthManager.ConnectionState.SignedIn:
                stateText.text = signedInText;
                break;
            case FirebaseAuthManager.ConnectionState.SignInFailed:
                stateText.text = signInFailedText;
                break;
            case FirebaseAuthManager.ConnectionState.ConnectFailed:
                stateText.text = connectFailedText;
                break;
        }
    }

    private void ShowWelcomeToast(string displayName)
    {
        string message = string.IsNullOrEmpty(displayName)
            ? welcomeFallback
            : string.Format(welcomeFormat, displayName);
        ToastMessage.Show(message, toastFont, toastDuration, toastPosition, toastAnchor);
    }

    private GameObject LoginVisual()
    {
        if (loginGroup != null)
        {
            return loginGroup;
        }

        return googleLoginButton != null ? googleLoginButton.gameObject : null;
    }

    private GameObject GuestVisual()
    {
        if (guestGroup != null)
        {
            return guestGroup;
        }

        return guestLoginButton != null ? guestLoginButton.gameObject : null;
    }

    private GameObject SignOutVisual()
    {
        if (signOutGroup != null)
        {
            return signOutGroup;
        }

        return signOutButton != null ? signOutButton.gameObject : null;
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
