using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>UI 를 코드로 만드는 도우미. 글자는 흰색 + 진한 갈색 외곽선(참고 게임처럼)</summary>
    public static class UIKit
    {
        public static readonly Color Brown = new(0.24f, 0.13f, 0.07f);
        public static readonly Color Cream = new(1f, 0.96f, 0.86f);
        public static readonly Color Gold = new(1f, 0.85f, 0.3f);
        public static readonly Color Green = new(0.55f, 0.9f, 0.4f);
        public static readonly Color Red = new(1f, 0.42f, 0.38f);
        public static readonly Color Sky = new(0.55f, 0.85f, 1f);
        public static readonly Color Dim = new(0f, 0f, 0f, 0.9f);
        /// <summary>양피지 위 글자색(진한 갈색)</summary>
        public static readonly Color Ink = new(0.33f, 0.19f, 0.1f);

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>앵커 한 점(ax,ay: 0~1) 기준 위치·크기</summary>
        public static RectTransform Place(this RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = pivot ?? new Vector2(ax, ay);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Img(Transform parent, Sprite s, Vector2 size, string name = "Img")
        {
            var rt = Rect(parent, name);
            rt.sizeDelta = size;
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = s;
            if (s != null && s.border != Vector4.zero) im.type = Image.Type.Sliced;
            im.preserveAspect = s != null && s.border == Vector4.zero;
            return im;
        }

        public static Image Box(Transform parent, Color c, string name = "Box")
        {
            var rt = Rect(parent, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Art.Get("Gen/round", 100f, null, new Vector4(16, 16, 16, 16));
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 1f;
            im.color = c;
            return im;
        }

        public static Image Plain(Transform parent, Color c, string name = "Plain")
        {
            var rt = Rect(parent, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.color = c;
            return im;
        }

        public static Text Label(Transform parent, string s, int size, Color? c = null, TextAnchor anchor = TextAnchor.MiddleCenter, float outline = 3f)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.text = s;
            t.fontSize = size;
            t.color = c ?? Color.white;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline > 0)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = Brown;
                o.effectDistance = new Vector2(outline, -outline);
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = Brown;
                sh.effectDistance = new Vector2(0, -outline - 1);
            }
            return t;
        }

        /// <summary>그림 버튼 + 글자. 누르면 살짝 작아짐</summary>
        public static Button Btn(Transform parent, Sprite s, Vector2 size, string label, int fontSize, Action onClick, string name = "Btn")
        {
            var im = Img(parent, s, size, name);
            im.preserveAspect = false;
            var b = im.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            im.gameObject.AddComponent<PressFx>();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(im.transform, label, fontSize);
                t.rectTransform.Fill();
                t.name = "Label";
            }
            return b;
        }

        /// <summary>색으로 고르는 UI 팩 버튼: 초록·파랑 계열 = 초록 버튼, 주황·갈색 = 나무 버튼, 빨강 = 붉은 나무 버튼, 회색 = 꺼진 초록 버튼</summary>
        public static Button ColorBtn(Transform parent, Color c, Vector2 size, string label, int fontSize, Action onClick)
        {
            var im = KitImg(parent, KitFor(c, out var tint), size, "ColorBtn");
            im.color = tint;
            var b = im.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            im.gameObject.AddComponent<PressFx>();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Label(im.transform, label, fontSize);
            t.rectTransform.Fill(6, 4, 6, 4);
            t.name = "Label";
            return b;
        }

        public static Text LabelOf(Button b) => b.transform.Find("Label")?.GetComponent<Text>();

        /// <summary>칸보다 글자가 길면 글자 크기를 줄여 맞춤 (영어처럼 긴 글자용). 최대 = 지금 크기</summary>
        public static Text Fit(Text t, int min = 16)
        {
            if (!t) return t;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = Mathf.Min(min, t.fontSize);
            t.resizeTextForBestFit = true;
            return t;
        }

        // ---------- UI 팩 (Art/Kit) ----------
        public static readonly Color Off = new(0.62f, 0.62f, 0.6f);

        static string KitFor(Color c, out Color tint)
        {
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            tint = Color.white;
            if (sat < 0.25f) { tint = Off; return "btn_green"; }                 // 회색 = 꺼짐
            if (h > 0.18f && h < 0.75f) return "btn_green";                     // 초록·파랑
            if (h < 0.03f || h > 0.92f) { tint = new Color(1f, 0.62f, 0.55f); return "plank"; }   // 빨강
            return "plank";                                                     // 주황·갈색
        }

        /// <summary>UI 팩 그림 (9-slice 는 알아서)</summary>
        public static Image KitImg(Transform parent, string kit, Vector2 size, string name = "Kit")
        {
            var im = Img(parent, Art.Kit(kit), size, name);
            im.preserveAspect = im.type != Image.Type.Sliced;
            return im;
        }

        /// <summary>UI 팩 버튼 + 글자</summary>
        public static Button KitBtn(Transform parent, string kit, Vector2 size, string label, int fontSize, Action onClick, string name = "KitBtn")
        {
            var im = KitImg(parent, kit, size, name);
            im.preserveAspect = false;
            var b = im.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            im.gameObject.AddComponent<PressFx>();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(im.transform, label, fontSize);
                t.rectTransform.Fill(10, 8, 10, 4);
                t.name = "Label";
            }
            return b;
        }

        /// <summary>나무 받침 + 아이콘 버튼 (HUD 의 설정·상점·미션·버프)</summary>
        public static Button TileBtn(Transform parent, Sprite icon, Vector2 size, float iconScale, Action onClick, string name = "TileBtn")
        {
            var b = KitBtn(parent, "tile", size, null, 0, onClick, name);
            var ic = Img(b.transform, icon, size * iconScale, "Icon");
            ic.rectTransform.Place(0.5f, 0.5f, new Vector2(0, size.y * 0.04f), size * iconScale, new Vector2(0.5f, 0.5f));
            ic.raycastTarget = false;
            return b;
        }

        /// <summary>켜짐/꺼짐 버튼 모양 (초록 / 회색)</summary>
        public static void SetOn(Button b, bool on)
        {
            var im = b.GetComponent<Image>();
            im.sprite = Art.Kit("btn_green");
            im.color = on ? Color.white : Off;
        }

        /// <summary>창 안의 줄·칸 바탕(둥근 나무판)</summary>
        public static Image Row(Transform parent, string name = "Row", bool dark = false) => KitImg(parent, dark ? "wood_dark" : "wood", new Vector2(100, 100), name);

        // 판 그림 = UI 팩 (리소스/ 의 도트 UI → tools/ui_kit.py)
        public static Sprite Plank => Art.Kit("plank");
        public static Sprite PlankThin => Art.Kit("plank_thin");
        public static Sprite Panel => Art.Kit("window");
        public static Sprite PanelSmall => Art.Kit("wood");
        public static Sprite Frame => Art.Kit("panel_brown");
    }

    /// <summary>누르면 작아졌다 돌아오는 효과</summary>
    public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        bool down;
        void Update()
        {
            float want = down ? 0.92f : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x, want, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1);
        }
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnPointerExit(PointerEventData e) => down = false;
    }

    /// <summary>누르고 있으면 계속 실행 (강화 연속 구매)</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Func<bool> action;
        bool down;
        float t, rate;
        int count;

        public void OnPointerDown(PointerEventData e)
        {
            down = true; t = 0.38f; rate = 0.12f; count = 0;
            action?.Invoke();
        }
        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnPointerExit(PointerEventData e) => down = false;

        void Update()
        {
            float target = down ? 0.93f : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1);
            if (!down) return;
            t -= Time.unscaledDeltaTime;
            if (t > 0) return;
            count++;
            rate = Mathf.Max(0.03f, 0.12f - count * 0.006f);
            t = rate;
            if (action != null && !action()) { /* 돈이 모자라면 기다림 */ }
        }
    }
}
