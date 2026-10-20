using System.Collections.Generic;
using UnityEngine;

namespace KachiGun
{
    /// <summary>총에 맞는 것(좀비·광석)의 공통 부분</summary>
    public abstract class Target : MonoBehaviour
    {
        public static readonly List<Target> All = new();

        public double hp, maxHp;
        public bool dead;
        public float radius = 0.45f, height = 1.3f;
        public SpriteRenderer sr;
        protected Transform body;          // 흔들림·통통 튀기용 (sr 이 붙은 자식)
        HpBar bar;

        public Vector2 Pos => transform.position;
        public Vector2 Center => (Vector2)transform.position + Vector2.up * (height * 0.5f);
        public bool Alive => !dead && this;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>맞음. fixedOne = 광산처럼 무조건 1 피해</summary>
        public abstract void Hit(double dmg, Vector2 from);

        protected void MakeBody(Sprite s, float scale)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * scale;
            sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = s;
        }

        protected void ShowBar(bool boss)
        {
            if (!bar) bar = HpBar.Make(transform, height + 0.15f, boss ? 1.6f : 0.8f, boss);
            bar.Set((float)(hp / maxHp));
        }

        protected void HideBar() { if (bar) bar.gameObject.SetActive(false); }

        protected virtual void LateUpdate()
        {
            int o = Mathf.RoundToInt(-transform.position.y * 100f);
            if (sr) sr.sortingOrder = o;
            if (bar) bar.SetOrder(o + 5);
        }

        public static Target Nearest(Vector2 from, float range, Target except = null)
        {
            Target best = null;
            float bd = range * range;
            foreach (var t in All)
            {
                if (t == except || !t.Alive) continue;
                float d = ((Vector2)t.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }
    }

    /// <summary>머리 위 체력 막대 (UI 팩 막대: 어두운 바탕 + 초록/빨강 채움, 9-slice 라 끝이 둥근 그대로)</summary>
    public class HpBar : MonoBehaviour
    {
        SpriteRenderer bg, fill;
        float w;
        const float H = 0.13f, Ppu = 260f;

        public static HpBar Make(Transform parent, float y, float width, bool boss)
        {
            var go = new GameObject("HpBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            var b = go.AddComponent<HpBar>();
            b.w = width;
            b.bg = Bar(go.transform, "Bg", "bar_track", new Vector2(width + 0.06f, H + 0.04f));
            b.fill = Bar(go.transform, "Fill", boss ? "bar_red" : "bar_green", new Vector2(width, H));
            return b;
        }

        static SpriteRenderer Bar(Transform p, string n, string kit, Vector2 size)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var s = go.AddComponent<SpriteRenderer>();
            s.sprite = Art.Kit(kit, Ppu);
            s.drawMode = SpriteDrawMode.Sliced;
            s.size = size;
            return s;
        }

        public void Set(float r)
        {
            r = Mathf.Clamp01(r);
            float fw = w * r;
            fill.enabled = r > 0;
            fill.size = new Vector2(Mathf.Max(fw, 0.09f), H);
            fill.transform.localPosition = new Vector3(-w / 2f + Mathf.Max(fw, 0.09f) / 2f, 0, 0);
        }

        public void SetOrder(int o) { bg.sortingOrder = o; fill.sortingOrder = o + 1; }
    }
}
