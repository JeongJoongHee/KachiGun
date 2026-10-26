using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 총알 창 (기획서 6장): 장착 3칸(리더·동료1·동료2) + 5x3 인벤토리 + 뽑기(다이아 100, D 등급 100%).
    /// 같은 등급 두 개를 끌어다 겹치면 한 단계 위 등급의 랜덤 종류로 합성. 장착 중인 총알은 해제한 뒤 합성.
    /// 장착 칸을 누르면 [슬롯 강화] (Weapon_Enchant), 인벤토리 총알을 누르면 [분해] → 다이아 (Grade_Enchant_Multiplier).
    /// </summary>
    public class WeaponPopup : Popup
    {
        protected override Vector2 Size => new(1010, 1640);
        protected override string Title => "총알";
        readonly WeaponCell[] slots = new WeaponCell[SaveData.SlotCount];
        readonly WeaponCell[] inv = new WeaponCell[SaveData.InvSize];
        Text info, gachaLabel;
        Button action, extra;
        WeaponCell selected;
        public RectTransform ghost;

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
            string[] names = { L.T("리더"), L.T("동료 1"), L.T("동료 2") };
            for (int i = 0; i < 3; i++)
            {
                var c = WeaponCell.Make(content, this, true, i, 230);
                c.rt.Place(0.5f, 1, new Vector2(-280 + i * 280, -20), new Vector2(230, 230), new Vector2(0.5f, 1));
                var t = UIKit.Label(c.rt, names[i], 32, UIKit.Ink, TextAnchor.MiddleCenter, 0);
                t.rectTransform.Place(0.5f, 0, new Vector2(0, -26), new Vector2(200, 40), new Vector2(0.5f, 0.5f));
                slots[i] = c;
            }

            var infoBg = UIKit.Row(content, "Info", true);
            infoBg.rectTransform.Place(0.5f, 1, new Vector2(0, -310), new Vector2(880, 170), new Vector2(0.5f, 1));
            info = UIKit.Label(infoBg.transform, "", 30, Color.white, TextAnchor.MiddleLeft, 2);
            info.rectTransform.Fill(26, 8, 230, 8);
            info.resizeTextForBestFit = true; info.resizeTextMinSize = 20; info.resizeTextMaxSize = 30;
            action = UIKit.ColorBtn(infoBg.transform, new Color(0.45f, 0.7f, 0.3f), new Vector2(190, 74), L.T("장착"), 36, DoAction);
            ((RectTransform)action.transform).Place(1, 0.5f, new Vector2(-20, 40), new Vector2(190, 74), new Vector2(1, 0.5f));
            // 장착 칸 = 슬롯 강화, 인벤토리 = 분해
            extra = UIKit.ColorBtn(infoBg.transform, new Color(0.95f, 0.6f, 0.15f), new Vector2(190, 74), L.T("강화"), 36, DoExtra);
            ((RectTransform)extra.transform).Place(1, 0.5f, new Vector2(-20, -40), new Vector2(190, 74), new Vector2(1, 0.5f));

            for (int i = 0; i < SaveData.InvSize; i++)
            {
                int r = i / 5, c = i % 5;
                var cell = WeaponCell.Make(content, this, false, i, 160);
                cell.rt.Place(0.5f, 1, new Vector2(-344 + c * 172, -510 - r * 172), new Vector2(160, 160), new Vector2(0.5f, 1));
                inv[i] = cell;
            }

            var g = UIKit.ColorBtn(content, new Color(0.3f, 0.55f, 0.95f), new Vector2(460, 120), "", 40, () =>
            {
                string e = GM.Gacha();
                if (e != null) Msg(e); else { Refresh(); H.Toast(L.T("D 등급 총알 획득!")); }
            });
            ((RectTransform)g.transform).Place(0.5f, 0, new Vector2(0, 10), new Vector2(460, 120), new Vector2(0.5f, 0));
            gachaLabel = UIKit.LabelOf(g);
            var gi = UIKit.Img(g.transform, Art.Get("UI/icon_gem"), new Vector2(60, 60), "Gem");
            gi.rectTransform.Place(0, 0.5f, new Vector2(40, 0), new Vector2(60, 60), new Vector2(0, 0.5f));
            var hint = UIKit.Label(content, L.T("같은 등급 두 개를 끌어다 겹치면 합성!\n장착 칸을 누르면 슬롯 강화 · 남는 총알은 분해 → 다이아"), 30, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            hint.rectTransform.Place(0.5f, 0, new Vector2(0, 260), new Vector2(880, 80), new Vector2(0.5f, 0.5f));

            ghost = UIKit.Img(transform, null, new Vector2(150, 150), "Ghost").rectTransform;
            ghost.GetComponent<Image>().raycastTarget = false;
            ghost.gameObject.SetActive(false);
        }

        public override void Refresh()
        {
            for (int i = 0; i < slots.Length; i++) slots[i].Set(S.slots[i]);
            for (int i = 0; i < inv.Length; i++) inv[i].Set(S.inv[i]);
            gachaLabel.text = $"   {L.T("뽑기")}  {Tables.Ci("GachaCost")}";
            ShowInfo();
        }

        public void Select(WeaponCell c) { selected = c; ShowInfo(); }

        void ShowInfo()
        {
            foreach (var c in slots) c.SetSelected(c == selected);
            foreach (var c in inv) c.SetSelected(c == selected);
            int code = selected ? selected.code : -1;
            if (code < 0)
            {
                info.text = L.T("총알을 누르면 정보가 나옵니다.\n장착 칸으로 끌어다 놓으면 장착!");
                action.gameObject.SetActive(false);
                extra.gameObject.SetActive(false);
                return;
            }
            var w = Tables.Weapon(code);
            int g = Tables.GradeOf(code);
            string more = w.type == "shotgun" ? " · " + L.F("{0}발", w.count) : w.type == "pierce" ? " · " + L.F("{0}마리 관통", w.pierce) : w.type == "missile" ? " · " + L.F("범위 {0}", w.splash.ToString("0.0")) : w.type == "laser" ? " · " + L.T("일직선 전부") : "";
            string line1 = $"<color=#{ColorUtility.ToHtmlStringRGB(GradeColor(g))}>{Tables.WeaponName(code)}</color>  ({Tables.TypeLabel(w.type)})";
            string line2 = L.F("공격력 {0} · 연사 {1}초", Num.F(w.atk), w.interval.ToString("0.##")) + more;
            double ov = Stats.SpeedOverflow(w, selected.isSlot ? selected.index : -1);
            if (ov > 1.005) line2 += $" · <color=#FFD966>{L.F("넘친 공속 → 공격 x{0}", ov < 1000 ? ov.ToString("0.##") : Num.F(ov))}</color>";
            if (selected.isSlot)
            {
                int i = selected.index, lv = Stats.SlotLvEff(i);
                string cap = S.slotLv[i] > lv ? " " + L.F("(칸 +{0}, 이 등급은 +{1}까지)", S.slotLv[i], lv) : " / " + L.F("최대 +{0}", Stats.SlotMax(i));
                info.text = $"{line1}\n<color=#FFD966>{L.F("슬롯 강화 +{0}", lv)}</color>{cap}\n" +
                            (lv > 0 ? L.F("공격 +{0}% · 속도 +{1}% · 보스 +{2}%", Stats.SlotAtkPct(i).ToString("0.#"), Stats.SlotSpdPct(i).ToString("0.#"), Stats.SlotBossPct(i).ToString("0.#")) : line2);
                UIKit.LabelOf(extra).text = L.T("강화");
            }
            else
            {
                info.text = $"{line1}\n{line2}";
                UIKit.LabelOf(extra).text = L.T("분해") + $" +{Tables.Grade(g).salvage:0}";
            }
            action.gameObject.SetActive(true);
            extra.gameObject.SetActive(true);
            UIKit.LabelOf(action).text = L.T(selected.isSlot ? "해제" : "장착");
        }

        void DoAction()
        {
            if (!selected || selected.code < 0) return;
            string e;
            if (selected.isSlot) e = GM.Unequip(selected.index);
            else
            {
                int slot = S.slots.IndexOf(-1);
                if (slot < 0) slot = SaveData.SlotCount - 1;   // 다 차 있으면 마지막 칸과 바꿈
                e = GM.Equip(selected.index, slot);
            }
            Msg(e);
            selected = null;
            Refresh();
        }

        /// <summary>[강화] = 장착 칸 슬롯 강화, [분해] = 인벤토리 총알 → 다이아</summary>
        void DoExtra()
        {
            if (!selected || selected.code < 0) return;
            var cell = selected;
            if (!cell.isSlot)
            {
                int g = Tables.GradeOf(cell.code);
                string nm = Tables.WeaponName(cell.code);
                ConfirmPopup.Ask($"<size=46>{L.T("분해")}</size>\n\n" + L.F("{0} 을(를) 분해할까요?", $"<color=#{ColorUtility.ToHtmlStringRGB(GradeColor(g))}>{nm}</color>") + $"\n\n{L.T("다이아")} +{Tables.Grade(g).salvage:0}", () =>
                {
                    Msg(GM.Salvage(cell.index));
                    selected = null;
                    Refresh();
                }, "분해");
                return;
            }
            int i = cell.index, lv = S.slotLv[i], max = Stats.SlotMax(i);
            if (lv >= max) { Msg(GM.EnchantSlot(i, out _)); return; }      // 최대: 이유만 토스트
            var e = Tables.Enchant(lv + 1);
            double gm = Tables.Grade(Tables.GradeOf(cell.code)).enchantMul;
            var now = Tables.Enchant(lv);
            ConfirmPopup.Ask($"<size=46>{L.T("슬롯 강화")} +{lv} → +{lv + 1}</size>\n\n{L.T("다이아")} {Num.Gem(e.gems)} · {L.T("골드")} {Num.F(e.gold)}\n{L.F("성공 확률 {0}%", e.rate.ToString("0"))}\n\n" +
                             $"{L.T("공격력")} +{now.atk * gm:0.#}% → <color=#3F8A2E>+{e.atk * gm:0.#}%</color>\n" + L.F("공격속도 +{0}% · 보스 피해 +{1}%", (e.spd * gm).ToString("0.#"), (e.boss * gm).ToString("0.#")), () =>
            {
                string err = GM.EnchantSlot(i, out bool ok);
                if (err != null) Msg(err);
                else { H.Toast(ok ? L.T("강화 성공!") + $" +{S.slotLv[i]}" : L.T("강화 실패… (재료만 사라짐)")); if (ok) slots[i].Pop(); }
                Squad.I.Rebuild();
                Refresh();
            }, "슬롯 강화");
        }

        /// <summary>끌어 놓기 처리 (WeaponCell 이 부름)</summary>
        public void Drop(WeaponCell from, WeaponCell to)
        {
            if (from == null || to == null || from == to || from.code < 0) return;
            string e = null;
            int beforeGrade = to.code >= 0 ? Tables.GradeOf(to.code) : -1;
            if (!from.isSlot && !to.isSlot)
            {
                bool merge = to.code >= 0 && Tables.GradeOf(to.code) == Tables.GradeOf(from.code);
                e = GM.Merge(from.index, to.index);
                if (merge && string.IsNullOrEmpty(e)) { H.Toast(L.T("합성!") + " " + Tables.WeaponName(S.inv[to.index])); to.Pop(); }
            }
            else if (!from.isSlot && to.isSlot) e = GM.Equip(from.index, to.index);
            else if (from.isSlot && !to.isSlot)
            {
                if (to.code < 0) e = GM.Unequip(from.index, to.index);
                else if (beforeGrade == Tables.GradeOf(from.code)) e = L.T("장착 중인 총알은 해제한 뒤 합성할 수 있습니다");
                else e = GM.Equip(to.index, from.index);
            }
            else GM.SwapSlots(from.index, to.index);
            Msg(e);
            selected = null;
            Refresh();
        }
    }

    /// <summary>총알 칸 하나 (끌기·놓기·누르기)</summary>
    public class WeaponCell : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform rt;
        public bool isSlot;
        public int index, code = -1;
        WeaponPopup pop;
        Image bg, frame, icon;
        Text grade, plus;
        float popT;

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
            c.plus = UIKit.Label(c.rt, "", (int)(size * 0.2f), new Color(1f, 0.85f, 0.4f), TextAnchor.LowerRight, 3);
            c.plus.rectTransform.Fill(0, 8, 14, 0);
            c.plus.raycastTarget = false;
            return c;
        }

        public void Set(int c)
        {
            code = c;
            bool has = c >= 0;
            icon.gameObject.SetActive(has);
            grade.text = has ? Tables.GradeName(Tables.GradeOf(c)) : "";
            int lv = isSlot && index < SaveData.Cur.slotLv.Count ? SaveData.Cur.slotLv[index] : 0;
            plus.text = isSlot && lv > 0 ? "+" + (has ? Stats.SlotLvEff(index) : lv) : "";
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

        public void SetSelected(bool s) { frame.rectTransform.offsetMin = s ? new Vector2(-6, -6) : Vector2.zero; frame.rectTransform.offsetMax = s ? new Vector2(6, 6) : Vector2.zero; }
        public void Pop() { popT = 0.25f; }

        void Update()
        {
            if (popT > 0) { popT -= Time.unscaledDeltaTime; rt.localScale = Vector3.one * (1 + Mathf.Max(0, popT) * 0.8f); }
            else rt.localScale = Vector3.one;
        }

        public void OnPointerClick(PointerEventData e) { if (!e.dragging) { AudioManager.Play(AudioManager.Sfx.UiClick); pop.Select(this); } }

        public void OnBeginDrag(PointerEventData e)
        {
            if (code < 0) return;
            pop.ghost.gameObject.SetActive(true);
            var im = pop.ghost.GetComponent<Image>();
            im.sprite = icon.sprite; im.preserveAspect = true;
            pop.ghost.SetAsLastSibling();
            icon.color = new Color(1, 1, 1, 0.35f);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (code < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)pop.ghost.parent, e.position, null, out var lp);
            pop.ghost.localPosition = lp;
        }

        public void OnEndDrag(PointerEventData e)
        {
            icon.color = Color.white;
            pop.ghost.gameObject.SetActive(false);
            if (code < 0) return;
            var go = e.pointerCurrentRaycast.gameObject;
            var to = go ? go.GetComponentInParent<WeaponCell>() : null;
            pop.Drop(this, to);
        }
    }
}
