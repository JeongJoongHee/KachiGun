using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 11-11 요청_04 카피바라 말풍선 — 흰 바탕 + 검정 선(Kit/bubble · bubble_tail), 대사는 Speech_Lines 표.
    ///   Clear = 스테이지 클리어(리더) · Boss = 보스 등장(리더) · Random = 싸우는 중 Speech_Random_Min~Max 초마다 아무나.
    /// 피해 숫자와 같은 층(Fx.uiLayer, 따로 판)에 그려서 매 프레임 따라가도 위 화면 전체를 다시 모으지 않음.
    /// </summary>
    public class Speech : MonoBehaviour
    {
        public static Speech I;

        public class Line { public string id, trigger, who, text; public float weight; }
        static List<Line> lines;

        class Bubble
        {
            public RectTransform rt, body, tail; public Text t; public CanvasGroup cg;
            public Capy who; public float time, life;
        }
        readonly List<Bubble> pool = new();
        float nextRandom = -1;

        static GameManager GM => GameManager.I;

        public static List<Line> Lines
        {
            get
            {
                if (lines != null) return lines;
                lines = new List<Line>();
                foreach (var r in Tables.ReadTable("Speech_Lines"))
                {
                    var l = new Line { id = r.S("Id"), trigger = r.S("Trigger"), who = r.S("Who"), text = r.S("Text"), weight = r.F("Weight", 1) };
                    if (l.text.Length > 0 && l.weight > 0) lines.Add(l);
                }
                return lines;
            }
        }

        void Awake() { I = this; }

        /// <summary>그 상황(Trigger)의 대사 하나 (비중대로)</summary>
        public static Line Pick(string trigger)
        {
            float sum = 0;
            foreach (var l in Lines) if (l.trigger == trigger) sum += l.weight;
            if (sum <= 0) return null;
            float k = Random.value * sum;
            Line last = null;
            foreach (var l in Lines)
            {
                if (l.trigger != trigger) continue;
                last = l;
                k -= l.weight;
                if (k <= 0) return l;
            }
            return last;
        }

        /// <summary>상황 대사 띄우기 (Clear · Boss · Random)</summary>
        public static void Say(string trigger)
        {
            if (I == null || Squad.I == null || GM == null || GM.eco) return;
            if (trigger == "Random" && I.ActiveCount > 0) return;   // 가끔 대사는 다른 말풍선이 떠 있으면 쉼 (클리어·보스는 덮어씀)
            var l = Pick(trigger);
            if (l == null) return;
            Capy c = Squad.I.Leader;
            if (l.who == "Any" && Squad.I.cats.Count > 0) c = Squad.I.cats[Random.Range(0, Squad.I.cats.Count)];
            if (c) I.Show(c, L.T(l.text));
        }

        Bubble Current(Capy c) { foreach (var b in pool) if (b.who == c && b.rt.gameObject.activeSelf) return b; return null; }
        public bool Showing(string text) { foreach (var b in pool) if (b.rt.gameObject.activeSelf && b.t.text == text) return true; return false; }

        /// <summary>그 카피바라 머리 위에 말풍선 (이미 말하는 중이면 그 풍선을 바꿈)</summary>
        public void Show(Capy c, string text)
        {
            var layer = Fx.I != null ? Fx.I.uiLayer : null;
            if (!layer || !c) return;
            var b = Current(c);
            if (b == null) foreach (var p in pool) if (!p.rt.gameObject.activeSelf) { b = p; break; }
            if (b == null) { b = Make(layer); pool.Add(b); }
            b.who = c;
            b.t.text = text;
            float w = Mathf.Clamp(b.t.preferredWidth + 64, 150, 620);
            b.body.sizeDelta = new Vector2(w, 92);
            b.life = Mathf.Max(0.6f, (float)Tables.C("Speech_Show", 2.2));
            b.time = 0;
            b.rt.gameObject.SetActive(true);
            b.rt.SetAsLastSibling();
            Place(b);
        }

        Bubble Make(RectTransform layer)
        {
            var b = new Bubble();
            b.rt = UIKit.Rect(layer, "Speech");
            b.rt.anchorMin = b.rt.anchorMax = new Vector2(0.5f, 0.5f);
            b.rt.pivot = new Vector2(0.5f, 0);
            b.rt.sizeDelta = new Vector2(10, 10);
            b.cg = b.rt.gameObject.AddComponent<CanvasGroup>();
            b.cg.blocksRaycasts = false; b.cg.interactable = false;
            var body = UIKit.KitImg(b.rt, "bubble", new Vector2(300, 92), "Body");
            body.raycastTarget = false;
            b.body = body.rectTransform;
            b.body.anchorMin = b.body.anchorMax = new Vector2(0.5f, 0);
            b.body.pivot = new Vector2(0.5f, 0);
            var tail = UIKit.KitImg(b.rt, "bubble_tail", new Vector2(40, 37), "Tail");   // 몸통 다음에 그려 몸통 아래 선을 덮음
            tail.raycastTarget = false;
            b.tail = tail.rectTransform;
            b.tail.anchorMin = b.tail.anchorMax = new Vector2(0.5f, 0);
            b.tail.pivot = new Vector2(0.33f, 0);   // 꼬리 끝(16/50 자리)이 카피바라 머리를 가리킴
            b.tail.sizeDelta = new Vector2(40, 37);
            b.tail.anchoredPosition = Vector2.zero;
            b.t = UIKit.Label(b.body, "", 38, new Color(0.11f, 0.07f, 0.04f), TextAnchor.MiddleCenter, 0);
            b.t.rectTransform.Fill(18, 4, 18, 8);
            b.t.raycastTarget = false;
            b.t.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.rt.gameObject.SetActive(false);
            return b;
        }

        /// <summary>머리 위(꼬리 끝) 자리 · 몸통은 화면 밖으로 안 나가게 옆으로 밀고 꼬리는 그대로 카피바라를 가리킴</summary>
        void Place(Bubble b)
        {
            // 머리 오른쪽 위를 가리키고 몸통은 오른쪽으로 (카피바라는 왼쪽에 서 있고, 바로 위에 띄우면 위 동료를 가림)
            var c = b.who;
            Vector2 head;
            if (c.sr) { var bd = c.sr.bounds; head = new Vector2(bd.center.x + bd.extents.x * 0.55f, bd.max.y - bd.extents.y * 0.62f); }   // 얼굴 오른쪽 (주둥이 옆)
            else head = (Vector2)c.transform.position + new Vector2(0.3f, 1.2f);
            var p = Fx.I.WorldToUi(head);
            b.rt.anchoredPosition = p;
            float half = ((RectTransform)b.rt.parent).rect.width * 0.5f - 12f;
            float w = b.body.sizeDelta.x;
            float x = Mathf.Clamp(w * 0.5f - 46f, -half + w * 0.5f - p.x, half - w * 0.5f - p.x);
            x = Mathf.Clamp(x, -w * 0.5f + 40, w * 0.5f - 40);   // 꼬리가 몸통 끝 밖으로 안 나가게
            b.body.anchoredPosition = new Vector2(x, 29);   // 꼬리(높이 37) 윗부분 8 이 몸통 아래 선 위로 겹침
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var b in pool)
            {
                if (!b.rt.gameObject.activeSelf) continue;
                b.time += dt;
                if (!b.who || b.time >= b.life || GM == null || GM.eco) { b.rt.gameObject.SetActive(false); continue; }
                Place(b);
                float pop = Mathf.Clamp01(b.time / 0.14f);
                b.rt.localScale = Vector3.one * (0.6f + 0.4f * (1 - (1 - pop) * (1 - pop)) + Mathf.Sin(pop * Mathf.PI) * 0.08f);
                b.cg.alpha = Mathf.Clamp01((b.life - b.time) / 0.25f);
            }

            // 가끔 아무나 한마디 (스테이지에서 싸우는 중만)
            float lo = (float)Tables.C("Speech_Random_Min", 15), hi = (float)Tables.C("Speech_Random_Max", 30);
            if (hi <= 0 || GM == null) return;
            if (nextRandom < 0) nextRandom = Random.Range(lo, hi) * 0.5f;    // 처음엔 조금 빨리
            bool fighting = GM.mode == GameManager.Mode.Stage && !GM.bossPhase && !GM.eco;
            if (!fighting) return;
            nextRandom -= dt;
            if (nextRandom > 0) return;
            nextRandom = Random.Range(lo, Mathf.Max(lo, hi));
            foreach (var b in pool) if (b.rt.gameObject.activeSelf) return;   // 다른 말풍선이 떠 있으면 이번엔 쉼
            Say("Random");
        }

        /// <summary>떠 있는 말풍선 모두 끔 (시험·장면 바뀜)</summary>
        public void Clear() { foreach (var b in pool) b.rt.gameObject.SetActive(false); }
        public int ActiveCount { get { int n = 0; foreach (var b in pool) if (b.rt.gameObject.activeSelf) n++; return n; } }
        public string ActiveText { get { foreach (var b in pool) if (b.rt.gameObject.activeSelf) return b.t.text; return null; } }
    }
}
