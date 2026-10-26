using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KachiGun
{
    /// <summary>효과: 폭발·불꽃·레이저 줄(월드) + 피해 숫자·날아가는 지폐(UI)</summary>
    public class Fx : MonoBehaviour
    {
        public static Fx I;
        public RectTransform uiLayer;      // HUD 가 넣어 줌 (HUD 아래 층)
        Camera cam;

        class Anim { public Transform t; public SpriteRenderer sr; public Sprite[] frames; public float time, dur, scale; public Vector2 vel; public bool fade; }
        readonly List<Anim> anims = new();
        class Pop { public RectTransform rt; public Text tx; public Vector2 vel; public float t, dur; }
        readonly List<Pop> pops = new();
        readonly Stack<Pop> popPool = new();
        class Bill { public RectTransform rt; public Vector2 start, mid, end; public float t, dur; public double gold; public bool gem; }
        readonly List<Bill> bills = new();
        readonly Stack<RectTransform> billPool = new();

        void Awake() { I = this; }

        Camera Cam => cam ? cam : (cam = Camera.main);

        // ---------- 월드 ----------
        Anim Spawn(Sprite[] fr, Vector2 p, float dur, float scale, int order)
        {
            var go = new GameObject("Fx");
            go.transform.position = p;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fr[0];
            sr.sortingOrder = order;
            var a = new Anim { t = go.transform, sr = sr, frames = fr, dur = dur, scale = scale };
            anims.Add(a);
            return a;
        }

        Sprite[] boom;
        public void Boom(Vector2 p, float radius)
        {
            boom ??= new[] { Art.Get("Gen/boom_0", 100), Art.Get("Gen/boom_1", 100), Art.Get("Gen/boom_2", 100) };
            Spawn(boom, p, 0.45f, Mathf.Max(0.6f, radius * 0.85f), 21000).fade = true;
            AudioManager.Play(AudioManager.Sfx.Boom);
            CamShake(0.12f);
        }

        Sprite[] spark;
        public void Spark(Vector2 p, bool big)
        {
            if (anims.Count > 120) return;
            spark ??= new[] { Art.Get("Gen/spark", 100) };
            var a = Spawn(spark, p + Random.insideUnitCircle * 0.1f, 0.12f, big ? 0.5f : 0.32f, 21000);
            a.fade = true;
            a.t.rotation = Quaternion.Euler(0, 0, Random.Range(0, 90f));
        }

        Sprite[] gem;
        public void Sparkle(Vector2 p)
        {
            gem ??= new[] { Art.Get("UI/icon_gem", 100) };
            for (int i = 0; i < 4; i++)
            {
                var a = Spawn(gem, p, 0.45f, 0.25f, 21000);
                a.vel = Random.insideUnitCircle.normalized * 3f + Vector2.up * 2f;
                a.fade = true;
            }
        }

        Sprite[] beam;
        public void Beam(Vector2 from, Vector2 dir, float len, int grade)
        {
            beam ??= new[] { Art.Get("Bullet/laser_beam", 100f, new Vector2(0, 0.5f)) };
            var a = Spawn(beam, from, 0.14f, 1f, 20500);
            a.t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float thick = 1f + Mathf.Clamp(grade - 4, 0, 4) * 0.3f;
            a.t.localScale = new Vector3(len / 0.32f, thick * 1.3f, 1);
            a.scale = -1;   // 크기 고정
            a.fade = true;
            if (grade >= 5) a.sr.color = grade >= 6 ? new Color(1f, 0.7f, 1f) : new Color(0.75f, 1f, 0.8f);
        }

        float shake;
        public void CamShake(float s) { shake = Mathf.Max(shake, s); }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = anims.Count - 1; i >= 0; i--)
            {
                var a = anims[i];
                a.time += dt;
                float k = a.time / a.dur;
                if (k >= 1 || a.t == null) { if (a.t) Destroy(a.t.gameObject); anims.RemoveAt(i); continue; }
                a.sr.sprite = a.frames[Mathf.Min(a.frames.Length - 1, (int)(k * a.frames.Length))];
                if (a.vel != Vector2.zero) { a.t.position += (Vector3)(a.vel * dt); a.vel += Vector2.down * 9f * dt; }
                if (a.fade) { var c = a.sr.color; c.a = 1 - k; a.sr.color = c; }
                if (a.scale > 0 && a.frames.Length > 1) a.t.localScale = Vector3.one * a.scale * (0.7f + k * 0.5f);
            }

            if (shake > 0 && Field.I)
            {
                shake -= Time.unscaledDeltaTime;
                Cam.transform.position = new Vector3(Random.Range(-1f, 1f) * 0.06f, Random.Range(-1f, 1f) * 0.06f, -10);
                if (shake <= 0) Cam.transform.position = new Vector3(0, 0, -10);
            }

            UpdatePops(Time.unscaledDeltaTime);
            UpdateBills(Time.unscaledDeltaTime);
        }

        // ---------- UI: 피해 숫자 ----------
        public void Damage(Vector2 world, double dmg, bool big)
        {
            if (!uiLayer || pops.Count > 40) return;
            Text(world, Num.F(dmg), big ? new Color(1f, 0.45f, 0.35f) : Color.white, big ? 50 : 38);
        }

        public void Text(Vector2 world, string s, Color c, int size, float dur = 0.7f)
        {
            if (!uiLayer) return;
            var p = popPool.Count > 0 ? popPool.Pop() : null;
            if (p == null)
            {
                var tx = UIKit.Label(uiLayer, "", size, c, TextAnchor.MiddleCenter);
                tx.rectTransform.sizeDelta = new Vector2(300, 80);
                tx.raycastTarget = false;
                p = new Pop { rt = tx.rectTransform, tx = tx };
            }
            p.rt.gameObject.SetActive(true);
            p.tx.text = s; p.tx.color = c; p.tx.fontSize = size;
            p.rt.anchoredPosition = WorldToUi(world);
            p.vel = new Vector2(Random.Range(-30f, 30f), 260f);
            p.t = 0; p.dur = dur;
            pops.Add(p);
        }

        void UpdatePops(float dt)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                p.t += dt;
                p.rt.anchoredPosition += p.vel * dt;
                p.vel *= 1 - dt * 5f;
                float k = p.t / p.dur;
                var c = p.tx.color; c.a = k < 0.6f ? 1 : 1 - (k - 0.6f) / 0.4f; p.tx.color = c;
                float s = k < 0.12f ? 1.3f - k * 2.5f : 1f;
                p.rt.localScale = Vector3.one * s;
                if (k >= 1) { p.rt.gameObject.SetActive(false); pops.RemoveAt(i); popPool.Push(p); }
            }
        }

        public Vector2 WorldToUi(Vector2 world)
        {
            Vector2 sp = Cam.WorldToScreenPoint(world);
            var c = uiLayer.GetComponentInParent<Canvas>().rootCanvas;
            var uiCam = c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;   // 시험 캡처는 카메라 모드
            RectTransformUtility.ScreenPointToLocalPointInRectangle(uiLayer, sp, uiCam, out var lp);
            return lp;
        }

        // ---------- UI: 지폐·다이아가 위 표시판으로 날아감 ----------
        public void Money(Vector2 world, double gold, int count, bool gemIcon = false)
        {
            if (!uiLayer || HUD.I == null) return;
            count = Mathf.Clamp(count, 1, 5);
            Vector2 start = WorldToUi(world);
            var target = gemIcon ? HUD.I.gemIcon : HUD.I.goldIcon;
            Vector2 end = uiLayer.InverseTransformPoint(target.position);
            for (int i = 0; i < count; i++)
            {
                if (bills.Count > 60) { if (gemIcon) SaveData.Cur.gems += gold / count; else SaveData.Cur.gold += gold / count; continue; }
                var rt = billPool.Count > 0 ? billPool.Pop() : null;
                if (rt == null) { rt = UIKit.Img(uiLayer, Art.Get("UI/icon_money"), new Vector2(64, 66)).rectTransform; rt.GetComponent<Image>().raycastTarget = false; }
                rt.gameObject.SetActive(true);
                rt.GetComponent<Image>().sprite = gemIcon ? Art.Get("UI/icon_gem") : Art.Get("UI/icon_money");
                rt.sizeDelta = gemIcon ? new Vector2(56, 56) : new Vector2(60, 62);
                rt.anchoredPosition = start;
                var mid = start + Random.insideUnitCircle * 110f + Vector2.up * 40f;
                bills.Add(new Bill { rt = rt, start = start, mid = mid, end = end, t = -i * 0.05f, dur = 0.75f, gold = gold / count, gem = gemIcon });
            }
        }

        void UpdateBills(float dt)
        {
            for (int i = bills.Count - 1; i >= 0; i--)
            {
                var b = bills[i];
                b.t += dt;
                float k = Mathf.Clamp01(b.t / b.dur);
                Vector2 p;
                if (k < 0.35f) p = Vector2.Lerp(b.start, b.mid, Ease(k / 0.35f));
                else p = Vector2.Lerp(b.mid, b.end, EaseIn((k - 0.35f) / 0.65f));
                b.rt.anchoredPosition = p;
                b.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(b.t * 20f) * 15f);
                if (b.t >= b.dur)
                {
                    if (b.gem) SaveData.Cur.gems += b.gold; else SaveData.Cur.gold += b.gold;
                    AudioManager.Play(AudioManager.Sfx.Coin);
                    HUD.I?.Bump(b.gem);
                    b.rt.gameObject.SetActive(false);
                    billPool.Push(b.rt);
                    bills.RemoveAt(i);
                }
            }
        }

        /// <summary>날아가는 중인 돈을 바로 넣는다 (던전 들어가기·저장 전)</summary>
        public void FlushMoney()
        {
            foreach (var b in bills) { if (b.gem) SaveData.Cur.gems += b.gold; else SaveData.Cur.gold += b.gold; b.rt.gameObject.SetActive(false); billPool.Push(b.rt); }
            bills.Clear();
        }

        static float Ease(float x) => 1 - (1 - x) * (1 - x);
        static float EaseIn(float x) => x * x;
    }
}
