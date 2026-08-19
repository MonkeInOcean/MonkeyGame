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

            // Two counter-scrolling noise layers, ridged and multiplied -> caustic web.
            float Caustics(float2 uv, float t)
            {
                float2 a = uv + float2(t, t * 0.6);
                float2 b = uv * 1.3 - float2(t * 0.8, t);
                float na = ValueNoise(a);
                float nb = ValueNoise(b);
                float ridged = (1.0 - abs(na - 0.5) * 2.0) * (1.0 - abs(nb - 0.5) * 2.0);
                return pow(saturate(ridged), 4.0);
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
                    float up = saturate(abs(nWS.y));

                    float caust = Caustics(posWS.xz / max(_CausticScale, 0.001), t * _CausticSpeed);
                    float depthBelow = _SurfaceY - posWS.y;
                    float depthFade = saturate(1.0 - depthBelow / max(_CausticDepthFade, 0.001));
                    float distFade = saturate(1.0 - viewDist / 120.0);

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
