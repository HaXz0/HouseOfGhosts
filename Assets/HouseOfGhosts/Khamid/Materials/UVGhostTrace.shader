Shader "HouseOfGhosts/UVGhostTrace"
{
    Properties
    {
        _MainTex ("Optional Trace Texture (Alpha)", 2D) = "white" {}
        [HDR] _UVGlowColor ("UV Fluorescent Color", Color) = (0.15, 1.0, 0.78, 1.0)
        _RevealAmount ("UV Reveal Amount (0 = Hidden, 1 = Revealed)", Range(0.0, 1.0)) = 0.0
        _TraceShape ("Trace Shape (0 = Footprint, 1 = WallPhaseRift, 2 = ResidualRing)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UVTraceForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _UVGlowColor;
                float _RevealAmount;
                float _TraceShape;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                if (_RevealAmount <= 0.002)
                {
                    return half4(0, 0, 0, 0);
                }

                float2 centered = (input.uv - 0.5) * 2.0;
                float shapeMask = 0.0;
                float edgeBoost = 0.0;

                if (_TraceShape < 0.5)
                {
                    // Shape 0: Floor Footprint (oval sole + heel)
                    float2 footUV = centered;
                    footUV.x *= 1.35;

                    float soleDist = length(footUV - float2(0.0, 0.22));
                    float heelDist = length((footUV - float2(0.0, -0.42)) * float2(1.25, 1.4));

                    float soleMask = smoothstep(0.62, 0.2, soleDist);
                    float heelMask = smoothstep(0.48, 0.15, heelDist);
                    float proceduralFootprint = saturate(soleMask + heelMask);

                    edgeBoost = smoothstep(0.65, 0.45, min(soleDist, heelDist)) * 0.5;
                    shapeMask = saturate(proceduralFootprint + edgeBoost);
                }
                else if (_TraceShape < 1.5)
                {
                    // Shape 1: Wall-Phase Spectral Rift / Ripple Mark (where the ghost passed through a solid wall)
                    float radialDist = length(centered * float2(1.15, 0.85));
                    float angle = atan2(centered.y, centered.x);
                    float organicWave = sin(angle * 5.0 + _Time.y * 2.2) * 0.06 + cos(angle * 3.0 - _Time.y * 1.5) * 0.04;
                    float warpedDist = radialDist + organicWave;

                    // Vertical ghostly core + concentric phase ripples
                    float coreRift = smoothstep(0.55, 0.08, length(centered * float2(2.1, 0.92)));
                    float outerRing = smoothstep(0.88, 0.72, warpedDist) * smoothstep(0.52, 0.70, warpedDist);
                    float innerRing = smoothstep(0.56, 0.44, warpedDist) * smoothstep(0.28, 0.42, warpedDist);

                    edgeBoost = saturate(outerRing * 1.3 + innerRing * 1.1);
                    shapeMask = saturate(coreRift * 0.85 + edgeBoost) * smoothstep(0.98, 0.78, radialDist);
                }
                else
                {
                    // Shape 2: Hiding Spot / 60s Search Hint Pulsing Ectoplasm Ring
                    float dist = length(centered);
                    float pulse = 0.5 + 0.5 * sin(_Time.y * 4.0 - dist * 6.0);
                    float mainRing = smoothstep(0.86, 0.68, dist) * smoothstep(0.42, 0.64, dist);
                    float centerGlow = smoothstep(0.45, 0.0, dist) * (0.45 + 0.35 * pulse);

                    edgeBoost = mainRing * (0.9 + 0.4 * pulse);
                    shapeMask = saturate(centerGlow + edgeBoost) * smoothstep(0.98, 0.82, dist);
                }

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float finalMask = shapeMask * texSample.a;

                float alpha = saturate(finalMask * _RevealAmount * _UVGlowColor.a);
                half3 rgb = _UVGlowColor.rgb * (half)(1.25 + edgeBoost);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
