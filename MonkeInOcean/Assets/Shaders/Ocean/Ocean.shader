Shader "MonkeInOcean/Ocean"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.55, 0.62, 1)
        _DeepColor    ("Deep Color", Color)    = (0.02, 0.13, 0.22, 1)
        _DepthFade    ("Color Depth Fade", Float) = 6.0
        _FloorColorDepth ("Seabed Color Depth (m)", Float) = 12.0

        [Header(Shore)]
        _WaveShoreFalloff ("Wave Shore Falloff (m)", Float) = 8.0
        _ShoreJitter      ("Shore Transition Jitter (m)", Range(0, 12)) = 3.0

        [Header(Reflection and Refraction)]
        _FresnelPower     ("Fresnel Power", Range(1, 8)) = 5.0
        _RefractionStrength ("Refraction Strength", Range(0, 1)) = 0.12
        _Smoothness       ("Smoothness", Range(0, 1)) = 0.9

        [Header(Foam)]
        _FoamColor      ("Foam Color", Color) = (1, 1, 1, 1)
        _CrestFoam      ("Crest Foam Amount", Range(0, 1)) = 0.55
        _CrestFoamSharp ("Crest Foam Sharpness", Range(1, 12)) = 5.0
        _ShoreFoam      ("Shore Foam Distance", Float) = 1.5
        _FoamNoiseScale ("Foam Noise Scale", Float) = 0.4

        [Header(Waves)]
        _WaveA ("Wave A (dir xy, steep, len)", Vector) = (1, 1, 0.11, 80)
        _WaveB ("Wave B (dir xy, steep, len)", Vector) = (1, 0.4, 0.11, 44)
        _WaveC ("Wave C (dir xy, steep, len)", Vector) = (0.4, 1, 0.09, 26)
        _WaveD ("Wave D (dir xy, steep, len)", Vector) = (1, 0.7, 0.08, 16)
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 1.0

        [Header(Detail Chop)]
        _DetailNormal ("Detail Normal Map (unused)", 2D) = "bump" {}
        _DetailScale  ("Detail Frequency", Float) = 0.2
        _DetailSpeed  ("Detail Scroll Speed", Float) = 0.03
        _DetailStrength ("Detail Chop Strength", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "OceanSurface"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero          // opaque-style; refraction is sampled manually
            ZWrite On
            Cull Off                // two-sided so the underside is visible from below

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "GerstnerWaves.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthFade;
                float  _FloorColorDepth;
                float  _WaveShoreFalloff;
                float  _ShoreJitter;
                float  _FresnelPower;
                float  _RefractionStrength;
                float  _Smoothness;
                float4 _FoamColor;
                float  _CrestFoam;
                float  _CrestFoamSharp;
                float  _ShoreFoam;
                float  _FoamNoiseScale;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float4 _WaveD;
                float  _WaveSpeed;
                float4 _DetailNormal_ST;
                float  _DetailScale;
                float  _DetailSpeed;
                float  _DetailStrength;
            CBUFFER_END

            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);

            // Global water-depth map baked from the terrain (see OceanDepthMap.cs).
            // Each texel = surfaceY - seabedHeight. Set globally, so declared OUTSIDE
            // the per-material CBUFFER.
            TEXTURE2D(_OceanDepthTex); SAMPLER(sampler_OceanDepthTex);
            float4 _OceanDepthArea; // xy = world min (x,z), zw = world span (x,z)

            // Returns water depth (metres, seabed below surface) at a world XZ.
            // Falls back to "deep" when no map is bound or the point is off-terrain,
            // so the ocean keeps full-size waves rather than going flat.
            float SampleFloorDepth(float2 worldXZ)
            {
                if (_OceanDepthArea.z <= 0.0) return 1000.0;
                float2 uv = (worldXZ - _OceanDepthArea.xy) / _OceanDepthArea.zw;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 1000.0;
                return SAMPLE_TEXTURE2D_LOD(_OceanDepthTex, sampler_OceanDepthTex, uv, 0).r;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
                float  foam        : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                float  floorDepth  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // cheap value noise for breaking up foam edges
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 flatWS = TransformObjectToWorld(IN.positionOS.xyz);

                float3 normalWS;
                float foam;
                float3 displacedWS = SampleGerstner(
                    flatWS, _WaveA, _WaveB, _WaveC, _WaveD,
                    _WaveSpeed, _Time.y, normalWS, foam);

                // Damp waves toward flat water as the seabed rises to the surface, so
                // islands and shorelines aren't flooded. Full waves stay in deep water.
                float floorDepth = SampleFloorDepth(flatWS.xz);
                // Jitter the depth the wave-calming keys off, so the boundary where waves
                // settle down near land is an organic wavy line, not a clean depth contour.
                float shoreJitter = (valueNoise(flatWS.xz * 0.12) - 0.5) * 2.0 * _ShoreJitter;
                float shoreFactor = smoothstep(0.0, max(_WaveShoreFalloff, 0.001), floorDepth + shoreJitter);
                displacedWS = lerp(flatWS, displacedWS, shoreFactor);
                normalWS = normalize(lerp(float3(0, 1, 0), normalWS, shoreFactor));
                foam *= shoreFactor;

                OUT.floorDepth = floorDepth;
                OUT.positionWS = displacedWS;
                OUT.normalWS = normalWS;
                OUT.positionHCS = TransformWorldToHClip(displacedWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.foam = foam;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            // Multi-octave scrolling height field used to derive fine ripple normals.
            // This is the "chop detail" that would otherwise need a huge, dense mesh —
            // we put it in shading instead of geometry so it never facets.
            float DetailHeight(float2 p, float t)
            {
                float h = 0.0;
                float amp = 0.5;
                float2 scroll = float2(0.11, 0.07);
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    h += amp * valueNoise(p + t * scroll);
                    p = p * 2.03 + 17.3;          // rotate/offset each octave
                    scroll *= -1.6;                // cross-scroll for a lively, non-repeating look
                    amp *= 0.5;
                }
                return h;
            }

            // Finite-difference slope of the detail height -> world-space normal perturbation.
            float2 ProceduralDetailSlope(float2 worldXZ, float t)
            {
                float2 p = worldXZ * _DetailScale;
                float ts = t * _DetailSpeed * 40.0;
                float e = 0.6;
                float hL = DetailHeight(p - float2(e, 0), ts);
                float hR = DetailHeight(p + float2(e, 0), ts);
                float hD = DetailHeight(p - float2(0, e), ts);
                float hU = DetailHeight(p + float2(0, e), ts);
                return float2(hL - hR, hD - hU);
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);
                // flip for the underside so lighting/fresnel read correctly from below
                N *= IS_FRONT_VFACE(facing, 1.0, -1.0);

                // blend in procedural micro-ripple chop (fades with distance so the far
                // ocean doesn't turn into sparkly noise)
                float distFade = saturate(1.0 - length(IN.positionWS - _WorldSpaceCameraPos) / 250.0);
                float2 slope = ProceduralDetailSlope(IN.positionWS.xz, _Time.y);
                N = normalize(N + float3(slope.x, 0, slope.y) * _DetailStrength * distFade);

                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // ---- screen-space depth for water thickness ----
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEyeDepth = IN.screenPos.w;
                float waterDepth = max(sceneEyeDepth - surfaceEyeDepth, 0);

                // ---- refraction: offset the opaque grab by the surface normal ----
                float depthFade = saturate(waterDepth / max(_DepthFade, 0.001));
                float2 refractOffset = N.xz * _RefractionStrength * (1.0 - depthFade * 0.5);
                float2 refractUV = screenUV + refractOffset;
                // guard against sampling in front of the surface (halos)
                float refrRaw = SampleSceneDepth(refractUV);
                float refrEye = LinearEyeDepth(refrRaw, _ZBufferParams);
                if (refrEye < surfaceEyeDepth) refractUV = screenUV;
                float3 refraction = SampleSceneColor(refractUV);

                // ---- water body color by SEABED depth (shallow near islands -> deep offshore) ----
                // Jitter + smoothstep so the shallow->deep band reads as an organic gradient
                // instead of a hard-coded contour ring around every island.
                float bandJitter = (valueNoise(IN.positionWS.xz * 0.12 + _Time.y * 0.015) - 0.5) * 2.0 * _ShoreJitter;
                float floorFade = smoothstep(0.0, max(_FloorColorDepth, 0.001), IN.floorDepth + bandJitter);
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, floorFade);

                // Tint opacity is driven mainly by how deep the seabed is: shallow water stays
                // clear (the bottom shows through, lighter) and only deep water turns fully
                // opaque. A little screen thickness keeps grazing edges reading as water.
                float opacity = saturate(floorFade + depthFade * 0.25);
                float3 throughWater = lerp(refraction, waterColor, opacity);

                // ---- reflection (environment probe / skybox) ----
                float3 R = reflect(-V, N);
                float3 reflection = GlossyEnvironmentReflection(R, IN.positionWS, 1.0 - _Smoothness, 1.0);

                // ---- fresnel mix ----
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float3 color = lerp(throughWater, reflection, saturate(fresnel));

                // ---- main light: diffuse tint + sharp sun specular glitter ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(N, mainLight.direction));
                color += waterColor * mainLight.color * ndotl * 0.15 * mainLight.shadowAttenuation;

                float3 H = normalize(mainLight.direction + V);
                float spec = pow(saturate(dot(N, H)), lerp(64.0, 2048.0, _Smoothness));
                color += mainLight.color * spec * mainLight.shadowAttenuation;

                // ---- foam: crest whitecaps + shoreline line ----
                float foamNoise = valueNoise(IN.positionWS.xz * _FoamNoiseScale + _Time.y * 0.1);
                float crestFoam = saturate((IN.foam - (1.0 - _CrestFoam)) * _CrestFoamSharp) * foamNoise;
                float shoreFoam = saturate(1.0 - waterDepth / max(_ShoreFoam, 0.001));
                shoreFoam *= step(0.0001, waterDepth); // no foam where there's no scene behind
                shoreFoam *= (0.6 + 0.4 * foamNoise);
                float foamMask = saturate(max(crestFoam, shoreFoam));
                color = lerp(color, _FoamColor.rgb, foamMask);

                color = MixFog(color, IN.fogFactor);

                // Opaque write (Blend One Zero): see-through comes from the manual
                // refraction grab above, not hardware alpha blending.
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
