using System;

namespace KachiGun
{
    /// <summary>
    /// 강화·스킨·보급품·슬롯 강화·버프를 합친 최종 수치.
    /// 공격력 = 총알 공격력 x 골드 강화(Gold_Upgrade) x (1+스킨 합) x (1+보급품 합) x 슬롯 강화 x 버프
    /// 보스 피해 = 위 x (1 + 보급품 보스 피해 + 슬롯 강화 보스 피해)
    /// </summary>
    public static class Stats
    {
        static SaveData S => SaveData.Cur;

        // ---------- 하단 골드 강화 3종 (Gold_Upgrade 표, % 값) ----------
        public static double Cost(int lv) => Math.Round(Tables.Upgrade(lv).cost);
        public static double Power(int lv) => Tables.Upgrade(lv).atk;
        public static double Speed(int lv) => Tables.Upgrade(lv).spd;
        public static double Money(int lv) => Tables.Upgrade(lv).gold;

        // ---------- 스킨 보유 효과 (가진 것 전부 합산, %) ----------
        public static double SkinAtk { get { Sums(); return sAtk; } }
        public static double SkinSpd { get { Sums(); return sSpd; } }
        public static double SkinGold { get { Sums(); return sGold; } }
        static SkinRow Skin(int id) { foreach (var r in Tables.Skins) if (r.id == id) return r; return null; }

        // ---------- 보급품 보유 효과 (가진 것 전부 합산, %) ----------
        public static double SupplyAtk { get { Sums(); return pAtk; } }
        public static double SupplySpd { get { Sums(); return pSpd; } }
        public static double SupplyGold { get { Sums(); return pGold; } }
        public static double SupplyBoss { get { Sums(); return pBoss; } }

        // 11-08: 스킨·보급품 합은 가진 목록이 바뀔 때만 다시 셈 (전엔 총알 한 발·좀비 한 마리마다 표 전체를 돌았음)
        //        목록 내용으로 지문을 만들어 비교 — 개수가 같아도 내용이 바뀌면(세이브 바꾸기·시험) 다시 셈
        static SaveData sumOf; static long sumKey = long.MinValue;
        static double sAtk, sSpd, sGold, pAtk, pSpd, pGold, pBoss;
        static void Sums()
        {
            var s = S;
            long k = 17;
            foreach (var i in s.skins) k = k * 31 + i + 1;
            k = k * 131 + 7;
            foreach (var i in s.supplies) k = k * 31 + i + 1;
            if (ReferenceEquals(s, sumOf) && k == sumKey) return;
            sumOf = s; sumKey = k;
            sAtk = sSpd = sGold = 0;
            foreach (var i in s.skins) { var r = Skin(i); if (r == null) continue; sAtk += r.atk; sSpd += r.spd; sGold += r.gold; }
            pAtk = pSpd = pGold = pBoss = 0;
            foreach (var r in Tables.Supplies) if (s.supplies.Contains(r.id)) { pAtk += r.atk; pSpd += r.spd; pGold += r.gold; pBoss += r.boss; }
        }

        // ---------- 총알 슬롯 강화 (장착 칸마다, 효과 = 표 값 x 장착한 총알 등급 배율) ----------
        /// <summary>그 칸에 장착한 총알 등급이 허용하는 최대 강화 레벨</summary>
        public static int SlotMax(int slot)
        {
            int code = S.slots[slot];
            int g = code >= 0 ? Tables.GradeOf(code) : 0;
            return Math.Min(Tables.EnchantTop, Tables.Grade(g).maxEnchant);
        }
        /// <summary>11-05 밤 사용자 "슬롯 강화 기능은 제거": 꺼 두면 강화 버튼·+N 표시가 없고, 옛 세이브의 강화 레벨도 효과 0.
        /// 코드·Weapon_Enchant 표는 남겨 둠 (true 로 바꾸면 효과는 돌아오지만 강화 버튼은 다시 만들어야 함)</summary>
        public static readonly bool SlotEnchantOn = false;
        /// <summary>실제로 적용되는 강화 레벨 (낮은 등급 총알로 바꾸면 그 등급 최대까지만)</summary>
        public static int SlotLvEff(int slot) => SlotEnchantOn ? Math.Min(S.slotLv[slot], SlotMax(slot)) : 0;
        static double SlotGradeMul(int slot) { int c = S.slots[slot]; return Tables.Grade(c >= 0 ? Tables.GradeOf(c) : 0).enchantMul; }
        public static double SlotAtkPct(int slot) => Tables.Enchant(SlotLvEff(slot)).atk * SlotGradeMul(slot);
        public static double SlotSpdPct(int slot) => Tables.Enchant(SlotLvEff(slot)).spd * SlotGradeMul(slot);
        public static double SlotBossPct(int slot) => Tables.Enchant(SlotLvEff(slot)).boss * SlotGradeMul(slot);

