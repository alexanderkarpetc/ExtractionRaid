using UnityEngine;

namespace State
{
    public struct HitSignal
    {
        public EId ProjectileId;
        public EId TargetId;
        public float Damage;
        public float Penetration;
        public float ArmorDamage;
        public Vector3 HitPoint;
        public EId TargetedEntityId; // who was aimed at (may differ from TargetId)
    }
}
