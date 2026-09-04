using System;
using UnityEngine;

/// <summary>
/// 네이티브(Android/iOS)에서 UnitySendMessage로 호출하는 구글 로그인 결과 수신용.
/// UnitySendMessage는 Unity 메인스레드에서 실행되므로 그대로 Task로 넘겨도 안전하다.
/// GameObject 이름은 네이티브에 전달하는 이름과 반드시 일치해야 한다.
/// </summary>
[DisallowMultipleComponent]
public class GoogleSignInBridge : MonoBehaviour
{
    public Action<string> OnSuccess;
    public Action<string> OnFailure;

    // 네이티브: UnitySendMessage(name, "OnGoogleSignInSuccess", idToken)
    public void OnGoogleSignInSuccess(string idToken)
    {
        OnSuccess?.Invoke(idToken);
    }

    // 네이티브: UnitySendMessage(name, "OnGoogleSignInFailure", error)
    public void OnGoogleSignInFailure(string error)
    {
        Debug.LogWarning($"[GoogleSignIn] 네이티브 로그인 실패/취소: {error}");
        OnFailure?.Invoke(error);
    }
}
