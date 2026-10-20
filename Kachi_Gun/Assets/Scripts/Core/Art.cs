using System.Collections.Generic;
using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// Resources/Art 의 그림을 불러와 스프라이트로 만든다 (한 번 만든 것은 보관).
    /// 그림 원본은 tools/slice_assets.py · tools/icon_gen.py 가 만든다.
    /// </summary>
    public static class Art
    {
        // 캐릭터 시트: 4x4 칸, 칸 360px, 발 가운데 = 칸 가운데(x 160), 발 = 칸 바닥에서 8px 위
        public const int CharCell = 360, CharFootX = 160;
        public const float CharPPU = 150f;
        // 좀비 시트: 7칸(걷기 0~3 · 맞기 4 · 죽기 5 · 맞기 흰 실루엣 6), 칸 400x380, 발 = 바닥에서 4px 위
        public const int ZW = 400, ZH = 380, ZCols = 7;
        public const int ZWalk = 0, ZHit = 4, ZDie = 5, ZFlash = 6;
        public const float ZombiePPU = 165f;

        static readonly Dictionary<string, Sprite> cache = new();
        static readonly Dictionary<string, Sprite[]> frames = new();
        static Font font;

        public static Font Font => font ? font : (font = Resources.Load<Font>("Font/MonaS12TextKR"));

        public static Texture2D Tex(string path)
        {
            var t = Resources.Load<Texture2D>("Art/" + path);
            if (t == null) Debug.LogError("그림 없음: Art/" + path);
            return t;
        }

        /// <summary>그림 한 장 전체를 스프라이트로. border = 9-slice 테두리(px, 왼·아래·오른·위)</summary>
        public static Sprite Get(string path, float ppu = 100f, Vector2? pivot = null, Vector4 border = default)
        {
            string key = path + "|" + ppu + "|" + pivot + "|" + border;
            if (cache.TryGetValue(key, out var s) && s) return s;
            var t = Tex(path);
            if (!t) return null;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot ?? new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = path;
            cache[key] = s;
            return s;
        }

        /// <summary>9-slice 판: 테두리 비율로 지정 (나뭇잎이 붙은 판도 모서리가 안 늘어나게)</summary>
        public static Sprite Sliced(string path, float bl, float bb, float br, float bt)
        {
            var t = Tex(path);
            if (!t) return null;
            return Get(path, 100f, null, new Vector4(t.width * bl, t.height * bb, t.width * br, t.height * bt));
        }

        static readonly Dictionary<int, bool> skinArt = new();
        /// <summary>그 스킨 그림(Char/skin_NN)이 있는지 — Skin_Table 에는 30종, 그림은 아직 0~16</summary>
        public static bool HasSkinArt(int skin)
        {
            if (!skinArt.TryGetValue(skin, out var ok)) skinArt[skin] = ok = Resources.Load<Texture2D>($"Art/Char/skin_{skin:00}") != null;
            return ok;
        }
        /// <summary>그릴 스킨 번호 (그림이 없으면 기본 카피 0)</summary>
        public static int ArtSkin(int skin) => HasSkinArt(skin) ? skin : 0;

        /// <summary>캐릭터 16장: 0~3 대기 · 4~7 사격 · 8~10 기타 · 12~15 쓰러짐</summary>
        public static Sprite[] Char(int skin)
        {
            string path = $"Char/skin_{skin:00}";
            if (frames.TryGetValue(path, out var f)) return f;
            var t = Tex(path);
            f = new Sprite[16];
            for (int i = 0; i < 16; i++)
            {
                int r = i / 4, c = i % 4;
                var rect = new Rect(c * CharCell, t.height - (r + 1) * CharCell, CharCell, CharCell);
                f[i] = Sprite.Create(t, rect, new Vector2((float)CharFootX / CharCell, 8f / CharCell), CharPPU, 0, SpriteMeshType.FullRect);
                f[i].name = path + "_" + i;
            }
            frames[path] = f;
            return f;
        }

        // ---------- 따로 그리는 총 (tools/gun_split.py) ----------
        // 총 그림 260x130 (사용자 그림 '총 이미지.png' + 스킨별 앞발), 회전 중심(손목) = (90, 55)px,
        // 총구 = 회전 중심에서 오른쪽 137.6px·위 17.6px (gun_split.gun_muzzle_px 와 같게)
        public static readonly Vector2 GunMuzzle = new(137.6f / CharPPU, 17.6f / CharPPU);
        public static readonly Vector2 FlashPivot = new(4f / 59f, 1f - 55f / 110f);
        static Dictionary<int, Vector2[]> gunPos;

        public static Sprite Gun(int skin) => Get($"Char/gun_{skin:00}", CharPPU, new Vector2(90f / 260f, 1f - 55f / 130f));
        public static Sprite MuzzleFlash => Get("Char/muzzle_flash", CharPPU, FlashPivot);

        /// <summary>캐릭터 칸 0~7 마다 총 회전 중심 자리(발 기준, 오른쪽을 볼 때)</summary>
        public static Vector2[] GunPos(int skin)
        {
            if (gunPos == null)
            {
                gunPos = new Dictionary<int, Vector2[]>();
                var ta = Resources.Load<TextAsset>("Art/Char/gun_pos");
                if (ta == null) Debug.LogError("그림 없음: Art/Char/gun_pos");
                else
                    foreach (var line in ta.text.Split((char)10))
                    {
                        var parts = line.Trim().Split(' ');
                        if (parts.Length < 9 || !parts[0].StartsWith("skin_")) continue;
                        var v = new Vector2[8];
                        for (int i = 0; i < 8; i++)
                        {
                            var xy = parts[i + 1].Split(',');
                            float x = float.Parse(xy[0]), y = float.Parse(xy[1]);
                            v[i] = new Vector2((x - CharFootX) / CharPPU, (CharCell - 8 - y) / CharPPU);
                        }
                        gunPos[int.Parse(parts[0].Substring(5))] = v;
                    }
            }
            if (gunPos.TryGetValue(skin, out var r)) return r;
            return gunPos.TryGetValue(0, out r) ? r : new Vector2[8];
        }

        /// <summary>좀비 7장: 0~3 걷기 · 4 맞기 · 5 죽기 · 6 맞기 흰 실루엣</summary>
        public static Sprite[] Zombie(string id)
        {
            string path = "Zombie/z_" + id;
            if (frames.TryGetValue(path, out var f)) return f;
            var t = Tex(path);
            f = new Sprite[ZCols];
            for (int i = 0; i < ZCols; i++)
            {
                f[i] = Sprite.Create(t, new Rect(i * ZW, 0, ZW, ZH), new Vector2(0.5f, 4f / ZH), ZombiePPU, 0, SpriteMeshType.FullRect);
                f[i].name = path + "_" + i;
            }
            frames[path] = f;
            return f;
        }

        /// <summary>발밑 그림자 (128x40px → 1.28 x 0.4칸, 맵 위·모든 캐릭터 아래에 그림)</summary>
        public const int ShadowOrder = -25000;
        public static SpriteRenderer Shadow(Transform parent, float width, float height)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.02f, 0);
            go.transform.localScale = new Vector3(width / 1.28f, height / 0.4f, 1);
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = Get("Gen/shadow", 100f);
            s.sortingOrder = ShadowOrder;
            return s;
        }

        // ---------- UI 팩 부품 (tools/ui_kit.py → Art/Kit, 원본 도트 1칸 = 5px) ----------
        public const int KitScale = 5;
        // 9-slice 테두리 (원본 px: 왼, 아래, 오른, 위) — ui_kit.py 의 BORDER 와 같게
        static readonly Dictionary<string, Vector4> kitBorder = new()
        {
            ["window"] = new(25, 8, 13, 16), ["panel_tan"] = new(5, 5, 5, 5), ["panel_brown"] = new(5, 5, 5, 5),
            ["slot"] = new(2, 2, 2, 2), ["slot_light"] = new(2, 2, 2, 2), ["wood"] = new(6, 7, 6, 5), ["wood_dark"] = new(6, 7, 6, 5),
            ["tile"] = new(5, 6, 5, 5), ["plank"] = new(5, 5, 5, 4), ["plank_thin"] = new(5, 4, 5, 3),
            ["btn_green"] = new(5, 5, 5, 4), ["btn_green_hi"] = new(5, 5, 5, 4), ["sq_green"] = new(6, 7, 6, 5),
            ["outline"] = new(4, 4, 4, 4), ["bar_track"] = new(2, 1, 2, 1), ["bar_red"] = new(2, 1, 2, 1),
            ["bar_teal"] = new(2, 1, 2, 1), ["bar_green"] = new(2, 1, 2, 1), ["check_on"] = new(4, 4, 4, 4), ["check_off"] = new(4, 4, 4, 4),
        };

        /// <summary>UI 팩 부품 (테두리가 있는 것은 9-slice 로 늘려도 모서리 그대로)</summary>
        public static Sprite Kit(string name, float ppu = 100f)
        {
            var b = kitBorder.TryGetValue(name, out var v) ? v * KitScale : Vector4.zero;
            return Get("Kit/" + name, ppu, null, b);
        }

        static Sprite white;
        public static Sprite White => white ? white : (white = Get("Gen/white", 8f));
    }
}
