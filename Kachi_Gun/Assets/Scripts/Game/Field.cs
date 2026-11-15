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

        // 11-15 카피바라 전진: 맵 판을 가로로 4장 이어 붙여(짝수 번째는 좌우 뒤집어 이음새가 맞게) 왼쪽으로 흘려보냄.
        //   map = 원래 자리(가운데) 판 · tiles = 4장 모두 · scroll = 지금까지 흘러간 거리(2판마다 처음으로)
        readonly System.Collections.Generic.List<SpriteRenderer> tiles = new();
        float scroll, tileW = MapW;
        // 맵 그림 왼쪽·오른쪽 끝 4px 은 짙은 테두리 → 이어 붙이면 세로 줄이 보여서 양 끝 Crop px 를 잘라 쓴 그림으로 붙임
        const int Crop = 5;
        static readonly System.Collections.Generic.Dictionary<string, Sprite> cropCache = new();
        static Sprite Cropped(string name)
        {
            if (cropCache.TryGetValue(name, out var sp) && sp) return sp;
            var tex = Resources.Load<Texture2D>("Art/Map/" + name);
            if (!tex) return null;
            sp = Sprite.Create(tex, new Rect(Crop, 0, tex.width - 2 * Crop, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sp.name = "Map/" + name;
            cropCache[name] = sp;
            return sp;
        }
        /// <summary>지금까지 흘러간 땅 거리 (계속 늘어남 — 무리 생성 줄 위치 보정용)</summary>
        public float ScrollTotal { get; private set; }
        SpriteRenderer fade;

        public void Build()
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(i == 1 ? "Map" : "Map" + i);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = Cropped("side_map") ?? Art.Get("Map/side_map", 100f);
                r.sortingOrder = -30000;
                r.flipX = i % 2 == 0;
                tiles.Add(r);
            }
            map = tiles[1];
            if (map.sprite) tileW = map.sprite.rect.width / 100f;
            PlaceTiles();
            Fit();
        }

        void PlaceTiles()
        {
            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i]) tiles[i].transform.localPosition = new Vector3((i - 1) * tileW - scroll, 0, 0);
        }

        /// <summary>11-15 땅을 왼쪽으로 dx 만큼 흘림 (맵 판 + 좀비·쓰러진 좀비). 분대는 제자리에서 걷는 그림</summary>
        public void Scroll(float dx)
        {
            if (dx <= 0) return;
            ScrollTotal += dx;
            scroll = (scroll + dx) % (tileW * 2f);
            PlaceTiles();
            foreach (var t in Target.All) if (t) t.transform.position += new Vector3(-dx, 0, 0);
        }

        /// <summary>11-15 다음 맵 이동 연출: 싸우는 땅을 어둡게 덮기 (0 = 끔 ~ 1 = 다 덮음, 위 UI 는 그대로 보임)</summary>
        public void SetFade(float a)
        {
            if (!fade)
            {
                if (a <= 0) return;
                var go = new GameObject("Fade");
                go.transform.SetParent(transform, false);
                fade = go.AddComponent<SpriteRenderer>();
                var tex = Texture2D.whiteTexture;
                fade.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                fade.sortingOrder = 32000;
            }
            fade.enabled = a > 0.001f;
            if (!fade.enabled) return;
            fade.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
            fade.transform.localScale = new Vector3(ViewHalfW * 2f + 1f, cam.orthographicSize * 2f + 1f, 1);
            fade.color = new Color(0.16f, 0.11f, 0.07f, Mathf.Clamp01(a));
        }
        public float FadeAlpha => fade && fade.enabled ? fade.color.a : 0f;

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
        /// <summary>오른쪽 화면 밖(좀비가 나오는 곳)까지 허용 — 11-15 전진할 땐 앞에 기다리는 무리 자리까지</summary>
        float RightLimit => ViewHalfW + (GameManager.I != null && GameManager.I.AdvanceMode ? 12f : 1.6f);

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
        /// <summary>정한 높이를 흙 띠 안으로 당긴 나오는 자리 (무리 생성용)</summary>
        public Vector2 SpawnPoint(float y) => new(ViewHalfW + 0.9f, Mathf.Clamp(y, Bottom + 0.4f, Top - 0.8f));
        /// <summary>11-15 흙 띠 위·아래 끝 (좀비 발이 설 수 있는 높이)</summary>
        public float RoadTop => Top - 0.8f;
        public float RoadBottom => Bottom + 0.4f;

        public void Tint(Color c) { foreach (var t in tiles) if (t) t.color = c; }

        /// <summary>11-11 배경 판 바꾸기 (Art/Map/&lt;이름&gt;, 흙길은 판마다 같은 자리) — 없으면 기본 side_map</summary>
        public void SetMap(string name)
        {
            if (!map) return;
            if (string.IsNullOrEmpty(name)) name = "side_map";
            if (map.sprite && map.sprite.name == "Map/" + name) return;
            if (Resources.Load<Texture2D>("Art/Map/" + name) == null) name = "side_map";
            var sp = Cropped(name) ?? Art.Get("Map/" + name, 100f);
            foreach (var t in tiles) if (t) t.sprite = sp;
        }

        /// <summary>챕터 맵 색 = Chapter_Summary 의 Map_Tint_Hex</summary>
        /// 11-05 밝은 낙서풍 맵이라 표 색을 Map_Tint_Strength 만큼만 섞음 (1 = 표 색 그대로)
        public static Color ChapterTint(int chapter) => Color.Lerp(Color.white, Tables.Chapter(chapter).tint, Mathf.Clamp01((float)Tables.C("Map_Tint_Strength", 0.45)));
    }
}
