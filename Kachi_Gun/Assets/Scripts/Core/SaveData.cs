using System;
using System.Collections.Generic;
using UnityEngine;

namespace KachiGun
{
    /// <summary>세이브 (PlayerPrefs 에 JSON 한 줄). 5초마다·앱을 내릴 때 저장.</summary>
    [Serializable]
    public class SaveData
    {
        public const string Key = "KachiGun.Save.v1";
        public const int InvSize = 20, SlotCount = 3;      // 11-05 인벤토리 한 줄 늘림 (5 x 3 → 5 x 4) · 옛 세이브는 Fix() 가 빈칸 채움

        public double gold, gems;
        public int stage;            // 서브 스테이지 번호 0 = 1-1, 4 = 1-5, 5 = 2-1 …
        public int kills;            // 지금 서브 스테이지에서 잡은 수
        public int bestEver;         // 가장 멀리 간 스테이지 (기능 열림 기준)
        public int lvAtk, lvSpd, lvGold;
        public bool bossFarm;        // 보스 실패 → 지금 스테이지 반복 사냥 중 ([보스 도전] 누르면 다시)

        public List<int> inv = new();      // InvSize 칸, -1 = 빈칸, 그 외 총알 코드(Tables.Code)
        public List<int> slots = new();    // 장착 3칸 (0 = 리더)
        public List<int> skins = new();    // 가진 스킨 번호
        public int skin;                   // 입은 스킨
        public List<int> challengeCleared = new();
        public List<int> supplies = new();      // 가진 보급품 번호 (Supply_Collection)
        public List<int> slotLv = new();        // 총알 슬롯 강화 레벨 (장착 칸 3개, Weapon_Enchant)
        public bool weaponUnlocked;

        public string ticketDay = "";
        public int mineTickets, bossTickets, bossLv;

        public double buffAtkEnd, buffGoldEnd, buffAtkCool, buffGoldCool;   // 유닉스 초

        public long statKills, statStages, statMines, statBossKills, statMerges;
        public List<int> missionTier = new();   // Mission_Master 줄 순서

        public bool bgm = true, sfx = true;     // 11-07 진동 스위치 삭제 (진동 코드가 없었음)
        public string lang = "ko";              // 화면 언어 (L.Ko · L.En)
        public int adGemUsed;                   // 오늘 광고로 받은 무료 다이아 횟수 (상점, Global_Config AdGems_Daily)
        public bool tutorialDone;
        public long firstPlay;

        // 결제 (10-30) — 영구 상품은 다시 설치해도 구글에서 되살림 (Iap.Restore)
        public bool noAds;                       // 모든 광고 제거 (remove_all_ads): 광고 버튼 = 광고 없이 바로 보상
        public bool autoUpgrade, autoOn = true;  // 자동강화 패키지 (auto_upgrade) 가짐 · 켜 둠
        public List<string> iapOrders = new();   // 이미 지급한 주문 번호 (같은 결제를 두 번 주지 않게)
        public List<string> coupons = new();     // 11-12 쓴 쿠폰 번호 (대문자 · 같은 쿠폰은 한 번만 — 클라우드로 계정 단위)

        // 방치 보상 (11-04): 저장할 때마다 시각을 남김 → 다시 켜거나 백그라운드에서 돌아오면 비운 시간만큼 골드
        public long lastSeen;                    // 마지막 저장 시각 (유닉스 초, 0 = 아직 없음)
        [NonSerialized] public long loadedLastSeen;   // 켤 때 읽은 값 (켜자마자 저장해도 안 사라지게)

        // 출석 (11-04): 하루 한 번 접속 창 → 1·2·3일차 다이아를 차례로, 다 받으면 1일차부터 다시 (빠진 날이 있어도 이어서)
        public string attendDay = "";            // 마지막으로 받은 날 (yyyyMMdd, 기기 시간)
        public int attendCount;                  // 지금까지 받은 횟수 (다음 칸 = attendCount % Attend_Days)
        /// <summary>오늘 이미 받음. 11-07: 받은 날보다 "뒤의 날짜"일 때만 다시 받음 — 전엔 날짜가 다르기만 하면 줘서
        /// 폰 날짜를 내일로 → 받기 → 오늘로 되돌리면 또 받을 수 있었음 (앞당겨 받으면 그 날짜가 될 때까지 못 받음)</summary>
        public bool AttendToday => !IsLaterDay(Today, attendDay);

