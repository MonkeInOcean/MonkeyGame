using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Rendering
{
    /// <summary>
    /// Per-camera, screen-space underwater look. Replaces the global
    /// <c>RenderSettings.fog</c> (which is a single shared toggle and can't work
    /// once there are multiple player cameras) with a fullscreen pass that runs
    /// once per rendered camera and decides submersion from THAT camera's world Y
    /// versus the ocean surface. Multiplayer-safe by construction.
    ///
    /// Does per-channel absorption (red fades first), depth-tinted water colour,
    /// procedural caustics on up-facing geometry, and a subtle lens wobble — all
    /// in one pass (shared depth->world reconstruction).
    ///
    /// Add it to the URP Renderer (PC_Renderer) via "Add Renderer Feature". The
    /// material is built automatically from the UnderwaterEffects shader.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnderwaterRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("World Y of the ocean surface. Defaults to the shared WaterLevel.SurfaceY.")]
            public float surfaceY = Ocean.WaterLevel.SurfaceY;
            [Tooltip("Depth (metres) below the surface at which the deep colour is reached.")]
            public float depthRange = 60f;

            [Header("Absorption")]
            public Color shallowColor = new Color(0.16f, 0.45f, 0.52f, 1f);
            public Color deepColor = new Color(0.02f, 0.09f, 0.18f, 1f);
            [Tooltip("Per-channel extinction. Red highest so it drains first.")]
            public Vector3 extinction = new Vector3(0.16f, 0.055f, 0.035f);
            [Range(0f, 4f)] public float fogDensity = 1f;

            [Header("Caustics")]
            public Color causticColor = new Color(0.55f, 0.75f, 0.7f, 1f);
            public float causticScale = 6f;
            public float causticSpeed = 0.06f;
            [Range(0f, 3f)] public float causticStrength = 0.8f;
            [Tooltip("Depth over which caustics fade to nothing.")]
            public float causticDepthFade = 45f;

            [Header("Lens Wobble")]
            [Range(0f, 0.02f)] public float wobbleAmp = 0.0025f;
            public float wobbleScale = 28f;
            public float wobbleSpeed = 0.7f;

            [Header("Advanced")]
            [Tooltip("Metres above/below the surface over which the effect eases in.")]
            public float boundaryBand = 0.5f;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        [SerializeField] private Settings settings = new Settings();
        [Tooltip("Log why the underwater pass does or doesn't run each time the camera " +
                 "crosses the waterline. Turn off once it's working.")]
        [SerializeField] private bool debugLogging = true;

        private Material material;
        private UnderwaterPass pass;
        private bool wasSubmerged;

        private const string ShaderName = "MonkeInOcean/UnderwaterEffects";

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[Underwater] Shader '{ShaderName}' not found — " +
                                     "effect disabled. Check the shader compiled without errors.");
                return false;
            }
            material = CoreUtils.CreateEngineMaterial(shader);
            return true;
        }

        public override void Create()
        {
            EnsureMaterial();
            pass = new UnderwaterPass(settings) { renderPassEvent = settings.renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Only game and scene-view cameras; skip reflection/preview passes.
            CameraType type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;

            if (!EnsureMaterial()) return;

            // Per-camera submersion test — the whole point of this feature.
            float camY = renderingData.cameraData.camera.transform.position.y;
            float submersion = Mathf.InverseLerp(
                settings.surfaceY + settings.boundaryBand,
                settings.surfaceY - settings.boundaryBand,
                camY);

            bool submerged = submersion > 0f;
            if (debugLogging && submerged != wasSubmerged && type == CameraType.Game)
            {
                Debug.Log($"[Underwater] camY={camY:F2} surfaceY={settings.surfaceY} " +
                          $"submersion={submersion:F2} -> {(submerged ? "ENQUEUED" : "above water, skipped")}");
                wasSubmerged = submerged;
            }

            if (!submerged) return; // camera is above water; nothing to do.

            pass.Setup(material, submersion);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
        }

        // ----------------------------------------------------------------------

        private class UnderwaterPass : ScriptableRenderPass
        {
            private readonly Settings settings;
            private Material material;
            private float submersion;

            private static readonly int IdShallow = Shader.PropertyToID("_ShallowColor");
            private static readonly int IdDeep = Shader.PropertyToID("_DeepColor");
            private static readonly int IdExtinction = Shader.PropertyToID("_Extinction");
            private static readonly int IdFogDensity = Shader.PropertyToID("_FogDensity");
            private static readonly int IdDeepFactor = Shader.PropertyToID("_DeepFactor");
            private static readonly int IdSurfaceY = Shader.PropertyToID("_SurfaceY");
            private static readonly int IdCausticColor = Shader.PropertyToID("_CausticColor");
            private static readonly int IdCausticScale = Shader.PropertyToID("_CausticScale");
            private static readonly int IdCausticSpeed = Shader.PropertyToID("_CausticSpeed");
            private static readonly int IdCausticStrength = Shader.PropertyToID("_CausticStrength");
            private static readonly int IdCausticDepthFade = Shader.PropertyToID("_CausticDepthFade");
            private static readonly int IdWobbleAmp = Shader.PropertyToID("_WobbleAmp");
            private static readonly int IdWobbleScale = Shader.PropertyToID("_WobbleScale");
            private static readonly int IdWobbleSpeed = Shader.PropertyToID("_WobbleSpeed");

            public UnderwaterPass(Settings settings)
            {
                this.settings = settings;
            }

            public void Setup(Material mat, float submersionAmount)
            {
                material = mat;
                submersion = submersionAmount;
            }

            private void PushParams(float camY)
            {
                float deepFactor = settings.depthRange > 0.001f
                    ? Mathf.Clamp01((settings.surfaceY - camY) / settings.depthRange)
                    : 1f;

                material.SetColor(IdShallow, settings.shallowColor);
                material.SetColor(IdDeep, settings.deepColor);
                material.SetVector(IdExtinction, settings.extinction);
                // Ease the whole effect in across the waterline band.
                material.SetFloat(IdFogDensity, settings.fogDensity * submersion);
                material.SetFloat(IdDeepFactor, deepFactor);
                material.SetFloat(IdSurfaceY, settings.surfaceY);
                material.SetColor(IdCausticColor, settings.causticColor);
                material.SetFloat(IdCausticScale, settings.causticScale);
                material.SetFloat(IdCausticSpeed, settings.causticSpeed);
                material.SetFloat(IdCausticStrength, settings.causticStrength * submersion);
                material.SetFloat(IdCausticDepthFade, settings.causticDepthFade);
                material.SetFloat(IdWobbleAmp, settings.wobbleAmp * submersion);
                material.SetFloat(IdWobbleScale, settings.wobbleScale);
                material.SetFloat(IdWobbleSpeed, settings.wobbleSpeed);
            }

            private class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            private static readonly Vector4 FullScreenScaleBias = new Vector4(1f, 1f, 0f, 0f);

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (material == null) return;
                if (resourceData.isActiveTargetBackBuffer)
                {
                    // No intermediate texture to read from — set PC_Renderer's
                    // "Intermediate Texture" to "Always".
                    Debug.LogWarning("[Underwater] Active target is the backbuffer; " +
                                     "set PC_Renderer > Intermediate Texture = Always.");
                    return;
                }

                PushParams(cameraData.camera.transform.position.y);

                TextureHandle cameraColor = resourceData.activeColorTexture;

                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_UnderwaterCopy", false);

                // Pass A: plain copy of the current camera colour into a temp texture.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Underwater Copy", out var copyData))
                {
                    copyData.source = cameraColor;
                    builder.UseTexture(copyData.source);
                    builder.SetRenderAttachment(temp, 0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, FullScreenScaleBias, 0, false);
                    });
                }

                // Pass B: blit temp -> camera colour through the underwater material.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Underwater Effects", out var passData))
                {
                    passData.source = temp;
                    passData.material = material;
                    builder.UseTexture(passData.source);
                    builder.SetRenderAttachment(cameraColor, 0);
                    // Makes _CameraDepthTexture (and other URP globals) valid in the shader.
                    builder.UseAllGlobalTextures(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, FullScreenScaleBias, data.material, 0);
                    });
                }
            }
        }
    }
}
