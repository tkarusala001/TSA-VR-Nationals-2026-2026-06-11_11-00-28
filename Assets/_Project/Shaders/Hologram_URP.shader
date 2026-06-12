// -----------------------------------------------------------------------------
//  Hologram_URP.shader
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A lightweight URP hologram used for the reveal-chamber sculpture stages and
//  small holographic accents. It is intentionally cheap for Quest:
//    * No textures (all procedural), no loops, single forward pass.
//    * Fresnel rim glow + scrolling scanlines for the "projected light" look.
//    * A procedural DISSOLVE driven by _Dissolve (0 = solid, 1 = gone) with a hot
//      emissive edge — this is what FinalRevealController animates to cross-fade
//      the Roman → gears → circuit stages.
//
//  Exposes _EmissionColor and _Dissolve so the C# (MaterialPropertyBlock) paths
//  in the project can drive it without material instances.
// -----------------------------------------------------------------------------

Shader "DECRYPTED/Hologram_URP"
{
    Properties
    {
        _BaseColor      ("Base Color", Color)        = (0.2, 0.85, 1.0, 0.35)
        [HDR] _EmissionColor ("Emission", Color)     = (0.2, 0.9, 1.0, 1.0)
        [HDR] _RimColor ("Rim Color", Color)         = (0.4, 1.0, 1.0, 1.0)
        _RimPower       ("Rim Power", Range(0.5, 8)) = 2.5
        _ScanSpeed      ("Scan Speed", Float)        = 1.5
        _ScanTiling     ("Scan Tiling", Float)       = 60.0
        _ScanStrength   ("Scan Strength", Range(0,1))= 0.35
        _Dissolve       ("Dissolve", Range(0,1))     = 0.0
        _DissolveEdge   ("Dissolve Edge Width", Range(0.001,0.3)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color)   = (1.0, 0.7, 0.2, 1.0)
        _Alpha          ("Overall Alpha", Range(0,1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _RimColor;
                float  _RimPower;
                float  _ScanSpeed;
                float  _ScanTiling;
                float  _ScanStrength;
                float  _Dissolve;
                float  _DissolveEdge;
                float4 _DissolveEdgeColor;
                float  _Alpha;
            CBUFFER_END

            // Cheap 2D hash noise (no texture lookups) for the dissolve mask.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv          = IN.uv;
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // --- dissolve --------------------------------------------------
                float n = hash21(floor(IN.uv * 48.0));
                // Clip where noise is below the dissolve threshold.
                float cut = n - _Dissolve;
                clip(cut + _DissolveEdge);             // discard fully-dissolved fragments
                float edge = 1.0 - saturate(cut / _DissolveEdge); // hot rim near the cut

                // --- fresnel rim ----------------------------------------------
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fres = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // --- scrolling scanlines --------------------------------------
                float scan = sin((IN.uv.y * _ScanTiling) + _Time.y * _ScanSpeed);
                scan = saturate(scan) * _ScanStrength;

                // --- compose ---------------------------------------------------
                half3 col = _BaseColor.rgb + _EmissionColor.rgb * (0.35 + scan);
                col += _RimColor.rgb * fres;
                col += _DissolveEdgeColor.rgb * edge * 2.0;

                half alpha = saturate(_BaseColor.a * _Alpha + fres * 0.5 + scan + edge);

                col = MixFog(col, IN.fogCoord);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