        // 클라우드 저장 (11-07): 같은 진행인지(saveId) · 어느 쪽이 새것인지(savedAt) 비교
        public string saveId = "";               // 이 진행의 번호 (처음 시작할 때 만듦)
        public long savedAt;                     // 마지막 저장 시각 (유닉스 초)
        /// <summary>11-12 클라우드 비교 기준: 켤 때(또는 클라우드와 마지막으로 맞춘 때) 세이브에 적혀 있던 저장 시각.
        /// savedAt 은 저장할 때마다 "지금"이 되어서, 켜자마자 출석을 받거나 자동 저장이 한 번 돌면 며칠 전 진행도 "더 새것"으로 보였음</summary>
        [NonSerialized] public long loadedSavedAt;
        /// <summary>위 값을 잡은 때 (Time.realtimeSinceStartup) — 그 뒤로 이 기기에서 얼마나 했는지</summary>
        [NonSerialized] public float loadedRealtime;

        // 11-12 세이브 판 번호: 세이브 칸을 더하거나 뜻을 바꾸면 Version 을 +1 (PlayTest 가 칸 목록 지문 FieldSig 로 잊지 않게 잡아 줌).
        //   옛 판 앱은 모르는 칸을 읽을 때 버리므로, 클라우드에서 더 높은 판의 세이브를 만나면 받지도 올리지도 않고 업데이트 안내만 (CloudSync.TooNew)
        public const int Version = 2;            // 2 = 11-12 쿠폰 칸(coupons)
        public const uint FieldSig = 2593409011;   // 11-12 coupons 칸
        public int ver;                          // 이 세이브를 마지막으로 쓴 앱의 세이브 판 (0 = 판 번호가 없던 1.0.8 까지)

        // 11-12 방치 보상 창을 띄워 놓고 못 받은 시간(초) — 창이 뜬 채로 앱이 꺼져도 다음에 켤 때 이어서 줌 (받으면 0)
        public double offlinePendingSec;

        // ---------- 불러오기·저장 ----------
        static SaveData cur;
        public static SaveData Cur => cur ??= LoadOrNew();

        // 11-07 예비 세이브: 본 세이브를 못 읽으면 예비 칸(1분마다 같은 내용을 한 벌 더)에서 불러옴 · 못 읽은 글자는 broken 칸에 남김
        public const string BakKey = Key + ".bak", BrokenKey = Key + ".broken";
        /// <summary>이번에 켤 때 본 세이브를 못 읽어 예비 세이브로 불러왔음 (HUD 가 한 번 알림)</summary>
        public static bool LoadedFromBackup;
        static float lastBak = -999f;
        /// <summary>마지막 클라우드 올리기 뒤 저장한 적이 있음 (CloudSync 가 올리고 false 로)</summary>
        public static bool Dirty;

        static SaveData LoadOrNew()
        {
            var json = PlayerPrefs.GetString(Key, "");
            SaveData d = Parse(json);
            if (d == null && !string.IsNullOrEmpty(json))
            {
                PlayerPrefs.SetString(BrokenKey, json);   // 나중에 살펴볼 수 있게 남김
                d = Parse(PlayerPrefs.GetString(BakKey, ""));
                LoadedFromBackup = d != null;
                Debug.LogWarning(d != null ? "세이브 읽기 실패 → 예비 세이브로 불러옴" : "세이브·예비 세이브 둘 다 읽기 실패 → 새로 시작");
            }
            d ??= new SaveData();
            d.Fix();
            d.loadedLastSeen = d.lastSeen;
            d.loadedSavedAt = d.savedAt;
            d.loadedRealtime = Time.realtimeSinceStartup;
            return d;
        }

        /// <summary>세이브 글자 → 데이터 + 값 정리 (못 읽으면 null) — 시험·점검용</summary>
        public static SaveData ParseFixed(string json) { var d = Parse(json); d?.Fix(); return d; }

