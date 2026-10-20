using System;

namespace KachiGun
{
    /// <summary>
    /// [임시] 광고 자리. 지금은 광고 SDK 가 없어서 바로 보상을 준다.
    /// 나중에 AdMob 보상형 광고를 붙일 때 Show 안만 바꾸면 된다.
    /// </summary>
    public static class AdService
    {
        public static void Show(string place, Action onReward)
        {
            HUD.I?.Toast(L.T("[테스트] 광고 대신 바로 지급"));
            onReward?.Invoke();
        }
    }

    /// <summary>[임시] 인앱 결제 자리. 결제 모듈을 붙이기 전까지 "준비 중"</summary>
    public static class Iap
    {
        public static void Buy(string product) => HUD.I?.Toast(L.T("결제는 준비 중입니다"));
    }
}
