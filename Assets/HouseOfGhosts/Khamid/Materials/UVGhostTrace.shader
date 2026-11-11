Shader "HouseOfGhosts/UVGhostTrace"
{
    Properties
    {
        _MainTex ("Optional Trace Texture (Alpha)", 2D) = "white" {}
        [HDR] _UVGlowColor ("UV Fluorescent Color", Color) = (0.15, 1.0, 0.75, 1.0)
        _RevealAmount ("UV Reveal Amount (0 = Hidden, 1 = Revealed)", Range(0.0, 1.0)) = 0.0
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

                // Procedural footprint / spectral step shape (oval sole + heel) so it looks great even without a texture
                float2 centered = (input.uv - 0.5) * 2.0;
                centered.x *= 1.35; // Narrower horizontally like a shoe/foot print

                float soleDist = length(centered - float2(0.0, 0.22));
                float heelDist = length((centered - float2(0.0, -0.42)) * float2(1.25, 1.4));

                float soleMask = smoothstep(0.62, 0.2, soleDist);
                float heelMask = smoothstep(0.48, 0.15, heelDist);
                float proceduralFootprint = saturate(soleMask + heelMask);

                // Add subtle glowing edge ring
                float edgeRing = smoothstep(0.65, 0.45, min(soleDist, heelDist)) * 0.5;
                float shapeMask = saturate(proceduralFootprint + edgeRing);

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float finalMask = shapeMask * texSample.a;

                float alpha = saturate(finalMask * _RevealAmount * _UVGlowColor.a);
                half3 rgb = _UVGlowColor.rgb * (1.2 + edgeRing);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
