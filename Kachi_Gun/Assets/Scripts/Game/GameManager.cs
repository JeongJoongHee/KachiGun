using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KachiGun
{
    /// <summary>
    /// 게임 진행: 스테이지(N-1~N-5, N-5 끝에 변이 오우거 보스) · 도전(60초 섬멸) · 다이아 광산(고정 피해 1) · 보스 던전.
    /// 총알 뽑기/합성·스킨·미션 규칙도 여기서 처리하고, 화면은 HUD 가 그린다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum Mode { Stage, Challenge, Mine, Boss }
        public static GameManager I;
        public Mode mode = Mode.Stage;
        public event Action Changed;          // 골드·레벨 등 화면 갱신
        public event Action StageChanged;

        static SaveData S => SaveData.Cur;
        float spawnT, saveT, dailyT;
        public Zombie boss;
        public bool bossPhase;
        public float bossTime;            // 보스 남은 시간
        public bool upgradeHint;          // 보스 실패 뒤 강화 안내 화살표 (강화하면 꺼짐)
        public bool BossFarm => S.bossFarm;
        public float BossTimeMax { get { float t = Tables.Stage(S.stage).bossTime; return t > 0 ? t : Tables.Cf("BossTime"); } }

        // 던전
        public float dungeonTime;
        public double dungeonGems;
        public int dungeonChapter, dungeonTotal;
        bool dungeonOver;
        public bool eco;

        public bool CanShoot => !dungeonOver;
        public bool InputAllowed => !eco && !(HUD.I && HUD.I.PopupOpen);

        void Awake() { I = this; Tables.Load(); }

        void Start()
        {
            S.RefreshDaily();
            ApplyTint();
            Squad.I.Rebuild();
            Squad.I.ResetPosition();
            StageChanged?.Invoke();
        }

        public void Notify() => Changed?.Invoke();

        void Update()
        {
            float dt = Time.deltaTime;
            saveT += Time.unscaledDeltaTime;
            if (saveT > 5f) { saveT = 0; S.Save(); }
            dailyT += Time.unscaledDeltaTime;
            if (dailyT > 10f) { dailyT = 0; S.RefreshDaily(); }

            switch (mode)
            {
                case Mode.Stage: StageTick(dt); break;
                default: DungeonTick(dt); break;
            }
        }

        // =====================================================================
        // 스테이지
        // =====================================================================
        public int Quota { get { int q = Tables.Stage(S.stage).quota; return q > 0 ? q : Tables.Ci("KillsPerStage"); } }
        public float StageProgress => bossPhase ? Mathf.Clamp01(bossTime / BossTimeMax) : S.bossFarm ? 1f : Mathf.Clamp01(S.kills / (float)Quota);

        void StageTick(float dt)
        {
            int alive = 0;
            foreach (var t in Target.All) if (t is Zombie z && z.Alive && !z.isBoss) alive++;

            var st = Tables.Stage(S.stage);
            if (!bossPhase && !S.bossFarm && S.kills >= Quota)
            {
                if (st.boss) StartBoss();
                else { AdvanceStage(); return; }
            }

            // 보스 제한 시간: 못 잡으면 보스가 물러나고 반복 사냥
            if (bossPhase)
            {
                bossTime -= dt;
                if (bossTime <= 0) { BossFail(); return; }
            }

            spawnT -= dt;
            bool want = alive < st.maxAlive && (bossPhase || S.bossFarm || S.kills + alive < Quota);
            if (spawnT <= 0 && want)
            {
                spawnT = st.spawn * Random.Range(0.7f, 1.3f);
                SpawnZombie();
            }
        }

        void SpawnZombie()
        {
            var r = PickZombie(S.Chapter);
            Zombie.Spawn(r, Field.I.SpawnPoint(), Stats.ZombieHp(S.stage) * r.hpMul, Stats.ZombieGold(S.stage) * r.goldMul);
        }

        public static ZombieRow PickZombie(int chapter)
        {
            float sum = 0;
            foreach (var z in Tables.Zombies) if (z.minChapter <= chapter && z.weight > 0) sum += z.weight;
            float k = Random.value * sum;
            foreach (var z in Tables.Zombies)
            {
                if (z.minChapter > chapter || z.weight <= 0) continue;
                k -= z.weight;
                if (k <= 0) return z;
            }
            return Tables.Zombies[0];
        }

        /// <summary>보스 실패: 보스는 오른쪽으로 물러남(보상 없음) → 지금 스테이지 반복 사냥, [보스 도전] 버튼</summary>
        public void BossFail()
        {
            if (!bossPhase) return;
            bossPhase = false;
            if (boss) boss.Retreat();
            boss = null;
            S.bossFarm = true;
            upgradeHint = true;
            S.Save();
            HUD.I?.Banner(L.T("보스 실패! 강화 후 다시 도전하세요"), new Color(1f, 0.55f, 0.45f));
            StageChanged?.Invoke();
            Notify();
        }

        /// <summary>[보스 도전] 버튼</summary>
        public string ChallengeBoss()
        {
            if (mode != Mode.Stage || bossPhase) return null;
            if (!S.bossFarm) return L.T("아직 보스를 만날 때가 아닙니다");
            S.bossFarm = false;
            StartBoss();
            return null;
        }

        void StartBoss()
        {
            bossPhase = true;
            bossTime = BossTimeMax;
            var st = Tables.Stage(S.stage);
            var r = Tables.Zombie(string.IsNullOrEmpty(st.bossId) || st.bossId == "-" ? "ogre" : st.bossId);
            var p = Field.I.SpawnPoint();
            double hp = st.bossHp > 0 ? st.bossHp : st.hp * Tables.C("BossHpMul");
            double gold = st.bossGold > 0 ? st.bossGold : st.gold * Tables.C("BossGoldMul");
            boss = Zombie.Spawn(r, p, hp, gold, st.bossScale > 0 ? st.bossScale : Tables.Cf("BossScale"), true);
            HUD.I?.Banner(L.T("보스 등장!"), new Color(1f, 0.4f, 0.3f));
            StageChanged?.Invoke();
        }

        public void AdvanceStage()
        {
            int before = S.stage;
            S.stage++;
            S.kills = 0;
            S.bossFarm = false;
            upgradeHint = false;
            bossPhase = false;
            boss = null;
            S.statStages++;
            S.bestEver = Math.Max(S.bestEver, S.stage);
            bool newChapter = S.stage / 5 != before / 5;
            if (newChapter) ApplyTint();
            string biome = L.T(Tables.Chapter(S.Chapter).biome);
            HUD.I?.Banner("Stage " + SaveData.StageName(S.stage) + (newChapter && biome.Length > 0 ? "\n<size=34>" + biome + "</size>" : ""), Color.white);
            CheckUnlocks(before, S.stage);
            S.Save();
            StageChanged?.Invoke();
            Notify();
        }

        /// <summary>Unlock_Roadmap: 스테이지로 열리는 기능을 지나면 알림 (알림 글이 '-' 이면 조용히)</summary>
        void CheckUnlocks(int before, int now)
        {
            foreach (var u in Tables.Unlocks)
            {
                if (u.type != "Stage" || string.IsNullOrEmpty(u.bannerMsg) || u.bannerMsg == "-") continue;
                if (before < u.stageIdx && now >= u.stageIdx) HUD.I?.Toast(L.T(u.bannerMsg));
            }
        }

        public void ApplyTint()
        {
            Color c = mode switch
            {
                Mode.Mine => new Color(0.55f, 0.62f, 0.85f),
                Mode.Challenge => new Color(1f, 0.82f, 0.8f),
                Mode.Boss => new Color(0.8f, 0.68f, 0.68f),
                _ => Tables.Chapter(S.Chapter).tint,
            };
            Field.I.Tint(c);
        }

        // =====================================================================
        // 피해·처치
        // =====================================================================
        /// <summary>bossMul = 쏜 카피바라의 보스 피해 배율 (보급품 + 슬롯 강화)</summary>
        public void Damage(Target t, double dmg, Vector2 from, double bossMul = 1)
        {
            if (mode == Mode.Mine) dmg = 1;      // 광산 = 고정 피해 1
            else if (t is Zombie z && z.isBoss) dmg *= bossMul;
            t.Hit(dmg, from);
        }

        public void OnTargetDead(Target t)
        {
            if (t is Zombie z)
            {
                S.statKills++;
                if (mode == Mode.Stage)
                {
                    double g = z.gold * Stats.GoldMul;
                    Fx.I?.Money(z.Center, g, z.isBoss ? 5 : 2);
                    if (z.isBoss) { S.statBossKills++; AdvanceStage(); return; }
                    if (!bossPhase && !S.bossFarm) S.kills++;   // 반복 사냥 중엔 골드만
                    StageChanged?.Invoke();
                }
                else if (mode == Mode.Challenge || mode == Mode.Boss)
                {
                    int left = 0;
                    foreach (var o in Target.All) if (o.Alive) left++;
                    if (left == 0) EndDungeon(true);
                    StageChanged?.Invoke();
                }
            }
            else if (t is Ore o && mode == Mode.Mine)
            {
                if (o.reward > 0)
                {
                    dungeonGems += o.reward;
                    Fx.I?.Text(o.Center + Vector2.up * 0.4f, "+" + o.reward.ToString("0.#"), new Color(0.5f, 0.85f, 1f), 40, 0.9f);
                }
                StageChanged?.Invoke();
            }
        }

        // =====================================================================
        // 강화
        // =====================================================================
        public bool TryUpgrade(int which)
        {
            int lv = which == 0 ? S.lvAtk : which == 1 ? S.lvSpd : S.lvGold;
            double cost = Stats.Cost(lv);
            if (S.gold < cost) return false;
            S.gold -= cost;
            if (which == 0) S.lvAtk++; else if (which == 1) S.lvSpd++; else S.lvGold++;
            if (!S.tutorialDone) S.tutorialDone = true;
            upgradeHint = false;
            Notify();
            return true;
        }

        // =====================================================================
        // 기능 열림 (기획서 3장)
        // =====================================================================
        // 기능 열림 = Unlock_Roadmap 표 (Stage = 그 스테이지까지 가 봤으면, Gems_Or_Challenge = 다이아를 모았거나 도전을 깼으면)
        public bool Unlocked(string id)
        {
            var u = Tables.Unlock(id);
            if (u == null) return true;
            if (u.type == "Gems_Or_Challenge") return S.weaponUnlocked || S.challengeCleared.Count > 0 || S.gems >= u.value;
            return S.bestEver >= u.stageIdx;
        }
        /// <summary>잠겨 있을 때 토스트 글</summary>
        public string LockMsg(string id) { var u = Tables.Unlock(id); return u != null && u.lockMsg.Length > 0 ? L.T(u.lockMsg) : L.T("아직 열리지 않았습니다"); }

        public bool WeaponOpen => Unlocked("weapon");
        public bool SkinOpen => Unlocked("skin");
        public bool MenuOpen => Unlocked("mission");
        public bool MineOpen => Unlocked("mine");
        public bool FishOpen => Unlocked("supply");
        public bool BossRaidOpen => Unlocked("boss_raid");

        // =====================================================================
        // 총알: 뽑기 · 합성 · 장착 (기획서 6장)
        // =====================================================================
        public string Gacha()
        {
            double cost = Tables.C("GachaCost");
            if (S.gems < cost) return L.T("다이아가 부족합니다");
            int empty = S.inv.IndexOf(-1);
            if (empty < 0) return L.T("인벤토리가 가득 찼습니다");
            S.gems -= cost;
            var types = Tables.TypesOfGrade(0);
            S.inv[empty] = Tables.Code(0, types[Random.Range(0, types.Count)]);
            S.weaponUnlocked = true;
            Notify();
            return null;
        }

        /// <summary>인벤토리 a 칸을 b 칸에 겹치기: 같은 등급이면 한 단계 위 등급의 랜덤 종류로 합성</summary>
        public string Merge(int a, int b)
        {
            if (a == b) return "";
            int ca = S.inv[a], cb = S.inv[b];
            if (ca < 0) return "";
            if (cb < 0) { S.inv[b] = ca; S.inv[a] = -1; Notify(); return null; }   // 빈칸으로 옮기기
            int g = Tables.GradeOf(ca);
            if (g != Tables.GradeOf(cb)) { S.inv[a] = cb; S.inv[b] = ca; Notify(); return null; }  // 등급 다르면 자리 바꾸기
            if (g >= Tables.MaxGrade) return L.T("최고 등급입니다");
            var types = Tables.TypesOfGrade(g + 1);
            S.inv[a] = -1;
            S.inv[b] = Tables.Code(g + 1, types[Random.Range(0, types.Count)]);
            S.statMerges++;
            Notify();
            return null;
        }

        /// <summary>인벤토리 총알 분해 → 다이아 (Grade_Enchant_Multiplier 의 Salvage_Gems_Reward)</summary>
        public string Salvage(int invIdx)
        {
            int c = S.inv[invIdx];
            if (c < 0) return "";
            double g = Tables.Grade(Tables.GradeOf(c)).salvage;
            S.inv[invIdx] = -1;
            S.gems += g;
            HUD.I?.Bump(true);
            Notify();
            return null;
        }

        // =====================================================================
        // 총알 슬롯 강화 (Weapon_Enchant) — 장착 칸 3개마다 레벨, 다이아 + 골드, 성공 확률
        // =====================================================================
        /// <summary>ok = 성공 여부. 돌려주는 글 = 못 한 이유(null = 시도함)</summary>
        public string EnchantSlot(int slot, out bool ok)
        {
            ok = false;
            int code = S.slots[slot];
            if (code < 0) return L.T("총알을 장착한 칸만 강화할 수 있습니다");
            int lv = S.slotLv[slot], max = Stats.SlotMax(slot);
            if (lv >= Tables.EnchantTop) return L.T("최고 강화입니다");
            if (lv >= max) return L.F("{0} 등급 총알은 +{1} 까지 — 더 높은 등급을 장착하세요", Tables.GradeName(Tables.GradeOf(code)), max);
            var e = Tables.Enchant(lv + 1);
            if (S.gems < e.gems) return L.T("다이아가 부족합니다");
            if (S.gold < e.gold) return L.T("골드가 부족합니다");
            S.gems -= e.gems;
            S.gold -= e.gold;
            ok = Random.value * 100 < e.rate;
            if (ok) S.slotLv[slot] = lv + 1;
            Notify();
            return null;
        }

        // =====================================================================
        // 보급품 (Supply_Collection) — 다이아로 사면 보유 효과(공격·속도·돈·보스 피해) 합산
        // =====================================================================
        public int BestChapter => S.bestEver / 5 + 1;
        public string BuySupply(int id)
        {
            var r = Tables.Supplies.Find(x => x.id == id);
            if (r == null) return "";
            if (S.supplies.Contains(id)) return L.T("이미 가진 보급품입니다");
            if (BestChapter < r.unlockChapter) return L.F("챕터 {0} 에서 열립니다", r.unlockChapter);
            if (S.gems < r.cost) return L.T("다이아가 부족합니다");
            S.gems -= r.cost;
            S.supplies.Add(id);
            Notify();
            return null;
        }

        // =====================================================================
        // 상점: 다이아로 사는 골드 상자 (Shop_Products 의 Gems 가격, 'StageGold * 600' = 지금 스테이지 좀비 600마리 골드)
        // =====================================================================
        public string BuyShop(ShopRow r)
        {
            double price = Digits(r.priceValue);
            if (S.gems < price) return L.T("다이아가 부족합니다");
            int k = r.reward.IndexOf('*');
            double n = k >= 0 ? Digits(r.reward.Substring(k + 1)) : 600;
            double g = Stats.ZombieGold(S.stage) * n * Stats.GoldMul;
            S.gems -= price;
            S.gold += g;
            HUD.I?.Bump(false);
            HUD.I?.Toast(L.T("골드") + " +" + Num.F(g));
            Notify();
            return null;
        }

        /// <summary>"100 다이아" → 100, "$1.99" → 1.99</summary>
        public static double Digits(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s ?? "")
            {
                if (char.IsDigit(ch) || ch == '.') sb.Append(ch);
                else if (ch == ',') continue;
                else if (sb.Length > 0) break;
            }
            return double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>인벤토리 칸 → 장착 칸 (장착 칸에 있던 것은 인벤토리로)</summary>
        public string Equip(int invIdx, int slot)
        {
            int c = S.inv[invIdx];
            if (c < 0) return "";
            S.inv[invIdx] = S.slots[slot];
            S.slots[slot] = c;
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
            return null;
        }

        /// <summary>장착 해제 (최소 1개는 장착해야 함)</summary>
        public string Unequip(int slot, int toInv = -1)
        {
            if (S.slots[slot] < 0) return "";
            int n = 0; foreach (var s in S.slots) if (s >= 0) n++;
            if (n <= 1) return L.T("최소 1개의 총알을 장착해야 합니다");
            if (toInv < 0) toInv = S.inv.IndexOf(-1);
            if (toInv < 0) return L.T("인벤토리가 가득 찼습니다");
            if (S.inv[toInv] >= 0) return L.T("빈 칸에만 놓을 수 있습니다");
            S.inv[toInv] = S.slots[slot];
            S.slots[slot] = -1;
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
            return null;
        }

        public void SwapSlots(int a, int b)
        {
            (S.slots[a], S.slots[b]) = (S.slots[b], S.slots[a]);
            CompactSlots();
            Squad.I.Rebuild();
            Notify();
        }

        /// <summary>리더 칸(0)이 비지 않게 앞으로 당긴다</summary>
        void CompactSlots()
        {
            var l = new List<int>();
            foreach (var s in S.slots) if (s >= 0) l.Add(s);
            for (int i = 0; i < S.slots.Count; i++) S.slots[i] = i < l.Count ? l[i] : -1;
        }

        // =====================================================================
        // 스킨 (기획서 7.1 — 가진 스킨 능력치는 전부 합산)
        // =====================================================================
        public string BuySkin(int id)
        {
            var r = Tables.Skins.Find(x => x.id == id);
            if (r == null) return "";
            if (S.skins.Contains(id)) { S.skin = id; Squad.I.SetSkin(id); Notify(); return null; }
            if (S.gems < r.price) return L.T("다이아가 부족합니다");
            S.gems -= r.price;
            S.skins.Add(id);
            S.skin = id;
            Squad.I.SetSkin(id);
            Notify();
            return null;
        }

        // =====================================================================
        // 버프 (광고 5분, 연속 시청 시 시간 누적, 재시청 대기 12초)
        // =====================================================================
        public string Buff(bool atk)
        {
            double now = SaveData.NowD;
            double cool = atk ? S.buffAtkCool : S.buffGoldCool;
            if (cool > now) return L.F("{0}초 뒤에 다시 볼 수 있습니다", Math.Ceiling(cool - now));
            var b = Tables.Buff(atk ? "buff_atk" : "buff_gold");
            AdService.Show(b.id, () =>
            {
                // 연속 시청 = 시간 누적 (최대 Max_Stack_Sec 까지)
                double t = SaveData.NowD;
                double cap = b.maxStack > 0 ? t + b.maxStack : double.MaxValue;
                if (atk) { S.buffAtkEnd = Math.Min(Math.Max(S.buffAtkEnd, t) + b.duration, cap); S.buffAtkCool = t + b.cooldown; }
                else { S.buffGoldEnd = Math.Min(Math.Max(S.buffGoldEnd, t) + b.duration, cap); S.buffGoldCool = t + b.cooldown; }
                Notify();
            });
            return null;
        }

        // =====================================================================
        // 미션 (기획서 10.1 — 단계별 반복)
        // =====================================================================
        /// <summary>미션 진행 값 (Mission_Master 의 Save_Stat_Key)</summary>
        public long MissionValue(int i) => Tables.Missions[i].statKey switch
        {
            "statKills" => S.statKills, "statStages" => S.statStages, "statMines" => S.statMines,
            "statBossKills" => S.statBossKills, "statMerges" => S.statMerges, _ => 0
        };

        public long MissionTarget(int i) => Tables.MissionTarget(i, S.missionTier[i]);
        public bool MissionDone(int i) => Tables.MissionDone(i, S.missionTier[i]);

        // 보상 = 다이아로 친 값(Mission_Master). 10-24 사용자 요청: 한 번에 한 종류만 — 단계마다 골드 → 다이아 → 골드 … 번갈아.
        // 두 단계를 합친 양은 예전(다이아 Mission_Gem_Ratio 30% + 골드 70%)과 같게: 다이아 차례 = 값 x 0.3 x 2, 골드 차례 = 값 x 0.7 x 2.
        // 골드 1다이아어치 = 지금 스테이지 좀비 Gem_Gold_Kills(6)마리 골드 x 돈 배율 (상점 골드 상자 100다이아 = 600마리와 같은 비율)
        static double GemRatio => Tables.C("Mission_Gem_Ratio", 0.3);
        /// <summary>이번 단계 보상이 다이아인지 (짝수 단계 0·2·4… = 골드, 홀수 = 다이아)</summary>
        public bool MissionIsGem(int i) => S.missionTier[i] % 2 == 1;
        public double MissionGems(int i) => MissionIsGem(i) ? Math.Max(1, Math.Round(Tables.MissionReward(i, S.missionTier[i]) * GemRatio * 2)) : 0;
        public double MissionGold(int i) => MissionIsGem(i) ? 0 : Tables.MissionReward(i, S.missionTier[i]) * (1 - GemRatio) * 2 * Tables.C("Gem_Gold_Kills", 6) * Stats.ZombieGold(S.stage) * Stats.GoldMul;
        /// <summary>[받기] 버튼·토스트 글자: "다이아 N" 또는 "골드 M"</summary>
        public string MissionRewardText(int i) => MissionIsGem(i) ? $"{L.T("다이아")} {Num.Gem(MissionGems(i))}" : $"{L.T("골드")} {Num.F(MissionGold(i))}";

        public bool MissionReady(int i) => !MissionDone(i) && MissionValue(i) >= MissionTarget(i);

        public string ClaimMission(int i)
        {
            if (MissionDone(i)) return L.T("모든 단계를 끝냈습니다");
            if (!MissionReady(i)) return L.T("아직 달성하지 못했습니다");
            bool gem = MissionIsGem(i);
            double gems = MissionGems(i), gold = MissionGold(i);
            S.gems += gems;
            S.gold += gold;
            S.missionTier[i]++;
            HUD.I?.Bump(gem);
            HUD.I?.Toast(gem ? L.T("다이아") + " +" + Num.Gem(gems) : L.T("골드") + " +" + Num.F(gold));
            Notify();
            return null;
        }

        // =====================================================================
        // 광고 무료 다이아 (상점 · 하루 AdGems_Daily 번, 한 번에 AdGems_Amount)
        // =====================================================================
        public int AdGemsLeft { get { S.RefreshDaily(); return Math.Max(0, (int)Tables.C("AdGems_Daily", 3) - S.adGemUsed); } }

        public string AdGems()
        {
            if (AdGemsLeft <= 0) return L.T("오늘은 다 받았습니다 (내일 다시)");
            AdService.Show("free_gems", () =>
            {
                double g = Tables.C("AdGems_Amount", 40);
                S.adGemUsed++;
                S.gems += g;
                HUD.I?.Bump(true);
                HUD.I?.Toast(L.T("다이아") + " +" + Num.Gem(g));
                S.Save();
                Notify();
            });
            return null;
        }

        public bool AnyMissionReady { get { for (int i = 0; i < Tables.Missions.Count; i++) if (MissionReady(i)) return true; return false; } }

        // =====================================================================
        // 던전 공통
        // =====================================================================
        /// <summary>도전할 수 있는 가장 낮은 챕터 (지금 챕터까지, 이미 깬 챕터 제외). 없으면 0</summary>
        public int ChallengeChapter
        {
            get
            {
                for (int c = 1; c <= S.Chapter; c++) if (!S.challengeCleared.Contains(c)) return c;
                return 0;
            }
        }

        public string StartChallenge()
        {
            int ch = ChallengeChapter;
            if (ch == 0) return L.T("다음 챕터에서 새 도전이 열립니다");
            var row = Tables.Challenge(ch);
            BeginDungeon(Mode.Challenge, row.time);
            dungeonChapter = ch;
            int n = row.count;
            double hp = row.hp;
            for (int i = 0; i < n; i++)
            {
                var r = PickZombie(ch);
                // 멀리(오른쪽)에 많이: AI 가 가까운 것만 쫓으면 동선이 낭비되도록
                var p = Field.I.RandomRoad(i < n / 3 ? 0.55f : 0.15f, 1f, 0.6f);
                var z = Zombie.Spawn(r, p, hp * r.hpMul, 0);
                z.standStill = true;
            }
            dungeonTotal = n;
            HUD.I?.Banner(L.F("도전 {0} — 모두 처치!", ch), new Color(1f, 0.85f, 0.4f));
            return null;
        }

        public string StartMine()
        {
            if (!MineOpen) return LockMsg("mine");
            S.RefreshDaily();
            if (S.mineTickets <= 0) return L.T("오늘 티켓을 다 썼습니다");
            S.mineTickets--;
            S.statMines++;
            BeginDungeon(Mode.Mine, Tables.Cf("MineTime"));
            dungeonGems = 0;
            // Mine_Ore_Spec 줄마다 Spawn_Count 개 (Spawn_X_Near~Far: 0 = 분대 바로 앞 ~ 1 = 오른쪽 끝), 보상 x Mine_Scaling 배율
            // 큰 보석 = 오른쪽 끝(멀리) 밀집, 작은 보석 = 곳곳, 회색 바위 = 분대 가까이(AI 가 먼저 때리는 함정)
            double mul = Tables.MineMul(S.Chapter);
            foreach (var o in Tables.Ores)
                for (int i = 0; i < o.count; i++) Ore.Spawn(o, Field.I.RandomRoad(o.near, o.far, o.scale >= 1 ? 0.7f : 0.6f), mul);
            HUD.I?.Banner(L.T("다이아 광산"), new Color(0.5f, 0.85f, 1f));
            return null;
        }

        public string StartBossRaid()
        {
            if (!BossRaidOpen) return LockMsg("boss_raid");
            S.RefreshDaily();
            if (S.bossTickets <= 0) return L.T("오늘 티켓을 다 썼습니다");
            S.bossTickets--;
            var row = Tables.BossRaid(S.bossLv);
            BeginDungeon(Mode.Boss, row.time);
            var r = Tables.Zombie(string.IsNullOrEmpty(row.bossId) ? "ogre" : row.bossId);
            var z = Zombie.Spawn(r, Field.I.RandomRoad(0.5f, 0.62f, 1.4f), row.hp, 0, row.scale, true);
            z.standStill = true;
            dungeonTotal = 1;
            HUD.I?.Banner(L.F("보스 던전 Lv.{0}", S.bossLv + 1), new Color(1f, 0.4f, 0.3f));
            return null;
        }

        void BeginDungeon(Mode m, float time)
        {
            Fx.I?.FlushMoney();
            ClearTargets();
            mode = m;
            dungeonTime = time;
            dungeonOver = false;
            bossPhase = false; boss = null;
            Squad.I.ResetPosition();
            ApplyTint();
            StageChanged?.Invoke();
        }

        void DungeonTick(float dt)
        {
            if (dungeonOver) return;
            dungeonTime -= dt;
            if (dungeonTime <= 0)
            {
                dungeonTime = 0;
                EndDungeon(mode == Mode.Mine);   // 광산은 시간 끝 = 정상 종료
            }
        }

        public int DungeonLeft { get { int n = 0; foreach (var t in Target.All) if (t.Alive) n++; return n; } }

        void EndDungeon(bool win)
        {
            if (dungeonOver) return;
            dungeonOver = true;
            double reward = 0;
            string title;
            if (mode == Mode.Challenge) { title = win ? L.F("도전 {0} 성공!", dungeonChapter) : L.T("시간 초과"); if (win) reward = Tables.Challenge(dungeonChapter).reward; }
            else if (mode == Mode.Mine) { title = L.T("채굴 끝!"); reward = Math.Round(dungeonGems, 1); }
            else { title = win ? L.T("보스 처치!") : L.T("시간 초과"); if (win) reward = Tables.BossRaid(S.bossLv).reward; }
            HUD.I?.ShowResult(title, win, reward);
        }

        /// <summary>결과 창에서 받기 (광고 = 배율)</summary>
        public void ClaimDungeon(double reward, bool win)
        {
            if (win || mode == Mode.Mine)
            {
                S.gems += reward;
                if (mode == Mode.Challenge && win && !S.challengeCleared.Contains(dungeonChapter)) S.challengeCleared.Add(dungeonChapter);
                if (mode == Mode.Boss && win) { S.bossLv++; S.statBossKills++; }
            }
            ExitDungeon();
        }

        public void ExitDungeon()
        {
            ClearTargets();
            mode = Mode.Stage;
            dungeonOver = false;
            Squad.I.ResetPosition();
            ApplyTint();
            S.Save();
            StageChanged?.Invoke();
            Notify();
        }

        public void ClearTargets()
        {
            foreach (var t in Target.All.ToArray()) if (t) Destroy(t.gameObject);
            Target.All.Clear();
            Bullet.ClearAll();
        }

        // =====================================================================
        void OnApplicationPause(bool p) { if (p) { Fx.I?.FlushMoney(); S.Save(); } }
        void OnApplicationQuit() { Fx.I?.FlushMoney(); S.Save(); }
    }
}