        // ---------- 버프 (Buff_Table) ----------
        public static bool BuffAtk => S.buffAtkEnd > SaveData.NowD;
        public static bool BuffGold => S.buffGoldEnd > SaveData.NowD;

        // ---------- 최종 ----------
        public static double AtkMul => Power(S.lvAtk) / 100.0 * (1 + SkinAtk / 100) * (1 + SupplyAtk / 100) * (BuffAtk ? Tables.Buff("buff_atk").mul : 1);
        public static double SpdMul => Speed(S.lvSpd) / 100.0 * (1 + SkinSpd / 100) * (1 + SupplySpd / 100);
        public static double GoldMul => Money(S.lvGold) / 100.0 * (1 + SkinGold / 100) * (1 + SupplyGold / 100) * (BuffGold ? Tables.Buff("buff_gold").mul : 1);

        /// <summary>slot = 쏘는 카피바라의 장착 칸 (-1 = 슬롯 강화 없음). 공속 상한을 넘은 만큼 1:1 로 곱함</summary>
        public static double Damage(WeaponRow w, int slot = -1) => w.atk * AtkMul * (slot >= 0 ? 1 + SlotAtkPct(slot) / 100 : 1) * SpeedOverflow(w, slot);
        public static double BossMul(int slot = -1) => 1 + (SupplyBoss + (slot >= 0 ? SlotBossPct(slot) : 0)) / 100;

        static float MinInterval => Tables.P("Min_Fire_Interval", 0.06f);
        /// <summary>상한 없이 계산한 발사 간격</summary>
        static double RawInterval(WeaponRow w, int slot) => w.interval / (SpdMul * (slot >= 0 ? 1 + SlotSpdPct(slot) / 100 : 1));
        /// <summary>실제 발사 간격 (최소 Min_Fire_Interval)</summary>
        public static float Interval(WeaponRow w, int slot = -1) => Math.Max(MinInterval, (float)RawInterval(w, slot));
        /// <summary>공속이 상한(Min_Fire_Interval)을 넘으면 넘친 배율 그대로 공격력 배율로 (1 = 안 넘침). 초당 피해가 상한 없이 공속에 비례</summary>
        public static double SpeedOverflow(WeaponRow w, int slot = -1)
        {
            double raw = RawInterval(w, slot);
            return raw < MinInterval ? MinInterval / raw : 1;
        }

        // ---------- 스테이지 (Stage_Table) ----------
        /// <summary>돈 배율에서 잠깐 켜는 골드 버프를 뺀 값 (방치 보상용)</summary>
        public static double GoldMulBase => GoldMul / (BuffGold ? Tables.Buff("buff_gold").mul : 1);

        /// <summary>분대 초당 피해 (장착 3칸 합, 버프 뺌) — 방치 보상의 처치 속도 계산용 (balance_sim.py 의 dps 와 같은 생각)</summary>
        public static double SquadDps()
        {
            double d = 0;
            for (int i = 0; i < S.slots.Count; i++)
            {
                var w = Tables.Weapon(S.slots[i]);
                if (w == null) continue;
                d += Damage(w, i) * Math.Max(1, w.count) / Math.Max(0.01f, Interval(w, i));
            }
            if (BuffAtk) d /= Tables.Buff("buff_atk").mul;
            return d;
        }

        public static double ZombieHp(int idx) => Tables.Stage(idx).hp;
        public static double ZombieGold(int idx) => Tables.Stage(idx).gold;
    }
}
