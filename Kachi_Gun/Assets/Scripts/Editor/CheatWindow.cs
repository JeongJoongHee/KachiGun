using UnityEditor;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 시험용 치트 창 (메뉴: 카치건 > 치트 창). 골드·다이아·던전 티켓을 늘리거나 비운다.
    /// 재생 중이면 바로 화면에 반영, 재생 전이면 세이브에 저장돼 다음 재생부터 적용.
    /// 에디터 전용이라 빌드(폰)에는 들어가지 않는다.
    /// </summary>
    public class CheatWindow : EditorWindow
    {
        double goldInput = 1000000, gemInput = 1000;

        [MenuItem("카치건/치트 창")]
        public static void Open()
        {
            var w = GetWindow<CheatWindow>("치트");
            w.minSize = new Vector2(300, 330);
        }

        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnGUI()
        {
            var s = SaveData.Cur;
            EditorGUILayout.HelpBox(Application.isPlaying ? "재생 중 — 누르면 바로 반영" : "재생 전 — 세이브에 저장돼 다음 재생부터 반영", MessageType.None);

            // ---------- 골드 ----------
            GUILayout.Label($"골드  {Num.F(s.gold)}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1k")) AddGold(1e3);
                if (GUILayout.Button("+1m")) AddGold(1e6);
                if (GUILayout.Button("+1b")) AddGold(1e9);
                if (GUILayout.Button("+1t")) AddGold(1e12);
                if (GUILayout.Button("x10")) AddGold(System.Math.Max(s.gold, 1) * 9);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                goldInput = EditorGUILayout.DoubleField(goldInput);
                if (GUILayout.Button("더하기", GUILayout.Width(60))) AddGold(goldInput);
                if (GUILayout.Button("0으로", GUILayout.Width(55))) AddGold(-s.gold);
            }
            GUILayout.Label($"   입력값 = {Num.F(goldInput)}", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);

            // ---------- 다이아 ----------
            GUILayout.Label($"다이아  {Num.Gem(s.gems)}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+100")) AddGems(100);
                if (GUILayout.Button("+1,000")) AddGems(1000);
                if (GUILayout.Button("+10,000")) AddGems(10000);
                if (GUILayout.Button("+100,000")) AddGems(100000);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                gemInput = EditorGUILayout.DoubleField(gemInput);
                if (GUILayout.Button("더하기", GUILayout.Width(60))) AddGems(gemInput);
                if (GUILayout.Button("0으로", GUILayout.Width(55))) AddGems(-s.gems);
            }

            EditorGUILayout.Space(8);

            // ---------- 던전 티켓 ----------
            GUILayout.Label($"던전 티켓  광산 {s.mineTickets} · 보스 {s.bossTickets}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("광산 +1")) { s.mineTickets++; Apply(false); }
                if (GUILayout.Button("광산 +10")) { s.mineTickets += 10; Apply(false); }
                if (GUILayout.Button("보스 +1")) { s.bossTickets++; Apply(false); }
                if (GUILayout.Button("보스 +10")) { s.bossTickets += 10; Apply(false); }
            }
        }

        static void AddGold(double v)
        {
            var s = SaveData.Cur;
            s.gold = System.Math.Max(0, s.gold + v);
            Apply(false);
        }

        static void AddGems(double v)
        {
            var s = SaveData.Cur;
            s.gems = System.Math.Max(0, s.gems + v);
            Apply(true);
        }

        /// <summary>저장하고, 재생 중이면 위 HUD 숫자를 바로 갱신(살짝 튀는 연출 포함)</summary>
        static void Apply(bool gem)
        {
            SaveData.Cur.Save();
            if (Application.isPlaying && HUD.I != null)
            {
                HUD.I.Refresh();
                HUD.I.Bump(gem);
            }
        }
    }
}
