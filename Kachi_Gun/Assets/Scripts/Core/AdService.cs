using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace KachiGun
{
    /// <summary>
    /// AdMob 번호 (10-26 사용자가 만든 것 = 키 보관/admob.txt). 앱 ID 는 Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset 에도 같은 값.
    /// 개발용 키 APK(BuildAab.BuildApkDebug)는 KACHI_TEST_ADS 로 구글 테스트 광고를 씀 — 내 광고를 내가 누르면 AdMob 계정이 막힐 수 있어서.
    /// </summary>
    public static class AdIds
    {
        public const string AppId = "ca-app-pub-5299482082775448~5156919833";
        public const string Rewarded = "ca-app-pub-5299482082775448/1272595276";
        /// <summary>구글이 공개한 안드로이드 테스트 보상형 광고 단위 (누구나 써도 됨)</summary>
        public const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
#if KACHI_TEST_ADS
        public const bool UseTest = true;
#else
        public const bool UseTest = false;
#endif
        public static string RewardedUnit => UseTest ? TestRewarded : Rewarded;
    }

    /// <summary>
    /// 보상형 광고 (10-30 실제 AdMob). 게임 속 광고 4곳 = 공격 x2 · 골드 x2 · 던전 보상 · 무료 다이아.
    ///  - "모든 광고 제거"를 샀으면 광고 없이 바로 보상
    ///  - 에디터·자동 시험 = 광고 없이 바로 보상 (예전과 같음)
    ///  - 폰 = 켜질 때 동의 확인(유럽 등만 창) → SDK 시작 → 광고 한 편 미리 불러 둠. 준비 안 됐으면 "불러오는 중" 토스트, 게임은 안 막힘
    ///  - 보상은 끝까지 봤을 때만 (중간에 닫으면 없음)
    /// </summary>
    public static class AdService
    {
        public static void Show(string place, Action onReward)
        {
            if (SaveData.Cur.noAds) { onReward?.Invoke(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            AdMob.ShowRewarded(place, onReward);
#else
            HUD.I?.Toast(L.T("[테스트] 광고 대신 바로 지급"));
            onReward?.Invoke();
#endif
        }
    }

    /// <summary>광고·결제 콜백을 게임 흐름(메인 스레드)으로 옮기고, 늦게 다시 하기를 돌려 주는 작은 물건 (씬이 바뀌어도 남음)</summary>
    public class MainThread : MonoBehaviour
    {
        static MainThread instance;
        static readonly Queue<Action> queue = new();

        public static MainThread Get()
        {
            if (instance == null)
            {
                var go = new GameObject("MainThread");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MainThread>();
            }
            return instance;
        }

        public static void Post(Action a) { lock (queue) queue.Enqueue(a); }

        public static void After(float sec, Action a) => Get().StartCoroutine(Later(sec, a));

        static IEnumerator Later(float sec, Action a)
        {
            yield return new WaitForSecondsRealtime(sec);
            a?.Invoke();
        }

        void Update()
        {
            while (true)
            {
                Action a;
                lock (queue) { if (queue.Count == 0) break; a = queue.Dequeue(); }
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>AdMob 보상형 광고 (Google Mobile Ads 11.5.0 — 심심할 때 하는 게임과 같은 버전)</summary>
    public static class AdMob
    {
        static readonly float[] RetryDelays = { 10f, 30f, 60f, 120f, 300f };
        static RewardedAd ad;
        static bool started, initialized, loading;
        static int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SaveData.Cur.noAds) return;   // 광고 제거를 샀으면 SDK 도 안 켬
            Start();
        }

        static void Start()
        {
            if (started) return;
            started = true;
            MainThread.Get();
            Debug.Log("[Ads] 시작 — " + (AdIds.UseTest ? "테스트 광고" : "실제 광고") + " " + AdIds.RewardedUnit);
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // 대상 연령 13세 이상 (게임설정_답안 5번) → 아동 대상 표시 안 함, 광고 등급 상한 = 12세 전후(PG)
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                TagForChildDirectedTreatment = TagForChildDirectedTreatment.Unspecified,
                MaxAdContentRating = MaxAdContentRating.PG,
            });

            if (ConsentInformation.CanRequestAds()) InitSdk();   // 지난번에 동의를 받았으면 바로
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => MainThread.Post(() =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] 동의 정보 확인 실패: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MainThread.Post(() =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] 동의 창 실패: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) InitSdk();
                }));
            }));
        }

        static void InitSdk()
        {
            if (initialized) return;
            initialized = true;
            MobileAds.Initialize(_ => MainThread.Post(Load));
        }

        static bool Ready => ad != null && ad.CanShowAd();

        static void Load()
        {
            if (!initialized || loading || Ready) return;
            loading = true;
            RewardedAd.Load(AdIds.RewardedUnit, new AdRequest(), (loaded, error) => MainThread.Post(() =>
            {
                loading = false;
                if (error != null || loaded == null)
                {
                    float delay = RetryDelays[Mathf.Min(failures, RetryDelays.Length - 1)];
                    failures++;
                    Debug.LogWarning($"[Ads] 광고 불러오기 실패 ({(error != null ? error.GetMessage() : "빈 응답")}) → {delay}초 뒤 다시");
                    MainThread.After(delay, Load);
                    return;
                }
                failures = 0;
                ad = loaded;
            }));
        }

        public static void ShowRewarded(string place, Action onReward)
        {
            if (!started) Start();
            if (!Ready)
            {
                HUD.I?.Toast(L.T("광고를 불러오는 중입니다. 잠시 뒤 다시 눌러 주세요"));
                if (!loading && initialized) { failures = 0; Load(); }
                return;
            }
            var a = ad;
            ad = null;
            bool rewarded = false, done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                AudioListener.pause = false;
                a.Destroy();
                Load();   // 다음 광고를 미리
            }
            a.OnAdFullScreenContentClosed += () => MainThread.Post(Finish);
            a.OnAdFullScreenContentFailed += e => MainThread.Post(() =>
            {
                Debug.LogWarning("[Ads] 광고를 띄우지 못함: " + e.GetMessage());
                HUD.I?.Toast(L.T("광고를 띄우지 못했습니다. 잠시 뒤 다시 해 주세요"));
                Finish();
            });
            AudioListener.pause = true;   // 광고 소리와 겹치지 않게
            Debug.Log("[Ads] 보여 줌 — " + place);
            a.Show(_ => MainThread.Post(() =>
            {
                if (rewarded) return;
                rewarded = true;
                onReward?.Invoke();
                SaveData.Cur.Save();
            }));
        }
    }
#endif
}
