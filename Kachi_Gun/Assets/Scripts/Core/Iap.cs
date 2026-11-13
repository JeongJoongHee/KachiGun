using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Linq;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
#endif

namespace KachiGun
{
    /// <summary>
    /// 인앱 결제 (10-30, 불도깨비 키우기 결제 코드를 가져옴 — Unity In-App Purchasing 5.4.4 · 구글 결제 라이브러리).
    /// 상품 = Shop 표 Price_Type KRW 줄 5개. 상품 ID = Product_Id 칸 그대로 (Play Console 인앱 상품 ID 와 똑같아야 함).
    ///  - 영구(한 번 사면 계속): Reward 칸 NoAllAds (remove_all_ads) · AutoUpgrade (auto_upgrade)
    ///  - 소모(살 때마다 다이아): gem_pack_small · gem_pack_mid · gem_pack_large (Bonus_Gems 칸)
    ///  - 1회 한정 총알(11-13): Reward 칸 Bullet:총알 코드 (starter_ss_missile = Bullet:55 SS 미사일탄 · 990원) — 구글엔 영구 상품,
    ///    빈 장착 칸 → 인벤토리 → 둘 다 차 있으면 빈 칸이 생길 때까지 기다림 (SaveData.PutBullet). 다시 설치해 되살리면 총알을 다시 줌
    ///
    ///  [구매] → 구글 결제창 → OnPurchasePending → 앱 안에서 구글 서명 확인 (CrossPlatformValidator, 라이선스 키)
    ///  → 지급(Grant) · 세이브 → 구글에 "지급 끝"(ConfirmPurchase).
    ///  - 결제 직후 앱이 꺼지면 다음 실행 때 지난 결제를 가져와 같은 흐름으로 지급 (구글은 "지급 끝"을 못 받은 결제를 3일 뒤 환불)
    ///  - 같은 주문을 두 번 주지 않게 SaveData.iapOrders 에 주문 번호
    ///  - 영구 상품은 앱을 지웠다 다시 깔아도 구글의 구매 기록으로 되살림 (세이브가 폰 안에만 있어서)
    ///
    /// 켜지는 조건: 안드로이드 기기 + 라이선스 키(GooglePlayTangle — 메뉴 카치건 > 결제 라이선스 키 넣기) + 스토어에 상품이 있음.
    /// 10-30 지금은 키를 안 넣음 → 폰에선 결제가 꺼져 있고 원화 상품·자동강화 버튼이 숨음.
    /// 새 개발자 계정(kachigoogoo)으로 앱을 옮긴 뒤 그 계정의 라이선스 키를 넣고 상품을 만들면 켜짐.
    /// 에디터는 가짜 결제(확인 창 → 바로 지급).
    /// </summary>
    public static class Iap
    {
        /// <summary>상품 정보를 받았거나 지급했을 때 — 상점 창·HUD 가 다시 그림</summary>
        public static event Action Changed;

        static SaveData S => SaveData.Cur;

        public static bool IsCash(ShopRow r) => r != null && r.priceType == "KRW" && !string.IsNullOrEmpty(r.id);
        /// <summary>한 번 사면 계속 (광고 제거 · 자동강화) · 1회 한정 총알</summary>
        public static bool Permanent(ShopRow r) => r != null && (r.reward == "NoAllAds" || r.reward == "AutoUpgrade" || BulletOf(r) >= 0);
        public static bool Owned(ShopRow r) => r != null && (r.reward == "NoAllAds" ? S.noAds : r.reward == "AutoUpgrade" ? S.autoUpgrade : BulletOf(r) >= 0 && S.iapOnce.Contains(r.id));
        /// <summary>1회 한정 총알 상품의 총알 코드 (Reward 칸 "Bullet:55") · 아니면 -1</summary>
        public static int BulletOf(ShopRow r) =>
            r != null && r.reward != null && r.reward.StartsWith("Bullet:") && int.TryParse(r.reward.Substring(7), out int c) && Tables.Weapon(c) != null ? c : -1;
        static ShopRow Row(string id) => Tables.Shop.Find(x => x.id == id && IsCash(x));

