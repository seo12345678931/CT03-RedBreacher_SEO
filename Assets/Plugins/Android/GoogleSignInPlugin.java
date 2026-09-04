package com.yourcompany.googlesignin;

import android.app.Activity;

import androidx.annotation.NonNull;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import com.unity3d.player.UnityPlayer;

import java.util.concurrent.Executors;

/**
 * Android Credential Manager로 "Sign in with Google"을 수행하고, ID 토큰을 Unity로 돌려준다.
 * serverClientId = Web 클라이언트 ID 로 요청하므로 idToken의 aud가 Web client → Firebase가 수락.
 * 결과는 UnityPlayer.UnitySendMessage(gameObject, ...)로 Unity 메인스레드에 전달.
 *
 * ※ UnitySendMessage는 반드시 Android UI(메인) 스레드에서 호출해야 Unity로 전달된다.
 *    Credential Manager 콜백은 백그라운드 executor에서 실행되므로, 결과 전달은 runOnUiThread로 메인스레드에서 한다.
 *    (이걸 누락하면 계정 선택 후 콜백이 유실돼 Unity가 무반응이 된다.)
 *
 * NativeGoogleSignIn.cs 의 AndroidJavaClass("com.yourcompany.googlesignin.GoogleSignInPlugin")와
 * 패키지/클래스명이 일치해야 한다(바꾸려면 양쪽 동시에).
 */
public class GoogleSignInPlugin {

    public static void signIn(final String gameObject, final String webClientId) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            UnityPlayer.UnitySendMessage(gameObject, "OnGoogleSignInFailure", "Android Activity is null");
            return;
        }

        final CredentialManager credentialManager = CredentialManager.create(activity);

        GetSignInWithGoogleOption option =
                new GetSignInWithGoogleOption.Builder(webClientId).build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(option)
                .build();

        credentialManager.getCredentialAsync(
                activity,
                request,
                null,
                Executors.newSingleThreadExecutor(),
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse response) {
                        try {
                            Credential credential = response.getCredential();
                            if (credential instanceof CustomCredential
                                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                GoogleIdTokenCredential cred =
                                        GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());
                                sendToUnity(activity, gameObject, "OnGoogleSignInSuccess", cred.getIdToken());
                            } else {
                                sendToUnity(activity, gameObject, "OnGoogleSignInFailure",
                                        "Unexpected credential type: " + credential.getType());
                            }
                        } catch (Exception e) {
                            sendToUnity(activity, gameObject, "OnGoogleSignInFailure", String.valueOf(e.getMessage()));
                        }
                    }

                    @Override
                    public void onError(@NonNull GetCredentialException e) {
                        sendToUnity(activity, gameObject, "OnGoogleSignInFailure", String.valueOf(e.getMessage()));
                    }
                });
    }

    // 결과를 항상 Android UI 스레드에서 Unity로 전달한다(UnitySendMessage 스레드 제약).
    private static void sendToUnity(final Activity activity, final String gameObject,
                                    final String method, final String payload) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                UnityPlayer.UnitySendMessage(gameObject, method, payload != null ? payload : "");
            }
        });
    }

    public static void signOut() {
        // 필요 시 CredentialManager.clearCredentialState(...) 로 자동선택 상태 초기화.
    }
}
