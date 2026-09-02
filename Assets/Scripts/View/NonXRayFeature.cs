using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace View
{
    /// <summary>
    /// Owns the authored NonXRay layer mask (serialized into PC_Renderer /
    /// Mobile_Renderer) and drives <see cref="NonXRayRenderQueue"/>.
    ///
    /// The feature enqueues no pass — it only needs a per-scene hook, and the render
    /// callback is the earliest one guaranteed to run before anything is drawn. The
    /// actual work is guarded behind a scene-applied flag, so the per-frame, per-camera
    /// cost here is a bool check. It used to be a full scene renderer scan.
    /// </summary>
    public class NonXRayFeature : ScriptableRendererFeature
    {
        [SerializeField] LayerMask nonXRayLayerMask = 1 << LayerUtils.NonXRay;

        public override void Create()
        {
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            NonXRayRenderQueue.EnsureSceneApplied(nonXRayLayerMask);
        }
    }
}
