Shader "HouseOfGhosts/UVGhostTrace"
{
    Properties
    {
        _MainTex ("Optional Trace Texture (Alpha)", 2D) = "white" {}
        [NoScaleOffset] _SilhouetteTex ("Optional 3D Ghost Silhouette (Alpha)", 2D) = "white" {}
        [Toggle] _UseSilhouetteTex ("Use Custom Ghost Silhouette Texture", Float) = 0.0
        [HDR] _UVGlowColor ("UV Fluorescent Color", Color) = (0.15, 1.0, 0.78, 1.0)
        _RevealAmount ("UV Reveal Amount (0 = Hidden, 1 = Revealed)", Range(0.0, 1.0)) = 0.0
        _TraceShape ("Trace Shape (0=Footprint, 1=WallSplash, 2=ResidualRing, 3=GhostOutline)", Float) = 0.0
        _TraceSeed ("Per-Instance Variation Seed", Float) = 0.0
        _QuadAspect ("Quad Height-to-Width Aspect Ratio", Float) = 2.0
        _OutlineThickness ("Ghost Outline Thickness", Range(0.015, 0.12)) = 0.042
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
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_SilhouetteTex);
            SAMPLER(sampler_SilhouetteTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _UVGlowColor;
                float _RevealAmount;
                float _TraceShape;
                float _TraceSeed;
                float _QuadAspect;
                float _OutlineThickness;
                float _UseSilhouetteTex;
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

            // Helper: evaluates one polar ring of flung ectoplasm droplets without loops
            void EvaluateDropletRing(
                float2 p,
                float angle,
                float numSectors,
                float seedOffset,
                float minDist,
                float maxDist,
                float minSize,
                float maxSize,
                out float outMask,
                out float outRim)
            {
                float sectorSize = 6.2831853 / numSectors;
                float shifted = angle + 3.14159265 + seedOffset;
                float sectorIdx = floor(shifted / sectorSize);
                float centerAngle = (sectorIdx + 0.5) * sectorSize - 3.14159265 - seedOffset;

                float h1 = frac(sin(sectorIdx * 127.1 + seedOffset * 311.7) * 43758.5453);
                float h2 = frac(sin(sectorIdx * 269.5 + seedOffset * 183.3) * 24634.6345);

                float dropAngle = centerAngle + (h1 - 0.5) * (sectorSize * 0.55);
                float dropDist = lerp(minDist, maxDist, h2);
                float dropRadius = lerp(minSize, maxSize, h1 * h2);

                float2 radialDir = float2(cos(dropAngle), sin(dropAngle));
                float2 tangentDir = float2(-radialDir.y, radialDir.x);
                float2 toDrop = p - (radialDir * dropDist);

                // Elongate slightly outward along impact direction so droplets look flung onto the wall
                float2 localDrop = float2(dot(toDrop, radialDir) * 0.68, dot(toDrop, tangentDir));
                float d = length(localDrop);

                outMask = smoothstep(dropRadius, dropRadius * 0.32, d);
                outRim = smoothstep(dropRadius * 1.08, dropRadius * 0.65, d) * smoothstep(dropRadius * 0.25, dropRadius * 0.65, d);
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
                    // Shape 1: Early-Round Organic UV Ectoplasm Wall Splash (central splat + wet rim + flung droplets)
                    float2 p = centered;
                    float r = length(p);
                    float angle = atan2(p.y, p.x);
                    float s1 = _TraceSeed * 6.2831853;

                    // Multi-lobe irregular ectoplasm impact core + outward splash fingers
                    float lobes =
                        0.12 * sin(angle * 3.0 + s1) +
                        0.085 * cos(angle * 5.0 - s1 * 1.37) +
                        0.060 * sin(angle * 8.0 + s1 * 2.11) +
                        0.042 * cos(angle * 13.0 - s1 * 0.73);

                    float fingers =
                        pow(saturate(0.5 + 0.5 * sin(angle * 6.0 + s1 * 1.9)), 5.0) * 0.17 +
                        pow(saturate(0.5 + 0.5 * cos(angle * 9.0 - s1 * 2.7)), 7.0) * 0.13;

                    float splatRadius = 0.39 + lobes + fingers;

                    // Mottled liquid interior + bright fluorescent pooled wet rim
                    float mottle = 0.68 + 0.32 * sin(p.x * 10.5 + s1) * cos(p.y * 10.5 - s1);
                    float coreFill = smoothstep(splatRadius + 0.025, splatRadius - 0.085, r) * mottle;
                    float wetRim = smoothstep(splatRadius + 0.032, splatRadius - 0.012, r)
                                 * smoothstep(splatRadius - 0.125, splatRadius - 0.018, r);

                    // Two offset polar rings of satellite splatter droplets
                    float dropMaskA, dropRimA;
                    EvaluateDropletRing(p, angle, 8.0, s1 * 0.35 + 1.1, 0.56, 0.84, 0.034, 0.078, dropMaskA, dropRimA);

                    float dropMaskB, dropRimB;
                    EvaluateDropletRing(p, angle, 6.0, s1 * 0.71 + 3.7, 0.66, 0.90, 0.024, 0.054, dropMaskB, dropRimB);

                    float shimmer = 0.93 + 0.07 * sin(_Time.y * 2.8 + r * 7.0);
                    float quadFade = smoothstep(0.99, 0.90, max(abs(p.x), abs(p.y)));

                    edgeBoost = saturate(wetRim * 1.55 + (dropRimA + dropRimB) * 1.35);
                    shapeMask = saturate(coreFill * 0.88 + dropMaskA + dropMaskB + edgeBoost) * shimmer * quadFade;
                }
                else if (_TraceShape < 2.5)
                {
                    // Shape 2: Hiding Spot / 60s Search Hint Pulsing Ectoplasm Ring
                    float dist = length(centered);
                    float pulse = 0.5 + 0.5 * sin(_Time.y * 4.0 - dist * 6.0);
                    float mainRing = smoothstep(0.86, 0.68, dist) * smoothstep(0.42, 0.64, dist);
                    float centerGlow = smoothstep(0.45, 0.0, dist) * (0.45 + 0.35 * pulse);

                    edgeBoost = mainRing * (0.9 + 0.4 * pulse);
                    shapeMask = saturate(centerGlow + edgeBoost) * smoothstep(0.98, 0.82, dist);
                }
                else
                {
                    // Shape 3: Late-Round Hollow Ghost Silhouette Outline (Capsule SDF now; 3D Ghost Girl Texture ready!)
                    float aspect = max(1.0, _QuadAspect);
                    float lineHalfWidth = max(0.015, _OutlineThickness);

                    if (_UseSilhouetteTex > 0.5)
                    {
                        // Automatic hollow UV edge-outline extractor for custom 3D Ghost Girl silhouette PNG
                        float centerA = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, input.uv).a;
                        float2 stepUV = float2(0.016, 0.016 / aspect) * (lineHalfWidth / 0.042);

                        float aL = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(-stepUV.x, 0.0))).a;
                        float aR = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2( stepUV.x, 0.0))).a;
                        float aD = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(0.0, -stepUV.y))).a;
                        float aU = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(0.0,  stepUV.y))).a;

                        float maxN = max(max(aL, aR), max(aD, aU));
                        float minN = min(min(aL, aR), min(aD, aU));
                        float crispEdge = saturate((maxN - minN) * 1.65);

                        float2 wideUV = stepUV * 2.4;
                        float wMax = max(
                            max(SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(-wideUV.x, 0.0))).a,
                                SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2( wideUV.x, 0.0))).a),
                            max(SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(0.0, -wideUV.y))).a,
                                SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, saturate(input.uv + float2(0.0,  wideUV.y))).a)
                        );
                        float wMin = min(centerA, minN);
                        float auraGlow = saturate((wMax - wMin) * 0.60);

                        float shimmer = 0.90 + 0.10 * sin(input.uv.y * 22.0 - _Time.y * 3.8);
                        float hollowMist = centerA * 0.075;

                        edgeBoost = (crispEdge * 1.75 + auraGlow * 0.70) * shimmer;
                        shapeMask = saturate(crispEdge + auraGlow + hollowMist);
                    }
                    else
                    {
                        // Aspect-correct 2D Capsule SDF (true circular hemispheres + straight torso sides)
                        float2 q = float2(centered.x, centered.y * aspect);
                        float capRadius = 0.80;
                        float segHalfH = max(0.0, capRadius * (aspect - 1.0));
                        float2 pa = float2(q.x, q.y - clamp(q.y, -segHalfH, segHalfH));
                        float sdf = length(pa) - capRadius;

                        // Subtle haunted spectral ripple along the capsule silhouette
                        float contourWave = sin(q.y * 8.5 - _Time.y * 3.6) * 0.009
                                          + cos(q.x * 10.0 + _Time.y * 2.4) * 0.006;
                        float distFromContour = abs(sdf + contourWave);

                        float crispContour = smoothstep(lineHalfWidth, lineHalfWidth * 0.20, distFromContour);
                        float outerAura = smoothstep(lineHalfWidth * 3.3, 0.0, distFromContour) * 0.50;

                        // Faint hollow interior mist (~8%) so the wall still shows right through the middle
                        float insideCapsule = smoothstep(0.01, -0.05, sdf);
                        float hollowMist = insideCapsule * (0.065 + 0.025 * sin(q.y * 12.0 + _Time.y * 2.5));

                        float quadFade = smoothstep(0.995, 0.92, max(abs(centered.x), abs(centered.y)));
                        edgeBoost = (crispContour * 1.75 + outerAura * 0.75) * quadFade;
                        shapeMask = saturate(crispContour + outerAura + hollowMist) * quadFade;
                    }
                }

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float finalMask = shapeMask * texSample.a;

                float alpha = saturate(finalMask * 1.15 * _RevealAmount * _UVGlowColor.a);
                // Vivid unlit fluorescent core + bright rim highlight that stands out on BOTH dark horror walls and lit white test walls
                half3 deepCoreTint = _UVGlowColor.rgb * 0.85h;
                half3 brightRimTint = _UVGlowColor.rgb * (half)(1.35 + edgeBoost * 0.85);
                half3 rgb = lerp(deepCoreTint, brightRimTint, (half)saturate(edgeBoost));

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
