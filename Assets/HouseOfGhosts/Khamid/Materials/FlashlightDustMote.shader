Shader "HouseOfGhosts/FlashlightDustMote"
{
    Properties
    {
        [HDR] _DustColor ("Dust Mote Color (RGB + Alpha)", Color) = (1.0, 0.94, 0.80, 0.80)
        _BeamOriginWS ("Beam Origin World Space", Vector) = (0, 1, 0, 1)
        _BeamDirWS ("Beam Forward World Space", Vector) = (0, 0, 1, 0)
        // x = cos(halfOuterAngle), y = cos(halfInnerAngle), z = wallStopDist, w = maxBeamRange
        _BeamParams ("Beam Cone & Distance Params", Vector) = (0.93, 0.97, 8.0, 8.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+55"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "DustMoteForward"
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

            CBUFFER_START(UnityPerMaterial)
                half4 _DustColor;
                float4 _BeamOriginWS;
                float4 _BeamDirWS;
                float4 _BeamParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                // Custom Vertex Stream: TEXCOORD0.xy = UV, TEXCOORD0.z = Stable Random01, TEXCOORD1.xyz = Particle Center WS
                float4 uvAndRand  : TEXCOORD0;
                float3 centerWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 beamData   : TEXCOORD1; // x = coneIllumination, y = nearLensBokehBlend
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
                // Fallback to vertex world position if custom particle center stream isn't active
                float3 samplePosWS = (dot(input.centerWS, input.centerWS) > 0.0001) ? input.centerWS : posWS;

                float3 toMote = samplePosWS - _BeamOriginWS.xyz;
                float dist = length(toMote);
                float3 dirToMote = toMote / max(0.0001, dist);
                float3 beamDir = normalize(_BeamDirWS.xyz);

                float cosAngle = dot(dirToMote, beamDir);
                float coneMask = smoothstep(_BeamParams.x, _BeamParams.y, cosAngle);

                float wallStop = max(0.15, _BeamParams.z);
                float maxRange = max(0.50, _BeamParams.w);
                float nearLensFade = smoothstep(0.06, 0.24, dist);
                float wallMask = smoothstep(wallStop + 0.02, max(0.08, wallStop - 0.22), dist);
                float distFalloff = 1.0 - saturate(dist / maxRange) * 0.72;

                float twinkle = 0.72 + 0.28 * sin(_Time.y * 3.4 + input.uvAndRand.z * 31.4159);
                float beamIllum = coneMask * nearLensFade * wallMask * distFalloff * twinkle;

                // Zero-area cull motes outside the spotlight cone or behind walls (0 overdraw on Quest 3!)
                if (beamIllum <= 0.003 || _DustColor.a <= 0.002)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                }

                output.positionCS = TransformWorldToHClip(posWS);
                output.color = input.color;
                output.uv = input.uvAndRand.xy;
                output.beamData = float2(beamIllum, smoothstep(0.75, 0.16, dist));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float r = length(centered);
                if (r >= 1.0)
                {
                    return half4(0, 0, 0, 0);
                }

                // Distant motes (>0.85m) = bright pinpoint dust speck + soft halo; Close motes (<0.45m) = airy out-of-focus bokeh disc!
                float radial = saturate(1.0 - r);
                float sharpSpeck = radial * radial * radial * 1.85 + radial * radial * 0.55;
                float softBokeh = smoothstep(1.0, 0.58, r) * (0.48 + 0.42 * smoothstep(0.30, 0.80, r));
                float moteShape = lerp(sharpSpeck, softBokeh, input.beamData.y);

                half alpha = (half)saturate(input.color.a * _DustColor.a * input.beamData.x * moteShape);
                half3 rgb = input.color.rgb * _DustColor.rgb * (half)(1.45 + sharpSpeck * 0.55);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
