Shader "MonkeInOcean/UnderwaterEffects"
{
    // Fullscreen underwater post pass driven by UnderwaterRendererFeature.
    // Replaces the global RenderSettings.fog with a per-camera, screen-space
    // effect (multiplayer-safe: each camera resolves its own submersion):
    //   - per-channel exponential absorption (red dies first -> deep teal/blue),
    //   - depth-tinted water colour (shallow near surface, deep further down),
    //   - procedural caustics projected onto up-facing seabed geometry,
    //   - a subtle lens wobble for the "looking through water" feel.
    //
    // Reads _BlitTexture (current camera colour, bound by the feature) and the
    // global _CameraDepthTexture (world-position / distance reconstruction).
    Properties {}

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "UnderwaterEffects"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ---- Material params (pushed each frame by the renderer feature) ----
            float4 _ShallowColor;     // water in-scatter tint near the surface
            float4 _DeepColor;        // water in-scatter tint far below
            float3 _Extinction;       // per-channel extinction (r > g > b)
            float  _FogDensity;       // global multiplier on extinction
            float  _DeepFactor;       // 0..1 how deep the camera is (surface->floor)
            float  _SurfaceY;         // world Y of the ocean surface

            float3 _CausticColor;
            float  _CausticScale;     // world units per caustic cell
            float  _CausticSpeed;
            float  _CausticStrength;
            float  _CausticDepthFade; // metres of depth over which caustics fade out

            float  _WobbleAmp;
            float  _WobbleScale;
            float  _WobbleSpeed;

            // Ocean wave state, published globally by OceanController. Lets the
            // caustics drift along the real wave direction so the light on the
            // seabed moves with the swell overhead. Zero when no ocean is present.
            float4 _OceanWaveA;       // (dirX, dirZ, steepness, wavelength)

            // Cheap 2D value noise (no texture dependency).
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Caustic web that drifts and ripples along the wave direction.
            // 'flow' is the per-frame travel offset (wave dir * time); an animated
            // domain warp makes the filaments writhe like refracted sunlight
            // instead of sliding rigidly across the floor.
            float Caustics(float2 p, float t, float2 flow)
            {
                // Wobble the sampling domain so the net breathes and reforms.
                float2 warp = float2(
                    ValueNoise(p * 0.5 + flow * 0.6),
                    ValueNoise(p * 0.5 - flow * 0.6 + 13.7)) - 0.5;
                p += warp * 1.6;

                float2 a = p + flow;
                float2 b = p * 1.4 - flow * 0.7 + float2(0.0, t * 0.35);
                float na = ValueNoise(a);
                float nb = ValueNoise(b);

                float ridged = saturate((1.0 - abs(na - 0.5) * 2.0) *
                                        (1.0 - abs(nb - 0.5) * 2.0));

                // Softer core than before (pow 2 instead of 4) -> broader, brighter
                // filaments, with a sharp inner streak layered on for the hot centre.
                float web = pow(ridged, 2.0) * 0.85 + pow(ridged, 6.0) * 0.7;

                // Slow brightness pulse so the whole field shimmers.
                web *= 0.7 + 0.3 * sin(t * 1.6 + na * 6.2831);
                return web;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float t = _Time.y;

                // Subtle lens wobble.
                float2 wob = float2(
                    sin(uv.y * _WobbleScale + t * _WobbleSpeed),
                    cos(uv.x * _WobbleScale + t * _WobbleSpeed)) * _WobbleAmp;
                float2 uvW = uv + wob;

                half3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvW).rgb;

                float rawDepth = SampleSceneDepth(uv);
                bool isGeometry = rawDepth > 1e-6;                 // reversed-Z: sky ~ 0
                float viewDist = LinearEyeDepth(rawDepth, _ZBufferParams);

                // ---- Caustics on up-facing geometry ----
                if (isGeometry)
                {
                    float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float3 nWS = normalize(cross(ddy(posWS), ddx(posWS)));
                    // Let gently sloped surfaces still catch light, not just flat floor.
                    float up = pow(saturate(abs(nWS.y)), 0.5);

                    // Drift the caustics along the dominant wave direction; fall back
                    // to a diagonal if no ocean is publishing wave globals.
                    float2 waveDir = _OceanWaveA.xy;
                    waveDir = (dot(waveDir, waveDir) > 1e-4) ? normalize(waveDir) : float2(0.7, 0.7);
                    float2 flow = waveDir * (t * _CausticSpeed);

                    float caust = Caustics(posWS.xz / max(_CausticScale, 0.001), t * _CausticSpeed, flow);
                    float depthBelow = _SurfaceY - posWS.y;
                    float depthFade = saturate(1.0 - depthBelow / max(_CausticDepthFade, 0.001));
                    float distFade = saturate(1.0 - viewDist / 200.0);

                    sceneColor += _CausticColor * (caust * _CausticStrength * up * depthFade * distFade);
                }

                // ---- Per-channel absorption + depth-tinted water ----
                float3 ext = _Extinction * _FogDensity;
                float3 trans = exp(-viewDist * ext);               // red drains first
                float3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(_DeepFactor));
                float3 col = sceneColor * trans + tint * (1.0 - trans);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
