Shader "HouseOfGhosts/DangerVignetteOverlay"
{
    Properties
    {
        _VignetteColor ("Danger Vignette Color", Color) = (0.55, 0.03, 0.06, 1.0)
        _OuterShadowColor ("Outer Corner Shadow Color", Color) = (0.05, 0.0, 0.01, 1.0)
        _ColdVeilColor ("Cold Horror Veil Color", Color) = (0.08, 0.14, 0.22, 1.0)
        _Intensity ("Vignette Intensity", Range(0.0, 1.0)) = 0.0
        _Smoothness ("Vignette Feather Smoothness", Range(0.15, 1.0)) = 0.55
        _PulseBoost ("Heartbeat Pulse Boost", Range(0.0, 0.35)) = 0.0
        _ColdVeilStrength ("Cold Veil Strength", Range(0.0, 0.5)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+450"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DangerVignetteOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _VignetteColor;
                half4 _OuterShadowColor;
                half4 _ColdVeilColor;
                float _Intensity;
                float _Smoothness;
                float _PulseBoost;
                float _ColdVeilStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 clipPos    : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.clipPos = output.positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float totalStrength = saturate(_Intensity + _PulseBoost);
                if (totalStrength <= 0.001 && _ColdVeilStrength <= 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                // Clip-space NDC: (0,0) at optical center of active eye/screen, +-1 at edges, 1.414 at corners
                float2 ndc = input.clipPos.xy / max(0.0001, abs(input.clipPos.w));
                float radialDist = length(ndc);

                // Keep VR center vision (>0.38 radius) clear to prevent motion sickness while framing periphery
                float innerRadius = lerp(1.18, 0.38, totalStrength);
                float featherWidth = lerp(0.18, 0.68, saturate(_Smoothness));
                float vignetteRing = smoothstep(innerRadius, innerRadius + featherWidth, radialDist);

                // Subtle cold horror tint across mid-to-outer periphery
                float coldVeilMask = smoothstep(0.32, 1.22, radialDist) * _ColdVeilStrength;

                // Rich customizable Danger Vignette Color blending slightly darker at the extreme corners
                float outerCornerFactor = smoothstep(0.95, 1.42, radialDist) * 0.45;
                half3 ringRgb = lerp(_VignetteColor.rgb, _OuterShadowColor.rgb, outerCornerFactor);

                float ringAlpha = vignetteRing * saturate(totalStrength * 1.25) * _VignetteColor.a;
                float finalAlpha = saturate(ringAlpha + coldVeilMask);

                float coldMix = coldVeilMask / max(0.001, ringAlpha + coldVeilMask);
                half3 finalRgb = lerp(ringRgb, _ColdVeilColor.rgb, coldMix * 0.45);

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
