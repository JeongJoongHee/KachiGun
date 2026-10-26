using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 좀비: 오른쪽 화면 밖에서 나와 왼쪽으로 곧장 걸어오다 분대 앞에서 멈춰 꿈틀댄다(플레이어를 공격하지는 않음 — 기획서 12장).
    /// 걷기·멈춤 = 뼈대 그림(RigView — tools/rig_gen.py 가 심은 뼈를 Unity 2D Animation SpriteSkin 으로 휘게 함):
    ///   번갈아 발을 들어 내딛고(디딘 발은 땅에 붙음), 팔·무기·머리가 흔들림. 멈춰 있을 땐 꿈틀·허우적.
    /// 맞기·죽기 = 원래 그림(맞기 · 죽기 · 맞기 흰 실루엣). 뼈대 데이터가 없으면 예전 걷기 4장으로.
    /// </summary>
    public class Zombie : Target
    {
        public ZombieRow row;
        public double gold;
        public bool isBoss, standStill;
        Sprite[] f;
        SpriteRenderer flash, shadow;
        Vector3 shadowScale;
        float speed, hitT, flashT, dieT, idlePhase, stopDist, scale, stride, walkDist;
        int dir = -1;          // 보는 방향 (-1 = 왼쪽)
        Vector2 knock;
        RigView rig;           // 뼈대 그림(없으면 null)
        bool retreating;       // 보스 실패: 오른쪽으로 물러나는 중
        float fallFrom;        // 쓰러질 때 높이

        const float HitTime = 0.16f, FlashTime = 0.09f;

        public static Zombie Spawn(ZombieRow r, Vector2 pos, double hp, double gold, float scaleMul = 1f, bool boss = false)
        {
            var go = new GameObject("Z_" + r.id);
            go.transform.position = pos;
            var z = go.AddComponent<Zombie>();
            z.row = r; z.isBoss = boss;
            z.hp = z.maxHp = hp; z.gold = gold;
            z.f = Art.Zombie(r.id);
            z.scale = r.scale * scaleMul;
            z.MakeBody(z.f[Art.ZWalk], z.scale);
            z.rig = RigView.Create(z.body, "z_" + r.id);
            if (z.rig) { z.rig.Flip = true; z.sr.enabled = false; }
            z.speed = Random.Range(r.speedMin, r.speedMax);      // Zombie_Master Actual_Speed_Min~Max
            z.idlePhase = Random.value * 10f;
            z.walkDist = Random.value;
            z.stride = 0.15f * z.scale;                 // 이만큼 걸을 때마다 다음 걷기 그림
            z.stopDist = Random.Range(r.stopMin, r.stopMax);     // 멈춘 좀비가 한 줄로 겹치지 않게 앞뒤로 퍼짐 (StopDist_Min~Max)
            z.height = r.height * scaleMul;
            z.radius = r.radius * scaleMul;
            z.sr.flipX = true;      // 그림은 오른쪽을 봄 → 분대(왼쪽)를 보게

            var fg = new GameObject("Flash");
            fg.transform.SetParent(z.body, false);
            z.flash = fg.AddComponent<SpriteRenderer>();
            z.flash.sprite = z.f[Art.ZFlash];
            z.flash.color = new Color(1, 1, 1, 0);
            // 발밑 그림자 (기어 다니는 좀비는 몸이 길어서 넓게)
            z.shadow = Art.Shadow(go.transform, r.shadowW * scaleMul, r.shadowH * scaleMul);
            z.shadowScale = z.shadow.transform.localScale;
            return z;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (retreating) { RetreatTick(dt); return; }
            if (dead) { DieAnim(dt); return; }

            // 넉백
            if (knock.sqrMagnitude > 0.0001f)
            {
                transform.position = Field.I.Clamp((Vector2)transform.position + knock * dt, 0.3f);
                knock = Vector2.MoveTowards(knock, Vector2.zero, dt * 10f);
            }

            bool moving = false;
            var sq = Squad.I;
            if (!standStill && hitT <= 0 && sq != null && sq.Leader != null)
            {
                // 왼쪽으로 곧장 걸어와 분대 맨 앞(가장 오른쪽 카피바라)에서 stopDist 만큼 떨어져 멈춤 — 높이는 그대로
                float front = float.MinValue;
                foreach (var c in sq.cats) front = Mathf.Max(front, c.transform.position.x);
                Vector2 to = new(front + stopDist - Pos.x, 0f);
                if (to.x < 0f)
                {
                    Vector2 step = new Vector2(-speed * dt, 0f);
                    // 서로 겹치지 않게 살짝 밀기
                    foreach (var t in All)
                    {
                        if (t == this || !(t is Zombie) || t.dead) continue;
                        Vector2 d = Pos - t.Pos;
                        float m = d.magnitude, want = (radius + t.radius) * 0.9f;
                        if (m < want && m > 0.001f) step += d / m * (want - m) * dt * 4f;
                    }
                    Vector2 before = Pos;
                    transform.position = Field.I.Clamp(Pos + step, 0.3f);
                    walkDist += ((Vector2)transform.position - before).magnitude / stride;
                    moving = true;
                    if (Mathf.Abs(to.x) > 0.05f) dir = to.x < 0 ? -1 : 1;
                }
            }
            sr.flipX = flash.flipX = dir < 0;

            if (hitT > 0)
            {
                // 맞음: 맞기 그림 + 흰색 번쩍 + 찌그러졌다 돌아옴 + 뒤로 젖힘
                hitT -= dt;
                float k = Mathf.Clamp01(hitT / HitTime);
                sr.sprite = f[Art.ZHit];
                ShowRig(false);
                body.localScale = new Vector3(scale * (1f + k * 0.14f), scale * (1f - k * 0.12f), 1);
                body.localRotation = Quaternion.Euler(0, 0, -dir * -8f * k);
                body.localPosition = Vector3.zero;
            }
            else
            {
                int frame = 1;
                float bob = 0, rot = 0;
                if (rig)
                {
                    // 뼈대: 걸은 거리로 걸음 위상(옛 걷기 4장 = 두 걸음), 멈추면 꿈틀
                    ShowRig(true);
                    rig.Flip = dir < 0;
                    if (moving) rig.Walk(walkDist / 4f % 1f);
                    else { idlePhase += dt * (standStill ? 2.2f : 3.5f); rig.Idle(idlePhase); }
                    body.localScale = Vector3.one * scale;
                    body.localPosition = Vector3.zero;
                    body.localRotation = Quaternion.Euler(0, 0, moving ? -dir * 1.5f : 0f);
                    shadow.transform.localScale = shadowScale;
                }
                else if (moving)
                {
                    frame = (int)walkDist % 4;
                    float ph = walkDist * Mathf.PI;                 // 프레임 2장마다 한 번 오르내림
                    bob = Mathf.Abs(Mathf.Cos(ph)) * 0.045f * scale; // 다리 모은 프레임(1·3)에서 위
                    rot = -dir * (3f + Mathf.Sin(ph * 0.5f) * 2.5f); // 앞으로 기울고 좌우로 비틀
                }
                else
                {
                    // 멈춰 있을 때: 다리 모은 그림으로 숨 쉬듯 흔들 (분대 앞에서 꿈틀)
                    idlePhase += dt * (standStill ? 2.2f : 3.5f);
                    frame = 1;
                    bob = 0;
                    rot = Mathf.Sin(idlePhase) * 3f;
                    float br = Mathf.Sin(idlePhase * 2f) * 0.025f;
                    body.localScale = new Vector3(scale * (1 - br * 0.5f), scale * (1 + br), 1);
                }
                if (!rig)
                {
                    sr.sprite = f[Art.ZWalk + frame];
                    if (moving) body.localScale = Vector3.one * scale;
                    body.localPosition = new Vector3(0, bob, 0);
                    body.localRotation = Quaternion.Euler(0, 0, rot);
                    shadow.transform.localScale = shadowScale * (1f - bob / scale * 1.6f);
                }
            }

            if (flashT > 0)
            {
                flashT -= dt;
                flash.sprite = f[Art.ZFlash];
                flash.color = new Color(1, 1, 1, Mathf.Clamp01(flashT / FlashTime) * 0.85f);
            }
            else flash.color = new Color(1, 1, 1, 0);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (flash) flash.sortingOrder = sr.sortingOrder + 1;
            if (rig) rig.sr.sortingOrder = sr.sortingOrder;
        }

        /// <summary>보스 실패: 더 맞지 않고(목표에서 빠짐) 오른쪽 화면 밖으로 걸어 나간 뒤 사라짐</summary>
        public void Retreat()
        {
            retreating = true;
            dead = true;           // 총알·조준 대상에서 빠짐 (OnTargetDead 는 안 부름 = 보상 없음)
            HideBar();
            hitT = 0;
        }

        void RetreatTick(float dt)
        {
            dir = 1;
            var p = transform.position;
            float sp = Mathf.Max(speed, 1.2f) * 2.2f;
            p.x += sp * dt;
            transform.position = p;
            walkDist += sp * dt / stride;
            sr.flipX = flash.flipX = false;
            flash.color = new Color(1, 1, 1, 0);
            body.localScale = Vector3.one * scale;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            if (rig) { ShowRig(true); rig.Flip = false; rig.Walk(walkDist / 4f % 1f); }
            else sr.sprite = f[Art.ZWalk + (int)walkDist % 4];
            if (p.x > Field.I.ViewHalfW + 3f) Destroy(gameObject);
        }

        /// <summary>뼈대 그림 ↔ 원래 그림(맞기·죽기) 바꾸기</summary>
        void ShowRig(bool on)
        {
            if (!rig) return;
            if (rig.sr.enabled != on) rig.sr.enabled = on;
            sr.enabled = !on;
        }

        /// <summary>쓰러짐: 털썩 떨어지며 한 번 튀고, 잠깐 누워 있다가 깜빡이며 사라짐</summary>
        void DieAnim(float dt)
        {
            dieT += dt;
            const float fall = 0.2f;
            if (knock.sqrMagnitude > 0.0001f)
            {
                transform.position += (Vector3)(knock * dt);
                knock = Vector2.MoveTowards(knock, Vector2.zero, dt * 8f);
            }
            float y, sy;
            if (dieT < fall)
            {
                float k = dieT / fall;
                y = fallFrom * (1 - k * k);
                sy = 1.18f - k * 0.18f;
            }
            else
            {
                float k = Mathf.Clamp01((dieT - fall) / 0.18f);
                y = Mathf.Sin(k * Mathf.PI) * 0.06f * scale;        // 바닥에 닿고 한 번 튐
                sy = 1f - Mathf.Sin(k * Mathf.PI) * 0.1f;
            }
            body.localPosition = new Vector3(0, y, 0);
            body.localScale = new Vector3(scale * (2f - sy), scale * sy, 1);
            // 쓰러지면 그림자가 몸 길이만큼 넓어지고, 몸과 같이 사라짐
            float spread = Mathf.Clamp01(dieT / 0.2f);
            shadow.transform.localScale = new Vector3(shadowScale.x * (1f + spread * 0.6f), shadowScale.y, 1);
            shadow.color = new Color(1, 1, 1, sr.color.a);
            body.localRotation = Quaternion.identity;
            flash.color = new Color(1, 1, 1, 0);

            if (dieT > 0.75f)
            {
                bool on = ((int)((dieT - 0.75f) / 0.07f)) % 2 == 0;     // 깜빡깜빡
                float a = Mathf.Clamp01(1 - (dieT - 0.75f) / 0.4f);
                sr.color = new Color(1, 1, 1, on ? a : a * 0.25f);
            }
            if (dieT > 1.15f) Destroy(gameObject);
        }

        public override void Hit(double dmg, Vector2 from)
        {
            if (dead) return;
            hp -= dmg;
            hitT = HitTime;
            flashT = FlashTime;
            if (!isBoss) knock = (Pos - from).normalized * 1.1f;
            Fx.I?.Damage(Center + Random.insideUnitCircle * 0.25f, dmg, isBoss);
            if (hp <= 0) Die(from);
            else { AudioManager.Play(AudioManager.Sfx.Hit); ShowBar(isBoss); }
        }

        void Die(Vector2 from)
        {
            dead = true;
            hp = 0;
            HideBar();
            sr.sprite = f[Art.ZDie];
            sr.color = Color.white;
            ShowRig(false);
            fallFrom = 0.35f * scale;
            knock = (Pos - from).normalized * (isBoss ? 0.5f : 1.8f);
            dir = (Pos - from).x > 0 ? 1 : -1;      // 맞은 쪽 반대로 쓰러짐 (죽기 그림은 머리가 오른쪽)
            sr.flipX = flash.flipX = dir < 0;
            AudioManager.Play(isBoss ? AudioManager.Sfx.BossDead : AudioManager.Sfx.Dead);
            GameManager.I?.OnTargetDead(this);
        }
    }
}
