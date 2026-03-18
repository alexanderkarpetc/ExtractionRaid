using Dev;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace View.FogOfWar
{
    /// <summary>
    /// URP ScriptableRendererFeature: blur → temporal blend → composite.
    /// Assign Blur, TemporalBlend and Composite materials in Inspector.
    /// </summary>
    public class FogOfWarFeature : ScriptableRendererFeature
    {
        [Header("Materials")]
        public Material blurMaterial;
        public Material temporalBlendMaterial;
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

            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (renderingData.cameraData.camera.targetTexture != null) return;

            _pass.Setup(blurMaterial, temporalBlendMaterial, compositeMaterial,
                DevCheats.FogBlurRadius, DevCheats.FogBlurIterations,
                DevCheats.FogIntensity, DevCheats.FogDesaturation,
                DevCheats.FogColor, DevCheats.FogTemporalBlend,
                DevCheats.FoWBypassBlur);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        class FogOfWarPass : ScriptableRenderPass
        {
            Material _blurMat;
            Material _temporalMat;
            Material _compositeMat;
            float _blurSize;
            int _blurIterations;
            float _fogIntensity;
            float _fogDesaturation;
            Color _fogColor;
            float _temporalBlend;
            bool _bypassBlur;

            // Cached RTHandle wrappers for ImportTexture (avoid GC alloc every frame)
            RTHandle _rawRTHandle;
            RTHandle _prevBlurredRTHandle;
            RenderTexture _cachedRawRT;
            RenderTexture _cachedPrevRT;

            static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
            static readonly int FoWBlurredId = Shader.PropertyToID("_FoWBlurred");
            static readonly int FoWVisibilityId = Shader.PropertyToID("_FoWVisibility");
            static readonly int FoWPrevBlurredId = Shader.PropertyToID("_FoWPrevBlurred");
            static readonly int FogIntensityId = Shader.PropertyToID("_FogIntensity");
            static readonly int DesaturationId = Shader.PropertyToID("_DesaturationAmount");
            static readonly int FogColorId = Shader.PropertyToID("_FogColor");
            static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");
            static readonly int BlendFactorId = Shader.PropertyToID("_BlendFactor");

            public void Setup(Material blur, Material temporal, Material composite,
                float blurSize, int iterations,
                float fogIntensity, float fogDesaturation,
                Color fogColor, float temporalBlend,
                bool bypassBlur)
            {
                _blurMat = blur;
                _temporalMat = temporal;
                _compositeMat = composite;
                _blurSize = blurSize;
                _blurIterations = iterations;
                _fogIntensity = fogIntensity;
                _fogDesaturation = fogDesaturation;
                _fogColor = fogColor;
                _temporalBlend = temporalBlend;
                _bypassBlur = bypassBlur;
            }

            class PassData
            {
                public Material blurMat;
                public Material temporalMat;
                public Material compositeMat;
                public float blurSize;
                public int blurIterations;
                public float fogIntensity;
                public float fogDesaturation;
                public Color fogColor;
                public float temporalBlend;
                public TextureHandle cameraColor;
                public TextureHandle tempA;
                public TextureHandle tempB;
                public TextureHandle rawFoW;       // imported via RenderGraph
                public TextureHandle prevBlurred;  // imported via RenderGraph
                public bool hasPrevBlurred;
                public bool bypassBlur;
            }

            /// <summary>
            /// Get or create a cached RTHandle wrapper for a RenderTexture.
            /// Only re-allocates if the underlying RT changed.
            /// </summary>
            RTHandle GetOrCreateRTHandle(ref RTHandle handle, ref RenderTexture cached, RenderTexture rt)
            {
                if (rt == null) return null;
                if (cached == rt && handle != null) return handle;

                handle?.Release();
                handle = RTHandles.Alloc(rt);
                cached = rt;
                return handle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var rawTex = Shader.GetGlobalTexture(FoWVisibilityId) as RenderTexture;
                if (rawTex == null || _blurMat == null || _compositeMat == null) return;

                var prevTex = Shader.GetGlobalTexture(FoWPrevBlurredId) as RenderTexture;

                var resourceData = frameData.Get<UniversalResourceData>();

                // ── Import external RTs into RenderGraph ──
                // This is the correct way to use external textures in RenderGraph.
                // On DX12, cmd.Blit(Texture, TextureHandle) silently fails —
                // the external Texture isn't bound as _MainTex.
                // ImportTexture wraps external RTs as TextureHandles so all blits
                // are TextureHandle↔TextureHandle, which works on all graphics APIs.
                var rawRTH = GetOrCreateRTHandle(ref _rawRTHandle, ref _cachedRawRT, rawTex);
                var rawHandle = renderGraph.ImportTexture(rawRTH);

                TextureHandle prevHandle = TextureHandle.nullHandle;
                bool hasPrev = false;
                if (prevTex != null)
                {
                    var prevRTH = GetOrCreateRTHandle(ref _prevBlurredRTHandle, ref _cachedPrevRT, prevTex);
                    prevHandle = renderGraph.ImportTexture(prevRTH);
                    hasPrev = true;
                }

                // Transient temps for blur + composite
                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.depthBufferBits = 0;
                desc.name = "_FoWTempA";
                var tempA = renderGraph.CreateTexture(desc);
                desc.name = "_FoWTempB";
                var tempB = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddUnsafePass<PassData>("FogOfWar", out var passData))
                {
                    passData.blurMat = _blurMat;
                    passData.temporalMat = _temporalMat;
                    passData.compositeMat = _compositeMat;
                    passData.blurSize = _blurSize;
                    passData.blurIterations = _blurIterations;
                    passData.fogIntensity = _fogIntensity;
                    passData.fogDesaturation = _fogDesaturation;
                    passData.fogColor = _fogColor;
                    passData.temporalBlend = _temporalBlend;
                    passData.cameraColor = resourceData.activeColorTexture;
                    passData.tempA = tempA;
                    passData.tempB = tempB;
                    passData.rawFoW = rawHandle;
                    passData.prevBlurred = prevHandle;
                    passData.hasPrevBlurred = hasPrev;
                    passData.bypassBlur = _bypassBlur;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(tempA, AccessFlags.ReadWrite);
                    builder.UseTexture(tempB, AccessFlags.ReadWrite);
                    builder.UseTexture(rawHandle, AccessFlags.Read);
                    if (hasPrev)
                        builder.UseTexture(prevHandle, AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                        // --- Composite helper ---
                        void Composite(TextureHandle blurredSource)
                        {
                            cmd.SetGlobalTexture(FoWBlurredId, blurredSource);
                            data.compositeMat.SetFloat(FogIntensityId, data.fogIntensity);
                            data.compositeMat.SetFloat(DesaturationId, data.fogDesaturation);
                            data.compositeMat.SetColor(FogColorId, data.fogColor);
                            cmd.Blit(data.cameraColor, data.tempA, data.compositeMat, 0);
                            cmd.Blit(data.tempA, data.cameraColor);
                        }

                        if (data.bypassBlur)
                        {
                            Composite(data.rawFoW);
                            return;
                        }

                        // --- Blur passes (all TextureHandle ↔ TextureHandle) ---
                        data.blurMat.SetFloat(BlurSizeId, data.blurSize);

                        // First H blur reads from rawFoW (imported TextureHandle)
                        cmd.Blit(data.rawFoW, data.tempA, data.blurMat, 0);   // H blur
                        cmd.Blit(data.tempA, data.tempB, data.blurMat, 1);     // V blur

                        for (int i = 1; i < data.blurIterations; i++)
                        {
                            cmd.Blit(data.tempB, data.tempA, data.blurMat, 0); // H blur
                            cmd.Blit(data.tempA, data.tempB, data.blurMat, 1); // V blur
                        }

                        // After blur: result is in tempB.

                        // --- Temporal blend pass ---
                        TextureHandle compositeSource;

                        if (data.temporalMat != null && data.hasPrevBlurred
                            && data.temporalBlend < 0.99f)
                        {
                            // _PrevTex is not in Properties — use cmd.SetGlobalTexture
                            // so it works with imported TextureHandle on all platforms.
                            cmd.SetGlobalTexture(PrevTexId, data.prevBlurred);
                            data.temporalMat.SetFloat(BlendFactorId, data.temporalBlend);
                            // tempB (current blurred) → tempA (blended with previous)
                            cmd.Blit(data.tempB, data.tempA, data.temporalMat, 0);
                            // Save blended result to persistent RT for next frame
                            cmd.Blit(data.tempA, data.prevBlurred);
                            compositeSource = data.tempA;
                        }
                        else
                        {
                            // No temporal — save current to persistent RT for next frame
                            if (data.hasPrevBlurred)
                                cmd.Blit(data.tempB, data.prevBlurred);
                            compositeSource = data.tempB;
                        }

                        // --- Composite pass ---
                        Composite(compositeSource);
                    });
                }
            }

            public void Dispose()
            {
                _rawRTHandle?.Release();
                _rawRTHandle = null;
                _cachedRawRT = null;

                _prevBlurredRTHandle?.Release();
                _prevBlurredRTHandle = null;
                _cachedPrevRT = null;
            }
        }
    }
}
