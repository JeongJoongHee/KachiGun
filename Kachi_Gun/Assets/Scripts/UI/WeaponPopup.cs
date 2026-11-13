using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 총알 관리 창 (기획서 6장 · 11-05 추가 요청/UI 수정.png 모양): 장착 카드 3장(리더·동료1·동료2 — 등급 · +강화 · 공격력 · 연사) + 5x4 인벤토리
    /// + 아래 버튼 [일괄 합성] · [다이아 100 뽑기] · [분해].
    /// 같은 등급 총알 두 개를 끌어다 겹치면 한 단계 위 등급의 랜덤 종류로 합성 (11-04: 종류가 다르면 안 됨 → 11-14 같은 등급이면 종류가 달라도 됨, Merge_Same_Grade). 장착 중인 총알은 해제한 뒤 합성.
    /// 장착 카드를 누르면 정보 창(WeaponInfoPopup: [해제]), 인벤토리 총알을 누르면 정보 창([분해]).
    /// 11-07 사용자 "장착 버튼은 제거, 드래그앤드롭으로만 착용": 인벤토리 총알을 카드(어디든)로 끌어다 놓으면 장착 — 정보 창의 [장착] 없음.
    /// 11-05 밤 사용자 "+0 숫자 제거 · 슬롯 강화 기능 제거" → 카드의 +N 없음 · 강화 없음 (Stats.SlotEnchantOn = false).
    /// [일괄 합성] = 같은 등급 두 개씩 한 단계만(GM.MergeAll) · [분해] = 누르면 여러 개 고르기 → 다시 누르면 고른 것 한 번에 분해(GM.SalvageMany).
    /// </summary>
    public class WeaponPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1590);
        protected override string Title => "총알 관리";
        protected override bool PillTitle => true;
        readonly WeaponCell[] slots = new WeaponCell[SaveData.SlotCount];
        readonly Text[] slotAtk = new Text[SaveData.SlotCount], slotSpd = new Text[SaveData.SlotCount];
        readonly WeaponCell[] inv = new WeaponCell[SaveData.InvSize];
        Text hint, gachaLabel, mergeLabel, salvageLabel;
        Button mergeBtn, gachaBtn, salvageBtn;
        public RectTransform ghost;

        /// <summary>[분해] 여러 개 고르기 중</summary>
        public bool Picking { get; private set; }
        public readonly HashSet<int> picked = new();

        /// <summary>등급 색 = Weapon_Grade_Growth 의 Grade_Color_Hex</summary>
        public static Color GradeColor(int g) => Tables.GradeColor(g);

        /// <summary>종류 아이콘 = Bullet_Spec 의 Icon_Path</summary>
        public static Sprite TypeIcon(string type)
        {
            var b = Tables.Bullet(type);
            return Art.Get(string.IsNullOrEmpty(b.icon) ? "UI/icon_pistol" : b.icon);
        }

        protected override void Build()
        {
            // ---- 장착 카드 3장 (11-05 밤: 동료1 | 리더 | 동료2 — 리더가 가운데 · 조금 더 크게, 게임 화면의 위·가운데·아래와 같은 순서) ----
            string[] names = { L.T("리더"), L.T("동료 1"), L.T("동료 2") };
            float[] cardX = { 0, -306, 306 };
            for (int i = 0; i < 3; i++)
            {
                bool lead = i == 0;
                float w = lead ? 310 : 276, h = lead ? 438 : 392, ts = lead ? 246 : 210, k = lead ? 1.08f : 0.94f;
                float top = lead ? -4 : -4 - (438 - 392) / 2f;                  // 동료 카드는 리더 카드와 가운데 높이를 맞춤
                var card = UIKit.KitImg(content, "panel_tan", new Vector2(w, h), "Card" + i);
                card.rectTransform.Place(0.5f, 1, new Vector2(cardX[i], top), new Vector2(w, h), new Vector2(0.5f, 1));
                if (lead)
                {
                    // 리더 카드 = 금색 테두리
                    var gl = UIKit.KitImg(card.transform, "outline", new Vector2(w + 18, h + 18), "LeaderGlow");
                    gl.rectTransform.Fill(-9, -9, -9, -9);
                    gl.color = UIKit.Gold;
                    gl.raycastTarget = false;
                }
                var c = WeaponCell.Make(card.transform, this, true, i, ts);
                c.rt.Place(0.5f, 1, new Vector2(0, -16), new Vector2(ts, ts), new Vector2(0.5f, 1));
                float ny = -16 - ts - 36, sy = ny - 64;
                var t = UIKit.Label(card.transform, names[i], (int)(38 * k), UIKit.Ink, TextAnchor.MiddleCenter, 0);
                t.rectTransform.Place(0.5f, 1, new Vector2(0, ny), new Vector2(w - 20, 50), new Vector2(0.5f, 0.5f));
                // 공격력 · 연사 (카드 폭에 맞춰 왼쪽 반 · 오른쪽 반)
                float q = w / 2f;
                var sw = UIKit.Img(card.transform, Art.Get("UI/icon_swords"), new Vector2(46, 42) * k, "AtkIcon");
                sw.rectTransform.Place(0.5f, 1, new Vector2(-q + 34 * k, sy), new Vector2(46, 42) * k, new Vector2(0.5f, 0.5f));
                slotAtk[i] = UIKit.Label(card.transform, "", (int)(34 * k), UIKit.Ink, TextAnchor.MiddleLeft, 0);
                slotAtk[i].rectTransform.Place(0.5f, 1, new Vector2(-q + 60 * k, sy), new Vector2(q - 60 * k - 4, 46), new Vector2(0, 0.5f));
                UIKit.Fit(slotAtk[i], 18);
                var bo = UIKit.KitImg(card.transform, "i_bolt", new Vector2(34, 42) * k, "SpdIcon");
                bo.rectTransform.Place(0.5f, 1, new Vector2(18 * k, sy), new Vector2(34, 42) * k, new Vector2(0.5f, 0.5f));
                slotSpd[i] = UIKit.Label(card.transform, "", (int)(34 * k), UIKit.Ink, TextAnchor.MiddleLeft, 0);
                slotSpd[i].rectTransform.Place(0.5f, 1, new Vector2(38 * k, sy), new Vector2(q - 38 * k - 10, 46), new Vector2(0, 0.5f));
                UIKit.Fit(slotSpd[i], 18);
                slots[i] = c;
                // 카드 아무 데나 눌러도 그 칸 정보 · 11-07 카드 어디에 끌어다 놓아도 그 칸에 장착
                card.gameObject.AddComponent<CardDrop>().cell = c;
                int si = i;
                var cb = card.gameObject.AddComponent<Button>();
                cb.transition = Selectable.Transition.None;
                cb.onClick.AddListener(() => { AudioManager.Play(AudioManager.Sfx.UiClick); Select(slots[si]); });
            }

            // ---- 인벤토리 5x4 (11-05 밤: 판에 꽉 차게 — 칸 172 · 사이 6 · 판 안쪽 여백 10) ----
            const float cs = 172, gap = 6, pad = 10, gridTop = -460;
            float gw = 5 * cs + 4 * gap + 2 * pad, gh = 4 * cs + 3 * gap + 2 * pad;
            var gridBg = UIKit.Box(content, new Color(0.95f, 0.89f, 0.76f), "GridBg");
            gridBg.rectTransform.Place(0.5f, 1, new Vector2(0, gridTop), new Vector2(gw, gh), new Vector2(0.5f, 1));
            gridBg.raycastTarget = false;
            for (int i = 0; i < SaveData.InvSize; i++)
            {
                int r = i / 5, c = i % 5;
                var cell = WeaponCell.Make(content, this, false, i, cs);
                cell.rt.Place(0.5f, 1, new Vector2((c - 2) * (cs + gap), gridTop - pad - r * (cs + gap)), new Vector2(cs, cs), new Vector2(0.5f, 1));
                inv[i] = cell;
            }

            hint = UIKit.Label(content, "", 34, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            hint.rectTransform.Place(0.5f, 1, new Vector2(0, -1214), new Vector2(900, 46), new Vector2(0.5f, 0.5f));   // 11-14 아래 뽑기 안내 줄 자리만큼 위로 (전 -1236)
            UIKit.Fit(hint, 22);
            // 11-14 사용자 "뽑기는 무조건 D등급만 나온다는 문구를 무기 뽑기창에": 판 아래 · 뽑기 버튼 바로 위
            var gn = UIKit.Fit(UIKit.Label(content, L.T("※ 뽑기는 무조건 D 등급 총알만 나옵니다"), 28, new Color(0.72f, 0.27f, 0.2f), TextAnchor.MiddleCenter, 0), 18);
            gn.name = "GachaNote";
            gn.rectTransform.Place(0.5f, 1, new Vector2(0, -1258), new Vector2(900, 36), new Vector2(0.5f, 0.5f));
            gn.raycastTarget = false;

            // ---- 아래 버튼 3개 ----
            mergeBtn = UIKit.KitBtn(content, "btn_green", new Vector2(250, 128), L.T("일괄 합성"), 40, OnMergeBtn, "MergeAll");
            ((RectTransform)mergeBtn.transform).Place(0.5f, 0, new Vector2(-336, 14), new Vector2(250, 128), new Vector2(0.5f, 0));
            mergeLabel = UIKit.Fit(UIKit.LabelOf(mergeBtn), 22);

            gachaBtn = UIKit.KitBtn(content, "btn_green", new Vector2(390, 140), "", 50, () =>
            {
                string e = GM.Gacha();
                if (e != null) Msg(e); else { Refresh(); H.Toast(L.T("D 등급 총알 획득!")); }
            }, "Gacha");
            ((RectTransform)gachaBtn.transform).Place(0.5f, 0, new Vector2(0, 8), new Vector2(390, 140), new Vector2(0.5f, 0));
            gachaLabel = UIKit.Label(gachaBtn.transform, "", 50);   // KitBtn 은 글자가 비면 Label 을 안 만듦
            gachaLabel.name = "Label";
            gachaLabel.rectTransform.Fill(96, 8, 16, 4);
            UIKit.Fit(gachaLabel, 24);
            var gi = UIKit.Img(gachaBtn.transform, Art.Get("UI/icon_gem"), new Vector2(74, 74), "Gem");
            gi.rectTransform.Place(0, 0.5f, new Vector2(34, 4), new Vector2(74, 74), new Vector2(0, 0.5f));
            gi.raycastTarget = false;

            salvageBtn = UIKit.KitBtn(content, "btn_red", new Vector2(250, 128), "", 40, OnSalvageBtn, "Salvage");
            ((RectTransform)salvageBtn.transform).Place(0.5f, 0, new Vector2(336, 14), new Vector2(250, 128), new Vector2(0.5f, 0));
            salvageLabel = UIKit.Label(salvageBtn.transform, "", 40);
            salvageLabel.name = "Label";
            salvageLabel.rectTransform.Fill(84, 8, 10, 4);
            UIKit.Fit(salvageLabel, 20);
            var tr = UIKit.KitImg(salvageBtn.transform, "i_trash_w", new Vector2(52, 58), "Trash");
            tr.rectTransform.Place(0, 0.5f, new Vector2(30, 4), new Vector2(52, 58), new Vector2(0, 0.5f));
            tr.raycastTarget = false;

            ghost = UIKit.Img(transform, null, new Vector2(150, 150), "Ghost").rectTransform;
            ghost.GetComponent<Image>().raycastTarget = false;
            ghost.gameObject.SetActive(false);
        }

        public override void Open()
        {
            Picking = false; picked.Clear();
            // 11-12 끄는 중에 창이 닫혔으면(뒤로 가기) 끌던 그림·흐린 아이콘이 남아 있음 → 열 때 정리
            if (ghost) ghost.gameObject.SetActive(false);
            foreach (var c in slots) if (c) c.ResetDrag();
            foreach (var c in inv) if (c) c.ResetDrag();
            base.Open();
        }

        public override void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int code = S.slots[i];
                slots[i].Set(code);
                if (code >= 0)
                {
                    var w = Tables.Weapon(code);
                    slotAtk[i].text = Num.F(w.atk);
                    slotSpd[i].text = w.interval.ToString("0.##") + "s";
                }
                else { slotAtk[i].text = "-"; slotSpd[i].text = "-"; }
            }
            picked.RemoveWhere(x => x < 0 || x >= S.inv.Count || S.inv[x] < 0);
            for (int i = 0; i < inv.Length; i++) { inv[i].Set(S.inv[i]); inv[i].SetMarked(Picking && picked.Contains(i)); }
            gachaLabel.text = $"{Tables.Ci("GachaCost")} {L.T("뽑기")}";

            if (Picking)
            {
                double sum = 0;
                foreach (int i in picked) sum += Tables.Grade(Tables.GradeOf(S.inv[i])).salvage;
                hint.text = picked.Count == 0 ? L.T("분해할 총알을 눌러 고르세요 (여러 개 가능)")
                                              : L.F("{0}개 고름 → 다이아 +{1}", picked.Count, sum.ToString("0.#"));
                mergeLabel.text = L.T("취소");
                mergeBtn.GetComponent<Image>().color = UIKit.Off;
                salvageLabel.text = picked.Count > 0 ? L.T("분해") + $" {picked.Count}" : L.T("분해");
            }
            else
            {
                hint.text = L.T(GameManager.MergeAnyType ? "카드로 끌어다 놓아 장착 · 같은 등급끼리 겹쳐 합성" : "카드로 끌어다 놓아 장착 · 같은 탄알끼리 겹쳐 합성");
                mergeLabel.text = L.T("일괄 합성");
                mergeBtn.GetComponent<Image>().color = Color.white;
                salvageLabel.text = L.T("분해");
            }
        }

        /// <summary>칸을 누름: 고르기 중이면 인벤토리 총알 고르기/빼기, 아니면 정보 창</summary>
        public void Select(WeaponCell c)
        {
            if (!c || c.code < 0) return;
            if (Picking)
            {
                if (c.isSlot) { Msg(L.T("장착 중인 총알은 분해할 수 없습니다")); return; }
                if (!picked.Remove(c.index)) picked.Add(c.index);
                Refresh();
                return;
            }
            WeaponInfoPopup.Show(c.isSlot, c.index);
        }

        void OnMergeBtn()
        {
            if (Picking) { Picking = false; picked.Clear(); Refresh(); return; }   // [취소]
            var made = GM.MergeAll();
            if (made.Count == 0) { Msg(L.T(GameManager.MergeAnyType ? "합성할 수 있는 같은 등급 총알이 없습니다" : "합성할 수 있는 같은 탄알이 없습니다")); return; }
            Refresh();
            foreach (int i in made) inv[i].Pop();
            H.Toast(L.F("{0}번 합성!", made.Count));
        }

        void OnSalvageBtn()
        {
            if (!Picking)
            {
                if (S.inv.TrueForAll(x => x < 0)) { Msg(L.T("분해할 총알이 없습니다")); return; }
                Picking = true; picked.Clear(); Refresh();
                return;
            }
            if (picked.Count == 0) { Msg(L.T("분해할 총알을 눌러 고르세요 (여러 개 가능)")); return; }
            double sum = 0;
            foreach (int i in picked) sum += Tables.Grade(Tables.GradeOf(S.inv[i])).salvage;
            var sel = new List<int>(picked);
            ConfirmPopup.Ask($"<size=46>{L.T("분해")}</size>\n\n" + L.F("총알 {0}개를 분해할까요?", sel.Count) + $"\n\n{L.T("다이아")} +{sum:0.#}", () =>
            {
                double got = GM.SalvageMany(sel);
                Picking = false; picked.Clear();
                Refresh();
                if (got > 0) H.Toast(L.T("다이아") + $" +{got:0.#}");
            }, "분해");
        }

        /// <summary>끌어 놓기 처리 (WeaponCell 이 부름)</summary>
        public void Drop(WeaponCell from, WeaponCell to)
        {
            if (from == null || to == null || from == to || from.code < 0) return;
            string e = null;
            if (!from.isSlot && !to.isSlot)
            {
                bool merge = GameManager.CanMergePair(from.code, to.code);
                e = GM.Merge(from.index, to.index);
                if (merge && string.IsNullOrEmpty(e)) { H.Toast(L.T("합성!") + " " + Tables.WeaponName(S.inv[to.index])); to.Pop(); }
            }
            else if (!from.isSlot && to.isSlot) e = GM.Equip(from.index, to.index);
            else if (from.isSlot && !to.isSlot)
            {
                if (to.code < 0) e = GM.Unequip(from.index, to.index);
                else if (to.code == from.code) e = L.T("장착 중인 총알은 해제한 뒤 합성할 수 있습니다");
                else e = GM.Equip(to.index, from.index);
            }
            else GM.SwapSlots(from.index, to.index);
            Msg(e);
            Refresh();
        }
    }

    /// <summary>총알 정보 창 (11-05): 장착 카드 = 정보 + [해제](가운데 하나, 슬롯 강화는 11-05 밤 제거),
    /// 인벤토리 총알 = 정보 + [장착] · [분해](확인 창)</summary>
    public class WeaponInfoPopup : Popup
    {
        protected override Vector2 Size => new(880, 660);   // 11-05 밤 강화 줄이 빠져 낮춤
        protected override string Title => "총알 정보";
        protected override bool PillTitle => true;
        Image tileBg, tileIcon;
        Text tileGrade, body;
        Button left, right;
        public bool isSlot;
        public int index;

        protected override void Build()
        {
            tileBg = UIKit.KitImg(content, "slot", new Vector2(170, 170), "Tile");
            tileBg.rectTransform.Place(0.5f, 1, new Vector2(0, -14), new Vector2(170, 170), new Vector2(0.5f, 1));
            tileIcon = UIKit.Img(tileBg.transform, null, new Vector2(122, 106), "Icon");
            tileIcon.rectTransform.Place(0.5f, 0.5f, new Vector2(0, -4), new Vector2(122, 106), new Vector2(0.5f, 0.5f));
            tileGrade = UIKit.Label(tileBg.transform, "", 48, Color.white, TextAnchor.UpperLeft, 3);
            tileGrade.rectTransform.Fill(12, 0, 0, 6);
            body = UIKit.Label(content, "", 34, UIKit.Ink, TextAnchor.UpperCenter, 0);
            body.rectTransform.Fill(10, 150, 10, 206);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            left = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(330, 116), "", 42, OnLeft);
            ((RectTransform)left.transform).Place(0.5f, 0, new Vector2(-185, 10), new Vector2(330, 116), new Vector2(0.5f, 0));
            right = UIKit.KitBtn(content, "plank", new Vector2(330, 116), " ", 40, OnRight, "Right");
            ((RectTransform)right.transform).Place(0.5f, 0, new Vector2(185, 10), new Vector2(330, 116), new Vector2(0.5f, 0));
            UIKit.Fit(UIKit.LabelOf(left), 22); UIKit.Fit(UIKit.LabelOf(right), 20);
        }

        public static WeaponInfoPopup Show(bool slot, int idx)
        {
            var p = HUD.I.Open<WeaponInfoPopup>();
            p.isSlot = slot; p.index = idx;
            p.Refresh();
            return p;
        }

        int Code => isSlot ? S.slots[index] : S.inv[index];

        public override void Refresh()
        {
            int code = Code;
            if (code < 0) { if (gameObject.activeSelf) base.Close(); return; }
            var w = Tables.Weapon(code);
            int g = Tables.GradeOf(code);
            var gc = WeaponPopup.GradeColor(g);
            tileBg.color = Color.Lerp(new Color(0.55f, 0.42f, 0.33f), gc, 0.35f);
            tileIcon.sprite = WeaponPopup.TypeIcon(w.type); tileIcon.preserveAspect = true;
            tileGrade.text = Tables.GradeName(g); tileGrade.color = gc;

            string more = w.type == "shotgun" ? " · " + L.F("{0}발", w.count) : w.type == "pierce" ? " · " + L.F("{0}마리 관통", w.pierce) : w.type == "missile" ? " · " + L.F("범위 {0}", w.splash.ToString("0.0")) : w.type == "laser" ? " · " + L.T("일직선 전부") : "";
            string line1 = $"<size=42><color=#{ColorUtility.ToHtmlStringRGB(gc)}>{Tables.WeaponName(code)}</color></size>  ({Tables.TypeLabel(w.type)})";
            string line2 = L.F("공격력 {0} · 연사 {1}초", Num.F(w.atk), w.interval.ToString("0.##")) + more;
            double ov = Stats.SpeedOverflow(w, isSlot ? index : -1);
            if (ov > 1.005) line2 += "\n" + $"<color=#B07A10>{L.F("넘친 공속 → 공격 x{0}", ov < 1000 ? ov.ToString("0.##") : Num.F(ov))}</color>";

            var rb = right.GetComponent<Image>();
            if (isSlot)
            {
                string[] names = { "리더", "동료 1", "동료 2" };
                title.text = L.T(names[Mathf.Clamp(index, 0, 2)]);
                body.text = $"{line1}\n{line2}";
                UIKit.LabelOf(left).text = L.T("해제");
                left.gameObject.SetActive(true);
                right.gameObject.SetActive(false);
                ((RectTransform)left.transform).anchoredPosition = new Vector2(0, 10);
            }
            else
            {
                // 11-07: [장착] 없음 — 장착은 카드로 끌어다 놓기만
                title.text = L.T("총알 정보");
                body.text = $"{line1}\n{line2}\n<size=28><color=#8A6A50>{L.T("장착: 총알을 위 카드로 끌어다 놓으세요")}</color></size>";
                left.gameObject.SetActive(false);
                UIKit.LabelOf(right).text = L.T("분해") + $" +{Tables.Grade(g).salvage:0}";
                right.gameObject.SetActive(true);
                ((RectTransform)right.transform).anchoredPosition = new Vector2(0, 10);
                rb.sprite = Art.Kit("btn_red"); rb.color = Color.white;
            }
        }

        /// <summary>장착 칸의 [해제] (인벤토리 총알은 왼쪽 버튼이 없음 — 11-07 장착은 끌어다 놓기만)</summary>
        void OnLeft()
        {
            if (Code < 0 || !isSlot) return;
            string e = GM.Unequip(index);
            Msg(e);
            if (string.IsNullOrEmpty(e)) base.Close();
            H.Get<WeaponPopup>()?.Refresh();
        }

        void OnRight()
        {
            int code = Code;
            if (code < 0) return;
            if (!isSlot)   // 장착 칸은 오른쪽 버튼이 없음 (슬롯 강화 제거)
            {
                int g = Tables.GradeOf(code), i = index;
                ConfirmPopup.Ask($"<size=46>{L.T("분해")}</size>\n\n" + L.F("{0} 을(를) 분해할까요?", $"<color=#{ColorUtility.ToHtmlStringRGB(WeaponPopup.GradeColor(g))}>{Tables.WeaponName(code)}</color>") + $"\n\n{L.T("다이아")} +{Tables.Grade(g).salvage:0}", () =>
                {
                    Msg(GM.Salvage(i));
                    Close();
                    H.Get<WeaponPopup>()?.Refresh();
                }, "분해");
                return;
            }
        }
    }

    /// <summary>총알 칸 하나 (끌기·놓기·누르기)</summary>
    public class WeaponCell : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform rt;
        public bool isSlot;
        public int index, code = -1;
        WeaponPopup pop;
        Image bg, frame, icon, mark;
        Text grade, plus;
        float popT;
        bool dragging;

        public static WeaponCell Make(Transform parent, WeaponPopup p, bool slot, int idx, float size)
        {
            var bgI = UIKit.KitImg(parent, "slot", new Vector2(size, size), slot ? "Slot" + idx : "Inv" + idx);
            var c = bgI.gameObject.AddComponent<WeaponCell>();
            c.rt = bgI.rectTransform; c.pop = p; c.isSlot = slot; c.index = idx; c.bg = bgI;
            c.frame = UIKit.KitImg(c.rt, "outline", new Vector2(size, size), "Frame");
            c.frame.rectTransform.Fill();
            c.frame.raycastTarget = false;
            c.icon = UIKit.Img(c.rt, null, new Vector2(size * 0.7f, size * 0.7f), "Icon");
            c.icon.rectTransform.Place(0.5f, 0.5f, new Vector2(0, -4), new Vector2(size * 0.72f, size * 0.62f), new Vector2(0.5f, 0.5f));
            c.icon.raycastTarget = false;
            c.grade = UIKit.Label(c.rt, "", (int)(size * 0.28f), Color.white, TextAnchor.UpperLeft, 3);
            c.grade.rectTransform.Fill(12, 0, 0, 6);
            c.grade.raycastTarget = false;
            c.plus = UIKit.Label(c.rt, "", (int)(size * 0.2f), Color.white, TextAnchor.LowerRight, 3);
            c.plus.rectTransform.Fill(0, 8, 14, 0);
            c.plus.raycastTarget = false;
            // [분해] 고르기 표시 = 오른쪽 위 녹색 체크
            c.mark = UIKit.KitImg(c.rt, "check_on", new Vector2(size * 0.34f, size * 0.37f), "Mark");
            c.mark.rectTransform.Place(1, 1, new Vector2(8, 8), new Vector2(size * 0.34f, size * 0.37f), new Vector2(1, 1));
            c.mark.raycastTarget = false;
            c.mark.enabled = false;
            return c;
        }

        public void Set(int c)
        {
            code = c;
            bool has = c >= 0;
            icon.gameObject.SetActive(has);
            grade.text = has ? Tables.GradeName(Tables.GradeOf(c)) : "";
            plus.text = "";   // 11-05 밤: +N 숫자 제거 (슬롯 강화 없음)
            bg.sprite = Art.Kit(has ? "slot" : "slot_light");
            if (has)
            {
                icon.sprite = WeaponPopup.TypeIcon(Tables.TypeOf(c));
                icon.preserveAspect = true;
                var gc = WeaponPopup.GradeColor(Tables.GradeOf(c));
                frame.color = gc;
                grade.color = gc;
                bg.color = Color.Lerp(new Color(0.55f, 0.42f, 0.33f), gc, 0.35f);
            }
            else
            {
                frame.color = new Color(1, 1, 1, isSlot ? 0.5f : 0.0f);
                bg.color = Color.white;
            }
        }

        public void SetMarked(bool m)
        {
            mark.enabled = m;
            icon.color = m ? new Color(1, 1, 1, 0.55f) : Color.white;
        }

        public void Pop() { popT = 0.25f; }
        public void ResetDrag() { dragging = false; icon.color = Color.white; }

        void Update()
        {
            if (popT > 0) { popT -= Time.unscaledDeltaTime; rt.localScale = Vector3.one * (1 + Mathf.Max(0, popT) * 0.8f); }
            else if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
        }

        public void OnPointerClick(PointerEventData e) { if (!e.dragging) { AudioManager.Play(AudioManager.Sfx.UiClick); pop.Select(this); } }

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = code >= 0 && !pop.Picking;   // 고르기 중엔 끌기 안 함
            if (!dragging) return;
            pop.ghost.gameObject.SetActive(true);
            var im = pop.ghost.GetComponent<Image>();
            im.sprite = icon.sprite; im.preserveAspect = true;
            pop.ghost.SetAsLastSibling();
            icon.color = new Color(1, 1, 1, 0.35f);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)pop.ghost.parent, e.position, null, out var lp);
            pop.ghost.localPosition = lp;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            icon.color = Color.white;
            pop.ghost.gameObject.SetActive(false);
            var go = e.pointerCurrentRaycast.gameObject;
            var to = go ? go.GetComponentInParent<WeaponCell>() : null;
            if (!to && go) { var cd = go.GetComponentInParent<CardDrop>(); if (cd) to = cd.cell; }   // 카드 빈 곳(이름·공격력 글자)에 놓아도 그 칸
            pop.Drop(this, to);
        }
    }

    /// <summary>장착 카드 전체를 놓는 자리로 (11-07) — 카드 안 총알 칸이 작아서, 카드 어디에 놓아도 그 칸에 장착</summary>
    public class CardDrop : MonoBehaviour { public WeaponCell cell; }
}
