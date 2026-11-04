// 11-04 맞을 때 번쩍임: 스프라이트(뼈대로 휜 그림 포함)를 그 모양 그대로 하얗게 물들임.
// 예전엔 "맞기 흰 실루엣" 그림을 위에 덮어서 자세가 안 맞고 하얀 덩어리처럼 보였음.
// _Flash (0~1) 는 SpriteRenderer 마다 MaterialPropertyBlock 으로 (Core/Art.cs Art.SetFlash).
// 2D 조명을 쓰지 않는 게임이라 조명 없는 셰이더 = 기본 Sprite-Lit 과 같은 밝기.
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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _Flash);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
