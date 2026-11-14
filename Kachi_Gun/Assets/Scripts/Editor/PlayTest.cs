using System.Collections;
using System.Collections.Generic;
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

        /// <summary>영어 화면 확인용: 언어를 English 로 바꿔 메인·창들을 촬영 → Logs/PlayTest/en/*.png (10-24)</summary>
        public static void RunEn()
        {
            SessionState.SetBool(Key + ".en", true);
            Run();
        }
        public static bool EnOnly => SessionState.GetBool(Key + ".en", false);

        /// <summary>스토어 스크린샷용: 진행한 세이브로 전투·보스·총알·상위 총알·보급품·스킨·광산·도전 촬영 → Logs/PlayTest/store/*.png (10-24)</summary>
        public static void RunStore()
        {
            SessionState.SetBool(Key + ".store", true);
            Run();
        }
        public static bool StoreOnly => SessionState.GetBool(Key + ".store", false);

        /// <summary>좀비 움직임 비교 촬영: 12종을 제자리에서 걷기 → 맞기 → 멈춤 (게임 시간 0.04초 간격) → Logs/PlayTest/zshow/*.png + pos.json (11-04)</summary>
        public static void RunZombie()
        {
            SessionState.SetBool(Key + ".zshow", true);
            Run();
        }
        public static bool ZombieOnly => SessionState.GetBool(Key + ".zshow", false);

        /// <summary>글꼴 비교 촬영(11-05): 한국어 메인·창들 → Logs/PlayTest/font/*.png — 글꼴은 환경 변수 KACHI_FONT · KACHI_FONT_SCALE</summary>
        public static void RunFont()
        {
            SessionState.SetBool(Key + ".font", true);
            Run();
        }
        public static bool FontOnly => SessionState.GetBool(Key + ".font", false);

        /// <summary>모바일 광고 영상용(11-07): 실제 게임을 1080x1920 으로 초당 30장 연속 촬영 → Logs/PlayTest/ad/장면/0000.jpg (tools/ad_video.py 가 영상으로)</summary>
        public static void RunAd()
        {
            SessionState.SetBool(Key + ".ad", true);
            Run();
        }
        public static bool AdOnly => SessionState.GetBool(Key + ".ad", false);

        /// <summary>11-08 성능 측정: 진행한 세이브로 스테이지·창·던전을 5바퀴 → Logs/PlayTest/perf/&lt;KACHI_PERF_LABEL&gt;.json (PlayTestPerf.cs)</summary>
        public static void RunPerf()
        {
            SessionState.SetBool(Key + ".perf", true);
            Run();
        }
        public static bool PerfOnly => SessionState.GetBool(Key + ".perf", false);
        public static bool Main => !AnimOnly && !RigOnly && !EnOnly && !StoreOnly && !ZombieOnly && !FontOnly && !AdOnly && !PerfOnly;

        public static void Run()
        {
            SaveData.ResetAll();
            ResetCloud();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>11-07 클라우드 시험 흔적(연결 상태 · 가짜 클라우드 칸) 지우기</summary>
        public static void ResetCloud()
        {
            CloudAuth.ResetForTest();
            PlayerPrefs.DeleteKey(CloudSave.FakeKey);
            CloudSave.SimulateFail = false;
            CloudSave.SimulateHang = false;
            CloudSync.TimeoutSec = 20f;
            CloudAuth.SimulateCancel = false;
            CloudAuth.SimulateAutoLogin = false;
            PlayerPrefs.Save();
        }

        /// <summary>연결 상태만 지움 (가짜 클라우드 칸의 데이터는 둠) — "새 폰에서 처음 켬" 시험</summary>
        public static void ResetCloudKeepData()
        {
            string keep = PlayerPrefs.GetString(CloudSave.FakeKey, "");
            ResetCloud();
            PlayerPrefs.SetString(CloudSave.FakeKey, keep);
            PlayerPrefs.Save();
        }

        public static void Finish(int errors)
        {
            SaveData.ResetAll();
            ResetCloud();
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".anim", false);
            SessionState.SetBool(Key + ".rig", false);
            SessionState.SetBool(Key + ".en", false);
            SessionState.SetBool(Key + ".store", false);
            SessionState.SetBool(Key + ".zshow", false);
            SessionState.SetBool(Key + ".font", false);
            SessionState.SetBool(Key + ".ad", false);
            SessionState.SetBool(Key + ".perf", false);
            GameManager.HoldFire = false;   // 11-07 스토어 촬영 사격 멈춤 풀기
            EditorApplication.Exit(errors == 0 ? 0 : 3);
        }
    }

    public partial class PlayTestRunner : MonoBehaviour
    {
        int errors;
        RenderTexture rt;
        readonly System.Collections.Generic.List<float> spawnYs = new();
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
            if (!PlayTest.Main) { var ap0 = HUD.I.Get<AttendPopup>(); if (ap0 && ap0.gameObject.activeSelf) ap0.Close(); }   // 촬영 모드엔 출석 창이 안 끼게
            if (PlayTest.AnimOnly) { yield return Anim(); yield break; }
            if (PlayTest.RigOnly) { yield return RigShow(); yield break; }
            if (PlayTest.EnOnly) { yield return EnTour(); yield break; }
            if (PlayTest.StoreOnly) { yield return StoreTour(); yield break; }
            if (PlayTest.ZombieOnly) { yield return ZombieShow(); yield break; }
            if (PlayTest.FontOnly) { yield return FontTour(); yield break; }
            if (PlayTest.AdOnly) { yield return AdTour(); yield break; }
            if (PlayTest.PerfOnly) { yield return PerfTour(); yield break; }

            // 출석 창이 시험 중간(10초마다 날짜 확인)에 끼어들지 않게 — 출석은 11f 에서 따로
            // 11-04 밤: 처음 켜자마자 출석 창(1일차) → 받기 → 다이아 +100 (뒤 시험에 영향 없게 다이아는 되돌림)
            {
                var ap0 = HUD.I.Get<AttendPopup>();
                Check(ap0 != null && ap0.gameObject.activeSelf && ap0.index == 0, "처음 켬 → 출석 창 (1일차)");
                Capture("18h_attend_first");
                double fg = S.gems;
                if (ap0) ap0.Close();
                Check(S.gems - fg == 100 && S.AttendToday && !HUD.I.PopupOpen, $"처음 출석 → 다이아 +{S.gems - fg}");
                S.gems = fg; S.attendCount = 0; GM.Notify();
            }

            // 11-08 성능: 자주 여는 창은 켤 때 미리 만들어 닫아 둠 · 보스·던전 배경음은 따로 일꾼이 미리 계산
            {
                var pwp = HUD.I.Get<WeaponPopup>(); var rp0 = HUD.I.Get<ResultPopup>();
                Check(pwp != null && !pwp.gameObject.activeSelf && rp0 != null && !rp0.gameObject.activeSelf, "창 미리 만들어 둠 (총알·결과 창, 닫힌 채)");
                var bt = typeof(AudioManager).GetField("bossTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(AudioManager.I) as System.Threading.Tasks.Task;
                Check(bt != null, "보스·던전 배경음 미리 계산 시작");
            }

            // 1) 시작 전투
            Log($"시작: Stage {SaveData.StageName(S.stage)}, 고양이 {Squad.I.cats.Count}, 총 {Tables.WeaponName(S.slots[0])}");
            Check(Squad.I.cats.Count == 1, "처음 분대 1마리");
            Check(!Squad.I.cats[0].Angry, "처음엔 평소 얼굴 (좀비가 멀리)");
            yield return WaitUntil(() => S.kills >= 3, 40f);
            Log($"처치 {S.kills}, 골드 {S.gold:0}, 좀비 {Target.All.Count}");
            Check(S.kills >= 3, "좀비를 잡음");
            Capture("01_battle");

            // 1b) 11-05 몰려오기: 처치 목표·한 마리 체력 · 무리로 한꺼번에 · 화면에 더 많이
            {
                float hm = GameManager.HordeMul;
                var st0 = Tables.Stage(S.stage);
                Check(hm > 1f && GM.Quota == Mathf.RoundToInt(st0.quota * hm), $"몰려오기 x{hm} → 처치 목표 {st0.quota} → {GM.Quota}");
                int peak = 0, jump = 0, prev = -1; Zombie one = null; bool angrySeen = false;
                var seen = new System.Collections.Generic.HashSet<Zombie>(); spawnYs.Clear();
                for (float t = 0; t < 8f; t += 0.1f)
                {
                    int na = 0;
                    foreach (var tg in Target.All) if (tg is Zombie z && z.Alive && !z.isBoss) { na++; if (!one) one = z; if (seen.Add(z)) spawnYs.Add(z.transform.position.y); }
                    peak = Mathf.Max(peak, na); if (prev >= 0) jump = Mathf.Max(jump, na - prev); prev = na;
                    if (t > 0.5f && na >= peak && na >= 12) { Capture("01b_horde"); }
                    if (Squad.I.cats[0].Angry) angrySeen = true;   // 11-05: 한 순간만 보면 가까운 좀비가 막 죽었을 때 실패 → 8초 동안 한 번이라도
                    yield return Wait(0.1f);
                }
                Check(angrySeen, "좀비가 가까이 오면 화난 얼굴");
                Capture("01c_angry");
                Log($"몰려오기: 화면 최대 {peak}마리 (예전 최대 {st0.maxAlive}) · 한 번에 늘어난 최대 {jump}마리");
                Check(peak > st0.maxAlive, $"좀비가 예전 최대({st0.maxAlive})보다 많이 보임 ({peak})");
                Check(jump >= Mathf.Min(3, Tables.Ci("Horde_Pack_Min")), $"무리로 한꺼번에 나옴 (+{jump})");
                if (one) Check(System.Math.Abs(one.maxHp / one.row.hpMul - Stats.ZombieHp(S.stage) / hm) < 0.01 * Stats.ZombieHp(S.stage), $"좀비 한 마리 체력 = 표 ÷ {hm} ({one.maxHp:0.#})");
                int mk = Tables.Missions.FindIndex(m => m.statKey == "statKills");
                if (mk >= 0) Check(GM.MissionTarget(mk) == (long)System.Math.Round(Tables.MissionTarget(mk, S.missionTier[mk]) * (double)hm), $"처치 미션 목표 x{hm} ({GM.MissionTarget(mk)})");
                if (!File.Exists($"{OutDir}/01b_horde.png")) Capture("01b_horde");
                // 11-05 2차: 위아래 고르게 — 지금까지 나온 좀비 높이를 흙 띠 세 칸(위·가운데·아래)으로 나눠 셈
                {
                    float y0 = Field.I.SpawnPoint(-999f).y, y1 = Field.I.SpawnPoint(999f).y;
                    var cnt = new int[3]; int tot = 0;
                    foreach (var y in spawnYs) { int k = Mathf.Clamp((int)((y - y0) / (y1 - y0 + 0.001f) * 3), 0, 2); cnt[k]++; tot++; }
                    Log($"나온 높이: 아래 {cnt[0]} · 가운데 {cnt[1]} · 위 {cnt[2]} (모두 {tot})");
                    Check(tot >= 12 && Mathf.Min(cnt[0], Mathf.Min(cnt[1], cnt[2])) >= tot * 0.2f, "좀비가 위아래 고르게 나옴 (세 칸 모두 20% 이상)");
                }
            }

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
            // 11-04 요청_02: 같은 탄알(종류·등급)끼리만 합성 → 11-14 사용자 "같은 무기가 아니어도 합성": 같은 등급이면 종류가 달라도 (Merge_Same_Grade 1)
            S.inv[0] = Tables.Code(0, "single"); S.inv[1] = Tables.Code(0, "shotgun");
            long mgx = S.statMerges;
            string me0 = GM.Merge(0, 1);
            Check(GameManager.MergeAnyType && me0 == null && S.inv[0] < 0 && S.inv[1] >= 0 && Tables.GradeOf(S.inv[1]) == 1 && S.statMerges == mgx + 1,
                  $"D 단발 + D 산탄 → C 합성 (11-14 같은 등급이면 종류가 달라도) → {Tables.WeaponName(S.inv[1])}");
            S.inv[0] = Tables.Code(0, "single"); S.inv[1] = Tables.Code(1, "single");
            GM.Merge(0, 1);
            Check(Tables.GradeOf(S.inv[0]) == 1 && Tables.GradeOf(S.inv[1]) == 0 && S.statMerges == mgx + 1, "D 단발 + C 단발 → 합성 안 됨(자리만 바꿈)");
            S.inv[0] = Tables.Code(0, "single");
            S.inv[1] = Tables.Code(0, "single"); S.inv[2] = Tables.Code(0, "fast"); S.inv[3] = Tables.Code(0, "fast");
            GM.Merge(0, 1); GM.Merge(2, 3);
            Log($"합성 뒤: {Inv()}");
            Check(S.inv[0] < 0 && S.inv[1] >= 0 && Tables.GradeOf(S.inv[1]) == 1 && Tables.GradeOf(S.inv[3]) == 1, "같은 D 두 개 → C");
            S.inv[1] = S.inv[3];   // 같은 C 두 개로 맞춤 (합성 결과 종류는 무작위)
            GM.Merge(1, 3);
            Check(Tables.GradeOf(S.inv[3]) == 2, "같은 C 두 개 → B");
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

            // 4b) 11-05 추가 요청/UI 수정.png: 분대 자리 · 아래 탭 이름 · 일괄 합성(한 단계만) · 분해 여러 개 고르기
            {
                var cs = Squad.I.cats;
                float ly = cs[0].transform.position.y, y1 = cs[1].transform.position.y, y2 = cs[2].transform.position.y;
                Log($"분대 자리 y: 리더 {ly:0.00} · 동료1 {y1:0.00} · 동료2 {y2:0.00}");
                Check(cs[0].slot == 0 && cs[1].slot == 1 && cs[2].slot == 2 && y1 > ly && ly > y2, "분대 자리: 리더 가운데 · 동료1 위 · 동료2 아래");
                // 11-07 사용자: 리더 카피바라 조금 크게 · 아래 총알 3칸 = 카피바라 위치 순서(동료1 | 리더 | 동료2)
                float ls = cs[0].transform.localScale.x, ss = cs[1].transform.localScale.x;
                Check(Mathf.Abs(ls / ss - Capy.LeaderMul) < 0.01f && Capy.LeaderMul > 1f, $"리더 카피바라 크기 x{ls / ss:0.00} (동료 대비)");
                float ax(int k) { var g = GameObject.Find("AmmoSlot" + k); return g ? ((RectTransform)g.transform).anchoredPosition.x : float.NaN; }
                Check(ax(1) < ax(0) && ax(0) < ax(2) && Mathf.Abs(ax(0)) < 1f, $"아래 총알 칸: 동료1 {ax(1):0} | 리더 {ax(0):0} | 동료2 {ax(2):0}");
                string NavText(string id) { var g = GameObject.Find("Nav_" + id); var t = g ? g.GetComponentInChildren<Text>() : null; return t ? t.text : ""; }
                Check(NavText("weapon") == "총알" && NavText("skin") == "코스튬", $"아래 탭 이름 총알 · 코스튬 ({NavText("weapon")} · {NavText("skin")})");

                for (int k = 0; k < S.inv.Count; k++) S.inv[k] = -1;
                int dS = Tables.Code(0, "single"), dF = Tables.Code(0, "fast"), cS = Tables.Code(1, "single");
                S.inv[0] = dS; S.inv[3] = dS; S.inv[5] = dS; S.inv[6] = dS; S.inv[7] = dS;   // D 단발 5개 → 2번 합성 + 1개 남음
                S.inv[8] = dF;                                                          // 11-14 D 연사 = 남은 D 단발(7)과 짝 → 1번 (종류가 달라도)
                S.inv[9] = cS; S.inv[10] = cS;                                          // C 두 개 → 1번
                S.inv[11] = Tables.Code(2, "single");                                   // B 하나 = 짝 없음
                long mg0 = S.statMerges;
                var made = GM.MergeAll();
                Log($"일괄 합성: {made.Count}번 → {Inv()}");
                Check(made.Count == 4 && S.statMerges == mg0 + 4, $"일괄 합성 4번 ({made.Count})");
                Check(Tables.GradeOf(S.inv[0]) == 1 && Tables.GradeOf(S.inv[5]) == 1 && Tables.GradeOf(S.inv[7]) == 1 && S.inv[3] < 0 && S.inv[6] < 0 && S.inv[8] < 0
                      && Tables.GradeOf(S.inv[9]) == 2 && S.inv[10] < 0 && S.inv[11] == Tables.Code(2, "single"),
                      "일괄 합성: 같은 등급 두 개씩(D 단발 + D 연사도) · 결과는 앞 칸 · 짝 없는 B 그대로");
                Check(S.inv.FindAll(x => x >= 0 && Tables.GradeOf(x) == 1).Count == 3 && S.inv.FindAll(x => x >= 0 && Tables.GradeOf(x) >= 2).Count == 2,
                      "일괄 합성: 새로 나온 C 3개는 이번엔 다시 안 합침(한 단계만)");

                var wpu = HUD.I.Open<WeaponPopup>();
                yield return Wait(0.3f);
                Check(wpu.title.text == "총알 관리" && GameObject.Find("MergeAll") && GameObject.Find("Gacha") && GameObject.Find("Salvage"), "총알 관리 창: 제목 · 아래 버튼 3개");
                {
                    var gno = GameObject.Find("GachaNote"); var gnt = gno ? gno.GetComponent<Text>() : null;
                    Check(gnt != null && gnt.text == L.T("※ 뽑기는 무조건 D 등급 총알만 나옵니다") && gnt.text.Contains("D"), $"11-14 총알 관리 창 뽑기 안내 ({(gnt ? gnt.text : "없음")})");
                    Capture("04b_weapon_gacha_note");
                }
                GameObject.Find("Salvage").GetComponent<Button>().onClick.Invoke();
                Check(wpu.Picking, "[분해] 누름 → 고르기");
                var c7 = GameObject.Find("Inv7").GetComponent<WeaponCell>(); var c8 = GameObject.Find("Inv11").GetComponent<WeaponCell>();   // 11-14 일괄 합성 뒤 남은 칸 (C · B)
                wpu.Select(c7); wpu.Select(c8); wpu.Select(GameObject.Find("Slot0").GetComponent<WeaponCell>());
                Check(wpu.picked.Count == 2, $"분해 고르기 2개(장착 칸은 안 됨) ({wpu.picked.Count})");
                yield return Wait(0.2f);
                Capture("04c_salvage_pick");
                double sg = S.gems, want = Tables.Grade(1).salvage + Tables.Grade(2).salvage;
                double got = GM.SalvageMany(new System.Collections.Generic.List<int>(wpu.picked));
                Check(S.inv[7] < 0 && S.inv[11] < 0 && System.Math.Abs(S.gems - sg - want) < 0.01 && System.Math.Abs(got - want) < 0.01, $"여러 개 분해 → 다이아 +{got}");
                GameObject.Find("MergeAll").GetComponent<Button>().onClick.Invoke();   // 고르기 중 = [취소]
                Check(!wpu.Picking, "고르기 중 왼쪽 버튼 = 취소");
                wpu.Select(GameObject.Find("Inv0").GetComponent<WeaponCell>());
                yield return Wait(0.3f);
                var info = HUD.I.Get<WeaponInfoPopup>();
                Check(info != null && info.gameObject.activeSelf && !info.isSlot && info.index == 0, "인벤토리 총알 누름 → 정보 창");
                Capture("04d_info_inv");
                {
                    bool equipBtn = false;
                    foreach (var b in info.GetComponentsInChildren<Button>(false)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("장착")) equipBtn = true; }
                    Check(!equipBtn, "인벤토리 총알 정보 창: [장착] 버튼 없음 (11-07 끌어다 놓기만)");
                }
                CloseAll();
                {
                    // 11-07 카드 아무 데나 끌어다 놓아도 그 칸에 장착
                    var wp2 = HUD.I.Open<WeaponPopup>();
                    yield return Wait(0.2f);
                    CardDrop card1 = null;
                    foreach (var cd in wp2.GetComponentsInChildren<CardDrop>(true)) if (cd.cell != null && cd.cell.index == 1) card1 = cd;
                    int from = S.inv.FindIndex(x => x >= 0);
                    WeaponCell fromCell = null;
                    foreach (var wc in wp2.GetComponentsInChildren<WeaponCell>(true)) if (!wc.isSlot && wc.index == from) fromCell = wc;
                    int code = from >= 0 ? S.inv[from] : -1, old1 = S.slots[1];
                    if (card1 != null && fromCell != null) wp2.Drop(fromCell, card1.cell);
                    Check(card1 != null && code >= 0 && S.slots.Contains(code) && S.inv[from] == old1, $"카드에 끌어다 놓기 → 장착 (동료 1 칸 {Tables.WeaponName(code)})");
                    CloseAll();
                }
                // 다시 3칸 채우기(뒤 시험용)
                for (int k = 0; k < 3; k++) if (S.slots[k] < 0) { int ix = S.inv.FindIndex(x => x >= 0); if (ix >= 0) GM.Equip(ix, k); }
            }

            // 5) 스킨
            S.gems += Tables.Skins.Find(r => r.id == 3).price + Tables.Skins.Find(r => r.id == 2).price;   // 표 가격대로 (11-04 스킨 재밸런스)
            var se = GM.BuySkin(3);
            Log($"스킨 3 구매: {(se ?? "성공")}, 입은 스킨 {S.skin}, 공격 보너스 +{Stats.SkinAtk}%");
            Check(se == null && S.skin == 3 && Stats.SkinAtk == Tables.Skins.Find(r => r.id == 3).atk, $"스킨 구매·합산 (+{Stats.SkinAtk}%)");
            GM.BuySkin(2);
            Check(Stats.SkinAtk == Tables.Skins.Find(r => r.id == 3).atk + Tables.Skins.Find(r => r.id == 2).atk, $"스킨 합산(2개) +{Stats.SkinAtk}%");
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
            // 11-05 보상 있는 광석을 모두 깨면 시간이 남아도 자동으로 끝
            foreach (var t in Target.All.ToArray())
                if (t is Ore o2 && o2.Alive && o2.reward > 0) for (int i = 0; i < 80 && o2.Alive; i++) GM.Damage(o2, 1e9, o2.Pos);
            float tLeft = GM.dungeonTime;
            yield return Wait(0.3f);
            Check(HUD.I.Get<ResultPopup>() == null || !HUD.I.Get<ResultPopup>().gameObject.activeSelf, "다 깬 직후엔 아직 안 끝남 (0.8초 기다림)");
            yield return Wait(0.8f);
            var mineRp = HUD.I.Get<ResultPopup>();
            Check(GM.MineRewardLeft == 0 && mineRp != null && mineRp.gameObject.activeSelf && tLeft > 5f, $"보상 광석 다 깸 → 자동 끝 (남은 시간 {tLeft:0}초, 다이아 {GM.dungeonGems:0.0})");
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
                int nLaser = 0, nSingle = 0, N = 20000;
                for (int k = 0; k < N; k++) { string t = Tables.RandomType(5); if (t == "laser") nLaser++; else if (t == "single") nSingle++; }
                Log($"합성 확률(SS): 레이저 {nLaser * 100f / N:0.0}% · 기본탄 {nSingle * 100f / N:0.0}%");
                Check(System.Math.Abs(nLaser * 100.0 / N - 14) < 1.2 && System.Math.Abs(nSingle * 100.0 / N - 18) < 1.2, "합성 확률 = 표 Merge_Weight (일반탄 18% · 레어탄 14%)");
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
            // 11-05 밤 사용자 "슬롯 강화 기능은 제거": 강화 안 됨 · 옛 세이브의 강화 레벨도 효과 0
            S.slotLv[0] = 5;
            var ee = GM.EnchantSlot(0, out bool eok);
            Log($"슬롯 강화 시도: {ee} · 옛 레벨 {S.slotLv[0]} → 효과 레벨 {Stats.SlotLvEff(0)}, 공격 +{Stats.SlotAtkPct(0):0.#}%");
            Check(!Stats.SlotEnchantOn && ee != null && !eok && S.slotLv[0] == 5 && Stats.SlotLvEff(0) == 0 && Stats.SlotAtkPct(0) == 0 && Stats.BossMul(0) == Stats.BossMul(), "슬롯 강화 없음(옛 레벨도 효과 0)");
            S.slotLv[0] = 0;
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
            {
                var ip = HUD.I.Get<WeaponInfoPopup>();
                var rbtn = ip ? ip.transform.Find("Panel/Content/Right") : null;
                var plusT = cell0 ? cell0.GetComponentsInChildren<Text>() : new Text[0];
                bool noPlus = true; foreach (var t in plusT) if (t.text.StartsWith("+")) noPlus = false;
                Check(ip != null && ip.gameObject.activeSelf && rbtn != null && !rbtn.gameObject.activeSelf && noPlus, "장착 카드: +N 없음 · 정보 창에 [강화] 없음");
            }
            CloseAll();

            // 10-4) 상점 골드 상자 (다이아 100)
            var box = Tables.Shop.Find(x => x.priceType == "Gems");
            if (box != null)
            {
                double gb = S.gold, db = S.gems;
                Check(GM.BuyShop(box) == null && S.gold > gb && S.gems == db - 100, "상점 골드 상자");
            }
            // 10-4b) 10-26: 결제 상품 5개(원화) · 뺀 상품 없음 · 가격 글자 — 11-13 SS 미사일탄 990원 더해 6개
            {
                var cash = Tables.Shop.FindAll(x => x.priceType == "KRW");
                string[] gone = { "remove_forced_ads", "perma_buff", "mine_ticket", "hotdeal_laser", "gold_instant_30m" };
                var ads = Tables.Shop.Find(x => x.id == "remove_all_ads");
                Check(cash.Count == 6 && !Tables.Shop.Exists(x => System.Array.IndexOf(gone, x.id) >= 0)
                      && ads != null && ShopPopup.KrwText(ads.priceValue) == L.F("{0}원", "9,900"), $"상점 결제 상품 6개 원화 ({cash.Count}개)");
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
            S.attendDay = System.DateTime.Now.ToString("yyyyMMdd");   // 촬영에 출석 창이 안 끼게
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

            // 11c) 11-04 게임 종료: 뒤로 가기(창 없음) → 종료 창 · 뒤로 가기는 맨 위 창부터 닫음 · 설정 [게임 종료] · [종료] = 저장 후 종료
            {
                HUD.I.Back(); yield return Wait(0.3f);
                var q = HUD.I.Get<QuitPopup>();
                Check(q != null && q.gameObject.activeSelf, "뒤로 가기(열린 창 없음) → 종료 확인 창");
                Capture("18b_quit_back");
                HUD.I.Back();
                Check(q != null && !q.gameObject.activeSelf, "뒤로 가기 한 번 더 → 종료 창 닫힘(취소)");
                var st = HUD.I.Open<SettingsPopup>(); yield return Wait(0.3f);
                Button qb = null;
                foreach (var b in st.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("게임 종료")) qb = b; }
                Check(qb != null, "설정 창 [게임 종료] 버튼");
                if (qb != null) qb.onClick.Invoke();
                yield return Wait(0.3f);
                Capture("18c_settings_quit");
                Check(q.gameObject.activeSelf && HUD.I.openPopups[HUD.I.openPopups.Count - 1] == q, "설정 [게임 종료] → 종료 창이 맨 위");
                HUD.I.Back();
                Check(!q.gameObject.activeSelf && st.gameObject.activeSelf, "뒤로 가기 → 종료 창만 닫히고 설정 창은 남음");
                HUD.I.Back();
                Check(!st.gameObject.activeSelf && HUD.I.openPopups.Count == 0, "뒤로 가기 → 설정 창 닫힘");
                HUD.I.Back();
                Button yes = null;
                foreach (var b in q.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("종료")) yes = b; }
                int qc = QuitPopup.editorQuitCount;
                if (yes != null) yes.onClick.Invoke();
                Check(QuitPopup.editorQuitCount == qc + 1, "[종료] → 저장 후 종료 (에디터는 꺼지지 않음)");
                CloseAll();
            }

            // 11d) 11-04 방치 보상: 1시간 비움 → 창·골드 · 10시간 = 최대 8시간 · 2분 = 없음 · 시계 뒤로 = 없음 · 광고 x2 · 뒤로 가기 = 기본 보상
            {
                CloseAll();
                double gps = GM.OfflineGoldPerSec;
                Log($"방치 초당 골드 {gps:0.##} (스테이지 {SaveData.StageName(S.stage)} · 분대 초당 피해 {Stats.SquadDps():0} · 좀비 체력 {Stats.ZombieHp(S.stage):0} · 좀비 골드 {Stats.ZombieGold(S.stage):0})");
                Check(gps > 0, "방치 초당 골드 > 0");
                S.loadedLastSeen = 0; S.lastSeen = SaveData.Now - 3600;
                bool shown = GM.CheckOffline(); yield return Wait(0.3f);
                var op = HUD.I.Get<OfflinePopup>();
                Check(shown && op != null && op.gameObject.activeSelf && System.Math.Abs(op.sec - 3600) < 5, $"1시간 비움 → 방치 보상 창 ({op?.sec:0}초)");
                Capture("18d_offline");
                double og0 = S.gold, want = op != null ? op.gold : 0;
                Check(want > 0 && System.Math.Abs(want - gps * op.sec) < want * 0.01 + 1, $"방치 골드 {Num.F(want)} = 초당 x 시간");
                Button take = null, adb = null;
                foreach (var b in op.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt == null) continue; if (lt.text == L.T("받기")) take = b; else if (lt.text.StartsWith(L.T("광고 보고"))) adb = b; }
                if (take != null) take.onClick.Invoke();
                Check(!op.gameObject.activeSelf && System.Math.Abs(S.gold - og0 - want) < want * 0.001 + 1, $"[받기] → 골드 +{Num.F(S.gold - og0)}");
                Check(!GM.CheckOffline(), "받은 뒤 바로 다시 → 없음 (두 번 안 줌)");

                S.lastSeen = SaveData.Now - 36000;
                GM.CheckOffline(); yield return Wait(0.2f);
                Check(System.Math.Abs(op.sec - GameManager.OfflineMaxSec) < 1, $"10시간 비움 → 최대 {GameManager.OfflineMaxSec / 3600:0.#}시간만");
                string maxHm = OfflinePopup.HhMm((long)GameManager.OfflineMaxSec);
                System.Func<string> offTime = () => { foreach (var t in op.GetComponentsInChildren<Text>(true)) if (t.text.StartsWith(L.T("오프라인 시간"))) return t.text; return ""; };
                Check(offTime().EndsWith(maxHm), $"10시간 비움 → 창 시간도 {maxHm} 에서 멈춤 ({offTime()})");
                double goldMax = op.gold;
                S.lastSeen = SaveData.Now - 18000;   // 창을 안 받고 또 5시간 비움 → 더해져도 최대 그대로
                GM.CheckOffline(); yield return Wait(0.2f);
                Check(System.Math.Abs(op.sec - GameManager.OfflineMaxSec) < 1 && System.Math.Abs(op.gold - goldMax) < goldMax * 0.001 + 1 && offTime().EndsWith(maxHm),
                    $"안 받고 또 비움 → 시간·골드 최대 그대로 ({offTime()} · {Num.F(op.gold)})");
                Capture("18e_offline_max");
                double og1 = S.gold, want1 = op.gold;
                if (adb != null) adb.onClick.Invoke();   // 에디터 = 광고 없이 바로 보상
                Check(System.Math.Abs(S.gold - og1 - want1 * Tables.C("Offline_AdMul", 2)) < want1 * 0.001 + 1, "[광고 보고] → x2");

                S.lastSeen = SaveData.Now - 120;
                Check(!GM.CheckOffline(), "2분 비움 → 없음");
                S.lastSeen = SaveData.Now + 7200;
                Check(!GM.CheckOffline(), "기기 시계를 뒤로 돌림 → 없음");
                Check(S.lastSeen >= SaveData.Now + 7000, "시계를 뒤로 돌려도 마지막으로 본 시각은 줄지 않음 (11-07 — 되돌릴 때 8시간을 또 주지 않게)");
                S.Save();
                Check(S.lastSeen >= SaveData.Now + 7000, "저장해도 마지막으로 본 시각은 앞으로만");

                S.lastSeen = SaveData.Now - 3600;
                GM.CheckOffline(); yield return Wait(0.2f);
                double og2 = S.gold, want2 = op.gold;
                HUD.I.Back();
                Check(!op.gameObject.activeSelf && System.Math.Abs(S.gold - og2 - want2) < want2 * 0.001 + 1, "뒤로 가기 → 방치 창 닫히고 기본 보상");

                // 11-12 코드 점검: 창이 뜬 채로 앱이 꺼져도 그 시간은 세이브에 남아 다음에 이어서 줌
                S.lastSeen = SaveData.Now - 3600;
                GM.CheckOffline(); yield return Wait(0.2f);
                var pendSave = SaveData.Parse(PlayerPrefs.GetString(SaveData.Key));
                Check(System.Math.Abs(S.offlinePendingSec - 3600) < 5 && pendSave != null && System.Math.Abs(pendSave.offlinePendingSec - 3600) < 5, $"방치 창을 띄우면 못 받은 시간을 세이브에 적음 ({S.offlinePendingSec:0}초)");
                op.gameObject.SetActive(false); HUD.I.openPopups.Remove(op);   // 받지 않고 앱이 꺼진 셈
                S.lastSeen = SaveData.Now;                                     // 바로 다시 켬 (새로 비운 시간 없음)
                bool again = GM.CheckOffline(); yield return Wait(0.2f);
                Check(again && op.gameObject.activeSelf && System.Math.Abs(op.sec - 3600) < 5, $"못 받고 꺼짐 → 다음에 켤 때 창이 다시 ({op.sec:0}초)");
                double og3 = S.gold, want3 = op.gold;
                if (take != null) take.onClick.Invoke();
                Check(System.Math.Abs(S.gold - og3 - want3) < want3 * 0.001 + 1 && S.offlinePendingSec == 0 && !GM.CheckOffline(), "받으면 못 받은 시간 0 · 또 주지 않음");
                S.gold = og0;   // 뒤 시험에 영향 없게
                CloseAll();
            }

            // 11f) 11-04 출석: 1·2·3일차 다이아 → 다 받으면 1일차부터 · 하루 한 번 · 뒤로 가기도 받음 · 던전 중엔 안 뜸
            {
                CloseAll();
                int n = GameManager.AttendDays;
                var agems = new System.Collections.Generic.List<double>(); for (int k = 0; k < n; k++) agems.Add(GameManager.AttendGems(k));
                Check(n == 7 && string.Join("/", agems) == "100/200/300/500/700/1000/1500", $"출석 {n}일 · {string.Join("/", agems)} 다이아 (11-14 7일)");
                Check(!GM.CheckAttend(true), "오늘 이미 받음 → 창 없음");
                S.attendDay = "20000101"; S.attendCount = 0;
                double ag0 = S.gems;
                bool ashown = GM.CheckAttend(true); yield return Wait(0.3f);
                var ap = HUD.I.Get<AttendPopup>();
                Check(ashown && ap != null && ap.gameObject.activeSelf && ap.index == 0, "새 날 → 출석 창 (1일차)");
                Capture("18f_attend");
                Button tb = null;
                foreach (var b in ap.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("받기")) tb = b; }
                tb?.onClick.Invoke(); yield return Wait(0.2f);
                Check(S.gems - ag0 == 100 && S.AttendToday && S.attendCount == 1 && ap.gameObject.activeSelf, $"[받기] → 다이아 +{S.gems - ag0} (체크 표시, 창은 그대로)");
                Capture("18g_attend_claimed");
                tb?.onClick.Invoke();
                Check(!ap.gameObject.activeSelf && S.gems - ag0 == 100, "[확인] → 닫힘 (두 번 안 줌)");
                Check(!GM.CheckAttend(true) && GM.ClaimAttend() < 0, "같은 날 다시 → 없음");
                // 2일차: 뒤로 가기로 닫아도 받음
                S.attendDay = "20000102"; ag0 = S.gems;
                GM.CheckAttend(true); yield return Wait(0.2f);
                Check(ap.index == 1, "다음 날 → 2일차");
                HUD.I.Back();
                Check(!ap.gameObject.activeSelf && S.gems - ag0 == 200, $"뒤로 가기 → 2일차 다이아 +{S.gems - ag0}");
                // 3~7일차 (11-14 7일) → 다음은 다시 1일차
                double[] wantG = { 300, 500, 700, 1000, 1500 };
                for (int d = 2; d < 7; d++)
                {
                    S.attendDay = "2000010" + (d + 1); ag0 = S.gems;
                    GM.CheckAttend(true); yield return Wait(0.2f);
                    Check(ap.index == d, $"{d + 1}일차");
                    if (d == 5) Capture("18h_attend_day6");
                    ap.Close();
                    Check(S.gems - ag0 == wantG[d - 2], $"{d + 1}일차 다이아 +{S.gems - ag0}");
                }
                S.attendDay = "20000108";
                GM.CheckAttend(true); yield return Wait(0.2f);
                Check(ap.index == 0, "7일 다 받음 → 다시 1일차");
                ap.Close();
                // 다른 창이 떠 있으면 (켜 둔 채 날이 바뀜) 안 끼어듦
                S.attendDay = "20000109";
                HUD.I.Open<SettingsPopup>(); yield return Wait(0.1f);
                Check(!GM.CheckAttend(false), "다른 창이 떠 있음 → 출석 창 기다림");
                CloseAll();
                Check(GM.CheckAttend(false), "창이 닫힘 → 출석 창");
                ap.Close();
                S.gems -= 100 + 200 + 300 + 500 + 700 + 1000 + 1500 + 100 + 200;   // 뒤 시험에 영향 없게
                GM.Notify();
                CloseAll();
            }

            // 11e) 11-04 요청_02 총알: 관통탄 0.8배 · A 이상 일반탄 네온 오오라 · 미사일 넉백 없음 · 폭발 등급 색
            {
                var dspec = Tables.Bullet("pierce");
                var db = Bullet.Fire(dspec, new Vector2(0, -20), Vector2.up, 1, 1, 0.5f, 1, 3);
                Check(Mathf.Abs(db.transform.localScale.x - 0.8f) < 0.01f, $"관통탄 크기 x{db.transform.localScale.x:0.##}");
                var aura = db.transform.Find("AuraOut");
                Check(aura != null && aura.GetComponent<SpriteRenderer>().enabled, "A 관통탄 → 네온 오오라");
                var nb = Bullet.Fire(Tables.Bullet("single"), new Vector2(0, -20), Vector2.up, 1, 1, 0.5f, 1, 2);
                var na = nb.transform.Find("AuraOut");
                Check(na == null || !na.GetComponent<SpriteRenderer>().enabled, "B 단발 → 오오라 없음");
                Check(Mathf.Abs(nb.transform.localScale.x - 1f) < 0.01f, "단발 크기 그대로");
                // 넉백: 좀비 하나를 세워 두고 일반 피해 / 미사일 피해(knock=false)
                GM.ClearTargets();
                var zr = GameManager.PickZombie(S.Chapter, S.stage);
                var z = Zombie.Spawn(zr, new Vector2(0, 2), 1e12, 0);
                yield return Wait(0.1f);
                // 맞은 바로 뒤 좀비의 밀림 값(knock, private)을 읽어 비교 — 움직인 거리는 걷기·프레임에 따라 흔들려서
                var kf = typeof(Zombie).GetField("knock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                kf.SetValue(z, Vector2.zero);
                GM.Damage(z, 1, z.Pos + Vector2.down, 1, false);
                float mv = ((Vector2)kf.GetValue(z)).magnitude;
                GM.Damage(z, 1, z.Pos + Vector2.down);
                float mv2 = ((Vector2)kf.GetValue(z)).magnitude;
                Log($"밀림 값: 미사일 {mv:0.00} · 일반 {mv2:0.00}");
                Check(mv < 0.001f && mv2 > 0.5f, "미사일 피해 = 넉백 없음 (일반 피해만 밀림)");
                GM.ClearTargets();
                // 11-08 죽는 방향: 서 있는 좀비 = 분대(왼쪽)에서 맞으면 뒤로 벌러덩(머리 오른쪽 = 뒤집지 않음)
                //                기어 다니는 좀비 = 돌아서지 않고 분대 쪽을 본 채 엎어짐(뒤집음). 전엔 물구나무로 죽었음
                {
                    var zrows = Tables.Zombies;
                    var dz = new System.Collections.Generic.List<Zombie>();
                    int dn = zrows.Count, dcol = 9;     // 11-11 좀비 44종 → 9칸씩
                    for (int i = 0; i < dn; i++)
                    {
                        float x = Mathf.Lerp(-Field.I.ViewHalfW + 0.8f, Field.I.ViewHalfW - 0.8f, (i % dcol) / (dcol - 1f));
                        float y = Field.I.PlayTop - 1.6f - (i / dcol) * 1.7f;
                        dz.Add(Zombie.Spawn(zrows[i], new Vector2(x, y), 1e12, 0));
                    }
                    yield return Wait(0.1f);
                    foreach (var q in dz) q.Hit(2e12, q.Pos + Vector2.left, false);
                    bool stand = true, crawl = true; int nCrawl = 0;
                    foreach (var q in dz)
                    {
                        if (q.IsCrawler) { nCrawl++; crawl &= q.sr.flipX; }
                        else stand &= !q.sr.flipX;
                    }
                    Check(stand, "죽는 방향: 서 있는 좀비는 뒤로 벌러덩(머리가 분대 반대쪽)");
                    Check(nCrawl >= 2 && crawl, $"죽는 방향: 기어 다니는 좀비 {nCrawl}종은 분대 쪽을 본 채 엎어짐");
                    yield return Wait(0.45f);
                    Capture("19d_zombie_die");
                    GM.ClearTargets();
                }
                // 11-11 새 좀비 32종 (요청_03 12 + 좀비 추가 제안 20) · 색 변형 · 배경 판 · 보스 6마리 순환 · 드럼통 · 강시
                {
                    var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    string[] news = { "hatchet", "chainsaw", "bucketw", "helmet", "suit", "sport", "flag", "ghost", "miner", "fisher", "jason", "pharaoh",
                        "padding", "ski", "scientist", "patient", "hazmat", "tube", "cowboy", "biker", "camper", "delivery", "jiangshi", "chef", "student",
                        "firefighter", "money", "drum", "shield", "yeti", "captain", "gasgiant" };
                    int okArt = 0;
                    foreach (var id in news)
                    {
                        var row = Tables.Zombie(id);
                        var fr = Art.Zombie(id);
                        if (row.id == id && fr[0] != null && fr[0].texture.width == Art.ZW * Art.ZCols && RigData.Get("z_" + id) != null) okArt++;
                        else Log("새 좀비 문제: " + id);
                    }
                    Check(okArt == news.Length, $"새 좀비 {okArt}/{news.Length}종 표 줄 · 그림 · 뼈대");
                    int normal = 0, bosses = 0;
                    foreach (var zr2 in Tables.Zombies) { if (zr2.weight > 0) normal++; else bosses++; }
                    Check(normal == 38 && bosses == 6, $"일반 좀비 {normal}종 · 보스 {bosses}종 (38 · 6)");
                    // 보스: 챕터마다 ogre → jason → pharaoh → yeti → captain → gasgiant, 표 끝(150챕터) 너머도 이어서
                    string[] cyc = { "ogre", "jason", "pharaoh", "yeti", "captain", "gasgiant" };
                    bool bossOk = true;
                    for (int c = 1; c <= 7; c++) bossOk &= Tables.Stage((c - 1) * 5 + 4).bossId == cyc[(c - 1) % 6];
                    bossOk &= Tables.Stage(150 * 5 + 4).bossId == "ogre" && Tables.Stage(152 * 5 + 4).bossId == "pharaoh";
                    bool raidOk = Tables.BossRaid(0).bossId == "ogre" && Tables.BossRaid(1).bossId == "jason" && Tables.BossRaid(155).bossId == cyc[155 % 6];
                    Check(bossOk && raidOk, $"보스 순환: 챕터 1~7 · 151 · 153 / 보스 던전 Lv.1·2·156 ({Tables.Stage(152 * 5 + 4).bossId} · {Tables.BossRaid(155).bossId})");
                    // 나올 비중: 롱패딩(28챕터 설원에 처음) = 6 x 배경 1.5 x 새 얼굴 3 · 40챕터 숲 = 6 · 원래 좀비는 새 얼굴 비중 없음
                    var pad = Tables.Zombie("padding");
                    float w28 = GameManager.ZombieWeight(pad, 28, Tables.Chapter(28).biome), w40 = GameManager.ZombieWeight(pad, 37, Tables.Chapter(37).biome);
                    float wArmor = GameManager.ZombieWeight(Tables.Zombie("armor"), 3, Tables.Chapter(3).biome);
                    Check(Mathf.Abs(w28 - 27f) < 0.01f && Mathf.Abs(w40 - 6f) < 0.01f && Mathf.Abs(wArmor - 3f) < 0.01f && GameManager.ZombieWeight(pad, 27, "") == 0,
                        $"나올 비중: 롱패딩 28챕터 {w28} · 37챕터 {w40} · 갑옷 3챕터 {wArmor} · 27챕터 0");
                    // 한 스테이지 = 3~5종 (마지막 스테이지에 모든 좀비가 다 나오면 정신없어서) · 다시 와도 같음 · 첫 등장 챕터엔 꼭 · 돈가방은 따로
                    int badN = 0, debutMiss = 0, minN = 99, maxN = 0;
                    var allSeen = new System.Collections.Generic.HashSet<string>();
                    for (int st = 0; st < 750; st++)
                    {
                        int ch = st / 5 + 1;
                        var ks = GameManager.StageKinds(ch, st);
                        minN = Mathf.Min(minN, ks.Count); maxN = Mathf.Max(maxN, ks.Count);
                        if (ks.Count < 3 || ks.Count > 5) badN++;
                        foreach (var k in ks) { allSeen.Add(k.id); if (k.id == "money" || k.weight <= 0) badN++; }
                        foreach (var zr3 in Tables.Zombies) if (zr3.minChapter == ch && ch >= 6 && zr3.weight > 0 && zr3.id != "money" && !ks.Contains(zr3)) debutMiss++;
                    }
                    var k80 = GameManager.StageKinds(80, 397);
                    bool same = string.Join(",", k80.ConvertAll(k => k.id)) == string.Join(",", GameManager.StageKinds(80, 397).ConvertAll(k => k.id));
                    var seenSet = new System.Collections.Generic.HashSet<string>(); int money = 0;
                    for (int i = 0; i < 6000; i++) { var pz = GameManager.PickZombie(80, 397); if (pz.id == "money") money++; else seenSet.Add(pz.id); }
                    bool inKinds = true; foreach (var id in seenSet) inKinds &= k80.Exists(k => k.id == id);
                    Check(badN == 0 && debutMiss == 0 && allSeen.Count == 37 && same && inKinds && seenSet.Count == k80.Count && money > 0 && money < 120,
                        $"한 스테이지 좀비 {minN}~{maxN}종 (1~750 스테이지) · 첫 등장 빠짐 {debutMiss} · 전체 {allSeen.Count}/37종 · 80챕터 한 스테이지 {seenSet.Count}종({string.Join(",", seenSet)}) · 돈가방 {money}/6000");
                    // 색: 챕터 1 초록 · 2 붉은 · 3 푸르딩딩 · 4 보라 / 꼭짓점 색에 숨김 · 쓰러져도 그대로
                    Check(GameManager.PickTint(1, true) == 0 && GameManager.PickTint(2, true) == 1 && GameManager.PickTint(3, true) == 2 && GameManager.PickTint(4, true) == 3,
                        "챕터 색: 1 초록 · 2 붉은 · 3 푸르딩딩 · 4 보라");
                    var tz = Zombie.Spawn(Tables.Zombie("basic"), new Vector2(0, 2), 10, 0, tint: 2);
                    var rigF = typeof(Zombie).GetField("rig", BF);
                    var trv = rigF.GetValue(tz) as RigView;
                    bool tintOk = Mathf.Abs(tz.sr.color.g - (1f - 16f / 255f)) < 0.002f && trv != null && Mathf.Abs(trv.sr.color.g - tz.sr.color.g) < 0.001f;
                    tz.Hit(100, tz.Pos + Vector2.left, false);
                    yield return Wait(0.9f);
                    Check(tintOk && Mathf.Abs(tz.sr.color.g - (1f - 16f / 255f)) < 0.002f && tz.sr.color.a < 1f, $"색 번호 2 = 꼭짓점 초록 {tz.sr.color.g:0.000} (뼈대 그림도) · 쓰러져 깜빡여도 그대로");
                    GM.ClearTargets();
                    // 실제 화면 색: 기본 좀비 4마리(초록 · 붉은 · 푸르딩딩 · 보라) 머리 픽셀을 읽어 색조 확인 (11-11 첫 판은 Linear 색 공간 때문에 붉은 → 푸른)
                    {
                        var tzs = new Zombie[4];
                        GameManager.HoldFire = true;     // 분대가 쏘면 번쩍임·폭발이 덮어서 색을 못 읽음
                        Fx.I?.FlushMoney();
                        yield return Wait(0.6f);
                        for (int k = 0; k < 4; k++) { tzs[k] = Zombie.Spawn(Tables.Zombie("basic"), new Vector2(0.2f + k * 1.4f, 0f), 1e12, 0, tint: k); tzs[k].standStill = true; }
                        yield return Wait(0.3f);
                        Capture("19h_tints");
                        GameManager.HoldFire = false;
                        var cam = Camera.main;
                        RenderTexture.active = rt;
                        var px = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        px.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); px.Apply();
                        RenderTexture.active = null;
                        float[] hue = new float[4];
                        for (int k = 0; k < 4; k++)
                        {
                            // 머리 = 발에서 1.0칸 위 · 얼굴 그늘을 피해 뒤통수 쪽(분대를 보게 뒤집혀 있으니 +x) 9점 평균
                            Color sum = Color.black;
                            for (int ax = -1; ax <= 1; ax++) for (int b2 = -1; b2 <= 1; b2++)
                            {
                                var sp = cam.WorldToScreenPoint((Vector3)(tzs[k].Pos + new Vector2(0.18f + ax * 0.04f, 1.0f + b2 * 0.04f)));
                                sum += px.GetPixel((int)sp.x, (int)sp.y);
                            }
                            Color.RGBToHSV(sum / 9f, out hue[k], out _, out _);
                            hue[k] *= 360f;
                        }
                        Destroy(px);
                        bool H(float h, float lo, float hi) => lo < hi ? h >= lo && h <= hi : h >= lo || h <= hi;
                        Check(H(hue[0], 70, 120) && H(hue[1], 340, 30) && H(hue[2], 170, 230) && H(hue[3], 245, 300),
                            $"화면 색조: 초록 {hue[0]:0}° · 붉은 {hue[1]:0}° · 푸르딩딩 {hue[2]:0}° · 보라 {hue[3]:0}°");
                        GM.ClearTargets();
                    }
                    // 드럼통: 죽으면 펑 → 가까운 좀비(체력 = 드럼통 x2)는 쓰러지고 먼 좀비는 그대로
                    var drum = Zombie.Spawn(Tables.Zombie("drum"), new Vector2(0, 1.5f), 100, 0); drum.standStill = true;
                    var near1 = Zombie.Spawn(Tables.Zombie("basic"), new Vector2(0.9f, 1.5f), 200, 0); near1.standStill = true;
                    var near2 = Zombie.Spawn(Tables.Zombie("basic"), new Vector2(-0.6f, 2.2f), 200, 0); near2.standStill = true;
                    var far = Zombie.Spawn(Tables.Zombie("basic"), new Vector2(4.5f, 1.5f), 200, 0); far.standStill = true;
                    yield return Wait(0.1f);
                    drum.Hit(1000, drum.Pos + Vector2.left, false);
                    yield return Wait(0.25f);
                    Capture("19e_drum_blast");
                    Check(near1.dead && near2.dead && !far.dead, $"드럼통 펑: 가까운 2마리 쓰러짐({near1.dead},{near2.dead}) · 먼 좀비 그대로({!far.dead})");
                    GM.ClearTargets();
                    // 강시: 걸어오며 깡총 (몸이 위로 뜸) · 스키: 걸을 때 앞으로 숙임
                    var hop = Zombie.Spawn(Tables.Zombie("jiangshi"), new Vector2(Field.I.ViewHalfW - 1f, 1f), 1e9, 0);
                    var ski = Zombie.Spawn(Tables.Zombie("ski"), new Vector2(Field.I.ViewHalfW - 1f, -1f), 1e9, 0);
                    float maxUp = 0, maxLean = 0;
                    float tEnd = Time.realtimeSinceStartup + 1.6f;
                    while (Time.realtimeSinceStartup < tEnd)
                    {
                        maxUp = Mathf.Max(maxUp, hop.sr.transform.localPosition.y);
                        maxLean = Mathf.Max(maxLean, Mathf.Abs(Mathf.DeltaAngle(0, ski.sr.transform.localEulerAngles.z)));
                        yield return null;
                    }
                    Check(maxUp > 0.08f && maxLean > 4f, $"강시 깡총 높이 {maxUp:0.00} · 스키 숙임 {maxLean:0.0}°");
                    GM.ClearTargets();
                    // 새 좀비 줄 세우기 (색 변형 돌아가며) → 캡처
                    var line = new System.Collections.Generic.List<Zombie>();
                    for (int i = 0; i < news.Length; i++)
                    {
                        int col = i % 8, rowN = i / 8;
                        float x = Mathf.Lerp(-Field.I.ViewHalfW + 0.9f, Field.I.ViewHalfW - 0.9f, col / 7f);
                        float y = Field.I.PlayTop - 1.7f - rowN * 1.9f;
                        var nz = Zombie.Spawn(Tables.Zombie(news[i]), new Vector2(x, y), 1e12, 0, Tables.Zombie(news[i]).weight > 0 ? 1f : 0.8f, false, i % 4);
                        nz.standStill = true; line.Add(nz);
                    }
                    yield return Wait(0.6f);
                    Capture("19f_new_zombies");
                    GM.ClearTargets();
                    // 배경 판 4장 (챕터 색에 맞춤): 1 기본 · 2 황폐 · 3 세모 나무 · 4 죽은 나무
                    string[] maps = { "side_map", "side_map_waste", "side_map_pine", "side_map_dead" };
                    bool mapOk = true;
                    for (int c = 1; c <= 4; c++)
                    {
                        mapOk &= Tables.Chapter(c).mapSprite == maps[c - 1];
                        Field.I.SetMap(Tables.Chapter(c).mapSprite);
                        Field.I.Tint(Field.ChapterTint(c));
                        for (int i = 0; i < 6; i++)
                        {
                            var mz = Zombie.Spawn(GameManager.PickZombie(60, 300 + c), new Vector2(Mathf.Lerp(-2.5f, 3f, i / 5f), Mathf.Lerp(Field.I.PlayTop - 2f, Field.I.PlayBottom + 2f, (i * 7 % 6) / 5f)), 1e12, 0, tint: GameManager.PickTint(c, true));
                            mz.standStill = true;
                        }
                        yield return Wait(0.3f);
                        mapOk &= Field.I.map.sprite != null && Field.I.map.sprite.name == "Map/" + maps[c - 1];
                        Capture("19g_map_ch" + c);
                        GM.ClearTargets();
                    }
                    Check(mapOk, "배경 판: 챕터 1~4 = 기본 · 황폐 · 세모 나무 · 죽은 나무");
                    GM.ApplyTint();
                }
                // 11-11 요청_04: 말풍선(클리어 · 보스 · 가끔) · 스테이지 마디 네온 · 눌러서 강화 크게/빨강 · 해금 알림 오래 + 버튼 빛
                {
                    var sp = Speech.I;
                    int nClear = 0, nBoss = 0, nRand = 0;
                    foreach (var l in Speech.Lines) { if (l.trigger == "Clear") nClear++; else if (l.trigger == "Boss") nBoss++; else if (l.trigger == "Random") nRand++; }
                    Check(sp != null && nClear >= 1 && nBoss >= 1 && nRand >= 5, $"말풍선 대사 표: 클리어 {nClear} · 보스 {nBoss} · 가끔 {nRand}");
                    {
                        // 11-14 대사 19개 추가: 주아 글꼴에 없는 글자('…' 등)가 없는지 — 없으면 다른 글꼴로 섞여 보임
                        var jua = Resources.Load<Font>("Font/Jua-Regular");
                        var miss = new System.Collections.Generic.List<string>();
                        foreach (var l in Speech.Lines) foreach (char ch in l.text) if (ch != ' ' && jua != null && !jua.HasCharacter(ch)) { miss.Add(l.id + ":" + ch); break; }
                        Check(jua != null && miss.Count == 0 && nRand >= 25 && nBoss >= 5 && nClear >= 3, $"11-14 대사 추가 (가끔 {nRand} · 보스 {nBoss} · 클리어 {nClear}) · 글꼴에 없는 글자 {string.Join(", ", miss)}");
                    }
                    sp.Clear();
                    int st0 = S.stage; S.kills = 0;
                    GM.AdvanceStage();     // 스테이지 클리어 → 리더 "스테이지 클리어!"
                    yield return Wait(0.3f);
                    string said = sp.ActiveText;
                    Capture("20a_speech_clear");
                    Check(said == "스테이지 클리어!", $"스테이지 클리어 말풍선: {said}");
                    S.stage = st0 - 1; GM.AdvanceStage();   // 원래 스테이지로 (리더가 한 번 더 말함 → 아래에서 끔)
                    sp.Clear();
                    Speech.Say("Boss");
                    string bs = sp.ActiveText;
                    Check(bs == "보스다!! 조심해!!" || bs == "큰 놈이 온다!", $"보스 말풍선: {bs}");
                    sp.Clear();
                    // 가끔: 싸우는 중에 아무나 한마디 (간격을 잠깐 짧게 하려고 대사만 직접 뽑아 봄)
                    var seen = new System.Collections.Generic.HashSet<string>();
                    for (int i = 0; i < 300; i++) seen.Add(Speech.Pick("Random").text);
                    Speech.Say("Random");
                    yield return Wait(0.4f);
                    Check(seen.Count == nRand && sp.ActiveCount == 1, $"가끔 대사 {seen.Count}/{nRand}종 고루 · 지금 말풍선 {sp.ActiveCount}개 ({sp.ActiveText})");
                    Capture("20b_speech_random");
                    sp.Clear();
                    // 11-12 받아치기: 답 줄이 가리키는 대사가 다 있나 · 껄껄껄! → (다른 동료) 뭐가 그렇게 웃겨? → (처음 동료) 그냥~ · 동료가 없으면 답 없음
                    {
                        var rpIds = new System.Collections.Generic.HashSet<string>();
                        foreach (var rpL in Speech.Lines) rpIds.Add(rpL.id);
                        int rpNRep = 0, rpOrphan = 0, rpWith = 0;
                        foreach (var rpL in Speech.Lines) if (rpL.trigger == "Reply") { rpNRep++; if (!rpIds.Contains(rpL.replyTo)) rpOrphan++; }
                        foreach (var rpL in Speech.Lines) if (rpL.trigger != "Reply" && Speech.PickReply(rpL.id) != null) rpWith++;
                        Check(rpNRep >= 5 && rpOrphan == 0 && rpWith > 0 && rpWith < nRand + nClear + nBoss, $"받아치기 표: 답 {rpNRep}줄 · 답 달린 대사 {rpWith}개(일부만) · 가리키는 대사 없음 {rpOrphan}");
                        Speech.Line rpLaugh = null; foreach (var rpL in Speech.Lines) if (rpL.id == "rand_10") rpLaugh = rpL;
                        int rpNCat = Squad.I.cats.Count;
                        Speech.SayLine(rpLaugh, true);
                        yield return Wait(0.4f);
                        var rpA = sp.WhoSays("껄껄껄!"); int rpN1 = sp.ActiveCount;
                        yield return Wait(1.0f);
                        var rpB = sp.WhoSays("뭐가 그렇게 웃겨?"); int rpN2 = sp.ActiveCount;
                        Capture("20d_speech_reply");
                        yield return Wait(1.0f);
                        string rpBack = rpA ? sp.TextOf(rpA) : null;
                        Capture("20e_speech_reply2");
                        Check(rpNCat >= 2 && rpA && rpN1 == 1 && rpB && rpB != rpA && rpN2 == 2 && rpBack == "그냥~", $"받아치기 대화: 동료 {rpNCat} · 처음 {rpN1}개 → 답 뒤 {rpN2}개(다른 동료 {(rpB && rpB != rpA)}) → 처음 동료 \"{rpBack}\"");
                        sp.Clear();
                        // 동료가 리더 1마리뿐이면 답 없음
                        var rpKeep = new System.Collections.Generic.List<Capy>(Squad.I.cats);
                        Squad.I.cats.RemoveRange(1, Squad.I.cats.Count - 1);
                        Speech.SayLine(rpLaugh, true);
                        yield return Wait(1.4f);
                        int rpSolo = sp.ActiveCount; bool rpPend = sp.ReplyPending;
                        Squad.I.cats.Clear(); Squad.I.cats.AddRange(rpKeep);
                        Check(rpSolo == 1 && !rpPend, $"동료가 없으면 답 없음: 말풍선 {rpSolo}개 · 예약 {rpPend}");
                        sp.Clear();
                        // 배고프당! 은 답이 2개 — 비중대로 둘 다 나옴
                        var rpSeen = new System.Collections.Generic.HashSet<string>();
                        for (int rpI = 0; rpI < 300; rpI++) { var rpR = Speech.PickReply("rand_14"); if (rpR != null) rpSeen.Add(rpR.text); }
                        Check(rpSeen.Count == 2, $"배고프당! 답 {rpSeen.Count}종 ({string.Join(" / ", rpSeen)})");
                    }
                    // 스테이지 마디 네온: 지금 서브 스테이지 마디 자리 · 깜빡임
                    // 11-12 두 번만 재면 깜빡임 주기와 맞아 같은 밝기가 잡힐 때가 있음 → 1초 동안 여러 번 재서 가장 어둡고 밝은 값
                    float a0 = HUD.I.NodeGlowColor.a, a1 = a0;
                    for (int gi = 0; gi < 8; gi++) { yield return Wait(0.13f); float ga = HUD.I.NodeGlowColor.a; a0 = Mathf.Min(a0, ga); a1 = Mathf.Max(a1, ga); }
                    var gp = HUD.I.NodeGlowPos;
                    Check(Mathf.Abs(gp.x - (-200 + (S.Sub - 1) * 100)) < 0.5f && Mathf.Abs(a0 - a1) > 0.02f, $"스테이지 마디 네온: 마디 {S.Sub} 자리 x {gp.x} · 밝기 {a0:0.00}→{a1:0.00}");
                    // 눌러서 강화: 54 · 빨강
                    var pt = HUD.I.PawText;
                    Check(pt != null && pt.fontSize >= 50 && pt.color.r > 0.9f && pt.color.g < 0.4f, $"눌러서 강화 글자 {pt?.fontSize} · 색 {pt?.color}");
                    // 해금: 44 → 45 (보스 던전) — 알림 글 오래 + 보스 버튼 빛
                    int best0 = S.bestEver;
                    S.bestEver = 44; HUD.I.Refresh();
                    S.bestEver = 45; HUD.I.Refresh();
                    float left = HUD.I.ToastLeft; string tt = HUD.I.ToastText; int fx = HUD.I.UnlockFxCount;
                    yield return Wait(0.5f);
                    Capture("20c_unlock");
                    yield return Wait(2.2f);
                    float left2 = HUD.I.ToastLeft;
                    Check(tt == "보스 던전이 열렸습니다!" && left > 3f && left2 > 0 && fx == 1, $"해금 알림: '{tt}' {left:0.0}초 (2.7초 뒤에도 {left2:0.0}초 남음) · 빛 {fx}개");
                    S.bestEver = best0; HUD.I.Refresh();
                    HUD.I.StopUnlockFx();
                }
                // 미사일 장착 장면 캡처 (등급 색 폭발 · 네온 오오라)
                var keep = new System.Collections.Generic.List<int>(S.slots);
                S.slots[0] = Tables.Code(7, "missile"); S.slots[1] = Tables.Code(5, "single"); S.slots[2] = Tables.Code(3, "pierce");   // 청록 미사일 · 빨강 단발 · 보라 관통
                Squad.I.Rebuild();
                yield return Wait(2.5f);
                Capture("19_bullet_fx");
                yield return Wait(0.35f);
                Capture("19b_bullet_fx");
                // 폭발 색 비교: 원래(주황) · S(주황) · SS(빨강) · R(청록) · SR(분홍) · U(연두)
                int[] gs = { -1, 4, 5, 7, 8, 10 };
                for (int k = 0; k < gs.Length; k++) Fx.I.Boom(new Vector2(-1.6f + (k % 3) * 1.6f, 1.6f - (k / 3) * 2.2f), 0.9f, gs[k]);
                yield return Wait(0.1f);
                Capture("19c_boom_colors");
                for (int k = 0; k < keep.Count; k++) S.slots[k] = keep[k];
                Squad.I.Rebuild();
            }

            // 11b) 10-30 결제·광고: 가짜 결제(배치는 바로 지급) · 영구 상품 두 번 안 줌 · 광고 제거 = 광고 없이 바로 보상 · 자동강화 켬/끔 · 결제 꺼진 폰 = 원화 줄 숨김
            {
                double iapG0 = S.gems;
                // 11-14 다이아 첫 구매 2배: 상품마다 처음 1번만 x Gem_First_Mul · 상점 줄 딱지(리본 + x2 별) · 설명 "500 → 1,000"
                S.iapOnce.RemoveAll(x => x.StartsWith("first2x:"));
                var gemRows = Tables.Shop.FindAll(x => x.category == "Gem_Shop" && Iap.IsCash(x));
                Check(Iap.FirstMul == 2 && gemRows.Count == 3 && gemRows.TrueForAll(Iap.FirstBonus), $"다이아 상품 {gemRows.Count}개 모두 첫 구매 x{Iap.FirstMul}");
                {
                    var shopF = HUD.I.Open<ShopPopup>(); yield return Wait(0.3f);
                    int ribbons = 0, bursts = 0; bool descOk = false;
                    foreach (var im in shopF.GetComponentsInChildren<Image>(true)) { if (im.name == "FirstRibbon") ribbons++; if (im.name == "FirstBurst") bursts++; }
                    foreach (var t in shopF.GetComponentsInChildren<Text>(true)) if (t.text == L.F("첫 구매 {0}배! 다이아 {1} → {2}", "2", "500", "1,000")) descOk = true;
                    Check(ribbons == 3 && bursts == 3 && descOk, $"상점 첫 구매 2배 딱지 리본 {ribbons} · 별 {bursts} · 설명 500 → 1,000 {descOk}");
                    shopF.ScrollToGems(); yield return Wait(0.2f);
                    Capture("17a_shop_first2x");
                    CloseAll();
                }
                var gpSmall = Tables.Shop.Find(x => x.id == "gem_pack_small");
                string cf1 = Iap.ConfirmText(gpSmall);
                Check(cf1.Contains(L.F("첫 구매 {0}배! 다이아 {1} → {2}", "2", "500", "1,000")) && !cf1.Contains(L.T(gpSmall.desc)), "11-14 구매 확인 창: 첫 구매 → 2배 글");
                ConfirmPopup.Ask(cf1, () => { }, "구매"); yield return Wait(0.3f);
                Capture("17a1_confirm_first2x"); CloseAll();
                Iap.Buy("gem_pack_small");
                Check(System.Math.Abs(S.gems - iapG0 - 1000) < 0.01 && S.iapOnce.Contains("first2x:gem_pack_small"), $"다이아 한 줌 첫 구매 → +{S.gems - iapG0} (2배)");
                string cf2 = Iap.ConfirmText(gpSmall);
                Check(cf2.Contains(L.T(gpSmall.desc)) && !cf2.Contains("첫 구매"), $"11-14 구매 확인 창: 산 뒤 → 기존 글 ({L.T(gpSmall.desc)})");
                ConfirmPopup.Ask(cf2, () => { }, "구매"); yield return Wait(0.3f);
                Capture("17a3_confirm_after"); CloseAll();
                yield return Wait(0.1f);
                Capture("17a2_first2x_toast");
                double iapG0b = S.gems;
                Iap.Buy("gem_pack_small");
                Check(System.Math.Abs(S.gems - iapG0b - 500) < 0.01, $"다이아 한 줌 두 번째 → +{S.gems - iapG0b} (보통)");
                {
                    var shopF2 = HUD.I.Open<ShopPopup>(); yield return Wait(0.3f);
                    int ribbons2 = 0;
                    foreach (var im in shopF2.GetComponentsInChildren<Image>(true)) if (im.name == "FirstRibbon") ribbons2++;
                    Check(ribbons2 == 2 && !Iap.FirstBonus(Tables.Shop.Find(x => x.id == "gem_pack_small")) && Iap.FirstBonus(Tables.Shop.Find(x => x.id == "gem_pack_mid")),
                          $"산 상품만 딱지 사라짐 (남은 리본 {ribbons2} · 주머니는 아직 2배)");
                    CloseAll();
                }
                {
                    var svF = SaveData.ParseFixed(S.ToJson());
                    Check(svF != null && svF.iapOnce.Contains("first2x:gem_pack_small"), "첫 구매 2배 기록이 세이브(클라우드)에 남음");
                }
                S.gems = iapG0 + 500;   // 아래 시험은 예전처럼 +500 한 번 산 뒤로
                double iapGA = S.gems;
                Iap.Buy("remove_all_ads");
                bool got = false;
                AdService.Show("test", () => got = true);
                Check(S.noAds && got, "모든 광고 제거 → 광고 버튼 바로 보상");
                Check(System.Math.Abs(S.gems - iapGA - 1000) < 0.01, $"모든 광고 제거 구매 → 다이아 +{S.gems - iapGA} (11-12 사용자 요청 1000)");
                double iapG1 = S.gems;
                Iap.Buy("auto_upgrade");
                Iap.Buy("auto_upgrade");   // 두 번째는 "이미 가지고 있습니다"
                Check(S.autoUpgrade && S.autoOn && System.Math.Abs(S.gems - iapG1 - 500) < 0.01, $"자동강화 패키지 (다이아 +{S.gems - iapG1}, 한 번만)");
                HUD.I.Open<ShopPopup>(); yield return Wait(0.3f); Capture("17b_shop_owned"); CloseAll();
                int autoLv0 = S.lvAtk + S.lvSpd + S.lvGold;
                double iapGold0 = S.gold;
                S.gold += Stats.Cost(S.lvAtk) * 20;
                yield return Wait(1.6f);
                int autoLv1 = S.lvAtk + S.lvSpd + S.lvGold;
                Check(autoLv1 >= autoLv0 + 2, $"자동강화 켜짐 → 강화 {autoLv1 - autoLv0}번");
                GM.AutoUpgradeButton();   // 끔
                S.gold += Stats.Cost(S.lvAtk) * 20;
                yield return Wait(1.1f);
                Check(!S.autoOn && S.lvAtk + S.lvSpd + S.lvGold == autoLv1, "자동강화 끄면 멈춤");
                Capture("17c_auto_off");
                Iap.SimulateNoStore = true;
                S.noAds = false; S.autoUpgrade = false; S.autoOn = true;
                Iap.Buy("gem_pack_small");   // 이벤트로 창 다시 그림 (지급은 됨 — 다음 줄에서 되돌림)
                S.gems = iapG0; S.gold = iapGold0;
                var shop = HUD.I.Open<ShopPopup>(); yield return Wait(0.3f);
                int krwRows = 0;
                foreach (var t in shop.GetComponentsInChildren<Text>(true)) if (t.text.EndsWith("원") && t.text.Length > 1 && char.IsDigit(t.text[0])) krwRows++;
                Capture("17d_shop_nostore");
                CloseAll();
                bool autoHidden = !GameObject.Find("AutoUpgrade") && !GameObject.Find("Starter");   // 11-13 SS 미사일탄 아이콘도
                Iap.SimulateNoStore = false;
                Iap.Buy("gem_pack_small"); S.gems = iapG0;   // 창·버튼 원래대로
                Check(krwRows == 0 && autoHidden, $"결제 꺼진 폰: 원화 줄 {krwRows}개 · 자동강화 버튼 숨김 {autoHidden}");
            }
            // 11c) 11-13 초반 정착 상품 990원 SS 미사일탄 (1회 한정): 빈 장착 칸에 바로(동료 +1) · 두 번째는 안 줌 ·
            //      칸이 다 차 있으면 기다렸다가 빈 칸이 생기면 들어감 · 다시 설치 되살리기 = 총알 다시 · 상점 "구매 완료"
            {
                var st = Tables.Shop.Find(x => x.id == "starter_ss_missile");
                int ss = Tables.Code(5, "missile");
                Check(st != null && Iap.IsCash(st) && Iap.Permanent(st) && Iap.BulletOf(st) == ss && st.bonusGems == 0 && ShopPopup.KrwText(st.priceValue) == L.F("{0}원", "990"),
                    $"상점 990원 SS 미사일탄 줄 ({st?.priceValue} · {st?.reward} · {Tables.WeaponName(ss)})");
                var sl0 = new List<int>(S.slots); var iv0 = new List<int>(S.inv); var on0 = new List<string>(S.iapOnce); double sg0 = S.gems; bool swu0 = S.weaponUnlocked;
                for (int i = 0; i < S.slots.Count; i++) S.slots[i] = i == 0 ? Tables.Code(0, "single") : -1;   // 처음 하는 사람처럼 리볼버 하나
                S.iapOnce.Clear(); S.pendingBullets.Clear(); S.weaponUnlocked = false; S.starterSeenAt = 0; Squad.I.Rebuild();
                int cats0 = Squad.I.cats.Count;
                // 11-13 화면 아이콘: 상점이 잠겨 있으면 없음 → 열리면 보임 → 누르면 구매
                int be0 = S.bestEver; S.bestEver = 0; HUD.I.Refresh();
                bool hidLocked = !GameObject.Find("Starter");
                S.bestEver = be0; HUD.I.Refresh();
                var stGo = GameObject.Find("Starter");
                yield return Wait(0.15f);
                Capture("17e0a_starter_pop");   // 나타나는 순간 (튀어나오는 중)
                yield return Wait(0.9f);
                Capture("17e0_starter_icon");
                Check(hidLocked && stGo != null && GM.Unlocked("shop"), $"SS 미사일탄 아이콘: 상점 잠김엔 숨김({hidLocked}) · 열리면 보임({stGo != null})");
                // 11-13 강조는 Starter_Fx_Time(5초)만 → 멈춘 채로 · 처음 보인 시각 기록 → Starter_Home_Hours(72시간) 뒤 홈에서 사라지고 상점 탭엔 남음
                var hf = typeof(HUD).GetField("starterSettled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool moving = !(bool)hf.GetValue(HUD.I);
                long seen = S.starterSeenAt;
                yield return Wait((float)Tables.C("Starter_Fx_Time", 5) + 0.3f);
                bool settled = (bool)hf.GetValue(HUD.I);
                var pulse = stGo ? stGo.transform.Find("Pulse") : null;
                Check(moving && settled && pulse && Mathf.Abs(pulse.localScale.x - 1f) < 0.01f && Tables.C("Starter_Fx_Time", 0) == 5,
                    $"SS 미사일탄 강조: 처음엔 움직임({moving}) · {Tables.C("Starter_Fx_Time", 0)}초 뒤 멈춤({settled}) · 크기 {(pulse ? pulse.localScale.x : 0):0.00}");
                Capture("17e0b_starter_settled");
                S.starterSeenAt = SaveData.Now - (long)(Tables.C("Starter_Home_Hours", 72) * 3600) + 60; HUD.I.Refresh();
                bool before72 = GameObject.Find("Starter") != null;
                S.starterSeenAt = SaveData.Now - (long)(Tables.C("Starter_Home_Hours", 72) * 3600) - 1;
                yield return null; yield return null;   // 켜 둔 채로도 TickStarter 가 숨김
                bool after72 = GameObject.Find("Starter") == null;
                var shop72 = HUD.I.Open<ShopPopup>(); yield return Wait(0.3f);
                bool inShop = false;
                foreach (var t in shop72.GetComponentsInChildren<Text>(true)) if (t.text.Contains("990") && t.text.EndsWith("원")) inShop = true;
                Capture("17e0c_starter_72h_shop");
                CloseAll();
                Check(seen > 0 && System.Math.Abs(seen - SaveData.Now) < 30 && before72 && after72 && inShop && Tables.C("Starter_Home_Hours", 0) == 72,
                    $"SS 미사일탄 홈 노출: 처음 보인 시각 기록({seen > 0}) · 72시간 1분 전 보임({before72}) · 지나면 홈에서 사라짐({after72}) · 상점 탭엔 990원 줄({inShop})");
                S.starterSeenAt = SaveData.Now; HUD.I.Refresh();
                stGo = GameObject.Find("Starter");
                var stB = stGo ? stGo.GetComponentInChildren<Button>() : null;
                Check(stB != null && stB.name == "StarterBtn", "SS 미사일탄 아이콘 안에 누르는 판(StarterBtn)");
                if (stB) stB.onClick.Invoke(); else Iap.Buy("starter_ss_missile");
                yield return Wait(0.2f);
                Check(!GameObject.Find("Starter"), "아이콘을 눌러 산 뒤 아이콘 사라짐");
                Check(S.slots[1] == ss && Squad.I.cats.Count == cats0 + 1 && S.iapOnce.Contains("starter_ss_missile") && S.weaponUnlocked && S.gems == sg0,
                    $"990원 SS 미사일탄 → 빈 장착 칸에 바로 (칸 {S.slots[1]} · 분대 {cats0} → {Squad.I.cats.Count} · 총알 메뉴 열림 {S.weaponUnlocked})");
                Capture("17e_starter_missile");
                Iap.Buy("starter_ss_missile");
                int ssN = 0; foreach (var c in S.slots) if (c == ss) ssN++; foreach (var c in S.inv) if (c == ss) ssN++;
                Check(ssN == 1 && Iap.Owned(st), $"SS 미사일탄은 한 번만 (가진 수 {ssN})");
                var shopS = HUD.I.Open<ShopPopup>(); yield return Wait(0.3f);
                bool doneLabel = false;
                foreach (var t in shopS.GetComponentsInChildren<Text>(true)) if (t.text == L.T("구매 완료")) doneLabel = true;
                Capture("17f_shop_starter_done");
                CloseAll();
                Check(doneLabel, "상점: 산 뒤엔 \"구매 완료\" (누를 수 없음)");
                // 장착 3칸 · 인벤토리 20칸이 다 찬 상태에서 되살리기(다시 설치) → 기다림 → 한 칸 비우면 들어감
                for (int i = 0; i < S.slots.Count; i++) S.slots[i] = Tables.Code(0, "single");
                for (int i = 0; i < S.inv.Count; i++) S.inv[i] = Tables.Code(0, "shotgun");
                S.iapOnce.Clear();
                Iap.Grant(st, true, true);
                bool waiting = S.pendingBullets.Count == 1 && S.pendingBullets[0] == ss && !S.inv.Contains(ss) && S.iapOnce.Contains("starter_ss_missile") && S.gems == sg0;
                S.inv[7] = -1;
                GM.Notify();
                Check(waiting && S.inv[7] == ss && S.pendingBullets.Count == 0, $"칸이 다 차 있으면 기다렸다가({waiting}) 빈 칸이 생기면 들어감 (칸 {S.inv[7]}) · 되살리기도 총알 줌");
                var sv = SaveData.ParseFixed(S.ToJson());
                Check(sv != null && sv.iapOnce.Contains("starter_ss_missile"), "1회 한정 구매 기록이 세이브(클라우드)에 남음");
                for (int i = 0; i < sl0.Count; i++) S.slots[i] = sl0[i];
                for (int i = 0; i < iv0.Count; i++) S.inv[i] = iv0[i];
                S.iapOnce.Clear(); S.iapOnce.AddRange(on0); S.pendingBullets.Clear(); S.gems = sg0; S.weaponUnlocked = swu0;
                Squad.I.Rebuild(); GM.Notify(); S.Save();
            }
            Check(AudioManager.I != null, "사운드 매니저 생성");
            // 10-26 소리 다듬기: 배경음 40초 (8초 반복 아님) · 효과음 16개 다 재생돼도 오류 없음
            Check(AudioManager.StageBgmSeconds >= 30f, $"배경음 길이 {AudioManager.StageBgmSeconds:F1}초");
            foreach (AudioManager.Sfx sx in System.Enum.GetValues(typeof(AudioManager.Sfx))) AudioManager.Play(sx);
            var buff = GM.Buff(true);
            Check(buff == null && Stats.BuffAtk, "버프");

            // 11-07 버그 점검: 기기 날짜·시간을 바꿔 다시 받기 막음
            {
                string ad0 = S.attendDay, td0 = S.ticketDay; int mt0 = S.mineTickets, bt0 = S.bossTickets, ag0 = S.adGemUsed;
                S.attendDay = "29991231";
                Check(S.AttendToday && GM.ClaimAttend() < 0, "출석: 받은 날이 오늘보다 뒤(날짜를 되돌림) → 다시 못 받음");
                S.attendDay = "20000101";
                Check(!S.AttendToday, "출석: 받은 날이 지난날 → 받을 수 있음");
                S.attendDay = ad0;
                S.ticketDay = "29991231"; S.mineTickets = 0; S.adGemUsed = 3; S.RefreshDaily();
                Check(S.mineTickets == 0 && S.adGemUsed == 3, "티켓·광고 다이아: 채운 날이 오늘보다 뒤 → 다시 안 채움");
                S.ticketDay = "20000101"; S.RefreshDaily();
                Check(S.mineTickets == Tables.Ci("MineTickets") && S.adGemUsed == 0 && S.ticketDay == SaveData.Today, "티켓·광고 다이아: 다음 날 → 채움");
                S.mineTickets = mt0; S.bossTickets = bt0; S.adGemUsed = ag0; S.ticketDay = td0;
                double now = SaveData.NowD;
                S.buffAtkCool = now + 86400; S.buffGoldEnd = now + 86400 * 2; S.ClampTimers();
                Check(S.buffAtkCool - now <= Tables.Buff("buff_atk").cooldown + 1 && S.buffGoldEnd - now <= Tables.Buff("buff_gold").maxStack + 1,
                    $"버프: 시계가 뒤로 고쳐져도 대기 {S.buffAtkCool - now:0}초 · 남은 {S.buffGoldEnd - now:0}초 (표 최대까지)");
                S.buffAtkCool = 0; S.buffGoldEnd = 0;
            }
            {
                // 예비 세이브: 본 세이브가 깨지면 예비에서
                S.Save();
                PlayerPrefs.SetString(SaveData.BakKey, PlayerPrefs.GetString(SaveData.Key));
                double bakGold = S.gold; int st0 = S.stage;
                PlayerPrefs.SetString(SaveData.Key, "{깨진 세이브");
                SaveData.ReloadForTest();
                bool fromBak = S != null && SaveData.LoadedFromBackup;
                Check(fromBak && System.Math.Abs(S.gold - bakGold) < 1 && S.stage == st0 && PlayerPrefs.GetString(SaveData.BrokenKey, "") == "{깨진 세이브",
                    "세이브가 깨짐 → 예비 세이브로 불러옴 · 깨진 글자는 따로 남김");
                S.Save();
            }

            // 12) 세이브 왕복
            S.Save();
            string json = PlayerPrefs.GetString(SaveData.Key);
            var back = JsonUtility.FromJson<SaveData>(json);
            Check(back.gems == S.gems && back.skins.Count == S.skins.Count && back.slots[0] == S.slots[0], "세이브 읽기");

            // 13) 숫자 표기
            Check(Num.F(1850) == "1.85k" && Num.F(14100000) == "14.1m" && Num.F(396) == "396" && Num.F(1600) == "1.6k" && Num.F(2.5e9) == "2.5b" && Num.F(1e12) == "1t" && Num.F(1e15) == "1aa" && Num.F(999999) == "999k" && Num.F(1e6) == "1m",
                "숫자 표기 " + Num.F(1850) + " " + Num.F(14100000) + " " + Num.F(2.5e9) + " " + Num.F(1e15));

            // 14) 11-12 코드 점검: 세이브 판 번호 · 칸 지문 · 값 정리 · 다이아 쓴 뒤 곧 저장 · 되살리기 보너스 · 나라 설정
            {
                Check(back.ver == SaveData.Version && SaveData.Version >= 1, $"세이브에 판 번호 {back.ver}");
                uint sig = SaveFieldSig();
                Check(sig == SaveData.FieldSig, $"세이브 칸 지문 {sig} (SaveData.FieldSig {SaveData.FieldSig}) — 다르면 세이브 칸이 바뀐 것: SaveData.Version 을 +1 하고 FieldSig 를 이 값으로");

                var bad = SaveData.Parse(S.ToJson());
                bad.gold = -5; bad.stage = -3; bad.skin = 999; bad.missionTier[0] = -2;
                bad.inv[0] = 99; bad.inv[1] = 7;
                for (int i = 0; i < bad.slots.Count; i++) bad.slots[i] = 8;
                var fx = SaveData.ParseFixed(bad.ToJson());
                Check(fx != null && fx.gold == 0 && fx.stage == 0 && fx.skin == 0 && fx.missionTier[0] == 0 && fx.inv[0] == -1 && fx.inv[1] == -1 && fx.slots[0] == Tables.Code(0, "single") && fx.slots[1] == -1,
                    "이상한 세이브 값 정리: 음수 골드·스테이지 → 0 · 없는 총알 번호 → 빈칸(장착은 기본 총알) · 없는 코스튬 → 기본");
                var same = SaveData.ParseFixed(S.ToJson());
                bool keep = same != null && same.gold == S.gold && same.gems == S.gems && same.skin == S.skin && same.stage == S.stage;
                for (int i = 0; keep && i < S.inv.Count; i++) keep = same.inv[i] == S.inv[i];
                for (int i = 0; keep && i < S.slots.Count; i++) keep = same.slots[i] == S.slots[i];
                Check(keep, "정상 세이브는 값 정리를 거쳐도 그대로");
                bad.gems = double.NaN;
                Log($"(참고) 다이아가 숫자 아님(NaN)인 세이브 글자를 다시 읽을 수 있나: {(SaveData.Parse(JsonUtility.ToJson(bad)) != null ? "읽힘" : "못 읽음")} · 값 정리 뒤 다이아 {SaveData.ParseFixed(JsonUtility.ToJson(bad))?.gems}");

                var inv0 = new List<int>(S.inv); double gg0 = S.gems; bool wu0 = S.weaponUnlocked;
                S.gems = Tables.C("GachaCost") + 7; int emptyAt = S.inv.IndexOf(-1);
                if (emptyAt < 0) { S.inv[S.inv.Count - 1] = -1; }
                string gErr = GM.Gacha();
                var soon = SaveData.Parse(PlayerPrefs.GetString(SaveData.Key));
                Check(gErr == null && soon != null && System.Math.Abs(soon.gems - 7) < 0.01, "뽑기(다이아를 씀) → 바로 세이브에 반영");
                for (int i = 0; i < inv0.Count; i++) S.inv[i] = inv0[i];
                S.gems = gg0; S.weaponUnlocked = wu0;

                var autoRow = Tables.Shop.Find(x => x.id == "auto_upgrade");
                bool au0 = S.autoUpgrade, ao0 = S.autoOn; double rg0 = S.gems;
                S.autoUpgrade = false;
                Iap.Grant(autoRow, true, true);
                Check(autoRow != null && autoRow.bonusGems > 0 && S.autoUpgrade && S.gems == rg0, $"자동강화 되살리기 → 가짐 · 보너스 다이아 {autoRow?.bonusGems:0} 은 다시 안 줌");
                S.autoUpgrade = au0; S.autoOn = ao0; S.gems = rg0;

                var adsRow = Tables.Shop.Find(x => x.id == "remove_all_ads");
                bool na0 = S.noAds;
                S.noAds = false;
                Iap.Grant(adsRow, true, true);
                Check(adsRow != null && adsRow.bonusGems == 1000 && S.noAds && S.gems == rg0, $"모든 광고 제거 되살리기(다시 설치) → 가짐 · 보너스 다이아 {adsRow?.bonusGems:0} 은 다시 안 줌");
                S.noAds = na0; S.gems = rg0;

                var cul = System.Globalization.CultureInfo.CurrentCulture;
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                string deNum = Num.F(1500) + " " + Num.F(12340);
                System.Globalization.CultureInfo.CurrentCulture = cul;
                Check(deNum == "1.5k 12.3k", $"소수점을 쉼표로 쓰는 나라 설정에서도 숫자 표기 그대로 ({deNum})");
                S.Save();
            }

            yield return CloudTest();

            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        /// <summary>11-07 클라우드 저장 (에디터 가짜 로그인·가짜 클라우드): 연결 → 올림 · 다른 진행이면 고르기 창 · 불러오기(씬 다시 불러옴)</summary>
        IEnumerator CloudTest()
        {
            DontDestroyOnLoad(gameObject);   // 불러오기가 씬을 다시 불러도 시험은 이어서
            PlayTest.ResetCloud();
            CloseAll();

            // ---- 11-12 (9번) 처음 켤 때 자동 로그인: 연결한 적 없는 기기 ----
            {
                CloudSync.AutoStart();
                yield return Wait(0.8f);
                Check(!CloudAuth.Linked && !CloudSync.Synced, "(에디터) 자동 로그인 스위치가 꺼져 있으면 켤 때 아무것도 안 함");

                CloudAuth.SimulateAutoLogin = true;
                CloudAuth.SimulateCancel = true;   // Play 게임즈 계정 없음 · 거절
                CloudSync.AutoStart();
                yield return Wait(0.8f);
                var cfA = HUD.I.Get<CloudConflictPopup>();
                Check(!CloudAuth.Linked && !CloudSync.Synced && !CloudAuth.SignedIn && !(cfA && cfA.gameObject.activeSelf) && PlayerPrefs.GetString(CloudSave.FakeKey, "") == "",
                    "처음 켬 · 자동 로그인 안 됨 → 조용히 그대로 (연결 안 됨 · 올리지 않음 · 창 없음)");
                CloudAuth.SimulateCancel = false;
                CloudSync.AutoStart();             // 같은 실행에서 씬이 다시 만들어져 또 불려도
                yield return Wait(0.8f);
                Check(!CloudAuth.Linked && !CloudSync.Synced, "자동 로그인은 한 실행에 한 번만 해 봄");

                CloudSync.ResetState();            // 다음에 켬
                CloudSync.AutoStart();
                yield return Wait(1.5f);
                var upA = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                Check(CloudAuth.Linked && CloudSync.Synced && CloudAuth.DisplayName != "" && upA != null && upA.saveId == S.saveId,
                    $"처음 켬 · 자동 로그인 됨 → 저절로 연결({CloudAuth.DisplayName}) · 이 기기 진행을 올림");

                // 새 폰(갓 시작한 진행) + 클라우드에 옛 폰의 진행 → 묻지 않고 받음
                var oldPhone = SaveData.Parse(S.ToJson());
                oldPhone.saveId = "old-phone"; oldPhone.gems = 99999; oldPhone.stage = S.stage + 3; oldPhone.bestEver = System.Math.Max(oldPhone.bestEver, oldPhone.stage); oldPhone.savedAt = SaveData.Now - 86400;
                PlayerPrefs.SetString(CloudSave.FakeKey, oldPhone.ToJson());
                PlayTest.ResetCloudKeepData();
                CloudAuth.SimulateAutoLogin = true;
                S.bestEver = 1; S.noAds = false; S.autoUpgrade = false; S.iapOrders = new System.Collections.Generic.List<string>();
                Check(CloudSync.IsFresh(S), "(준비) 이 기기 = 갓 시작한 진행");
                CloudSync.AutoStart();
                yield return Wait(2.2f);
                SetupCapture();
                yield return Wait(0.5f);
                Check(CloudAuth.Linked && CloudSync.Synced && S.saveId == "old-phone" && System.Math.Abs(S.gems - 99999) < 0.5 && GameManager.I != null && HUD.I != null,
                    $"새 폰에서 처음 켬 → 자동 로그인 → 클라우드의 옛 진행을 받아 화면 다시 만듦 (다이아 {Num.Gem(S.gems)})");
                PlayTest.ResetCloud();
                CloseAll();
            }
            var sp = HUD.I.Open<SettingsPopup>();
            yield return Wait(0.2f);
            Button cloudRow = null;
            foreach (var b in sp.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("클라우드 저장")) cloudRow = b; }
            bool vibeRow = false;
            foreach (var t in sp.GetComponentsInChildren<Text>(true)) if (t.text == L.T("진동")) vibeRow = true;
            Check(cloudRow != null && !vibeRow, "설정: [클라우드 저장] 줄 · 진동 줄 없음");
            Capture("18_settings");
            cloudRow?.onClick.Invoke();
            yield return Wait(0.3f);
            var cp = HUD.I.Get<CloudPopup>();
            Button link = null;
            foreach (var b in cp.GetComponentsInChildren<Button>(false)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("구글 계정 연결")) link = b; }
            Check(cp != null && cp.gameObject.activeSelf && link != null, "클라우드 창: [구글 계정 연결]");
            Capture("19a_cloud_unlinked");
            link?.onClick.Invoke();
            yield return Wait(1.5f);
            var up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
            Check(CloudAuth.Linked && CloudSync.Synced && up != null && up.saveId == S.saveId, "연결 → 클라우드 비어 있음 → 이 기기 진행을 올림");
            Capture("19b_cloud_linked");

            // 다른 폰의 진행(다른 saveId · 둘 다 진행함) → 고르기 창 → [이 기기 쓰기] → 확인 → 이 기기 것을 올림
            var other = SaveData.Parse(S.ToJson());
            other.saveId = "other-phone"; other.stage = S.stage + 5; other.bestEver = other.stage; other.gems = 54321; other.savedAt = SaveData.Now - 600;
            PlayerPrefs.SetString(CloudSave.FakeKey, other.ToJson());
            CloseAll();
            CloudSync.ResetState();
            CloudSync.AfterLogin(null);
            yield return Wait(0.8f);
            var cf = HUD.I.Get<CloudConflictPopup>();
            Check(cf != null && cf.gameObject.activeSelf, "다른 진행 → 고르기 창");
            Capture("19c_cloud_conflict");
            Button useLocal = null;
            if (cf) foreach (var b in cf.GetComponentsInChildren<Button>(true)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.F("{0} 쓰기", L.T("이 기기"))) useLocal = b; }
            useLocal?.onClick.Invoke();
            yield return Wait(0.2f);
            var ask = HUD.I.Get<ConfirmPopup>();
            bool warned = false;   // 11-12 지워진 진행은 복구 못 함 경고
            if (ask) foreach (var t in ask.GetComponentsInChildren<Text>(false)) if (t.text.Contains(L.T(CloudConflictPopup.NoUndo))) warned = true;
            Check(warned, "고르기 확인 창에 '복구할 수 없음' 경고 줄");
            Capture("19d_cloud_confirm");
            Button ok = null;
            if (ask) foreach (var b in ask.GetComponentsInChildren<Button>(false)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == L.T("확인")) ok = b; }
            ok?.onClick.Invoke();
            yield return Wait(0.8f);
            up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
            Check(up != null && up.saveId == S.saveId && S.gems != 54321, "[이 기기 쓰기] → 확인 → 이 기기 진행을 클라우드에 올림");

            // 11-12 다이아 부족 → [상점으로] → 상점 다이아 줄 · 새 버전 알림
            yield return GemShortAndUpdate();
            // 11-12 쿠폰
            yield return CouponTest();

            // 같은 진행인데 클라우드가 더 새것(다른 폰에서 더 함) → 불러오기 → 씬 다시 불러옴
            var newer = SaveData.Parse(S.ToJson());
            newer.gems = 77777; newer.stage = S.stage + 1; newer.savedAt = SaveData.Now + 60;
            PlayerPrefs.SetString(CloudSave.FakeKey, newer.ToJson());
            CloudSync.ResetState();
            CloudSync.AfterLogin(null, true);
            yield return Wait(2.0f);
            SetupCapture();
            yield return Wait(0.5f);
            Check(System.Math.Abs(S.gems - 77777) < 0.5 && S.stage == newer.stage && GameManager.I != null && HUD.I != null, $"같은 진행 · 클라우드가 더 새것 → 불러와 화면 다시 만듦 (다이아 {Num.Gem(S.gems)})");
            Check(CloudSync.pendingToast == null, "불러온 뒤 알림 띄움");

            // 자동 올리기: 저장하면 1분 뒤 올림 → [지금 저장]은 바로
            S.gems = 88888; S.Save();
            bool done = false, okUp = false;
            CloudSync.Upload(r => { done = true; okUp = r; });
            yield return Wait(0.6f);
            up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
            Check(done && okUp && up != null && System.Math.Abs(up.gems - 88888) < 0.5 && CloudSync.LastUploadAt > 0, "[지금 저장] → 클라우드에 올림");

            // 클라우드 읽기 실패 → 안내만 (데이터 그대로)
            CloudSave.SimulateFail = true;
            CloudSync.ResetState();
            CloudSync.AfterLogin(null);
            yield return Wait(0.6f);
            Check(!CloudSync.Synced && System.Math.Abs(S.gems - 88888) < 0.5, "클라우드 읽기 실패 → 이 기기 진행 그대로 · 올리지 않음");
            CloudSave.SimulateFail = false;

            // ---- 11-12 코드 점검: 같은 진행(saveId 같음) 비교 ----
            // (1) 옛 진행으로 켬(한 시간 전에 맞춘 그대로) + 다른 기기가 10분 전에 더 한 것을 올림 + 켜자마자 저장(출석 받기·자동 저장)
            //     → 그래도 클라우드를 받음 (전엔 방금 저장한 이 기기가 "더 새것"이 되어 옛 진행을 올렸음)
            {
                long t0 = SaveData.Now - 3600;
                CloudSync.ResetState();
                CloudSync.RememberBase(t0, S.saveId);
                S.loadedSavedAt = t0; S.loadedRealtime = Time.realtimeSinceStartup;
                var otherDev = SaveData.Parse(S.ToJson());
                otherDev.gems = 66666; otherDev.stage = S.stage + 2; otherDev.bestEver = System.Math.Max(otherDev.bestEver, otherDev.stage); otherDev.savedAt = SaveData.Now - 600;
                PlayerPrefs.SetString(CloudSave.FakeKey, otherDev.ToJson());
                S.Save();
                Check(S.savedAt > otherDev.savedAt, "(준비) 이 기기 저장 시각이 클라우드보다 늦음");
                CloudSync.AfterLogin(null, true);
                yield return Wait(2.0f);
                SetupCapture();
                yield return Wait(0.5f);
                Check(System.Math.Abs(S.gems - 66666) < 0.5 && S.stage == otherDev.stage && CloudSync.Synced, $"옛 진행으로 켜서 바로 저장해도 → 다른 기기가 올린 새 진행을 받음 (다이아 {Num.Gem(S.gems)})");
            }
            // (2) 클라우드가 내가 마지막에 맞춘 그대로 → 묻지 않고 이 기기 것을 올림
            {
                var mine = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                CloudSync.ResetState();
                CloudSync.RememberBase(mine.savedAt, S.saveId);
                S.gems = 55555; S.Save();
                CloudSync.AfterLogin(null, true);
                yield return Wait(0.8f);
                up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                var cf2 = HUD.I.Get<CloudConflictPopup>();
                Check(CloudSync.Synced && up != null && System.Math.Abs(up.gems - 55555) < 0.5 && !(cf2 && cf2.gameObject.activeSelf), "클라우드가 마지막으로 맞춘 그대로 → 이 기기 진행을 올림 (창 없음)");
            }
            // (3) 맞춘 뒤 양쪽 다 진행함 → 고르기 창 (올리지 않음)
            {
                var div = SaveData.Parse(S.ToJson());
                div.gems = 44444; div.savedAt = SaveData.Now - 300;
                PlayerPrefs.SetString(CloudSave.FakeKey, div.ToJson());
                CloudSync.ResetState();
                CloudSync.RememberBase(SaveData.Now - 7200, S.saveId);
                S.loadedSavedAt = SaveData.Now - 1800; S.loadedRealtime = Time.realtimeSinceStartup;
                CloudSync.AfterLogin(null, true);
                yield return Wait(0.8f);
                var cf3 = HUD.I.Get<CloudConflictPopup>();
                up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                Check(cf3 && cf3.gameObject.activeSelf && !CloudSync.Synced && up != null && System.Math.Abs(up.gems - 44444) < 0.5, "같은 진행을 양쪽 다 더 함 → 고르기 창 · 고르기 전엔 올리지 않음");
                CloseAll();
            }
            // (4) 내가 올린 것인데 "올림 끝"을 못 받고 꺼졌던 경우(클라우드 시각 = 켤 때 시각) → 창 없이 이 기기 것
            {
                long tl = SaveData.Now - 900;
                var mineOld = SaveData.Parse(S.ToJson());
                mineOld.savedAt = tl; mineOld.gems = 22222;
                PlayerPrefs.SetString(CloudSave.FakeKey, mineOld.ToJson());
                CloudSync.ResetState();
                CloudSync.RememberBase(SaveData.Now - 7200, S.saveId);
                S.loadedSavedAt = tl; S.loadedRealtime = Time.realtimeSinceStartup;
                S.gems = 33333;
                CloudSync.AfterLogin(null, true);
                yield return Wait(0.8f);
                up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                var cf4 = HUD.I.Get<CloudConflictPopup>();
                Check(CloudSync.Synced && up != null && System.Math.Abs(up.gems - 33333) < 0.5 && !(cf4 && cf4.gameObject.activeSelf), "내가 내릴 때 올린 그대로 → 창 없이 이 기기 진행");
            }
            // (5) 클라우드 세이브가 더 높은 판 → 받지도 올리지도 않고 업데이트 안내
            {
                var future = SaveData.Parse(S.ToJson());
                future.ver = SaveData.Version + 1; future.gems = 11111; future.savedAt = SaveData.Now - 60;
                PlayerPrefs.SetString(CloudSave.FakeKey, future.ToJson());
                CloudSync.ResetState();
                CloudSync.RememberBase(SaveData.Now - 7200, S.saveId);
                S.loadedSavedAt = SaveData.Now - 7200; S.loadedRealtime = Time.realtimeSinceStartup;
                CloudSync.AfterLogin(null, true);
                yield return Wait(0.8f);
                up = SaveData.Parse(PlayerPrefs.GetString(CloudSave.FakeKey, ""));
                Check(!CloudSync.Synced && CloudSync.NeedUpdate && System.Math.Abs(S.gems - 33333) < 0.5 && up != null && System.Math.Abs(up.gems - 11111) < 0.5 && HUD.I.ToastText != null,
                    $"클라우드가 더 새 판의 세이브 → 받지도 올리지도 않음 · 안내 \"{(HUD.I.ToastText ?? "").Replace("\n", " ")}\"");
            }
            // (6) 응답이 끝내 안 옴 → 시간 초과 뒤 다시 할 수 있음
            {
                CloudSync.ResetState();
                CloudSave.SimulateHang = true; CloudSync.TimeoutSec = 0.2f;
                bool cb = false, cbRes = true;
                CloudSync.AfterLogin(r => { cb = true; cbRes = r; }, true);
                bool busyMid = CloudSync.Busy;
                yield return Wait(2.5f);
                Check(busyMid && cb && !cbRes && !CloudSync.Busy && !CloudSync.Synced, "클라우드 응답 없음 → 시간 초과로 끝내고 다시 할 수 있음");
                CloudSave.SimulateHang = false; CloudSync.TimeoutSec = 20f;
                CloudSync.ResetState();
            }
            HUD.I.Open<CloudPopup>();
            yield return Wait(0.3f);
            Capture("19d_cloud_popup");
            CloseAll();
            PlayTest.ResetCloud();
        }

        /// <summary>창 안에서 글자가 text 인 버튼</summary>
        static Button BtnOf(Component root, string text)
        {
            if (root) foreach (var b in root.GetComponentsInChildren<Button>(false)) { var lt = UIKit.LabelOf(b); if (lt != null && lt.text == text) return b; }
            return null;
        }

        /// <summary>11-12 설정 > [쿠폰]: 틀린 번호 · KACHI2000(소문자·띄어쓰기) +2000 · 두 번째는 막힘 · 기간 지난/아직 쿠폰</summary>
        IEnumerator CouponTest()
        {
            CloseAll();
            S.coupons.Remove("KACHI2000");
            var st = HUD.I.Open<SettingsPopup>();
            yield return Wait(0.3f);
            var row = BtnOf(st, L.T("쿠폰"));
            Check(row != null && BtnOf(st, L.T("게임 종료")) != null, "설정에 [쿠폰] 줄");
            Capture("19h_settings_coupon");
            row?.onClick.Invoke();
            yield return Wait(0.3f);
            var cp = HUD.I.Get<CouponPopup>();
            Check(cp && cp.gameObject.activeSelf && cp.input != null && cp.input.text == "", "[쿠폰] → 쿠폰 창 (번호 칸 비어 있음)");
            double g0 = S.gems;
            cp.input.text = "NOPE123"; cp.Use();
            Check(System.Math.Abs(S.gems - g0) < 0.5 && cp.input.text == "NOPE123", "틀린 번호 → 그대로 (다이아 안 늘어남)");
            cp.input.text = "kachi 2000"; cp.Use();
            Check(System.Math.Abs(S.gems - g0 - 2000) < 0.5 && S.coupons.Contains("KACHI2000") && cp.input.text == "", $"KACHI2000 (소문자 · 띄어쓰기) → 다이아 +2000 ({g0:0} → {S.gems:0}) · 칸 비움");
            cp.input.text = "KACHI2000";
            yield return Wait(0.1f);
            Capture("19i_coupon");
            cp.Use();
            Check(System.Math.Abs(S.gems - g0 - 2000) < 0.5, "같은 쿠폰 두 번째 → 막힘");
            string err = GM.UseCoupon("", out _);
            Check(err == L.T("쿠폰 번호를 넣어 주세요"), "빈 칸 → 안내");
            var back = SaveData.Parse(S.ToJson());
            Check(back != null && back.coupons.Contains("KACHI2000") && back.ver == SaveData.Version, $"쓴 쿠폰이 세이브에 남음 (판 {back?.ver})");
            CloseAll();
        }

        /// <summary>11-12 코스튬 · 보급품 다이아 부족 → [상점으로] → 상점 다이아 줄 · 새 버전 알림(다른 창이 닫힌 뒤에만)</summary>
        IEnumerator GemShortAndUpdate()
        {
            double gems0 = S.gems;
            var tap = typeof(SkinPopup).GetMethod("Tap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var skin = Tables.Skins.Find(x => !S.skins.Contains(x.id) && x.price > 0);
            CloseAll();
            S.gems = 0;
            var sp = HUD.I.Open<SkinPopup>();
            if (skin != null) tap.Invoke(sp, new object[] { skin.id });
            yield return Wait(0.3f);
            var cp = HUD.I.Get<ConfirmPopup>();
            Check(skin != null && cp && cp.gameObject.activeSelf && cp.title.text == L.T("다이아 부족") && BtnOf(cp, L.T("상점으로")) != null && !S.skins.Contains(skin.id),
                  "코스튬 다이아 부족 → '다이아 부족' 창 + [상점으로] (사지 않음)");
            Capture("19e_gem_short");
            BtnOf(cp, L.T("상점으로"))?.onClick.Invoke();
            yield return Wait(0.4f);
            var shop = HUD.I.Get<ShopPopup>();
            var sr = shop ? shop.GetComponentInChildren<ScrollRect>() : null;
            float top = sr && shop.GemRowY >= 0 ? shop.GemRowY - sr.content.anchoredPosition.y : -1, viewH = sr ? ((RectTransform)sr.transform).rect.height : 0;
            Check(shop && shop.gameObject.activeSelf && HUD.I.openPopups[HUD.I.openPopups.Count - 1] == shop && top >= 0 && top + 155 <= viewH + 1,
                  $"[상점으로] → 상점이 코스튬 창 위에 열리고 다이아 줄이 화면 안 (줄 위치 {top:0} / 보이는 높이 {viewH:0})");
            Capture("19f_shop_gems");

            var supTap = typeof(SupplyPopup).GetMethod("Tap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var sup = Tables.Supplies.Find(x => !S.supplies.Contains(x.id) && x.cost > 0);
            int best0 = S.bestEver;
            if (sup != null && GM.BestChapter < sup.unlockChapter) S.bestEver = (sup.unlockChapter - 1) * 5;   // 잠시 열어 둠 (아래에서 되돌림)
            CloseAll();
            var sup0 = HUD.I.Open<SupplyPopup>();
            if (sup != null) supTap.Invoke(sup0, new object[] { sup.id });
            yield return Wait(0.3f);
            cp = HUD.I.Get<ConfirmPopup>();
            Check(sup == null || (cp && cp.gameObject.activeSelf && cp.title.text == L.T("다이아 부족") && !S.supplies.Contains(sup.id)),
                  sup == null ? "보급품 다이아 부족 (열린 보급품이 없어 건너뜀)" : "보급품 다이아 부족 → '다이아 부족' 창");
            CloseAll();
            S.gems = gems0;
            S.bestEver = best0;

            // 새 버전 알림: 다른 창이 떠 있으면 기다렸다가 닫히면 뜸
            AppUpdateCheck.ResetForTest();
            AppUpdateCheck.FakeAvailable = true;
            HUD.I.Open<SkinPopup>();
            AppUpdateCheck.Start();
            yield return Wait(1.2f);
            Check(AppUpdateCheck.Available && !AppUpdateCheck.Shown, "새 버전 있음 + 다른 창이 떠 있음 → 아직 안 띄움");
            CloseAll();
            yield return Wait(1.2f);
            cp = HUD.I.Get<ConfirmPopup>();
            Check(AppUpdateCheck.Shown && cp && cp.gameObject.activeSelf && cp.title.text == L.T("업데이트") && BtnOf(cp, L.T("업데이트")) != null,
                  "창이 닫힌 뒤 [업데이트] 창");
            Capture("19g_update");
            AppUpdateCheck.Start();   // 한 실행에 한 번만
            Check(AppUpdateCheck.StoreUrl == "market://details?id=" + Application.identifier, $"[업데이트] → {AppUpdateCheck.StoreUrl}");
            CloseAll();
            AppUpdateCheck.FakeAvailable = false;
        }

        /// <summary>영어 화면: 진행을 조금 해 둔 세이브 + English → 씬 다시 불러 메인·창 촬영</summary>
        IEnumerator EnTour()
        {
            DontDestroyOnLoad(gameObject);
            S.lang = L.En;
            S.stage = 22; S.bestEver = 22; S.gems = 5000; S.gold = 2.5e6;
            S.slots[0] = Tables.Code(4, "laser"); S.slots[1] = Tables.Code(3, "shotgun"); S.slots[2] = Tables.Code(5, "single");
            S.inv[0] = Tables.Code(2, "pierce"); S.inv[1] = Tables.Code(6, "missile");
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            SetupCapture();
            yield return Wait(2.5f);
            Check(L.IsEn && L.T("상점") == "Shop", "영어로 다시 불러옴");
            Capture("en/01_main");
            HUD.I.Open<WeaponPopup>(); yield return Wait(0.3f); Capture("en/02_weapon"); CloseAll();
            HUD.I.Open<SkinPopup>(); yield return Wait(0.3f); Capture("en/03_skin"); CloseAll();
            HUD.I.Open<SupplyPopup>(); yield return Wait(0.3f); Capture("en/04_supply"); CloseAll();
            HUD.I.Open<MissionPopup>(); yield return Wait(0.3f); Capture("en/05_mission"); CloseAll();
            HUD.I.Open<ShopPopup>(); yield return Wait(0.3f); Capture("en/06_shop"); CloseAll();
            HUD.I.Open<SettingsPopup>(); yield return Wait(0.3f); Capture("en/07_settings"); CloseAll();
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        /// <summary>글꼴 비교: 5챕터쯤 세이브 → 메인 · 강화 · 창 6개 (한국어)</summary>
        IEnumerator FontTour()
        {
            DontDestroyOnLoad(gameObject);
            S.stage = 22; S.bestEver = 22; S.gems = 5000; S.gold = 2.5e6;
            S.lvAtk = 30; S.lvSpd = 40; S.lvGold = 25;
            S.weaponUnlocked = true; S.tutorialDone = true;
            S.attendDay = System.DateTime.Now.ToString("yyyyMMdd");
            S.slots[0] = Tables.Code(4, "laser"); S.slots[1] = Tables.Code(3, "shotgun"); S.slots[2] = Tables.Code(5, "single");
            S.inv[0] = Tables.Code(2, "pierce"); S.inv[1] = Tables.Code(6, "missile");
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            SetupCapture();
            yield return Wait(3.0f);
            Log($"글꼴 {Art.Font?.name} x{Art.FontScale}");
            Capture("font/01_main");
            HUD.I.Open<WeaponPopup>(); yield return Wait(0.3f); Capture("font/02_weapon"); CloseAll();
            HUD.I.Open<MissionPopup>(); yield return Wait(0.3f); Capture("font/03_mission"); CloseAll();
            HUD.I.Open<ShopPopup>(); yield return Wait(0.3f); Capture("font/04_shop"); CloseAll();
            HUD.I.Open<SettingsPopup>(); yield return Wait(0.3f); Capture("font/05_settings"); CloseAll();
            HUD.I.Open<SkinPopup>(); yield return Wait(0.3f); Capture("font/06_skin"); CloseAll();
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        /// <summary>스토어 스크린샷: 11챕터쯤 진행한 세이브 (분대 3 · 레이저/미사일 · 해적 선장 스킨) → 장면 8개</summary>
        IEnumerator StoreTour()
        {
            DontDestroyOnLoad(gameObject);
            S.stage = 51; S.bestEver = 51; S.gems = 12500; S.gold = 3.2e7;
            S.lvAtk = 40; S.lvSpd = 60; S.lvGold = 150;   // 공격력은 낮게 — 좀비가 화면에 쌓여 보이게
            S.weaponUnlocked = true; S.tutorialDone = true;
            S.attendDay = System.DateTime.Now.ToString("yyyyMMdd");   // 촬영에 출석 창이 안 끼게
            S.slots[0] = Tables.Code(5, "laser"); S.slots[1] = Tables.Code(4, "missile"); S.slots[2] = Tables.Code(3, "shotgun");
            int[] inv = { Tables.Code(3, "single"), Tables.Code(3, "single"), Tables.Code(2, "pierce"), Tables.Code(4, "fast"), Tables.Code(2, "shotgun"), Tables.Code(1, "single"), Tables.Code(5, "missile") };
            for (int i = 0; i < inv.Length && i < S.inv.Count; i++) S.inv[i] = inv[i];
            foreach (int k in new[] { 1, 2, 3, 6, 17 }) if (!S.skins.Contains(k)) S.skins.Add(k);
            S.skin = 17;
            for (int k = 1; k <= 7; k++) if (!S.supplies.Contains(k)) S.supplies.Add(k);
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            SetupCapture();
            // 11-07: 좀비가 오른쪽 끝에서 바로 죽어 화면이 비어 보였음 → 사격을 잠깐 멈추고(HoldFire) 좀비가 화면 가운데까지 걸어오면 쏘기 시작 → 잠깐 뒤 찍음
            int Near(float frac) { int n = 0; foreach (var t in Target.All) if (t is Zombie z && z.Alive && !z.isBoss && z.Pos.x < Field.I.ViewHalfW * frac) n++; return n; }
            // 11-11: 코스튬을 장마다 새싹(3) · 해적 선장(17) · 원시인(6) 으로 섞고, 리더 말풍선을 넣어 찍음
            void Dress(int k) { S.skin = k; Squad.I.SetSkin(k); GM.Notify(); }
            void Say(string s) { Speech.I.Clear(); Speech.I.Show(Squad.I.Leader, L.T(s)); }
            Dress(3);   // 11챕터 = 푸르딩딩 좀비 · 세모 나무 배경
            GameManager.HoldFire = true;
            yield return WaitUntil(() => Near(0.3f) >= 10, 25f);
            GameManager.HoldFire = false;
            Say("좀비가 몰려온다!");
            yield return Wait(0.45f);
            Log($"01 전투: 가운데까지 온 좀비 {Near(0.3f)} · 전체 {Target.All.Count}");
            Capture("store/01_battle");

            // 보스 (챕터 마지막 스테이지 11-5 = 54)
            Dress(17);   // 11챕터 보스 = 닻 선장 ↔ 해적 선장 카피바라
            // 11-11: 예전엔 보스가 오른쪽 끝에서 맞고 있어 화면에 안 보였음 → 사격을 멈추고 보스가 화면 안쪽까지 걸어오면 쏘기 시작
            S.stage = 54; S.kills = GM.Quota; GM.Notify();
            GameManager.HoldFire = true;
            yield return WaitUntil(() => GM.boss != null && GM.boss.Pos.x < Field.I.ViewHalfW * 0.4f, 45f);
            GameManager.HoldFire = false;
            Say("보스다!! 조심해!!");
            yield return Wait(0.4f);
            Log($"보스: {(GM.boss != null ? "있음 y=" + GM.boss.transform.position.y.ToString("0.0") : "없음")}");
            Capture("store/02_boss");
            if (GM.boss != null) GM.boss.Hit(GM.boss.hp + 1, GM.boss.Pos);
            yield return Wait(1.0f);

            // 총알 창 (합성)
            HUD.I.Open<WeaponPopup>(); yield return Wait(0.4f); Capture("store/03_weapon"); CloseAll();

            // 상위 총알 전투
            S.slots[0] = Tables.Code(9, "laser"); S.slots[1] = Tables.Code(9, "missile"); S.slots[2] = Tables.Code(8, "laser");
            S.lvAtk = 0; S.lvSpd = 0;   // 상위 총알이라 강화를 빼야 좀비가 화면에 남아 보임
            S.skin = 6; S.stage = 64; GM.AdvanceStage(); GM.ClearTargets(); Speech.I.Clear();   // 11-11: 14-1 = 붉은 좀비 · 황폐 배경 · 원시인 (스테이지 글자까지 바뀌게 넘어가기로)
            Squad.I.Rebuild(); GM.Notify();
            GameManager.HoldFire = true;
            yield return WaitUntil(() => Near(0.3f) >= 10, 25f);
            GameManager.HoldFire = false;
            yield return Wait(0.35f);
            Log($"04 상위 총알: 가운데까지 온 좀비 {Near(0.3f)} · 전체 {Target.All.Count}");
            Speech.I.Clear();
            Capture("store/04_high");

            HUD.I.Open<SupplyPopup>(); yield return Wait(0.4f); Capture("store/05_supply"); CloseAll();
            HUD.I.Open<SkinPopup>(); yield return Wait(0.4f); Capture("store/06_skin"); CloseAll();

            // 광산
            Dress(3); Speech.I.Clear();
            S.RefreshDaily();
            // 11-07: "비밀던전" 알림 글자가 사라진 뒤 찍음 (전엔 0.9초라 글자가 가운데를 가렸음)
            if (GM.StartMine() == null) { yield return Wait(2.2f); Capture("store/07_mine"); GM.dungeonTime = 0.05f; yield return Wait(0.5f); HUD.I.Get<ResultPopup>()?.Close(); yield return Wait(0.4f); }
            else Log("광산 못 들어감");

            // 도전
            // 11-07: 3초면 이미 다 잡아 결과 창이 찍혔음 → 싸우는 중에 (알림 글자가 흐려질 때쯤)
            Dress(17); Speech.I.Clear();
            GameManager.HoldFire = true;   // 도전 좀비는 제자리에 서 있음 → 알림 글자가 사라질 때까지 쏘지 않고 기다렸다가 잠깐 쏨
            if (GM.StartChallenge() == null) { yield return Wait(1.9f); GameManager.HoldFire = false; yield return Wait(0.12f); Log($"08 도전: 남은 좀비 {GM.DungeonLeft}"); Capture("store/08_challenge"); HUD.I.Get<ResultPopup>()?.Close(); GM.ExitDungeon(); }
            else Log("도전 못 들어감");
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

        // =====================================================================
        // 11-07 모바일 광고 영상 촬영: 장면마다 실제 게임을 초당 30장(게임 시간 1/30초 고정) → ad/장면/0000.jpg
        // =====================================================================
        const int AdFps = 30;

        /// <summary>sec 초 동안 매 프레임 촬영. each(t) = 그 프레임 전에 할 일 (t = 장면 안 시간)</summary>
        IEnumerator Rec(string clip, float sec, System.Action<float> each = null)
        {
            string dir = $"{OutDir}/ad/{clip}";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = 1f / AdFps;
            int n = Mathf.RoundToInt(sec * AdFps);
            for (int i = 0; i < n; i++)
            {
                each?.Invoke(i / (float)AdFps);
                yield return null;
                CaptureJpg($"{dir}/{i:0000}.jpg");
            }
            Time.captureDeltaTime = 0;
            Log($"광고 장면 {clip}: {n}장");
        }

        void CaptureJpg(string path)
        {
            var cam = Camera.main;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToJPG(92));
            Destroy(tex);
        }

        int NearZombies(float frac) { int n = 0; foreach (var t in Target.All) if (t is Zombie z && z.Alive && !z.isBoss && z.Pos.x < Field.I.ViewHalfW * frac) n++; return n; }

        IEnumerator AdTour()
        {
            DontDestroyOnLoad(gameObject);
            S.stage = 51; S.bestEver = 60; S.gems = 12500; S.gold = 3.2e7;
            S.lvAtk = 40; S.lvSpd = 60; S.lvGold = 150;
            S.weaponUnlocked = true; S.tutorialDone = true;
            S.attendDay = SaveData.Today;   // 촬영에 출석 창이 안 끼게
            S.slots[0] = Tables.Code(5, "laser"); S.slots[1] = Tables.Code(4, "missile"); S.slots[2] = Tables.Code(3, "shotgun");
            foreach (int k in new[] { 1, 2, 3, 6, 17 }) if (!S.skins.Contains(k)) S.skins.Add(k);
            S.skin = 17;
            for (int k = 1; k <= 7; k++) if (!S.supplies.Contains(k)) S.supplies.Add(k);
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            SetupCapture(1080, 1920);
            yield return Wait(0.5f);

            // 11-11 요청_04: 장면마다 코스튬을 새싹 · 원시인 · 해적 선장으로 돌아가며 (게임처럼 분대는 같은 옷)
            void Dress(int k) { S.skin = k; Squad.I.SetSkin(k); GM.Notify(); }
            // A. 몰려오는 좀비 떼를 쓸어버림 (첫 3초 훅) — 사격을 멈추고 좀비가 가운데까지 오면 쏘기 시작
            Dress(3);
            GameManager.HoldFire = true;
            yield return WaitUntil(() => NearZombies(0.25f) >= 14, 30f);
            Log($"A 좀비 {NearZombies(0.25f)} · 전체 {Target.All.Count}");
            GameManager.HoldFire = false;
            bool said = false;
            yield return Rec("a_horde", 5f, t => { if (!said && t >= 1.2f) { said = true; Speech.Say("Random"); } });   // 한마디

            // B. 강화 카드 연타 — 숫자가 오르고 카드가 톡톡
            Dress(6);
            S.gold = 1e30; GM.Notify();
            HoldButton Card(int i) { var g = GameObject.Find("CardHolder" + i); return g ? g.GetComponentInChildren<HoldButton>() : null; }
            var atk = Card(0); var spd = Card(1); var gold = Card(2);
            float next = 0;
            yield return Rec("b_upgrade", 3.5f, t =>
            {
                if (t < next) return;
                next = t + 0.13f;
                var hb = t < 1.6f ? atk : t < 2.6f ? spd : gold;
                hb?.action?.Invoke();
            });

            // C. 총알 합성 — 같은 탄알을 끌어다 겹치기(유령 그림이 날아감) 3번 → [일괄 합성]
            for (int i = 0; i < S.inv.Count; i++) S.inv[i] = -1;
            int[] inv = { Tables.Code(4, "laser"), Tables.Code(4, "laser"), Tables.Code(3, "missile"), Tables.Code(3, "missile"), Tables.Code(5, "single"), Tables.Code(5, "single"),
                          Tables.Code(2, "fast"), Tables.Code(2, "fast"), Tables.Code(1, "shotgun"), Tables.Code(1, "shotgun"), Tables.Code(3, "pierce"), Tables.Code(3, "pierce"), Tables.Code(6, "laser") };
            for (int i = 0; i < inv.Length; i++) S.inv[i] = inv[i];
            var wp = HUD.I.Open<WeaponPopup>();
            yield return Wait(0.4f);
            WeaponCell Cell(int idx) { foreach (var c in wp.GetComponentsInChildren<WeaponCell>(true)) if (!c.isSlot && c.index == idx) return c; return null; }
            (int a, int b)[] drags = { (1, 0), (3, 2), (5, 4) };
            yield return Rec("c_merge", 4.0f, t =>
            {
                // 0.0~0.45 끌기 · 0.5 놓기 — 1.0초마다 한 번, 3.2초에 [일괄 합성]
                int k = Mathf.FloorToInt(t / 1.0f);
                float u = t - k * 1.0f;
                if (k < drags.Length)
                {
                    var from = Cell(drags[k].a); var to = Cell(drags[k].b);
                    if (from && to && from.code >= 0)
                    {
                        if (u < 0.45f)
                        {
                            wp.ghost.gameObject.SetActive(true);
                            var im = wp.ghost.GetComponent<Image>();
                            im.sprite = WeaponPopup.TypeIcon(Tables.TypeOf(from.code)); im.preserveAspect = true;
                            wp.ghost.SetAsLastSibling();
                            float e = Mathf.SmoothStep(0, 1, u / 0.45f);
                            wp.ghost.position = Vector3.Lerp(from.rt.position, to.rt.position, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.6f;
                        }
                        else if (wp.ghost.gameObject.activeSelf)
                        {
                            wp.ghost.gameObject.SetActive(false);
                            wp.Drop(from, to);
                        }
                    }
                }
                else if (Mathf.Abs(t - 3.2f) < 0.017f)
                {
                    var mb = GameObject.Find("MergeAll");
                    mb?.GetComponent<Button>()?.onClick.Invoke();
                }
            });
            CloseAll();

            // D. 보스 — 걸어 들어오면 쏘기 시작 · 3.2초에 쓰러뜨림 → 다음 스테이지
            Dress(17);
            S.stage = 54; S.kills = GM.Quota; GM.Notify();
            GameManager.HoldFire = true;
            yield return WaitUntil(() => GM.boss != null && GM.boss.Pos.x < Field.I.ViewHalfW * 0.45f, 40f);
            GameManager.HoldFire = false;
            bool killed = false;
            yield return Rec("d_boss", 5.0f, t =>
            {
                if (!killed && t >= 3.2f && GM.boss != null && GM.boss.Alive) { killed = true; GM.boss.Hit(GM.boss.hp + 1, GM.boss.Pos); }
            });

            // E. 상위 총알(SSR 레이저 · 미사일)로 좀비 떼 — 강화를 빼야 좀비가 화면에 남아 보임
            S.slots[0] = Tables.Code(9, "laser"); S.slots[1] = Tables.Code(9, "missile"); S.slots[2] = Tables.Code(8, "laser");
            S.lvAtk = 0; S.lvSpd = 0; S.gold = 3.2e7;
            S.skin = 3;
            Squad.I.Rebuild(); GM.Notify();
            GameManager.HoldFire = true;
            yield return WaitUntil(() => NearZombies(0.25f) >= 14, 30f);
            GameManager.HoldFire = false;
            yield return Rec("e_high", 4.5f);

            // F. 비밀던전 — 광석을 깨면 다이아가 튐 (알림 글자가 사라진 뒤부터)
            Dress(17);
            S.RefreshDaily(); S.mineTickets = Mathf.Max(S.mineTickets, 1);
            if (GM.StartMine() == null)
            {
                yield return Wait(1.9f);
                yield return Rec("f_mine", 3.5f);
                GM.dungeonTime = 0.05f; yield return Wait(0.5f); HUD.I.Get<ResultPopup>()?.Close(); yield return Wait(0.4f);
            }
            else Log("광산 못 들어감");

            // G. 코스튬 창 (원시인 입은 채로)
            Dress(6);
            HUD.I.Open<SkinPopup>();
            yield return Rec("g_skin", 2.0f);
            CloseAll();

            GameManager.HoldFire = false;
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        IEnumerator ZombieShow()
        {
            GameManager.Demo = true; Zombie.Treadmill = true;
            S.attendDay = System.DateTime.Now.ToString("yyyyMMdd");
            CloseAll(); GM.ClearTargets();
            yield return Wait(0.3f);
            var rows = Tables.Zombies;
            var list = new System.Collections.Generic.List<Zombie>();
            int n = rows.Count, nr = (n + 2) / 3;
            for (int i = 0; i < n; i++)
            {
                int c = i % 3, r = i / 3;
                float x = Mathf.Lerp(-Field.I.ViewHalfW + 1.7f, Field.I.ViewHalfW - 1.4f, c / 2f);
                float y = Mathf.Lerp(Field.I.PlayTop - 2.2f, Field.I.PlayBottom + 1.0f, r / Mathf.Max(1f, nr - 1f));
                list.Add(Zombie.Spawn(rows[i], new Vector2(x, y), 1e12, 0));
            }
            Time.captureDeltaTime = 0.04f;          // 한 프레임 = 게임 0.04초 (캡처가 느려도 같은 간격)
            for (int i = 0; i < 25; i++) yield return null;
            var cam = Camera.main;
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(list[i].transform.position);
                sb.Append($"{(i > 0 ? "," : "")}{{\"id\":\"{list[i].row.id}\",\"x\":{sp.x:0},\"y\":{rt.height - sp.y:0},\"s\":{list[i].row.scale:0.00}}}");
            }
            Directory.CreateDirectory($"{OutDir}/zshow");
            File.WriteAllText($"{OutDir}/zshow/pos.json", sb.Append("]").ToString());
            for (int i = 0; i < 30; i++) { Capture($"zshow/walk_{i:00}", false); yield return null; }
            for (int i = 0; i < 30; i++)
            {
                if (i % 3 == 0) foreach (var z in list) if (z) z.Hit(1, z.Pos + Vector2.left * 3f, false);
                Capture($"zshow/hit_{i:00}", false); yield return null;
            }
            for (int i = 0; i < 10; i++) { Capture($"zshow/stop_{i:00}", false); yield return null; }
            Zombie.TreadmillStop = true;
            for (int i = 0; i < 30; i++) { Capture($"zshow/stop_{i + 10:00}", false); yield return null; }
            Time.captureDeltaTime = 0;
            Zombie.Treadmill = Zombie.TreadmillStop = false; GameManager.Demo = false;
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }

        string Inv() => string.Join(",", S.inv.ConvertAll(x => x < 0 ? "-" : Tables.GradeName(Tables.GradeOf(x)) + Tables.TypeOf(x)[0]));

        /// <summary>세이브에 들어가는 칸 목록(이름:종류)의 지문 — 칸을 더하거나 빼면 값이 바뀜 (SaveData.FieldSig 와 비교)</summary>
        static uint SaveFieldSig()
        {
            var names = new List<string>();
            foreach (var f in typeof(SaveData).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (!f.IsNotSerialized) names.Add(f.Name + ":" + f.FieldType.Name);
            names.Sort(string.CompareOrdinal);
            uint h = 2166136261;
            foreach (var n in names) foreach (char c in n + ";") { h ^= c; h *= 16777619; }
            return h;
        }

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
        void SetupCapture() => SetupCapture(1080, 2340);

        /// <summary>w x h 로 렌더 (광고 영상은 1080x1920 = 9:16)</summary>
        void SetupCapture(int w, int h)
        {
            rt = new RenderTexture(w, h, 24);
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
