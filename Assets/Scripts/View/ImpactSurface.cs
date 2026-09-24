using UnityEngine;

namespace View
{
    public enum ImpactSurfaceType
    {
        Default = 0,
        Wood = 1,
        Concrete = 2,
        Metal = 3,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Raid/Impact Surface")]
    public sealed class ImpactSurface : MonoBehaviour
    {
        [Tooltip("Surface for bullet impacts. Applies to this object and child colliders; a closer Impact Surface overrides it. Default explicitly uses the generic surface.")]
        public ImpactSurfaceType Surface = ImpactSurfaceType.Default;

        public string SurfaceId => Surface switch
        {
            ImpactSurfaceType.Wood => "wood",
            ImpactSurfaceType.Concrete => "concrete",
            ImpactSurfaceType.Metal => "metal",
            _ => "surface",
        };
    }
}
