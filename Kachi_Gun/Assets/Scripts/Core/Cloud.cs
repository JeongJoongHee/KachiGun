using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 클라우드 저장 (11-07, 버그 점검 "클라우드 저장으로만 진행") — 불도깨비 Account · CloudSave 를 줄여 가져옴.
    /// 구글 계정의 Play 게임즈 "저장된 게임" 한 칸에 세이브 JSON 을 통째로 둠 (우리 서버 없음). 게스트·계정 삭제·랭킹 같은 계정 기능은 없음.
    ///  - 켜지는 조건: 안드로이드 기기 + Scripting Define KACHI_GPGS(안드로이드에 넣어 둠) + Play 게임즈 App ID
    ///    (Window > Google Play Games > Setup > Android setup 이 Assets/GooglePlayGames/Resources/PlayGamesSettings.asset 을 만듦)
    ///    + Play Console 게임즈 서비스 설정에서 "저장된 게임" 켜기. App ID 가 없으면 폰에서는 꺼져 있음([초안], 결제의 Configured 와 같은 방식)
    ///  - 에디터 = 가짜 로그인·가짜 클라우드 (PlayerPrefs "FakeCloud.Kachi")
    /// 흐름: 설정 > [클라우드 저장] > [구글 계정 연결] → 클라우드와 비교(CloudSync.AfterLogin) → 그 뒤 1분마다·앱을 내릴 때 올림.
    ///       연결해 둔 기기는 켤 때 자동 로그인 → 같은 비교.
    /// </summary>
    public static class CloudAuth
    {
        const string LinkedKey = "Cloud.Linked", NameKey = "Cloud.Name";

        /// <summary>이 기기에서 구글 계정을 연결해 둠 (켤 때 자동 로그인)</summary>
        public static bool Linked => PlayerPrefs.GetInt(LinkedKey, 0) == 1;
        public static string DisplayName => PlayerPrefs.GetString(NameKey, "");
        /// <summary>이번 실행에서 로그인됨</summary>
        public static bool SignedIn { get; private set; }

        public static void SetLinked(string name)
        {
            PlayerPrefs.SetInt(LinkedKey, 1);
            PlayerPrefs.SetString(NameKey, name ?? "");
            PlayerPrefs.Save();
            SignedIn = true;
        }

        /// <summary>시험용: 연결 상태 지우기</summary>
        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(LinkedKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.Save();
            SignedIn = false;
            CloudSync.ResetState();
        }

        /// <summary>창 없이 자동 로그인 (켤 때) · done(성공, 이름)</summary>
        public static void SilentLogin(Action<bool, string> done) => Run(false, done);
        /// <summary>계정 고르는 창을 띄워 로그인 ([구글 계정 연결])</summary>
        public static void InteractiveLogin(Action<bool, string> done) => Run(true, done);

        static void Run(bool interactive, Action<bool, string> done)
        {
            RunImpl(interactive, (ok, name) =>
            {
                if (ok) SignedIn = true;
                done?.Invoke(ok, name);
            });
        }

#if KACHI_GPGS && UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Play 게임즈 App ID 를 넣었는지 (없으면 SDK 를 부르지 않음 — 설정 없이 부르면 앱이 멈출 수 있어서)</summary>
        public static bool Configured
        {
            get
            {
                var s = GooglePlayGames.PlayGamesSettings.LoadInstance();
                return s != null && !string.IsNullOrEmpty(s.AppId);
            }
        }

        static bool activated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EarlyInit() => Activate();

        static void Activate()
        {
            if (activated || !Configured) return;
            GooglePlayGames.PlayGamesPlatform.Activate();
            activated = true;
        }

        static void RunImpl(bool interactive, Action<bool, string> done)
        {
            if (!Configured || Application.internetReachability == NetworkReachability.NotReachable) { done(false, null); return; }
            Activate();
            var p = GooglePlayGames.PlayGamesPlatform.Instance;
            Action<GooglePlayGames.BasicApi.SignInStatus> cb = status => MainThread.Post(() =>
            {
                if (status == GooglePlayGames.BasicApi.SignInStatus.Success) done(true, p.GetUserDisplayName());
                else done(false, null);
            });
            MainThread.Get();
            if (interactive) p.ManuallyAuthenticate(cb);
            else p.Authenticate(cb);
        }
#else
        /// <summary>에디터 = 가짜 로그인으로 늘 켜짐 · 그 밖(Android 아닌 빌드) = 꺼짐</summary>
        public static bool Configured => Application.isEditor;
        /// <summary>시험용: 로그인 실패(창 닫음) 흉내</summary>
        public static bool SimulateCancel;

        static void RunImpl(bool interactive, Action<bool, string> done) =>
            MainThread.After(0.3f, () =>
            {
                if (!Application.isEditor || SimulateCancel) done(false, null);
                else done(true, "테스트 계정");
            });
#endif
    }

    /// <summary>클라우드 칸 읽기·쓰기 (Play 게임즈 저장된 게임 · 에디터는 PlayerPrefs)</summary>
    public static class CloudSave
    {
        public const string SlotName = "kachigun_save";

        /// <summary>done(ok, json) — 비어 있으면 ok true · json ""</summary>
        public static void Read(Action<bool, string> done) => ReadImpl(done);
        public static void Write(string json, Action<bool> done) => WriteImpl(json, done);

#if KACHI_GPGS && UNITY_ANDROID && !UNITY_EDITOR
        static GooglePlayGames.BasicApi.SavedGame.ISavedGameClient Client
        {
            get
            {
                if (!CloudAuth.Configured) return null;
                var p = GooglePlayGames.PlayGamesPlatform.Instance;
                return p != null && p.IsAuthenticated() ? p.SavedGame : null;
            }
        }

        static void Open(GooglePlayGames.BasicApi.DataSource source,
            Action<GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus, GooglePlayGames.BasicApi.SavedGame.ISavedGameMetadata> cb)
        {
            Client.OpenWithAutomaticConflictResolution(SlotName, source,
                GooglePlayGames.BasicApi.SavedGame.ConflictResolutionStrategy.UseMostRecentlySaved, cb);
        }

        static void ReadImpl(Action<bool, string> done)
        {
            if (Client == null) { done(false, null); return; }
            Open(GooglePlayGames.BasicApi.DataSource.ReadNetworkOnly, (status, meta) =>
            {
                if (status != GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus.Success)
                {
                    Debug.LogWarning("[Cloud] 열기 실패: " + status);
                    MainThread.Post(() => done(false, null));
                    return;
                }
                Client.ReadBinaryData(meta, (st, bytes) =>
                {
                    bool ok = st == GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus.Success;
                    if (!ok) Debug.LogWarning("[Cloud] 읽기 실패: " + st);
                    string json = ok && bytes != null && bytes.Length > 0 ? Encoding.UTF8.GetString(bytes) : "";
                    MainThread.Post(() => done(ok, json));
                });
            });
        }

        static void WriteImpl(string json, Action<bool> done)
        {
            if (Client == null) { done(false); return; }
            Open(GooglePlayGames.BasicApi.DataSource.ReadCacheOrNetwork, (status, meta) =>
            {
                if (status != GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus.Success)
                {
                    Debug.LogWarning("[Cloud] 열기 실패: " + status);
                    MainThread.Post(() => done(false));
                    return;
                }
                var update = new GooglePlayGames.BasicApi.SavedGame.SavedGameMetadataUpdate.Builder()
                    .WithUpdatedDescription(Application.productName + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                    .Build();
                Client.CommitUpdate(meta, update, Encoding.UTF8.GetBytes(json), (st, m) =>
                {
                    bool ok = st == GooglePlayGames.BasicApi.SavedGame.SavedGameRequestStatus.Success;
                    if (!ok) Debug.LogWarning("[Cloud] 쓰기 실패: " + st);
                    MainThread.Post(() => done(ok));
                });
            });
        }
#else
        public const string FakeKey = "FakeCloud.Kachi";
        /// <summary>시험용: 읽기·쓰기 실패 흉내</summary>
        public static bool SimulateFail;

        static void ReadImpl(Action<bool, string> done) => MainThread.After(0.2f, () =>
        {
            if (!Application.isEditor || SimulateFail) done(false, null);
            else done(true, PlayerPrefs.GetString(FakeKey, ""));
        });

        static void WriteImpl(string json, Action<bool> done) => MainThread.After(0.2f, () =>
        {
            if (!Application.isEditor || SimulateFail) { done(false); return; }
            PlayerPrefs.SetString(FakeKey, json);
            PlayerPrefs.Save();
            done(true);
        });
#endif
    }

    /// <summary>
    /// 세이브 ↔ 클라우드 맞추기 (불도깨비 CloudSync 와 같은 규칙):
    ///   클라우드 비어 있음 → 이 기기 것을 올림 · 같은 진행(saveId 같음) → 더 늦게 저장한 쪽 ·
    ///   이 기기가 갓 시작한 진행 → 클라우드를 받음 · 클라우드가 갓 시작한 진행 → 이 기기 것을 올림 · 둘 다 진행함 → 비교 창에서 고름.
    ///   클라우드를 한 번 읽어 맞춘 뒤에만 올림 (안 읽고 올리면 다른 폰의 진행을 덮을 수 있어서).
    /// </summary>
    public static class CloudSync
    {
        public const float UploadIntervalSec = 60f;
        const string LastUpKey = "Cloud.LastUpload";

        static bool synced, busy;
        static float lastUpload = -999f;

        /// <summary>이번 실행에서 클라우드와 맞췄음 (그 뒤부터 자동으로 올림)</summary>
        public static bool Synced => synced;
        public static bool Busy => busy;
        /// <summary>마지막으로 클라우드에 올린 시각 (유닉스 초, 0 = 없음)</summary>
        public static long LastUploadAt => long.TryParse(PlayerPrefs.GetString(LastUpKey, "0"), out var v) ? v : 0;

        public static void ResetState() { synced = false; busy = false; lastUpload = -999f; PlayerPrefs.DeleteKey(LastUpKey); }

        /// <summary>켤 때: 연결해 둔 기기면 자동 로그인 → 맞추기 (실패해도 조용히)</summary>
        public static void AutoStart()
        {
            CloudRunner.Get();
            if (!CloudAuth.Linked || !CloudAuth.Configured || synced) return;
            CloudAuth.SilentLogin((ok, name) => { if (ok) AfterLogin(null, true); });
        }

        /// <param name="done">true = 클라우드 데이터로 바꿈 (씬을 다시 불러 화면이 새로 만들어짐)</param>
        /// <param name="quiet">실패해도 안내 글자를 안 띄움</param>
        public static void AfterLogin(Action<bool> done, bool quiet = false)
        {
            CloudRunner.Get();
            if (busy) { done?.Invoke(false); return; }
            busy = true;
            CloudSave.Read((ok, json) =>
            {
                busy = false;
                if (!ok)
                {
                    if (!quiet) HUD.I?.Toast(L.T("클라우드를 읽지 못했습니다. 잠시 뒤 다시 해 주세요"));
                    done?.Invoke(false);
                    return;
                }
                var cloud = SaveData.Parse(json);
                var local = SaveData.Cur;
                if (cloud == null || string.IsNullOrEmpty(cloud.saveId)) { Finish(null, done, quiet); return; }
                if (cloud.saveId == local.saveId) { Finish(cloud.savedAt > local.savedAt ? json : null, done, quiet); return; }
                if (IsFresh(local)) { Finish(json, done, quiet); return; }
                if (IsFresh(cloud)) { Finish(null, done, quiet); return; }
                if (HUD.I == null) { done?.Invoke(false); return; }   // 고를 창이 없으면 다음에 (올리지도 않음)
                CloudConflictPopup.Show(cloud, local, pickCloud => Finish(pickCloud ? json : null, done, quiet));
            });
        }

        /// <summary>갓 시작한 진행 (1-2 전 · 산 것 없음) — 비교 없이 다른 쪽을 씀</summary>
        public static bool IsFresh(SaveData d) =>
            d.bestEver <= 1 && !d.noAds && !d.autoUpgrade && (d.iapOrders == null || d.iapOrders.Count == 0);

        /// <param name="cloudJson">받을 클라우드 데이터 (null = 이 기기 것을 올림)</param>
        static void Finish(string cloudJson, Action<bool> done, bool quiet)
        {
            synced = true;
            if (cloudJson != null)
            {
                Load(cloudJson);
                done?.Invoke(true);
                return;
            }
            Upload(ok => { if (!quiet) HUD.I?.Toast(ok ? L.T("클라우드에 저장했습니다") : L.T("클라우드에 저장하지 못했습니다")); });
            done?.Invoke(false);
        }

        /// <summary>클라우드 데이터로 바꾸고 화면을 새로 만듦</summary>
        public static void Load(string json)
        {
            if (!SaveData.ReplaceWith(json)) { HUD.I?.Toast(L.T("클라우드 데이터를 읽지 못했습니다")); return; }
            pendingToast = L.T("클라우드에서 불러왔습니다");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        /// <summary>씬을 다시 불러온 뒤 띄울 알림 (HUD 가 생긴 뒤 GameManager 가 꺼냄)</summary>
        public static string pendingToast;

        /// <summary>[지금 저장] · 앱을 내릴 때 — 맞춘 뒤에만</summary>
        public static void Upload(Action<bool> done = null)
        {
            if (!synced || busy) { done?.Invoke(false); return; }
            if (GameManager.I) { Fx.I?.FlushMoney(); }
            SaveData.Cur.Save();
            busy = true;
            SaveData.Dirty = false;
            lastUpload = Time.unscaledTime;
            CloudSave.Write(SaveData.Cur.ToJson(), ok =>
            {
                busy = false;
                if (ok) { PlayerPrefs.SetString(LastUpKey, SaveData.Now.ToString()); PlayerPrefs.Save(); }
                else SaveData.Dirty = true;   // 다음에 다시
                done?.Invoke(ok);
            });
        }

        /// <summary>[클라우드에서 불러오기] — 클라우드 데이터로 바꿈 (비어 있으면 알림만)</summary>
        public static void Download()
        {
            if (busy) return;
            busy = true;
            CloudSave.Read((ok, json) =>
            {
                busy = false;
                if (!ok) { HUD.I?.Toast(L.T("클라우드를 읽지 못했습니다. 잠시 뒤 다시 해 주세요")); return; }
                if (SaveData.Parse(json) == null) { HUD.I?.Toast(L.T("클라우드에 저장된 데이터가 없습니다")); return; }
                synced = true;
                Load(json);
            });
        }

        /// <summary>CloudRunner.Update — 바뀐 게 있으면 1분마다</summary>
        public static void Tick()
        {
            if (!synced || busy || !SaveData.Dirty || !CloudAuth.SignedIn) return;
            if (Time.unscaledTime - lastUpload < UploadIntervalSec) return;
            Upload();
        }

        /// <summary>앱을 내리거나 끌 때</summary>
        public static void UploadNow()
        {
            if (synced && !busy && SaveData.Dirty && CloudAuth.SignedIn) Upload();
        }
    }

    /// <summary>클라우드 자동 올리기를 돌리는 곳 (씬이 바뀌어도 남음)</summary>
    public class CloudRunner : MonoBehaviour
    {
        static CloudRunner instance;
        public static CloudRunner Get()
        {
            if (instance == null)
            {
                var go = new GameObject("CloudRunner");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CloudRunner>();
            }
            return instance;
        }

        void Update() => CloudSync.Tick();
        void OnApplicationPause(bool paused) { if (paused) CloudSync.UploadNow(); }
        void OnApplicationQuit() => CloudSync.UploadNow();
    }
}
