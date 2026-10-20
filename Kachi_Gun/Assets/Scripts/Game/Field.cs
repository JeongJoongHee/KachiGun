using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 맵과 카메라 맞추기, 싸우는 땅(가로 띠) 계산.
    /// 맵 = tools/side_map.py 가 Main_Map 조각을 이어 붙여 만든 가로 전장(위·아래 숲, 가운데 흙길). 1728x2448px, 100px = 1칸, 원점 가운데.
    /// 왼쪽 = 카피바라 3마리 고정, 오른쪽 화면 밖에서 좀비가 나와 왼쪽으로 걸어온다 (사용자 그림 '배경 수정.png' 처럼, 줄 구분 없음).
    /// </summary>
    public class Field : MonoBehaviour
    {
        public static Field I;
        public SpriteRenderer map;
        public Camera cam;

        public const float MapW = 17.28f, MapH = 24.48f;
        // 걸어 다닐 수 있는 흙 띠(맵 픽셀 y, 위가 0) — side_map.py 의 숲 경계 + 바위 턱 안쪽.
        // 아래 끝은 아래 UI(총알 칸·강화 카드·버튼 줄, HUD.BottomUiHeight)에 안 가리게 9:16 화면 기준으로 올림
        public static readonly float FieldTop = P(0, 820).y, FieldBottom = P(0, 1760).y;

        // 화면에서 UI 가 덮는 비율 (위 HUD · 아래 강화 카드)
        public const float TopUi = 0.15f, BottomUi = 0.19f;

        public static Vector2 P(float px, float py) => new((px - 864) / 100f, (1224 - py) / 100f);

        /// <summary>화면 비율 좌표(fx 0 = 왼쪽 끝 ~ 1 = 오른쪽 끝, fy 0 = 위 ~ 1 = 아래) → 월드. 폰 폭이 달라도 화면에서 같은 자리</summary>
        public Vector2 ScreenPt(float fx, float fy) =>
            new(-ViewHalfW + fx * 2f * ViewHalfW, ViewTop - fy * 2f * cam.orthographicSize);

        // 분대 자리(발 위치): 왼쪽 세로 한 줄 — 리더 = 가운데, 동료 1 = 위, 동료 2 = 아래
        // Player_Base 의 Leader/Sub1/Sub2_Screen_Pos (화면 비율)
        static Vector2 LeaderPos => Tables.PV("Leader_Screen_Pos", new Vector2(0.13f, 0.512f));
        static Vector2 Sub1Pos => Tables.PV("Sub1_Screen_Pos", new Vector2(0.145f, 0.385f));
        static Vector2 Sub2Pos => Tables.PV("Sub2_Screen_Pos", new Vector2(0.145f, 0.640f));
        public Vector2 Home => ScreenPt(LeaderPos.x, LeaderPos.y);
        public Vector2 Sub1Offset => ScreenPt(Sub1Pos.x, Sub1Pos.y) - Home;
        public Vector2 Sub2Offset => ScreenPt(Sub2Pos.x, Sub2Pos.y) - Home;

        void Awake() { I = this; }

        public void Build()
        {
            var go = new GameObject("Map");
            go.transform.SetParent(transform, false);
            map = go.AddComponent<SpriteRenderer>();
            map.sprite = Art.Get("Map/side_map", 100f);
            map.sortingOrder = -30000;
            Fit();
        }

        public void Fit()
        {
            if (!cam) cam = Camera.main;
            float aspect = cam.aspect;
            cam.orthographicSize = Mathf.Min(MapH / 2f, MapW / aspect / 2f);
            cam.transform.position = new Vector3(0, 0, -10);
        }

        public float ViewTop => cam.transform.position.y + cam.orthographicSize;
        public float ViewBottom => cam.transform.position.y - cam.orthographicSize;
        public float ViewHalfW => cam.orthographicSize * cam.aspect;
        /// <summary>UI 에 안 가려지는 화면 위·아래</summary>
        public float PlayTop => ViewTop - cam.orthographicSize * 2 * TopUi;
        public float PlayBottom => ViewBottom + cam.orthographicSize * 2 * BottomUi;
        float Top => Mathf.Min(FieldTop, PlayTop);
        float Bottom => Mathf.Max(FieldBottom, PlayBottom);
        /// <summary>오른쪽 화면 밖(좀비가 나오는 곳)까지 허용</summary>
        float RightLimit => ViewHalfW + 1.6f;

        /// <summary>흙 띠 안인지</summary>
        public bool OnRoad(Vector2 p, float margin = 0.3f) =>
            p.y <= Top - margin && p.y >= Bottom + margin && p.x >= -ViewHalfW + 0.4f && p.x <= RightLimit;

        /// <summary>흙 띠 안으로 당긴다 (오른쪽은 화면 밖 나오는 자리까지 허용)</summary>
        public Vector2 Clamp(Vector2 p, float margin = 0.3f)
        {
            p.x = Mathf.Clamp(p.x, -ViewHalfW + 0.6f, RightLimit);
            p.y = Mathf.Clamp(p.y, Bottom + margin, Top - margin);
            return p;
        }

        /// <summary>던전 대상 놓을 자리. near~far: 0 = 분대 바로 앞 ~ 1 = 오른쪽 끝, 높이는 흙 띠 아무 데나</summary>
        public Vector2 RandomRoad(float near = 0f, float far = 1f, float margin = 0.8f)
        {
            float x0 = Home.x + 2.6f, x1 = ViewHalfW - 0.8f;
            float x = Mathf.Lerp(x0, x1, Random.Range(near, far));
            float y = Random.Range(Bottom + margin, Top - margin - 0.6f);
            return new Vector2(x, y);
        }

        /// <summary>좀비가 나오는 곳: 오른쪽 화면 바로 밖, 흙 띠 아무 높이</summary>
        public Vector2 SpawnPoint() => new(ViewHalfW + 0.9f, Random.Range(Bottom + 0.4f, Top - 0.8f));

        public void Tint(Color c) { if (map) map.color = c; }

        /// <summary>챕터 맵 색 = Chapter_Summary 의 Map_Tint_Hex</summary>
        public static Color ChapterTint(int chapter) => Tables.Chapter(chapter).tint;
    }
}
