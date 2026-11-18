using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 11-18 1차 업데이트 (업데이트/1차 업데이트/261118.pptx 2번): 코스튬 · 보급품 · 카피친구 = [컬렉션] 하나로 묶고 안에서 탭으로 나눔.
    /// 세 창(SkinPopup · SupplyPopup · FriendPopup)은 같은 크기·같은 제목이라 탭을 누르면 같은 자리에서 내용만 바뀌어 보임.
    /// 탭마다 여는 조건은 예전 그대로 (코스튬 1-2 · 보급품 6-1 · 카피친구 = 친구 던전과 같이 6-1, Unlock 표)
    /// </summary>
    public class CollectionTabs
    {
        public const float Off = 100f;   // 탭 줄 높이만큼 창 내용이 아래로
        public const string Title = "컬렉션";
        static readonly (string name, string unlock)[] Tabs = { ("코스튬", "skin"), ("보급품", "supply"), ("카피친구", "friend") };

        readonly List<(Button b, Image lk, GameObject dot)> btns = new();
        readonly int cur;

        CollectionTabs(int cur) { this.cur = cur; }

        public static CollectionTabs Build(RectTransform content, int cur, Popup self)
        {
            var t = new CollectionTabs(cur);
            float tw = 280;
            for (int k = 0; k < Tabs.Length; k++)
            {
                int i = k;
                bool sel = i == cur;
                float th = sel ? 96 : 82;
                // 상점 탭과 같은 모양: 고른 탭 = 원래 나무색 · 높이 96 / 나머지 = 흐리게 · 높이 82
                var b = UIKit.ColorBtn(content, new Color(0.55f, 0.36f, 0.22f), new Vector2(tw, th), L.T(Tabs[i].name), sel ? 40 : 36, () => Go(i, self));
                b.name = "CollTab" + i;
                ((RectTransform)b.transform).Place(0.5f, 1, new Vector2((i - 1) * (tw + 12), sel ? -8 : -22), new Vector2(tw, th), new Vector2(0.5f, 1));
                if (!sel) { b.GetComponent<Image>().color = new Color(0.7f, 0.66f, 0.6f); var tl = UIKit.LabelOf(b); if (tl) tl.color = new Color(1, 1, 1, 0.8f); }
                var lk = UIKit.KitImg(b.transform, "lock", new Vector2(26, 37), "Lock");
                lk.rectTransform.Place(1, 0.5f, new Vector2(-22, 0), new Vector2(26, 37), new Vector2(0.5f, 0.5f));
                lk.raycastTarget = false;
                var dot = UIKit.KitImg(b.transform, "badge", new Vector2(34, 34), "Dot");
                dot.rectTransform.Place(1, 1, new Vector2(8, 8), new Vector2(34, 34), new Vector2(1, 1));
                dot.raycastTarget = false;
                t.btns.Add((b, lk, dot.gameObject));
            }
            t.Refresh();
            return t;
        }

        /// <summary>잠김 자물쇠 · 빨간 점(카피친구 레벨 업 가능)</summary>
        public void Refresh()
        {
            var gm = GameManager.I;
            for (int i = 0; i < btns.Count; i++)
            {
                bool open = gm.Unlocked(Tabs[i].unlock);
                btns[i].lk.gameObject.SetActive(!open);
                btns[i].dot.SetActive(open && i == 2 && gm.AnyFriendReady);
            }
        }

        static void Go(int i, Popup self)
        {
            var gm = GameManager.I;
            if (!gm.Unlocked(Tabs[i].unlock)) { HUD.I.Toast(gm.LockMsg(Tabs[i].unlock)); return; }
            Open(i, self);
        }

        /// <summary>그 탭의 창을 엶 (지금 창은 닫음) — 0 코스튬 · 1 보급품 · 2 카피친구</summary>
        public static void Open(int i, Popup from = null)
        {
            if (from) from.Close();
            Popup p = i switch { 1 => HUD.I.Open<SupplyPopup>(), 2 => HUD.I.Open<FriendPopup>(), _ => HUD.I.Open<SkinPopup>() };
            p.panel.localScale = from ? Vector3.one : p.panel.localScale;   // 탭을 바꿀 땐 창이 다시 커지는 효과 없이
        }
    }

    /// <summary>11-18 카피친구 (Friend.xlsx): 친구 던전에서 카드를 모아 레벨 업 → 보유 효과(공격력·공격속도·금전·보스 피해). 최대 Friend_Level 줄 수</summary>
    public class FriendPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1660);
        protected override string Title => CollectionTabs.Title;
        CollectionTabs tabs;
        Text total;
        class Cell { public FriendRow row; public RectTransform rt; public Image bg, icon, bar, barFill; public Text name, info, cards; }
        readonly List<Cell> cells = new();
        public IReadOnlyList<(int id, string name, string cards, bool ready)> Shown
        {
            get { var l = new List<(int, string, string, bool)>(); foreach (var c in cells) l.Add((c.row.id, c.name.text, c.cards.text, GM.FriendReady(c.row))); return l; }
        }

        public static Sprite Icon(FriendRow r) =>
            string.IsNullOrEmpty(r.icon) || Resources.Load<Texture2D>("Art/Friend/" + r.icon) == null ? Art.Get("Gen/capy_head") : Art.Get("Friend/" + r.icon);

        /// <summary>능력 이름 (Friend_List Stat 칸)</summary>
        public static string StatName(string st) => L.T(st switch { "Atk" => "공격력", "Spd" => "공격속도", "Gold" => "금전", "Boss" => "보스 피해", _ => st });

        protected override void Build()
        {
            tabs = CollectionTabs.Build(content, 2, this);
            var tb = UIKit.Row(content, "Total", true);
            tb.rectTransform.Place(0.5f, 1, new Vector2(0, -10 - CollectionTabs.Off), new Vector2(890, 110), new Vector2(0.5f, 1));
            total = UIKit.Label(tb.transform, "", 30, Color.white, TextAnchor.MiddleCenter, 2);
            total.rectTransform.Fill(10, 6, 10, 6);

            SkinPopup.MakeScroll(content, new Vector2(0, -135 - CollectionTabs.Off), new Vector2(900, 1300 - CollectionTabs.Off), out var inner);
            const int cols = 3; const float cw = 282, ch = 330, gap = 12;
            int n = Tables.Friends.Count;
            inner.sizeDelta = new Vector2(0, Mathf.CeilToInt(n / (float)cols) * (ch + gap) + 10);
            for (int i = 0; i < n; i++)
            {
                var r = Tables.Friends[i];
                int x = i % cols, y = i / cols;
                var c = new Cell { row = r };
                c.bg = UIKit.Row(inner, "Friend" + r.id);
                c.rt = c.bg.rectTransform;
                c.rt.Place(0.5f, 1, new Vector2(-(cols - 1) * (cw + gap) / 2 + x * (cw + gap), -10 - y * (ch + gap)), new Vector2(cw, ch), new Vector2(0.5f, 1));
                c.icon = UIKit.Img(c.bg.transform, Icon(r), new Vector2(150, 150), "Icon");
                c.icon.rectTransform.Place(0.5f, 1, new Vector2(0, -8), new Vector2(150, 150), new Vector2(0.5f, 1));
                c.icon.raycastTarget = false;
                c.name = UIKit.Fit(UIKit.Label(c.bg.transform, "", 30, Color.white, TextAnchor.MiddleCenter, 3), 18);
                c.name.rectTransform.Place(0.5f, 1, new Vector2(0, -178), new Vector2(cw - 16, 40), new Vector2(0.5f, 0.5f));
                c.info = UIKit.Fit(UIKit.Label(c.bg.transform, "", 25, UIKit.Cream, TextAnchor.MiddleCenter, 2), 16);
                c.info.rectTransform.Place(0.5f, 1, new Vector2(0, -218), new Vector2(cw - 16, 34), new Vector2(0.5f, 0.5f));
                // 카드 막대 (모은 카드 / 다음 레벨에 드는 카드)
                c.bar = UIKit.Plain(c.bg.transform, new Color(0.25f, 0.16f, 0.1f, 0.85f), "Bar");
                c.bar.rectTransform.Place(0.5f, 0, new Vector2(0, 26), new Vector2(cw - 40, 46), new Vector2(0.5f, 0));
                c.barFill = UIKit.Plain(c.bar.transform, new Color(0.55f, 0.85f, 1f), "Fill");
                c.barFill.rectTransform.anchorMin = new Vector2(0, 0); c.barFill.rectTransform.anchorMax = new Vector2(0, 1);
                c.barFill.rectTransform.pivot = new Vector2(0, 0.5f);
                c.barFill.rectTransform.offsetMin = new Vector2(3, 3); c.barFill.rectTransform.offsetMax = new Vector2(3, -3);
                c.cards = UIKit.Fit(UIKit.Label(c.bar.transform, "", 28, Color.white, TextAnchor.MiddleCenter, 3), 16);
                c.cards.rectTransform.Fill();
                var b = c.bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                int id = r.id;
                b.onClick.AddListener(() => Tap(id));
                cells.Add(c);
            }
        }

        void Tap(int id)
        {
            var r = GM.Friend(id);
            if (r == null) return;
            int lv = Stats.FriendLv(r);
            if (lv <= 0) { H.Toast(L.F("친구 던전 Lv.{0} 부터 만날 수 있어요", r.unlockLv)); return; }
            if (GM.FriendReady(r))
            {
                Msg(GM.FriendLevelUp(id));
                H.Toast(L.F("{0} Lv.{1}! {2} +{3}%", L.T(r.name), Stats.FriendLv(r), StatName(r.stat), Stats.FriendBonus(r).ToString("0.#")));
                var c = cells.Find(x => x.row.id == id);
                if (c != null) c.rt.localScale = Vector3.one * 1.08f;
                Refresh();
                return;
            }
            if (lv >= Tables.FriendMaxLv) { H.Toast(L.F("{0} 최대 레벨 · {1} +{2}%", L.T(r.name), StatName(r.stat), Stats.FriendBonus(r).ToString("0.#"))); return; }
            H.Toast(L.F("다음 레벨까지 카드 {0}/{1} · {2} +{3}% → +{4}%", Stats.FriendCards(r), Tables.FriendNeed(lv + 1), StatName(r.stat),
                Stats.FriendBonus(r).ToString("0.#"), Stats.FriendBonus(r, lv + 1).ToString("0.#")));
        }

        void Update()
        {
            foreach (var c in cells) if (c.rt.localScale.x > 1f) c.rt.localScale = Vector3.MoveTowards(c.rt.localScale, Vector3.one, Time.unscaledDeltaTime * 0.5f);
        }

        public override void Refresh()
        {
            tabs?.Refresh();
            int have = 0; foreach (var r in Tables.Friends) if (Stats.FriendLv(r) > 0) have++;
            total.text = L.F("보유 효과 합계 ({0}/{1})", have, Tables.Friends.Count) + "\n" +
                         L.F("공격력 +{0}% · 공격속도 +{1}% · 금전 +{2}% · 보스 +{3}%", Stats.FriendAtk.ToString("0.#"), Stats.FriendSpd.ToString("0.#"), Stats.FriendGold.ToString("0.#"), Stats.FriendBoss.ToString("0.#"));
            int max = Tables.FriendMaxLv;
            foreach (var c in cells)
            {
                var r = c.row;
                int lv = Stats.FriendLv(r), cards = Stats.FriendCards(r);
                bool own = lv > 0, ready = GM.FriendReady(r);
                c.icon.color = own ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.85f);
                c.bg.color = ready ? new Color(1f, 0.92f, 0.6f) : own ? Color.white : new Color(0.72f, 0.68f, 0.64f);
                c.name.text = own ? $"{L.T(r.name)} <color=#ffd34d>Lv.{lv}</color>" : L.T(r.name);
                c.info.text = own ? $"{StatName(r.stat)} +{Stats.FriendBonus(r):0.#}%" : $"{StatName(r.stat)} +{r.bonus:0.#}%/Lv";
                float k;
                if (!own) { c.cards.text = L.F("던전 Lv.{0}", r.unlockLv); k = 0; }
                else if (lv >= max) { c.cards.text = "MAX"; k = 1; }
                else
                {
                    int need = Tables.FriendNeed(lv + 1);
                    c.cards.text = ready ? L.T("레벨 업!") + $" {cards}/{need}" : $"{cards}/{need}";
                    k = Mathf.Clamp01(cards / (float)need);
                }
                c.barFill.color = ready || lv >= max ? new Color(0.55f, 0.85f, 0.4f) : new Color(0.55f, 0.8f, 0.95f);
                float w = ((RectTransform)c.bar.transform).sizeDelta.x - 6;
                c.barFill.rectTransform.sizeDelta = new Vector2(w * k, -6);
                c.barFill.gameObject.SetActive(k > 0);
            }
        }
    }

    /// <summary>11-18 친구 던전 입장 창 (기획 3~7번): 단계 고르기 ◀ ▶ (1 ~ 깬 단계 + 1) · 보스 3마리 · 받을 카드 · 오늘 입장(무료 2 + 광고 3)</summary>
    public class FriendDungeonPopup : Popup
    {
        protected override Vector2 Size => new(900, 1240);
        protected override string Title => "친구 던전";
        Text lvText, info, tickets, poolText;
        Button prev, next, go;
        readonly List<Image> bossImgs = new();
        readonly List<Image> poolImgs = new();
        public int ShownLv { get; private set; }
        public string GoText => go ? UIKit.LabelOf(go).text : "";

        protected override void Build()
        {
            lvText = UIKit.Label(content, "", 58, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            lvText.rectTransform.Place(0.5f, 1, new Vector2(0, -50), new Vector2(420, 80), new Vector2(0.5f, 0.5f));
            prev = UIKit.ColorBtn(content, new Color(0.55f, 0.36f, 0.22f), new Vector2(130, 100), "", 50, () => { GM.FriendSelect(GM.FriendSelLv - 1); Refresh(); });
            prev.name = "FriendPrev";
            ((RectTransform)prev.transform).Place(0.5f, 1, new Vector2(-300, -50), new Vector2(130, 100), new Vector2(0.5f, 0.5f));
            next = UIKit.ColorBtn(content, new Color(0.55f, 0.36f, 0.22f), new Vector2(130, 100), "", 50, () => { GM.FriendSelect(GM.FriendSelLv + 1); Refresh(); });
            next.name = "FriendNext";
            ((RectTransform)next.transform).Place(0.5f, 1, new Vector2(300, -50), new Vector2(130, 100), new Vector2(0.5f, 0.5f));
            // 글꼴(주아)에 ◀ ▶ 글자가 없어 UI 팩 재생 세모(i_play)를 씀 — 이전은 뒤집어서
            foreach (var (b, flip) in new[] { (prev, true), (next, false) })
            {
                var ic = UIKit.KitImg(b.transform, "i_play", new Vector2(50, 50), "Arrow");
                ic.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 4), new Vector2(50, 50), new Vector2(0.5f, 0.5f));
                ic.rectTransform.localScale = new Vector3(flip ? -1 : 1, 1, 1);
                ic.raycastTarget = false;
            }
            var hint = UIKit.Label(content, L.T("깬 단계는 왼쪽 버튼으로 다시 골라 도전할 수 있어요"), 26, new Color(0.45f, 0.32f, 0.22f), TextAnchor.MiddleCenter, 0);
            hint.rectTransform.Place(0.5f, 1, new Vector2(0, -112), new Vector2(780, 36), new Vector2(0.5f, 0.5f));

            // 보스 3마리 (차례로 나옴)
            var bb = UIKit.Row(content, "Bosses", true);
            bb.rectTransform.Place(0.5f, 1, new Vector2(0, -140), new Vector2(780, 250), new Vector2(0.5f, 1));
            int nb = Mathf.Clamp(Tables.Ci("FriendDgn_Bosses"), 1, 5);
            for (int i = 0; i < nb; i++)
            {
                var im = UIKit.Img(bb.transform, Art.Zombie("ogre")[0], new Vector2(190, 190), "Boss" + i);
                im.preserveAspect = true; im.raycastTarget = false;
                im.rectTransform.Place(0.5f, 0.5f, new Vector2((i - (nb - 1) * 0.5f) * 240, 6), new Vector2(190, 190), new Vector2(0.5f, 0.5f));
                bossImgs.Add(im);
            }
            info = UIKit.Label(content, "", 34, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            info.rectTransform.Place(0.5f, 1, new Vector2(0, -455), new Vector2(800, 120), new Vector2(0.5f, 0.5f));
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 받을 수 있는 카드 = 그 단계에서 나오는 친구들
            poolText = UIKit.Label(content, "", 32, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            poolText.rectTransform.Place(0.5f, 1, new Vector2(0, -545), new Vector2(800, 46), new Vector2(0.5f, 0.5f));
            var pb = UIKit.Row(content, "Pool");
            pb.rectTransform.Place(0.5f, 1, new Vector2(0, -575), new Vector2(800, 210), new Vector2(0.5f, 1));
            for (int i = 0; i < Tables.Friends.Count; i++)
            {
                var im = UIKit.Img(pb.transform, FriendPopup.Icon(Tables.Friends[i]), new Vector2(88, 88), "Pool" + i);
                im.raycastTarget = false;
                im.rectTransform.Place(0.5f, 0.5f, new Vector2((i % 8 - 3.5f) * 94, i < 8 ? 48 : -48), new Vector2(88, 88), new Vector2(0.5f, 0.5f));
                poolImgs.Add(im);
            }
            tickets = UIKit.Label(content, "", 34, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            tickets.rectTransform.Place(0.5f, 0, new Vector2(0, 190), new Vector2(800, 96), new Vector2(0.5f, 0.5f));
            go = UIKit.KitBtn(content, "btn_green", new Vector2(420, 120), L.T("입장"), 44, () =>
            {
                var e = GM.StartFriend();
                if (e != null) { Msg(e); return; }
                Close();
            }, "FriendGo");
            ((RectTransform)go.transform).Place(0.5f, 0, new Vector2(0, 20), new Vector2(420, 120), new Vector2(0.5f, 0));
        }

        public override void Refresh()
        {
            int lv = GM.FriendSelLv, top = GM.FriendTopLv;
            ShownLv = lv;
            lvText.text = $"Lv.{lv}" + (lv < top ? $" <size=30>({L.T("깬 단계")})</size>" : "");
            UIKit.SetOn(prev, lv > 1);
            UIKit.SetOn(next, lv < top);
            for (int i = 0; i < bossImgs.Count; i++) bossImgs[i].sprite = Art.Zombie(HUD.BossId(Tables.FriendDgnBoss(lv, i)))[0];
            int idx = Tables.FriendDgnStage(lv);
            info.text = L.F("{0}초 안에 보스 {1}마리를 모두 처치!", Tables.Ci("FriendDgn_Time"), bossImgs.Count) + "\n" +
                        L.T("보스 체력") + $" {Num.F(Tables.FriendDgnHp(lv))} · " + L.F("스테이지 {0} 수준", SaveData.StageName(idx));
            poolText.text = L.F("이기면 카피친구 카드 {0}장", Tables.FriendDgnCards(lv));
            for (int i = 0; i < poolImgs.Count; i++)
            {
                bool can = Tables.Friends[i].unlockLv <= lv;
                poolImgs[i].color = can ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
            }
            int t = GM.FriendTickets, ad = GM.FriendAdLeft;
            tickets.text = L.T("오늘 입장") + $" {t}/{Tables.Ci("FriendDgn_Tickets")} · " + L.T("광고") + $" {ad}/{Tables.Ci("FriendDgn_Ad_Daily")}" +
                           "\n<size=26>" + L.T("실패해도 사용한 입장은 돌아오지 않습니다") + "</size>";
            UIKit.LabelOf(go).text = t > 0 ? L.T("입장") : ad > 0 ? L.T("광고 보고 입장") : L.T("내일 다시");
            UIKit.SetOn(go, t > 0 || ad > 0);
        }
    }
}
