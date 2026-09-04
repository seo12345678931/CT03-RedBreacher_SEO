using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// 네이티브 구글 로그인으로 Google ID 토큰을 받는다.
///  - Android: Credential Manager(Sign in with Google) 네이티브 플러그인 호출
///  - iOS: GoogleSignIn SDK 네이티브 브리지 호출
/// idToken은 serverClientId = Web 클라이언트 ID로 요청하므로 aud가 Web client가 되어 Firebase가 받아준다.
/// 네이티브 결과는 UnitySendMessage로 <see cref="GoogleSignInBridge"/>(메인스레드)가 받는다.
/// </summary>
public class NativeGoogleSignIn : IGoogleSignIn
{
    // google-services.json의 oauth_client(client_type:3) = Web 클라이언트 ID
    private const string WebClientId = "728496036155-ndkt9de5n7p2f6blvia6p8svn2qtbp9d.apps.googleusercontent.com";
    private const string BridgeObjectName = "GoogleSignInBridge";

    private GoogleSignInBridge bridge;
    private TaskCompletionSource<string> pending;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _GoogleSignIn(string gameObjectName, string webClientId);
    [DllImport("__Internal")] private static extern void _GoogleSignOut();
#endif

    public Task<string> SignInAsync()
    {
        if (pending != null && !pending.Task.IsCompleted)
        {
            return pending.Task;
        }

        pending = new TaskCompletionSource<string>();
        Task<string> signInTask = pending.Task;
        EnsureBridge();

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null)
                {
                    Debug.LogError("[NativeGoogleSignIn] Android Activity를 찾을 수 없습니다.");
                    Complete(null);
                    return signInTask;
                }

                Debug.Log("[NativeGoogleSignIn] Android 구글 로그인 UI 호출");
                // Credential Manager UI는 Android Activity UI thread에서 실행한다.
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using (var plugin = new AndroidJavaClass("com.yourcompany.googlesignin.GoogleSignInPlugin"))
                        {
                            plugin.CallStatic("signIn", BridgeObjectName, WebClientId);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[NativeGoogleSignIn] Android 호출 실패: {e}");
                        Complete(null);
                    }
                }));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NativeGoogleSignIn] Android 호출 실패: {e}");
            Complete(null);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        try
        {
            _GoogleSignIn(BridgeObjectName, WebClientId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NativeGoogleSignIn] iOS 호출 실패: {e}");
            Complete(null);
        }
#else
        Debug.LogWarning("[NativeGoogleSignIn] 에디터에서는 동작하지 않습니다. 실기기에서 테스트하세요.");
        Complete(null);
#endif
        return signInTask;
    }

    public Credential CreateCredential(string idToken)
    {
        return GoogleAuthProvider.GetCredential(idToken, null);
    }

    public Task<string> TrySilentSignInAsync()
    {
        // 네이티브 구글 브리지는 무음 로그인을 지원하지 않는다(첫 로그인은 버튼).
        // 재실행 시 자동 로그인은 Firebase 세션 복원으로 처리된다.
        return Task.FromResult<string>(null);
    }

    public void SignOut()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var plugin = new AndroidJavaClass("com.yourcompany.googlesignin.GoogleSignInPlugin"))
            {
                plugin.CallStatic("signOut");
            }
        }
        catch { /* 무시 */ }
#elif UNITY_IOS && !UNITY_EDITOR
        try { _GoogleSignOut(); } catch { /* 무시 */ }
#endif
    }

    private void EnsureBridge()
    {
        if (bridge != null)
        {
            return;
        }

        GameObject host = new GameObject(BridgeObjectName);
        UnityEngine.Object.DontDestroyOnLoad(host);
        bridge = host.AddComponent<GoogleSignInBridge>();
        bridge.OnSuccess = token => Complete(token);
        bridge.OnFailure = _ => Complete(null);
    }

    private void Complete(string idToken)
    {
        TaskCompletionSource<string> tcs = pending;
        pending = null;
        tcs?.TrySetResult(string.IsNullOrEmpty(idToken) ? null : idToken);
    }
}
