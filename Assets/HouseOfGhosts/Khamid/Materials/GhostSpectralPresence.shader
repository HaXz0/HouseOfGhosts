Shader "HouseOfGhosts/GhostSpectralPresence"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.25

        [Header(Spectral Presence and Dissolve)]
        _Visibility ("Visibility (0 = Cloaked, 1 = Solid)", Range(0.0, 1.0)) = 1.0
        _WipeMode ("Wipe Mode (0 = Phantom, 1 = BottomUp, 2 = CaptureAscend)", Float) = 0.0
        _LocalBoundsMinY ("Local Min Y", Float) = -1.0
        _LocalBoundsMaxY ("Local Max Y", Float) = 1.0

        [HDR] _DissolveEdgeColor ("Materialize Frontier Glow", Color) = (0.25, 0.92, 1.0, 1.0)
        _DissolveEdgeWidth ("Frontier Glow Width", Range(0.02, 0.35)) = 0.14

        [Header(Spectral Rim and Emission)]
        [HDR] _RimColor ("Spectral Rim Color", Color) = (0.3, 0.85, 1.0, 1.0)
        _RimPower ("Fresnel Rim Sharpness", Range(0.8, 6.0)) = 2.6
        _RimIntensity ("Fresnel Rim Intensity", Range(0.0, 3.0)) = 0.0
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)

        [Header(Stage 2D Beam Lock Fracture and Flaking)]
        _CaptureStrain ("Capture Destabilization Strain (0-1)", Range(0.0, 1.0)) = 0.0
        [HDR] _StrainCrackColor ("Strain Fracture Glow", Color) = (0.96, 0.99, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+25"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP Lighting & Spotlight Support (Unity 6.3 LTS)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                float _Visibility;
                float _WipeMode;
                float _LocalBoundsMinY;
                float _LocalBoundsMaxY;
                half4 _DissolveEdgeColor;
                float _DissolveEdgeWidth;
                half4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                half4 _EmissionColor;
                float _CaptureStrain;
                half4 _StrainCrackColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half3 vertexLight : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float InterleavedGradientNoise(float2 pixelPos)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixelPos, magic.xy)));
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float OrganicNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLight = VertexLighting(posInputs.positionWS, normInputs.normalWS);
                #else
                output.vertexLight = half3(0, 0, 0);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                if (_Visibility <= 0.002)
                {
                    discard;
                }

                half4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseAlbedo = texSample.rgb * _BaseColor.rgb;

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half alphaCoverage = 1.0h;
                half3 frontierEmission = half3(0, 0, 0);

                if (_Visibility < 0.995)
                {
                    float screenDither = InterleavedGradientNoise(input.positionCS.xy);
                    float bodyNoise = OrganicNoise3D(input.positionOS * 7.5 + float3(0, _Time.y * 1.6, 0));

                    if (_WipeMode > 0.5)
                    {
                        float heightSpan = max(0.05, _LocalBoundsMaxY - _LocalBoundsMinY);
                        float height01 = saturate((input.positionOS.y - _LocalBoundsMinY) / heightSpan);

                        // WipeMode 2 = Capture Ascend (dissolves from feet upward into light)
                        if (_WipeMode > 1.5)
                        {
                            height01 = 1.0 - height01;
                        }

                        float frontier = lerp(-0.22, 1.25, _Visibility);
                        float noisyHeight = height01 + (bodyNoise - 0.5) * 0.24;
                        float signedDist = frontier - noisyHeight;

                        if (signedDist < 0.0)
                        {
                            discard;
                        }

                        float edgeBand = 1.0 - saturate(signedDist / max(0.02, _DissolveEdgeWidth));
                        frontierEmission = _DissolveEdgeColor.rgb * (edgeBand * edgeBand * 2.4);

                        float tipFade = saturate(signedDist * 12.0 + (screenDither - 0.5) * 0.35);
                        alphaCoverage = (half)saturate(lerp(0.25, 1.0, tipFade));
                    }
                    else
                    {
                        // Full-body translucent phantom presence (Stalking 62%, UV Shimmer 38%, Hint Flicker 45%)
                        float phantomMask = _Visibility + (bodyNoise - 0.5) * 0.18 + (screenDither - 0.5) * 0.45;
                        if (phantomMask < 0.22)
                        {
                            discard;
                        }
                        alphaCoverage = (half)saturate(phantomMask);
                    }
                }

                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower) * _RimIntensity;
                half3 rimEmission = _RimColor.rgb * (half)fresnel;

                // Stage 2D: 0s -> 3s Beam-Lock 3D Soul-Fracture Veins & Grazing Rim Flaking
                half3 strainEmission = half3(0, 0, 0);
                if (_CaptureStrain > 0.015 && _WipeMode < 1.5)
                {
                    float strain = saturate(_CaptureStrain);
                    float tFlow = _Time.y;

                    // Multi-octave tilted 3D fractal zero-crossing (true jagged diagonal lightning/porcelain fissures, zero cubic grid loops!)
                    float3 q0 = input.positionOS * float3(4.6, 3.4, 4.6) + float3(0.0, -tFlow * 0.28, 0.0);
                    float3 p1 = float3(
                        q0.x * 0.80 + q0.y * 0.36 - q0.z * 0.48,
                        -q0.x * 0.44 + q0.y * 0.88 + q0.z * 0.18,
                        q0.x * 0.41 - q0.y * 0.31 + q0.z * 0.86
                    );
                    float3 p2 = float3(
                        p1.x * 0.64 - p1.z * 0.77,
                        p1.y * 1.08 + p1.x * 0.42 - tFlow * 0.35,
                        p1.x * 0.77 + p1.z * 0.64
                    ) * 2.15 + float3(5.3, 2.7, 8.1);
                    float3 p3 = float3(
                        p2.z * 0.72 + p2.y * 0.69,
                        p2.y * 0.72 - p2.z * 0.69 - tFlow * 0.45,
                        p2.x * 1.04
                    ) * 2.10 + float3(11.4, 6.9, 3.2);

                    float n1 = OrganicNoise3D(p1) - 0.5;
                    float n2 = OrganicNoise3D(p2) - 0.5;
                    float n3 = OrganicNoise3D(p3) - 0.5;

                    // Primary jagged diagonal fissure + secondary cross-branching fracture forks
                    float fbmPrimary = n1 + n2 * 0.56 + n3 * 0.30;
                    float fbmBranch  = (n2 * 0.85 - n1 * 0.42) + n3 * 0.48;

                    float ridgeA = saturate(1.0 - abs(fbmPrimary) * 3.6);
                    float ridgeB = saturate(1.0 - abs(fbmBranch) * 4.2);

                    float revealMask = smoothstep(0.04, 0.22, strain);

                    // Razor-sharp glowing silver-white veins framed by a subtle dark spectral fissure recess
                    float seamBand = pow(ridgeA, lerp(5.0, 2.6, strain)) * revealMask;
                    float primaryCore = pow(ridgeA, lerp(13.0, 6.5, strain));
                    float branchCore = pow(ridgeB, lerp(14.0, 7.2, strain)) * saturate(ridgeA * 1.6 + strain * 0.45);
                    float glowingCore = saturate(primaryCore * 1.45 + branchCore * 1.05) * revealMask;

                    float borderDarken = saturate(seamBand - glowingCore * 1.35) * (0.26 + 0.42 * strain);
                    baseAlbedo *= (half)(1.0 - borderDarken);

                    float pulse = 0.88 + 0.12 * sin(tFlow * (6.0 + strain * 10.0) + n1 * 9.0);
                    float veinIntensity = glowingCore * (0.80 + strain * 2.05) * pulse;

                    // Above 52% strain: fine-grain silhouette rim physically crumbles open via AlphaToMask!
                    float rimFlakeGlow = 0.0;
                    if (strain > 0.52)
                    {
                        float flakePhase = saturate((strain - 0.52) / 0.48);
                        float grazingMask = pow(1.0 - ndotv, 1.75);
                        float flakeThreshold = flakePhase * grazingMask * 0.84;

                        float fineGrain = OrganicNoise3D(input.positionOS * 24.0 + float3(0.0, -tFlow * 1.4, 0.0));
                        float flakeField = saturate(fineGrain * 0.62 + (n3 + 0.5) * 0.26 + (1.0 - ridgeA) * 0.12);

                        if (flakeField < flakeThreshold)
                        {
                            discard;
                        }

                        float tearEdge = 1.0 - saturate((flakeField - flakeThreshold) / 0.12);
                        rimFlakeGlow = tearEdge * tearEdge * flakePhase * grazingMask * 2.6;
                    }

                    strainEmission = _StrainCrackColor.rgb * (half)(veinIntensity + rimFlakeGlow);
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = (half3)viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.vertexLighting = input.vertexLight;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseAlbedo;
                surfaceData.specular = half3(0.15, 0.15, 0.15);
                surfaceData.metallic = 0.0h;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = _EmissionColor.rgb + frontierEmission + rimEmission + strainEmission;
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = alphaCoverage;

                half4 litColor = UniversalFragmentBlinnPhong(inputData, surfaceData);
                litColor.a = alphaCoverage;
                return litColor;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
    FallBack Off
}
