using System.Collections;
using System.IO;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 11-15 연출 촬영 (사용자 "카피바라가 전진하고 좀비떼가 뭉쳐 있게 · 보스전엔 보스만 + 말풍선 · 보스를 잡으면 다음 맵으로 이동"):
    ///   KACHI_FLOW_LABEL=before bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunFlow --play
    /// 같은 세이브 · 같은 순서로 세 장면을 초당 15장(게임 시간 1/15초 고정) → Logs/PlayTest/flow/&lt;이름표&gt;/&lt;장면&gt;/0000.jpg
    ///   a_battle = 평소 전투 · b_boss = 보스 등장~싸움 · c_next = 보스 처치 뒤 다음 맵
    /// 고치기 전·뒤를 같은 장면으로 찍어 tools/flow_compare.py 가 GIF 나란히 비교 HTML 을 만든다. 게임 코드에 없는 API 는 안 씀(옛 코드에서도 돌게).
    /// </summary>
    public partial class PlayTestRunner
    {
        const int FlowFps = 15;

        IEnumerator FlowRec(string label, string clip, float sec, System.Action<float> each = null)
        {
            string dir = $"{OutDir}/flow/{label}/{clip}";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = 1f / FlowFps;
            int n = Mathf.RoundToInt(sec * FlowFps);
            for (int i = 0; i < n; i++)
            {
                each?.Invoke(i / (float)FlowFps);
                yield return null;
                CaptureJpg($"{dir}/{i:0000}.jpg");
            }
            Time.captureDeltaTime = 0;
            Log($"연출 장면 {label}/{clip}: {n}장");
        }

        IEnumerator FlowTour()
        {
            string label = System.Environment.GetEnvironmentVariable("KACHI_FLOW_LABEL");
            if (string.IsNullOrEmpty(label)) label = "run";
            DontDestroyOnLoad(gameObject);
            Random.InitState(1115);
            S.stage = 1; S.bestEver = 4; S.gems = 500; S.gold = 0;
            S.lvAtk = 12; S.lvSpd = 12; S.lvGold = 5;
            S.weaponUnlocked = true; S.tutorialDone = true;
            S.attendDay = SaveData.Today;   // 촬영에 출석 창이 안 끼게
            S.slots[0] = Tables.Code(0, "single"); S.slots[1] = Tables.Code(0, "shotgun"); S.slots[2] = Tables.Code(0, "fast");
            S.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return Wait(1.0f);
            SetupCapture(540, 1170);
            yield return Wait(0.5f);
            var ap = HUD.I.Get<AttendPopup>(); if (ap && ap.gameObject.activeSelf) ap.Close();
            Random.InitState(1115);

            // A. 평소 전투 (처음 3초는 자리 잡기)
            yield return Wait(3f);
            yield return FlowRec(label, "a_battle", 8f);

            // B. 보스: 1-5 처치 목표를 채움 → 보스 등장 · 싸움 (보스 체력을 크게 해 10초 동안 안 죽게)
            S.stage = 4; S.kills = GM.Quota; GM.Notify();
            bool hpSet = false;
            yield return FlowRec(label, "b_boss", 10f, t =>
            {
                if (!hpSet && GM.boss != null) { GM.boss.hp = GM.boss.maxHp = 1e12; hpSet = true; }
            });

            // C. 보스 처치 → 다음 맵
            bool killed = false;
            yield return FlowRec(label, "c_next", 7f, t =>
            {
                if (!killed && GM.boss != null) { killed = true; GM.boss.Hit(GM.boss.hp + 1, GM.boss.Pos); }
            });
            Log($"연출 촬영 끝: Stage {SaveData.StageName(S.stage)}");

            // D. (새 코드에서만) 스테이지 하나 깨는 시간: Advance_On 0(예전) ↔ 1(전진) — 같은 분대·같은 스테이지, 게임 시간 4배속
            var cfgF = typeof(Tables).GetField("config", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var cfg = cfgF != null ? cfgF.GetValue(null) as System.Collections.Generic.Dictionary<string, double> : null;
            if (cfg != null && cfg.ContainsKey("Advance_On"))
            {
                yield return Wait(4f);   // 이동 연출이 끝나게
                double keep = cfg["Advance_On"];
                Time.timeScale = 4f;
                foreach (int st in new[] { 2, 21 })
                    foreach (double on in new[] { 0.0, 1.0 })
                    {
                        cfg["Advance_On"] = on;
                        GM.ClearTargets();
                        S.stage = st; S.kills = 0; S.bossFarm = false; GM.bossPhase = false; GM.Notify();
                        Squad.I.ResetPosition();
                        float t0 = Time.time;
                        yield return WaitUntil(() => S.stage != st, 120f);
                        float sec = Time.time - t0;
                        Log($"[FLOW] 스테이지 {SaveData.StageName(st)} 깨는 시간 Advance_On={on}: {sec:0.0}초 (처치 목표 {GM.Quota})");
                    }
                Time.timeScale = 1f;
                cfg["Advance_On"] = keep;
            }
            PlayTest.Finish(errors);
        }
    }
}