        /// <summary>상점 버튼 글자: 스토어 가격(받았으면) → 표의 원화</summary>
        public static string PriceText(ShopRow r) => StorePrice(r.id) ?? ShopPopup.KrwText(r.priceValue);

        /// <summary>상품을 받는 처리 (에디터 가짜 결제 · 폰 결제 · 되살리기 공통). 영구 상품을 이미 가졌으면 아무것도 안 함.
        /// restore = 구글 구매 기록으로 되살리는 중 (11-12: 처음 살 때 주는 보너스 다이아는 다시 주지 않음 — 자동강화 500)</summary>
        public static void Grant(ShopRow r, bool quiet = false, bool restore = false)
        {
            if (r == null) return;
            if (Permanent(r) && Owned(r)) return;
            if (r.reward == "NoAllAds") S.noAds = true;
            else if (r.reward == "AutoUpgrade") { S.autoUpgrade = true; S.autoOn = true; }
            int bullet = BulletOf(r), put = -1;
            if (bullet >= 0)
            {
                // 11-13 1회 한정 총알: 되살릴 때(다시 설치)도 총알은 줌 — 산 물건 자체라서 (보너스 다이아와 다름)
                S.iapOnce.Add(r.id);
                S.weaponUnlocked = true;   // 총알 메뉴가 아직 잠겨 있어도 산 총알을 볼 수 있게
                put = S.PutBullet(bullet);
                if (put == 0) Squad.I?.Rebuild();
            }
            double bonus = restore ? 0 : r.bonusGems;
            if (bonus > 0) S.gems += bonus;
            S.Save();
            Debug.Log($"[Iap] {(restore ? "되살림" : "지급")} {r.id} ({r.reward}, 다이아 +{bonus}{(put >= 0 ? $", 총알 {bullet} → {(put == 0 ? "장착" : put == 1 ? "인벤토리" : "기다림")}" : "")})");
            if (!quiet)
            {
                AudioManager.Play(AudioManager.Sfx.Coin);
                if (bonus > 0) HUD.I?.Bump(true);
                string msg = L.F("{0} 구매 완료!", L.T(r.name));
                string sub = put == 0 ? "빈 칸에 바로 장착했습니다" : put == 1 ? "총알 인벤토리에 넣었습니다" : put == 2 ? "인벤토리가 가득 차 있어 빈 칸이 생기면 들어갑니다" : null;
                if (sub != null) msg += "\n<size=30>" + L.T(sub) + "</size>";   // 알림 판(800x100)에 두 줄이 들어가게 둘째 줄은 작게
                HUD.I?.Toast(msg);
            }
            GameManager.I?.Notify();
            Changed?.Invoke();
        }

