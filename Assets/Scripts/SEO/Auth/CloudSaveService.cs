using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Jinyou 통합 세이브(PlayerPrefs의 단일 JSON 블롭)를 Firestore에 계정(UID)별로 동기화한다.
/// 설계:
/// - 로컬 우선(Local-first): PlayerPrefs는 그대로 두고, 체크포인트마다 클라우드로 푸시한다(디바운스).
/// - 로그인 시 풀(pull): 클라우드/로컬을 lastSavedUnixTime으로 비교해 더 최신본을 채택한다(자동 충돌 해결).
/// - 문서 경로: users/{uid} (Phase 1 — Jinyou 통합 세이브 한정).
/// 비로그인/오프라인/Firestore 미준비 시에는 조용히 no-op 하고 로컬 저장만 동작한다.
/// </summary>
[DisallowMultipleComponent]
public class CloudSaveService : MonoBehaviour
{
    private const string UsersCollection = "users";
    private const string SaveField = "save";
    private const string TimestampField = "lastSaved";
    private const string VersionField = "version";
    private const string UpdatedAtField = "updatedAt";
    private const float PushDebounceSeconds = 3f;

    private static CloudSaveService instance;

    /// <summary>지연 생성되는 싱글턴. 씬에 배치하지 않아도 첫 접근 시 자동 생성된다.</summary>
    public static CloudSaveService Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("[CloudSaveService]");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<CloudSaveService>();
            }

            return instance;
        }
    }

    private FirebaseFirestore firestore;
    private readonly Dictionary<string, Task> syncing = new Dictionary<string, Task>();
    // 이번 세션에 이미 클라우드와 맞춘 키. 타이틀 프리워밍 후 같은 키를 재다운로드하지 않게 한다.
    private readonly HashSet<string> syncedKeys = new HashSet<string>();
    private readonly HashSet<string> pendingPushKeys = new HashSet<string>();
    private float nextPushTime;
    private bool pushing;
    // 종료 지연 플러시가 진행 중인 업로드까지 기다릴 수 있도록 마지막 푸시 작업을 추적한다.
    private Task lastPushTask = Task.CompletedTask;

    [Serializable]
    private class SaveMeta
    {
        public long lastSavedUnixTime;
        public int version;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (pushing || pendingPushKeys.Count == 0)
        {
            return;
        }

        if (Time.unscaledTime >= nextPushTime)
        {
            FlushNow();
        }
    }

    // ───────── 공개 API ─────────

    /// <summary>클라우드와 로컬을 비교해 최신본을 로컬 PlayerPrefs[key]에 맞춘다(필요 시 클라우드로 업로드). 같은 키 동시 호출은 합쳐진다.</summary>
    public Task EnsureSyncedAsync(string playerPrefsKey)
    {
        if (string.IsNullOrEmpty(playerPrefsKey))
        {
            return Task.CompletedTask;
        }

        if (syncing.TryGetValue(playerPrefsKey, out Task running) && !running.IsCompleted)
        {
            return running;
        }

        // 이번 세션에 이미 동기화한 키는 재다운로드하지 않는다(프리워밍 → 씬 로드 중복 방지).
        if (syncedKeys.Contains(playerPrefsKey))
        {
            return Task.CompletedTask;
        }

        Task task = SyncAsync(playerPrefsKey);
        syncing[playerPrefsKey] = task;
        return task;
    }

    /// <summary>저장 직후 호출. 디바운스되어 잠시 뒤 클라우드로 업로드된다.</summary>
    public void RequestPush(string playerPrefsKey)
    {
        if (string.IsNullOrEmpty(playerPrefsKey) || !IsReady())
        {
            return;
        }

        pendingPushKeys.Add(playerPrefsKey);
        nextPushTime = Time.unscaledTime + PushDebounceSeconds;
    }

    /// <summary>일시정지/종료 등 Update가 멈추는 상황에서 즉시 업로드한다(대기 중인 키 전부).</summary>
    public void FlushNow()
    {
        if (pushing || pendingPushKeys.Count == 0 || !IsReady())
        {
            return;
        }

        List<string> keys = new List<string>(pendingPushKeys);
        pendingPushKeys.Clear();
        lastPushTask = PushManyAsync(keys);
    }

    /// <summary>대기/진행 중인 업로드가 있는지(종료 지연 플러시 판단용).</summary>
    public bool HasPendingUploads => pendingPushKeys.Count > 0 || pushing;

    /// <summary>대기 중인 키를 즉시 업로드하고, 진행 중인 업로드까지 포함해 완료를 기다릴 수 있는 Task를 돌려준다.</summary>
    public Task FlushNowAsync()
    {
        if (IsReady() && pendingPushKeys.Count > 0 && !pushing)
        {
            List<string> keys = new List<string>(pendingPushKeys);
            pendingPushKeys.Clear();
            lastPushTask = PushManyAsync(keys);
        }

        return lastPushTask;
    }

    /// <summary>
    /// 로그아웃/계정 전환 시 이전 계정의 대기 업로드와 세션 캐시를 정리한다.
    /// 대기 키를 남겨두면 푸시 시점의 Doc()이 '현재 UID' 문서를 가리키므로,
    /// 이전 계정의 데이터가 다음 계정의 클라우드 문서로 업로드될 수 있다.
    /// </summary>
    public void HandleAccountChanged()
    {
        pendingPushKeys.Clear();
        syncedKeys.Clear();
    }

    // ───────── 내부 구현 ─────────

    private async Task SyncAsync(string key)
    {
        if (!IsReady())
        {
            return;
        }

        string uid = CurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }

        try
        {
            DocumentSnapshot snap = await Doc(uid).GetSnapshotAsync();

            // 스냅샷 대기 중 계정이 바뀌었으면(로그아웃/전환) 이전 계정 데이터를 로컬/클라우드에 반영하지 않는다.
            if (CurrentUid() != uid)
            {
                return;
            }

            long localTs = GetLocalTimestamp(key);

            if (snap.Exists
                && snap.TryGetValue(SaveField, out string cloudJson)
                && !string.IsNullOrEmpty(cloudJson))
            {
                long cloudTs = snap.TryGetValue(TimestampField, out long t) ? t : 0L;

                if (cloudTs > localTs)
                {
                    // 클라우드가 최신 → 로컬에 반영(다음 LoadUnifiedGame이 이 값을 읽는다).
                    PlayerPrefs.SetString(key, cloudJson);
                    PlayerPrefs.Save();
                    Debug.Log($"[Cloud] 클라우드 세이브 채택 (cloud {cloudTs} > local {localTs}).");
                }
                else if (localTs > cloudTs)
                {
                    await PushAsync(key, uid);
                    Debug.Log($"[Cloud] 로컬 세이브 업로드 (local {localTs} > cloud {cloudTs}).");
                }
            }
            else if (localTs >= 0)
            {
                await PushAsync(key, uid);
                Debug.Log("[Cloud] 클라우드에 첫 세이브 업로드.");
            }

            // 정상 동기화 완료 → 이번 세션에는 이 키를 다시 다운로드하지 않는다.
            syncedKeys.Add(key);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Cloud] 동기화 실패(로컬 유지): {e.Message}");
        }
    }

    private async Task PushManyAsync(List<string> keys)
    {
        pushing = true;
        try
        {
            string uid = CurrentUid();
            if (string.IsNullOrEmpty(uid))
            {
                return;
            }

            foreach (string key in keys)
            {
                await PushAsync(key, uid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Cloud] 업로드 실패: {e.Message}");
        }
        finally
        {
            pushing = false;
        }
    }

    private async Task PushAsync(string key, string uid)
    {
        if (!IsReady() || string.IsNullOrEmpty(uid))
        {
            return;
        }

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        SaveMeta meta = ParseMeta(json);
        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { SaveField, json },
            { TimestampField, meta != null ? meta.lastSavedUnixTime : 0L },
            { VersionField, meta != null ? meta.version : 0 },
            { UpdatedAtField, FieldValue.ServerTimestamp },
        };

        // 업로드 직전 계정이 바뀌었으면 이전 계정 데이터를 새 계정 문서에 쓰지 않도록 중단한다.
        if (CurrentUid() != uid)
        {
            return;
        }

        await Doc(uid).SetAsync(payload, SetOptions.MergeAll);
    }

    private DocumentReference Doc(string uid)
    {
        return firestore.Collection(UsersCollection).Document(uid);
    }

    private static string CurrentUid()
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        return auth != null ? auth.Uid : null;
    }

    private bool IsReady()
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        if (auth == null || !auth.IsReady || !auth.IsSignedIn)
        {
            return false;
        }

        if (firestore == null)
        {
            try
            {
                firestore = FirebaseFirestore.DefaultInstance;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cloud] Firestore 초기화 실패: {e.Message}");
                return false;
            }
        }

        return firestore != null;
    }

    private static long GetLocalTimestamp(string key)
    {
        SaveMeta meta = ParseMeta(PlayerPrefs.GetString(key, string.Empty));
        return meta != null ? meta.lastSavedUnixTime : -1L;
    }

    private static SaveMeta ParseMeta(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<SaveMeta>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
