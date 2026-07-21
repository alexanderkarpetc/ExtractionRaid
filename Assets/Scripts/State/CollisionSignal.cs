using UnityEngine;

namespace State
{
    public struct CollisionSignal
    {
        public EId ProjectileId;
        public Vector3 Position;
        public Vector3 Normal;  // surface normal at impact (used for decal orientation)
        public string SurfaceType;
    }
}
