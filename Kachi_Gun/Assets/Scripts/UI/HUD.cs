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
        Image bossBtnIcon, bossNavIcon;     // 11-11 보스가 6마리로 돌아가서 버튼 아이콘도 그 보스로
        readonly Image[] nodes = new Image[5];
        RectTransform nodeMarker, stageGroup, dungeonGroup;
        Button ecoBtn, missionBtn, quitBtn, autoBtn, bossBtn;
        Text autoState;
        GameObject missionDot;
        // 11-13 SS급 미사일탄 990원 바로 사기 (미션 아래) — 리본 "단 1번!" · 금색 판 + 미사일 + SS · 별 딱지 "1회 구매" · "SS 총알 지급" · 가격
        RectTransform starterRoot, starterPulse, starterRibbon, starterBurst;
        Button starterBtn;
        Text starterPrice;
        Image starterGlow, starterRing, starterFlash;
        readonly Image[] starterStars = new Image[2];
        float starterT, starterShowT = -1, starterFxT;   // starterFxT = 나타난 뒤 지난 시간 (Config Starter_Fx_Time 초까지만 강조)
        public const string StarterId = "starter_ss_missile";
        readonly Card[] cards = new Card[3];
        readonly Dictionary<string, SideBtn> side = new();
        Button buffAtk, buffGold;
        Text buffAtkT, buffGoldT;
        RectTransform paw;
        Image nodeGlow, nodeGlowIn, nodeRing;      // 11-11 요청_04 지금 스테이지 마디 네온 빛 + 고리
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
            fxLayer.gameObject.AddComponent<Canvas>();   // 11-08: 피해 숫자·날아가는 돈이 움직여도 위 화면 전체를 다시 모으지 않게 따로 판
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
            gameObject.AddComponent<Speech>();   // 11-11 요청_04 카피바라 말풍선

            GM.Changed += () => refreshT = 0;
            GM.StageChanged += RefreshStage;
            RefreshStage();
            Refresh();
            Prewarm();
        }

        // ---------- 위 ----------
        void BuildTop()
        {
            // 맨 위 진행 막대 (지금 서브 스테이지 처치 %) — 11-05: 금화 줄과 같은 나무 판 테두리 안에 어두운 홈 + 녹색 막대
            var frame = UIKit.KitImg(root, "plank_thin", new Vector2(1064, 52), "TopBar");
            frame.rectTransform.Place(0.5f, 1f, new Vector2(0, -34), new Vector2(1064, 52), new Vector2(0.5f, 1f));   // 아래 카피바라 표시가 글자를 안 가리게 얇게·위로
            var bar = UIKit.KitImg(frame.transform, "bar_track", new Vector2(10, 10), "Track");
            bar.rectTransform.Fill(10, 10, 10, 10);
            pctFill = UIKit.KitImg(bar.transform, "bar_green", new Vector2(10, 10), "Fill");
            pctFill.rectTransform.anchorMin = new Vector2(0, 0); pctFill.rectTransform.anchorMax = new Vector2(0.4f, 1);
            pctFill.rectTransform.offsetMin = new Vector2(2, 2); pctFill.rectTransform.offsetMax = new Vector2(-2, -2);
            pctText = UIKit.Label(frame.transform, "0%", 28, Color.white, TextAnchor.MiddleCenter, 2);
            pctText.rectTransform.Fill();

            // 설정
            var gear = UIKit.TileBtn(root, Art.Kit("i_gear_w"), new Vector2(92, 98), 0.6f, () => Open<SettingsPopup>(), "Settings");
            ((RectTransform)gear.transform).Place(0, 1, new Vector2(22, -108), new Vector2(92, 98), new Vector2(0, 1));
            // 챕터 마디 5개 + 진행 표시
            stageGroup = UIKit.Rect(root, "StageGroup").Place(0.5f, 1f, new Vector2(0, -110), new Vector2(560, 280), new Vector2(0.5f, 1f));
            var line = UIKit.KitImg(stageGroup, "bar_track", new Vector2(480, 22), "Line");
            line.rectTransform.Place(0.5f, 1f, new Vector2(0, -26), new Vector2(480, 22), new Vector2(0.5f, 0.5f));
            // 11-11 요청_04: 지금 있는 마디를 네온 빛으로 (바깥 흐린 빛 + 안쪽 진한 빛, 숨 쉬듯 커졌다 작아짐)
            //   마디보다 먼저 만들어 뒤에 그림 · 따로 판(Canvas)이라 깜빡여도 위 화면 전체를 다시 모으지 않음
            nodeGlow = UIKit.Img(stageGroup, Art.Glow, new Vector2(150, 150), "NodeGlow");
            nodeGlow.gameObject.AddComponent<Canvas>();
            nodeGlow.raycastTarget = false;
            nodeGlow.rectTransform.Place(0.5f, 1f, new Vector2(-200, -26), new Vector2(150, 150), new Vector2(0.5f, 0.5f));
            nodeGlowIn = UIKit.Img(nodeGlow.rectTransform, Art.Glow, new Vector2(84, 84), "In");
            nodeGlowIn.raycastTarget = false;
            nodeGlowIn.rectTransform.Place(0.5f, 0.5f, Vector2.zero, new Vector2(84, 84), new Vector2(0.5f, 0.5f));
            nodeRing = UIKit.Img(nodeGlow.rectTransform, Art.Ring, new Vector2(78, 78), "Ring");   // 네온 관 같은 고리 (마디 바깥)
            nodeRing.raycastTarget = false;
            nodeRing.rectTransform.Place(0.5f, 0.5f, Vector2.zero, new Vector2(78, 78), new Vector2(0.5f, 0.5f));
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

            // 11-13 초반 정착 상품 (Shop 표 starter_ss_missile · Reward Bullet:코드): 상점이 열리면 미션 아래에 보이고,
            // 누르면 바로 구매(폰 = 구글 결제창 · 에디터 = 가짜 결제 확인 창). 사면 · 결제가 꺼진 폰이면 숨김 (RefreshStarter)
            BuildStarter();

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
            var bi = bossBtnIcon = UIKit.Img(bossBtn.transform, Art.Zombie("ogre")[0], new Vector2(60, 58), "Icon");
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
            string[] names = { L.T("공격력"), L.T("공격속도"), L.T("금전 획득량") };
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
            // 11-07 사용자 "카피바라 위치와 동일하게": 화면의 위(동료1)·가운데(리더)·아래(동료2) 순서대로 왼쪽부터 동료1 | 리더 | 동료2 (총알 관리 창 카드와 같음)
            for (int i = 0; i < SaveData.SlotCount; i++)
            {
                var v = new SlotView();
                var bg = UIKit.KitImg(root, "panel_brown", new Vector2(300, SlotH), "AmmoSlot" + i);
                v.bg = bg;
                v.rt = bg.rectTransform.Place(0.5f, 0, new Vector2(-CardStep + SlotColumn(i) * CardStep, SlotY), new Vector2(300, SlotH), new Vector2(0.5f, 0));
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

        /// <summary>장착 칸 → 아래 줄 몇 번째 자리 (0 왼쪽 · 1 가운데 · 2 오른쪽): 리더 가운데 · 동료1 왼쪽 · 동료2 오른쪽</summary>
        public static int SlotColumn(int slot) => slot == 0 ? 1 : slot == 1 ? 0 : 2;

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
                    string[] roles = { "리더", "동료 1", "동료 2" };   // 11-07: 칸 순서가 동료1 | 리더 | 동료2 라 번호 대신 역할 이름
                    v.name.text = L.T(roles[Mathf.Clamp(i, 0, 2)]) + "\n<size=24>" + L.T("비어 있음") + "</size>";
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
            NavBtn(0, "weapon", "UI/icon_pistol", L.T("총알"), () => GM.WeaponOpen, "weapon", () => Open<WeaponPopup>());
            NavBtn(1, "skin", "Gen/capy_head", L.T("코스튬"), () => GM.SkinOpen, "skin", () => Open<SkinPopup>());
            NavBtn(2, "fish", "Kit/i_chest_w", L.T("보급품"), () => GM.FishOpen, "supply", () => Open<SupplyPopup>());
            NavBtn(3, "boss", null, L.T("보스"), () => GM.BossRaidOpen, "boss_raid", () => Confirm(ConfirmPopup.Kind.Boss));
            NavBtn(4, "mine", "UI/icon_gem", MineNav, () => GM.MineOpen, "mine", () => Confirm(ConfirmPopup.Kind.Mine));
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
            if (icon == null) bossNavIcon = ic;
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
            // 11-11 요청_04: 더 크고 붉게 (38 금색 → 54 빨강, 외곽선 굵게)
            var t = UIKit.Label(paw, L.T("눌러서 강화!"), 54, PawRed, TextAnchor.MiddleCenter, 5);
            t.rectTransform.Place(0.5f, 1, new Vector2(0, 0), new Vector2(440, 70), new Vector2(0.5f, 0));
            pawText = t;
        }

        Text pawText;
        public static readonly Color PawRed = new(1f, 0.3f, 0.26f);
        public Text PawText => pawText;

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
            if (Input.GetKeyDown(KeyCode.Escape)) Back();   // 안드로이드 뒤로 가기 = Escape
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
            foreach (var c in cards) if (c.rt.localScale != Vector3.one) c.rt.localScale = Vector3.MoveTowards(c.rt.localScale, Vector3.one, dt * 0.6f);

            // 11-08: 움직일 때만 크기를 넣음 (같은 값을 매 프레임 넣으면 화면을 다시 모음)
            if (goldBump > 0 || goldIcon.localScale.x != 1f) { goldBump = Mathf.MoveTowards(goldBump, 0, dt * 2f); goldIcon.localScale = Vector3.one * (1 + goldBump * 0.3f); }
            if (gemBump > 0 || gemIcon.localScale.x != 1f) { gemBump = Mathf.MoveTowards(gemBump, 0, dt * 2f); gemIcon.localScale = Vector3.one * (1 + gemBump * 0.3f); }

            if (nodeGlow.gameObject.activeInHierarchy)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                nodeGlow.rectTransform.localScale = Vector3.one * (0.85f + 0.3f * k);
                var gc = nodeGlowColor; gc.a = 0.5f + 0.4f * k; nodeGlow.color = gc;
                nodeGlowIn.color = new Color(1f, 1f, 0.92f, 0.55f + 0.35f * k);
                nodeRing.color = Color.Lerp(nodeGlowColor, Color.white, 0.35f) * new Color(1, 1, 1, 0.75f + 0.25f * k);
                nodeRing.rectTransform.localScale = Vector3.one / (0.85f + 0.3f * k) * (0.95f + 0.1f * k);   // 바깥 빛보다 덜 움직임
            }
            TickUnlockFx(dt);
            TickStarter(dt);

            if (paw.gameObject.activeSelf)
                paw.anchoredPosition = new Vector2(-CardStep, CardY + 254 * CardScale + 56 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 22f);   // 공격력 카드 위에서 아래를 가리키며 콩콩

            if (GM.mode != GameManager.Mode.Stage)
            {
                timerText.text = Mathf.CeilToInt(GM.dungeonTime).ToString();
                timerText.color = GM.dungeonTime < 10 ? UIKit.Red : Color.white;
                leftText.text = GM.mode == GameManager.Mode.Mine ? L.T("다이아") + $" +{GM.dungeonGems:0.0}" : L.F("남은 좀비 {0}", GM.DungeonLeft);
            }
        }

        /// <summary>폰 뒤로 가기 (11-04): 절전 모드면 돌아오기 → 열린 창이 있으면 맨 위 창 닫기(X 와 같음) → 없으면 게임 종료 확인 창</summary>
        public void Back()
        {
            if (GM != null && GM.eco) { ToggleEco(); return; }
            if (openPopups.Count > 0) { openPopups[openPopups.Count - 1].Close(); return; }
            Open<QuitPopup>();
        }

        /// <summary>아래 메뉴 버튼 이름: 영어 "Secret Dungeon 2/2" 는 칸에 안 들어가 글자가 너무 작아짐 → 버튼만 "Secret" (11-04)</summary>
        static string MineNav => L.IsEn ? "Secret" : L.T("비밀던전");

        public void Bump(bool gem) { if (gem) gemBump = 1; else goldBump = 1; }

        double shownGold = double.NaN, shownGems = double.NaN;
        readonly int[] shownLv = { -1, -1, -1 };

        public void Refresh()
        {
            if (S == null || GM == null) return;
            // 11-08: 값이 바뀔 때만 글자를 만듦 (0.1초마다 글자 10여 개를 새로 만들었음)
            if (S.gold != shownGold) { shownGold = S.gold; goldText.text = Num.F(S.gold); }
            if (S.gems != shownGems) { shownGems = S.gems; gemText.text = Num.Gem(S.gems); }
            for (int i = 0; i < 3; i++)
            {
                var c = cards[i];
                int lv = i == 0 ? S.lvAtk : i == 1 ? S.lvSpd : S.lvGold;
                double cost = Stats.Cost(lv);
                if (shownLv[i] != lv)
                {
                    shownLv[i] = lv;
                    double v = i == 0 ? Stats.Power(lv) : i == 1 ? Stats.Speed(lv) : Stats.Money(lv);
                    c.lv.text = "Lv." + lv;
                    c.val.text = Num.F(v);
                    c.cost.text = Num.F(cost);
                }
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
            RefreshStarter();
            ecoBtn.gameObject.SetActive(GM.Unlocked("eco") && GM.mode == GameManager.Mode.Stage);
            buffAtk.gameObject.SetActive(buffOpen);
            buffGold.gameObject.SetActive(buffOpen);
            double now = SaveData.NowD;
            buffAtkT.text = Stats.BuffAtk ? Num.Time(S.buffAtkEnd - now) : L.T("공격") + " " + BuffX(true);
            buffGoldT.text = Stats.BuffGold ? Num.Time(S.buffGoldEnd - now) : L.T("골드") + " " + BuffX(false);
            buffAtkT.color = Stats.BuffAtk ? UIKit.Gold : Color.white;
            buffGoldT.color = Stats.BuffGold ? UIKit.Gold : Color.white;

            CheckUnlockFx();
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
            side["mine"].label.text = GM.MineOpen ? $"{MineNav} {S.mineTickets}/{Tables.Ci("MineTickets")}" : MineNav;
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

        /// <summary>11-13 SS급 미사일탄 아이콘 (사용자 "단 1번 · 1회 구매 · SS 총알 지급 · 노출될 때 강조를 강하게").
        /// 글자는 그림에 굽지 않고 얹음(영어 화면). 매 프레임 움직이므로 따로 Canvas (위 화면 전체를 다시 모으지 않게)</summary>
        void BuildStarter()
        {
            var row = Tables.Shop.Find(x => x.id == StarterId);
            int code = Iap.BulletOf(row);
            int g = code >= 0 ? Tables.GradeOf(code) : 5;
            string gn = Tables.GradeName(g);
            starterRoot = UIKit.Rect(root, "Starter").Place(0, 1, new Vector2(10, -503), new Vector2(150, 208), new Vector2(0, 1));
            starterRoot.gameObject.AddComponent<Canvas>();
            starterRoot.gameObject.AddComponent<GraphicRaycaster>();   // 따로 Canvas 라 누르기를 받으려면 필요
            // 판 뒤 금빛 (깜빡임) — 판 가운데 (75, -92)
            starterGlow = UIKit.Img(starterRoot, Art.Glow, new Vector2(260, 260), "Glow");
            starterGlow.raycastTarget = false; starterGlow.preserveAspect = false;
            starterGlow.rectTransform.Place(0, 1, new Vector2(75, -92), new Vector2(260, 260), new Vector2(0.5f, 0.5f));
            // 통통 튀는 판 (누름 효과가 판 크기를 1 로 당겨도 이 부모 크기는 그대로)
            starterPulse = UIKit.Rect(starterRoot, "Pulse").Place(0, 1, new Vector2(75, -92), new Vector2(104, 114), new Vector2(0.5f, 0.5f));
            starterBtn = UIKit.KitBtn(starterPulse, "tile_gold", new Vector2(104, 114), null, 0, () => Iap.Buy(StarterId), "StarterBtn");
            ((RectTransform)starterBtn.transform).Place(0.5f, 0.5f, Vector2.zero, new Vector2(104, 114), new Vector2(0.5f, 0.5f));
            var ic = UIKit.Img(starterBtn.transform, code >= 0 ? WeaponPopup.TypeIcon(Tables.TypeOf(code)) : Art.Get("UI/icon_pistol"), new Vector2(70, 70), "Icon");
            ic.rectTransform.Place(0.5f, 0.5f, new Vector2(4, 0), new Vector2(70, 70), new Vector2(0.5f, 0.5f));
            ic.raycastTarget = false;
            var gt = UIKit.Label(starterBtn.transform, gn, 30, Tables.GradeColor(g), TextAnchor.MiddleCenter, 4);   // 등급 글자 (SS 빨강)
            gt.rectTransform.Place(0, 1, new Vector2(6, -12), new Vector2(56, 34), new Vector2(0, 1));   // 왼쪽 위 빈 곳 (미사일은 왼쪽 아래 → 오른쪽 위)
            gt.raycastTarget = false;
            // 판 번쩍 (1.6초마다 하얗게 한 번)
            starterFlash = UIKit.Img(starterBtn.transform, Art.Kit("tile_flash"), new Vector2(104, 114), "Flash");
            starterFlash.rectTransform.Place(0.5f, 0.5f, Vector2.zero, new Vector2(104, 114), new Vector2(0.5f, 0.5f));
            starterFlash.raycastTarget = false; starterFlash.color = new Color(1, 1, 1, 0);
            // 판 앞 네온 고리
            starterRing = UIKit.Img(starterPulse, Art.Ring, new Vector2(176, 186), "Ring");
            starterRing.raycastTarget = false; starterRing.preserveAspect = false;
            starterRing.rectTransform.Place(0.5f, 0.5f, Vector2.zero, new Vector2(176, 186), new Vector2(0.5f, 0.5f));   // 판 테두리 바깥에
            // 판 오른쪽 별 딱지 "1회 구매" (딱지만 천천히 돎 · 리본 아래라 가려지지 않게)
            starterBurst = UIKit.KitImg(starterPulse, "burst_red", new Vector2(64, 64), "Burst").rectTransform;
            starterBurst.Place(1, 0.5f, new Vector2(10, 4), new Vector2(64, 64), new Vector2(0.5f, 0.5f));
            starterBurst.GetComponent<Image>().raycastTarget = false;
            var bt = UIKit.Label(starterPulse, L.T("1회 구매"), 18, Color.white, TextAnchor.MiddleCenter, 2.5f);
            bt.rectTransform.Place(1, 0.5f, new Vector2(10, 4), new Vector2(46, 46), new Vector2(0.5f, 0.5f));
            bt.horizontalOverflow = HorizontalWrapMode.Wrap; bt.lineSpacing = 0.85f; bt.raycastTarget = false;
            // 위 빨간 리본 "단 1번!" (살랑살랑)
            starterRibbon = UIKit.KitImg(starterRoot, "ribbon_red", new Vector2(150, 48), "Ribbon").rectTransform;
            starterRibbon.Place(0, 1, new Vector2(75, -20), new Vector2(150, 48), new Vector2(0.5f, 0.5f));
            starterRibbon.GetComponent<Image>().raycastTarget = false;
            var rtx = UIKit.Fit(UIKit.Label(starterRibbon, L.T("단 1번!"), 26, Color.white, TextAnchor.MiddleCenter, 3), 16);
            rtx.rectTransform.Place(0.5f, 0.5f, new Vector2(0, 4), new Vector2(110, 34), new Vector2(0.5f, 0.5f));
            rtx.raycastTarget = false;
            // 반짝이 별 2개
            for (int i = 0; i < 2; i++)
            {
                starterStars[i] = UIKit.KitImg(starterRoot, "star_s", new Vector2(30, 30), "Spark" + i);
                starterStars[i].raycastTarget = false;
                starterStars[i].rectTransform.Place(0, 1, i == 0 ? new Vector2(14, -70) : new Vector2(30, -150), new Vector2(30, 30), new Vector2(0.5f, 0.5f));
            }
            // 아래 "SS 총알 지급" · 가격
            var cap = UIKit.Fit(UIKit.Label(starterRoot, L.F("{0} 총알 지급", gn), 24, Color.white, TextAnchor.MiddleCenter, 3), 16);
            cap.rectTransform.Place(0, 1, new Vector2(75, -164), new Vector2(160, 30), new Vector2(0.5f, 0.5f));
            cap.raycastTarget = false;
            starterPrice = UIKit.Fit(UIKit.Label(starterRoot, "", 28, UIKit.Gold, TextAnchor.MiddleCenter, 3), 18);
            starterPrice.rectTransform.Place(0, 1, new Vector2(75, -191), new Vector2(160, 32), new Vector2(0.5f, 0.5f));
            starterPrice.raycastTarget = false;
            starterRoot.gameObject.SetActive(false);
        }

        /// <summary>SS급 미사일탄 아이콘: 스테이지 화면 + 상점 열림 + 아직 안 삼 + 살 수 있음(스토어에 상품이 있음)
        /// + 11-13 홈 화면에 처음 보인 때(S.starterSeenAt)부터 Config Starter_Home_Hours(72시간) 안. 그 뒤엔 상점 탭에만 있음</summary>
        void RefreshStarter()
        {
            if (!starterRoot) return;
            var row = Tables.Shop.Find(x => x.id == StarterId);
            bool show = row != null && GM.mode == GameManager.Mode.Stage && GM.Unlocked("shop") && !Iap.Owned(row) && Iap.CanSell(row);
            if (show && StarterHomeExpired) show = false;
            if (show && S.starterSeenAt == 0) S.starterSeenAt = SaveData.Now;   // 처음 보인 때 (여기서부터 72시간)
            if (starterRoot.gameObject.activeSelf != show)
            {
                starterRoot.gameObject.SetActive(show);
                if (show) { starterShowT = 0; starterFxT = 0; starterSettled = false; }   // 나타나는 순간 크게 튀어나옴 → 강조 5초
            }
            if (show) { string pt = Iap.PriceText(row); if (starterPrice.text != pt) starterPrice.text = pt; }
        }

        /// <summary>11-13 홈 화면 노출 시간이 지남 (처음 보인 때부터 Starter_Home_Hours 시간 · 0 = 계속 보임)</summary>
        public static bool StarterHomeExpired
        {
            get
            {
                double h = Tables.C("Starter_Home_Hours", 72);
                return h > 0 && S.starterSeenAt > 0 && SaveData.Now - S.starterSeenAt >= h * 3600;
            }
        }

        bool starterSettled;

        /// <summary>강조 움직임: 나타날 때 크게 튀어나옴(0.6초) → 통통 · 뒤 금빛 깜빡임 · 네온 고리 · 리본 살랑 · 별 딱지 돎 · 반짝이.
        /// 11-13 사용자 "계속 나오지 말고 5초 정도만": 나타난 뒤 Starter_Fx_Time 초 동안만 움직이고 끝 0.5초에 잦아들어 멈춘 채로 보임
        /// (다시 나타날 때 = 던전에서 돌아옴 · 다시 켬 → 또 5초)</summary>
        void TickStarter(float dt)
        {
            if (!starterRoot || !starterRoot.gameObject.activeInHierarchy) return;
            if (StarterHomeExpired) { RefreshStarter(); return; }   // 화면을 켜 둔 채 72시간이 지나도 사라지게
            if (starterSettled) return;   // 강조 끝 = 멈춘 그림 그대로 (매 프레임 다시 그리지 않음)
            float fx = Mathf.Max(0f, (float)Tables.C("Starter_Fx_Time", 5));
            starterFxT += dt;
            float amp = Mathf.Clamp01((fx - starterFxT) / 0.5f);   // 1 → 끝 0.5초에 0
            if (amp <= 0f && starterShowT < 0) starterSettled = true;
            starterT += dt;
            float k = Mathf.Abs(Mathf.Sin(starterT * 4f)) * amp;
            float pop = 1f;
            if (starterShowT >= 0)
            {
                starterShowT += dt;
                float u = Mathf.Clamp01(starterShowT / 0.6f);
                pop = u < 1f ? Mathf.Lerp(0.2f, 1f, u) + Mathf.Sin(u * Mathf.PI) * 0.45f : 1f;   // 작게 → 크게 → 제자리
                if (u >= 1f) starterShowT = -1;
            }
            starterPulse.localScale = Vector3.one * (pop * (1f + 0.09f * k));
            starterGlow.rectTransform.localScale = Vector3.one * (pop * (0.9f + 0.35f * k));
            starterGlow.color = new Color(1f, 0.78f, 0.22f, 0.8f + 0.2f * k);
            starterRing.color = new Color(1f, 0.97f, 0.7f, 0.75f + 0.25f * k);
            starterRing.rectTransform.localScale = Vector3.one * (1f + 0.1f * k);
            starterRibbon.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(starterT * 3f) * 5f * amp);
            starterRibbon.localScale = Vector3.one * pop;
            starterBurst.localRotation = Quaternion.Euler(0, 0, -starterT * 40f);
            float ft = starterT % 1.6f;
            starterFlash.color = new Color(1, 1, 1, ft < 0.25f ? 0.7f * Mathf.Sin(ft / 0.25f * Mathf.PI) * amp : 0f);
            for (int i = 0; i < starterStars.Length; i++)
            {
                float s2 = Mathf.Max(0f, Mathf.Sin(starterT * 3.2f + i * Mathf.PI));
                starterStars[i].rectTransform.localScale = Vector3.one * (s2 * amp);
                starterStars[i].rectTransform.localRotation = Quaternion.Euler(0, 0, starterT * 90f);
            }
        }

        static string BossId(string id) => string.IsNullOrEmpty(id) || id == "-" ? "ogre" : id;

        void RefreshStage()
        {
            bool stage = GM.mode == GameManager.Mode.Stage;
            stageGroup.gameObject.SetActive(stage);
            dungeonGroup.gameObject.SetActive(!stage);
            navBar.gameObject.SetActive(stage);
            if (bossBtnIcon) bossBtnIcon.sprite = Art.Zombie(BossId(Tables.Stage((S.Chapter - 1) * 5 + 4).bossId))[0];   // 이 챕터 보스
            if (bossNavIcon) bossNavIcon.sprite = Art.Zombie(BossId(Tables.BossRaid(S.bossLv).bossId))[0];               // 다음 보스 던전 보스
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
            nodeGlow.rectTransform.anchoredPosition = new Vector2(-200 + sub * 100, -26);
            nodeGlowColor = GM.bossPhase ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.86f, 0.32f);   // 보스 = 빨강 · 평소 = 노란 네온
            Refresh();
        }

        // =====================================================================
        float toastPop;

        public void Toast(string s) => ShowToast(s, Color.white);

        void ShowToast(string s, Color c, float time = 1.8f, bool banner = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            toastRt.anchoredPosition = banner ? BannerPos() : new Vector2(0, 180);
            toastT.text = s;
            toastT.color = c;
            toastPop = 0.15f;
            toastTime = time;
            toastRt.gameObject.SetActive(true);
            toastRt.SetAsLastSibling();
        }

        /// <summary>규칙 함수가 돌려준 오류 글이 있으면 보여 줌</summary>
        public void Msg(string err) { if (!string.IsNullOrEmpty(err)) Toast(err); }

        /// <summary>스테이지·보스·던전 알림 = 색 글자 토스트</summary>
        public void Banner(string s, Color c) => ShowToast(s, c, 1.8f, true);

        /// <summary>11-14 요청_05: 스테이지·보스 알림이 가운데 카피바라 말풍선과 겹침 → 맨 위 카피바라 머리(그림 위 끝)보다
        /// Banner_Capy_Gap 만큼 위 (알림 판 높이 100 의 아래 끝 기준). 분대가 없으면 예전 자리</summary>
        Vector2 BannerPos()
        {
            float top = float.NegativeInfinity;
            if (Squad.I) foreach (var c in Squad.I.cats) if (c && c.sr && c.gameObject.activeInHierarchy) top = Mathf.Max(top, c.sr.bounds.max.y);
            if (float.IsNegativeInfinity(top) || Fx.I == null || Field.I == null) return new Vector2(0, 180);
            float y = Fx.I.WorldToUi(new Vector2(0, top)).y + (float)Tables.C("Banner_Capy_Gap", 40) + 50f;
            return new Vector2(0, Mathf.Max(180f, y));
        }
        public Vector2 ToastPos => toastRt.anchoredPosition;
        public RectTransform ToastRect => toastRt;
        public Vector2 TopCapyHeadUi()
        {
            float top = float.NegativeInfinity;
            if (Squad.I) foreach (var c in Squad.I.cats) if (c && c.sr && c.gameObject.activeInHierarchy) top = Mathf.Max(top, c.sr.bounds.max.y);
            return float.IsNegativeInfinity(top) ? Vector2.zero : Fx.I.WorldToUi(new Vector2(0, top));
        }

        void Confirm(ConfirmPopup.Kind k) => ConfirmPopup.Dungeon(k);

        public void ShowResult(string title, bool win, double reward) => ResultPopup.Show(title, win, reward);

        // ---------- 11-11 요청_04 해금: 알림 글을 더 오래(Unlock_Toast_Time) + 열린 버튼 뒤 빛 깜빡임 · 통통 (Unlock_Glow_Time) ----------
        Color nodeGlowColor = new(1f, 0.86f, 0.32f);
        public Color NodeGlowColor => nodeGlow ? nodeGlow.color : Color.clear;
        public Vector2 NodeGlowPos => nodeGlow ? nodeGlow.rectTransform.anchoredPosition : Vector2.zero;
        readonly Dictionary<string, bool> wasOpen = new();
        class UnlockFx { public RectTransform target; public Image glow, ring; public float t; }
        readonly List<UnlockFx> unlockFx = new();
        public int UnlockFxCount => unlockFx.Count;
        public float ToastLeft => toastRt.gameObject.activeSelf ? toastTime : 0;
        public string ToastText => toastRt.gameObject.activeSelf ? toastT.text : null;

        /// <summary>해금 id → 강조할 버튼들 (표 Unlock_Roadmap 의 Content_Id)</summary>
        List<RectTransform> UnlockTargets(string id)
        {
            var l = new List<RectTransform>();
            string sk = id == "supply" ? "fish" : id == "boss_raid" ? "boss" : id;
            if (side.TryGetValue(sk, out var sb)) l.Add((RectTransform)sb.b.transform);
            if (id == "mission") l.Add((RectTransform)missionBtn.transform);
            if (id == "eco") l.Add((RectTransform)ecoBtn.transform);
            if (id == "buff") { l.Add((RectTransform)buffAtk.transform); l.Add((RectTransform)buffGold.transform); }
            return l;
        }

        /// <summary>잠겼던 기능이 열리는 순간(처음 본 값은 기억만) → 알림 글(더 오래) + 버튼 빛. 다이아로 여는 총알 메뉴도 같이 잡힘</summary>
        void CheckUnlockFx()
        {
            string msg = null;
            foreach (var u in Tables.Unlocks)
            {
                bool open = GM.Unlocked(u.id);
                bool had = wasOpen.TryGetValue(u.id, out var was);
                wasOpen[u.id] = open;
                if (!had || was || !open) continue;
                UnlockNow(u.id);
                if (!string.IsNullOrEmpty(u.bannerMsg) && u.bannerMsg != "-") msg = L.T(u.bannerMsg);
            }
            if (msg != null) ShowToast(msg, UIKit.Gold, Mathf.Max(1.8f, (float)Tables.C("Unlock_Toast_Time", 3.5)));
        }

        /// <summary>그 기능 버튼을 강조 (버튼 뒤 노란 빛 깜빡임 + 버튼 통통)</summary>
        public void UnlockNow(string id)
        {
            foreach (var rt in UnlockTargets(id))
            {
                if (!rt) continue;
                unlockFx.RemoveAll(f => { if (f.target != rt) return false; if (f.glow) Destroy(f.glow.gameObject); if (f.ring) Destroy(f.ring.gameObject); return true; });
                var g = UIKit.Img(rt.parent, Art.Glow, rt.sizeDelta * 2.6f, "UnlockGlow");
                g.raycastTarget = false;
                g.preserveAspect = false;          // 버튼 모양대로 타원
                var pf = rt.GetComponent<PressFx>(); if (pf) pf.enabled = false;   // 누름 효과가 크기를 1 로 당기지 않게
                g.gameObject.AddComponent<Canvas>();
                var gr = g.rectTransform;
                gr.anchorMin = rt.anchorMin; gr.anchorMax = rt.anchorMax; gr.pivot = new Vector2(0.5f, 0.5f);
                gr.sizeDelta = rt.sizeDelta * 2.6f;
                gr.anchoredPosition = rt.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - rt.pivot, rt.sizeDelta);   // 버튼 가운데
                gr.SetSiblingIndex(rt.GetSiblingIndex());   // 버튼 뒤
                // 버튼 앞: 네온 고리(버튼 모양 타원) — 뒤 빛은 버튼 판에 가려 가장자리만 보여서
                var r = UIKit.Img(rt.parent, Art.Ring, rt.sizeDelta * 1.45f, "UnlockRing");
                r.raycastTarget = false; r.preserveAspect = false;
                r.gameObject.AddComponent<Canvas>();
                var rr = r.rectTransform;
                rr.anchorMin = gr.anchorMin; rr.anchorMax = gr.anchorMax; rr.pivot = gr.pivot;
                rr.sizeDelta = rt.sizeDelta * 1.45f; rr.anchoredPosition = gr.anchoredPosition;
                rr.SetSiblingIndex(rt.GetSiblingIndex() + 1);
                unlockFx.Add(new UnlockFx { target = rt, glow = g, ring = r });
            }
        }

        public void StopUnlockFx() { foreach (var f in unlockFx) f.t = 1e6f; }

        void TickUnlockFx(float dt)
        {
            if (unlockFx.Count == 0) return;
            float life = Mathf.Max(1f, (float)Tables.C("Unlock_Glow_Time", 6));
            for (int i = unlockFx.Count - 1; i >= 0; i--)
            {
                var f = unlockFx[i];
                f.t += dt;
                if (f.t >= life || !f.target)
                {
                    if (f.target) { f.target.localScale = Vector3.one; var pf = f.target.GetComponent<PressFx>(); if (pf) pf.enabled = true; }
                    if (f.glow) Destroy(f.glow.gameObject);
                    if (f.ring) Destroy(f.ring.gameObject);
                    unlockFx.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Abs(Mathf.Sin(f.t * 5f));
                float fade = Mathf.Clamp01((life - f.t) / 0.6f);
                f.target.localScale = Vector3.one * (1f + 0.12f * k * fade);
                bool vis = f.target.gameObject.activeInHierarchy;
                if (f.glow.gameObject.activeSelf != vis) { f.glow.gameObject.SetActive(vis); f.ring.gameObject.SetActive(vis); }
                f.ring.color = new Color(1f, 0.95f, 0.6f, (0.75f + 0.25f * k) * fade);
                f.ring.rectTransform.localScale = Vector3.one * (1f + 0.12f * k * fade);   // 버튼과 같이 통통
                f.glow.color = new Color(1f, 0.9f, 0.4f, (0.7f + 0.3f * k) * fade);
                f.glow.rectTransform.localScale = Vector3.one * (0.9f + 0.2f * k);
            }
        }

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

        // 11-08: 자주 여는 창을 켜는 로딩 안(첫 화면이 나오기 전)에 미리 만들고, 한 번 켜서 글자까지 그려 둔 뒤 닫아 둠
        //        — 처음 열 때(창 만들기 + 한글 글자 그리기) 멈칫하던 것 (PC 측정 첫 열기 103ms). 열 때는 예전과 같음
        void Make<T>() where T : Popup
        {
            if (popups.TryGetValue(typeof(T), out var p) && p != null) return;
            popups[typeof(T)] = Popup.Create<T>(root);
        }

        void Prewarm()
        {
            Make<WeaponPopup>(); Make<WeaponInfoPopup>(); Make<SkinPopup>(); Make<SupplyPopup>();
            Make<ShopPopup>(); Make<MissionPopup>(); Make<ConfirmPopup>(); Make<ResultPopup>();
            var made = new List<Popup>(popups.Values);
            foreach (var p in made) p.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();                   // 글자 그물·글꼴 그림을 지금 만듦 (화면에 그려지기 전에 다시 닫음)
            foreach (var p in made) p.gameObject.SetActive(false);
        }
    }
}
