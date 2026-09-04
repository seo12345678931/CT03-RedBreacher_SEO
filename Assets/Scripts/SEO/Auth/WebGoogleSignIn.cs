using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 웹 기반 Google OAuth(시스템 브라우저 + 커스텀 URL 스킴 리다이렉트, Auth Code + PKCE)로 ID 토큰을 받는다.
/// 네이티브 플러그인 없이 iOS/Android 공용. 구글의 GoogleSignIn SDK deprecation 영향 없음.
///
/// 필요한 설정(★):
///  - Google Cloud 콘솔에서 OAuth 클라이언트(iOS/Android 또는 "Desktop") 발급 → ClientId
///  - 리다이렉트로 쓸 커스텀 URL 스킴을 양 플랫폼에 등록(Android: intent-filter, iOS: CFBundleURLSchemes)
///    그리고 OAuth 클라이언트의 승인된 리다이렉트에 등록
///  - 받은 id_token은 Firebase에 그대로 넘어가므로, Firebase 콘솔 Google 공급자의 클라이언트와 동일 프로젝트여야 함
/// 실제 기기에서 리다이렉트 복귀 동작을 반드시 테스트할 것.
/// </summary>
public class WebGoogleSignIn : IGoogleSignIn
{
    // ★ 플랫폼별로 콘솔 발급값 교체. (커스텀 스킴 리다이렉트는 iOS/Android 클라이언트에서만 가능)
    // ⚠️ Firebase는 idToken의 aud가 보통 'Web 클라이언트 ID'이길 기대한다. 아래 플랫폼 클라이언트로 받은
    //    idToken이 SignInWithCredential에서 'audience not authorized'로 거부되면, 웹 플로우로는 한계이며
    //    네이티브 방식(B)으로 전환해야 한다. 반드시 실기기에서 확인할 것.
#if UNITY_IOS
    // GoogleService-Info.plist: CLIENT_ID / REVERSED_CLIENT_ID
    private const string ClientId = "IOS_CLIENT_ID.apps.googleusercontent.com";
    private const string RedirectUri = "com.googleusercontent.apps.IOS_CLIENT_ID:/oauth2redirect"; // = REVERSED_CLIENT_ID
#elif UNITY_ANDROID
    // google-services.json: oauth_client(client_type:1=Android). 커스텀 스킴은 manifest에 등록 필요.
    private const string ClientId = "ANDROID_CLIENT_ID.apps.googleusercontent.com";
    private const string RedirectUri = "com.yourcompany.yourgame:/oauth2redirect";
#else
    // 에디터/데스크톱 테스트용(Web 또는 Desktop 클라이언트 + 루프백)
    private const string ClientId = "WEB_OR_DESKTOP_CLIENT_ID.apps.googleusercontent.com";
    private const string RedirectUri = "http://localhost";
#endif
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private TaskCompletionSource<string> pending;
    private string codeVerifier;
    private string state;

    public Task<string> SignInAsync()
    {
        if (pending != null && !pending.Task.IsCompleted)
        {
            return pending.Task; // 중복 방지
        }

        pending = new TaskCompletionSource<string>();

        codeVerifier = RandomUrlSafe(64);
        string codeChallenge = Base64UrlSha256(codeVerifier);
        state = RandomUrlSafe(16);

        string url = AuthEndpoint
            + "?client_id=" + UnityWebRequest.EscapeURL(ClientId)
            + "&redirect_uri=" + UnityWebRequest.EscapeURL(RedirectUri)
            + "&response_type=code"
            + "&scope=" + UnityWebRequest.EscapeURL("openid email profile")
            + "&code_challenge=" + codeChallenge
            + "&code_challenge_method=S256"
            + "&state=" + state;

        Application.deepLinkActivated += OnDeepLink;
        Application.OpenURL(url);
        return pending.Task;
    }

    public Credential CreateCredential(string idToken)
    {
        return GoogleAuthProvider.GetCredential(idToken, null);
    }

    public Task<string> TrySilentSignInAsync()
    {
        // 웹 OAuth 플로우는 무음 로그인을 지원하지 않는다(브라우저 리다이렉트 필요).
        return Task.FromResult<string>(null);
    }

    private void OnDeepLink(string link)
    {
        Application.deepLinkActivated -= OnDeepLink;

        Dictionary<string, string> query = ParseQuery(link);
        if (!query.TryGetValue("state", out string returnedState) || returnedState != state)
        {
            Complete(null);
            return;
        }

        if (!query.TryGetValue("code", out string code) || string.IsNullOrEmpty(code))
        {
            Complete(null); // 사용자 취소 또는 error
            return;
        }

        ExchangeCodeForIdToken(code);
    }

    private async void ExchangeCodeForIdToken(string code)
    {
        WWWForm form = new WWWForm();
        form.AddField("client_id", ClientId);
        form.AddField("code", code);
        form.AddField("code_verifier", codeVerifier);
        form.AddField("grant_type", "authorization_code");
        form.AddField("redirect_uri", RedirectUri);

        using (UnityWebRequest request = UnityWebRequest.Post(TokenEndpoint, form))
        {
            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WebGoogleSignIn] 토큰 교환 실패: {request.error} / {request.downloadHandler.text}");
                Complete(null);
                return;
            }

            string idToken = ExtractJsonString(request.downloadHandler.text, "id_token");
            Complete(idToken);
        }
    }

    public void SignOut()
    {
        // 웹 플로우는 별도 세션 캐시가 없다(Firebase 쪽 SignOut으로 충분). 필요 시 토큰 폐기 호출 추가 가능.
    }

    private void Complete(string idToken)
    {
        TaskCompletionSource<string> tcs = pending;
        pending = null;
        tcs?.TrySetResult(idToken);
    }

    // ---- helpers ----

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var result = new Dictionary<string, string>();
        int q = url.IndexOf('?');
        if (q < 0)
        {
            return result;
        }

        foreach (string pair in url.Substring(q + 1).Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq > 0)
            {
                result[pair.Substring(0, eq)] = UnityWebRequest.UnEscapeURL(pair.Substring(eq + 1));
            }
        }

        return result;
    }

    private static string RandomUrlSafe(int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Base64Url(bytes);
    }

    private static string Base64UrlSha256(string input)
    {
        using (SHA256 sha = SHA256.Create())
        {
            return Base64Url(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // 가벼운 추출(의존성 없이). 견고함이 필요하면 JsonUtility 래퍼로 교체 권장.
    private static string ExtractJsonString(string json, string key)
    {
        string token = "\"" + key + "\"";
        int i = json.IndexOf(token, StringComparison.Ordinal);
        if (i < 0)
        {
            return null;
        }

        i = json.IndexOf('"', json.IndexOf(':', i + token.Length) + 1);
        int end = json.IndexOf('"', i + 1);
        return i >= 0 && end > i ? json.Substring(i + 1, end - i - 1) : null;
    }
}