        /// <summary>상점·HUD 의 [구매]</summary>
        public static void Buy(string id)
        {
            var r = Row(id);
            if (r == null) { HUD.I?.Toast(L.T("결제는 준비 중입니다")); return; }
            if (Permanent(r) && Owned(r)) { HUD.I?.Toast(L.T(BulletOf(r) >= 0 ? "이미 구매한 상품입니다 (1회 한정)" : "이미 가지고 있습니다")); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            switch (Purchase(r))
            {
                case 0: break;   // 결제창 열림
                case 1: HUD.I?.Toast(L.T("스토어에 연결하는 중입니다. 잠시 뒤 다시 눌러 주세요")); break;
                default: HUD.I?.Toast(L.T("결제는 준비 중입니다")); break;
            }
#else
            if (Application.isBatchMode) { Grant(r); return; }   // 자동 시험
            ConfirmPopup.Ask($"<size=46>{L.T(r.name)}</size>\n\n{L.T(r.desc)}\n\n" + L.F("[테스트] 가짜 결제 {0} — 살까요?", PriceText(r)), () => Grant(r), "구매");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static StoreController store;
        static CrossPlatformValidator validator;
        static bool connecting, connected;
        static readonly Dictionary<string, Product> products = new();
        const int KeepOrders = 100;

        /// <summary>라이선스 키가 들어 있는지 (없으면 결제를 아예 안 켬 — 영수증 서명을 확인할 수 없으니까)</summary>
        public static bool Configured => GooglePlayTangle.IsPopulated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Connect();

        static List<ProductDefinition> Definitions()
        {
            Tables.Load();
            return Tables.Shop.Where(IsCash).GroupBy(r => r.id).Select(g => g.First())
                .Select(r => new ProductDefinition(r.id, Permanent(r) ? ProductType.NonConsumable : ProductType.Consumable)).ToList();
        }

        static async void Connect()
        {
            if (connecting || connected) return;
            if (!Configured) { Debug.Log("[Iap] 라이선스 키 없음 → 결제 꺼짐 (카치건 > 결제 라이선스 키 넣기)"); return; }
            if (Definitions().Count == 0) { Debug.Log("[Iap] 원화 상품 없음 → 결제 꺼짐"); return; }
            connecting = true;
            MainThread.Get();
            try
            {
                if (validator == null) validator = new CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier);
                if (store == null)
                {
                    store = UnityIAPServices.StoreController();
                    store.OnStoreConnected += OnConnected;
                    store.OnStoreDisconnected += d => { connected = false; connecting = false; Debug.LogWarning("[Iap] 스토어 연결 끊김: " + d.message); };
                    store.OnProductsFetched += OnProducts;
                    store.OnProductsFetchFailed += f => Debug.LogWarning($"[Iap] 상품 정보 실패 {f.FailedFetchProducts.Count}개: {f.FailureReason}");
                    store.OnPurchasesFetched += OnPurchasesFetched;
                    store.OnPurchasesFetchFailed += f => Debug.LogWarning("[Iap] 지난 결제 가져오기 실패: " + f.message);
                    store.OnPurchasePending += GrantOrder;
                    store.OnPurchaseConfirmed += OnConfirmed;
                    store.OnPurchaseFailed += OnFailed;
                    store.OnPurchaseDeferred += _ => HUD.I?.Toast(L.T("결제 승인을 기다리는 중입니다"));
                }
                await store.Connect();
            }
            catch (Exception e)
            {
                connecting = false;
                Debug.LogWarning("[Iap] 연결 실패: " + e.Message);
            }
        }

        static void OnConnected()
        {
            connected = true;
            connecting = false;
            Debug.Log("[Iap] 스토어 연결됨");
            store.FetchProducts(Definitions());
        }

        static void OnProducts(List<Product> list)
        {
            foreach (var p in list)
                if (p != null && p.availableToPurchase) products[p.definition.id] = p;
            Debug.Log($"[Iap] 상품 {products.Count}개: " + string.Join(", ", products.Keys));
            Changed?.Invoke();
            store.FetchPurchases();   // 못 준 결제 → OnPurchasePending · 이미 산 영구 상품 → OnPurchasesFetched
        }

        /// <summary>상점에 이 원화 상품을 보일지: 스토어에서 상품 정보를 받았을 때만 (Play Console 에 상품을 만들면 저절로 보임)</summary>
        public static bool CanSell(ShopRow r) => Configured && IsCash(r) && products.ContainsKey(r.id);

        public static string StorePrice(string id)
        {
            if (string.IsNullOrEmpty(id) || !products.TryGetValue(id, out var p)) return null;
            var s = p.metadata?.localizedPriceString;
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <returns>0 = 결제창 열림 · 1 = 연결 중 · 2 = 쓸 수 없음</returns>
        static int Purchase(ShopRow r)
        {
            if (!Configured) return 2;
            if (!connected || !products.TryGetValue(r.id, out var product)) { Connect(); return 1; }
            store.PurchaseProduct(product);
            return 0;
        }

        static string ProductIdOf(Order order) => order?.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id ?? "";

        static bool AlreadyGranted(string tx) => !string.IsNullOrEmpty(tx) && S.iapOrders.Contains(tx);

        static void RememberOrder(string tx)
        {
            if (string.IsNullOrEmpty(tx)) return;
            S.iapOrders.Add(tx);
            while (S.iapOrders.Count > KeepOrders) S.iapOrders.RemoveAt(0);
        }

        static void GrantOrder(PendingOrder order)
        {
            string id = ProductIdOf(order);
            string tx = order.Info?.TransactionID ?? "";
            var row = Row(id);
            if (row == null)
            {
                Debug.LogWarning($"[Iap] 표에 없는 상품 '{id}' — 지급 안 함 (Shop 표 Product_Id 확인)");
                return;   // 표를 고친 다음 실행 때 다시 들어옴
            }
            if (AlreadyGranted(tx)) { store.ConfirmPurchase(order); return; }   // 지급은 끝났는데 "지급 끝"만 못 보낸 경우
            if (!ReceiptValid(order.Info?.Receipt, id)) { HUD.I?.Toast(L.T("결제를 확인하지 못했습니다")); return; }
            RememberOrder(tx);
            Grant(row);   // 안에서 세이브
            store.ConfirmPurchase(order);
        }

        /// <summary>이미 산 영구 상품 되살리기 (다시 설치·세이브 초기화 뒤)</summary>
        static void OnPurchasesFetched(Orders orders)
        {
            if (orders?.ConfirmedOrders == null) return;
            foreach (var o in orders.ConfirmedOrders)
            {
                var row = Row(ProductIdOf(o));
                if (row == null || !Permanent(row) || Owned(row)) continue;
                if (!ReceiptValid(o.Info?.Receipt, row.id)) continue;
                Debug.Log("[Iap] 되살림 " + row.id);
                Grant(row, true, true);
                HUD.I?.Toast(L.F("{0} 구매 기록을 되살렸습니다", L.T(row.name)));
            }
        }

        static bool ReceiptValid(string receipt, string productId)
        {
            try
            {
                foreach (var r in validator.Validate(receipt))
                    if (r.productID == productId) return true;
                Debug.LogWarning($"[Iap] 영수증에 '{productId}' 없음");
                return false;
            }
            catch (IAPSecurityException e) { Debug.LogWarning("[Iap] 영수증 서명 불일치: " + e.Message); return false; }
            catch (Exception e) { Debug.LogWarning("[Iap] 영수증 확인 실패: " + e.Message); return false; }
        }

        static void OnConfirmed(Order order)
        {
            if (order is FailedOrder f) Debug.LogWarning($"[Iap] '지급 끝' 알림 실패 {ProductIdOf(order)}: {f.FailureReason} {f.Details}");
        }

        static void OnFailed(FailedOrder f)
        {
            Debug.Log($"[Iap] 결제 실패 {ProductIdOf(f)}: {f.FailureReason} {f.Details}");
            switch (f.FailureReason)
            {
                case PurchaseFailureReason.UserCancelled: HUD.I?.Toast(L.T("결제를 취소했습니다")); break;
                case PurchaseFailureReason.ExistingPurchasePending: HUD.I?.Toast(L.T("앞의 결제를 처리하는 중입니다")); break;
                default: HUD.I?.Toast(L.T("결제하지 못했습니다")); break;
            }
        }
#else
        // 에디터·다른 플랫폼: 스토어 없음. 에디터는 가짜 결제로 살 수 있게 늘 보임
        public static bool Configured => false;
        /// <summary>시험용: 에디터에서 "결제가 꺼진 휴대폰"처럼 원화 상품을 숨김</summary>
        public static bool SimulateNoStore;
        public static bool CanSell(ShopRow r) => Application.isEditor && !SimulateNoStore && IsCash(r);
        public static string StorePrice(string id) => null;
#endif
    }
}
