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
    /// 11-12 (9번) 처음 켤 때도: 연결한 적 없는 기기도 켤 때 창 없이 로그인을 한 번 해 봄(CloudSync.AutoStart) → 되면 저절로 연결 · 자동 저장,
    ///       안 되면(Play 게임즈 계정 없음 · 거절 · 인터넷 없음) 아무 창 없이 그대로 — 설정에서 직접 연결. 한 실행에 한 번만 해 봄.
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

        /// <summary>연결한 적 없는 기기에서도 켤 때 창 없이 로그인을 해 봄</summary>
        public static bool AutoLoginAllowed => true;
#else
        /// <summary>에디터 = 가짜 로그인으로 늘 켜짐 · 그 밖(Android 아닌 빌드) = 꺼짐</summary>
        public static bool Configured => Application.isEditor;
        /// <summary>시험용: 로그인 실패(창 닫음) 흉내</summary>
        public static bool SimulateCancel;
        /// <summary>에디터에서는 꺼 둠 — 가짜 로그인은 늘 성공해서, 켜 두면 평소 재생·다른 시험마다 클라우드가 끼어듦. 자동 로그인 시험이 켬</summary>
        public static bool SimulateAutoLogin;
        public static bool AutoLoginAllowed => SimulateAutoLogin;

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
        /// <summary>시험용: 응답이 끝내 안 오는 통신 흉내 (11-12 시간 초과 시험)</summary>
        public static bool SimulateHang;

        static void ReadImpl(Action<bool, string> done) => MainThread.After(0.2f, () =>
        {
            if (SimulateHang) return;
            if (!Application.isEditor || SimulateFail) done(false, null);
            else done(true, PlayerPrefs.GetString(FakeKey, ""));
        });

        static void WriteImpl(string json, Action<bool> done) => MainThread.After(0.2f, () =>
        {
            if (SimulateHang) return;
            if (!Application.isEditor || SimulateFail) { done(false); return; }
            PlayerPrefs.SetString(FakeKey, json);
            PlayerPrefs.Save();
            done(true);
        });
