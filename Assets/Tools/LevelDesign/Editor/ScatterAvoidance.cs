using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    public static class ScatterAvoidance
    {
        public static bool Excluded(Vector3 point, IReadOnlyList<Collider> explicitColliders,
            PhysicsScene physics, int layerMask, Collider[] buffer)
        {
            foreach (var collider in explicitColliders)
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                    (collider.ClosestPoint(point) - point).sqrMagnitude < .0001f) return true;
            if (layerMask == 0) return false;
            int count = physics.OverlapSphere(point, .01f, buffer, layerMask, QueryTriggerInteraction.Collide);
            return count > 0;
        }
    }
}
