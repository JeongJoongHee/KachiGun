using System;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>창 공통: 어두운 바탕 + UI 팩 양피지 창(초록 제목줄 + X) + 제목. 각 창은 Build 에서 내용을 만들고 Refresh 로 갱신.
    /// 창 안 글자는 양피지 위라 진한 갈색(UIKit.Ink), 나무판(UIKit.Row) 위 글자는 흰색.</summary>
    public abstract class Popup : MonoBehaviour
    {
        public RectTransform panel, content;
        public Text title;
        protected virtual Vector2 Size => new(1000, 1500);
        protected virtual string Title => "";
        protected virtual bool CloseOnDim => true;

        protected static SaveData S => SaveData.Cur;
        protected static GameManager GM => GameManager.I;
        protected static HUD H => HUD.I;

        public static T Create<T>(RectTransform root) where T : Popup
        {
            var rt = UIKit.Rect(root, typeof(T).Name).Fill();
            var p = rt.gameObject.AddComponent<T>();
            var dim = rt.gameObject.AddComponent<Image>();
            dim.color = UIKit.Dim;
            var db = rt.gameObject.AddComponent<Button>();
            db.transition = Selectable.Transition.None;
            db.onClick.AddListener(() => { if (p.CloseOnDim) p.Close(); });

            var pan = UIKit.Img(rt, UIKit.Panel, p.Size, "Panel");
            pan.rectTransform.Place(0.5f, 0.5f, new Vector2(0, -20), p.Size, new Vector2(0.5f, 0.5f));
            pan.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 판 누름이 바탕까지 안 가게
            p.panel = pan.rectTransform;

            // 제목 = 초록 제목줄 위 (창 그림 위쪽 14칸 x 5px = 70)
            p.title = UIKit.Label(p.panel, L.T(p.Title), 46, Color.white, TextAnchor.MiddleCenter, 4);
            p.title.rectTransform.Place(0.5f, 1, new Vector2(0, -36), new Vector2(p.Size.x - 260, 70), new Vector2(0.5f, 0.5f));
            // 닫기 = 제목줄 오른쪽의 X 칸 위에 투명 버튼(누르기 쉽게 넓게)
            var xb = UIKit.Rect(p.panel, "Close").Place(1, 1, new Vector2(4, 4), new Vector2(120, 100), new Vector2(1, 1));
            var xi = xb.gameObject.AddComponent<Image>(); xi.color = new Color(0, 0, 0, 0.001f);
            xb.gameObject.AddComponent<Button>().onClick.AddListener(p.Close);

            p.content = UIKit.Rect(p.panel, "Content").Fill(44, 50, 44, 100);
            p.Build();
            rt.gameObject.SetActive(false);
            return p;
        }

        protected abstract void Build();
        public virtual void Refresh() { }
        /// <summary>HUD 가 0.1초마다 부름 (열려 있을 때)</summary>
        public virtual void Tick() { }

        public virtual void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (!H.openPopups.Contains(this)) H.openPopups.Add(this);
            panel.localScale = Vector3.one * 0.9f;
            Refresh();
        }

        public virtual void Close()
        {
            gameObject.SetActive(false);
            H.openPopups.Remove(this);
        }

        void Update()
        {
            panel.localScale = Vector3.MoveTowards(panel.localScale, Vector3.one, Time.unscaledDeltaTime * 1.2f);
        }

        protected void Msg(string err) => H.Msg(err);
    }

    /// <summary>예/아니오 · 던전 입장 확인</summary>
    public class ConfirmPopup : Popup
    {
        public enum Kind { Ask, Challenge, Mine, Boss }
        protected override Vector2 Size => new(860, 700);
        protected override string Title => "확인";
        Text body;
        Button ok;
        Action onOk;

        protected override void Build()
        {
            body = UIKit.Label(content, "", 38, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            body.rectTransform.Fill(0, 140, 0, 20);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            ok = UIKit.KitBtn(content, "btn_green", new Vector2(330, 110), L.T("시작"), 44, () => { var a = onOk; Close(); a?.Invoke(); });
            ((RectTransform)ok.transform).Place(0.5f, 0, new Vector2(0, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
        }

        /// <summary>titleText 는 한국어 그대로 주면 지금 언어로 바뀜</summary>
        public static void Ask(string text, Action yes, string titleText = "확인")
        {
            var p = HUD.I.Open<ConfirmPopup>();
            p.title.text = L.T(titleText);
            p.body.text = text;
            p.onOk = yes;
            UIKit.LabelOf(p.ok).text = L.T("확인");
        }

        public static void Dungeon(Kind k)
        {
            var gm = GameManager.I; var s = SaveData.Cur;
            string text; Action go;
            switch (k)
            {
                case Kind.Challenge:
                    int ch = gm.ChallengeChapter;
                    if (ch == 0) { HUD.I.Toast(L.T("다음 챕터에서 새 도전이 열립니다")); return; }
                    var cr = Tables.Challenge(ch);
                    text = $"<size=48>{L.F("도전 {0}", ch)}</size>\n\n" + L.F("{0}초 안에 좀비 {1}마리를 모두 처치!", cr.time.ToString("0"), cr.count) + "\n" + L.T("화면을 눌러 분대를 직접 움직이면 빨라요") + "\n\n" + L.F("보상: 다이아 {0} (챕터당 1번)", cr.reward.ToString("0"));
                    go = () => HUD.I.Msg(gm.StartChallenge());
                    break;
                case Kind.Mine:
                    s.RefreshDaily();
                    double mm = Tables.MineMul(s.Chapter);
                    var ores = new System.Text.StringBuilder();
                    foreach (var o in Tables.Ores) if (o.count > 0) ores.Append(o.reward > 0 ? $"{L.T(o.name)} = {L.T("다이아")} {o.reward * mm:0.##}\n" : $"{L.T(o.name)} = {L.T("보상 없음")}\n");
                    text = $"<size=48>{L.F("다이아 광산 {0}단계", Tables.MineTier(s.Chapter))}</size>\n\n{L.T("모든 공격이 1 피해로 고정!")}\n{ores}\n{L.T("오늘 티켓")} {s.mineTickets}/{Tables.Ci("MineTickets")}";
                    go = () => HUD.I.Msg(gm.StartMine());
                    break;
                default:
                    s.RefreshDaily();
                    var br = Tables.BossRaid(s.bossLv);
                    text = $"<size=48>{L.F("보스 던전 Lv.{0}", s.bossLv + 1)}</size>\n\n{L.F("{0}초 안에 {1}를 처치!", br.time.ToString("0"), L.T(Tables.Zombie(br.bossId).name))}\n{L.T("체력")} {Num.F(br.hp)} · {L.F("보상: 다이아 {0}", br.reward.ToString("0"))}\n\n{L.T("오늘 티켓")} {s.bossTickets}/{Tables.Ci("BossRaidTickets")}";
                    go = () => HUD.I.Msg(gm.StartBossRaid());
                    break;
            }
            var p = HUD.I.Open<ConfirmPopup>();
            p.title.text = L.T(k == Kind.Challenge ? "도전" : k == Kind.Mine ? "광산" : "보스");
            p.body.text = text;
            p.onOk = go;
            UIKit.LabelOf(p.ok).text = L.T("시작");
        }
    }

    /// <summary>던전 결과: 받기 · 광고 보고 x3</summary>
    public class ResultPopup : Popup
    {
        protected override Vector2 Size => new(860, 760);
        protected override bool CloseOnDim => false;
        Text body;
        Button take, ad;
        Image gemIcon;
        double reward; bool win;

        Image[] stars; Image lose;

        protected override void Build()
        {
            // 성공 = 별 3개(가운데 큰 별), 실패 = 파란 X (UI 팩 Win_loose)
            stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                bool mid = i == 1;
                var sz = mid ? new Vector2(170, 164) : new Vector2(112, 112);
                stars[i] = UIKit.KitImg(content, mid ? "star" : "star_s", sz, "Star" + i);
                stars[i].rectTransform.Place(0.5f, 1, new Vector2((i - 1) * 170, mid ? -10 : -50), sz, new Vector2(0.5f, 1));
            }
            lose = UIKit.KitImg(content, "lose", new Vector2(150, 150), "Lose");
            lose.rectTransform.Place(0.5f, 1, new Vector2(0, -10), new Vector2(150, 150), new Vector2(0.5f, 1));
            body = UIKit.Label(content, "", 46, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            body.rectTransform.Place(0.5f, 0.5f, new Vector2(60, -40), new Vector2(600, 80), new Vector2(0.5f, 0.5f));
            var gem = UIKit.Img(content, Art.Get("UI/icon_gem"), new Vector2(90, 90), "Gem");
            gem.rectTransform.Place(0.5f, 0.5f, new Vector2(-160, -40), new Vector2(90, 90), new Vector2(0.5f, 0.5f));
            gemIcon = gem;
            take = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(330, 110), L.T("받기"), 42, () => Claim(1));
            ((RectTransform)take.transform).Place(0.5f, 0, new Vector2(-180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
            ad = UIKit.ColorBtn(content, new Color(0.95f, 0.6f, 0.15f), new Vector2(330, 110), L.T("광고 보고") + " x" + Tables.Buff("dungeon_ad_x3").mul.ToString("0.#"), 40, () =>
                AdService.Show("dungeon", () => Claim(Tables.Buff("dungeon_ad_x3").mul)));
            ((RectTransform)ad.transform).Place(0.5f, 0, new Vector2(180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
        }

        public static void Show(string t, bool win, double reward)
        {
            var p = HUD.I.Open<ResultPopup>();
            p.title.text = t;
            p.win = win; p.reward = reward;
            p.body.text = reward > 0 ? L.T("다이아") + $" +{reward:0.#}" : L.T("보상 없음");
            p.gemIcon.gameObject.SetActive(reward > 0);
            ((RectTransform)p.body.transform).anchoredPosition = new Vector2(reward > 0 ? 60 : 0, -40);
            // 성공(또는 광산 정상 종료) = 별, 실패 = X
            foreach (var st in p.stars) st.gameObject.SetActive(win);
            p.lose.gameObject.SetActive(!win);
            p.ad.gameObject.SetActive(reward > 0);
            UIKit.LabelOf(p.take).text = L.T(reward > 0 ? "받기" : "돌아가기");
            ((RectTransform)p.take.transform).anchoredPosition = new Vector2(reward > 0 ? -180 : 0, 10);
        }

        bool claimed;

        void Claim(double mul)
        {
            if (claimed) return;
            claimed = true;
            base.Close();
            GM.ClaimDungeon(reward * mul, win);
            if (reward > 0) H.Toast(L.T("다이아") + $" +{reward * mul:0.#}");
        }

        public override void Open() { claimed = false; base.Open(); }

        /// <summary>X 로 닫아도 기본 보상은 받고 돌아감</summary>
        public override void Close() => Claim(1);
    }
}
