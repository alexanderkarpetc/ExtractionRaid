using Dev;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace View.FogOfWar
{
    /// <summary>
    /// URP ScriptableRendererFeature that blurs the raw FOV visibility mask
    /// and composites it over the scene (darken + desaturate non-visible areas).
    /// Add to PC_Renderer.asset, assign Blur and Composite materials in Inspector.
    /// Uses RenderGraph API (Unity 6 URP default).
    /// </summary>
    public class FogOfWarFeature : ScriptableRendererFeature
    {
        [Header("Materials")]
        public Material blurMaterial;
        public Material compositeMaterial;

        FogOfWarPass _pass;

        public override void Create()
        {
            _pass = new FogOfWarPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (blurMaterial == null || compositeMaterial == null) return;

            // Only apply to the main game camera — skip scene view, preview, and FOV camera (has targetTexture)
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (renderingData.cameraData.camera.targetTexture != null) return;

            _pass.Setup(blurMaterial, compositeMaterial,
                DevCheats.FogBlurRadius, DevCheats.FogBlurIterations,
                DevCheats.FogIntensity, DevCheats.FogDesaturation, DevCheats.FogColor);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        class FogOfWarPass : ScriptableRenderPass
        {
            Material _blurMat;
            Material _compositeMat;
            float _blurSize;
            int _blurIterations;
            float _fogIntensity;
            float _fogDesaturation;
            Color _fogColor;

            static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
            static readonly int FoWBlurredId = Shader.PropertyToID("_FoWBlurred");
            static readonly int FoWVisibilityId = Shader.PropertyToID("_FoWVisibility");
            static readonly int FogIntensityId = Shader.PropertyToID("_FogIntensity");
            static readonly int DesaturationId = Shader.PropertyToID("_DesaturationAmount");
            static readonly int FogColorId = Shader.PropertyToID("_FogColor");

            public void Setup(Material blur, Material composite,
                float blurSize, int iterations,
                float fogIntensity, float fogDesaturation, Color fogColor)
            {
                _blurMat = blur;
                _compositeMat = composite;
                _blurSize = blurSize;
                _blurIterations = iterations;
                _fogIntensity = fogIntensity;
                _fogDesaturation = fogDesaturation;
                _fogColor = fogColor;
            }

            // ── Render Graph path (Unity 6 default) ──────────────────────────

            class PassData
            {
                public Material blurMat;
                public Material compositeMat;
                public float blurSize;
                public int blurIterations;
                public float fogIntensity;
                public float fogDesaturation;
                public Color fogColor;
                public TextureHandle cameraColor;
                public TextureHandle tempA;
                public TextureHandle tempB;
                public Texture rawFoW;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var rawTex = Shader.GetGlobalTexture(FoWVisibilityId);
                if (rawTex == null || _blurMat == null || _compositeMat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();

                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.depthBufferBits = 0;
                desc.name = "_FoWTempA";
                var tempA = renderGraph.CreateTexture(desc);
                desc.name = "_FoWTempB";
                var tempB = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddUnsafePass<PassData>("FogOfWar", out var passData))
                {
                    passData.blurMat = _blurMat;
                    passData.compositeMat = _compositeMat;
                    passData.blurSize = _blurSize;
                    passData.blurIterations = _blurIterations;
                    passData.fogIntensity = _fogIntensity;
                    passData.fogDesaturation = _fogDesaturation;
                    passData.fogColor = _fogColor;
                    passData.cameraColor = resourceData.activeColorTexture;
                    passData.tempA = tempA;
                    passData.tempB = tempB;
                    passData.rawFoW = rawTex;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(tempA, AccessFlags.ReadWrite);
                    builder.UseTexture(tempB, AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                        // --- Blur passes ---
                        data.blurMat.SetFloat(BlurSizeId, data.blurSize);

                        // First iteration: raw → tempA (H) → tempB (V)
                        cmd.Blit(data.rawFoW, data.tempA, data.blurMat, 0);
                        cmd.Blit(data.tempA, data.tempB, data.blurMat, 1);

                        // Additional iterations
                        for (int i = 1; i < data.blurIterations; i++)
                        {
                            cmd.Blit(data.tempB, data.tempA, data.blurMat, 0);
                            cmd.Blit(data.tempA, data.tempB, data.blurMat, 1);
                        }

                        // --- Composite pass ---
                        data.compositeMat.SetFloat(FogIntensityId, data.fogIntensity);
                        data.compositeMat.SetFloat(DesaturationId, data.fogDesaturation);
                        data.compositeMat.SetColor(FogColorId, data.fogColor);
                        data.compositeMat.SetTexture(FoWBlurredId, data.tempB);
                        cmd.Blit(data.cameraColor, data.tempA, data.compositeMat, 0);
                        cmd.Blit(data.tempA, data.cameraColor);
                    });
                }
            }

            public void Dispose() { }
        }
    }
}
