using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace View
{
    public class InteractableOutlineFeature : ScriptableRendererFeature
    {
        [Header("Materials")]
        [SerializeField] Material maskMaterialOverride;
        [SerializeField] Material outlineMaterialOverride;

        [Header("Style")]
        [SerializeField] Color outlineColor = new(0.2f, 0.95f, 1f, 1f);
        [SerializeField, Range(1f, 8f)] float thicknessPixels = 3f;
        [SerializeField, Range(0f, 1f)] float opacity = 0.9f;

        InteractableOutlinePass _pass;
        Material _runtimeMaskMaterial;
        Material _runtimeOutlineMaterial;

        public override void Create()
        {
            EnsureMaterials();
            _pass = new InteractableOutlinePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            EnsureMaterials();
            var maskMaterial = maskMaterialOverride != null ? maskMaterialOverride : _runtimeMaskMaterial;
            var outlineMaterial = outlineMaterialOverride != null ? outlineMaterialOverride : _runtimeOutlineMaterial;
            if (maskMaterial == null || outlineMaterial == null) return;

            var entries = InteractableOutlineRegistry.GetSnapshot();
            if (entries.Length == 0) return;

            float maxEntryOpacity = 0f;
            for (int i = 0; i < entries.Length; i++)
                maxEntryOpacity = Mathf.Max(maxEntryOpacity, entries[i].Opacity);

            _pass.Setup(maskMaterial, outlineMaterial, outlineColor, thicknessPixels,
                opacity * maxEntryOpacity, entries);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            DestroyOwnedMaterial(_runtimeMaskMaterial);
            DestroyOwnedMaterial(_runtimeOutlineMaterial);
            _runtimeMaskMaterial = null;
            _runtimeOutlineMaterial = null;
        }

        static void DestroyOwnedMaterial(Material material)
        {
            if (material == null) return;

#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(material)) return;
            Object.DestroyImmediate(material);
#else
            Object.Destroy(material);
#endif
        }

        void EnsureMaterials()
        {
            if (maskMaterialOverride == null && _runtimeMaskMaterial == null)
            {
                var shader = Shader.Find("Hidden/ExtractionRaid/InteractableOutlineMask");
                if (shader != null)
                    _runtimeMaskMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            if (outlineMaterialOverride == null && _runtimeOutlineMaterial == null)
            {
                var shader = Shader.Find("Hidden/ExtractionRaid/InteractableOutlineComposite");
                if (shader != null)
                    _runtimeOutlineMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
        }

        class InteractableOutlinePass : ScriptableRenderPass
        {
            Material _maskMaterial;
            Material _outlineMaterial;
            Color _outlineColor;
            float _thicknessPixels;
            float _opacity;
            InteractableOutlineRegistry.Entry[] _entries;

            static readonly int OutlineMaskId = Shader.PropertyToID("_OutlineMask");
            static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
            static readonly int OpacityId = Shader.PropertyToID("_Opacity");

            public void Setup(Material maskMaterial, Material outlineMaterial, Color outlineColor,
                float thicknessPixels, float opacity, InteractableOutlineRegistry.Entry[] entries)
            {
                _maskMaterial = maskMaterial;
                _outlineMaterial = outlineMaterial;
                _outlineColor = outlineColor;
                _thicknessPixels = thicknessPixels;
                _opacity = opacity;
                _entries = entries;
            }

            class MaskPassData
            {
                public InteractableOutlineRegistry.Entry[] entries;
                public Material material;
            }

            class CompositePassData
            {
                public Material material;
                public Color outlineColor;
                public float thicknessPixels;
                public float opacity;
                public TextureHandle cameraColor;
                public TextureHandle mask;
                public TextureHandle temp;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.name = "_InteractableOutlineMask";
                desc.depthBufferBits = 0;
                desc.colorFormat = GraphicsFormat.R8_UNorm;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                var mask = renderGraph.CreateTexture(desc);

                desc.name = "_InteractableOutlineTemp";
                desc.colorFormat = renderGraph.GetTextureDesc(resourceData.activeColorTexture).colorFormat;
                var temp = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Interactable Outline Mask", out var passData))
                {
                    passData.entries = _entries;
                    passData.material = _maskMaterial;
                    builder.SetRenderAttachment(mask, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1, 0);

                        if (data.entries == null || data.material == null) return;

                        for (int i = 0; i < data.entries.Length; i++)
                        {
                            var entry = data.entries[i];
                            var renderer = entry.Renderer;
                            if (renderer == null) continue;

                            int subMeshCount = renderer.sharedMaterials != null
                                ? renderer.sharedMaterials.Length
                                : 1;
                            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                                context.cmd.DrawRenderer(renderer, data.material, subMesh, 0);
                        }
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Interactable Outline Composite", out var passData))
                {
                    passData.material = _outlineMaterial;
                    passData.outlineColor = _outlineColor;
                    passData.thicknessPixels = _thicknessPixels;
                    passData.opacity = _opacity;
                    passData.cameraColor = resourceData.activeColorTexture;
                    passData.mask = mask;
                    passData.temp = temp;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseTexture(temp, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositePassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalTexture(OutlineMaskId, data.mask);
                        data.material.SetColor(OutlineColorId, data.outlineColor);
                        data.material.SetFloat(ThicknessId, data.thicknessPixels);
                        data.material.SetFloat(OpacityId, data.opacity);
                        cmd.Blit(data.cameraColor, data.temp, data.material, 0);
                        cmd.Blit(data.temp, data.cameraColor);
                    });
                }
            }

            public void Dispose()
            {
            }
        }
    }
}
