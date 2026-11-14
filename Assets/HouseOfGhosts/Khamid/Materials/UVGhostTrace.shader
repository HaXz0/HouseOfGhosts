Shader "HouseOfGhosts/UVGhostTrace"
{
    Properties
    {
        _MainTex ("Optional Trace Texture (Alpha)", 2D) = "white" {}
        [NoScaleOffset] _SilhouetteTex ("Optional 3D Ghost Silhouette (Alpha)", 2D) = "white" {}
        [Toggle] _UseSilhouetteTex ("Use Custom Ghost Silhouette Texture", Float) = 0.0
        [HDR] _UVGlowColor ("UV Fluorescent Color", Color) = (0.15, 1.0, 0.78, 1.0)
        _RevealAmount ("UV Reveal Amount (0 = Hidden, 1 = Revealed)", Range(0.0, 1.0)) = 0.0
        _TraceShape ("Trace Shape (0=Footprint, 1=WallSplash, 2=ResidualRing, 3=GhostOutline, 4=Handprint)", Float) = 0.0
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

            float SmoothMinSDF(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / max(0.0001, k));
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float CapsuleSDF2D(float2 p, float2 a, float2 b, float rA, float rB)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(0.0001, dot(ba, ba)));
                return length(pa - ba * h) - lerp(rA, rB, h);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 2.0 - 1.0;
            }

            float HandOrganicNoise(float2 p, float phase)
            {
                float2 q = p * 4.2 + float2(phase * 1.7, -phase * 1.3);
                float n1 = ValueNoise2D(q);
                float2 q2 = float2(q.x * 1.62 - q.y * 1.18, q.x * 1.18 + q.y * 1.62);
                float n2 = ValueNoise2D(q2 + 7.3);
                float2 q3 = float2(q2.x * 1.75 + q2.y * 1.15, -q2.x * 1.15 + q2.y * 1.75);
                float n3 = ValueNoise2D(q3 - 3.9);
                return n1 * 0.54 + n2 * 0.30 + n3 * 0.16;
            }

            // Evaluates one organic child finger skin-contact print (continuous pad + subtle joint folds + solid whorl fingertip + downward smear)
            void EvaluateChildFingerContact(
                float2 h,
                float2 basePos,
                float2 tipPos,
                float baseRadius,
                float bendAmount,
                float isThumb,
                float edgeWarp,
                out float outFill,
                out float outPressureCore,
                out float outSlipSmudge)
            {
                outFill = 0.0;
                outPressureCore = 0.0;
                outSlipSmudge = 0.0;

                float2 seg = tipPos - basePos;
                float segLen = max(0.001, length(seg));
                float2 dir = seg / segLen;
                float2 perp = float2(-dir.y, dir.x);

                float2 rel = h - basePos;
                float along = dot(rel, dir);
                float t = along / segLen;
                float tClamped = saturate(t);
                float curveOffset = bendAmount * tClamped * tClamped;
                float side = dot(rel, perp) - curveOffset;

                // 1. Continuous tapered finger shaft fused seamlessly into oval distal pad (zero hollow rings!)
                float tc = clamp(t, 0.02, 0.94);
                float shaftRadius = baseRadius * lerp(0.96, 0.80, tClamped);

                // Subtle joint-crease narrowing (natural skin folds, not mechanical rings)
                float j1 = (t - (isThumb > 0.5 ? 0.48 : 0.36)) / 0.055;
                float j2 = (t - 0.68) / 0.050;
                float crease1 = exp(-(j1 * j1));
                float crease2 = (isThumb > 0.5) ? 0.0 : exp(-(j2 * j2));
                shaftRadius *= (1.0 - 0.08 * crease1 - 0.10 * crease2);

                float dShaft = length(float2(side, along - tc * segLen)) - shaftRadius;

                // Plump solid oval fingertip pad fused smoothly at the distal phalanx
                float2 tipCenter = basePos + dir * (segLen * 0.96) + perp * bendAmount;
                float2 toTip = h - tipCenter;
                float tipX = dot(toTip, perp);
                float tipY = dot(toTip, dir);
                float rTipX = baseRadius * 0.96;
                float rTipY = baseRadius * 1.22;
                float dTip = (length(float2(tipX / rTipX, tipY / rTipY)) - 1.0) * min(rTipX, rTipY);

                float dFinger = SmoothMinSDF(dShaft, dTip, 0.035) + edgeWarp * 0.011;

                // Soft chalky blacklight skin contact (brightest in fleshy pad interior, organically feathered edge)
                float contactMask = smoothstep(0.010, -0.042, dFinger);

                // Natural knuckle webbing fade + thin irregular joint-crease dips
                float creaseMod = 1.0 - (0.36 * crease1 + 0.42 * crease2) * saturate(0.72 + edgeWarp * 0.45);
                float baseWebFade = smoothstep(-0.05, 0.12, t);
                contactMask *= saturate(creaseMod) * baseWebFade;

                // Concentric papillary fingerprint whorl ridges + firmer pressure core on fingertip & proximal pad
                float whorlDist = length(float2(tipX * 1.25, (tipY + baseRadius * 0.12) * 0.92));
                float whorlRidges = 0.86 + 0.14 * sin(whorlDist * 175.0 + edgeWarp * 5.5);
                contactMask *= whorlRidges;

                float tipCore = smoothstep(0.95, 0.12, length(float2(tipX / rTipX, tipY / rTipY)));
                float proxCore = smoothstep(-0.010, -0.048, dShaft) * smoothstep(0.05, 0.25, t) * 0.55;

                // Creepy downward ectoplasm finger-slide streak in wall -Y
                float2 slideRel = (h + float2(0.0, 0.095)) - tipCenter;
                float slideX = dot(slideRel, perp) / (baseRadius * 0.85);
                float slideY = dot(slideRel, dir) / (baseRadius * 1.95);
                outSlipSmudge = smoothstep(1.0, 0.15, length(float2(slideX, slideY))) * 0.30;

                outFill = contactMask;
                outPressureCore = max(tipCore * 0.95, proxCore) * contactMask;
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
                else if (_TraceShape < 3.5)
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
                else
                {
                    // Shape 4: Creepy Child-Sized 5-Finger UV Handprint (Latent Blacklight Skin-Residue Style)
                    // (_TraceSeed >= 0 = Right Hand [thumb on viewer-left], _TraceSeed < 0 = Left Hand [thumb on viewer-right])
                    float aspect = clamp(_QuadAspect, 0.85, 1.65);
                    float handSign = (_TraceSeed < 0.0) ? -1.0 : 1.0;
                    float seedVal = abs(_TraceSeed);
                    float sPhase = seedVal * 3.17 + 0.9;
                    float splay = (frac(seedVal * 1.73 + 0.21) - 0.5) * 0.04;

                    // Canonical hand coordinates with comfortable margin from quad borders
                    float2 h = float2(centered.x * handSign + 0.05, centered.y * aspect + 0.05);
                    float edgeWarp = HandOrganicNoise(h * 1.25, sPhase);
                    float fineGrainNoise = HandOrganicNoise(h * 3.6 + float2(1.7, -2.3), sPhase * 1.9);

                    // 1. Unified Anatomical Palm Cushions (Upper Metacarpal Bar + Thenar + Hypothenar + Heel Bridge)
                    float dMetaLeft = CapsuleSDF2D(h, float2(-0.22, -0.02), float2(0.02, 0.02), 0.138, 0.146);
                    float dMetaRight = CapsuleSDF2D(h, float2(0.02, 0.02), float2(0.29, -0.07), 0.146, 0.128);
                    float dThenar = CapsuleSDF2D(h, float2(-0.26, -0.15), float2(-0.11, -0.45), 0.168, 0.142);
                    float dHypothenar = CapsuleSDF2D(h, float2(0.27, -0.16), float2(0.13, -0.47), 0.140, 0.126);
                    float dHeelBridge = CapsuleSDF2D(h, float2(-0.10, -0.44), float2(0.12, -0.46), 0.126, 0.120);

                    float dPalm = SmoothMinSDF(dMetaLeft, dMetaRight, 0.11);
                    dPalm = SmoothMinSDF(dPalm, dThenar, 0.13);
                    dPalm = SmoothMinSDF(dPalm, dHypothenar, 0.13);
                    dPalm = SmoothMinSDF(dPalm, dHeelBridge, 0.12) + edgeWarp * 0.017;

                    // Soft anatomical radial palm arch depression (angled hollow opening gently toward inner thumb webbing, NOT a round hole!)
                    float2 archOffset = h - float2(-0.03, -0.21);
                    float2 archRot = float2(archOffset.x * 0.88 - archOffset.y * 0.47, archOffset.x * 0.47 + archOffset.y * 0.88);
                    float palmHollow = smoothstep(1.18, 0.12, length(archRot / float2(0.20, 0.135)) + edgeWarp * 0.24);

                    // Subtle organic palmar skin creases (transverse head line + soft curved thumb crease)
                    float cLine1 = abs((h.y + 0.11) + (h.x + 0.06) * 0.26 + edgeWarp * 0.022);
                    float palmCrease1 = smoothstep(0.017, 0.002, cLine1) * smoothstep(0.32, 0.05, abs(h.x - 0.02));
                    float lifeDist = abs(length(h - float2(-0.38, -0.32)) - 0.31 + edgeWarp * 0.018);
                    float palmCrease2 = smoothstep(0.015, 0.002, lifeDist) * smoothstep(-0.46, -0.14, h.y) * smoothstep(0.12, -0.05, h.x);

                    float palmFill = smoothstep(0.012, -0.082, dPalm);
                    palmFill *= (1.0 - palmHollow * 0.48) * (1.0 - saturate(palmCrease1 + palmCrease2) * 0.44);
                    float palmCore = smoothstep(-0.020, -0.115, dPalm) * (1.0 - palmHollow * 0.78);

                    // Faint downward palm-heel slip smudge
                    float dHeelSmudge = CapsuleSDF2D(h + float2(0.0, 0.10), float2(-0.14, -0.43), float2(0.14, -0.45), 0.13, 0.12) + edgeWarp * 0.02;
                    float heelSmudge = smoothstep(0.015, -0.065, dHeelSmudge) * 0.26;

                    // 2. Five Splayed Child Fingers (Solid Whorl Pads + Subtle Joint Folds + Ectoplasm Drag)
                    float fThumb, cThumb, sThumb;
                    EvaluateChildFingerContact(
                        h, float2(-0.32, -0.20), float2(-0.60 - splay, 0.10 + splay * 0.5),
                        0.094, 0.016, 1.0, edgeWarp, fThumb, cThumb, sThumb);

                    float fIndex, cIndex, sIndex;
                    EvaluateChildFingerContact(
                        h, float2(-0.22, 0.11), float2(-0.34 - splay * 0.7, 0.68),
                        0.082, 0.012, 0.0, edgeWarp, fIndex, cIndex, sIndex);

                    float fMiddle, cMiddle, sMiddle;
                    EvaluateChildFingerContact(
                        h, float2(-0.01, 0.14), float2(-0.01, 0.80),
                        0.085, 0.005, 0.0, edgeWarp, fMiddle, cMiddle, sMiddle);

                    float fRing, cRing, sRing;
                    EvaluateChildFingerContact(
                        h, float2(0.17, 0.11), float2(0.27 + splay * 0.6, 0.70),
                        0.080, -0.010, 0.0, edgeWarp, fRing, cRing, sRing);

                    float fPinky, cPinky, sPinky;
                    EvaluateChildFingerContact(
                        h, float2(0.32, 0.01), float2(0.51 + splay, 0.45),
                        0.070, -0.015, 0.0, edgeWarp, fPinky, cPinky, sPinky);

                    float fingersFill = max(fThumb, max(max(fIndex, fMiddle), max(fRing, fPinky)));
                    float fingersCore = max(cThumb, max(max(cIndex, cMiddle), max(cRing, cPinky)));
                    float fingersSlip = max(sThumb, max(max(sIndex, sMiddle), max(sRing, sPinky)));

                    // 3. Chalky Fluorescent Blacklight Skin-Oil Grain + Vertical Slip Streaks
                    float papillaryRidges = 0.91 + 0.09 * sin(h.y * 135.0 + edgeWarp * 3.8);
                    float dustyMottle = saturate(0.82 + 0.24 * fineGrainNoise + 0.12 * edgeWarp);
                    float residueTexture = papillaryRidges * dustyMottle;

                    float streakLines = saturate(0.50 + 0.50 * ValueNoise2D(float2(h.x * 26.0 + sPhase, h.y * 3.5)));
                    float totalSlip = max(heelSmudge, fingersSlip) * streakLines;

                    float rawContact = max(palmFill * 0.94, fingersFill);
                    // Slight dusty UV fluorescence halo around heavily pressed contact pads
                    float pressureGlow = max(palmCore * 0.78, fingersCore * 1.15) * dustyMottle;

                    float quadFade = smoothstep(0.99, 0.88, max(abs(centered.x), abs(centered.y)));
                    float shimmer = 0.95 + 0.05 * sin(_Time.y * 2.4 + h.y * 5.0);

                    edgeBoost = saturate(pressureGlow * 1.15) * shimmer * quadFade;
                    shapeMask = saturate(max(rawContact * residueTexture, totalSlip) + edgeBoost * 0.22) * shimmer * quadFade;
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
