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
        public const int InvSize = 15, SlotCount = 3;

        public double gold, gems;
        public int stage;            // 서브 스테이지 번호 0 = 1-1, 4 = 1-5, 5 = 2-1 …
        public int kills;            // 지금 서브 스테이지에서 잡은 수
        public int bestEver;         // 가장 멀리 간 스테이지 (기능 열림 기준)
        public int lvAtk, lvSpd, lvGold;
        public bool bossFarm;        // 보스 실패 → 지금 스테이지 반복 사냥 중 ([보스 도전] 누르면 다시)

        public List<int> inv = new();      // 15칸, -1 = 빈칸, 그 외 총알 코드(Tables.Code)
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

        public bool bgm = true, sfx = true, vibe = true;
        public bool tutorialDone;
        public long firstPlay;

        // ---------- 불러오기·저장 ----------
        static SaveData cur;
        public static SaveData Cur => cur ??= LoadOrNew();

        static SaveData LoadOrNew()
        {
            SaveData d = null;
            var json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { d = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception e) { Debug.LogWarning("세이브 읽기 실패, 새로 시작: " + e.Message); }
            }
            d ??= new SaveData();
            d.Fix();
            return d;
        }

        void Fix()
        {
            while (inv.Count < InvSize) inv.Add(-1);
            while (slots.Count < SlotCount) slots.Add(-1);
            while (slotLv.Count < SlotCount) slotLv.Add(0);
            if (!skins.Contains(0)) skins.Insert(0, 0);
            bool any = false;
            foreach (var s in slots) if (s >= 0) any = true;
            if (!any) slots[0] = Tables.Code(0, "single");   // 처음 = D 리볼버 하나
            while (missionTier.Count < 8) missionTier.Add(0);
            if (firstPlay == 0) firstPlay = Now;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            cur = null;
        }

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static double NowD => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        /// <summary>하루가 바뀌었으면 던전 티켓을 채운다 (기기 시간 기준)</summary>
        public void RefreshDaily()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (ticketDay == today) return;
            ticketDay = today;
            mineTickets = Math.Max(mineTickets, Tables.Ci("MineTickets"));
            bossTickets = Math.Max(bossTickets, Tables.Ci("BossRaidTickets"));
        }

        public int Chapter => stage / 5 + 1;
        public int Sub => stage % 5 + 1;
        public static string StageName(int idx) => $"{idx / 5 + 1}-{idx % 5 + 1}";
    }
}
