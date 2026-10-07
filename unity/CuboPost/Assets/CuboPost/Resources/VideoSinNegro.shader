// Video para la UI que vuelve transparente el negro puro (#000). Lo usan "cierre de círculo" (el
// círculo #252525 se cierra sobre negro puro) y la insignia (su círculo crema entra y sale sobre
// negro puro): ese negro deja ver la pantalla de post. de atrás.
// Respeta los recortes de la UI (RectMask2D). Está en Resources para que se incluya en el build.
Shader "CuboPost/VideoSinNegro"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video", 2D) = "black" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Desde ("Negro (transparente)", Float) = 0.002
        _Hasta ("Opaco desde", Float) = 0.012
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Desde, _Hasta;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // En espacio lineal el #252525 del círculo vale ~0,018: por debajo de _Desde es negro puro.
                float m = max(c.r, max(c.g, c.b));
                float a = smoothstep(_Desde, _Hasta, m);
                fixed4 col = fixed4(c.rgb / max(a, 0.001), a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
