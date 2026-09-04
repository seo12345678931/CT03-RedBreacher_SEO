using System.Threading.Tasks;
using Firebase.Auth;

/// <summary>
/// Firebase 인증에 넘길 자격증명을 만드는 로그인 공급자(구글 로그인 / Play Games 등).
/// 구현 교체만으로 <see cref="FirebaseAuthManager"/>와 게임 코드는 그대로 둔다.
/// </summary>
public interface IGoogleSignIn
{
    /// <summary>로그인 수행 후 토큰을 반환(구글 idToken 또는 Play Games server auth code). 취소/실패 시 null.</summary>
    Task<string> SignInAsync();

    /// <summary>UI 없이 무음 로그인 시도. 가능하면 토큰, 아니면 null(예: Android Play Games 시작 시 자동 연결).</summary>
    Task<string> TrySilentSignInAsync();

    /// <summary>토큰으로 Firebase 자격증명을 만든다(공급자별 Provider 사용).</summary>
    Credential CreateCredential(string token);

    void SignOut();
}
