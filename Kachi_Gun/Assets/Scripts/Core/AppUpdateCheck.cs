using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
#endif

namespace KachiGun
{
    /// <summary>
    /// 새 버전 알림 (11-12): 켤 때 한 번 Play 스토어에 "이 폰에 받을 업데이트가 있나" 물어봄 (구글 앱 내 업데이트 라이브러리
    /// com.google.play.appupdate — 서버 없음) → 있으면 다른 창이 다 닫힌 뒤 [업데이트] 창 → 누르면 스토어 이 게임 페이지.
    ///  - Play 스토어에서 설치한 앱에서만 답이 옴 (개발용 APK · 에디터는 조용히 안 띄움)
    ///  - 한 번 실행에 한 번만 (클라우드 불러오기로 씬을 다시 만들어도 다시 안 물음) · X = 나중에
    ///  - 에디터 시험: FakeAvailable = true
    /// </summary>
    public static class AppUpdateCheck
    {
        /// <summary>시험용: 에디터에서 "업데이트 있음"으로 침</summary>
        public static bool FakeAvailable;
        static bool started, available, shown;

        public static bool Available => available;
        public static bool Shown => shown;

        /// <summary>시험용: 다시 물을 수 있게</summary>
        public static void ResetForTest() { started = false; available = false; shown = false; }

        public static string StoreUrl => "market://details?id=" + Application.identifier;
        public static string WebUrl => "https://play.google.com/store/apps/details?id=" + Application.identifier;

        public static void Start()
        {
            if (started) return;
            started = true;
            Runner.Get().StartCoroutine(Ask());
        }

        static IEnumerator Ask()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var mgr = new AppUpdateManager();
            var op = mgr.GetAppUpdateInfo();
            yield return op;
            if (!op.IsSuccessful) { Debug.Log("[Update] 확인 못 함: " + op.Error); yield break; }
            var info = op.GetResult();
            Debug.Log($"[Update] 상태 {info.UpdateAvailability} · 새 버전 코드 {info.AvailableVersionCode}");
            available = info.UpdateAvailability == UpdateAvailability.UpdateAvailable;
#else
            yield return null;
            available = FakeAvailable;
#endif
            if (!available) yield break;
            // 켤 때 뜨는 출석 · 방치 보상 · 클라우드 고르기 창이 다 닫힌 뒤에
            while (HUD.I == null || HUD.I.PopupOpen) yield return new WaitForSecondsRealtime(0.5f);
            Show();
        }

        public static void Show()
        {
            shown = true;
            ConfirmPopup.Ask($"<size=46>{L.T("새 버전")}</size>\n\n" + L.T("새 버전이 나왔습니다.\n스토어에서 업데이트해 주세요."), OpenStore, "업데이트", "업데이트");
        }

        public static void OpenStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.OpenURL(StoreUrl);
#else
            Debug.Log("[Update] 스토어 열기 " + WebUrl);
#endif
        }

        /// <summary>씬이 바뀌어도 남는 코루틴 자리</summary>
        class Runner : MonoBehaviour
        {
            static Runner instance;
            public static Runner Get()
            {
                if (instance == null)
                {
                    var go = new GameObject("AppUpdateCheck");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }
                return instance;
            }
        }
    }
}
