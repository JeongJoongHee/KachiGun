using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>
    /// 씬 시작점: 맵·분대·게임 진행·효과·UI 를 코드로 만든다. 씬에는 카메라와 이 오브젝트만 있으면 된다.
    /// 기획서 2.2 — 컷신 없이 바로 Stage 1-1 전투 시작.
    /// </summary>
    public class Boot : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Tables.Load();
            AudioManager.Ensure();
            var cam = Camera.main;
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.16f, 0.12f, 0.09f);

            var field = new GameObject("Field").AddComponent<Field>();
            field.cam = cam;
            field.Build();
            new GameObject("Squad").AddComponent<Squad>();
            new GameObject("Fx").AddComponent<Fx>();
            new GameObject("GameManager").AddComponent<GameManager>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var cgo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            // 폰(세로로 긴 화면) = 폭 기준, 태블릿처럼 넓은 화면 = 높이 기준
            scaler.matchWidthOrHeight = cam.aspect > 1080f / 1920f ? 1f : 0f;
            cgo.AddComponent<GraphicRaycaster>();
            var safe = UIKit.Rect(cgo.transform, "Safe").Fill();
            safe.gameObject.AddComponent<SafeArea>();

            var hud = safe.gameObject.AddComponent<HUD>();
            hud.Build(canvas);
        }
    }

    /// <summary>노치·둥근 모서리를 피하도록 UI 영역을 줄인다</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var r = Screen.safeArea;
            if (r == last || Screen.width == 0) return;
            last = r;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
            rt.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
