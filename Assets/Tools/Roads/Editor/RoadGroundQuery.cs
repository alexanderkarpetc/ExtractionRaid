using System;
using ExtractionRaid.View.Authoring;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads
{
    public sealed class RoadGroundQuery
    {
        readonly SplatRoad road;
        readonly RaycastHit[] hits = new RaycastHit[64];

        public RoadGroundQuery(SplatRoad road) => this.road = road;

        public bool Project(Vector3 point, out Vector3 projected)
        {
            bool found = Raycast(new Ray(point + Vector3.up * road.rayHeight, Vector3.down), road.rayDistance, out RaycastHit hit);
            projected = found ? hit.point : point;
            return found;
        }

        public bool Raycast(Ray ray, float distance, out RaycastHit result)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, distance, road.groundLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) throw new InvalidOperationException("Too many ground colliders along the ray. Narrow Ground Layers.");
            result = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (!collider || collider.GetComponentInParent<SplatRoad>()) continue;
                if ((collider.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0) continue;
                if (hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                result = hits[i];
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
