using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace View
{
    public class NonXRayFeature : ScriptableRendererFeature
    {
        const int NonXRayRenderQueue = 2999; // Transparent-1: after x-ray, before normal transparents.

        [SerializeField] LayerMask nonXRayLayerMask = 1 << LayerUtils.NonXRay;

        public override void Create()
        {
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (((1 << r.gameObject.layer) & nonXRayLayerMask.value) == 0) continue;

                var materials = r.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].renderQueue != NonXRayRenderQueue)
                        materials[i].renderQueue = NonXRayRenderQueue;
                }
            }
        }
    }
}
