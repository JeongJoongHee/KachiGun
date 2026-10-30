using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 전투 화면 UI — 사용자 그림 'UI 수정.png' 배치.
    /// 위: 진행 막대 · 챕터 마디 · Stage N-M · [도전] · 절전모드
    /// 왼쪽 위: 설정 → 상점 → 미션 · 오른쪽 위: 골드/다이아 → 공격 x3 → 골드 x3 → 자동강화
    /// 아래(위에서부터): 총알 1·2·3 칸(장착 무기, 누르면 무기 창) → 강화 카드 3장(누르고 있으면 연속 강화)
    ///   → 맨 아래 버튼 줄: 무기 · 스킨 · 보급품 · 보스 · 광산
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I;
        public RectTransform root;
        public RectTransform goldIcon, gemIcon;
        Text goldText, gemText, stageText, pctText, timerText, leftText;
        Image pctFill;
        readonly Image[] nodes = new Image[5];
        RectTransform nodeMarker, stageGroup, dungeonGroup;
        Button ecoBtn, missionBtn, quitBtn, autoBtn, bossBtn;
        Text autoState;
        GameObject missionDot;
        readonly Card[] cards = new Card[3];
        readonly Dictionary<string, SideBtn> side = new();
        Button buffAtk, buffGold;
        Text buffAtkT, buffGoldT;
        RectTransform paw;
        RectTransform toastRt; Text toastT; float toastTime;
        GameObject eco;
        float refreshT;
        float goldBump, gemBump;
        public readonly List<Popup> openPopups = new();
        public bool PopupOpen => openPopups.Count > 0;

        static SaveData S => SaveData.Cur;
        static GameManager GM => GameManager.I;

        class Card { public RectTransform rt, holder; public Text lv, val, cost, name; public Image img; }
        class SideBtn { public Button b; public Image icon; public GameObject lockIcon, dot; public Text label; public Func<bool> open; public string lockText; }

        void Awake() { I = this; }

        // =====================================================================
        public void Build(Canvas canvas)
        {
            root = (RectTransform)transform;   // 안전 영역(Safe) 안

            var fxLayer = UIKit.Rect(root, "FxLayer").Fill();
            Fx.I.uiLayer = fxLayer;

            BuildTop();
            BuildBottom();
            BuildNav();
            BuildDungeonHud();

            // 토스트 (안내·오류·스테이지/보스 알림 모두 이것 하나로)
            var tb = UIKit.KitImg(root, "panel_brown", new Vector2(800, 100), "Toast");
            tb.color = new Color(1, 1, 1, 0f);      // 판 없이 글자만 (외곽선으로 읽힘)
            toastRt = tb.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 180), new Vector2(800, 100), new Vector2(0.5f, 0.5f));
            tb.raycastTarget = false;
            toastT = UIKit.Label(toastRt, "", 44, Color.white, TextAnchor.MiddleCenter, 4);
            toastT.rectTransform.Fill(10, 6, 10, 6);
            toastRt.gameObject.SetActive(false);

            BuildTutorial();
            BuildEco();

            GM.Changed += () => refreshT = 0;
            GM.StageChanged += RefreshStage;
            RefreshStage();
            Refresh();
        }

        // ---------- 위 ----------
        void BuildTop()
        {
            // 맨 위 진행 막대 (지금 서브 스테이지 처치 %)
            var bar = UIKit.KitImg(root, "bar_track", new Vector2(1060, 40), "TopBar");
            bar.rectTransform.Place(0.5f, 1f, new Vector2(0, -52), new Vector2(1060, 40), new Vector2(0.5f, 1f));
            pctFill = UIKit.KitImg(bar.transform, "bar_green", new Vector2(10, 10), "Fill");
            pctFill.rectTransform.anchorMin = new Vector2(0, 0); pctFill.rectTransform.anchorMax = new Vector2(0.4f, 1);
            pctFill.rectTransform.offsetMin = Vector2.zero; pctFill.rectTransform.offsetMax = Vector2.zero;
            pctText = UIKit.Label(bar.transform, "0%", 30, Color.white, TextAnchor.MiddleCenter, 2);
            pctText.rectTransform.Fill();

            // 설정
            var gear = UIKit.TileBtn(root, Art.Kit("i_gear_w"), new Vector2(92, 98), 0.6f, () => Open<SettingsPopup>(), "Settings");
            ((RectTransform)gear.transform).Place(0, 1, new Vector2(22, -108), new Vector2(92, 98), new Vector2(0, 1));
            // 챕터 마디 5개 + 진행 표시
            stageGroup = UIKit.Rect(root, "StageGroup").Place(0.5f, 1f, new Vector2(0, -110), new Vector2(560, 280), new Vector2(0.5f, 1f));
            var line = UIKit.KitImg(stageGroup, "bar_track", new Vector2(480, 22), "Line");
            line.rectTransform.Place(0.5f, 1f, new Vector2(0, -26), new Vector2(480, 22), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 5; i++)
            {
                var n = UIKit.KitImg(stageGroup, "node", new Vector2(40, 40), "Node" + i);
                n.rectTransform.Place(0.5f, 1f, new Vector2(-200 + i * 100, -26), new Vector2(i == 4 ? 50 : 36, i == 4 ? 50 : 36), new Vector2(0.5f, 0.5f));
                nodes[i] = n;
            }
            var head = UIKit.Img(stageGroup, Art.Get("Gen/capy_head"), new Vector2(70, 48), "Marker");
            nodeMarker = head.rectTransform;
            nodeMarker.Place(0.5f, 1f, new Vector2(-200, -12), new Vector2(70, 48), new Vector2(0.5f, 0f));
            stageText = UIKit.Label(stageGroup, "Stage 1-1", 46, Color.white, TextAnchor.MiddleCenter, 4);
            stageText.rectTransform.Place(0.5f, 1f, new Vector2(0, -90), new Vector2(500, 60), new Vector2(0.5f, 0.5f));
            // 절전모드 (오른쪽, 골드 x3 밑)
            ecoBtn = UIKit.TileBtn(root, Art.Kit("i_pause_w"), new Vector2(88, 94), 0.5f, ToggleEco, "Eco");
            ((RectTransform)ecoBtn.transform).Place(1, 1, new Vector2(-38, -554), new Vector2(88, 94), new Vector2(1, 1));
            var el = UIKit.Label(ecoBtn.transform, L.T("절전모드"), 28, Color.white, TextAnchor.MiddleCenter, 2);
            el.rectTransform.Place(0.5f, 0, new Vector2(0, -14), new Vector2(160, 34), new Vector2(0.5f, 0.5f));

            // 골드·다이아
            goldText = Pill(new Vector2(-20, -112), "UI/icon_money", new Vector2(64, 66), out goldIcon);
            gemText = Pill(new Vector2(-20, -194), "UI/icon_gem", new Vector2(62, 62), out gemIcon);

            // 상점 (설정 아래) · 미션 (상점 아래)
            IconBtn("shop", "UI/icon_shop", L.T("상점"), new Vector2(88, 80), 0, -232, () => GM.Unlocked("shop"), "shop", () => Open<ShopPopup>());
            missionBtn = UIKit.TileBtn(root, Art.Kit("i_trophy_w"), new Vector2(88, 94), 0.6f, () => Open<MissionPopup>(), "Mission");
            ((RectTransform)missionBtn.transform).Place(0, 1, new Vector2(24, -372), new Vector2(88, 94), new Vector2(0, 1));
            var ml = UIKit.Fit(UIKit.Label(missionBtn.transform, L.T("미션"), 28, Color.white, TextAnchor.MiddleCenter, 2), 18);
            ml.rectTransform.Place(0.5f, 0, new Vector2(0, -12), new Vector2(120, 34), new Vector2(0.5f, 0.5f));
            var dot = UIKit.KitImg(missionBtn.transform, "badge", new Vector2(34, 34), "Dot");
            dot.rectTransform.Place(1, 1, new Vector2(8, 8), new Vector2(34, 34), new Vector2(1, 1));
            missionDot = dot.gameObject;

            // 자동강화 (오른쪽 맨 아래 = 절전모드 밑). 10-30: 안 가졌으면 구매(auto_upgrade), 가졌으면 켜기/끄기.
            // 폰에서 결제가 꺼져 있고(라이선스 키 없음·상품 없음) 안 가졌으면 숨김
            autoBtn = UIKit.Btn(root, Art.Kit("btn_play"), new Vector2(84, 84), null, 0, () => GM.AutoUpgradeButton(), "AutoUpgrade");
            ((RectTransform)autoBtn.transform).Place(1, 1, new Vector2(-40, -694), new Vector2(84, 84), new Vector2(1, 1));
            var al = UIKit.Label(autoBtn.transform, L.T("자동강화"), 28, Color.white, TextAnchor.MiddleCenter, 2);
            al.rectTransform.Place(0.5f, 0, new Vector2(0, -16), new Vector2(160, 34), new Vector2(0.5f, 0.5f));
            autoState = UIKit.Label(autoBtn.transform, "", 24, Color.white, TextAnchor.MiddleCenter, 2);
            autoState.rectTransform.Place(0.5f, 0, new Vector2(0, -46), new Vector2(120, 30), new Vector2(0.5f, 0.5f));
            Iap.Changed += RefreshAuto;

            // 광고 버프 (오른쪽, 다이아 아래) — 배율 = Buff_Table
            buffAtk = BuffBtn(new Vector2(-38, -282), "UI/stat_atk", L.T("공격") + " " + BuffX(true), true, out buffAtkT);
            buffGold = BuffBtn(new Vector2(-38, -418), "UI/stat_gold", L.T("골드") + " " + BuffX(false), false, out buffGoldT);

            // 보스 도전 (보스 실패 뒤 반복 사냥 중에만, 도전 버튼 아래)
            bossBtn = UIKit.ColorBtn(stageGroup, new Color(0.85f, 0.3f, 0.25f), new Vector2(250, 66), L.T("보스 도전"), 34, () => Msg(GM.ChallengeBoss()));
            ((RectTransform)bossBtn.transform).Place(0.5f, 1f, new Vector2(0, -236), new Vector2(250, 66), new Vector2(0.5f, 0.5f));
            var bi = UIKit.Img(bossBtn.transform, Art.Zombie("ogre")[0], new Vector2(60, 58), "Icon");
            bi.rectTransform.Place(0, 0.5f, new Vector2(36, 4), new Vector2(60, 58), new Vector2(0.5f, 0.5f));
            bi.raycastTarget = false;
            UIKit.LabelOf(bossBtn).rectTransform.offsetMin = new Vector2(62, 4);
            UIKit.Fit(UIKit.LabelOf(bossBtn), 18);
            bossBtn.gameObject.SetActive(false);

            // 도전 (Stage 글자 아래)
            var chb = UIKit.Btn(stageGroup, UIKit.PlankThin, new Vector2(230, 70), null, 0, () => Confirm(ConfirmPopup.Kind.Challenge), "Challenge");
            var chr = ((RectTransform)chb.transform).Place(0.5f, 1f, new Vector2(0, -160), new Vector2(230, 70), new Vector2(0.5f, 0.5f));
            var chi = UIKit.Img(chr, Art.Get("UI/icon_swords"), new Vector2(50, 46), "Icon");
            chi.rectTransform.Place(0, 0.5f, new Vector2(44, 2), new Vector2(50, 46), new Vector2(0.5f, 0.5f));
            var chl = UIKit.Label(chr, L.T("도전"), 34, Color.white, TextAnchor.MiddleCenter, 3);
            chl.rectTransform.Fill(70, 4, 14, 0);
            UIKit.Fit(chl, 18);
            challengeDot = UIKit.KitImg(chr, "badge", new Vector2(34, 34), "Dot").gameObject;
            ((RectTransform)challengeDot.transform).Place(1, 1, new Vector2(8, 8), new Vector2(34, 34), new Vector2(1, 1));
            var noLock = new GameObject("NoLock");
            noLock.transform.SetParent(chr, false);
            side["challenge"] = new SideBtn { b = chb, icon = chi, label = chl, open = () => true, lockIcon = noLock };
        }

        Text Pill(Vector2 pos, string icon, Vector2 iconSize, out RectTransform iconRt)
        {
            var bg = UIKit.KitImg(root, "plank_thin", new Vector2(290, 70), "Pill");
            bg.rectTransform.Place(1, 1, pos, new Vector2(290, 70), new Vector2(1, 1));
            var ic = UIKit.Img(bg.transform, Art.Get(icon), iconSize, "Icon");
            ic.rectTransform.Place(0, 0.5f, new Vector2(-10, 0), iconSize, new Vector2(0, 0.5f));
            iconRt = ic.rectTransform;
            var t = UIKit.Label(bg.transform, "0", 42, Color.white, TextAnchor.MiddleRight, 3);
            t.rectTransform.Fill(60, 0, 18, 0);
            return t;
        }

        /// <summary>"x2" — 광고 버프 배율 글자</summary>
        public static string BuffX(bool atk) => "x" + Tables.Buff(atk ? "buff_atk" : "buff_gold").mul.ToString("0.#");

        Button BuffBtn(Vector2 pos, string icon, string label, bool atk, out Text timeT)
        {
            var b = UIKit.TileBtn(root, Art.Get(icon), new Vector2(88, 94), 0.78f, () => Msg(GM.Buff(atk)), "Buff");
            ((RectTransform)b.transform).Place(1, 1, pos, new Vector2(88, 94), new Vector2(1, 1));
            var x3 = UIKit.Label(b.transform, BuffX(atk), 34, new Color(1f, 0.95f, 0.4f), TextAnchor.MiddleCenter, 3);
            x3.rectTransform.Place(0, 1, new Vector2(-16, 8), new Vector2(64, 40), new Vector2(0, 1));
            timeT = UIKit.Label(b.transform, label, 26, Color.white, TextAnchor.MiddleCenter, 2);
            timeT.rectTransform.Place(0.5f, 0, new Vector2(0, -16), new Vector2(160, 34), new Vector2(0.5f, 0.5f));
            return b;
        }

        // ---------- 아래: 총알 칸 · 강화 카드 (버튼 줄은 BuildNav) ----------
        // 높이(아래에서부터, 캔버스 단위): 버튼 줄 12~140 · 카드 150~374 · 카드 이름 376~420 · 총알 칸 428~532
        public const float BottomUiHeight = 540f;
        Image upgradePanel;
        const float CardY = 150f, CardScale = 0.88f, CardStep = 330f;
        const float SlotY = 428f, SlotH = 104f;
        readonly SlotView[] slotViews = new SlotView[SaveData.SlotCount];
        class SlotView { public RectTransform rt; public Image bg, frame, icon; public Text name; }

        void BuildBottom()
        {
            string[] names = { L.T("공격력"), L.T("공격속도"), L.T("돈 획득량") };
            string[] imgs = { "UI/card_atk", "UI/card_spd", "UI/card_gold" };
            // 세 카드를 묶는 판 (카드 아래 8 ~ 이름 위 8)
            upgradePanel = UIKit.KitImg(root, "panel_brown", new Vector2(CardStep * 3 + 24, 254 * CardScale + 66), "UpgradePanel");
            upgradePanel.rectTransform.Place(0.5f, 0, new Vector2(0, CardY - 10), new Vector2(CardStep * 3 + 24, 254 * CardScale + 66), new Vector2(0.5f, 0));
            upgradePanel.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                var c = new Card();
                // 카드는 원래 크기(340x254)로 만들고 받침을 줄여서 작게(안쪽 글자 자리 그대로)
                var holder = UIKit.Rect(root, "CardHolder" + i).Place(0.5f, 0, new Vector2(-CardStep + i * CardStep, CardY), new Vector2(340, 254), new Vector2(0.5f, 0));
                holder.localScale = Vector3.one * CardScale;
                var im = UIKit.Img(holder, Art.Get(imgs[i]), new Vector2(340, 254), "Card" + i);
                im.preserveAspect = false;
                c.img = im;
                c.rt = im.rectTransform.Place(0.5f, 0, Vector2.zero, new Vector2(340, 254), new Vector2(0.5f, 0));
                c.holder = holder;
                var hb = im.gameObject.AddComponent<HoldButton>();
                hb.action = () => { bool ok = GM.TryUpgrade(which); if (ok) { Refresh(); PopCard(which); } return ok; };
                c.name = UIKit.Fit(UIKit.Label(root, names[i], 34, Color.white, TextAnchor.MiddleCenter, 3), 20);
                c.name.rectTransform.Place(0.5f, 0, new Vector2(-CardStep + i * CardStep, CardY + 254 * CardScale - 2), new Vector2(300, 44), new Vector2(0.5f, 0));
                c.lv = UIKit.Label(c.rt, "Lv.0", 32, Color.white, TextAnchor.MiddleRight, 2);
                c.lv.rectTransform.Place(1, 1, new Vector2(-32, -60), new Vector2(170, 40), new Vector2(1, 0.5f));
                c.val = UIKit.Label(c.rt, "100", 50, Color.white, TextAnchor.MiddleCenter, 4);
                c.val.rectTransform.Place(0.5f, 1, new Vector2(-8, -112), new Vector2(240, 60), new Vector2(0.5f, 0.5f));
                c.cost = UIKit.Label(c.rt, "50", 36, Color.white, TextAnchor.MiddleCenter, 3);
                c.cost.rectTransform.Place(0.5f, 0, new Vector2(22, 78), new Vector2(150, 46), new Vector2(0.5f, 0.5f));
                cards[i] = c;
            }

            // 총알 1·2·3 칸 = 카피바라 1·2·3 이 장착한 총알 (누르면 무기 창)
            for (int i = 0; i < SaveData.SlotCount; i++)
            {
                var v = new SlotView();
                var bg = UIKit.KitImg(root, "panel_brown", new Vector2(300, SlotH), "AmmoSlot" + i);
                v.bg = bg;
                v.rt = bg.rectTransform.Place(0.5f, 0, new Vector2(-CardStep + i * CardStep, SlotY), new Vector2(300, SlotH), new Vector2(0.5f, 0));
                var btn = bg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    if (!GM.WeaponOpen) { Toast(GM.LockMsg("weapon")); return; }
                    Open<WeaponPopup>();
                });
                v.frame = UIKit.KitImg(v.rt, "outline", new Vector2(300, SlotH), "Frame");
                v.frame.rectTransform.Fill();
                v.frame.raycastTarget = false;
                v.icon = UIKit.Img(v.rt, null, new Vector2(84, 66), "Icon");
                v.icon.rectTransform.Place(0, 0.5f, new Vector2(58, 0), new Vector2(84, 66), new Vector2(0.5f, 0.5f));
                v.icon.preserveAspect = true;
                v.icon.raycastTarget = false;
                v.name = UIKit.Label(v.rt, "", 30, Color.white, TextAnchor.MiddleLeft, 3);
                v.name.rectTransform.Fill(108, 4, 10, 4);
                v.name.raycastTarget = false;
                v.name.supportRichText = true;
                // 긴 이름은 칸 안에서 글자를 줄여 맞춤
                v.name.horizontalOverflow = HorizontalWrapMode.Wrap;
                v.name.verticalOverflow = VerticalWrapMode.Truncate;
                v.name.resizeTextForBestFit = true;
                v.name.resizeTextMinSize = 18;
                v.name.resizeTextMaxSize = 30;
                slotViews[i] = v;
            }
        }

        void RefreshSlots()
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                var v = slotViews[i];
                int code = i < S.slots.Count ? S.slots[i] : -1;
                bool has = code >= 0;
                v.icon.gameObject.SetActive(has);
                if (has)
                {
                    var gc = WeaponPopup.GradeColor(Tables.GradeOf(code));
                    v.icon.sprite = WeaponPopup.TypeIcon(Tables.TypeOf(code));
                    v.frame.color = gc;
                    v.bg.color = Color.Lerp(Color.white, gc, 0.25f);
                    int lv = Stats.SlotLvEff(i);
                    string wn = Tables.Weapon(code).name, gn = Tables.GradeName(Tables.GradeOf(code));
                    v.name.text = (Tables.NameHasGrade(wn, gn)
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(gc)}>{L.T(wn)}</color>"
                        : $"<color=#{ColorUtility.ToHtmlStringRGB(gc)}>{gn}</color> {L.T(wn)}") + (lv > 0 ? $" <color=#FFD966>+{lv}</color>" : "");
                    v.name.rectTransform.offsetMin = new Vector2(108, 4);
                    v.name.alignment = TextAnchor.MiddleLeft;
                }
                else
                {
                    v.frame.color = new Color(1, 1, 1, 0f);
                    v.bg.color = new Color(1, 1, 1, 0.75f);
                    v.name.text = L.F("총알 {0}", i + 1) + "\n<size=24>" + L.T("비어 있음") + "</size>";
                    v.name.rectTransform.offsetMin = new Vector2(10, 4);
                    v.name.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        void PopCard(int i) { cards[i].rt.localScale = Vector3.one * 1.05f; }

        // ---------- 맨 아래 버튼 줄 (무기 · 스킨 · 보급품 · 보스 · 광산) ----------
        RectTransform navBar;
        const float NavY = 12f, NavH = 128f, NavW = 198f, NavStep = 210f;

        void BuildNav()
        {
            navBar = UIKit.Rect(root, "NavBar").Place(0.5f, 0, new Vector2(0, NavY), new Vector2(NavStep * 5, NavH), new Vector2(0.5f, 0));
            // 잠금 글 = Unlock_Roadmap 의 Lock_Toast_Message
            NavBtn(0, "weapon", "UI/icon_pistol", L.T("무기"), () => GM.WeaponOpen, "weapon", () => Open<WeaponPopup>());
            NavBtn(1, "skin", "Gen/capy_head", L.T("스킨"), () => GM.SkinOpen, "skin", () => Open<SkinPopup>());
            NavBtn(2, "fish", "Kit/i_chest_w", L.T("보급품"), () => GM.FishOpen, "supply", () => Open<SupplyPopup>());
            NavBtn(3, "boss", null, L.T("보스"), () => GM.BossRaidOpen, "boss_raid", () => Confirm(ConfirmPopup.Kind.Boss));
            NavBtn(4, "mine", "UI/icon_gem", L.T("광산"), () => GM.MineOpen, "mine", () => Confirm(ConfirmPopup.Kind.Mine));
        }

        void NavBtn(int i, string id, string icon, string label, Func<bool> open, string lockText, Action click)
        {
            var sb = new SideBtn { open = open, lockText = lockText };
            sb.b = UIKit.Btn(navBar, UIKit.PanelSmall, new Vector2(NavW, NavH), null, 0, () =>
            {
                if (!sb.open()) { Toast(GM.LockMsg(sb.lockText)); return; }
                click();
            }, "Nav_" + id);
            sb.b.GetComponent<Image>().pixelsPerUnitMultiplier = 1.3f;   // 판 테두리를 버튼 크기에 맞게
            var rt = ((RectTransform)sb.b.transform).Place(0.5f, 0, new Vector2(-NavStep * 2 + i * NavStep, 0), new Vector2(NavW, NavH), new Vector2(0.5f, 0));
            var isz = icon != null ? new Vector2(64, 58) : new Vector2(76, 72);
            Image ic = UIKit.Img(rt, icon != null ? Art.Get(icon) : Art.Zombie("ogre")[0], isz, "Icon");
            ic.rectTransform.Place(0.5f, 1, new Vector2(0, icon != null ? -50 : -48), isz, new Vector2(0.5f, 0.5f));
            sb.icon = ic;
            sb.label = UIKit.Fit(UIKit.Label(rt, label, 30, Color.white, TextAnchor.MiddleCenter, 3), 18);
            sb.label.rectTransform.Place(0.5f, 0, new Vector2(0, 28), new Vector2(NavW - 10, 38), new Vector2(0.5f, 0.5f));
            var lk = UIKit.KitImg(rt, "lock", new Vector2(30, 43), "Lock");
            lk.rectTransform.Place(0.5f, 1, new Vector2(34, -66), new Vector2(30, 43), new Vector2(0.5f, 0.5f));
            sb.lockIcon = lk.gameObject;
            sb.dot = UIKit.KitImg(rt, "badge", new Vector2(34, 34), "Dot").gameObject;
            ((RectTransform)sb.dot.transform).Place(1, 1, new Vector2(8, 8), new Vector2(34, 34), new Vector2(1, 1));
            sb.dot.SetActive(false);
            side[id] = sb;
        }

        /// <summary>아이콘 + 아래 글자 버튼 (왼쪽 위 상점)</summary>
        void IconBtn(string id, string icon, string label, Vector2 size, float x, float y, Func<bool> open, string lockText, Action click)
        {
            var sb = new SideBtn { open = open, lockText = lockText };
            size = new Vector2(88, 94);
            sb.b = UIKit.TileBtn(root, Art.Get(icon), size, 0.72f, () =>
            {
                if (!sb.open()) { Toast(GM.LockMsg(sb.lockText)); return; }
                click();
            }, "Top_" + id);
            var rt = ((RectTransform)sb.b.transform).Place(0, 1, new Vector2(24 + x, y), size, new Vector2(0, 1));
            sb.icon = rt.Find("Icon").GetComponent<Image>();
            sb.label = UIKit.Fit(UIKit.Label(rt, label, 28, Color.white, TextAnchor.MiddleCenter, 2), 18);
            sb.label.rectTransform.Place(0.5f, 0, new Vector2(0, -14), new Vector2(120, 34), new Vector2(0.5f, 0.5f));
            var lk = UIKit.KitImg(rt, "lock", new Vector2(27, 39), "Lock");
            lk.rectTransform.Place(1, 0, new Vector2(-6, 18), new Vector2(27, 39), new Vector2(0.5f, 0.5f));
            sb.lockIcon = lk.gameObject;
            sb.dot = UIKit.KitImg(rt, "badge", new Vector2(34, 34), "Dot").gameObject;   // 상점: 오늘 광고 다이아가 남았으면 빨간 점
            ((RectTransform)sb.dot.transform).Place(1, 1, new Vector2(8, 8), new Vector2(34, 34), new Vector2(1, 1));
            sb.dot.SetActive(false);
            side[id] = sb;
        }

        GameObject challengeDot;

        // ---------- 던전 표시 ----------
        void BuildDungeonHud()
        {
            dungeonGroup = UIKit.Rect(root, "DungeonHud").Place(0.5f, 1f, new Vector2(0, -110), new Vector2(560, 260), new Vector2(0.5f, 1f));
            var bg = UIKit.KitImg(dungeonGroup, "panel_brown", new Vector2(100, 100), "Bg");
            bg.color = new Color(1, 1, 1, 0.85f);
            bg.rectTransform.Fill(0, 60, 0, 0);
            timerText = UIKit.Label(dungeonGroup, "60", 80, Color.white, TextAnchor.MiddleCenter, 5);
            timerText.rectTransform.Place(0.5f, 1, new Vector2(0, -60), new Vector2(400, 100), new Vector2(0.5f, 0.5f));
            leftText = UIKit.Label(dungeonGroup, "", 40, UIKit.Gold, TextAnchor.MiddleCenter, 3);
            leftText.rectTransform.Place(0.5f, 1, new Vector2(0, -140), new Vector2(520, 50), new Vector2(0.5f, 0.5f));
            quitBtn = UIKit.ColorBtn(dungeonGroup, new Color(0.6f, 0.2f, 0.15f, 0.9f), new Vector2(160, 56), L.T("포기"), 30, () =>
            {
                ConfirmPopup.Ask(L.T("던전을 포기할까요?\n(사용한 티켓은 돌아오지 않습니다)"), () => GM.ExitDungeon());
            });
            ((RectTransform)quitBtn.transform).Place(0.5f, 0, new Vector2(0, 20), new Vector2(160, 56), new Vector2(0.5f, 0.5f));
            dungeonGroup.gameObject.SetActive(false);
        }

        // ---------- 튜토리얼 화살표 (첫 강화 전까지 공격력 카드를 가리킴) ----------
        void BuildTutorial()
        {
            var p = UIKit.Img(root, Art.Get("Gen/arrow"), new Vector2(120, 120), "TutorialArrow");   // 처음 쓰던 노란 화살표(icon_gen.py)
            paw = p.rectTransform;
            paw.Place(0.5f, 0, new Vector2(-300, 150), new Vector2(120, 120), new Vector2(0.5f, 0.5f));
            p.raycastTarget = false;
            var t = UIKit.Label(paw, L.T("눌러서 강화!"), 38, UIKit.Gold, TextAnchor.MiddleCenter, 3);
            t.rectTransform.Place(0.5f, 1, new Vector2(0, 0), new Vector2(300, 50), new Vector2(0.5f, 0));
        }

        // ---------- 절전모드 ----------
        void BuildEco()
        {
            var o = UIKit.Plain(root, new Color(0, 0, 0, 0.93f), "Eco");
            o.rectTransform.Fill();
            var b = o.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(ToggleEco);
            var t = UIKit.Label(o.transform, L.T("절전 모드\n\n화면을 누르면 돌아옵니다"), 44, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, 0);
            t.rectTransform.Fill();
            ecoInfo = UIKit.Label(o.transform, "", 40, new Color(1f, 0.9f, 0.5f, 0.8f), TextAnchor.MiddleCenter, 0);
            ecoInfo.rectTransform.Place(0.5f, 0.5f, new Vector2(0, -260), new Vector2(900, 160), new Vector2(0.5f, 0.5f));
            eco = o.gameObject;
            eco.SetActive(false);
        }
        Text ecoInfo;

        void ToggleEco()
        {
            GM.eco = !GM.eco;
            AudioManager.Muffle(GM.eco);
            eco.SetActive(GM.eco);
            Application.targetFrameRate = GM.eco ? 15 : 60;
        }

        // =====================================================================
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            refreshT -= dt;
            if (refreshT <= 0) { refreshT = 0.1f; Refresh(); }

            if (toastTime > 0)
            {
                toastTime -= dt;
                toastRt.gameObject.SetActive(toastTime > 0);
                var c = toastT.color; c.a = Mathf.Clamp01(toastTime / 0.3f); toastT.color = c;
            }
            if (bossBtn.gameObject.activeSelf) bossBtn.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * 0.06f);   // 콩닥콩닥
            if (toastPop > 0) { toastPop -= dt; toastRt.localScale = Vector3.one * (1f + Mathf.Max(0, toastPop) * 0.6f); }
            foreach (var c in cards) c.rt.localScale = Vector3.MoveTowards(c.rt.localScale, Vector3.one, dt * 0.6f);

            goldBump = Mathf.MoveTowards(goldBump, 0, dt * 2f);
            gemBump = Mathf.MoveTowards(gemBump, 0, dt * 2f);
            goldIcon.localScale = Vector3.one * (1 + goldBump * 0.3f);
            gemIcon.localScale = Vector3.one * (1 + gemBump * 0.3f);

            if (paw.gameObject.activeSelf)
                paw.anchoredPosition = new Vector2(-CardStep, CardY + 254 * CardScale + 56 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 22f);   // 공격력 카드 위에서 아래를 가리키며 콩콩

            if (GM.mode != GameManager.Mode.Stage)
            {
                timerText.text = Mathf.CeilToInt(GM.dungeonTime).ToString();
                timerText.color = GM.dungeonTime < 10 ? UIKit.Red : Color.white;
                leftText.text = GM.mode == GameManager.Mode.Mine ? L.T("다이아") + $" +{GM.dungeonGems:0.0}" : L.F("남은 좀비 {0}", GM.DungeonLeft);
            }
        }

        public void Bump(bool gem) { if (gem) gemBump = 1; else goldBump = 1; }

        public void Refresh()
        {
            if (S == null || GM == null) return;
            goldText.text = Num.F(S.gold);
            gemText.text = Num.Gem(S.gems);
            int[] lvs = { S.lvAtk, S.lvSpd, S.lvGold };
            for (int i = 0; i < 3; i++)
            {
                var c = cards[i];
                int lv = lvs[i];
                double v = i == 0 ? Stats.Power(lv) : i == 1 ? Stats.Speed(lv) : Stats.Money(lv);
                c.lv.text = "Lv." + lv;
                c.val.text = Num.F(v);
                double cost = Stats.Cost(lv);
                c.cost.text = Num.F(cost);
                bool can = S.gold >= cost;
                c.cost.color = can ? Color.white : UIKit.Red;
                c.img.color = can ? Color.white : new Color(0.82f, 0.82f, 0.82f);
            }
            bool hint = !S.tutorialDone || (GM.upgradeHint && S.gold >= Stats.Cost(S.lvAtk));
            paw.gameObject.SetActive(hint && GM.mode == GameManager.Mode.Stage);
            cards[0].name.enabled = !paw.gameObject.activeSelf;   // 안내 글자와 겹치지 않게

            bool menu = GM.MenuOpen;
            missionBtn.gameObject.SetActive(menu);
            bool buffOpen = GM.Unlocked("buff");
            missionDot.SetActive(GM.AnyMissionReady);
            ecoBtn.gameObject.SetActive(GM.Unlocked("eco") && GM.mode == GameManager.Mode.Stage);
            buffAtk.gameObject.SetActive(buffOpen);
            buffGold.gameObject.SetActive(buffOpen);
            double now = SaveData.NowD;
            buffAtkT.text = Stats.BuffAtk ? Num.Time(S.buffAtkEnd - now) : L.T("공격") + " " + BuffX(true);
            buffGoldT.text = Stats.BuffGold ? Num.Time(S.buffGoldEnd - now) : L.T("골드") + " " + BuffX(false);
            buffAtkT.color = Stats.BuffAtk ? UIKit.Gold : Color.white;
            buffGoldT.color = Stats.BuffGold ? UIKit.Gold : Color.white;

            foreach (var kv in side)
            {
                bool open = kv.Value.open();
                kv.Value.lockIcon.SetActive(!open);
                kv.Value.icon.color = open ? Color.white : new Color(0.45f, 0.45f, 0.45f);
            }
            var ch = side["challenge"];
            int cc = GM.ChallengeChapter;
            ch.label.text = cc > 0 ? L.F("도전 {0}", cc) : L.T("도전");
            ch.icon.color = cc > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            challengeDot.SetActive(cc > 0);
            S.RefreshDaily();
            side["mine"].label.text = GM.MineOpen ? $"{L.T("광산")} {S.mineTickets}/{Tables.Ci("MineTickets")}" : L.T("광산");
            side["boss"].label.text = GM.BossRaidOpen ? $"{L.T("보스")} {S.bossTickets}/{Tables.Ci("BossRaidTickets")}" : L.T("보스");
            side["mine"].dot.SetActive(GM.MineOpen && S.mineTickets > 0);
            side["boss"].dot.SetActive(GM.BossRaidOpen && S.bossTickets > 0);
            side["shop"].dot.SetActive(GM.Unlocked("shop") && GM.AdGemsLeft > 0);
            if (autoState)
            {
                autoState.text = S.autoUpgrade ? (S.autoOn ? "ON" : "OFF") : "";
                autoState.color = S.autoOn ? new Color(0.6f, 1f, 0.5f) : new Color(0.8f, 0.8f, 0.8f);
                autoBtn.image.color = S.autoUpgrade && !S.autoOn ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
            }
            RefreshSlots();

            pctFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.03f, GM.StageProgress), 1);
            pctFill.sprite = Art.Kit(GM.bossPhase ? "bar_red" : "bar_green");
            pctText.text = GM.bossPhase ? L.F("보스! {0}초", Mathf.CeilToInt(GM.bossTime)) : GM.BossFarm ? L.T("반복 사냥 중 — 강화 후 보스 도전!") : Mathf.FloorToInt(GM.StageProgress * 100) + "%";
            bool showBoss = GM.mode == GameManager.Mode.Stage && GM.BossFarm && !GM.bossPhase;
            if (bossBtn.gameObject.activeSelf != showBoss) bossBtn.gameObject.SetActive(showBoss);
            if (GM.eco && ecoInfo) ecoInfo.text = $"Stage {SaveData.StageName(S.stage)}\n{L.T("골드")} {Num.F(S.gold)}";

            foreach (var p in openPopups) p.Tick();
        }

        void OnDestroy() => Iap.Changed -= RefreshAuto;

        /// <summary>자동강화 버튼: 스테이지 화면 + (가졌거나 살 수 있을 때)</summary>
        void RefreshAuto()
        {
            if (!autoBtn) return;
            var row = Tables.Shop.Find(x => x.id == "auto_upgrade");
            bool show = GM.mode == GameManager.Mode.Stage && (S.autoUpgrade || Iap.CanSell(row));
            if (autoBtn.gameObject.activeSelf != show) autoBtn.gameObject.SetActive(show);
        }

        void RefreshStage()
        {
            bool stage = GM.mode == GameManager.Mode.Stage;
            stageGroup.gameObject.SetActive(stage);
            dungeonGroup.gameObject.SetActive(!stage);
            navBar.gameObject.SetActive(stage);
            RefreshAuto();
            ecoBtn.gameObject.SetActive(stage && GM.Unlocked("eco"));
            side["shop"].b.gameObject.SetActive(stage);
            side["challenge"].b.gameObject.SetActive(stage);
            foreach (var c in cards) { c.holder.gameObject.SetActive(stage); c.name.gameObject.SetActive(stage); }
            upgradePanel.gameObject.SetActive(stage);
            foreach (var v in slotViews) v.rt.gameObject.SetActive(stage);
            stageText.text = "Stage " + SaveData.StageName(S.stage);
            int sub = S.Sub - 1;
            for (int i = 0; i < 5; i++)
                nodes[i].color = i < sub ? new Color(0.98f, 0.78f, 0.45f) : i == sub ? Color.white : new Color(0.45f, 0.35f, 0.28f);
            if (GM.bossPhase) nodes[4].color = UIKit.Red;
            nodeMarker.anchoredPosition = new Vector2(-200 + sub * 100, -12);
            Refresh();
        }

        // =====================================================================
        float toastPop;

        public void Toast(string s) => ShowToast(s, Color.white);

        void ShowToast(string s, Color c)
        {
            if (string.IsNullOrEmpty(s)) return;
            toastT.text = s;
            toastT.color = c;
            toastPop = 0.15f;
            toastTime = 1.8f;
            toastRt.gameObject.SetActive(true);
            toastRt.SetAsLastSibling();
        }

        /// <summary>규칙 함수가 돌려준 오류 글이 있으면 보여 줌</summary>
        public void Msg(string err) { if (!string.IsNullOrEmpty(err)) Toast(err); }

        /// <summary>스테이지·보스·던전 알림 = 색 글자 토스트</summary>
        public void Banner(string s, Color c) => ShowToast(s, c);

        void Confirm(ConfirmPopup.Kind k) => ConfirmPopup.Dungeon(k);

        public void ShowResult(string title, bool win, double reward) => ResultPopup.Show(title, win, reward);

        // ---------- 팝업 ----------
        readonly Dictionary<Type, Popup> popups = new();

        public T Open<T>() where T : Popup
        {
            if (!popups.TryGetValue(typeof(T), out var p) || p == null)
            {
                p = Popup.Create<T>(root);
                popups[typeof(T)] = p;
            }
            p.Open();
            return (T)p;
        }

        public T Get<T>() where T : Popup => popups.TryGetValue(typeof(T), out var p) ? (T)p : null;
    }
}
