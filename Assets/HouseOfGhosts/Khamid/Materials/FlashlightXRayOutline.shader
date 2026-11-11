Shader "HouseOfGhosts/FlashlightXRayOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0.2, 0.9, 1.0, 1.0)
        _OutlineWidth ("Outline Width (Meters)", Range(0.0, 0.015)) = 0.0035
        _RimPower ("Fresnel Rim Sharpness", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Fresnel Rim Intensity", Range(0.0, 3.0)) = 1.2
        _Alpha ("Master Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+150"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float _RimPower;
            float _RimIntensity;
            float _Alpha;
        CBUFFER_END
        ENDHLSL

        // =====================================================================
        // PASS 1: Sharp Outer Silhouette Outline (Visible Through Walls)
        // =====================================================================
        Pass
        {
            Name "XRaySilhouetteOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertOutline(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                // Extrude along world normal for consistent outline thickness regardless of mesh scale
                posWS += normWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 col = _OutlineColor;
                col.a = saturate(_OutlineColor.a * _Alpha);
                return col;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 2: Inner Fresnel Edge Rim (Sleek X-Ray Contour Glow Through Walls)
        // =====================================================================
        Pass
        {
            Name "XRayFresnelRim"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZTest Always
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vertRim
            #pragma fragment fragRim
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertRim(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(posWS);
                return output;
            }

            half4 fragRim(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = SafeNormalize(input.viewDirWS);

                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower) * _RimIntensity;

                half4 col = _OutlineColor;
                col.rgb *= fresnel;
                col.a = saturate(fresnel * _Alpha);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
