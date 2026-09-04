// iOS 네이티브 구글 로그인 브리지 (GoogleSignIn SDK 7.x 기준).
// NativeGoogleSignIn.cs 의 _GoogleSignIn(gameObjectName, webClientId) 에서 호출.
// 결과는 UnitySendMessage 로 GoogleSignInBridge(메인스레드)에 전달.
//
// 필요 설정:
//  - GoogleSignIn iOS SDK (EDM4U의 *Dependencies.xml 에 CocoaPods 'GoogleSignIn' 추가)
//  - GoogleService-Info.plist 포함 + Info.plist URL Types 에 REVERSED_CLIENT_ID (Firebase iOS 설정 시 보통 완료)
//  - serverClientID = Web 클라이언트 ID → idToken 의 aud 가 Web client 가 되어 Firebase 가 수락

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <GoogleSignIn/GoogleSignIn.h>

extern "C" UIViewController* UnityGetGLViewController();
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static void SendToUnity(NSString* obj, const char* method, NSString* msg) {
    UnitySendMessage([obj UTF8String], method, msg ? [msg UTF8String] : "");
}

extern "C" {

void _GoogleSignIn(const char* gameObjectName, const char* webClientId) {
    NSString* objName = [NSString stringWithUTF8String:gameObjectName];
    NSString* serverClientId = [NSString stringWithUTF8String:webClientId];

    // iOS 클라이언트 ID는 GoogleService-Info.plist 에서 읽는다.
    NSString* plistPath = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
    NSDictionary* plist = plistPath ? [NSDictionary dictionaryWithContentsOfFile:plistPath] : nil;
    NSString* iosClientId = plist[@"CLIENT_ID"];
    if (iosClientId.length == 0) {
        SendToUnity(objName, "OnGoogleSignInFailure", @"GoogleService-Info.plist CLIENT_ID 누락");
        return;
    }

    GIDSignIn.sharedInstance.configuration =
        [[GIDConfiguration alloc] initWithClientID:iosClientId serverClientID:serverClientId];

    UIViewController* presenter = UnityGetGLViewController();
    [GIDSignIn.sharedInstance signInWithPresentingViewController:presenter
        completion:^(GIDSignInResult* _Nullable result, NSError* _Nullable error) {
            if (error != nil || result == nil) {
                SendToUnity(objName, "OnGoogleSignInFailure",
                            error ? error.localizedDescription : @"cancelled");
                return;
            }
            NSString* idToken = result.user.idToken.tokenString;
            if (idToken.length == 0) {
                SendToUnity(objName, "OnGoogleSignInFailure", @"idToken 없음");
                return;
            }
            SendToUnity(objName, "OnGoogleSignInSuccess", idToken);
        }];
}

void _GoogleSignOut() {
    [GIDSignIn.sharedInstance signOut];
}

}
