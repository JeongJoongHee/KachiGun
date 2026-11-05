using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 총알 관리 창 (기획서 6장 · 11-05 추가 요청/UI 수정.png 모양): 장착 카드 3장(리더·동료1·동료2 — 등급 · +강화 · 공격력 · 연사) + 5x4 인벤토리
    /// + 아래 버튼 [일괄 합성] · [다이아 100 뽑기] · [분해].
    /// 같은 탄알(종류·등급 같음) 두 개를 끌어다 겹치면 한 단계 위 등급의 랜덤 종류로 합성 (11-04: 종류가 다르면 안 됨). 장착 중인 총알은 해제한 뒤 합성.
    /// 장착 카드를 누르면 슬롯 강화 창(WeaponInfoPopup: [해제] · [강화]), 인벤토리 총알을 누르면 정보 창([장착] · [분해]) — 카드의 [강화] 버튼은 빼고 기능은 그대로.
    /// [일괄 합성] = 같은 탄알 두 개씩 한 단계만(GM.MergeAll) · [분해] = 누르면 여러 개 고르기 → 다시 누르면 고른 것 한 번에 분해(GM.SalvageMany).
    /// </summary>
    public class WeaponPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1660);
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
            // ---- 장착 카드 3장 ----
            string[] names = { L.T("리더"), L.T("동료 1"), L.T("동료 2") };
            for (int i = 0; i < 3; i++)
            {
                var card = UIKit.KitImg(content, "panel_tan", new Vector2(286, 440), "Card" + i);
                card.rectTransform.Place(0.5f, 1, new Vector2(-304 + i * 304, -4), new Vector2(286, 440), new Vector2(0.5f, 1));
                if (i == 0)
                {
                    // 리더 카드 = 금색 테두리
                    var gl = UIKit.KitImg(card.transform, "outline", new Vector2(300, 454), "LeaderGlow");
                    gl.rectTransform.Fill(-9, -9, -9, -9);
                    gl.color = UIKit.Gold;
                    gl.raycastTarget = false;
                }
                var c = WeaponCell.Make(card.transform, this, true, i, 226);
                c.rt.Place(0.5f, 1, new Vector2(0, -18), new Vector2(226, 226), new Vector2(0.5f, 1));
                var t = UIKit.Label(card.transform, names[i], 38, UIKit.Ink, TextAnchor.MiddleCenter, 0);
                t.rectTransform.Place(0.5f, 1, new Vector2(0, -284), new Vector2(260, 50), new Vector2(0.5f, 0.5f));
                // 공격력 · 연사
                var sw = UIKit.Img(card.transform, Art.Get("UI/icon_swords"), new Vector2(46, 42), "AtkIcon");
                sw.rectTransform.Place(0.5f, 1, new Vector2(-104, -356), new Vector2(46, 42), new Vector2(0.5f, 0.5f));
                slotAtk[i] = UIKit.Label(card.transform, "", 34, UIKit.Ink, TextAnchor.MiddleLeft, 0);
                slotAtk[i].rectTransform.Place(0.5f, 1, new Vector2(-78, -356), new Vector2(96, 46), new Vector2(0, 0.5f));
                UIKit.Fit(slotAtk[i], 20);
                var bo = UIKit.KitImg(card.transform, "i_bolt", new Vector2(34, 42), "SpdIcon");
                bo.rectTransform.Place(0.5f, 1, new Vector2(34, -356), new Vector2(34, 42), new Vector2(0.5f, 0.5f));
                slotSpd[i] = UIKit.Label(card.transform, "", 34, UIKit.Ink, TextAnchor.MiddleLeft, 0);
                slotSpd[i].rectTransform.Place(0.5f, 1, new Vector2(54, -356), new Vector2(86, 46), new Vector2(0, 0.5f));
                UIKit.Fit(slotSpd[i], 20);
                slots[i] = c;
                // 카드 아무 데나 눌러도 그 칸 정보
                int si = i;
                var cb = card.gameObject.AddComponent<Button>();
                cb.transition = Selectable.Transition.None;
                cb.onClick.AddListener(() => { AudioManager.Play(AudioManager.Sfx.UiClick); Select(slots[si]); });
            }

            // ---- 인벤토리 5x4 ----
            var gridBg = UIKit.Box(content, new Color(0.95f, 0.89f, 0.76f), "GridBg");
            gridBg.rectTransform.Place(0.5f, 1, new Vector2(0, -470), new Vector2(904, 726), new Vector2(0.5f, 1));
            gridBg.raycastTarget = false;
            for (int i = 0; i < SaveData.InvSize; i++)
            {
                int r = i / 5, c = i % 5;
                var cell = WeaponCell.Make(content, this, false, i, 160);
                cell.rt.Place(0.5f, 1, new Vector2(-344 + c * 172, -486 - r * 174), new Vector2(160, 160), new Vector2(0.5f, 1));
                inv[i] = cell;
            }

            hint = UIKit.Label(content, "", 34, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            hint.rectTransform.Place(0.5f, 1, new Vector2(0, -1250), new Vector2(900, 60), new Vector2(0.5f, 0.5f));
            UIKit.Fit(hint, 22);

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

        public override void Open() { Picking = false; picked.Clear(); base.Open(); }

        public void PopSlot(int i) { if (i >= 0 && i < slots.Length) slots[i].Pop(); }

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
                hint.text = L.T("같은 탄알을 겹쳐서 합성하세요!");
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
            if (made.Count == 0) { Msg(L.T("합성할 수 있는 같은 탄알이 없습니다")); return; }
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
                bool merge = to.code >= 0 && to.code == from.code;
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

    /// <summary>총알 정보 창 (11-05): 장착 카드 = 슬롯 강화 정보 + [해제] · [강화](바로 강화, 창은 그대로 — 여러 번 누르기 좋게),
    /// 인벤토리 총알 = 정보 + [장착] · [분해](확인 창)</summary>
    public class WeaponInfoPopup : Popup
    {
        protected override Vector2 Size => new(880, 860);
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
                int lv = Stats.SlotLvEff(index), max = Stats.SlotMax(index);
                string cap = S.slotLv[index] > lv ? " " + L.F("(칸 +{0}, 이 등급은 +{1}까지)", S.slotLv[index], lv) : " / " + L.F("최대 +{0}", max);
                string now = lv > 0 ? L.F("공격 +{0}% · 속도 +{1}% · 보스 +{2}%", Stats.SlotAtkPct(index).ToString("0.#"), Stats.SlotSpdPct(index).ToString("0.#"), Stats.SlotBossPct(index).ToString("0.#")) : "";
                string next;
                if (S.slotLv[index] >= max || lv >= Tables.EnchantTop) next = $"<color=#B07A10>{L.T("최고 강화입니다")}</color>";
                else
                {
                    var e = Tables.Enchant(lv + 1);
                    double gm = Tables.Grade(g).enchantMul;
                    next = $"<color=#3F8A2E>+{lv + 1}: {L.T("공격력")} +{e.atk * gm:0.#}%</color>\n{L.T("다이아")} {Num.Gem(e.gems)} · {L.T("골드")} {Num.F(e.gold)} · {L.F("성공 확률 {0}%", e.rate.ToString("0"))}";
                }
                body.text = $"{line1}\n{line2}\n<color=#B07A10>{L.F("슬롯 강화 +{0}", lv)}</color>{cap}" + (now != "" ? "\n" + now : "") + "\n" + next;
                UIKit.LabelOf(left).text = L.T("해제");
                UIKit.LabelOf(right).text = L.T("강화");
                rb.sprite = Art.Kit("plank"); rb.color = Color.white;
            }
            else
            {
                title.text = L.T("총알 정보");
                body.text = $"{line1}\n{line2}";
                UIKit.LabelOf(left).text = L.T("장착");
                UIKit.LabelOf(right).text = L.T("분해") + $" +{Tables.Grade(g).salvage:0}";
                rb.sprite = Art.Kit("btn_red"); rb.color = Color.white;
            }
        }

        void AfterChange()
        {
            H.Get<WeaponPopup>()?.Refresh();
            Refresh();
        }

        void OnLeft()
        {
            if (Code < 0) return;
            string e;
            if (isSlot) e = GM.Unequip(index);
            else
            {
                int slot = S.slots.IndexOf(-1);
                if (slot < 0) slot = SaveData.SlotCount - 1;   // 다 차 있으면 마지막 칸과 바꿈
                e = GM.Equip(index, slot);
            }
            Msg(e);
            if (string.IsNullOrEmpty(e)) base.Close();
            H.Get<WeaponPopup>()?.Refresh();
        }

        void OnRight()
        {
            int code = Code;
            if (code < 0) return;
            if (!isSlot)
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
            string err = GM.EnchantSlot(index, out bool ok);
            if (err != null) { Msg(err); return; }
            H.Toast(ok ? L.T("강화 성공!") + $" +{S.slotLv[index]}" : L.T("강화 실패… (재료만 사라짐)"));
            Squad.I.Rebuild();
            AfterChange();
            var wp = H.Get<WeaponPopup>();
            if (ok && wp) wp.PopSlot(index);
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
            int lv = isSlot && index < SaveData.Cur.slotLv.Count ? SaveData.Cur.slotLv[index] : 0;
            plus.text = isSlot ? "+" + (has ? Stats.SlotLvEff(index) : lv) : "";   // 11-05: 장착 카드는 +0 도 보임
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

        void Update()
        {
            if (popT > 0) { popT -= Time.unscaledDeltaTime; rt.localScale = Vector3.one * (1 + Mathf.Max(0, popT) * 0.8f); }
            else rt.localScale = Vector3.one;
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
            pop.Drop(this, to);
        }
    }
}
