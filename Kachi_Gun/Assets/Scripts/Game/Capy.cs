using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 분대 한 마리(카피바라). 장착한 총알 종류대로 가장 가까운 대상을 자동 사격한다.
    /// 그림: 몸 = 뼈대 그림(RigView, tools/rig_gen.py — 사격 자세 1장에 골반·몸통·머리·두 다리 뼈) → 숨쉬기·쏠 때 뒤로 젖힘·
    /// 위를 겨누면 살짝 젖힘·걷기를 뼈로 움직임. 총(스킨별, 대상 쪽으로 돌아감)은 몸통 뼈에 붙어 같이 움직이고 총구 불꽃은 총에 붙음.
    /// 뼈대 데이터가 없으면 예전 방식(몸 시트 대기 0~3 · 사격 4~7 장 넘기기).
    /// </summary>
    public class Capy : MonoBehaviour
    {
        public int slot;
        public int weaponCode = -1;
        public WeaponRow weapon;
        public SpriteRenderer sr;
        public Target target;
        Sprite[] f;
        Vector2[] gunPos;
        float fireT, animT, shootAnimT, walkPhase;
        public bool moving;
        Transform body, gun, flash;
        RigView rig;
        Vector2 gunLocal;      // 뼈대일 때: 몸통 뼈 기준 총 자리
        float walk01;
        SpriteRenderer gunSr, flashSr, shadow;
        Vector3 shadowScale;
        int frame;

        float recoilT, aimT, shootTotal, flashT;
        float gunAngle;                       // 오른쪽을 볼 때 기준 각도(위 +)
        static float MaxUp => Tables.P("Aim_Angle_Up", 80f);
        static float MaxDown => Tables.P("Aim_Angle_Down", -45f);
        const float RecoilTime = 0.12f;
        int Facing => sr.flipX ? -1 : 1;
        /// <summary>카피바라 크기 (몸·총·그림자·총구 위치가 같이 커짐)</summary>
        public static float Scale => Tables.P("Capy_Scale", 1.2f);     // Player_Base

        public static Capy Make(int slot, Transform parent)
        {
            var go = new GameObject("Capy" + slot);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * Scale;
            var c = go.AddComponent<Capy>();
            c.slot = slot;
            c.body = new GameObject("Body").transform;
            c.body.SetParent(go.transform, false);
            c.sr = c.body.gameObject.AddComponent<SpriteRenderer>();

            c.gun = new GameObject("Gun").transform;
            c.gun.SetParent(c.body, false);
            c.gunSr = c.gun.gameObject.AddComponent<SpriteRenderer>();
            c.flash = new GameObject("Flash").transform;
            c.flash.SetParent(c.gun, false);
            c.flash.localPosition = Art.GunMuzzle;
            c.flashSr = c.flash.gameObject.AddComponent<SpriteRenderer>();
            c.flashSr.sprite = Art.MuzzleFlash;
            c.flashSr.enabled = false;

            c.SetSkin(SaveData.Cur.skin);
            c.shadow = Art.Shadow(go.transform, 1.15f, 0.32f);
            c.shadowScale = c.shadow.transform.localScale;
            c.fireT = Random.Range(0.1f, 0.4f);
            return c;
        }

        public void SetSkin(int skin)
        {
            skin = Art.ArtSkin(skin);     // 그림이 아직 없는 스킨은 기본 그림
            f = Art.Char(skin);
            sr.sprite = f[0];
            gunSr.sprite = Art.Gun(skin);
            gunPos = Art.GunPos(skin);
            // 뼈대 그림으로 바꾸기: 총을 몸통 뼈에 붙임 (뼈대는 통째로 뒤집으니 총은 따로 안 뒤집음)
            gun.SetParent(body, false);
            if (rig) Destroy(rig.gameObject);
            rig = RigView.Create(body, $"c_{skin:00}");
            sr.enabled = rig == null;
            if (rig)
            {
                var sp = rig.Bone("spine");
                gun.SetParent(sp, false);
                gunLocal = gunPos[4] - rig.RestPos("spine");
            }
            PlaceGun();
        }

        public void SetWeapon(int code)
        {
            weaponCode = code;
            weapon = code >= 0 ? Tables.Weapon(code) : null;
        }

        /// <summary>총구(총 끝) 월드 위치 — 총이 돌아간 만큼 같이</summary>
        public Vector2 MuzzlePos => gun.TransformPoint(Art.GunMuzzle);
        Vector2 GunPivot => gun.position;

        void Update()
        {
            float dt = Time.deltaTime;
            if (weapon == null) { gunSr.enabled = false; return; }
            gunSr.enabled = true;

            // 대상 고르기: 지금 대상이 살아 있고 사거리 안이면 유지
            float range = weapon.range;
            if (target == null || !target.Alive || ((Vector2)target.transform.position - (Vector2)transform.position).magnitude > range + 0.5f)
                target = Target.Nearest(transform.position, range);

            if (target != null)
            {
                float dx = target.Center.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.25f) sr.flipX = dx < 0;
            }

            // 총 겨누기: 대상이 있으면 그쪽, 없으면 천천히 수평으로
            float want = target != null ? AimAngle(target.Center) : 0f;
            gunAngle = Mathf.LerpAngle(gunAngle, want, 1f - Mathf.Exp(-dt * (target != null ? 22f : 6f)));

            fireT -= dt;
            if (fireT <= 0 && target != null && GameManager.I.CanShoot)
            {
                fireT = Stats.Interval(weapon, slot);
                gunAngle = want;            // 쏘는 순간엔 정확히 대상 쪽
                PlaceGun();
                Fire();
                shootTotal = Mathf.Min(0.21f, fireT * 0.9f);
                shootAnimT = shootTotal;
                recoilT = RecoilTime;
                flashT = 0.06f;
                flash.localScale = Vector3.one * Random.Range(0.85f, 1.1f);
            }
            if (target != null) aimT = 0.6f; else aimT -= dt;    // 대상이 사라져도 잠깐은 겨눈 채로

            // ---------- 그림 ----------
            // 사격: 5 → 6 → 7 → 겨눔 유지(4). 대상이 없으면 숨 쉬는 대기(0~3)
            animT += dt;
            float breathe = 0;
            if (shootAnimT > 0)
            {
                float k = 1 - shootAnimT / shootTotal;
                frame = k < 0.3f ? 5 : k < 0.65f ? 6 : 7;
                shootAnimT -= dt;
            }
            else if (aimT > 0)
            {
                frame = 4;
                breathe = Mathf.Sin(animT * 3.2f) * 0.012f;
            }
            else frame = (int)(animT * 5f) % 4;
            sr.sprite = f[frame];
            if (rig)
            {
                // 뼈대: 숨쉬기 · 조준 각도만큼 젖힘 · 반동 · 걷기
                rig.Flip = Facing < 0;
                if (moving) walk01 = (walk01 + dt * 2.2f) % 1f; else walk01 = 0;
                float kk = recoilT > 0 ? Mathf.Clamp01(recoilT / RecoilTime) : 0f;
                rig.Capy(animT, target != null || aimT > 0 ? gunAngle : 0f, kk, moving ? walk01 : -1f);
            }

            // 반동: 몸은 살짝 뒤로, 총은 총 방향 뒤로 밀리며 위로 튐
            float rx = 0, kick = 0;
            if (recoilT > 0) { recoilT -= dt; kick = Mathf.Clamp01(recoilT / RecoilTime); rx = -Facing * 0.03f * kick; }

            // 걸을 때(던전): 통통 + 가는 쪽으로 기울기
            float by = 0, rot = 0;
            if (moving)
            {
                walkPhase += dt * 13f;
                by = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.07f;
                rot = -Facing * (4f + Mathf.Sin(walkPhase) * 2f);
            }
            else walkPhase = 0;
            if (rig) { by = 0; body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity; body.localScale = Vector3.one; }
            else
            {
                body.localPosition = new Vector3(rx, by, 0);
                body.localRotation = Quaternion.Lerp(body.localRotation, Quaternion.Euler(0, 0, rot), dt * 12f);
                body.localScale = new Vector3(1 - breathe * 0.5f, 1 + breathe, 1);
            }
            PlaceGun(kick);

            if (flashT > 0) { flashT -= dt; flashSr.enabled = true; } else flashSr.enabled = false;

            // 몸이 뜨면 그림자가 조금 작고 옅어짐
            float lift = Mathf.Clamp01(by / 0.07f);
            shadow.transform.localScale = shadowScale * (1f - lift * 0.15f);
            shadow.color = new Color(1, 1, 1, 1f - lift * 0.25f);
        }

        /// <summary>대상 쪽 각도(오른쪽을 볼 때 기준). 총 회전 중심에서 잼</summary>
        float AimAngle(Vector2 at)
        {
            Vector2 d = at - GunPivot;
            float a = Mathf.Atan2(d.y, Mathf.Abs(d.x) < 0.001f ? 0.001f : d.x * Facing) * Mathf.Rad2Deg;
            return Mathf.Clamp(a, MaxDown, MaxUp);
        }

        /// <summary>총 자리·각도 맞추기: 지금 몸 그림 칸의 손 자리 + 바라보는 쪽으로 뒤집기 + 반동</summary>
        void PlaceGun(float kick = 0)
        {
            if (gunPos == null || gun == null) return;
            if (rig)
            {
                // 몸통 뼈 기준(뒤집기는 뼈대가 함): 각도 그대로 + 총 방향 뒤로 밀림
                float a2 = gunAngle + kick * 9f, r2 = a2 * Mathf.Deg2Rad;
                Vector2 bk = new Vector2(-Mathf.Cos(r2), -Mathf.Sin(r2)) * (0.07f * kick);
                gun.localPosition = gunLocal + bk;
                gun.localScale = Vector3.one;
                gun.localRotation = Quaternion.Euler(0, 0, a2);
                return;
            }
            int fc = Facing;
            Vector2 p = gunPos[Mathf.Clamp(frame, 0, 7)];
            float ang = gunAngle + kick * 9f;
            float r = ang * Mathf.Deg2Rad;
            Vector2 back = new Vector2(-Mathf.Cos(r), -Mathf.Sin(r)) * (0.07f * kick);   // 총 방향 반대로 밀림
            gun.localPosition = new Vector3((p.x + back.x) * fc, p.y + back.y, 0);
            gun.localScale = new Vector3(fc, 1, 1);
            gun.localRotation = Quaternion.Euler(0, 0, ang * fc);
        }

        void LateUpdate()
        {
            sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
            if (rig) rig.sr.sortingOrder = sr.sortingOrder;
            gunSr.sortingOrder = sr.sortingOrder + 1;
            flashSr.sortingOrder = sr.sortingOrder + 2;
        }

        void Fire()
        {
            Vector2 m = MuzzlePos;
            Vector2 aim = target.Center - m;
            // 대상이 총구보다 뒤(총 회전 중심과 총구 사이)에 있으면 총 방향으로 쏨
            Vector2 gunDir = (Vector2)(gun.TransformPoint(Art.GunMuzzle) - gun.position);
            Vector2 dir = Vector2.Dot(aim, gunDir) > 0 ? aim.normalized : gunDir.normalized;
            double dmg = Stats.Damage(weapon, slot);
            double bm = Stats.BossMul(slot);
            var spec = Tables.Bullet(weapon.type);          // Bullet_Spec: 속도·판정·사거리 더하기·흔들림
            float range = weapon.range + spec.rangeBonus, jit = spec.jitter;
            switch (weapon.type)
            {
                case "laser":
                    Fx.I.Beam(m, dir, range, weapon.grade);
                    Bullet.LaserHit(m, dir, range, dmg, spec.hitBonus, bm);
                    break;
                case "missile":
                    for (int i = 0; i < weapon.count; i++)
                    {
                        float a = weapon.count == 1 ? 0 : Mathf.Lerp(-weapon.spread / 2, weapon.spread / 2, i / (float)(weapon.count - 1));
                        var b = Bullet.Fire(spec, m, Rot(dir, a + Random.Range(-jit, jit)), dmg, 1, range, bm);
                        b.homing = target; b.splash = weapon.splash;
                    }
                    break;
                default:   // single · fast · shotgun · pierce
                    for (int i = 0; i < weapon.count; i++)
                    {
                        float a = weapon.count == 1 ? 0 : Mathf.Lerp(-weapon.spread / 2, weapon.spread / 2, i / (float)(weapon.count - 1));
                        Bullet.Fire(spec, m, Rot(dir, a + Random.Range(-jit, jit)), dmg, weapon.pierce, range, bm);
                    }
                    break;
            }
        }

        static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
