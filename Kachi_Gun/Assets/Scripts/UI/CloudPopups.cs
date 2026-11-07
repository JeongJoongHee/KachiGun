using System;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 클라우드 저장 창 (11-07, 설정 > [클라우드 저장]).
    ///  - 꺼진 빌드(App ID 없음): 안내만 · 연결 안 함: [구글 계정 연결] · 연결함: 계정 이름 · 마지막 저장 시각 · [지금 저장] · [불러오기]
    /// </summary>
    public class CloudPopup : Popup
    {
        protected override Vector2 Size => new(900, 820);
        protected override string Title => "클라우드 저장";
        Text body;
        Button left, right, center;

        protected override void Build()
        {
            body = UIKit.Label(content, "", 36, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            body.rectTransform.Fill(10, 150, 10, 10);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            left = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(360, 116), L.T("지금 저장"), 40, OnSave);
            ((RectTransform)left.transform).Place(0.5f, 0, new Vector2(-195, 10), new Vector2(360, 116), new Vector2(0.5f, 0));
            right = UIKit.ColorBtn(content, new Color(0.3f, 0.55f, 0.95f), new Vector2(360, 116), L.T("불러오기"), 40, OnLoad);
            ((RectTransform)right.transform).Place(0.5f, 0, new Vector2(195, 10), new Vector2(360, 116), new Vector2(0.5f, 0));
            center = UIKit.ColorBtn(content, new Color(0.45f, 0.7f, 0.3f), new Vector2(520, 116), L.T("구글 계정 연결"), 40, OnLink);
            ((RectTransform)center.transform).Place(0.5f, 0, new Vector2(0, 10), new Vector2(520, 116), new Vector2(0.5f, 0));
            UIKit.Fit(UIKit.LabelOf(center), 22);
        }

        public override void Tick() => Refresh();

        public override void Refresh()
        {
            bool on = CloudAuth.Configured, linked = CloudAuth.Linked && CloudAuth.SignedIn, ready = linked && CloudSync.Synced;
            string info = L.T("구글 계정에 진행을 저장해 두면\n폰을 바꾸거나 다시 설치해도 이어서 할 수 있어요.");
            if (!on) body.text = info + "\n\n<color=#B07A10>" + L.T("클라우드 저장은 다음 업데이트에서 열립니다") + "</color>";
            else if (!linked) body.text = info + (CloudSync.Busy ? "\n\n" + L.T("연결하는 중…") : "");
            else
            {
                long t = CloudSync.LastUploadAt;
                string when = t > 0 ? DateTimeOffset.FromUnixTimeSeconds(t).ToLocalTime().ToString("MM-dd HH:mm") : "-";
                body.text = L.F("연결된 계정: {0}", CloudAuth.DisplayName) + "\n\n" + L.F("마지막 클라우드 저장: {0}", when) + "\n<size=28>" + L.T("1분마다 · 게임을 내릴 때 자동으로 저장됩니다") + "</size>"
                            + (CloudSync.Busy ? "\n" + L.T("저장하는 중…") : "");
            }
            center.gameObject.SetActive(on && !ready);
            UIKit.LabelOf(center).text = CloudAuth.Linked ? L.T("다시 연결") : L.T("구글 계정 연결");
            left.gameObject.SetActive(ready);
            right.gameObject.SetActive(ready);
            UIKit.SetOn(left, !CloudSync.Busy);
            UIKit.SetOn(right, !CloudSync.Busy);
        }

        void OnLink()
        {
            if (CloudSync.Busy) return;
            CloudAuth.InteractiveLogin((ok, name) =>
            {
                if (!ok) { H.Toast(L.T("구글 계정에 연결하지 못했습니다")); Refresh(); return; }
                CloudAuth.SetLinked(name);
                CloudSync.AfterLogin(_ => { if (this) Refresh(); });
                Refresh();
            });
        }

        void OnSave()
        {
            CloudSync.Upload(ok => { H?.Toast(ok ? L.T("클라우드에 저장했습니다") : L.T("클라우드에 저장하지 못했습니다")); if (this) Refresh(); });
            Refresh();
        }

        void OnLoad()
        {
            ConfirmPopup.Ask($"<size=46>{L.T("불러오기")}</size>\n\n" + L.T("클라우드에 저장된 진행으로 바꿀까요?\n이 기기의 지금 진행은 지워집니다."), CloudSync.Download, "불러오기");
        }
    }

    /// <summary>클라우드와 이 기기의 진행이 서로 다를 때 (11-07): 두 쪽 요약을 보고 하나를 고름 → 한 번 더 확인</summary>
    public class CloudConflictPopup : Popup
    {
        protected override Vector2 Size => new(960, 1040);
        protected override string Title => "진행 고르기";
        protected override bool CloseOnDim => false;
        Text head, localT, cloudT;
        Action<bool> pick;

        protected override void Build()
        {
            head = UIKit.Label(content, L.T("이 기기와 클라우드의 진행이 다릅니다.\n이어서 할 진행을 골라 주세요."), 34, UIKit.Ink, TextAnchor.MiddleCenter, 0);
            head.rectTransform.Place(0.5f, 1, new Vector2(0, -60), new Vector2(860, 110), new Vector2(0.5f, 0.5f));
            localT = Box(-215, L.T("이 기기"), () => Choose(false));
            cloudT = Box(215, L.T("클라우드"), () => Choose(true));
        }

        Text Box(float x, string name, Action click)
        {
            var bg = UIKit.Row(content, name);
            bg.rectTransform.Place(0.5f, 1, new Vector2(x, -140), new Vector2(400, 660), new Vector2(0.5f, 1));
            var t = UIKit.Label(bg.transform, "", 32, Color.white, TextAnchor.UpperCenter, 2);
            t.rectTransform.Fill(16, 24, 16, 128);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var b = UIKit.ColorBtn(bg.transform, new Color(0.45f, 0.7f, 0.3f), new Vector2(340, 104), L.F("{0} 쓰기", name), 36, click);
            ((RectTransform)b.transform).Place(0.5f, 0, new Vector2(0, 16), new Vector2(340, 104), new Vector2(0.5f, 0));
            UIKit.Fit(UIKit.LabelOf(b), 20);
            return t;
        }

        static string Summary(string name, SaveData d) =>
            $"<size=40>{name}</size>\n\n" + L.T("스테이지") + " " + SaveData.StageName(d.stage) + "\n" + L.T("골드") + " " + Num.F(d.gold) + "\n" + L.T("다이아") + " " + Num.Gem(d.gems)
            + "\n" + L.F("코스튬 {0}개", d.skins.Count) + "\n<size=28>" + L.T("저장") + " " + (d.savedAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(d.savedAt).ToLocalTime().ToString("MM-dd HH:mm") : "-") + "</size>";

        public static void Show(SaveData cloud, SaveData local, Action<bool> pick)
        {
            var p = HUD.I.Open<CloudConflictPopup>();
            p.pick = pick;
            p.localT.text = Summary(L.T("이 기기"), local);
            p.cloudT.text = Summary(L.T("클라우드"), cloud);
        }

        void Choose(bool cloud)
        {
            var cb = pick;
            string other = cloud ? L.T("이 기기") : L.T("클라우드");
            ConfirmPopup.Ask(L.F("{0} 진행으로 이어서 할까요?\n{1}의 진행은 지워집니다.", cloud ? L.T("클라우드") : L.T("이 기기"), other), () =>
            {
                pick = null;
                base.Close();
                cb?.Invoke(cloud);
            }, "진행 고르기");
        }

        /// <summary>X · 뒤로 가기 = 이번엔 고르지 않음 (클라우드에 올리지도 않음 — 다음에 켤 때 다시 물음)</summary>
        public override void Close() { pick = null; base.Close(); }
    }
}
