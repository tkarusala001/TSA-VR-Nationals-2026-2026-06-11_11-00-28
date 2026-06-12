// -----------------------------------------------------------------------------
//  EnergyScanline_URP.shader
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  An additive URP "energy field" used for the vault's data-stream panels and
//  modern-security accents: a softly pulsing grid with a bright scan band that
//  sweeps across the surface. Like the hologram it is texture-free, loop-free and
//  single-pass for Quest, and uses additive blending so it reads as emitted light
//  rather than a surface. Pair it with bloom in the URP post stack for the glow.
//
//  _EmissionColor is exposed so brightness can be driven per-instance from C#
//  via a MaterialPropertyBlock.
// -----------------------------------------------------------------------------

Shader "DECRYPTED/EnergyScanline_URP"
{
    Properties
    {
        [HDR] _EmissionColor ("Emission", Color)      = (0.15, 0.8, 1.0, 1.0)
        _GridTiling   ("Grid Tiling", Float)          = 18.0
        _GridWidth    ("Grid Line Width", Range(0.001,0.2)) = 0.04
        _GridStrength ("Grid Strength", Range(0,2))   = 0.6
        _ScanSpeed    ("Scan Speed", Float)           = 0.6
        _ScanWidth    ("Scan Band Width", Range(0.01,0.6)) = 0.12
        _ScanBoost    ("Scan Brightness", Range(0,4)) = 2.0
        _Pulse        ("Pulse Speed", Float)          = 2.0
        _Alpha        ("Overall Alpha", Range(0,1))   = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode"="UniversalForward" }

            Blend One One          // additive
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  fogCoord    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
                float  _GridTiling;
                float  _GridWidth;
                float  _GridStrength;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanBoost;
                float  _Pulse;
                float  _Alpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // --- grid ------------------------------------------------------
                float2 g = frac(IN.uv * _GridTiling);
                float2 lines = smoothstep(0.0, _GridWidth, g) *
                               smoothstep(0.0, _GridWidth, 1.0 - g);
                float grid = (1.0 - min(lines.x, lines.y)) * _GridStrength;

                // --- sweeping scan band (vertical travel) ----------------------
                float scanPos = frac(_Time.y * _ScanSpeed);
                float d = abs(IN.uv.y - scanPos);
                d = min(d, 1.0 - d); // wrap so the band is seamless
                float scan = smoothstep(_ScanWidth, 0.0, d) * _ScanBoost;

                // --- gentle global pulse --------------------------------------
                float pulse = 0.75 + 0.25 * sin(_Time.y * _Pulse);

                half3 col = _EmissionColor.rgb * (grid + scan) * pulse;
                half a = saturate((grid + scan) * _Alpha);

                col = MixFog(col, IN.fogCoord);
                return half4(col * a, a); // pre-mult into additive
            }
            ENDHLSL
        }
    }
    FallBack Off
}
