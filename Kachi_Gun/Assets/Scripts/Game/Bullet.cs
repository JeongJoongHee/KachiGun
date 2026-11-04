using System.Collections.Generic;
using UnityEngine;

namespace KachiGun
{
    /// <summary>날아가는 총알 (단발·속사·산탄·관통 드릴·미사일). 레이저는 즉시 판정(LaserHit)</summary>
    public class Bullet : MonoBehaviour
    {
        public enum Kind { Normal, Small, Pellet, Drill, Missile }
        static readonly Stack<Bullet> pool = new();

        public Kind kind;
        public double dmg;
        public int pierceLeft;
        public float life, speed, splash, maxSpeed, accel, turn, hitBonus;
        public double bossMul = 1;          // 보스 피해 배율 (쏜 카피바라의 보급품·슬롯 강화)
        public int grade;                   // 총알 등급 (오오라·폭발 색)
        SpriteRenderer auraOut, auraIn;     // 11-04 요청_02: A 등급부터 일반탄 네온 오오라 (등급 색)
        float auraT;
        public Target homing;
        Vector2 dir;
        SpriteRenderer sr;
        readonly HashSet<Target> hitSet = new();

        public static Kind KindOf(string bulletKind) => bulletKind switch
        {
            "Small" => Kind.Small, "Pellet" => Kind.Pellet, "Drill" => Kind.Drill, "Missile" => Kind.Missile, _ => Kind.Normal
        };

        /// <summary>spec = Bullet_Spec 줄 (속도·가속·유도·판정 크기·그림). range = 날아가는 거리</summary>
        public static Bullet Fire(BulletRow spec, Vector2 pos, Vector2 dir, double dmg, int pierce, float range, double bossMul = 1, int grade = 0)
        {
            Bullet b = pool.Count > 0 ? pool.Pop() : null;
            if (b == null)
            {
                var go = new GameObject("Bullet");
                b = go.AddComponent<Bullet>();
                b.sr = go.AddComponent<SpriteRenderer>();
            }
            b.gameObject.SetActive(true);
            var k = KindOf(spec.kind);
            b.kind = k; b.grade = grade; b.dmg = dmg; b.pierceLeft = pierce; b.dir = dir.normalized; b.homing = null; b.splash = 0; b.bossMul = bossMul;
            b.speed = Mathf.Max(0.5f, spec.speed); b.maxSpeed = Mathf.Max(b.speed, spec.maxSpeed); b.accel = spec.accel; b.turn = spec.turn; b.hitBonus = spec.hitBonus;
            string path = string.IsNullOrEmpty(spec.sprite) ? "Bullet/bullet_basic" : spec.sprite;
            b.hitSet.Clear();
            b.transform.position = pos;
            switch (k)
            {
                case Kind.Pellet: b.sr.sprite = Art.Get(path, 190f); break;
                case Kind.Drill: b.sr.sprite = Art.Get(path, 220f); break;
                case Kind.Missile: b.sr.sprite = Art.Get(path, 260f, new Vector2(0.75f, 0.5f)); break;
                case Kind.Small: b.sr.sprite = Art.Get(path, 300f, new Vector2(0.85f, 0.5f)); break;
                default: b.sr.sprite = Art.Get(path, 230f, new Vector2(0.85f, 0.5f)); break;
            }
            b.life = range / b.speed;
            b.sr.sortingOrder = 20000;
            b.transform.localScale = Vector3.one * (k == Kind.Drill ? 0.8f : 1f);   // 11-04 요청_02: 관통탄 크기 0.8배 (맞는 판정은 그대로)
            b.SetAura(k != Kind.Missile && grade >= AuraGrade);
            b.Face();
            return b;
        }

        /// <summary>네온 오오라가 붙는 등급 (A = 3)</summary>
        public const int AuraGrade = 3;

