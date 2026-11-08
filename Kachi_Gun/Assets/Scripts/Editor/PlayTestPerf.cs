using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 11-08 성능 측정 (사용자 "오래 돌리면서 컨텐츠를 왔다갔다하면 화면이 밀림"):
    ///   bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunPerf --play   (이름표 = 환경 변수 KACHI_PERF_LABEL, 기본 "run")
    /// 진행한 세이브(51스테이지 · 레이저/미사일/산탄)로 한 바퀴 = 스테이지 사냥 → 창 4개 열고 닫기 → 도전 → 스테이지 → 비밀던전 → 스테이지 → 보스 던전, 5바퀴.
    /// 구간마다: 게임 코드 시간(BehaviourUpdate + LateBehaviourUpdate, 화면 그리기 뺌) 평균·최악 · 프레임당 쓰레기 메모리 개수(GC.Alloc)
    ///          · 메모리 청소(GC.Collect) 시간 · 물체 수 · 대상 수 · 메모리 → [PERF] 줄 + Logs/PlayTest/perf/&lt;이름표&gt;.json
    /// PC 에서 잰 값이라 폰보다 훨씬 빠름 — 고치기 전·뒤 비교와 "바퀴가 갈수록 늘어나는지" 를 보는 용도.
    /// </summary>
    public class PerfProbe
    {
        readonly Recorder upd = Recorder.Get("BehaviourUpdate");
        readonly Recorder late = Recorder.Get("LateBehaviourUpdate");
        readonly Recorder alloc = Recorder.Get("GC.Alloc");
        readonly Recorder gc = Recorder.Get("GC.Collect");
        readonly Recorder loop = Recorder.Get("PlayerLoop");
        readonly Recorder canvas = Recorder.Get("Canvas.SendWillRenderCanvases");
        readonly Recorder batch = Recorder.Get("Canvas.BuildBatch");

        public string name;
        public int frames, slow;
        public double sumMs, maxMs, sumAlloc, maxAlloc, gcMs, sumLoop, maxLoop, sumUi, maxUi, sumBytes, maxBytes;
        int gc0;

        public PerfProbe()
        {
            upd.enabled = late.enabled = alloc.enabled = gc.enabled = loop.enabled = canvas.enabled = batch.enabled = true;
            if (!Object.FindFirstObjectByType<PerfAllocStart>()) { var go = new GameObject("PerfAlloc"); Object.DontDestroyOnLoad(go); go.AddComponent<PerfAllocStart>(); go.AddComponent<PerfAllocEnd>(); }
        }

        public void Begin(string n)
        {
            name = n; frames = slow = 0; sumMs = maxMs = sumAlloc = maxAlloc = gcMs = sumLoop = maxLoop = sumUi = maxUi = sumBytes = maxBytes = 0;
            gc0 = System.GC.CollectionCount(0);
        }

        /// <summary>한 프레임 기록 (Recorder 는 지난 프레임 값)</summary>
        public void Frame()
        {
            double ms = (upd.elapsedNanoseconds + late.elapsedNanoseconds) / 1e6;
            int a = alloc.sampleBlockCount;
            frames++;
            sumMs += ms; if (ms > maxMs) maxMs = ms;
            if (ms > 4.0) slow++;
            sumAlloc += a; if (a > maxAlloc) maxAlloc = a;
            gcMs += gc.elapsedNanoseconds / 1e6;
            double lp = loop.elapsedNanoseconds / 1e6; sumLoop += lp; if (lp > maxLoop) maxLoop = lp;
            double ui = (canvas.elapsedNanoseconds + batch.elapsedNanoseconds) / 1e6; sumUi += ui; if (ui > maxUi) maxUi = ui;
            long b = PerfAllocEnd.LastBytes; sumBytes += b; if (b > maxBytes) maxBytes = b;
        }

        public string Json(int cycle)
        {
            int objs = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            long mono = Profiler.GetMonoUsedSizeLong();
            int gcs = System.GC.CollectionCount(0) - gc0;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string s = string.Format(inv,
                "{{\"cycle\":{0},\"seg\":\"{1}\",\"frames\":{2},\"avgMs\":{3:0.000},\"maxMs\":{4:0.00},\"slow\":{5},\"allocPerFrame\":{6:0.0},\"allocMax\":{7},\"gcCount\":{8},\"gcMs\":{9:0.00},\"objects\":{10},\"targets\":{11},\"monoMB\":{12:0.0},\"loopMs\":{13:0.00},\"loopMax\":{14:0.0},\"uiMs\":{15:0.000},\"uiMax\":{16:0.00},\"scriptKB\":{17:0.00},\"scriptKBMax\":{18:0.0}}}",
                cycle, name, frames, frames > 0 ? sumMs / frames : 0, maxMs, slow, frames > 0 ? sumAlloc / frames : 0, maxAlloc, gcs, gcMs, objs, Target.All.Count, mono / 1048576.0,
                frames > 0 ? sumLoop / frames : 0, maxLoop, frames > 0 ? sumUi / frames : 0, maxUi, frames > 0 ? sumBytes / frames / 1024 : 0, maxBytes / 1024);
            Debug.Log("[PERF] " + s);
            return s;
        }
    }

    /// <summary>게임 코드가 한 프레임에 만든 쓰레기 메모리(바이트) = 모든 Update 직전 ~ 모든 LateUpdate 직후의 Mono 사용량 차이
    /// (그 사이 메모리 청소가 일어나면 음수 → 0 으로 침). 에디터 몫은 안 들어감</summary>
    [DefaultExecutionOrder(-32000)]
    public class PerfAllocStart : MonoBehaviour
    {
        public static long Start0;
        void Update() => Start0 = Profiler.GetMonoUsedSizeLong();
    }
    [DefaultExecutionOrder(32000)]
    public class PerfAllocEnd : MonoBehaviour
    {
        public static long LastBytes;
        void LateUpdate() => LastBytes = System.Math.Max(0, Profiler.GetMonoUsedSizeLong() - PerfAllocStart.Start0);
    }

    public partial class PlayTestRunner
    {
        IEnumerator PerfTour()
        {
            DontDestroyOnLoad(gameObject);
            S.stage = 51; S.bestEver = 51; S.gems = 1e6; S.gold = 3.2e7;
            S.lvAtk = 40; S.lvSpd = 60; S.lvGold = 150;
            S.weaponUnlocked = true; S.tutorialDone = true;
            S.attendDay = System.DateTime.Now.ToString("yyyyMMdd");   // 출석 창이 안 끼게
            S.slots[0] = Tables.Code(5, "laser"); S.slots[1] = Tables.Code(4, "missile"); S.slots[2] = Tables.Code(3, "shotgun");
            foreach (int k in new[] { 1, 2, 3, 6, 17 }) if (!S.skins.Contains(k)) S.skins.Add(k);
            for (int k = 1; k <= 7; k++) if (!S.supplies.Contains(k)) S.supplies.Add(k);
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            { var ap0 = HUD.I.Get<AttendPopup>(); if (ap0 && ap0.gameObject.activeSelf) ap0.Close(); }

            string label = System.Environment.GetEnvironmentVariable("KACHI_PERF_LABEL");
            if (string.IsNullOrEmpty(label)) label = "run";
            var probe = new PerfProbe();
            var rows = new List<string>();
            const int Cycles = 3;

            IEnumerator Seg(int cyc, string n, float sec)
            {
                probe.Begin(n);
                float end = Time.realtimeSinceStartup + sec;
                yield return null;   // 바꾼 그 프레임은 다음 프레임 기록에 들어감
                while (Time.realtimeSinceStartup < end) { probe.Frame(); yield return null; }
                rows.Add(probe.Json(cyc));
            }

            IEnumerator Dungeon(int cyc, string n, System.Func<string> start)
            {
                S.mineTickets = 99; S.bossTickets = 99; S.challengeCleared.Clear();
                string e = start();
                if (e != null) Log("PERF 던전 시작 못 함 " + n + ": " + e);
                yield return Seg(cyc, n, 6f);
                var rp = HUD.I.Get<ResultPopup>(); if (rp && rp.gameObject.activeSelf) rp.Close();
                if (GM.mode != GameManager.Mode.Stage) GM.ExitDungeon();
            }

            for (int c = 1; c <= Cycles; c++)
            {
                yield return Seg(c, "stage", c == 1 ? 15f : 10f);
                probe.Begin("menus");
                foreach (var open in new System.Action[] { () => HUD.I.Open<WeaponPopup>(), () => HUD.I.Open<SupplyPopup>(), () => HUD.I.Open<SkinPopup>(), () => HUD.I.Open<ShopPopup>() })
                {
                    open();
                    for (int f = 0; f < 20; f++) { yield return null; probe.Frame(); }
                    CloseAll();
                    for (int f = 0; f < 5; f++) { yield return null; probe.Frame(); }
                }
                rows.Add(probe.Json(c));
                yield return Dungeon(c, "challenge", GM.StartChallenge);
                yield return Seg(c, "stage2", 5f);
                yield return Dungeon(c, "mine", GM.StartMine);
                yield return Seg(c, "stage3", 5f);
                yield return Dungeon(c, "bossraid", GM.StartBossRaid);
            }

            Directory.CreateDirectory($"{OutDir}/perf");
            File.WriteAllText($"{OutDir}/perf/{label}.json", "[\n" + string.Join(",\n", rows) + "\n]\n", new UTF8Encoding(false));
            Log($"PERF 끝 → {OutDir}/perf/{label}.json ({rows.Count}줄)");
            Log($"DONE errors={errors}");
            PlayTest.Finish(errors);
        }
    }
}
