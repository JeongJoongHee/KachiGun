using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>스킨 (기획서 7.1): 사면 바로 입고, 가진 스킨의 능력치는 입지 않아도 전부 합산</summary>
    public class SkinPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1660);
        protected override string Title => "스킨";
        Text total;
        readonly List<(SkinRow row, Image bg, Image pic, Text name, Text price)> cells = new();

        protected override void Build()
        {
            var tb = UIKit.Row(content, "Total", true);
            tb.rectTransform.Place(0.5f, 1, new Vector2(0, -10), new Vector2(890, 110), new Vector2(0.5f, 1));
            total = UIKit.Label(tb.transform, "", 32, Color.white, TextAnchor.MiddleCenter, 2);
            total.rectTransform.Fill(10, 6, 10, 6);

            var scroll = MakeScroll(content, new Vector2(0, -135), new Vector2(900, 1300), out var inner);
            int cols = 4; float cw = 212, ch = 300;
            int n = Tables.Skins.Count;
            inner.sizeDelta = new Vector2(0, Mathf.CeilToInt(n / (float)cols) * (ch + 12) + 10);
            for (int i = 0; i < n; i++)
            {
                var r = Tables.Skins[i];
                int x = i % cols, y = i / cols;
                var bg = UIKit.Row(inner, "Skin" + r.id);
                bg.rectTransform.Place(0.5f, 1, new Vector2(-(cols - 1) * (cw + 10) / 2 + x * (cw + 10), -10 - y * (ch + 12)), new Vector2(cw, ch), new Vector2(0.5f, 1));
                var pic = UIKit.Img(bg.transform, Art.Char(Art.ArtSkin(r.id))[0], new Vector2(200, 200), "Pic");
                pic.rectTransform.Place(0.5f, 1, new Vector2(0, 0), new Vector2(200, 200), new Vector2(0.5f, 1));
                pic.raycastTarget = false;
                if (!Art.HasSkinArt(r.id))
                {
                    var wip = UIKit.Label(bg.transform, L.T("그림 준비 중"), 22, UIKit.Cream, TextAnchor.MiddleCenter, 2);
                    wip.rectTransform.Place(0.5f, 1, new Vector2(0, -24), new Vector2(cw, 30), new Vector2(0.5f, 0.5f));
                }
                var nm = UIKit.Fit(UIKit.Label(bg.transform, L.T(r.name), 26, Color.white, TextAnchor.MiddleCenter, 2), 16);
                nm.rectTransform.Place(0.5f, 0, new Vector2(0, 74), new Vector2(cw, 34), new Vector2(0.5f, 0.5f));
                var pr = UIKit.Label(bg.transform, "", 28, UIKit.Sky, TextAnchor.MiddleCenter, 2);
                pr.rectTransform.Place(0.5f, 0, new Vector2(0, 32), new Vector2(cw, 34), new Vector2(0.5f, 0.5f));
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                int id = r.id;
                b.onClick.AddListener(() => Tap(id));
                cells.Add((r, bg, pic, nm, pr));
            }
        }

        void Tap(int id)
        {
            var r = Tables.Skins.Find(x => x.id == id);
            if (S.skins.Contains(id)) { Msg(GM.BuySkin(id)); Refresh(); return; }
            ConfirmPopup.Ask($"<size=46>{L.T(r.name)}</size>\n\n" + L.F("다이아 {0} 으로 살까요?", r.price) + "\n\n" + L.T("보유 효과") + "\n" + L.F("공격력 +{0}% · 공격속도 +{1}% · 돈 +{2}%", r.atk, r.spd, r.gold),
                () => { Msg(GM.BuySkin(id)); H.Open<SkinPopup>(); }, "스킨 구매");
        }

        public override void Refresh()
        {
            total.text = L.T("보유 효과 합계") + "\n" + L.F("공격력 +{0}% · 공격속도 +{1}% · 돈 획득량 +{2}%", Stats.SkinAtk, Stats.SkinSpd, Stats.SkinGold);
            foreach (var c in cells)
            {
                bool own = S.skins.Contains(c.row.id), on = S.skin == c.row.id;
                c.price.text = on ? L.T("입는 중") : own ? L.T("보유") : L.T("다이아") + " " + c.row.price;
                c.price.color = on ? UIKit.Gold : own ? UIKit.Green : UIKit.Sky;
                c.bg.color = on ? new Color(1f, 0.86f, 0.45f) : own ? Color.white : new Color(0.78f, 0.74f, 0.7f);
                c.pic.color = own ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            }
        }

        public static ScrollRect MakeScroll(RectTransform parent, Vector2 pos, Vector2 size, out RectTransform inner)
        {
            var view = UIKit.Rect(parent, "Scroll").Place(0.5f, 1, pos, size, new Vector2(0.5f, 1));
            var img = view.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.01f);
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            inner = UIKit.Rect(view, "Inner");
            inner.anchorMin = new Vector2(0, 1); inner.anchorMax = new Vector2(1, 1); inner.pivot = new Vector2(0.5f, 1);
            inner.anchoredPosition = Vector2.zero; inner.sizeDelta = new Vector2(0, size.y);
            sr.content = inner; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;
            return sr;
        }
    }

    /// <summary>상점 (기획서 11장) = Shop_Products 표. 광고 버프 · 광고 무료 다이아(하루 N번) · 다이아로 사는 골드 상자는 동작, 현금 결제는 [임시] "준비 중"</summary>
    public class ShopPopup : Popup
    {
        protected override Vector2 Size => new(1000, 1600);
        protected override string Title => "상점";
        Button adGemBtn;

        protected override void Build()
        {
            var items = new List<(string id, string name, string desc, string price, int kind, System.Action act)>();   // kind 0 = 광고 · 1 = 다이아 · 2 = 결제
            foreach (var r in Tables.Shop)
            {
                var row = r;
                System.Action act;
                string price;
                int kind;
                if (r.id == "buff_atk") { act = () => Msg(GM.Buff(true)); price = L.T("광고"); kind = 0; }
                else if (r.id == "buff_gold") { act = () => Msg(GM.Buff(false)); price = L.T("광고"); kind = 0; }
                else if (r.id == "free_gems_ad") { act = () => { Msg(GM.AdGems()); Refresh(); }; price = L.T("광고"); kind = 0; }
                else if (r.priceType == "Gems")
                {
                    price = L.T("다이아") + " " + Num.F(GameManager.Digits(r.priceValue));
                    kind = 1;
                    act = () => ConfirmPopup.Ask($"<size=46>{L.T(row.name)}</size>\n\n{L.T(row.desc)}\n\n" + L.F("{0} 으로 살까요?", price), () => Msg(GM.BuyShop(row)), "구매");
                }
                else { price = r.priceType == "Ad" ? L.T("광고") : r.priceValue; kind = r.priceType == "Ad" ? 0 : 2; act = () => Iap.Buy(row.id); }
                items.Add((r.id, L.T(r.name), L.T(r.desc), price, kind, act));
            }
            var scroll = SkinPopup.MakeScroll(content, new Vector2(0, -10), new Vector2(900, 1430), out var inner);
            inner.sizeDelta = new Vector2(0, items.Count * 170 + 10);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var rb = UIKit.Row(inner, "Item" + i);
                rb.rectTransform.Place(0.5f, 1, new Vector2(0, -10 - i * 170), new Vector2(880, 155), new Vector2(0.5f, 1));
                var nm = UIKit.Fit(UIKit.Label(rb.transform, it.name, 38, Color.white, TextAnchor.MiddleLeft, 3), 24);
                nm.rectTransform.Place(0, 0.5f, new Vector2(30, 26), new Vector2(560, 50), new Vector2(0, 0.5f));
                var ds = UIKit.Fit(UIKit.Label(rb.transform, it.desc, 28, UIKit.Cream, TextAnchor.MiddleLeft, 2), 18);
                ds.rectTransform.Place(0, 0.5f, new Vector2(30, -30), new Vector2(560, 40), new Vector2(0, 0.5f));
                bool ad = it.kind == 0, gem = it.kind == 1;
                var b = UIKit.ColorBtn(rb.transform, ad ? new Color(0.95f, 0.6f, 0.15f) : gem ? new Color(0.3f, 0.55f, 0.95f) : new Color(0.45f, 0.7f, 0.3f), new Vector2(220, 100), it.price, gem ? 32 : 38, it.act);
                ((RectTransform)b.transform).Place(1, 0.5f, new Vector2(-24, 0), new Vector2(220, 100), new Vector2(1, 0.5f));
                if (it.id == "free_gems_ad") adGemBtn = b;
            }
        }

        /// <summary>광고 무료 다이아: 남은 횟수 "광고 2/3"</summary>
        public override void Refresh()
        {
            if (!adGemBtn) return;
            int left = GM.AdGemsLeft;
            UIKit.LabelOf(adGemBtn).text = $"{L.T("광고")} {left}/{(int)Tables.C("AdGems_Daily", 3)}";
            UIKit.SetOn(adGemBtn, left > 0);
        }
    }

    /// <summary>
    /// 보급품 (Supply_Collection 25종): 챕터를 넘기면 하나씩 열리고, 다이아로 사면 보유 효과(공격·속도·돈·보스 피해)가 계속 더해짐.
    /// </summary>
    public class SupplyPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1660);
        protected override string Title => "보급품";
        Text total;
        readonly List<(SupplyRow row, Image bg, Image icon, Text name, Text info, Button buy)> rows = new();

        /// <summary>보급품 등급 색 (일반 → 태초)</summary>
        public static Color GradeColor(string g) => g switch
        {
            "일반" => new Color(0.75f, 0.75f, 0.75f), "고급" => new Color(0.5f, 0.85f, 0.4f), "희귀" => new Color(0.4f, 0.65f, 1f),
            "영웅" => new Color(0.75f, 0.5f, 0.95f), "전설" => new Color(1f, 0.65f, 0.25f), "신화" => new Color(1f, 0.4f, 0.4f),
            "초월" => new Color(0.35f, 0.95f, 0.9f), "불멸" => new Color(1f, 0.55f, 0.85f), _ => new Color(1f, 0.85f, 0.3f),
        };

        protected override void Build()
        {
            var tb = UIKit.Row(content, "Total", true);
            tb.rectTransform.Place(0.5f, 1, new Vector2(0, -10), new Vector2(890, 110), new Vector2(0.5f, 1));
            total = UIKit.Label(tb.transform, "", 30, Color.white, TextAnchor.MiddleCenter, 2);
            total.rectTransform.Fill(10, 6, 10, 6);

            MakeList(out var inner);
            int n = Tables.Supplies.Count;
            const float rh = 150;
            inner.sizeDelta = new Vector2(0, n * (rh + 10) + 10);
            for (int i = 0; i < n; i++)
            {
                var r = Tables.Supplies[i];
                var bg = UIKit.Row(inner, "Supply" + r.id);
                bg.rectTransform.Place(0.5f, 1, new Vector2(0, -10 - i * (rh + 10)), new Vector2(880, rh), new Vector2(0.5f, 1));
                var art = Icon(r);
                var ic = art ? UIKit.Img(bg.transform, art, new Vector2(110, 110), "Icon") : UIKit.KitImg(bg.transform, "i_chest_w", new Vector2(96, 86), "Icon");
                if (art) ic.preserveAspect = true;
                ic.rectTransform.Place(0, 0.5f, new Vector2(art ? 22 : 30, 0), art ? new Vector2(110, 110) : new Vector2(96, 86), new Vector2(0, 0.5f));
                ic.raycastTarget = false;
                var nm = UIKit.Label(bg.transform, "", 34, Color.white, TextAnchor.MiddleLeft, 3);
                nm.rectTransform.Place(0, 0.5f, new Vector2(146, 28), new Vector2(470, 46), new Vector2(0, 0.5f));
                var inf = UIKit.Label(bg.transform, "", 26, UIKit.Cream, TextAnchor.MiddleLeft, 2);
                inf.rectTransform.Place(0, 0.5f, new Vector2(146, -30), new Vector2(470, 40), new Vector2(0, 0.5f));
                inf.horizontalOverflow = HorizontalWrapMode.Wrap; inf.verticalOverflow = VerticalWrapMode.Truncate;   // 칸 안에 맞춰 글자 줄임
                inf.resizeTextForBestFit = true; inf.resizeTextMinSize = 16; inf.resizeTextMaxSize = 26;
                int id = r.id;
                var b = UIKit.ColorBtn(bg.transform, new Color(0.3f, 0.55f, 0.95f), new Vector2(210, 96), "", 32, () => Tap(id));
                ((RectTransform)b.transform).Place(1, 0.5f, new Vector2(-20, 0), new Vector2(210, 96), new Vector2(1, 0.5f));
                rows.Add((r, bg, ic, nm, inf, b));
            }
        }

        /// <summary>보급품 그림 = Supply 표 Icon 칸 → Art/Supply/그 이름 (tools/supply_icons.py 가 만듦). 비었거나 없으면 null(상자 아이콘)</summary>
        public static Sprite Icon(SupplyRow r) =>
            string.IsNullOrEmpty(r.icon) || Resources.Load<Texture2D>("Art/Supply/" + r.icon) == null ? null : Art.Get("Supply/" + r.icon);

        void MakeList(out RectTransform inner) => SkinPopup.MakeScroll(content, new Vector2(0, -135), new Vector2(900, 1300), out inner);

        void Tap(int id)
        {
            var r = Tables.Supplies.Find(x => x.id == id);
            if (r == null) return;
            if (S.supplies.Contains(id)) { H.Toast(L.T("이미 가진 보급품입니다")); return; }
            if (GM.BestChapter < r.unlockChapter) { H.Toast(L.F("챕터 {0} 에서 열립니다", r.unlockChapter)); return; }
            ConfirmPopup.Ask($"<size=46>{L.T(r.name)}</size>\n<color=#{ColorUtility.ToHtmlStringRGB(GradeColor(r.grade) * 0.75f)}>{L.T(r.grade)}</color>\n\n" + L.F("다이아 {0} 으로 살까요?", r.cost.ToString("0")) + "\n\n" + L.T("보유 효과") + $"\n{Effect(r)}",
                () => { Msg(GM.BuySupply(id)); Refresh(); }, "보급품 구매");
        }

        static string Effect(SupplyRow r) => L.F("공격 +{0}% · 속도 +{1}% · 돈 +{2}% · 보스 +{3}%", r.atk.ToString("0.#"), r.spd.ToString("0.#"), r.gold.ToString("0.#"), r.boss.ToString("0.#"));

        public override void Refresh()
        {
            total.text = L.F("보유 효과 합계 ({0}/{1})", S.supplies.Count, Tables.Supplies.Count) + "\n" +
                         L.F("공격력 +{0}% · 공격속도 +{1}% · 돈 +{2}% · 보스 +{3}%", Stats.SupplyAtk.ToString("0.#"), Stats.SupplySpd.ToString("0.#"), Stats.SupplyGold.ToString("0.#"), Stats.SupplyBoss.ToString("0.#"));
            foreach (var c in rows)
            {
                bool own = S.supplies.Contains(c.row.id), open = GM.BestChapter >= c.row.unlockChapter;
                var gc = GradeColor(c.row.grade);
                c.name.text = $"{L.T(c.row.name)}  <size=26><color=#{ColorUtility.ToHtmlStringRGB(gc)}>{L.T(c.row.grade)}</color></size>";
                c.info.text = Effect(c.row);
                bool art = c.icon.sprite && c.icon.sprite.name.StartsWith("Supply/");
                c.icon.color = art ? (open ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.85f)) : open ? gc : new Color(0.45f, 0.42f, 0.4f);   // 그림이면 잠김만 어둡게
                c.name.color = open ? Color.white : new Color(0.85f, 0.82f, 0.78f);
                c.bg.color = own ? new Color(1f, 0.92f, 0.6f) : open ? Color.white : new Color(0.72f, 0.68f, 0.64f);
                UIKit.LabelOf(c.buy).text = own ? L.T("보유") : open ? L.T("다이아") + " " + c.row.cost.ToString("0") : L.F("챕터 {0}", c.row.unlockChapter);
                UIKit.SetOn(c.buy, !own && open);
            }
        }
    }

    /// <summary>미션 (기획서 10.1): 단계별 반복 미션 → 단계마다 골드·다이아 번갈아 한 종류 (10-24, 합은 다이아 30% + 골드 70%). 패스 상품은 넣지 않음(10-20 결정)</summary>
    public class MissionPopup : Popup
    {
        protected override Vector2 Size => new(1000, 1320);
        protected override string Title => "미션";
        readonly List<(Text name, Text prog, Image fill, Button claim)> rows = new();

        protected override void Build()
        {
            for (int i = 0; i < Tables.Missions.Count; i++)
            {
                int idx = i;
                var rb = UIKit.Row(content, "Row" + i);
                rb.rectTransform.Place(0.5f, 1, new Vector2(0, -6 - i * 186), new Vector2(890, 170), new Vector2(0.5f, 1));
                var nm = UIKit.Label(rb.transform, "", 36, Color.white, TextAnchor.MiddleLeft, 3);
                nm.rectTransform.Place(0, 0.5f, new Vector2(30, 34), new Vector2(560, 50), new Vector2(0, 0.5f));
                var barBg = UIKit.KitImg(rb.transform, "bar_track", new Vector2(560, 44), "Bar");
                barBg.rectTransform.Place(0, 0.5f, new Vector2(30, -32), new Vector2(560, 44), new Vector2(0, 0.5f));
                var fill = UIKit.KitImg(barBg.transform, "bar_green", new Vector2(10, 10), "Fill");
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.3f, 1);
                fill.rectTransform.offsetMin = new Vector2(0, 0); fill.rectTransform.offsetMax = new Vector2(0, 0);
                var pr = UIKit.Label(barBg.transform, "", 28, Color.white, TextAnchor.MiddleCenter, 2);
                pr.rectTransform.Fill();
                var b = UIKit.ColorBtn(rb.transform, new Color(0.3f, 0.55f, 0.95f), new Vector2(230, 110), "", 32, () => { Msg(GM.ClaimMission(idx)); Refresh(); });
                ((RectTransform)b.transform).Place(1, 0.5f, new Vector2(-24, 0), new Vector2(230, 110), new Vector2(1, 0.5f));
                rows.Add((nm, pr, fill, b));
            }
        }

        public override void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var m = Tables.Missions[i];
                long v = GM.MissionValue(i), t = GM.MissionTarget(i);
                if (GM.MissionDone(i))
                {
                    rows[i].name.text = L.T(m.name);
                    rows[i].prog.text = L.T("모든 단계 완료");
                    rows[i].fill.rectTransform.anchorMax = Vector2.one;
                    rows[i].fill.enabled = true;
                    UIKit.LabelOf(rows[i].claim).text = L.T("완료");
                    UIKit.SetOn(rows[i].claim, false);
                    continue;
                }
                rows[i].name.text = $"{L.T(m.name)} {Num.F(t)}";
                rows[i].prog.text = $"{Num.F(System.Math.Min(v, t))} / {Num.F(t)}";
                float r01 = Mathf.Clamp01(v / (float)t);
                rows[i].fill.rectTransform.anchorMax = new Vector2(Mathf.Max(r01, 0.04f), 1);
                rows[i].fill.enabled = r01 > 0;
                bool ready = v >= t;
                UIKit.LabelOf(rows[i].claim).text = $"{L.T("받기")}\n<size=24>{GM.MissionRewardText(i)}</size>";
                UIKit.SetOn(rows[i].claim, ready);
            }
        }

        public override void Tick() => Refresh();
    }

    /// <summary>설정 (기획서 2.2): 배경음·효과음·진동 · 저장/불러오기 · 언어(한국어/English) · [테스트] 재화</summary>
    public class SettingsPopup : Popup
    {
        // 출시 빌드에는 [테스트용] 버튼이 없어 창이 짧음 (10-24 출시용 정리 — 테스트 버튼은 에디터에서만)
#if UNITY_EDITOR
        protected override Vector2 Size => new(1000, 1500);
#else
        protected override Vector2 Size => new(1000, 1070);
#endif
        protected override string Title => "설정";
        Button bgm, sfx, vibe, lang;
        readonly Image[] icons = new Image[4];   // 배경음·효과음·진동·언어 (10-24: 3칸이라 언어 줄에서 넘쳐 설정 창이 깨졌음)

        protected override void Build()
        {
            bgm = Toggle(L.T("배경음"), 0, () => { S.bgm = !S.bgm; Refresh(); });
            sfx = Toggle(L.T("효과음"), 1, () => { S.sfx = !S.sfx; Refresh(); });
            vibe = Toggle(L.T("진동"), 2, () => { S.vibe = !S.vibe; Refresh(); });
            // 언어: 누르면 바로 바뀌고 화면을 다시 만듦 (던전 중에는 막음)
            lang = Toggle("언어 · Language", 3, () =>
            {
                if (GM.mode != GameManager.Mode.Stage) { H.Toast(L.T("던전에서는 바꿀 수 없습니다")); return; }
                L.Set(L.IsEn ? L.Ko : L.En);
            });

            Row(L.T("저장"), 4, new Color(0.45f, 0.7f, 0.3f), () => { S.Save(); H.Toast(L.T("저장했습니다 (기기 안)")); });
            Row(L.T("불러오기 · 구매 복원 · 문의"), 5, new Color(0.42f, 0.4f, 0.38f), () => H.Toast(L.T("클라우드 저장·결제 복원·문의는 준비 중입니다")));

#if UNITY_EDITOR
            var t = UIKit.Label(content, "[테스트용 — 에디터에서만 보임]", 30, UIKit.Red, TextAnchor.MiddleCenter, 2);
            t.rectTransform.Place(0.5f, 1, new Vector2(0, -940), new Vector2(880, 40), new Vector2(0.5f, 0.5f));
            Small("다이아 +1000", 0, () => { S.gems += 1000; GM.Notify(); });
            Small("골드 x100", 1, () => { S.gold = System.Math.Max(S.gold, 100) * 100; GM.Notify(); });
            Small("다음 스테이지", 2, () => { if (GM.mode == GameManager.Mode.Stage) GM.AdvanceStage(); });
            Small("처음부터", 3, () => ConfirmPopup.Ask("세이브를 지우고 처음부터 시작할까요?", () =>
            {
                SaveData.ResetAll();
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }));
#endif
        }

        Button Toggle(string name, int i, System.Action a)
        {
            var rb = UIKit.Row(content, name);
            rb.rectTransform.Place(0.5f, 1, new Vector2(0, -10 - i * 150), new Vector2(890, 130), new Vector2(0.5f, 1));
            // 왼쪽 아이콘 (UI 팩: 음악 · 소리 · 하트)
            string ico = i == 0 ? "music" : i == 1 ? "sound" : i == 2 ? "i_heart" : "i_gear_w";
            var ic = UIKit.KitImg(rb.transform, ico, new Vector2(70, 64), "Icon");
            ic.rectTransform.Place(0, 0.5f, new Vector2(40, 4), new Vector2(70, 64), new Vector2(0, 0.5f));
            icons[i] = ic;
            var nm = UIKit.Label(rb.transform, name, 40, Color.white, TextAnchor.MiddleLeft, 3);
            nm.rectTransform.Place(0, 0.5f, new Vector2(130, 0), new Vector2(400, 60), new Vector2(0, 0.5f));
            var b = UIKit.ColorBtn(rb.transform, UIKit.Green, new Vector2(200, 90), L.T("켜짐"), 36, a);
            ((RectTransform)b.transform).Place(1, 0.5f, new Vector2(-30, 0), new Vector2(200, 90), new Vector2(1, 0.5f));
            return b;
        }

        void Row(string name, int i, Color c, System.Action a)
        {
            var b = UIKit.ColorBtn(content, c, new Vector2(890, 120), name, 38, a);
            ((RectTransform)b.transform).Place(0.5f, 1, new Vector2(0, -10 - i * 150), new Vector2(890, 120), new Vector2(0.5f, 1));
        }

        void Small(string name, int i, System.Action a)
        {
            var b = UIKit.ColorBtn(content, new Color(0.5f, 0.3f, 0.25f), new Vector2(430, 110), name, 34, a);
            ((RectTransform)b.transform).Place(0.5f, 1, new Vector2(i % 2 == 0 ? -225 : 225, -990 - (i / 2) * 130), new Vector2(430, 110), new Vector2(0.5f, 1));
        }

        public override void Refresh()
        {
            Set(bgm, S.bgm); Set(sfx, S.sfx); Set(vibe, S.vibe);
            UIKit.LabelOf(lang).text = L.IsEn ? "English" : "한국어";
            UIKit.SetOn(lang, true);
            if (icons[0]) icons[0].sprite = Art.Kit(S.bgm ? "music" : "music_off");
            if (icons[1]) icons[1].sprite = Art.Kit(S.sfx ? "sound" : "sound_off");
            if (icons[2]) icons[2].color = S.vibe ? Color.white : UIKit.Off;
        }

        static void Set(Button b, bool on)
        {
            UIKit.LabelOf(b).text = L.T(on ? "켜짐" : "꺼짐");
            UIKit.SetOn(b, on);
        }
    }
}