        /// <summary>총알 그림 뒤에 등급 색 빛 두 겹 (바깥 넓게 흐리게 · 안쪽 진하게) — 총알 방향으로 길쭉하게</summary>
        void SetAura(bool on)
        {
            if (!on) { if (auraOut) { auraOut.enabled = false; auraIn.enabled = false; } return; }
            if (!auraOut)
            {
                auraOut = NewGlow("AuraOut", 19998);
                auraIn = NewGlow("AuraIn", 19999);
            }
            var c = Tables.GradeColor(grade);
            Vector2 size = sr.sprite ? (Vector2)sr.sprite.bounds.size : new Vector2(0.3f, 0.15f);
            float big = 1f + Mathf.Clamp(grade - AuraGrade, 0, 6) * 0.08f;   // 등급이 높을수록 조금 더 크게
            auraOut.transform.localScale = new Vector3(size.x * 2.2f + 0.25f, size.y * 3.2f + 0.25f, 1) * big;
            auraIn.transform.localScale = new Vector3(size.x * 1.5f + 0.08f, size.y * 1.9f + 0.08f, 1) * big;
            auraOut.transform.localPosition = auraIn.transform.localPosition = new Vector3(size.x * (0.5f - sr.sprite.pivot.x / sr.sprite.rect.width), 0, 0);
            auraOut.color = new Color(c.r, c.g, c.b, 0.55f);
            auraIn.color = Color.Lerp(c, Color.white, 0.35f) * new Color(1, 1, 1, 0.9f);
            auraOut.enabled = auraIn.enabled = true;
            auraT = Random.value * 6f;
        }

        SpriteRenderer NewGlow(string n, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var g = go.AddComponent<SpriteRenderer>();
            g.sprite = Art.Glow;
            g.sortingOrder = order;
            return g;
        }

        void Face() => transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (auraOut && auraOut.enabled) { auraT += dt * 14f; var c = auraOut.color; c.a = 0.45f + 0.15f * Mathf.Sin(auraT); auraOut.color = c; }   // 네온 일렁임
            if (life <= 0) { if (kind == Kind.Missile) Explode(); Recycle(); return; }

            if (kind == Kind.Missile && homing != null && homing.Alive)
            {
                Vector2 want = (homing.Center - (Vector2)transform.position).normalized;
                dir = Vector3.RotateTowards(dir, want, dt * turn, 0).normalized;
                Face();
                speed = Mathf.Min(speed + dt * accel, maxSpeed);
            }

            Vector2 p0 = transform.position;
            Vector2 p1 = p0 + dir * speed * dt;
            transform.position = p1;

            // 맞았는지: 선분과 대상 원
            foreach (var t in Target.All)
            {
                if (!t.Alive || hitSet.Contains(t)) continue;
                if (DistToSeg(t.Center, p0, p1) > t.radius + hitBonus &&
                    DistToSeg(t.Pos + Vector2.up * 0.25f, p0, p1) > t.radius) continue;
                if (kind == Kind.Missile) { Explode(); Recycle(); return; }
                hitSet.Add(t);
                GameManager.I.Damage(t, dmg, p0, bossMul);
                Fx.I?.Spark(p1, kind == Kind.Drill);
                pierceLeft--;
                if (pierceLeft <= 0) { Recycle(); return; }
            }
        }

        void Explode()
        {
            Vector2 c = transform.position;
            Fx.I?.Boom(c, splash, grade);
            foreach (var t in Target.All.ToArray())
                if (t.Alive && (t.Center - c).magnitude <= splash + t.radius)
                    GameManager.I.Damage(t, dmg, c, bossMul, false);   // 11-04 요청_02: 미사일은 넉백 없음
        }

        void Recycle()
        {
            gameObject.SetActive(false);
            homing = null;
            pool.Push(this);
        }

        public static void LaserHit(Vector2 from, Vector2 dir, float len, double dmg, float hitBonus = 0.25f, double bossMul = 1)
        {
            Vector2 to = from + dir.normalized * len;
            foreach (var t in Target.All.ToArray())
                if (t.Alive && DistToSeg(t.Center, from, to) <= t.radius + hitBonus)
                    GameManager.I.Damage(t, dmg, from, bossMul);
        }

        public static void ClearAll()
        {
            foreach (var b in FindObjectsByType<Bullet>(FindObjectsSortMode.None))
                if (b.gameObject.activeSelf) b.Recycle();
        }

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
