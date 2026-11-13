Shader "HouseOfGhosts/SoulDustDispersion"
{
    Properties
    {
        [HDR] _TintColor ("Dust Color", Color) = (0.95, 0.97, 1.0, 1.0)
        _Brightness ("Brightness Boost", Range(0.2, 5.0)) = 1.25
        [Enum(WavyCausticMist, 0, MultiGrainCluster, 1, SinglePinpointSpeck, 2, SoftAshVeil, 3)] _DustMode ("Dust Layer Mode", Float) = 1
        _WaveFrequency ("3D Caustic Wave Scale", Range(0.5, 8.0)) = 3.45
        _RidgeSharpness ("Caustic Ridge Sharpness", Range(1.5, 12.0)) = 4.8
        _GrainDensity ("Grain Grid Scale", Range(6.0, 42.0)) = 26.0
        _GrainSize ("Grain Radius", Range(0.02, 0.30)) = 0.046
        _MistyCore ("Subtle Misty Cohesion", Range(0.0, 0.5)) = 0.0
        _CloudScaleFactor ("Runtime Round Cloud Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+45"
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
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Brightness;
                float _DustMode;
                float _WaveFrequency;
                float _RidgeSharpness;
                float _GrainDensity;
                float _GrainSize;
                float _MistyCore;
                float _CloudScaleFactor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color       : COLOR;
                float4 uvAndRand  : TEXCOORD0; // xy = UV, zw = StableRandom.xy
                float  agePercent : TEXCOORD1; // x = AgePercent (0..1)
                uint vertexID     : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float4 uvAndRand  : TEXCOORD0;
                float4 wsAndAge   : TEXCOORD1; // xyz = positionWS, w = AgePercent
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float InterleavedGradientNoise(float2 pixelPos)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixelPos, magic.xy)));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n + 19.19));
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
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

                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            // Continuous 3D world-space organic undulating smoke-silk wave field (zero geometric criss-cross V-lines!)
            void EvalWorldCausticField(float3 posWS, out float sharpRidge, out float softFold)
            {
                float t = _Time.y * 0.35;
                float safeScale = max(0.25, _CloudScaleFactor);
                float effectiveWaveFreq = _WaveFrequency / pow(safeScale, 0.58);
                float3 p = float3(posWS.x * 0.72, posWS.y * 1.15, posWS.z * 0.72) * effectiveWaveFreq;

                // Smooth 2-octave 3D organic turbulence warps parallel horizontal flow into natural wind-rippled silk folds
                float n1 = ValueNoise3D(p * 0.95 + float3(-t * 0.45, t * 0.18, -t * 0.45));
                float n2 = ValueNoise3D(p * 2.05 + float3(4.3 - t * 0.7, 1.9, 7.1 - t * 0.7));
                float organicTurbulence = (n1 - 0.5) * 1.65 + (n2 - 0.5) * 0.72;

                // Gentle long-wavelength horizontal undulation + organic 3D noise (no regular grid or sharp V-hooks!)
                float gentleWave = sin(p.x * 1.05 + p.z * 1.05 - t * 0.55) * 0.38;
                float phase = p.y * 2.05 + gentleWave + organicTurbulence;

                float crestBase = 1.0 - abs(sin(phase));
                sharpRidge = pow(saturate(crestBase), _RidgeSharpness) * lerp(0.55, 1.25, n1);
                softFold = pow(saturate(crestBase), 1.55) * 0.46 * lerp(0.65, 1.20, n2);
            }

            // Photographic sand/ash micro-grains: delicate silver-ash specks + crisp bright white glints!
            float EvalGrainGrid(float2 uv, float gridScale, float2 randomOffset, float baseRadius, float uvPixelSize, float age01, float densityBias)
            {
                float2 scaledUV = uv * gridScale + randomOffset;
                float2 cellId = floor(scaledUV);
                float2 cellLocal = frac(scaledUV);

                float2 rnd = Hash22(cellId);
                float grainSeed = Hash21(cellId + 11.7);

                float survivalThreshold = saturate(densityBias * (1.02 - age01 * 0.84));
                if (grainSeed >= survivalThreshold)
                {
                    return 0.0;
                }

                float2 grainCenter = 0.20 + 0.60 * rnd;
                float distInCell = length(cellLocal - grainCenter);

                float minCellRadius = uvPixelSize * gridScale * 0.46;
                float radius = max(baseRadius * lerp(0.48, 1.15, rnd.x), minCellRadius);
                float feather = max(radius * 0.48, minCellRadius * 0.65);

                float core = smoothstep(radius + feather, radius * 0.05, distInCell);

                float glintRoll = frac(grainSeed * 31.7);
                float brightnessVar = lerp(0.20, 0.65, glintRoll) + pow(glintRoll, 5.0) * 0.80;
                float smoothBirthDeath = saturate((survivalThreshold - grainSeed) * 7.5);
                return core * brightnessVar * smoothBirthDeath;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.color = input.color;

                float fallbackSeed = (float)(input.vertexID / 4u) * 0.173;
                float2 randXY = input.uvAndRand.zw;
                if (abs(randXY.x) + abs(randXY.y) < 0.0001)
                {
                    randXY = float2(frac(fallbackSeed), frac(fallbackSeed * 3.71));
                }

                output.uvAndRand = float4(input.uvAndRand.xy, randXY);
                output.wsAndAge = float4(posWS, input.agePercent);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uvAndRand.xy;
                float2 randXY = input.uvAndRand.zw;
                float3 posWS = input.wsAndAge.xyz;
                float age01 = saturate(input.wsAndAge.w);

                float2 centeredUV = uv * 2.0 - 1.0;
                float r2 = dot(centeredUV, centeredUV);
                if (r2 >= 1.0)
                {
                    return half4(0, 0, 0, 0);
                }

                // Ultra-soft quartic + Gaussian falloff -> reaches 0.000 smoothly well inside quad border
                float oneMinusR2 = 1.0 - r2;
                float wideWindow = oneMinusR2 * oneMinusR2;
                float gaussianWindow = exp(-r2 * 5.2) * wideWindow;

                float uvPixelSize = max(length(fwidth(uv)), 0.0005);
                float2 seed = randXY * 47.3 + float2(Hash21(randXY + 7.13) * 19.7, randXY.y * 29.1);
                float safeScale = max(0.25, _CloudScaleFactor);
                float scaledGrainDensity = clamp(_GrainDensity * pow(safeScale, 0.45), 14.0, 40.0);

                float finalMask = 0.0;
                float lumBoost = 1.0;

                if (_DustMode < 0.5)
                {
                    // MODE 0: Gossamer 3D Wavy Caustic Smoke-Silk + Embedded Fine Crest Dust
                    float sharpRidge;
                    float softFold;
                    EvalWorldCausticField(posWS, sharpRidge, softFold);

                    float gossamerMist = (sharpRidge * 0.95 + softFold * 0.48) * gaussianWindow;
                    float crestSpecks = EvalGrainGrid(uv, scaledGrainDensity, seed, _GrainSize, uvPixelSize, age01, wideWindow * saturate(sharpRidge * 1.2 + 0.08));

                    finalMask = gossamerMist * (1.0 - age01 * 0.78) + crestSpecks * 0.90;
                    lumBoost = 0.88 + sharpRidge * 0.45 + crestSpecks * 0.48;
                }
                else if (_DustMode < 1.5)
                {
                    // MODE 1: Fine Photographic Multi-Grain Dust Carpet
                    float sharpRidge;
                    float softFold;
                    EvalWorldCausticField(posWS, sharpRidge, softFold);

                    float waveClustering = lerp(0.14, 1.0, saturate(sharpRidge * 1.25 + softFold * 0.75));
                    float localDensity = wideWindow * waveClustering;

                    float grainsA = EvalGrainGrid(uv, scaledGrainDensity, seed, _GrainSize, uvPixelSize, age01, localDensity);
                    float grainsB = EvalGrainGrid(uv + float2(0.41, 0.63), scaledGrainDensity * 1.43, seed * 1.63 + 8.7, _GrainSize * 0.84, uvPixelSize, age01, localDensity * 0.85);

                    float grains = saturate(grainsA + grainsB);
                    float subtleMist = gaussianWindow * sharpRidge * _MistyCore * (1.0 - age01);
                    finalMask = grains + subtleMist;
                    lumBoost = 0.95 + grains * 0.40;
                }
                else if (_DustMode < 2.5)
                {
                    // MODE 2: Individual Pinpoint Downwind Dust Specks
                    float dist = sqrt(r2);
                    float aa = max(uvPixelSize * 2.2, 0.08);
                    float coreRadius = max(0.20, uvPixelSize * 1.5);
                    float core = smoothstep(coreRadius + aa, coreRadius * 0.08, dist);
                    float brightnessVar = lerp(0.32, 1.15, randXY.x);
                    finalMask = core * brightnessVar;
                    lumBoost = 1.0 + core * 0.25;
                }
                else
                {
                    // MODE 3: Subtle Charcoal-Ash Contrast Veil
                    finalMask = gaussianWindow * (1.0 - age01 * 0.60);
                    lumBoost = 1.0;
                }

                half rawAlpha = (half)saturate(input.color.a * _TintColor.a * saturate(finalMask));
                // Screen-space dither eliminates 8-bit framebuffer alpha banding contours completely!
                float dither = (InterleavedGradientNoise(input.positionCS.xy) - 0.5) * (1.6 / 255.0);
                half alpha = (half)saturate((float)rawAlpha + (rawAlpha > 0.0004h ? dither : 0.0));
                if (alpha <= 0.0005h)
                {
                    return half4(0, 0, 0, 0);
                }

                half3 rgb = input.color.rgb * _TintColor.rgb * (half)(_Brightness * lumBoost);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
