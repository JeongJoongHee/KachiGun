using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 다이아 광산(감염된 크리스탈 둥지)의 광석. 공격력과 상관없이 한 대에 1씩 깎인다(기획서 8.2).
    /// 회색 바위 = 보상 없음 · 작은 푸른 보석 = +0.1 · 큰 푸른 보석 = +1.0
    /// </summary>
    public class Ore : Target
    {
        public OreRow row;
        public double reward;
        float shakeT, goneT;
        float baseScale;

        /// <summary>r = Mine_Ore_Spec 줄 (타수·보상·크기·판정·그림), mul = Mine_Scaling 보상 배율</summary>
        public static Ore Spawn(OreRow r, Vector2 pos, double mul = 1)
        {
            var go = new GameObject("Ore_" + r.kind);
            go.transform.position = pos;
            var o = go.AddComponent<Ore>();
            o.row = r;
            o.baseScale = r.scale;
            string path = string.IsNullOrEmpty(r.sprite) ? "UI/icon_gem" : r.sprite;
            if (r.kind.Contains("Huge") && path == "UI/icon_gem") path = "Gen/gem_gold";   // 거대 황금 크리스탈 = 금빛 보석 (tools/icon_gen.py gem_gold)
            o.MakeBody(Art.Get(path, 100f, new Vector2(0.5f, 0.08f)), o.baseScale);
            o.hp = o.maxHp = System.Math.Max(1, r.hp);
            o.reward = r.reward * mul;
            o.height = 1.2f * o.baseScale;
            o.radius = r.radius;
            return o;
        }

        void Update()
        {
            if (dead)
            {
                goneT += Time.deltaTime;
                float s = baseScale * (1 + goneT * 2f);
                body.localScale = Vector3.one * s;
                var c = sr.color; c.a = 1 - goneT / 0.3f; sr.color = c;
                if (goneT > 0.3f) Destroy(gameObject);
                return;
            }
            if (shakeT > 0)
            {
                shakeT -= Time.deltaTime;
                body.localPosition = new Vector3(Mathf.Sin(shakeT * 90f) * 0.05f, 0, 0);
            }
            else body.localPosition = Vector3.zero;
        }

        public override void Hit(double dmg, Vector2 from)
        {
            if (dead) return;
            hp -= 1;        // 고정 피해 1
            shakeT = 0.1f;
            if (hp <= 0)
            {
                dead = true;
                HideBar();
                GameManager.I?.OnTargetDead(this);
                AudioManager.Play(reward > 0 ? AudioManager.Sfx.Coin : AudioManager.Sfx.MineHit);
                if (reward > 0) Fx.I?.Sparkle(Center);
            }
            else { AudioManager.Play(AudioManager.Sfx.MineHit); ShowBar(false); }
        }
    }
}
