using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KachiGun
{
    // ---------- 표 한 줄씩 (칸 이름은 xlsx 그대로, 뒤의 "(초)" 같은 단위 표시만 떼고 읽음) ----------
    public class WeaponRow
    {
        public int code, grade; public string type, name;
        public double atk; public float interval, spread, splash, range; public int count, pierce;
    }
    public class GradeRow { public int idx; public string name; public Color color; public int maxEnchant; public double enchantMul = 1, salvage; }
    public class BulletRow { public string type, kind, sprite, icon; public float speed, maxSpeed, accel, turn, hitBonus, rangeBonus, jitter; }
    public class SkinRow { public int id, recChapter; public string name, sprite; public double price, atk, spd, gold; }
    public class ZombieRow
    {
        public string id, name; public double hpMul, goldMul; public int minChapter;
        public float speed, speedMin, speedMax, scale, weight, stopMin, stopMax, height, radius, shadowW, shadowH;
    }
    public class MissionRow
    {
        public string id, name, statKey; public double baseReward, addPer10;
        public long[] targets;                  // 단계별 목표 (Mission_Tier 표에 칸이 있으면 그것, 없으면 Mission_Master 미리 정한 값)
        public double extraMul = 2; public long extraAdd;   // 표 끝 너머 단계
    }
    public class StageRow
    {
        public int idx, chapter, sub, quota, maxAlive; public bool boss; public string bossId;
        public float spawn, bossScale, bossTime;
        public double hp, gold, hpGrowth, goldGrowth, bossHp, bossGold;
    }
    public class ChapterRow { public int chapter; public string biome; public Color tint; }
    public class UpgradeRow { public int lv; public double cost, costGrowth, atk, statGrowth, spd, spdAdd, spdGrowth, gold, goldGrowth; }   // atk·spd·gold = %, spdGrowth > 0 = 공속도 곱으로 오름
    public class ChallengeRow { public int chapter, count; public float time; public double hp, reward; }
    public class BossRaidRow { public int lv, recChapter; public string bossId; public double hp, reward, rewardAdd; public float time, scale; }
    public class OreRow { public string kind, name, sprite; public double hp, reward; public int count; public float scale, radius, near, far; }
    public class MineTierRow { public int tier, chapter; public double mul; }
    public class BuffRow { public string id, name; public double mul, duration, maxStack, cooldown; }
    public class ShopRow { public int index; public string id, category, name, desc, priceType, priceValue, reward; public double bonusGems; }
    public class UnlockRow { public string id, name, type, lockMsg, bannerMsg; public int stageIdx; public double value; }
    public class SupplyRow { public int id, unlockChapter; public string grade, name; public double cost, atk, spd, gold, boss; }
    public class EnchantRow { public int lv; public double gems, gold, atk, spd, boss, rate; }

    /// <summary>
    /// 기획 표 (원본 = Kachi_Gun/Table/*.xlsx → [카치건 → 표 변환] 또는 재생할 때 자동으로 Resources/Tables/*.csv).
    /// 시트 하나 = csv 하나. 표 끝(750 스테이지·강화 1500 레벨 등)을 넘으면 마지막 줄의 성장률로 이어서 계산.
    /// </summary>
    public static class Tables
    {
        static bool loaded;
        static readonly Dictionary<string, double> config = new();
        static readonly Dictionary<string, string> player = new();
        static readonly Dictionary<int, WeaponRow> weapons = new();
        public static readonly List<WeaponRow> Weapons = new();
        static readonly List<GradeRow> grades = new();
        static string[] gradeNames = { "D" };
        static readonly Dictionary<string, BulletRow> bullets = new();
        public static readonly List<SkinRow> Skins = new();
        public static readonly List<ZombieRow> Zombies = new();
        public static readonly List<MissionRow> Missions = new();
        static readonly List<StageRow> stages = new();
        static readonly List<ChapterRow> chapters = new();
        static readonly List<UpgradeRow> upgrades = new();
        static readonly List<ChallengeRow> challenges = new();
        static readonly List<BossRaidRow> bossRaids = new();
        public static readonly List<OreRow> Ores = new();
        static readonly List<MineTierRow> mineTiers = new();
        static readonly Dictionary<string, BuffRow> buffs = new();
        public static readonly List<ShopRow> Shop = new();
        public static readonly List<UnlockRow> Unlocks = new();
        public static readonly List<SupplyRow> Supplies = new();
        static readonly List<EnchantRow> enchants = new();

        public static void Load()
        {
            if (loaded) return;
            loaded = true;

            foreach (var r in Read("Global_Config")) config[r.S("Key")] = r.D("Value");
            foreach (var r in Read("Player_Base")) player[r.S("Param_Key")] = r.S("Value");

            // 총알 등급 · 종류
            foreach (var r in Read("Weapon_Grade_Growth"))
                grades.Add(new GradeRow { idx = r.I("Grade_Idx"), name = r.S("Grade_Name"), color = r.Hex("Grade_Color_Hex", Color.white) });
            grades.Sort((a, b) => a.idx.CompareTo(b.idx));
            foreach (var r in Read("Grade_Enchant_Multiplier"))
            {
                var g = grades.Find(x => x.idx == r.I("Grade_Idx"));
                if (g == null) continue;
                g.maxEnchant = r.I("Max_Enchant_Lv"); g.enchantMul = r.D("Enchant_Effect_Mul", 1); g.salvage = r.D("Salvage_Gems_Reward");
            }
            if (grades.Count > 0) gradeNames = grades.ConvertAll(g => g.name).ToArray();
            foreach (var r in Read("Weapon_Master"))
            {
                var w = new WeaponRow
                {
                    code = r.I("Code"), grade = r.I("Grade_Idx"), type = r.S("Type"), name = r.S("Name"), atk = r.D("Atk"), interval = r.F("Interval"),
                    count = Math.Max(1, r.I("Count")), spread = r.F("Spread"), pierce = Math.Max(1, r.I("Pierce")), splash = r.F("Splash"), range = r.F("Range")
                };
                Weapons.Add(w);
                weapons[Code(w.grade, w.type)] = w;
            }
            foreach (var r in Read("Bullet_Spec"))
                bullets[r.S("Type")] = new BulletRow
                {
                    type = r.S("Type"), kind = r.S("Bullet_Kind"), speed = r.F("Base_Speed"), maxSpeed = r.F("Max_Speed"), accel = r.F("Accel"),
                    turn = r.F("Homing_TurnRate"), hitBonus = r.F("Hit_Radius_Bonus"), rangeBonus = r.F("Range_Bonus"), jitter = r.F("Random_Angle_Jitter"),
                    sprite = r.S("Sprite_Path"), icon = r.S("Icon_Path")
                };

            foreach (var r in Read("Skin_Table"))
                Skins.Add(new SkinRow
                {
                    id = r.I("Id"), name = r.S("Name"), recChapter = r.I("Recommended_Chapter"), price = r.D("Price_Gems"),
                    atk = r.D("Atk_Bonus"), spd = r.D("Spd_Bonus"), gold = r.D("Gold_Bonus"), sprite = r.S("Char_Sprite")
                });

            foreach (var r in Read("Zombie_Master"))
            {
                float sp = r.F("Speed");
                Zombies.Add(new ZombieRow
                {
                    id = r.S("Id"), name = r.S("Name"), minChapter = r.I("MinChapter"), weight = r.F("Base_Weight"),
                    hpMul = r.D("HpMul", 1), goldMul = r.D("GoldMul", 1), speed = sp,
                    speedMin = r.F("Actual_Speed_Min", sp * 0.935f), speedMax = r.F("Actual_Speed_Max", sp * 1.265f), scale = r.F("Scale", 1),
                    stopMin = r.F("StopDist_Min", 2f), stopMax = r.F("StopDist_Max", 4.6f), height = r.F("Height", 1.35f), radius = r.F("Radius", 0.5f),
                    shadowW = r.F("Shadow_W", 0.8f), shadowH = r.F("Shadow_H", 0.22f)
                });
            }

            // 스테이지 · 챕터 · 강화
            foreach (var r in Read("Stage_Table"))
                stages.Add(new StageRow
                {
                    idx = r.I("Stage_Idx"), chapter = r.I("Chapter"), sub = r.I("Sub_Stage"), boss = r.S("Is_Boss_Stage").ToUpper() == "Y",
                    quota = r.I("Kills_Quota", 20), maxAlive = r.I("Max_Alive", 8), spawn = r.F("Spawn_Interval", 0.7f),
                    hp = r.D("Base_Zombie_HP"), gold = r.D("Base_Zombie_Gold"), hpGrowth = r.D("Hp_Growth_Rate", 1), goldGrowth = r.D("Gold_Growth_Rate", 1),
                    bossId = r.S("Boss_Id"), bossHp = r.D("Boss_HP"), bossGold = r.D("Boss_Gold"), bossScale = r.F("Boss_Scale", 1.7f), bossTime = r.F("Boss_Time_Sec", 30)
                });
            stages.Sort((a, b) => a.idx.CompareTo(b.idx));
            foreach (var r in Read("Chapter_Summary"))
                chapters.Add(new ChapterRow { chapter = r.I("Chapter"), biome = r.S("Biome_Name"), tint = r.Hex("Map_Tint_Hex", Color.white) });
            chapters.Sort((a, b) => a.chapter.CompareTo(b.chapter));
            foreach (var r in Read("Gold_Upgrade"))
                upgrades.Add(new UpgradeRow
                {
                    lv = r.I("Lv"), cost = r.D("Upgrade_Cost"), costGrowth = r.D("Cost_Growth_Rate", 1),
                    atk = r.D("Atk_Power"), statGrowth = r.D("Stat_Growth_Rate", 1), spd = r.D("Atk_Speed"), spdAdd = r.D("Spd_Add_Per_Lv"), spdGrowth = r.D("Spd_Growth_Rate"),
                    gold = r.D("Money_Gain"), goldGrowth = r.D("Gold_Growth_Rate", 1)
                });
            upgrades.Sort((a, b) => a.lv.CompareTo(b.lv));

            // 던전
            foreach (var r in Read("Challenge"))
                challenges.Add(new ChallengeRow
                {
                    chapter = r.I("Challenge_Chapter"), time = r.F("Time_Limit", 60), count = r.I("Zombie_Count", 24),
                    hp = r.D("Base_Zombie_HP"), reward = r.D("Reward_Gems")
                });
            challenges.Sort((a, b) => a.chapter.CompareTo(b.chapter));
            foreach (var r in Read("Boss_Dungeon"))
                bossRaids.Add(new BossRaidRow
                {
                    lv = r.I("Save_bossLv"), recChapter = r.I("Recommended_Chapter"), bossId = r.S("Boss_Id"), hp = r.D("Boss_HP"),
                    time = r.F("Time_Limit", 60), scale = r.F("Boss_Scale", 2.3f), reward = r.D("Reward_Gems"), rewardAdd = r.D("Reward_Add_Gems")
                });
            bossRaids.Sort((a, b) => a.lv.CompareTo(b.lv));
            foreach (var r in Read("Mine_Ore_Spec"))
                Ores.Add(new OreRow
                {
                    kind = r.S("Ore_Kind"), name = r.S("Name_KR"), hp = r.D("Hit_HP"), reward = r.D("Base_Reward_Gems"), count = r.I("Spawn_Count"),
                    scale = r.F("Scale", 1), radius = r.F("Hitbox_Radius", 0.55f), near = r.F("Spawn_X_Near"), far = r.F("Spawn_X_Far", 1), sprite = r.S("Sprite_Path")
                });
            foreach (var r in Read("Mine_Scaling"))
                mineTiers.Add(new MineTierRow { tier = mineTiers.Count + 1, chapter = r.I("Unlock_Chapter"), mul = r.D("Reward_Multiplier", 1) });
            mineTiers.Sort((a, b) => a.chapter.CompareTo(b.chapter));

            // 미션
            var tierRows = Read("Mission_Tier");
            foreach (var r in Read("Mission_Master"))
            {
                var m = new MissionRow
                {
                    id = r.S("Mission_Id"), name = r.S("Mission_Name"), statKey = r.S("Save_Stat_Key"),
                    baseReward = r.D("Base_Reward_Gems"), addPer10 = r.D("Reward_Add_Per_10Tier"), extraMul = r.D("Default_Growth_Rate", 2)
                };
                string col = Title(m.id) + "_Target";        // kill → Kill_Target
                if (tierRows.Count > 0 && tierRows[0].Has(col))
                {
                    m.targets = tierRows.ConvertAll(t => (long)Math.Round(t.D(col))).ToArray();
                    var last = tierRows[^1];
                    if (last.Has(Title(m.id) + "_Growth_Rate")) m.extraMul = last.D(Title(m.id) + "_Growth_Rate");
                    else if (last.Has(Title(m.id) + "_Add_Step")) m.extraAdd = Math.Max(1, (long)last.D(Title(m.id) + "_Add_Step"));
                }
                else m.targets = ParseLongs(r.S("Tier_1_7_Preset"));
                if (m.targets.Length == 0) m.targets = new long[] { 1 };
                Missions.Add(m);
            }

            foreach (var r in Read("Buff_Table"))
                buffs[r.S("Buff_Id")] = new BuffRow
                {
                    id = r.S("Buff_Id"), name = r.S("Name"), mul = r.D("Multiplier", 1), duration = r.D("Duration_Sec"),
                    maxStack = r.D("Max_Stack_Sec"), cooldown = r.D("Cooldown_Sec")
                };
            foreach (var r in Read("Shop_Products"))
                Shop.Add(new ShopRow
                {
                    index = r.I("Index"), id = r.S("Product_Id"), category = r.S("Category"), name = r.S("Name"), desc = r.S("Description"),
                    priceType = r.S("Price_Type"), priceValue = r.S("Price_Value"), bonusGems = r.D("Bonus_Gems"), reward = r.S("Reward_Multiplier_Or_Item")
                });
            Shop.Sort((a, b) => a.index.CompareTo(b.index));
            foreach (var r in Read("Unlock_Roadmap"))
                Unlocks.Add(new UnlockRow
                {
                    id = r.S("Content_Id"), name = r.S("Content_Name"), type = r.S("Unlock_Condition_Type"), stageIdx = r.I("Unlock_Stage_Idx"),
                    value = r.D("Unlock_Value"), lockMsg = r.S("Lock_Toast_Message"), bannerMsg = r.S("Unlock_Banner_Message")
                });
            foreach (var r in Read("Supply_Collection"))
                Supplies.Add(new SupplyRow
                {
                    id = r.I("Supply_Id"), grade = r.S("Grade"), name = r.S("Name"), unlockChapter = r.I("Unlock_Chapter"), cost = r.D("Cost_Gems"),
                    atk = r.D("Atk_Bonus"), spd = r.D("Spd_Bonus"), gold = r.D("Gold_Bonus"), boss = r.D("Boss_Dmg_Bonus")
                });
            foreach (var r in Read("Weapon_Enchant"))
                enchants.Add(new EnchantRow
                {
                    lv = r.I("Enchant_Lv"), gems = r.D("Cost_Gems"), gold = r.D("Cost_Gold"), atk = r.D("Atk_Bonus_Pct"),
                    spd = r.D("Spd_Bonus_Pct"), boss = r.D("Boss_Dmg_Bonus_Pct"), rate = r.D("Success_Rate", 100)
                });
            enchants.Sort((a, b) => a.lv.CompareTo(b.lv));
        }

        // =====================================================================
        // 설정값 (Global_Config · Player_Base)
        // =====================================================================
        public static double C(string key)
        {
            Load();
            if (config.TryGetValue(key, out var v)) return v;
            Debug.LogError("Global_Config 에 없는 키: " + key);
            return 0;
        }
        public static float Cf(string key) => (float)C(key);
        public static int Ci(string key) => (int)Math.Round(C(key));

        /// <summary>Player_Base 숫자 (없으면 def)</summary>
        public static float P(string key, float def)
        {
            Load();
            return player.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        /// <summary>Player_Base "0.13, 0.512" 같은 두 수</summary>
        public static Vector2 PV(string key, Vector2 def)
        {
            Load();
            if (!player.TryGetValue(key, out var s)) return def;
            var p = s.Split(',');
            if (p.Length < 2) return def;
            bool ok = float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    & float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
            return ok ? new Vector2(x, y) : def;
        }

        // =====================================================================
        // 스테이지 · 챕터
        // =====================================================================
        public static StageRow Stage(int idx)
        {
            Load();
            if (idx < 0) idx = 0;
            if (stages.Count == 0) return new StageRow { idx = idx, quota = 20, maxAlive = 8, spawn = 0.7f, hp = 150, gold = 30, bossTime = 30, bossScale = 1.7f, bossId = "ogre" };
            if (idx < stages.Count && stages[idx].idx == idx) return stages[idx];
            var found = stages.Find(s => s.idx == idx);
            if (found != null) return found;
            // 표 끝 너머: 마지막 챕터의 같은 서브 스테이지에서 마지막 성장률로 늘림
            var last = stages[^1];
            int sub = idx % 5 + 1;
            var b = stages.FindLast(s => s.sub == sub) ?? last;
            int n = idx - b.idx;
            double hg = Math.Pow(last.hpGrowth, n), gg = Math.Pow(last.goldGrowth, n);
            return new StageRow
            {
                idx = idx, chapter = idx / 5 + 1, sub = sub, boss = b.boss, bossId = b.bossId, quota = b.quota, maxAlive = b.maxAlive, spawn = b.spawn,
                hp = b.hp * hg, gold = b.gold * gg, hpGrowth = last.hpGrowth, goldGrowth = last.goldGrowth,
                bossHp = b.bossHp * hg, bossGold = b.bossGold * gg, bossScale = b.bossScale, bossTime = b.bossTime
            };
        }

        public static ChapterRow Chapter(int ch)
        {
            Load();
            if (chapters.Count == 0) return new ChapterRow { chapter = ch, biome = "", tint = Color.white };
            var r = chapters.Find(c => c.chapter == ch);
            return r ?? chapters[(Math.Max(1, ch) - 1) % chapters.Count];
        }

        /// <summary>하단 골드 강화 Lv 줄 (atk·spd·gold = %, 공격력·공격속도·돈 획득량 셋이 같은 줄을 씀)</summary>
        public static UpgradeRow Upgrade(int lv)
        {
            Load();
            if (lv < 0) lv = 0;
            if (upgrades.Count == 0) return new UpgradeRow { lv = lv, cost = 50 * Math.Pow(1.38, lv), atk = 100, spd = 100, gold = 100 };
            if (lv < upgrades.Count && upgrades[lv].lv == lv) return upgrades[lv];
            var last = upgrades[^1];
            if (lv < last.lv) return upgrades.Find(u => u.lv == lv) ?? last;
            int n = lv - last.lv;
            return new UpgradeRow
            {
                lv = lv, costGrowth = last.costGrowth, statGrowth = last.statGrowth, spdAdd = last.spdAdd, spdGrowth = last.spdGrowth, goldGrowth = last.goldGrowth,
                cost = last.cost * Math.Pow(last.costGrowth, n), atk = last.atk * Math.Pow(last.statGrowth, n),
                spd = last.spdGrowth > 0 ? last.spd * Math.Pow(last.spdGrowth, n) : last.spd + last.spdAdd * n, gold = last.gold * Math.Pow(last.goldGrowth, n)
            };
        }

        // =====================================================================
        // 던전
        // =====================================================================
        public static ChallengeRow Challenge(int ch)
        {
            Load();
            var r = challenges.Find(c => c.chapter == ch);
            if (r != null) return r;
            if (challenges.Count == 0) return new ChallengeRow { chapter = ch, time = 60, count = 24, hp = Stage((ch - 1) * 5).hp * 1.2, reward = 30 };
            var last = challenges[^1];
            double k = Stage((ch - 1) * 5).hp / Math.Max(1e-9, Stage((last.chapter - 1) * 5).hp);
            return new ChallengeRow { chapter = ch, time = last.time, count = last.count, hp = last.hp * k, reward = last.reward };
        }

        /// <summary>보스 던전 (lv = 세이브의 bossLv, 0 = Lv.1)</summary>
        public static BossRaidRow BossRaid(int lv)
        {
            Load();
            var r = bossRaids.Find(b => b.lv == lv);
            if (r != null) return r;
            if (bossRaids.Count == 0) return new BossRaidRow { lv = lv, bossId = "ogre", hp = Stage(45 + lv * 2).hp * 25, time = 60, scale = 2.3f, reward = 50 + lv * 10 };
            var last = bossRaids[^1];
            var prev = bossRaids.Count > 1 ? bossRaids[^2] : last;
            int n = lv - last.lv;
            double g = prev.hp > 0 ? last.hp / prev.hp : 1.1;
            return new BossRaidRow { lv = lv, bossId = last.bossId, hp = last.hp * Math.Pow(g, n), time = last.time, scale = last.scale, reward = last.reward + last.rewardAdd * n, rewardAdd = last.rewardAdd };
        }

        /// <summary>광산 보상 배율 (지금 챕터에 맞는 단계)</summary>
        public static double MineMul(int chapter)
        {
            Load();
            double m = 1;
            foreach (var t in mineTiers) if (t.chapter <= chapter) m = t.mul;
            return m;
        }

        public static int MineTier(int chapter)
        {
            Load();
            int k = 1;
            for (int i = 0; i < mineTiers.Count; i++) if (mineTiers[i].chapter <= chapter) k = i + 1;
            return k;
        }

        public static BuffRow Buff(string id)
        {
            Load();
            return buffs.TryGetValue(id, out var b) ? b : new BuffRow { id = id, mul = 3, duration = 300, maxStack = 3600, cooldown = 12 };
        }

        public static UnlockRow Unlock(string id)
        {
            Load();
            return Unlocks.Find(u => u.id == id);
        }

        // =====================================================================
        // 미션
        // =====================================================================
        public static long MissionTarget(int i, int tier)
        {
            var m = Missions[i];
            if (tier < m.targets.Length) return m.targets[tier];
            int n = tier - (m.targets.Length - 1);
            long last = m.targets[^1];
            if (m.extraAdd > 0) return last + m.extraAdd * n;
            double v = last * Math.Pow(Math.Max(1.01, m.extraMul), n);
            return v > 4e18 ? (long)4e18 : (long)Math.Round(v);
        }

        /// <summary>보상 = 기본 + 10단계마다 더하기 (1~10단계 = 기본)</summary>
        public static double MissionReward(int i, int tier) => Missions[i].baseReward + Missions[i].addPer10 * (tier / 10);

        // =====================================================================
        // 총알 (코드 = 등급 x 10 + 종류 번호)
        // =====================================================================
        public static readonly string[] Types = { "single", "shotgun", "fast", "pierce", "laser", "missile" };
        public static int Code(int grade, string type) => grade * 10 + Array.IndexOf(Types, type);
        public static int GradeOf(int code) => code / 10;
        public static string TypeOf(int code) => Types[code % 10];

        /// <summary>등급 이름 D · C · B · A · S · SS · SSS · R · SR · SSR · U · SU · SSU (Weapon_Grade_Growth)</summary>
        public static string[] Grades { get { Load(); return gradeNames; } }
        public static int MaxGrade { get { Load(); return gradeNames.Length - 1; } }
        public static string GradeName(int g) { var n = Grades; return n[Mathf.Clamp(g, 0, n.Length - 1)]; }
        public static GradeRow Grade(int g)
        {
            Load();
            if (grades.Count == 0) return new GradeRow { idx = g, name = "D", color = Color.white, maxEnchant = 150 };
            return grades[Mathf.Clamp(g, 0, grades.Count - 1)];
        }
        public static Color GradeColor(int g) => Grade(g).color;

        public static WeaponRow Weapon(int code)
        {
            Load();
            if (code < 0) return null;
            if (weapons.TryGetValue(code, out var w)) return w;
            // 표에 없는 등급(최고 등급 위): 그 종류의 가장 높은 줄을 2배씩
            int g = GradeOf(code); string t = TypeOf(code);
            WeaponRow top = null;
            foreach (var x in Weapons) if (x.type == t && x.grade < g && (top == null || x.grade > top.grade)) top = x;
            if (top == null) return null;
            double mul = Math.Pow(2, g - top.grade);
            return new WeaponRow { code = code, grade = g, type = t, name = top.name, atk = top.atk * mul, interval = top.interval, count = top.count, spread = top.spread, pierce = top.pierce, splash = top.splash, range = top.range };
        }

        public static List<string> TypesOfGrade(int grade)
        {
            Load();
            int g = Math.Min(grade, MaxGrade);
            var l = new List<string>();
            foreach (var w in Weapons) if (w.grade == g && !l.Contains(w.type)) l.Add(w.type);
            if (l.Count == 0) l.Add("single");
            return l;
        }

        public static string WeaponName(int code)
        {
            var w = Weapon(code);
            return w == null ? "?" : $"{GradeName(GradeOf(code))} {w.name}";
        }

        public static string TypeLabel(string t) => t switch
        {
            "single" => "단발", "shotgun" => "산탄", "fast" => "속사", "pierce" => "관통", "laser" => "레이저", "missile" => "미사일", _ => t
        };

        public static BulletRow Bullet(string type)
        {
            Load();
            return bullets.TryGetValue(type, out var b) ? b : new BulletRow { type = type, kind = "Normal", speed = 19, maxSpeed = 19, hitBonus = 0.05f, rangeBonus = 2, sprite = "Bullet/bullet_basic", icon = "UI/icon_pistol" };
        }

        public static ZombieRow Zombie(string id)
        {
            Load();
            foreach (var z in Zombies) if (z.id == id) return z;
            return Zombies[0];
        }

        // =====================================================================
        // 총알 슬롯 강화 (Weapon_Enchant: 1~150 레벨, 장착한 총알 등급이 최대 레벨·효과 배율을 정함)
        // =====================================================================
        public static int EnchantTop { get { Load(); return enchants.Count > 0 ? enchants[^1].lv : 0; } }
        /// <summary>lv 레벨의 효과·비용 (lv 0 = 효과 없음)</summary>
        public static EnchantRow Enchant(int lv)
        {
            Load();
            if (lv <= 0 || enchants.Count == 0) return new EnchantRow { lv = 0, rate = 100 };
            return enchants.Find(e => e.lv == lv) ?? enchants[^1];
        }

        // =====================================================================
        // CSV 읽기
        // =====================================================================
        public class Row
        {
            readonly Dictionary<string, string> d;
            public Row(Dictionary<string, string> d) { this.d = d; }
            public bool Has(string k) => d.ContainsKey(k);
            public string S(string k) => d.TryGetValue(k, out var s) ? s : "";
            public double D(string k, double def = 0) =>
                d.TryGetValue(k, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
            public float F(string k, float def = 0) => (float)D(k, def);
            public int I(string k, int def = 0) => (int)Math.Round(D(k, def));
            public Color Hex(string k, Color def) => ColorUtility.TryParseHtmlString(S(k), out var c) ? c : def;
        }

        /// <summary>칸 이름 정리: "Time_Limit (초)" → "Time_Limit"</summary>
        static readonly Regex unitSuffix = new(@"\s*\(.*\)\s*$");
        static string Head(string h) => unitSuffix.Replace(h.Trim(), "").Trim();

        static List<Row> Read(string name)
        {
            var list = new List<Row>();
            var ta = Resources.Load<TextAsset>("Tables/" + name);
            if (ta == null) { Debug.LogError("표 없음: Tables/" + name + " (Table 폴더의 xlsx 를 [카치건 → 표 변환])"); return list; }
            var lines = SplitLines(ta.text.TrimStart('﻿'));
            string[] head = null;
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var cells = SplitCsv(raw);
                if (head == null) { head = Array.ConvertAll(cells, Head); continue; }
                var d = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++)
                    if (head[i].Length > 0 && !d.ContainsKey(head[i])) d[head[i]] = i < cells.Length ? cells[i].Trim() : "";
                list.Add(new Row(d));
            }
            return list;
        }

        /// <summary>줄 나누기 (따옴표 안의 줄바꿈은 칸 안의 글자)</summary>
        static List<string> SplitLines(string text)
        {
            var l = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool q = false;
            foreach (char ch in text)
            {
                if (ch == '"') q = !q;
                if (!q && (ch == '\n' || ch == '\r')) { if (sb.Length > 0) l.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(ch);
            }
            if (sb.Length > 0) l.Add(sb.ToString());
            return l;
        }

        static string[] SplitCsv(string line)
        {
            var res = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (q)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') q = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') q = true;
                else if (ch == ',') { res.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            res.Add(sb.ToString());
            return res.ToArray();
        }

        static string Title(string id) => string.IsNullOrEmpty(id) ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);

        static long[] ParseLongs(string s)
        {
            var l = new List<long>();
            foreach (var p in s.Split('|'))
                if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) l.Add((long)Math.Round(v));
            return l.ToArray();
        }
    }
}
