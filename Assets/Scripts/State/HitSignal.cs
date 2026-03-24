using UnityEngine;

namespace State
{
    public struct HitSignal
    {
        public EId ProjectileId;
        public EId TargetId;
        public float Damage;
        public Vector3 HitPoint;
    }
}
