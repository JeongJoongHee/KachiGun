using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace KachiGun
{
    /// <summary>
    /// 뼈대(리깅) 데이터: tools/rig_gen.py 가 만든 Resources/Art/Rig/&lt;이름&gt;.txt 를 읽어
    /// 그림 한 장 + 그물(꼭짓점·삼각형) + 뼈 + 무게가 든 스프라이트를 만든다 (Unity 2D Animation SpriteSkin 용).
    /// 이름: 좀비 = z_&lt;id&gt;, 카피바라 = c_&lt;스킨 번호 2자리&gt;.
    /// </summary>
    public class RigData
    {
        public string name, kind;
        public float ppu;
        public Vector2 pivotPx;
        public readonly Dictionary<string, float> anim = new();
        public readonly List<string> boneNames = new();
        public readonly List<int> boneParent = new();
        public readonly List<Vector2> bonePos = new();   // 발(피벗) 기준 칸 단위 위치
        public Sprite sprite;

        static readonly Dictionary<string, RigData> cache = new();

        public static RigData Get(string name)
        {
            if (cache.TryGetValue(name, out var d)) return d;
            var ta = Resources.Load<TextAsset>("Art/Rig/" + name);
            d = ta != null ? Parse(name, ta.text) : null;
            cache[name] = d;
            return d;
        }

        public int Bone(string n) => boneNames.IndexOf(n);
        public float A(string key, float def = 0) => anim.TryGetValue(key, out var v) ? v : def;

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static RigData Parse(string name, string text)
        {
            var d = new RigData { name = name };
            string src = null;
            Rect rect = default;
            var verts = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<ushort>();
            foreach (var raw in text.Split((char)10))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 2) continue;
                switch (p[0])
                {
                    case "src": src = p[1]; break;
                    case "rect": rect = new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])); break;
                    case "pivot": d.pivotPx = new Vector2(F(p[1]), F(p[2])); break;
                    case "ppu": d.ppu = F(p[1]); break;
                    case "kind": d.kind = p[1]; break;
                    case "anim":
                        for (int i = 1; i < p.Length; i++)
                        {
                            var kv = p[i].Split('=');
                            if (kv.Length == 2) d.anim[kv[0]] = F(kv[1]);
                        }
                        break;
                    case "bone":
                        d.boneNames.Add(p[1]);
                        d.boneParent.Add(int.Parse(p[2]));
                        d.bonePos.Add(new Vector2(F(p[3]), F(p[4])));
                        break;
                    case "v":
                        verts.Add(new Vector2(F(p[1]), F(p[2])));
                        weights.Add(new BoneWeight
                        {
                            boneIndex0 = int.Parse(p[3]), weight0 = F(p[4]),
                            boneIndex1 = int.Parse(p[5]), weight1 = F(p[6]),
                            boneIndex2 = int.Parse(p[7]), weight2 = F(p[8]),
                            boneIndex3 = int.Parse(p[9]), weight3 = F(p[10]),
                        });
                        break;
                    case "t":
                        tris.Add(ushort.Parse(p[1])); tris.Add(ushort.Parse(p[2])); tris.Add(ushort.Parse(p[3]));
                        break;
                }
            }
            var tex = Art.Tex(src);
            if (tex == null || verts.Count == 0) return null;

            // 뼈 위치: 칸 픽셀 → 피벗(발) 기준 칸 단위
            for (int i = 0; i < d.bonePos.Count; i++) d.bonePos[i] = (d.bonePos[i] - d.pivotPx) / d.ppu;

            var s = Sprite.Create(tex, rect, new Vector2(d.pivotPx.x / rect.width, d.pivotPx.y / rect.height), d.ppu, 0, SpriteMeshType.FullRect);
            s.name = "Rig_" + name;
            int n = verts.Count;
            var pos = new NativeArray<Vector3>(n, Allocator.Temp);
            var uv = new NativeArray<Vector2>(n, Allocator.Temp);
            var bw = new NativeArray<BoneWeight>(n, Allocator.Temp);
            for (int i = 0; i < n; i++)
            {
                var v = verts[i];
                pos[i] = new Vector3((v.x - d.pivotPx.x) / d.ppu, (v.y - d.pivotPx.y) / d.ppu, 0);
                uv[i] = new Vector2((rect.x + v.x) / tex.width, (rect.y + v.y) / tex.height);
                bw[i] = weights[i];
            }
            var idx = new NativeArray<ushort>(tris.ToArray(), Allocator.Temp);
            s.SetVertexCount(n);
            s.SetVertexAttribute(VertexAttribute.Position, pos);
            s.SetVertexAttribute(VertexAttribute.TexCoord0, uv);
            s.SetIndices(idx);

            int nb = d.boneNames.Count;
            var bones = new SpriteBone[nb];
            var bind = new NativeArray<Matrix4x4>(nb, Allocator.Temp);
            for (int i = 0; i < nb; i++)
            {
                int par = d.boneParent[i];
                Vector2 local = par < 0 ? d.bonePos[i] : d.bonePos[i] - d.bonePos[par];
                bones[i] = new SpriteBone
                {
                    name = d.boneNames[i], parentId = par, length = 0.1f,
                    position = new Vector3(local.x, local.y, 0), rotation = Quaternion.identity,
                };
                bind[i] = Matrix4x4.Translate(-(Vector3)d.bonePos[i]);   // 쉬는 자세의 뼈 = 회전 없음 → 되돌리기 = 위치만 빼기
            }
            s.SetBones(bones);
            s.SetBindPoses(bind);
            s.SetVertexAttribute(VertexAttribute.BlendWeight, bw);
            pos.Dispose(); uv.Dispose(); bw.Dispose(); idx.Dispose(); bind.Dispose();
            d.sprite = s;
            return d;
        }
    }

    /// <summary>
    /// 뼈대 그림 하나(SpriteRenderer + SpriteSkin + 뼈 Transform). 자세 함수(Walk·Idle·Capy)는
    /// tools/rig_gen.py 의 pose_angles 와 같은 식 — 미리보기와 게임 움직임이 같다.
    /// 각도는 오른쪽을 보는 그림 기준(+ = 반시계 = 앞으로), 왼쪽을 볼 땐 Flip 으로 통째로 뒤집음.
    /// </summary>
    public class RigView : MonoBehaviour
    {
        public RigData data;
        public SpriteRenderer sr;
        SpriteSkin skin;
        Transform[] bones;
        Vector3[] restLocal;
        readonly Dictionary<string, Transform> byName = new();

        public static RigView Create(Transform parent, string rigName)
        {
            var d = RigData.Get(rigName);
            if (d == null || d.sprite == null) return null;
            var go = new GameObject("Rig_" + rigName);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RigView>();
            r.data = d;
            r.sr = go.AddComponent<SpriteRenderer>();
            r.sr.sprite = d.sprite;
            int nb = d.boneNames.Count;
            r.bones = new Transform[nb];
            r.restLocal = new Vector3[nb];
            for (int i = 0; i < nb; i++)
            {
                var b = new GameObject(d.boneNames[i]).transform;
                int par = d.boneParent[i];
                b.SetParent(par < 0 ? go.transform : r.bones[par], false);
                Vector2 local = par < 0 ? d.bonePos[i] : d.bonePos[i] - d.bonePos[par];
                b.localPosition = local;
                r.bones[i] = b;
                r.restLocal[i] = local;
                r.byName[d.boneNames[i]] = b;
            }
            r.skin = go.AddComponent<SpriteSkin>();
            r.skin.SetRootBone(r.bones[0]);
            r.skin.SetBoneTransforms(r.bones);
            r.skin.alwaysUpdate = true;
            go.SetActive(true);
            return r;
        }

        public Transform Bone(string n) => byName.TryGetValue(n, out var t) ? t : null;
        /// <summary>쉬는 자세에서 뼈의 위치(뿌리 기준 칸 단위)</summary>
        public Vector2 RestPos(string n) { int i = data.Bone(n); return i >= 0 ? data.bonePos[i] : Vector2.zero; }

        public bool Flip { set => transform.localScale = new Vector3(value ? -1 : 1, 1, 1); }

        void ResetPose()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = restLocal[i];
                bones[i].localRotation = Quaternion.identity;
                bones[i].localScale = Vector3.one;
            }
        }

        void Rot(string n, float deg) { var t = Bone(n); if (t) t.localRotation = Quaternion.Euler(0, 0, deg); }
        void Move(string n, float dxPx, float dyPx) { var t = Bone(n); if (t) t.localPosition += new Vector3(dxPx, dyPx, 0) / data.ppu; }

        static float S(float v) => Mathf.Sin(v);
        static float C(float v) => Mathf.Cos(v);

        /// <summary>좀비 걷기 (phase 0~1 = 두 걸음)</summary>
        public void Walk(float phase)
        {
            ResetPose();
            float f = phase * Mathf.PI * 2f;
            float A = data.A("A", 18), Sw = data.A("S", 5);
            switch (data.kind)
            {
                case "walk":
                {
                    // 3/4 앞모습이라 두 다리가 좌우로 벌어져 있음 → 번갈아 발을 들어 내딛는 터벅터벅 걸음
                    float sw = A * 0.5f;
                    float lF = Mathf.Max(0, S(f)), lB = Mathf.Max(0, -S(f));
                    Rot("thighF", sw * S(f) + 10 * lF);
                    Rot("thighB", -sw * S(f) + 10 * lB);
                    Rot("shinF", -26 * lF);
                    Rot("shinB", -26 * lB);
                    Rot("spine", -3 + 1.5f * S(2 * f + 0.5f));
                    Rot("head", 2 - 3f * S(2 * f - 0.4f));
                    Rot("armF", Sw * 0.5f * S(2 * f + 1.2f) - Sw * 0.6f * C(f));
                    Rot("armB", Sw * C(f));
                    FootLift();
                    break;
                }
                case "robe":
                    Move("pelvis", 0, 2f * C(2 * f));
                    Rot("robe1", 3.5f * S(f));
                    Rot("robe2", 6f * S(f - 0.9f));
                    Rot("spine", -3 + 1.5f * S(2 * f + 0.4f));
                    Rot("head", 2 - 2.5f * S(2 * f - 0.6f));
                    break;
                case "crawl":
                    Rot("armF", 14 * S(f)); Move("armF", 0, 2.5f * Mathf.Max(0, C(f)));
                    Rot("armB", -14 * S(f)); Move("armB", 0, 2.5f * Mathf.Max(0, -C(f)));
                    Rot("spine", 2f * S(2 * f + 0.6f));
                    Rot("head", -2.5f * S(2 * f + 1.2f)); Move("head", 0, 1.5f * S(2 * f));
                    Move("pelvis", 2f * S(2 * f), 0);
                    Rot("legs", 3f * S(f + 0.5f));
                    break;
            }
        }

        /// <summary>좀비가 멈춰 있을 때(분대 앞): 몸을 꿈틀, 머리 갸웃, 팔로 허우적</summary>
        public void Idle(float t)
        {
            ResetPose();
            switch (data.kind)
            {
                case "crawl":
                    Rot("armF", -4 + 6 * S(t * 3.1f)); Rot("armB", 3 * S(t * 2.4f + 1));
                    Rot("head", 3 * S(t * 2.0f)); Move("head", 0, 1.5f * S(t * 4f));
                    Rot("spine", 1.5f * S(t * 1.6f));
                    break;
                default:
                    Rot("spine", -2 + 2f * S(t * 1.7f));
                    Rot("head", 1 + 4f * S(t * 2.3f + 1f));
                    Rot("armF", -3 + 5f * S(t * 3.1f));
                    Rot("armB", 3f * S(t * 2.6f + 0.5f));
                    Rot("robe1", 1.5f * S(t * 1.7f)); Rot("robe2", 2.5f * S(t * 1.7f - 0.8f));
                    Move("pelvis", 0, 1.2f * S(t * 3.4f) - 1.2f);
                    break;
            }
        }

        /// <summary>카피바라: t = 숨 시계, aim = 총 각도(°, 위 +), kick = 반동 0~1, walk = 걷는 중이면 0~1 위상(아니면 음수)</summary>
        public void Capy(float t, float aim, float kick, float walk)
        {
            ResetPose();
            float br = S(t * 2.2f);
            // 숨쉬기: 몸통이 살짝 늘었다 줄고, 머리는 조금 늦게 끄덕
            var sp = Bone("spine");
            if (sp) sp.localScale = new Vector3(1 - br * 0.006f, 1 + br * 0.012f, 1);
            float spine = 1.0f * br + Mathf.Clamp(aim, -40, 80) * 0.05f + kick * 5f;   // 위를 겨누면 살짝 젖힘, 쏘면 뒤로
            float head = 1.4f * S(t * 2.2f - 0.7f) + kick * 4f;
            Move("pelvis", -kick * 2.5f, 0);
            if (walk >= 0)
            {
                float f = walk * Mathf.PI * 2f, A = data.A("A", 16);
                float lF = Mathf.Max(0, S(f)), lB = Mathf.Max(0, -S(f));
                Rot("legF", A * 0.6f * S(f) + 6 * lF); Move("legF", 0, 5 * lF);
                Rot("legB", -A * 0.6f * S(f) + 6 * lB); Move("legB", 0, 5 * lB);
                Move("pelvis", 0, 4 * Mathf.Abs(S(f)));
                spine += -2 * S(f);
                head += 1.5f * S(f - 0.5f);
            }
            Rot("spine", spine);
            Rot("head", head);
        }

        /// <summary>다리를 돌린 뒤 낮은 쪽 발이 원래 바닥에 오도록 골반을 올림 (rig_gen.foot_lift 와 같음)</summary>
        void FootLift()
        {
            if (!data.anim.ContainsKey("footFx")) return;
            var shF = Bone("shinF"); var shB = Bone("shinB"); var pel = Bone("pelvis");
            if (!shF || !shB || !pel) return;
            float g = (data.A("groundY") - data.pivotPx.y) / data.ppu;
            Vector2 sF = RestPos("shinF"), sB = RestPos("shinB");
            Vector3 fF = new Vector3((data.A("footFx") - data.pivotPx.x) / data.ppu - sF.x, g - sF.y, 0);
            Vector3 fB = new Vector3((data.A("footBx") - data.pivotPx.x) / data.ppu - sB.x, g - sB.y, 0);
            float yF = transform.InverseTransformPoint(shF.TransformPoint(fF)).y;
            float yB = transform.InverseTransformPoint(shB.TransformPoint(fB)).y;
            pel.localPosition += new Vector3(0, g - Mathf.Min(yF, yB), 0);
        }
    }
}
