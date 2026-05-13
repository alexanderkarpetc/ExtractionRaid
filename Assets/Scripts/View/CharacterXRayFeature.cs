using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace View
{
    public class CharacterXRayFeature : ScriptableRendererFeature
    {
        [SerializeField] LayerMask characterLayerMask = ~0;

        CharacterXRayPass _xRayPass;

        public override void Create()
        {
            _xRayPass = new CharacterXRayPass(characterLayerMask, "VibeXRay", "Character X-Ray")
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            _xRayPass.Setup(characterLayerMask);
            renderer.EnqueuePass(_xRayPass);
        }

        class CharacterXRayPass : ScriptableRenderPass
        {
            readonly List<ShaderTagId> _shaderTagIds = new();
            readonly string _shaderPassName;
            readonly string _renderGraphPassName;
            LayerMask _layerMask;

            public CharacterXRayPass(LayerMask layerMask, string shaderPassName, string renderGraphPassName)
            {
                _shaderPassName = shaderPassName;
                _renderGraphPassName = renderGraphPassName;
                Setup(layerMask);
            }

            public void Setup(LayerMask layerMask)
            {
                _layerMask = layerMask;
            }

            class PassData
            {
                public RendererListHandle rendererList;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var universalData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                _shaderTagIds.Clear();
                _shaderTagIds.Add(new ShaderTagId(_shaderPassName));

                var sorting = SortingCriteria.CommonTransparent;
                var drawingSettings = RenderingUtils.CreateDrawingSettings(
                    _shaderTagIds, universalData, cameraData, lightData, sorting);
                var filteringSettings = new FilteringSettings(RenderQueueRange.transparent, _layerMask);
                var rendererListParams = new RendererListParams(
                    universalData.cullResults, drawingSettings, filteringSettings);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(_renderGraphPassName, out var passData))
                {
                    passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
                    if (!passData.rendererList.IsValid()) return;

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }
            }
        }
    }
}