#endif
    }

    /// <summary>
    /// 세이브 ↔ 클라우드 맞추기 (불도깨비 CloudSync 와 같은 규칙):
    ///   클라우드 비어 있음 → 이 기기 것을 올림 · 이 기기가 갓 시작한 진행 → 클라우드를 받음 ·
    ///   클라우드가 갓 시작한 진행 → 이 기기 것을 올림 · 다른 진행(saveId 다름)을 둘 다 진행함 → 비교 창에서 고름.
    ///   클라우드를 한 번 읽어 맞춘 뒤에만 올림 (안 읽고 올리면 다른 폰의 진행을 덮을 수 있어서).
    /// 11-12 같은 진행(saveId 같음)은 Compare 로: "마지막으로 맞춘 클라우드 시각(Base)" 과 "켤 때 세이브에 적혀 있던 시각" 을 봄
    ///   — 전엔 savedAt 이 큰 쪽이었는데, 이 기기 savedAt 은 저장할 때마다 지금이 되어 옛 진행이 새 진행을 덮을 수 있었음.
    /// 11-12 세이브 판(ver)이 내 앱보다 높은 클라우드 세이브는 받지도 올리지도 않음 (TooNew) · 응답이 없으면 TimeoutSec 뒤 실패로 처리.
    /// </summary>
    public static class CloudSync
    {
        public const float UploadIntervalSec = 60f;
        const string LastUpKey = "Cloud.LastUpload", BaseKey = "Cloud.Base", BaseIdKey = "Cloud.BaseId";

        /// <summary>읽기·쓰기 응답을 기다리는 최대 시간(초) — 넘으면 실패로 치고 다시 할 수 있게 함</summary>
        public static float TimeoutSec = 20f;
        /// <summary>같은 진행에서 "맞춘 뒤로 이 기기에서 더 했다"고 보는 최소 시간(초) — 올리기 간격보다 넉넉히</summary>
        public static long SlackSec = 150;

        static bool synced, busy, autoTried;
        static float lastUpload = -999f, busyT;
        static int op;                  // 통신 번호 — 시간 초과로 버린 통신의 늦은 응답은 무시
        static Action onTimeout;

        /// <summary>이번 실행에서 클라우드와 맞췄음 (그 뒤부터 자동으로 올림)</summary>
        public static bool Synced => synced;
        public static bool Busy => busy;
        /// <summary>클라우드 세이브가 더 새 판의 게임에서 저장됨 → 업데이트해야 맞출 수 있음 (클라우드 창에 안내)</summary>
        public static bool NeedUpdate { get; private set; }
        /// <summary>마지막으로 클라우드에 올린 시각 (유닉스 초, 0 = 없음)</summary>
        public static long LastUploadAt => long.TryParse(PlayerPrefs.GetString(LastUpKey, "0"), out var v) ? v : 0;

        /// <summary>이 기기가 마지막으로 클라우드와 맞춘(올리거나 받은) 세이브의 저장 시각. 그 진행(saveId)이 아니면 0</summary>
        public static long BaseAt(string saveId) =>
            PlayerPrefs.GetString(BaseIdKey, "") == (saveId ?? "") && long.TryParse(PlayerPrefs.GetString(BaseKey, "0"), out var v) ? v : 0;

        public static void RememberBase(long at, string saveId)
        {
            PlayerPrefs.SetString(BaseKey, at.ToString());
            PlayerPrefs.SetString(BaseIdKey, saveId ?? "");
            PlayerPrefs.Save();
        }

        public static void ResetState()
        {
            synced = false; busy = false; autoTried = false; lastUpload = -999f; onTimeout = null; op++; NeedUpdate = false;
            PlayerPrefs.DeleteKey(LastUpKey); PlayerPrefs.DeleteKey(BaseKey); PlayerPrefs.DeleteKey(BaseIdKey);
        }

        static int Begin(Action timeout) { busy = true; busyT = 0; onTimeout = timeout; return ++op; }
        /// <summary>응답이 왔을 때: 내 통신이 아직 살아 있으면 true (시간 초과로 이미 버렸으면 false → 응답 무시)</summary>
        static bool End(int my) { if (my != op || !busy) return false; busy = false; onTimeout = null; return true; }

        /// <summary>켤 때(씬이 만들어질 때마다 불림): 창 없이 로그인 → 맞추기 (실패해도 조용히)
        ///   - 연결해 둔 기기: 맞출 때까지 씬이 다시 만들어질 때마다 해 봄
        ///   - 11-12 연결한 적 없는 기기: 한 실행에 한 번만 해 보고, 되면 저절로 연결(그 뒤로는 연결해 둔 기기와 같음)</summary>
        public static void AutoStart()
        {
            CloudRunner.Get();
            if (!CloudAuth.Configured || synced || busy) return;
            if (!CloudAuth.Linked)
            {
                if (autoTried || !CloudAuth.AutoLoginAllowed) return;
                autoTried = true;
            }
            CloudAuth.SilentLogin((ok, name) =>
            {
                if (!ok) return;
                if (!CloudAuth.Linked) CloudAuth.SetLinked(name);
                AfterLogin(null, true);
            });
        }

        public enum Pick { Local, Cloud, Ask }

        /// <summary>같은 진행(saveId 같음)일 때 어느 쪽을 쓸지.
        ///   C = 클라우드 세이브의 저장 시각 · L = 이 기기가 켤 때(또는 마지막으로 맞춘 때) 갖고 있던 저장 시각 · B = 마지막으로 맞춘 클라우드 시각
        ///   - C == B : 클라우드는 내가 마지막에 맞춘 그대로 → 이 기기 것을 올림
        ///   - C == L : 내가 올렸는데 "올림 끝"을 못 받고 꺼진 것(앱을 내릴 때 올림) → 같은 내용 → 이 기기 것
        ///   - 그 밖 = 다른 기기가 올림 → 이 기기에서 맞춘 뒤 더 한 게 없으면 클라우드를 받고, 양쪽 다 했으면 고르기 창
        ///   B 가 없으면(처음 맞춤) 저장 시각이 늦은 쪽 — 단 이번 실행에서 오래 했으면 고르기 창</summary>
        public static Pick Compare(SaveData cloud, SaveData local)
        {
            long c = cloud.savedAt, l = local.loadedSavedAt, b = BaseAt(local.saveId);
            bool played = Time.realtimeSinceStartup - local.loadedRealtime > SlackSec;
            if (b <= 0) return c > l ? (played ? Pick.Ask : Pick.Cloud) : Pick.Local;
            if (c == b || c == l) return Pick.Local;
            return l > b + SlackSec || played ? Pick.Ask : Pick.Cloud;
        }

        /// <summary>클라우드 세이브가 내 앱보다 높은 세이브 판 → 받으면 모르는 칸이 사라지고, 올리면 그 기기의 새 칸을 지움 → 둘 다 안 함</summary>
        static bool TooNew(SaveData cloud)
        {
            if (cloud == null || cloud.ver <= SaveData.Version) return false;
            NeedUpdate = true;
            Debug.LogWarning($"[Cloud] 클라우드 세이브 판 {cloud.ver} > 이 앱 {SaveData.Version} → 맞추지 않음");
            HUD.I?.Toast(L.T("클라우드 진행이 더 새 버전의 게임에서 저장되었습니다.\n게임을 업데이트해 주세요"));
            return true;
        }

        /// <param name="done">true = 클라우드 데이터로 바꿈 (씬을 다시 불러 화면이 새로 만들어짐)</param>
        /// <param name="quiet">실패해도 안내 글자를 안 띄움</param>
        public static void AfterLogin(Action<bool> done, bool quiet = false)
        {
            CloudRunner.Get();
            if (busy) { done?.Invoke(false); return; }
            Action fail = () =>
            {
                if (!quiet) HUD.I?.Toast(L.T("클라우드를 읽지 못했습니다. 잠시 뒤 다시 해 주세요"));
                done?.Invoke(false);
            };
            int my = Begin(fail);
            CloudSave.Read((ok, json) =>
            {
                if (!End(my)) return;
                if (!ok) { fail(); return; }
                var cloud = SaveData.Parse(json);
                var local = SaveData.Cur;
                if (cloud == null || string.IsNullOrEmpty(cloud.saveId)) { Finish(null, done, quiet); return; }
                if (TooNew(cloud)) { done?.Invoke(false); return; }
                bool ask;
                if (cloud.saveId == local.saveId)
                {
                    var pick = Compare(cloud, local);
                    if (pick != Pick.Ask) { Finish(pick == Pick.Cloud ? json : null, done, quiet); return; }
                    ask = true;
                }
                else
                {
                    if (IsFresh(local)) { Finish(json, done, quiet); return; }
                    if (IsFresh(cloud)) { Finish(null, done, quiet); return; }
                    ask = true;
                }
                if (!ask || HUD.I == null) { done?.Invoke(false); return; }   // 고를 창이 없으면 다음에 (올리지도 않음)
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
            RememberBase(SaveData.Cur.loadedSavedAt, SaveData.Cur.saveId);   // 지금 클라우드 = 이 기기와 같은 내용
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
            var s = SaveData.Cur;
            s.Save();
            long at = s.savedAt; string id = s.saveId; float rt = Time.realtimeSinceStartup;
            SaveData.Dirty = false;
            lastUpload = Time.unscaledTime;
            int my = Begin(() => { SaveData.Dirty = true; done?.Invoke(false); });
            CloudSave.Write(s.ToJson(), ok =>
            {
                if (!End(my)) return;
                if (ok)
                {
                    PlayerPrefs.SetString(LastUpKey, SaveData.Now.ToString());
                    RememberBase(at, id);
                    var cur = SaveData.Cur;
                    if (cur.saveId == id) { cur.loadedSavedAt = at; cur.loadedRealtime = rt; }   // 여기까지는 클라우드와 같음
                }
                else SaveData.Dirty = true;   // 다음에 다시
                done?.Invoke(ok);
            });
        }

        /// <summary>[클라우드에서 불러오기] — 클라우드 데이터로 바꿈 (비어 있으면 알림만)</summary>
        public static void Download()
        {
            if (busy) return;
            Action fail = () => HUD.I?.Toast(L.T("클라우드를 읽지 못했습니다. 잠시 뒤 다시 해 주세요"));
            int my = Begin(fail);
            CloudSave.Read((ok, json) =>
            {
                if (!End(my)) return;
                if (!ok) { fail(); return; }
                var cloud = SaveData.Parse(json);
                if (cloud == null) { HUD.I?.Toast(L.T("클라우드에 저장된 데이터가 없습니다")); return; }
                if (TooNew(cloud)) return;
                synced = true;
                Load(json);
            });
        }

        /// <summary>CloudRunner.Update — 응답 없는 통신 정리 + 바뀐 게 있으면 1분마다 올림</summary>
        public static void Tick()
        {
            if (busy)
            {
                // 앱이 내려가 있던 시간은 세지 않음 (한 프레임에 0.5초까지만) — 내릴 때 올린 것의 응답은 돌아온 첫 프레임에 처리됨
                busyT += Mathf.Min(Time.unscaledDeltaTime, 0.5f);
                if (busyT > TimeoutSec)
                {
                    var t = onTimeout;
                    busy = false; onTimeout = null; op++;
                    Debug.LogWarning("[Cloud] 응답 없음 → 시간 초과");
                    t?.Invoke();
                }
            }
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
