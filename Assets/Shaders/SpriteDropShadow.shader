Shader "Custom/SpriteDropShadow"
{
    // Sprites/Default 와 동일하게 보이는 메인 패스 + 월드 오프셋 그림자 프리패스.
    // SpriteRenderer 하나에서 그림자까지 함께 그리므로 별도 그림자 렌더러/매 프레임 동기화가 필요 없다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.3)
        _ShadowOffset ("Shadow World Offset", Vector) = (0.16,-0.02,-0.16,0)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        // ── Pass 1: 그림자 (월드 오프셋, 단색·반투명) ──
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _ShadowColor;
            float4 _ShadowOffset;

            struct appdata_t { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float4 worldPos = mul(unity_ObjectToWorld, IN.vertex);
                worldPos.xyz += _ShadowOffset.xyz;
                OUT.vertex = mul(UNITY_MATRIX_VP, worldPos);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed a = tex2D(_MainTex, IN.texcoord).a * IN.color.a * _ShadowColor.a;
                fixed4 c = fixed4(_ShadowColor.rgb, a);
                c.rgb *= c.a; // premultiplied alpha (Blend One OneMinusSrcAlpha)
                return c;
            }
            ENDCG
        }

        // ── Pass 2: 스프라이트 본체 (Sprites/Default 동일) ──
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            struct appdata_t { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
