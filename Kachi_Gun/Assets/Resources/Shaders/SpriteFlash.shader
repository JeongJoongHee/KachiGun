// 11-04 맞을 때 번쩍임: 스프라이트(뼈대로 휜 그림 포함)를 그 모양 그대로 하얗게 물들임.
// 예전엔 "맞기 흰 실루엣" 그림을 위에 덮어서 자세가 안 맞고 하얀 덩어리처럼 보였음.
// _Flash (0~1) 는 SpriteRenderer 마다 MaterialPropertyBlock 으로 (Core/Art.cs Art.SetFlash).
// 2D 조명을 쓰지 않는 게임이라 조명 없는 셰이더 = 기본 Sprite-Lit 과 같은 밝기.
// 11-11 색 변형(요청_03): 초록 피부만 붉은 · 푸르딩딩 · 보라로. 색 번호 k(1~3)는 꼭짓점 색 초록 채널에 숨김
//   (SpriteRenderer.color.g = 1 - k*8/255, Art.TintColor) — 값 블록을 안 써서 같은 재질끼리 묶여 그려짐.
//   Linear 색 공간이면 꼭짓점 색이 감마→리니어로 바뀌어 들어오므로 감마로 되돌려 읽음 (11-11 첫 판은 이걸 빠뜨려 붉은 → 푸른으로 나왔음).
//   식은 tools/doodle_art.py tint_skin() · TINTS 와 같음 (피부 색조 95° 근처 · 채도·밝기가 어느 정도 있는 곳만).
Shader "KachiGun/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Flash;
            fixed4 _FlashColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
            }

            float3 hsv2rgb(float3 c)
            {
                float3 rgb = saturate(abs(frac(c.x + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return c.z * lerp(float3(1, 1, 1), rgb, c.y);
            }

            // k = 1 붉은 · 2 푸르딩딩 · 3 보라 → (목표 색조°, 채도 배, 밝기 배)
            float3 skinTint(float3 col, float k)
            {
                float3 prm = k < 1.5 ? float3(6.0, 0.95, 1.0) : (k < 2.5 ? float3(200.0, 0.62, 1.03) : float3(272.0, 0.80, 1.0));
                float3 hsv = rgb2hsv(col);
                float hdeg = hsv.x * 360.0;
                float w = (1.0 - smoothstep(28.0, 48.0, abs(hdeg - 95.0))) * smoothstep(0.10, 0.22, hsv.y) * smoothstep(0.12, 0.30, hsv.z);
                float3 to = float3(frac((hdeg + prm.x - 95.0) / 360.0), saturate(hsv.y * prm.y), saturate(hsv.z * prm.z));
                return lerp(col, hsv2rgb(to), w);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 vc = i.color;
            #if defined(UNITY_COLORSPACE_GAMMA)
                float gs = vc.g;
            #else
                float gs = LinearToGammaSpaceExact(vc.g);
            #endif
                float k = round((1.0 - gs) * 255.0 / 8.0);
                fixed4 c = tex2D(_MainTex, i.uv);
                if (k > 0.5 && k < 3.5)
                {
                    // 색 돌리기는 감마 값으로 (시안 tint_skin 과 같은 색) — Linear 면 바꿨다 되돌림
                #if defined(UNITY_COLORSPACE_GAMMA)
                    c.rgb = skinTint(c.rgb, k);
                #else
                    float3 g3 = float3(LinearToGammaSpaceExact(c.r), LinearToGammaSpaceExact(c.g), LinearToGammaSpaceExact(c.b));
                    g3 = skinTint(g3, k);
                    c.rgb = float3(GammaToLinearSpaceExact(g3.r), GammaToLinearSpaceExact(g3.g), GammaToLinearSpaceExact(g3.b));
                #endif
                    vc.g = 1;
                }
                c *= vc;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _Flash);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
