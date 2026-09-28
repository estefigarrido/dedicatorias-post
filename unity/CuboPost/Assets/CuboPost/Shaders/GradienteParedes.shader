// Fondo de las pantallas LED del cubo post.
// Negro con degradés leves que se mueven lento (como una luz desenfocada con grano),
// en el verde y el lila de las notas. Usa el UV.x como posición en el perímetro del cubo
// (0..1), así las luces pasan de una pared a la otra sin cortes en las esquinas.
Shader "CuboPost/GradienteParedes"
{
    Properties
    {
        _Base ("Fondo", Color) = (0.022, 0.022, 0.026, 1)
        _C1 ("Verde", Color) = (0.286, 0.722, 0.404, 1)
        _C2 ("Lila", Color) = (0.671, 0.541, 0.902, 1)
        _Perimetro ("Perímetro del cubo (m)", Float) = 46
        _Alto ("Alto de la pantalla (m)", Float) = 2.8
        _Tamano ("Tamaño de las luces (m)", Float) = 2.5
        _Velocidad ("Velocidad", Float) = 0.018
        _Intensidad ("Intensidad", Range(0, 1.5)) = 0.34
        _Grano ("Grano", Range(0, 0.15)) = 0.018
        _Brillo ("Brillo LED", Range(0.2, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Base, _C1, _C2;
                float _Perimetro, _Alto, _Tamano, _Velocidad, _Intensidad, _Grano, _Brillo;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            // Luz gaussiana; la distancia horizontal da la vuelta al perímetro.
            float Luz(float2 p, float2 c, float r)
            {
                float2 d = p - c;
                d.x -= _Perimetro * round(d.x / _Perimetro);
                return exp(-dot(d, d) / (r * r));
            }

            float Ruido(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = float2(i.uv.x * _Perimetro, i.uv.y * _Alto);
                float t = _Time.y * _Velocidad;

                half3 luz = 0;
                [unroll]
                for (int k = 0; k < 8; k++)
                {
                    float fk = (float)k;
                    // Cada luz recorre el perímetro a su ritmo y sube/baja suavemente.
                    float x = frac(fk / 8.0 + t * (0.5 + 0.2 * sin(fk * 1.9))) * _Perimetro + sin(t * 5.0 + fk) * 1.8;
                    // Algunas luces salen de la pantalla por arriba o por abajo: queda más negro.
                    float y = _Alto * (0.5 + 0.75 * sin(t * 7.0 + fk * 2.1));
                    float r = _Tamano * (0.7 + 0.35 * sin(t * 4.0 + fk * 2.7));
                    // Cada luz "respira": se prende y se apaga despacio, a destiempo de las otras.
                    float a = saturate(0.45 + 0.6 * sin(t * 11.0 + fk * 1.3));
                    half3 c = (k % 2 == 0) ? _C1.rgb : _C2.rgb;
                    luz += c * Luz(p, float2(x, y), r) * a;
                }

                half3 color = _Base.rgb + luz * _Intensidad;
                // Grano de película, como la referencia.
                float g = Ruido(i.uv * float2(9200.0, 1900.0) + frac(_Time.y * 7.0) * 61.0) - 0.5;
                color += g * _Grano;
                return half4(max(color, 0) * _Brillo, 1);
            }
            ENDHLSL
        }
    }
}
