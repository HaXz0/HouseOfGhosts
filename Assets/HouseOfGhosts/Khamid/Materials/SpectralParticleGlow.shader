Shader "HouseOfGhosts/SpectralParticleGlow"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _GlowBoost ("Glow Brightness", Range(0.5, 5.0)) = 1.9
        [Enum(EmberOrStreak, 0, ShockwaveRing, 1)] _ShapeMode ("Particle Shape Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            // Alpha blending so BOTH pitch-black (0,0,0) shadow motes AND radiant white (1,1,1) embers render crisply!
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _GlowBoost;
                float _ShapeMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
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
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centeredUV = input.uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                if (dist >= 1.0)
                {
                    return half4(0, 0, 0, 0);
                }

                float shapeMask = 0.0;
                if (_ShapeMode > 0.5)
                {
                    // Mode 1: Crisp hollow shockwave ring
                    float ringDist = abs(dist - 0.72);
                    float ringBand = smoothstep(0.22, 0.0, ringDist);
                    float outerFade = smoothstep(1.0, 0.85, dist);
                    shapeMask = ringBand * outerFade;
                }
                else
                {
                    // Mode 0: Hot pinpoint core + smooth outer halo (works for both White glow and Black ink motes!)
                    float radial = saturate(1.0 - dist);
                    float core = pow(radial, 5.0) * 1.65;
                    float halo = pow(radial, 1.80) * 0.75;
                    shapeMask = core + halo;
                }

                half3 finalRgb = input.color.rgb * _TintColor.rgb * (half)(_GlowBoost * max(0.4, shapeMask));
                half finalAlpha = (half)saturate(input.color.a * _TintColor.a * saturate(shapeMask));
                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
