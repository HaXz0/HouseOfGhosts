Shader "HouseOfGhosts/VolumetricFlashlightBeam"
{
    Properties
    {
        [HDR] _BeamColor ("Volumetric Beam Color (RGB + Alpha)", Color) = (1.0, 0.95, 0.84, 0.085)
        _StartRadius ("Lens Start Radius (m)", Float) = 0.024
        _EndRadius ("Cone End Radius at MeshLength (m)", Float) = 2.52
        _MeshLength ("Active Cone Mesh Length (m)", Float) = 8.0
        _MaxBeamRange ("Full Flashlight Range (m)", Float) = 8.0
        _WallStopDist ("Obstacle Stop Distance (m)", Float) = 8.0
        _FresnelPower ("Edge Feather Fresnel Power", Range(1.0, 5.0)) = 2.35
        _MistStrength ("3D Haunted Air Mist Strength", Range(0.0, 0.8)) = 0.30
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+35"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricBeamForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            // Soft additive blending so volumetric light shafts brighten dark air without muddying surfaces
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                float _StartRadius;
                float _EndRadius;
                float _MeshLength;
                float _MaxBeamRange;
                float _WallStopDist;
                float _FresnelPower;
                float _MistStrength;
            CBUFFER_END

            struct Attributes
            {
                // positionOS.xy = unit circle (cos theta, sin theta), uv.y = 0 at lens .. 1 at tip
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 posWS       : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 beamDirWS   : TEXCOORD2;
                float  axialDist   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * float3(127.1, 311.7, 74.7));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float t = saturate(input.uv.y);
                float safeLen = max(0.05, _MeshLength);
                float radius = lerp(_StartRadius, _EndRadius, t);
                float zDist = t * safeLen;

                float3 localPos = float3(input.positionOS.xy * radius, zDist);
                float tanSlope = (_EndRadius - _StartRadius) / safeLen;
                float3 localNormal = normalize(float3(input.positionOS.xy, -tanSlope));

                output.posWS = TransformObjectToWorld(localPos);
                output.normalWS = TransformObjectToWorldNormal(localNormal);
                output.beamDirWS = normalize(TransformObjectToWorldDir(float3(0.0, 0.0, 1.0)));
                output.axialDist = zDist;
                output.positionCS = TransformWorldToHClip(output.posWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 viewVec = GetWorldSpaceViewDir(input.posWS);
                float eyeDist = length(viewVec);
                float3 viewDirWS = viewVec / max(0.0001, eyeDist);
                float3 normalWS = normalize(input.normalWS);
                float3 beamDirWS = normalize(input.beamDirWS);

                // 1. Grazing Rim Fresnel: silky-soft cone sides with zero hard polygon edges
                float ndotv = saturate(abs(dot(normalWS, viewDirWS)));
                float edgeSoftness = pow(ndotv, _FresnelPower);

                // 2. First-Person VR Aiming Clarity: keep center aiming axis clear so the ghost & UV traces stay 100% sharp!
                float onAxis = abs(dot(beamDirWS, viewDirWS));
                float vrAimClarity = lerp(1.0, 0.34, smoothstep(0.76, 0.99, onAxis));
                float nearEyeFade = smoothstep(0.08, 0.34, eyeDist);

                // 3. Longitudinal Falloff + Concentrated Lens Glow Core + Smooth Wall-Stop Feathering
                float maxRange = max(0.5, _MaxBeamRange);
                float normDist = saturate(input.axialDist / maxRange);
                float lensFadeIn = smoothstep(0.0, 0.09, input.axialDist);
                float lensHotCore = exp(-input.axialDist * 2.8) * 0.75;
                float rangeFalloff = pow(1.0 - normDist, 1.65);
                float wallFeather = smoothstep(_WallStopDist + 0.04, max(0.02, _WallStopDist - 0.28), input.axialDist);

                // 4. 3D World-Space Haunted Room Air Mist (beam reveals stationary drifting air pockets as you sweep!)
                float3 mistCoordA = input.posWS * 1.35 + float3(_Time.y * 0.045, -_Time.y * 0.065, _Time.y * 0.035);
                float3 mistCoordB = input.posWS * 2.85 + float3(-_Time.y * 0.07, -_Time.y * 0.04, _Time.y * 0.06);
                float mistNoise = ValueNoise3D(mistCoordA) * 0.65 + ValueNoise3D(mistCoordB) * 0.35;
                float airMist = lerp(1.0, 0.62 + 0.76 * mistNoise, _MistStrength);

                float combinedDensity = edgeSoftness * vrAimClarity * nearEyeFade * lensFadeIn * (rangeFalloff + lensHotCore) * wallFeather * airMist;

                // 5. Jimenez Interleaved Gradient Dithering: eliminates 8-bit Mach banding on Quest 3 & dark monitors!
                float ign = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                float rawAlpha = combinedDensity * _BeamColor.a;
                // Scale dither to zero where alpha is zero so black background stays pure 0
                float dither = ign * (1.8 / 255.0) * saturate(rawAlpha * 45.0);

                half finalAlpha = (half)saturate(rawAlpha + dither);
                half3 finalRgb = _BeamColor.rgb * (half)(1.08 + lensHotCore * 0.50);

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
