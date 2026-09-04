using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// 구글 ID 토큰을 받아 Firebase 인증으로 교환하고, 로그인 상태/UID를 관리한다.
/// 로그인 방식(C 웹 / B 네이티브)은 <see cref="IGoogleSignIn"/> 구현 교체로만 바꾼다.
/// ※ Firebase Auth Unity SDK가 import 되어 있어야 컴파일된다.
/// </summary>
[DisallowMultipleComponent]
public class FirebaseAuthManager : MonoBehaviour
{
    public static FirebaseAuthManager Instance { get; private set; }

    private FirebaseAuth auth;
    private IGoogleSignIn googleSignIn;

    public bool IsReady { get; private set; }
    public bool IsSignedIn => auth != null && auth.CurrentUser != null;
    public string Uid => auth != null && auth.CurrentUser != null ? auth.CurrentUser.UserId : null;

    /// <summary>현재 사용자가 익명(게스트) 계정인지. 게스트 표시/계정 연동 안내 등에 사용.</summary>
    public bool IsAnonymous => auth != null && auth.CurrentUser != null && auth.CurrentUser.IsAnonymous;

    /// <summary>표시 이름. 구글은 프로필 이름, 익명(게스트)은 "게스트-XXXX"(UID 끝 4자리). 환영 문구/플레이어 이름 등에 사용.</summary>
    public string DisplayName
    {
        get
        {
            if (auth == null || auth.CurrentUser == null)
            {
                return null;
            }

            if (auth.CurrentUser.IsAnonymous)
            {
                string uid = auth.CurrentUser.UserId;
                string suffix = !string.IsNullOrEmpty(uid) && uid.Length >= 4
                    ? uid.Substring(uid.Length - 4)
                    : uid;
                return $"게스트-{suffix}";
            }

            return auth.CurrentUser.DisplayName;
        }
    }

    /// <summary>준비 완료/로그인 상태 변화 시 호출.</summary>
    public event Action OnAuthStateChanged;

    /// <summary>플레이어에게 보여줄 접속/로그인 진행 단계.</summary>
    public enum ConnectionState
    {
        Connecting,    // Firebase 초기화/네트워크 확인 중
        Ready,         // 준비 완료, 로그인 대기
        SigningIn,     // 구글 로그인 진행 중
        SignedIn,      // 로그인 완료
        SignInFailed,  // 로그인 실패(취소 제외)
        ConnectFailed, // Firebase 초기화/네트워크 실패
    }

    /// <summary>현재 진행 단계. 변경 시 <see cref="OnAuthStateChanged"/>가 호출된다.</summary>
    public ConnectionState State { get; private set; } = ConnectionState.Connecting;

    private void SetState(ConnectionState state)
    {
        State = state;
        OnAuthStateChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 로그인 공급자: Android/iOS/에디터 모두 네이티브 구글 로그인으로 통일.
        googleSignIn = new NativeGoogleSignIn();
    }

    private async void Start()
    {
        // 전체를 감싸 초기화/무음 로그인 도중의 예외가 async void로 삼켜져
        // 상태가 Connecting("접속 중...")에 영구히 고착되는 것을 막는다.
        try
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError($"[Auth] Firebase 의존성 사용 불가: {status}");
                SetState(ConnectionState.ConnectFailed);
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            IsReady = true;

            // 1) 이전 Firebase 세션이 남아 있으면 그대로 자동 로그인.
            if (IsSignedIn)
            {
                SetState(ConnectionState.SignedIn);
                return;
            }

            // 2) 무음 자동 연결 시도. 네이티브 구글은 무음 미지원이라 버튼을 노출하고, 재방문자는 위의 Firebase 세션 복원으로 자동 로그인된다.
            await TrySilentSignIn();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] 초기화 실패: {e.Message}");
            SetState(ConnectionState.ConnectFailed);
        }
    }

    private async Task TrySilentSignIn()
    {
        string token = await googleSignIn.TrySilentSignInAsync();
        if (string.IsNullOrEmpty(token))
        {
            SetState(ConnectionState.Ready); // 무음 불가 → 수동 로그인 버튼 노출
            return;
        }

        try
        {
            Credential credential = googleSignIn.CreateCredential(token);
            await auth.SignInWithCredentialAsync(credential);
            SetState(ConnectionState.SignedIn);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auth] 무음 자동 로그인 실패: {e.Message}");
            SetState(ConnectionState.Ready);
        }
    }

    /// <summary>구글 로그인 → Firebase 자격증명 교환. 성공 시 true.</summary>
    public async Task<bool> SignInWithGoogleAsync()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Auth] 아직 Firebase 준비 전입니다.");
            return false;
        }

        SetState(ConnectionState.SigningIn);

        string token = await googleSignIn.SignInAsync();
        if (string.IsNullOrEmpty(token))
        {
            SetState(ConnectionState.Ready); // 사용자 취소 → 다시 대기 상태로
            return false;
        }

        try
        {
            // 공급자별 자격증명 생성(구글=GoogleAuthProvider, Play Games=PlayGamesAuthProvider).
            Credential credential = googleSignIn.CreateCredential(token);
            await auth.SignInWithCredentialAsync(credential);
            SetState(ConnectionState.SignedIn);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] Firebase 로그인 실패: {e.Message}");
            SetState(ConnectionState.SignInFailed);
            return false;
        }
    }

    /// <summary>익명(게스트) 로그인. 진짜 UID를 받아 Firestore/클라우드 세이브가 동작한다. 성공 시 true.</summary>
    public async Task<bool> SignInAnonymouslyAsync()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Auth] 아직 Firebase 준비 전입니다.");
            return false;
        }

        // 기존 세션(이전 구글/익명 로그인)을 게스트로 재사용하지 않도록 먼저 정리한다.
        // 이래야 게스트가 항상 '새 익명 계정'으로 처음부터 시작하고, 이전 계정 데이터가 딸려오지 않는다.
        if (IsSignedIn)
        {
            googleSignIn?.SignOut();
            auth.SignOut();
        }

        SetState(ConnectionState.SigningIn);
        try
        {
            await auth.SignInAnonymouslyAsync();
            SetState(ConnectionState.SignedIn);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] 익명 로그인 실패: {e.Message}");
            SetState(ConnectionState.SignInFailed);
            return false;
        }
    }

    public void SignOut()
    {
        googleSignIn?.SignOut();
        auth?.SignOut();
        // 이전 계정의 대기 업로드/세션 캐시를 정리해, 다음 계정의 클라우드 문서로 새는 것을 막는다.
        CloudSaveService.Instance.HandleAccountChanged();
        SetState(ConnectionState.Ready);
    }

    /// <summary>계정별 세이브 키. 로그인 시 baseKey_UID, 비로그인 시 baseKey 그대로.</summary>
    public string ScopedSaveKey(string baseKey)
    {
        return IsSignedIn ? $"{baseKey}_{Uid}" : baseKey;
    }
}