        /// <summary>세이브 글자 → 데이터 (못 읽으면 null)</summary>
        public static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(json);
                return d != null && d.inv != null && d.slots != null ? d : null;
            }
            catch (Exception e) { Debug.LogWarning("세이브 읽기 실패: " + e.Message); return null; }
        }

        /// <summary>클라우드에서 받은 세이브로 통째로 바꿈 (저장까지) — 부르는 쪽이 씬을 다시 불러 화면을 새로 만듦</summary>
        public static bool ReplaceWith(string json)
        {
            var d = Parse(json);
            if (d == null) return false;
            d.Fix();
            // 방치 보상은 이 기기에서 마지막으로 본 시각 기준으로만 (클라우드 저장 시각으로 또 주지 않게)
            d.lastSeen = Math.Max(d.lastSeen, cur != null ? cur.lastSeen : 0);
            d.loadedLastSeen = 0;
            d.loadedSavedAt = d.savedAt;                       // 클라우드에 적혀 있던 저장 시각 (아래 Save 가 savedAt 을 지금으로 바꾸기 전)
            d.loadedRealtime = Time.realtimeSinceStartup;
            cur = d;
            d.Save();
            Dirty = false;
            return true;
        }

        public string ToJson() => JsonUtility.ToJson(this);

        static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? 0 : v;

        /// <summary>총알 번호가 쓸 수 있는 값인지 (-1 = 빈칸). 표에 없는 종류·등급이면 화면이 그 줄을 못 읽어 멈춤</summary>
        static bool ValidCode(int c) => c == -1 || (c >= 0 && c % 10 < Tables.Types.Length && Tables.Weapon(c) != null);

        void Fix()
        {
            // 11-12 값 정리: 칸 개수뿐 아니라 값이 말이 되는지도 (클라우드로 다른 기기·다른 판의 세이브가 들어올 수 있음)
            inv ??= new(); slots ??= new(); skins ??= new(); challengeCleared ??= new(); supplies ??= new();
            slotLv ??= new(); missionTier ??= new(); iapOrders ??= new();
            ticketDay ??= ""; attendDay ??= ""; saveId ??= "";
            gold = Clean(gold); gems = Clean(gems); offlinePendingSec = Clean(offlinePendingSec);
            stage = Math.Max(0, stage); kills = Math.Max(0, kills); bestEver = Math.Max(bestEver, stage);
            lvAtk = Math.Max(0, lvAtk); lvSpd = Math.Max(0, lvSpd); lvGold = Math.Max(0, lvGold);
            mineTickets = Math.Max(0, mineTickets); bossTickets = Math.Max(0, bossTickets); bossLv = Math.Max(0, bossLv);
            attendCount = Math.Max(0, attendCount); adGemUsed = Math.Max(0, adGemUsed);

            while (inv.Count < InvSize) inv.Add(-1);
            while (slots.Count < SlotCount) slots.Add(-1);
            if (slots.Count > SlotCount) slots.RemoveRange(SlotCount, slots.Count - SlotCount);
            Tables.Load();
            if (Tables.Weapons.Count > 0)   // 표를 못 읽었을 때 총알을 다 지우지 않게
            {
                for (int i = 0; i < inv.Count; i++) if (!ValidCode(inv[i])) inv[i] = -1;
                for (int i = 0; i < slots.Count; i++) if (!ValidCode(slots[i])) slots[i] = -1;
            }
            var eq = slots.FindAll(x => x >= 0);                // 리더 칸(0)이 비지 않게 앞으로 당김 (GameManager.CompactSlots 와 같음)
            for (int i = 0; i < slots.Count; i++) slots[i] = i < eq.Count ? eq[i] : -1;
            while (slotLv.Count < SlotCount) slotLv.Add(0);
            if (!skins.Contains(0)) skins.Insert(0, 0);
            if (!skins.Contains(skin)) skin = 0;                // 가진 목록에 없는 코스튬을 입고 있으면 기본으로
            bool any = false;
            foreach (var s in slots) if (s >= 0) any = true;
            if (!any) slots[0] = Tables.Code(0, "single");   // 처음 = D 리볼버 하나
            while (missionTier.Count < 8) missionTier.Add(0);
            for (int i = 0; i < missionTier.Count; i++) if (missionTier[i] < 0) missionTier[i] = 0;
            if (firstPlay == 0) firstPlay = Now;
            if (lang != L.Ko && lang != L.En) lang = L.Ko;
            if (string.IsNullOrEmpty(saveId)) saveId = Guid.NewGuid().ToString("N");
            coupons ??= new();
        }

        static float lastFlush = -999f;
        static bool flushSoon;

        /// <summary>11-12 다이아를 쓰거나 받은 직후: 메모리에 바로 저장하고, 저장소 쓰기는 다음 자동 저장(5초 안) 때 —
        /// 뽑기를 연달아 눌러도 저장소를 매번 쓰지 않으면서, 앱이 갑자기 죽어도 잃는 건 몇 초</summary>
        public void SaveSoon() { flushSoon = true; Save(false); }

        /// <summary>flush = 폰 저장소에 바로 씀. 11-08: 5초마다 하는 자동 저장은 flush=false — 저장소 쓰기는 30초마다만
        /// (앱을 나가거나 끌 때 · 보상·구매 같은 저장은 그대로 바로 씀)</summary>
        public void Save(bool flush = true)
        {
            // 11-07: 마지막으로 본 시각은 앞으로만 — 폰 시간을 뒤로 돌렸다 되돌리면 오래 비운 것처럼 계산돼 방치 8시간을 계속 받았음
            lastSeen = Math.Max(lastSeen, Now);
            savedAt = Now;
            ver = Math.Max(ver, Version);
            string json = ToJson();
            PlayerPrefs.SetString(Key, json);
            float t = Time.realtimeSinceStartup;
            if (t - lastBak >= 60f || t < lastBak) { PlayerPrefs.SetString(BakKey, json); lastBak = t; }
            if (flush || t - lastFlush >= (flushSoon ? 2f : 30f) || t < lastFlush) { PlayerPrefs.Save(); lastFlush = t; flushSoon = false; }
            Dirty = true;
        }

        /// <summary>시험용: 다음 Cur 때 PlayerPrefs 에서 다시 읽음 (예비 세이브 시험)</summary>
        public static void ReloadForTest() { cur = null; LoadedFromBackup = false; }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BakKey);
            PlayerPrefs.DeleteKey(BrokenKey);
            PlayerPrefs.Save();
            cur = null;
            lastBak = -999f;
            LoadedFromBackup = false;
        }

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static double NowD => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        /// <summary>오늘 날짜 yyyyMMdd (기기 시간, 어느 나라 설정이든 같은 모양)</summary>
        public static string Today
        {
            get
            {
                // 11-08: 날짜가 바뀔 때만 글자를 만듦 (화면이 0.1초마다 물어봄)
                var n = DateTime.Now;
                int k = n.Year * 10000 + n.Month * 100 + n.Day;
                if (k != todayKey) { todayKey = k; todayStr = n.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture); }
                return todayStr;
            }
        }
        static int todayKey = -1;
        static string todayStr;
        /// <summary>a 가 b 보다 뒤의 날짜인지 (yyyyMMdd 글자 비교, b 가 비었으면 true)</summary>
        public static bool IsLaterDay(string a, string b) => string.CompareOrdinal(a, b ?? "") > 0;

        /// <summary>하루가 바뀌었으면 던전 티켓·광고 다이아 횟수를 채운다 (기기 시간 기준).
        /// 11-07: 마지막으로 채운 날보다 뒤의 날짜일 때만 — 날짜를 앞뒤로 바꿔 다시 채우는 것을 막음</summary>
        public void RefreshDaily()
        {
            string today = Today;
            if (!IsLaterDay(today, ticketDay)) return;
            ticketDay = today;
            adGemUsed = 0;
            mineTickets = Math.Max(mineTickets, Tables.Ci("MineTickets"));
            bossTickets = Math.Max(bossTickets, Tables.Ci("BossRaidTickets"));
        }

        /// <summary>11-07: 폰 시간이 뒤로 고쳐졌을 때 광고 버프의 남은 시간·다시 보기 대기가 표의 최대보다 길면 최대로 줄임
        /// (하루 앞서 있던 시계가 바로잡히면 "86400초 뒤에 다시"가 떴음)</summary>
        public void ClampTimers()
        {
            double now = NowD;
            var a = Tables.Buff("buff_atk"); var g = Tables.Buff("buff_gold");
            if (a.maxStack > 0) buffAtkEnd = Math.Min(buffAtkEnd, now + a.maxStack);
            if (g.maxStack > 0) buffGoldEnd = Math.Min(buffGoldEnd, now + g.maxStack);
            buffAtkCool = Math.Min(buffAtkCool, now + Math.Max(0, a.cooldown));
            buffGoldCool = Math.Min(buffGoldCool, now + Math.Max(0, g.cooldown));
        }

        public int Chapter => stage / 5 + 1;
        public int Sub => stage % 5 + 1;
        public static string StageName(int idx) => $"{idx / 5 + 1}-{idx % 5 + 1}";
    }
}
