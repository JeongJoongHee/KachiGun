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
        /// <summary>11-05 (추가 요청/UI 수정.png): 띠 없는 크림 창 + 위 가운데 노란 제목 띠 + 오른쪽 위 빨간 X</summary>
        protected virtual bool PillTitle => false;

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

            var pan = UIKit.Img(rt, p.PillTitle ? Art.Kit("window_plain") : UIKit.Panel, p.Size, "Panel");
            pan.rectTransform.Place(0.5f, 0.5f, new Vector2(0, -20), p.Size, new Vector2(0.5f, 0.5f));
            pan.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 판 누름이 바탕까지 안 가게
            p.panel = pan.rectTransform;

            if (p.PillTitle)
            {
                // 노란 제목 띠 = 창 위 끝에 걸침 · X = 오른쪽 위 빨간 버튼
                float pw = Mathf.Min(p.Size.x - 300, 620);
                var pill = UIKit.KitImg(p.panel, "title_pill", new Vector2(pw, 118), "TitlePill");
                pill.rectTransform.Place(0.5f, 1, new Vector2(0, 26), new Vector2(pw, 118), new Vector2(0.5f, 0.5f));
                p.title = UIKit.Label(pill.transform, L.T(p.Title), 50, Color.white, TextAnchor.MiddleCenter, 4);
                p.title.rectTransform.Fill(20, 14, 20, 4);
                var xr = UIKit.KitBtn(p.panel, "btn_x_red", new Vector2(96, 96), null, 0, p.Close, "Close");
                ((RectTransform)xr.transform).Place(1, 1, new Vector2(18, 18), new Vector2(96, 96), new Vector2(1, 1));
            }
            else
            {
                // 제목 = 초록 제목줄 위 (창 그림 위쪽 14칸 x 5px = 70)
                p.title = UIKit.Label(p.panel, L.T(p.Title), 46, Color.white, TextAnchor.MiddleCenter, 4);
                p.title.rectTransform.Place(0.5f, 1, new Vector2(0, -36), new Vector2(p.Size.x - 260, 70), new Vector2(0.5f, 0.5f));
                // 닫기 = 제목줄 오른쪽의 X 칸 위에 투명 버튼(누르기 쉽게 넓게)
                var xb = UIKit.Rect(p.panel, "Close").Place(1, 1, new Vector2(4, 4), new Vector2(120, 100), new Vector2(1, 1));
                var xi = xb.gameObject.AddComponent<Image>(); xi.color = new Color(0, 0, 0, 0.001f);
                xb.gameObject.AddComponent<Button>().onClick.AddListener(p.Close);
            }

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
            H.openPopups.Remove(this); H.openPopups.Add(this);   // 마지막 = 맨 위 창 (뒤로 가기가 이 창부터 닫음)
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
            if (panel.localScale != Vector3.one) panel.localScale = Vector3.MoveTowards(panel.localScale, Vector3.one, Time.unscaledDeltaTime * 1.2f);
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
                    text = $"<size=48>{L.F("비밀던전 {0}단계", Tables.MineTier(s.Chapter))}</size>\n\n{L.T("모든 공격이 1 피해로 고정!")}\n{ores}\n{L.T("오늘 티켓")} {s.mineTickets}/{Tables.Ci("MineTickets")}";
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
            p.title.text = L.T(k == Kind.Challenge ? "도전" : k == Kind.Mine ? "비밀던전" : "보스");
            p.body.text = text;
            p.onOk = go;
            UIKit.LabelOf(p.ok).text = L.T("시작");
        }
    }

    /// <summary>게임 종료 확인 (11-04): 설정 창 [게임 종료] · 폰 뒤로 가기(열린 창이 없을 때). [취소] · [종료]</summary>
    public class QuitPopup : Popup
    {
        protected override Vector2 Size => new(860, 560);
        protected override string Title => "게임 종료";
        /// <summary>시험용: 에디터에서 [종료]를 누른 횟수 (에디터는 실제로 꺼지지 않음)</summary>
        public static int editorQuitCount;

        protected override void Build()
        {
            var body = UIKit.Label(content, L.T("게임을 종료할까요?"), 42, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            body.rectTransform.Fill(0, 140, 0, 20);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            var no = UIKit.ColorBtn(content, new Color(0.42f, 0.4f, 0.38f), new Vector2(330, 110), L.T("취소"), 42, Close);
            ((RectTransform)no.transform).Place(0.5f, 0, new Vector2(-180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
            var yes = UIKit.ColorBtn(content, new Color(0.8f, 0.32f, 0.26f), new Vector2(330, 110), L.T("종료"), 42, Quit);
            ((RectTransform)yes.transform).Place(0.5f, 0, new Vector2(180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
        }

        static void Quit()
        {
            Fx.I?.FlushMoney();
            SaveData.Cur.Save();
#if UNITY_EDITOR
            editorQuitCount++;
            HUD.I.Toast(L.T("에디터에서는 종료되지 않습니다"));
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>방치 보상 (11-04): 비운 시간 · 기준 스테이지 · 골드 → [받기] · [광고 보고 x2]. X·뒤로 가기도 기본 보상은 받음</summary>
    public class OfflinePopup : Popup
    {
        protected override Vector2 Size => new(860, 770);
        protected override string Title => "방치 보상";
        protected override bool CloseOnDim => false;
        Text timeT, stageT, rewardT, note;
        Button take, ad;
        public long awaySec;      // 실제로 비운 시간
        public double sec, gold;  // 보상에 쓴 시간(최대까지) · 골드
        bool claimed;

        // 11-04 요청_02: 가운데 정렬 · 잘 보이는 글자(나무판 위 흰 글자 + 테두리) · "오프라인 시간 : hh:mm" / "스테이지 : n-n" / "보상 : n"
        protected override void Build()
        {
            timeT = InfoRow(0, null);
            stageT = InfoRow(1, null);
            rewardT = InfoRow(2, "UI/icon_money");
            note = UIKit.Label(content, "", 30, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            note.rectTransform.Place(0.5f, 1, new Vector2(0, -400), new Vector2(760, 50), new Vector2(0.5f, 0.5f));
            take = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(330, 110), L.T("받기"), 42, () => Claim(1));
            ((RectTransform)take.transform).Place(0.5f, 0, new Vector2(-180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
            ad = UIKit.ColorBtn(content, new Color(0.95f, 0.6f, 0.15f), new Vector2(330, 110), "", 40, () =>
                AdService.Show("offline", () => Claim(Tables.C("Offline_AdMul", 2))));
            ((RectTransform)ad.transform).Place(0.5f, 0, new Vector2(180, 10), new Vector2(330, 110), new Vector2(0.5f, 0));
        }

        /// <summary>나무판 한 줄 + 가운데 흰 글자 (icon 이 있으면 글자 왼쪽에 붙여 그림)</summary>
        Text InfoRow(int i, string icon)
        {
            var row = UIKit.Row(content, "Info" + i);
            row.rectTransform.Place(0.5f, 1, new Vector2(0, -10 - i * 124), new Vector2(740, 108), new Vector2(0.5f, 1));
            // 11-04 밤: 글자·금화 0.8배(46 → 37) · 판 아래 턱만큼 위로 4 (앞면 가운데)
            var t = UIKit.Label(row.transform, "", 37, Color.white, TextAnchor.MiddleCenter, 2.5f);
            t.rectTransform.Fill(20, 8, 20, 0);
            if (icon != null)
            {
                var ic = UIKit.Img(row.transform, Art.Get(icon), new Vector2(50, 50), "Icon");
                ic.preserveAspect = true;
                ic.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 4), new Vector2(50, 50), new Vector2(0.5f, 0.5f));
                t.gameObject.AddComponent<IconFollow>().icon = ic.rectTransform;
            }
            return t;
        }

        /// <summary>창이 이미 떠 있으면(백그라운드를 또 다녀옴) 시간을 더함</summary>
        public static void Show(long away, double sec)
        {
            var old = HUD.I.Get<OfflinePopup>();
            bool stack = old != null && old.gameObject.activeSelf && !old.claimed;
            var p = HUD.I.Open<OfflinePopup>();
            if (stack) { away += p.awaySec; sec += p.sec; }
            p.claimed = false;
            p.awaySec = away;
            p.sec = System.Math.Min(sec, GameManager.OfflineMaxSec);
            p.gold = GameManager.I.OfflineGoldPerSec * p.sec;
            double maxH = Tables.C("Offline_MaxHours", 8);
            // 11-07: 보여 주는 시간도 최대(8시간)에서 멈춤 — 전엔 실제로 비운 시간(10:00 등)이 그대로 보여 무제한으로 쌓이는 것처럼 보였음
            p.timeT.text = L.T("오프라인 시간") + " : " + HhMm((long)p.sec);
            p.stageT.text = L.T("스테이지") + " : " + SaveData.StageName(SaveData.Cur.stage);
            p.rewardT.text = L.T("보상") + " : " + Num.F(p.gold);
            p.note.text = L.F("최대 {0}시간까지 쌓입니다", maxH.ToString("0.#"));
            UIKit.LabelOf(p.ad).text = L.T("광고 보고") + " x" + Tables.C("Offline_AdMul", 2).ToString("0.#");
        }

        /// <summary>비운 시간 hh:mm (24시간이 넘어도 시간으로 셈: 30:05)</summary>
        public static string HhMm(long s) => $"{s / 3600:00}:{s % 3600 / 60:00}";

        void Claim(double mul)
        {
            if (claimed) return;
            claimed = true;
            base.Close();
            double g = gold * mul;
            S.gold += g;
            AudioManager.Play(AudioManager.Sfx.Coin);
            H.Bump(false);
            H.Toast(L.T("골드") + " +" + Num.F(g));
            GM.Notify();
            S.Save();
        }

        /// <summary>X · 뒤로 가기로 닫아도 기본 보상은 받음</summary>
        public override void Close() => Claim(1);
    }

    /// <summary>가운데 정렬 글자의 왼쪽 끝 바로 앞에 아이콘을 붙여 둠 (글자 길이가 바뀌어도 따라감)</summary>
    public class IconFollow : MonoBehaviour
    {
        public RectTransform icon;
        Text t;
        void LateUpdate()
        {
            if (!icon) return;
            t ??= GetComponent<Text>();
            float w = t.preferredWidth;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = new Vector2(icon.sizeDelta.x / 2 + 4, rt.anchoredPosition.y);   // 세로 자리는 그대로
            icon.anchoredPosition = new Vector2(rt.anchoredPosition.x - w / 2 - icon.sizeDelta.x / 2 - 8, rt.anchoredPosition.y);
        }
    }

    /// <summary>출석 보상 (11-04): 하루 한 번 접속 창 — 1·2·3일차 칸(받은 칸 = 체크, 오늘 칸 = 금색 테두리) + [받기].
    /// 칸 수·다이아는 Global_Config Attend_Days · Attend_Gems_N. X·뒤로 가기로 닫아도 오늘 보상은 받음.</summary>
    public class AttendPopup : Popup
    {
        protected override Vector2 Size => new(900, 730);
        protected override string Title => "출석 보상";
        protected override bool CloseOnDim => false;

        class Cell { public RectTransform rt; public Image board, glow, check, gem; public Text day, amount; }
        Cell[] cells = new Cell[0];
        Text note;
        Button take;
        public int index;        // 오늘 받을(받은) 칸
        public bool claimed;
        float pulse;

        protected override void Build()
        {
            note = UIKit.Label(content, "", 30, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            note.rectTransform.Place(0.5f, 1, new Vector2(0, -398), new Vector2(800, 60), new Vector2(0.5f, 0.5f));
            take = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(380, 110), L.T("받기"), 42, OnButton);
            ((RectTransform)take.transform).Place(0.5f, 0, new Vector2(0, 10), new Vector2(380, 110), new Vector2(0.5f, 0));
        }

        void MakeCells(int n)
        {
            foreach (var c in cells) if (c != null) Destroy(c.rt.gameObject);
            cells = new Cell[n];
            float step = Mathf.Min(260, 800f / n), w = step - 20;
            for (int i = 0; i < n; i++)
            {
                var c = new Cell();
                c.rt = UIKit.Rect(content, "Day" + (i + 1)).Place(0.5f, 1, new Vector2((i - (n - 1) / 2f) * step, -200), new Vector2(w, 320), new Vector2(0.5f, 0.5f));
                c.glow = UIKit.KitImg(c.rt, "outline", new Vector2(w + 24, 344), "Glow");
                c.glow.rectTransform.Fill(-12, -12, -12, -12);
                c.glow.color = UIKit.Gold;
                c.board = UIKit.Row(c.rt, "Board");
                c.board.rectTransform.Fill();
                c.day = UIKit.Label(c.rt, "", 36, Color.white, TextAnchor.MiddleCenter, 3);
                c.day.rectTransform.Place(0.5f, 1, new Vector2(0, -42), new Vector2(w - 10, 50), new Vector2(0.5f, 0.5f));
                c.gem = UIKit.Img(c.rt, Art.Get("UI/icon_gem"), new Vector2(110, 110), "Gem");
                c.gem.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 4), new Vector2(110, 110), new Vector2(0.5f, 0.5f));
                c.amount = UIKit.Label(c.rt, "", 46, Color.white, TextAnchor.MiddleCenter, 3);
                c.amount.rectTransform.Place(0.5f, 0, new Vector2(0, 54), new Vector2(w - 10, 60), new Vector2(0.5f, 0.5f));
                c.check = UIKit.KitImg(c.rt, "i_check_w", new Vector2(150, 150), "Check");   // 흰색판(tools/kit_white.py)에 녹색
                c.check.color = new Color(0.5f, 0.88f, 0.38f);
                c.check.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 4), new Vector2(150, 150), new Vector2(0.5f, 0.5f));
                foreach (var g in c.rt.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                cells[i] = c;
            }
        }

        public static AttendPopup Show()
        {
            var p = HUD.I.Open<AttendPopup>();
            p.claimed = false;
            p.index = S.attendCount % GameManager.AttendDays;
            p.Refresh();
            return p;
        }

        public override void Refresh()
        {
            int n = GameManager.AttendDays;
            if (cells.Length != n) MakeCells(n);
            for (int i = 0; i < n; i++)
            {
                var c = cells[i];
                bool got = i < index || (i == index && claimed), today = i == index && !claimed;
                c.day.text = L.F("{0}일차", i + 1);
                c.amount.text = Num.F(GameManager.AttendGems(i));
                c.check.enabled = got;
                c.glow.enabled = today;
                var dim = got ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
                c.board.color = dim; c.gem.color = dim;
                c.day.color = today ? UIKit.Gold : Color.white;
                c.rt.localScale = Vector3.one;
            }
            note.text = claimed ? L.T("내일 또 받을 수 있어요!") : L.F("{0}일을 다 받으면 1일차부터 다시 시작합니다", n);
            UIKit.LabelOf(take).text = claimed ? L.T("확인") : L.T("받기");
        }

        void Update()
        {
            if (claimed || index >= cells.Length) return;
            pulse += Time.unscaledDeltaTime;
            cells[index].rt.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(pulse * 5f));
        }

        void OnButton()
        {
            if (claimed) { base.Close(); return; }
            Claim();
        }

        void Claim()
        {
            if (claimed) return;
            int i = GM.ClaimAttend();
            claimed = true;
            if (i >= 0)
            {
                index = i;
                AudioManager.Play(AudioManager.Sfx.Coin);
                H.Bump(true);   // 알림 글자는 칸을 가려서 안 띄움 (체크 + 위 다이아 숫자로 보임)
            }
            Refresh();
        }

        /// <summary>X · 뒤로 가기: 안 받았으면 받고 닫음</summary>
        public override void Close() { Claim(); base.Close(); }
    }

    /// <summary>던전 결과: 받기 · 광고 보고 x2 (Buff_Table dungeon_ad_x2 — 11-07 이름 x3 → x2)</summary>
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
            ad = UIKit.ColorBtn(content, new Color(0.95f, 0.6f, 0.15f), new Vector2(330, 110), L.T("광고 보고") + " x" + Tables.Buff("dungeon_ad_x2").mul.ToString("0.#"), 40, () =>
                AdService.Show("dungeon", () => Claim(Tables.Buff("dungeon_ad_x2").mul)));
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
