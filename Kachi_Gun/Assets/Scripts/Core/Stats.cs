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
        public static double SkinAtk { get { double s = 0; foreach (var i in S.skins) s += Skin(i)?.atk ?? 0; return s; } }
        public static double SkinSpd { get { double s = 0; foreach (var i in S.skins) s += Skin(i)?.spd ?? 0; return s; } }
        public static double SkinGold { get { double s = 0; foreach (var i in S.skins) s += Skin(i)?.gold ?? 0; return s; } }
        static SkinRow Skin(int id) { foreach (var r in Tables.Skins) if (r.id == id) return r; return null; }

        // ---------- 보급품 보유 효과 (가진 것 전부 합산, %) ----------
        public static double SupplyAtk { get { double s = 0; foreach (var r in OwnedSupplies()) s += r.atk; return s; } }
        public static double SupplySpd { get { double s = 0; foreach (var r in OwnedSupplies()) s += r.spd; return s; } }
        public static double SupplyGold { get { double s = 0; foreach (var r in OwnedSupplies()) s += r.gold; return s; } }
        public static double SupplyBoss { get { double s = 0; foreach (var r in OwnedSupplies()) s += r.boss; return s; } }
        static System.Collections.Generic.IEnumerable<SupplyRow> OwnedSupplies()
        {
            foreach (var r in Tables.Supplies) if (S.supplies.Contains(r.id)) yield return r;
        }

        // ---------- 총알 슬롯 강화 (장착 칸마다, 효과 = 표 값 x 장착한 총알 등급 배율) ----------
        /// <summary>그 칸에 장착한 총알 등급이 허용하는 최대 강화 레벨</summary>
        public static int SlotMax(int slot)
        {
            int code = S.slots[slot];
            int g = code >= 0 ? Tables.GradeOf(code) : 0;
            return Math.Min(Tables.EnchantTop, Tables.Grade(g).maxEnchant);
        }
        /// <summary>실제로 적용되는 강화 레벨 (낮은 등급 총알로 바꾸면 그 등급 최대까지만)</summary>
        public static int SlotLvEff(int slot) => Math.Min(S.slotLv[slot], SlotMax(slot));
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
        public static double ZombieHp(int idx) => Tables.Stage(idx).hp;
        public static double ZombieGold(int idx) => Tables.Stage(idx).gold;
    }
}
