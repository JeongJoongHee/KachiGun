using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 배치 모드 자동 시험: bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.Run --play
    /// 깨끗한 세이브로 시작 → 전투·강화·보스·메뉴 드롭다운·뽑기/합성/장착·스킨·도전·광산·보스 던전·상위 총알을 차례로 확인하고
    /// 화면을 Logs/PlayTest/*.png (1080x2340) 로 찍는다. 끝나면 시험 세이브는 지운다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayTest
    {
        const string Key = "KachiGun.PlayTest";

        static PlayTest()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode) new GameObject("PlayTestRunner").AddComponent<PlayTestRunner>();
            };
        }

        /// <summary>움직임 확인용: 전투를 0.05초 간격으로 연속 촬영 → Logs/PlayTest/anim/*.png</summary>
        public static void RunAnim()
        {
            SessionState.SetBool(Key + ".anim", true);
            Run();
        }
        public static bool AnimOnly => SessionState.GetBool(Key + ".anim", false);

        /// <summary>뼈대 확인용: 좀비 12종 + 카피바라를 세워 걸음 위상을 직접 돌리며 촬영 → Logs/PlayTest/rig/*.png</summary>
        public static void RunRig()
        {
            SessionState.SetBool(Key + ".rig", true);
            Run();
        }
        public static bool RigOnly => SessionState.GetBool(Key + ".rig", false);

        public static void Run()
        {
            SaveData.ResetAll();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        public static void Finish(int errors)
        {
            SaveData.ResetAll();
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".anim", false);
            SessionState.SetBool(Key + ".rig", false);
            EditorApplication.Exit(errors == 0 ? 0 : 3);
        }
    }

    public class PlayTestRunner : MonoBehaviour
    {
        int errors;
        RenderTexture rt;
        const string OutDir = "Logs/PlayTest";
        static SaveData S => SaveData.Cur;
        static GameManager GM => GameManager.I;

        void Awake()
        {
            Application.logMessageReceived += (msg, st, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) { errors++; Debug.Log("[TEST] ERR " + msg + "\n" + st); }
            };
        }

        IEnumerator Start()
        {
            yield return null;
            SetupCapture();
            yield return Wait(0.5f);
            if (PlayTest.AnimOnly) { yield return Anim(); yield break; }
            if (PlayTest.RigOnly) { yield return RigShow(); yield break; }

            // 1) 시작 전투
            Log($"시작: Stage {SaveData.StageName(S.stage)}, 고양이 {Squad.I.cats.Count}, 총 {Tables.WeaponName(S.slots[0])}");
            Check(Squad.I.cats.Count == 1, "처음 분대 1마리");
            yield return WaitUntil(() => S.kills >= 3, 40f);
            Log($"처치 {S.kills}, 골드 {S.gold:0}, 좀비 {Target.All.Count}");
            Check(S.kills >= 3, "좀비를 잡음");
            Capture("01_battle");

            // 2) 강화 (튜토리얼 발바닥 → 사라짐)
            S.gold += 5000; GM.Notify();
            int ok = 0;
            for (int i = 0; i < 12; i++) if (GM.TryUpgrade(i % 3)) ok++;
            Log($"강화 {ok}번: 공격 Lv{S.lvAtk} 공속 Lv{S.lvSpd} 돈 Lv{S.lvGold}, 골드 {S.gold:0}, 튜토리얼 끝 {S.tutorialDone}");
            Check(ok == 12 && S.tutorialDone, "강화");
            yield return Wait(0.3f);
            Capture("02_upgrade");

            // 3) 1-5 보스 → 2-1
            S.stage = 4; S.kills = GM.Quota; S.bestEver = 4; GM.Notify();
            yield return WaitUntil(() => GM.boss != null && GM.boss.transform.position.y < Field.I.PlayTop - 1f, 30f);
            Check(GM.boss != null, "보스 등장");
            if (GM.boss != null)
            {
                Log($"보스: 체력 {GM.boss.hp:0}");
                Capture("03_boss");
                GM.boss.Hit(GM.boss.hp + 1, GM.boss.Pos);
            }
            yield return Wait(0.3f);
            Log($"보스 뒤: Stage {SaveData.StageName(S.stage)}");
            Check(S.stage == 5, "보스 잡고 2-1");

            // 3-0) 보스 실패 → 반복 사냥 → [보스 도전] → 처치
            S.stage = 9; S.kills = GM.Quota; GM.Notify();
            yield return WaitUntil(() => GM.boss != null, 10f);
            var oldBoss = GM.boss;
            GM.bossTime = 0.01f;
            yield return Wait(0.5f);
            Check(!GM.bossPhase && GM.BossFarm && GM.boss == null && (oldBoss == null || !oldBoss.Alive), "보스 실패 → 반복 사냥");
            int k0 = S.kills; double g1 = S.gold;
            yield return Wait(3f);
            int zs = 0; foreach (var t in Target.All) if (t.Alive) zs++;
            Log($"반복 사냥: 좀비 {zs}, 처치 {k0}→{S.kills}, 골드 {g1:0}→{S.gold:0}");
            Check(zs > 0 && S.kills == k0 && S.stage == 9, "반복 사냥 중 좀비 나옴 · 스테이지 그대로");
            var bb = GameObject.Find("StageGroup")?.transform.Find("ColorBtn"); yield return Wait(0.2f); Check(bb != null && bb.gameObject.activeSelf, "보스 도전 버튼");
            Capture("03b_boss_farm");
            Check(GM.ChallengeBoss() == null && GM.bossPhase && GM.boss != null, "보스 도전");
            if (GM.boss != null) GM.boss.Hit(GM.boss.hp + 1, GM.boss.Pos);
            yield return Wait(0.3f);
            Check(S.stage == 10 && !GM.BossFarm, "보스 잡고 3-1");

            // 3-1) 아래 버튼 줄(무기·스킨·보급품·보스·광산) + 위 상점·도전 → 스킨 버튼 눌러 창 열기
            var items = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int navItems = 0; Button skinItem = null;
            foreach (var b in items) if (b.name.StartsWith("Nav_")) { navItems++; if (b.name == "Nav_skin") skinItem = b; }
            Log($"아래 버튼 {navItems}개");
            Check(navItems == 5 && GameObject.Find("AutoUpgrade") != null && GameObject.Find("Top_shop") != null && GameObject.Find("Challenge") != null,
                "아래 버튼 5개 + 상점·도전·자동강화");
            Check(GameObject.Find("MenuBtn") == null && GameObject.Find("AmmoSlot0") != null, "메뉴 드롭다운 없어짐 + 총알 칸");
            Capture("02b_menu");
            skinItem?.onClick.Invoke();
            yield return Wait(0.4f);
            Check(HUD.I.Get<SkinPopup>() != null && HUD.I.Get<SkinPopup>().gameObject.activeSelf, "아래 버튼 → 스킨 창");
            CloseAll();

            // 4) 총알: 뽑기 4번 → 합성 2번 → 장착
            S.gems += 1000; GM.Notify();
            for (int i = 0; i < 4; i++) { var e = GM.Gacha(); if (e != null) { Log("뽑기 실패 " + e); errors++; } }
            Log($"뽑은 뒤 인벤토리: {Inv()}  다이아 {S.gems}");
            GM.Merge(0, 1); GM.Merge(2, 3);
            Log($"합성 뒤: {Inv()}");
            Check(S.inv[1] >= 0 && Tables.GradeOf(S.inv[1]) == 1 && Tables.GradeOf(S.inv[3]) == 1, "D+D → C");
            GM.Merge(1, 3);
            Check(Tables.GradeOf(S.inv[3]) == 2, "C+C → B");
            Log($"B 합성: {Tables.WeaponName(S.inv[3])}");
            GM.Gacha(); GM.Gacha();
            int a = S.inv.FindIndex(x => x >= 0 && Tables.GradeOf(x) == 0);
            GM.Equip(3, 1);
            if (a >= 0) GM.Equip(S.inv.FindIndex(x => x >= 0 && Tables.GradeOf(x) == 0), 2);
            Log($"장착: {string.Join(", ", S.slots.ConvertAll(Tables.WeaponName))}, 분대 {Squad.I.cats.Count}");
            Check(Squad.I.cats.Count == 3, "분대 3마리");
            string err = GM.Unequip(0); GM.Unequip(0);
            var e2 = GM.Unequip(0);
            Log($"모두 해제 시도: {e2}");
            Check(e2 != null && e2.Contains("최소"), "최소 1개 장착");
            // 다시 3칸 채우기
            for (int k = 0; k < 3; k++) { int ix = S.inv.FindIndex(x => x >= 0); int sl = S.slots.IndexOf(-1); if (ix >= 0 && sl >= 0) GM.Equip(ix, sl); }
            HUD.I.Open<WeaponPopup>();
            yield return Wait(0.4f);
            Capture("04_weapon");
            CloseAll();

            // 5) 스킨
            S.gems += 1000;
            var se = GM.BuySkin(3);
            Log($"스킨 3 구매: {(se ?? "성공")}, 입은 스킨 {S.skin}, 공격 보너스 +{Stats.SkinAtk}%");
            Check(se == null && S.skin == 3 && Stats.SkinAtk == 48, "스킨 구매·합산");
            GM.BuySkin(2);
            Check(Stats.SkinAtk == 48 + 32, "스킨 합산(2개)");
            HUD.I.Open<SkinPopup>();
            yield return Wait(0.4f);
            Capture("05_skin");
            CloseAll();
            yield return Wait(1.5f);
            Capture("06_squad3");

            // 6) 도전
            double g0 = S.gems;
            ConfirmPopup.Dungeon(ConfirmPopup.Kind.Challenge);
            yield return Wait(0.3f);
            Capture("07_challenge_confirm");
            CloseAll();
            var ce = GM.StartChallenge();
            Check(ce == null && GM.mode == GameManager.Mode.Challenge, "도전 시작");
            yield return Wait(2.5f);
            Log($"도전: 남은 {GM.DungeonLeft}/{GM.dungeonTotal}, 시간 {GM.dungeonTime:0.0}");
            Squad.I.ForceMove(new Vector2(2f, 3f));
            yield return Wait(1.2f);
            Capture("08_challenge");
            foreach (var t in Target.All.ToArray()) if (t.Alive) t.Hit(t.hp + 1, t.Pos);
            yield return Wait(0.4f);
            var rp = HUD.I.Get<ResultPopup>();
            Check(rp != null && rp.gameObject.activeSelf, "도전 결과 창");
            Capture("09_result");
            rp?.Close();
            yield return Wait(0.3f);
            Log($"도전 보상: 다이아 {g0} → {S.gems}, 깬 챕터 {string.Join(",", S.challengeCleared)}");
            Check(S.gems - g0 >= Tables.Challenge(1).reward - 0.1 && S.challengeCleared.Contains(1) && GM.mode == GameManager.Mode.Stage, "도전 보상·기록");

            // 7) 광산
            S.bestEver = 16; S.stage = 16; GM.AdvanceStage();
            S.RefreshDaily();
            int tk = S.mineTickets;
            var me = GM.StartMine();
            Check(me == null && S.mineTickets == tk - 1, "광산 입장·티켓");
            yield return Wait(4f);
            int dead = 0; foreach (var t in Target.All) if (!t.Alive) dead++;
            Log($"광산 4초: 다이아 {GM.dungeonGems:0.0}, 광석 {Target.All.Count}");
            Capture("10_mine");
            // 큰 보석 하나 직접 부수기 = +1
            double before = GM.dungeonGems;
            foreach (var t in Target.All.ToArray())
                if (t is Ore o && o.row.kind == "Big" && o.Alive) { for (int i = 0; i < 60 && o.Alive; i++) GM.Damage(o, 1e9, o.Pos); break; }
            Log($"큰 보석 부숨: {before:0.0} → {GM.dungeonGems:0.0} (고정 피해 1이라 40번 때림)");
            Check(GM.dungeonGems - before >= 0.99, "큰 보석 +1");
            GM.dungeonTime = 0.05f;
            yield return Wait(0.4f);
            Capture("11_mine_result");
            HUD.I.Get<ResultPopup>()?.Close();
            yield return Wait(0.3f);

            // 9) 상위 총알 (레이저·미사일·관통)
            S.slots[0] = Tables.Code(4, "laser"); S.slots[1] = Tables.Code(5, "missile"); S.slots[2] = Tables.Code(2, "pierce");
            Squad.I.Rebuild();
            yield return Wait(2.5f);
            Capture("14_high_weapons");

            // 10) 보스 던전
            S.bestEver = 46;
            var be = GM.StartBossRaid();
            Check(be == null && GM.mode == GameManager.Mode.Boss, "보스 던전 시작");
            yield return Wait(2f);
            Capture("15_boss_raid");
            GM.ExitDungeon();

            // 10-1) 기획 표 (Table/*.xlsx → csv) 값이 들어왔는지
            Log($"표: 1-5 보스 {Tables.Stage(4).bossHp:0.0}, 강화 Lv1 {Stats.Cost(1)}, 등급 {Tables.Grades.Length}개(끝 {Tables.GradeName(Tables.MaxGrade)}), 미션 {Tables.Missions.Count}, 보급품 {Tables.Supplies.Count}, 스킨 {Tables.Skins.Count}");
            Check(System.Math.Abs(Tables.Stage(4).bossHp - 6039.8) < 1 && Stats.Cost(1) == System.Math.Round(Tables.Upgrade(1).cost) && Tables.GradeName(12) == "SSU", "표: 스테이지·강화·등급");
            Check(Tables.Upgrade(1600).spd > Tables.Upgrade(1500).spd * 1.5 && Tables.Buff("buff_atk").mul == 2, "공속 곱 성장 · 광고 x2");
            {
                // 10-24) 기본 4종도 SSU 까지, 같은 등급 레이저·미사일보다 약함 · 이름 "종류_등급" · 총알 아이콘·보급품 그림이 표대로 있음
                bool all = true, weaker = true;
                for (int g = 4; g <= Tables.MaxGrade; g++)
                {
                    var types = Tables.TypesOfGrade(g);
                    all &= types.Count == 6;
                    var la = Tables.Weapon(Tables.Code(g, "laser")); var mi = Tables.Weapon(Tables.Code(g, "missile"));
                    foreach (var t in new[] { "single", "shotgun", "fast", "pierce" })
                    {
                        var w = Tables.Weapon(Tables.Code(g, t));
                        weaker &= w != null && w.atk * w.count / w.interval < System.Math.Max(la.atk * la.count / la.interval, mi.atk * mi.count / mi.interval);
                    }
                }
                Check(all && weaker && Tables.WeaponName(Tables.Code(12, "single")) == L.T("기본탄_SSU"), "S~SSU 6종 · 기본 4종이 더 약함 · 이름 종류_등급");
                bool icons = true;
                foreach (var t in Tables.Types) icons &= Resources.Load<Texture2D>("Art/" + Tables.Bullet(t).icon) != null && Resources.Load<Texture2D>("Art/" + Tables.Bullet(t).sprite) != null;
                foreach (var r in Tables.Supplies) icons &= SupplyPopup.Icon(r) != null;
                Check(icons, "총알 아이콘·발사체 그림 · 보급품 그림 25장 (표 Icon 칸)");
            }
            {
                // 공속 상한을 넘으면 넘친 만큼 공격력 (초당 피해가 공속에 비례)
                var w0 = Tables.Weapon(Tables.Code(0, "single"));
                int sv = S.lvSpd;
                S.lvSpd = 0; double d0 = Stats.Damage(w0) / Stats.Interval(w0);
                S.lvSpd = 900; double d1 = Stats.Damage(w0) / Stats.Interval(w0), want = Stats.Speed(900) / Stats.Speed(0);
                Log($"공속 넘침: 간격 {Stats.Interval(w0):0.###}초, 공격 x{Stats.SpeedOverflow(w0):0.##}, 초당 피해 {d1 / d0:0.#}배 (공속 {want:0.#}배)");
                Check(Stats.Interval(w0) <= 0.0601f && System.Math.Abs(d1 / d0 / want - 1) < 0.01, "공속 넘침 → 공격력 1:1");
                S.lvSpd = sv;
            }
            Check(Tables.Missions.Count == 5 && Tables.Supplies.Count == 25 && Tables.Skins.Count == 18 && Tables.Ores.Count == 4, "표: 미션·보급품·스킨·광석");
            Check(Tables.Stage(800).hp > Tables.Stage(749).hp && Tables.Upgrade(1600).cost > Tables.Upgrade(1500).cost, "표 끝 너머 이어서 계산");
            Check(GM.LockMsg("mine").Contains("4-1") && GM.Unlocked("boss_raid"), "표: 기능 열림");

            // 10-2) 보급품 (Supply_Collection)
            S.gems += 2000;
            var su = GM.BuySupply(1);
            Check(su == null && System.Math.Abs(Stats.SupplyAtk - 12) < 0.01 && System.Math.Abs(Stats.SupplyBoss - 3) < 0.01, "보급품 구매·효과");
            Check(GM.BuySupply(25) != null, "보급품 챕터 잠금");
            HUD.I.Open<SupplyPopup>(); yield return Wait(0.3f); Capture("16b_supply"); CloseAll();

            // 10-3) 총알 슬롯 강화 (Weapon_Enchant) · 분해
            S.slots[0] = Tables.Code(4, "laser"); Squad.I.Rebuild();
            S.gold += 1e7;
            int lv0 = S.slotLv[0];
            var ee = GM.EnchantSlot(0, out bool eok);
            Log($"슬롯 강화: {lv0} → {S.slotLv[0]} ({(ee ?? (eok ? "성공" : "실패"))}), 공격 +{Stats.SlotAtkPct(0):0.#}%, 최대 +{Stats.SlotMax(0)}");
            Check(ee == null && eok && S.slotLv[0] == lv0 + 1 && Stats.SlotAtkPct(0) > 0 && Stats.SlotMax(0) == 58, "슬롯 강화");
            S.slotLv[1] = 12; S.slots[1] = Tables.Code(0, "single");
            Check(GM.EnchantSlot(1, out _) != null && Stats.SlotLvEff(1) == 12, "D 등급 슬롯 최대 +12");
            S.slotLv[1] = 0; S.slots[1] = Tables.Code(5, "missile");
            int fi = S.inv.IndexOf(-1);
            if (fi >= 0)
            {
                S.inv[fi] = Tables.Code(3, "single");
                double gs = S.gems;
                GM.Salvage(fi);
                Check(S.inv[fi] < 0 && System.Math.Abs(S.gems - gs - 60) < 0.01, "A 등급 분해 +60");
            }
            Squad.I.Rebuild();
            var wp = HUD.I.Open<WeaponPopup>();
            yield return Wait(0.2f);
            var cell0 = GameObject.Find("Slot0")?.GetComponent<WeaponCell>();
            if (cell0) wp.Select(cell0);
            yield return Wait(0.2f);
            Capture("04b_enchant");
            CloseAll();

            // 10-4) 상점 골드 상자 (다이아 100)
            var box = Tables.Shop.Find(x => x.priceType == "Gems");
            if (box != null)
            {
                double gb = S.gold, db = S.gems;
                Check(GM.BuyShop(box) == null && S.gold > gb && S.gems == db - 100, "상점 골드 상자");
            }
            // 10-5) 10-20: 광고 무료 다이아 (하루 AdGems_Daily 번)
            {
                S.adGemUsed = 0;
                int n = (int)Tables.C("AdGems_Daily", 3);
                double g0a = S.gems;
                for (int i = 0; i < n; i++) GM.AdGems();
                Check(System.Math.Abs(S.gems - g0a - n * Tables.C("AdGems_Amount", 40)) < 0.01 && GM.AdGemsLeft == 0 && GM.AdGems() != null, $"광고 다이아 {n}번 x {Tables.C("AdGems_Amount", 40)}");
            }
            // 10-6) 미션 보상 = 단계마다 골드 → 다이아 번갈아 한 종류 (10-24), 스테이지 미션은 Max_Tier 에서 끝
            {
                int mi = Tables.Missions.FindIndex(m => m.id == "kill");
                long keep = S.statKills;
                int keepTier = S.missionTier[mi];
                for (int k = 0; k < 2; k++)
                {
                    S.missionTier[mi] = k;   // 0 = 골드 차례 · 1 = 다이아 차례
                    S.statKills = GM.MissionTarget(mi);
                    double gg = S.gems, go = S.gold, eg = GM.MissionGems(mi), eo = GM.MissionGold(mi);
                    bool one = k == 0 ? (eg == 0 && eo > 0) : (eg > 0 && eo == 0);
                    Check(one && GM.ClaimMission(mi) == null && System.Math.Abs(S.gems - gg - eg) < 0.01 && S.gold - go >= eo * 0.99,
                        $"미션 보상 {k + 1}단계 = {(k == 0 ? "골드 " + Num.F(eo) : "다이아 " + eg)} 한 종류만");
                }
                S.missionTier[mi] = keepTier;
                S.statKills = keep;
                int si = Tables.Missions.FindIndex(m => m.id == "stage");
                int tier = S.missionTier[si];
                S.missionTier[si] = Tables.Missions[si].maxTier;
                Check(Tables.Missions[si].maxTier == 150 && GM.MissionDone(si) && !GM.MissionReady(si) && GM.ClaimMission(si) != null, "스테이지 미션 150단계에서 끝");
                S.missionTier[si] = tier;
            }
            // 10-7) 영어 글자 표 (화면은 다시 만들지 않고 글자만 확인)
            {
                string keepLang = S.lang;
                S.lang = L.En;
                string shop = L.T("상점"), f = L.F("챕터 {0} 에서 열립니다", 3), skin = L.T(Tables.Skins[17].name);
                S.lang = L.Ko;
                Check(shop == "Shop" && f == "Unlocks at chapter 3" && skin == "Pirate Captain" && L.T("상점") == "상점", $"영어 글자 {shop} · {f} · {skin}");
                S.lang = keepLang;
            }
            Log($"미션 진행: 합성 {S.statMerges}, 보스 {S.statBossKills}");
            Check(S.statMerges > 0 && S.statBossKills >= 2, "합성·보스 미션 기록");

            // 11) 창들
            HUD.I.Open<MissionPopup>(); yield return Wait(0.3f); Capture("16_mission"); CloseAll();
            HUD.I.Open<ShopPopup>(); yield return Wait(0.3f); Capture("17_shop"); CloseAll();
            HUD.I.Open<SettingsPopup>(); yield return Wait(0.3f); Capture("18_settings"); CloseAll();
            var buff = GM.Buff(true);
            Check(buff == null && Stats.BuffAtk, "버프");

            // 12) 세이브 왕복
            S.Save();
            string json = PlayerPrefs.GetString(SaveData.Key);
            var back = JsonUtility.FromJson<SaveData>(json);
            Check(back.gems == S.gems && back.skins.Count == S.skins.Count && back.slots[0] == S.slots[0], "세이브 읽기");

            // 13) 숫자 표기
            Check(Num.F(1850) == "1.85k" && Num.F(14100000) == "14.1m" && Num.F(396) == "396" && Num.F(1600) == "1.6k" && Num.F(2.5e9) == "2.5b" && Num.F(1e12) == "1t" && Num.F(1e15) == "1aa" && Num.F(999999) == "999k" && Num.F(1e6) == "1m",
                "숫자 표기 " + Num.F(1850) + " " + Num.F(14100000) + " " + Num.F(2.5e9) + " " + Num.F(1e15));

            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        IEnumerator RigShow()
        {
            // 좀비 생성 멈추고 화면 비우기 → 12종을 4x3 으로 세움 + 분대 3마리
            S.slots[0] = Tables.Code(0, "single"); S.slots[1] = Tables.Code(0, "fast"); S.slots[2] = Tables.Code(1, "shotgun");
            Squad.I.Rebuild();
            GM.enabled = false;
            GM.ClearTargets();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c.isRootCanvas) c.enabled = false;
            string[] ids = { "basic", "hat", "mummy", "knife", "bat", "farmer", "armor", "fat", "ogre", "reaper", "crawler", "bucket" };
            var zs = new System.Collections.Generic.List<Zombie>();
            for (int i = 0; i < ids.Length; i++)
            {
                var row = Tables.Zombie(ids[i]);
                var p = new Vector2(-1.2f + (i % 4) * 2.1f, 3.4f - (i / 4) * 3.4f);
                var z = Zombie.Spawn(row, p, 1e12, 0);
                z.standStill = true;
                zs.Add(z);
            }
            yield return Wait(0.6f);
            int rigs = 0;
            foreach (var z in zs) { if (z.GetComponentInChildren<RigView>()) rigs++; z.enabled = false; }
            var capRigs = new System.Collections.Generic.List<RigView>();
            foreach (var c in Squad.I.cats) { var r = c.GetComponentInChildren<RigView>(); if (r) capRigs.Add(r); c.enabled = false; }
            Squad.I.enabled = false;
            Log($"뼈대: 좀비 {rigs}/{ids.Length}, 카피바라 {capRigs.Count}/{Squad.I.cats.Count}");
            Check(rigs == ids.Length && capRigs.Count == Squad.I.cats.Count, "뼈대 그림 붙음");
            // 걷기 16장 (좀비 걷기 + 카피바라 걷기)
            for (int k = 0; k < 16; k++)
            {
                float ph = k / 16f;
                foreach (var z in zs) { var r = z.GetComponentInChildren<RigView>(); if (r) { r.Flip = true; r.Walk(ph); } }
                foreach (var r in capRigs) r.Capy(k * 0.12f, 0, 0, ph);
                yield return null;
                Capture($"rig/walk_{k:00}", false);
            }
            // 멈춤(꿈틀) + 카피바라 반동 12장
            for (int k = 0; k < 12; k++)
            {
                foreach (var z in zs) { var r = z.GetComponentInChildren<RigView>(); if (r) r.Idle(k * 0.35f); }
                float kick = k < 6 ? 1f - k / 6f : 0f;
                foreach (var r in capRigs) r.Capy(k * 0.3f, 35f, kick, -1f);
                yield return null;
                Capture($"rig/idle_{k:00}", false);
            }
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        IEnumerator Anim()
        {
            // 분대 3마리 + 걷는 좀비 + 쓰러지는 좀비가 같이 보이게
            S.slots[0] = Tables.Code(0, "single"); S.slots[1] = Tables.Code(0, "fast"); S.slots[2] = Tables.Code(1, "shotgun");
            Squad.I.Rebuild();
            yield return Wait(2.5f);
            for (int i = 0; i < 40; i++)
            {
                Capture($"anim/{i:00}", false);
                yield return Wait(0.05f);
            }
            // 걷는 모습만(분대는 스테이지 자리에 고정 — 좀비가 걸어오는 동안 찍음)
            S.lvAtk = 0; GM.ClearTargets();
            yield return Wait(2.5f);
            for (int i = 0; i < 30; i++)
            {
                Capture($"walk/{i:00}", false);
                yield return Wait(0.05f);
            }
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        string Inv() => string.Join(",", S.inv.ConvertAll(x => x < 0 ? "-" : Tables.GradeName(Tables.GradeOf(x)) + Tables.TypeOf(x)[0]));

        void CloseAll()
        {
            foreach (var p in HUD.I.openPopups.ToArray()) if (!(p is ResultPopup)) p.Close();
        }

        void Check(bool ok, string what)
        {
            if (ok) Log("OK " + what);
            else { errors++; Log("FAIL " + what); }
        }

        static void Log(string s) => Debug.Log("[TEST] " + s);

        static IEnumerator Wait(float s)
        {
            float t = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < t) yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> c, float timeout)
        {
            float t = Time.realtimeSinceStartup + timeout;
            while (!c() && Time.realtimeSinceStartup < t) yield return null;
        }

        // ---------- 화면 찍기 (1080x2340, 폰 비율) ----------
        void SetupCapture()
        {
            rt = new RenderTexture(1080, 2340, 24);
            var cam = Camera.main;
            cam.targetTexture = rt;
            cam.ResetAspect();
            Field.I.Fit();
            var safe = Object.FindFirstObjectByType<SafeArea>();
            if (safe) { safe.enabled = false; ((RectTransform)safe.transform).Fill(); }
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 5;
                c.sortingOrder = 32767;
                var sc = c.GetComponent<CanvasScaler>();
                if (sc) sc.matchWidthOrHeight = 0;
            }
        }

        void Capture(string name, bool log = true)
        {
            Directory.CreateDirectory(Path.GetDirectoryName($"{OutDir}/{name}.png"));
            var cam = Camera.main;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
            Destroy(tex);
            if (log) Log("찍음 " + name);
        }
    }
}
