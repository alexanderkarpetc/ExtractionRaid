using System.Collections.Generic;
using Constants;
using UnityEngine;

namespace View.FogOfWar
{
    /// <summary>
    /// Runtime ray sweep that produces a continuous visibility polygon.
    /// Two-pass: coarse sweep → edge-finding binary search at shadow boundaries.
    /// endpoints[0] = center (playerPos), endpoints[1..N] = perimeter.
    /// </summary>
    public static class FOVRaySweep
    {
        static readonly List<Vector3> Endpoints = new(512);

        const float FineEdgeMargin = 3f;
        const float EdgeThreshold = 0.5f; // distance diff to trigger edge-finding
        const int BinarySearchIterations = 4;

        public struct RawRay
        {
            public float Angle;
            public float Dist;
            public float MaxDist;
            public bool Hit;
        }

        static readonly List<RawRay> RawRays = new(256);

        /// <summary>
        /// Cached copy of the last sweep's raw rays (for gizmo visualization).
        /// Updated every Sweep() call.
        /// </summary>
        public static readonly List<RawRay> LastRawRays = new(256);

        public static List<Vector3> Sweep(Vector3 playerPos, Vector3 forward,
            float nearRadius, float farRadius, float fovAngle,
            float rayStep, int layerMask, Collider[] collidersToDisable)
        {
            Endpoints.Clear();
            Endpoints.Add(playerPos); // fan center

            float halfFOV = fovAngle * 0.5f;
            var rayOrigin = playerPos + Vector3.up * BotConstants.PlayerEyeHeight;

            // Disable player colliders so rays don't hit self
            for (int i = 0; i < collidersToDisable.Length; i++)
                collidersToDisable[i].enabled = false;

            try
            {
                // ── Pass 1: Coarse sweep ──────────────────────────
                RawRays.Clear();
                float fineStep = Mathf.Max(rayStep * 0.5f, 0.25f);
                float angle = -180f;
                while (angle <= 180f)
                {
                    float absAngle = Mathf.Abs(angle);
                    float maxDist = absAngle <= halfFOV ? farRadius : nearRadius;

                    var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                    float dist = maxDist;
                    bool hit = Physics.Raycast(rayOrigin, dir, out var hitInfo, maxDist, layerMask);
                    if (hit) dist = hitInfo.distance;

                    RawRays.Add(new RawRay { Angle = angle, Dist = dist, MaxDist = maxDist, Hit = hit });

                    bool nearFovEdge = Mathf.Abs(absAngle - halfFOV) < FineEdgeMargin;
                    angle += nearFovEdge ? fineStep : rayStep;
                }

                // ── Pass 2: Build endpoints with edge-finding ─────
                for (int i = 0; i < RawRays.Count; i++)
                {
                    var ray = RawRays[i];
                    AddRayEndpoint(playerPos, forward, ray);

                    // Edge-finding between consecutive rays with large distance jump
                    if (i < RawRays.Count - 1)
                    {
                        var next = RawRays[i + 1];
                        if (Mathf.Abs(ray.Dist - next.Dist) > EdgeThreshold)
                        {
                            BinarySearchEdge(playerPos, forward, rayOrigin, layerMask,
                                halfFOV, nearRadius, farRadius, ray, next);
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < collidersToDisable.Length; i++)
                    collidersToDisable[i].enabled = true;
            }

            // Cache for gizmo visualization
            LastRawRays.Clear();
            LastRawRays.AddRange(RawRays);

            return Endpoints;
        }

        static void AddRayEndpoint(Vector3 playerPos, Vector3 forward, in RawRay ray)
        {
            var dir = Quaternion.Euler(0f, ray.Angle, 0f) * forward;
            Endpoints.Add(playerPos + dir * ray.Dist);
        }

        static void BinarySearchEdge(Vector3 playerPos, Vector3 forward, Vector3 rayOrigin,
            int layerMask, float halfFOV, float nearRadius, float farRadius,
            RawRay rayA, RawRay rayB)
        {
            float lo = rayA.Angle;
            float hi = rayB.Angle;
            float loDistFromA = 0f; // tracks which "side" lo is on
            float hiDistFromA = Mathf.Abs(rayB.Dist - rayA.Dist);

            for (int iter = 0; iter < BinarySearchIterations; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                float absMid = Mathf.Abs(mid);
                float maxDist = absMid <= halfFOV ? farRadius : nearRadius;

                var dir = Quaternion.Euler(0f, mid, 0f) * forward;
                float dist = maxDist;
                if (Physics.Raycast(rayOrigin, dir, out var hit, maxDist, layerMask))
                    dist = hit.distance;

                float distFromA = Mathf.Abs(dist - rayA.Dist);
                float distFromB = Mathf.Abs(dist - rayB.Dist);

                if (distFromA < distFromB)
                {
                    lo = mid;
                    loDistFromA = distFromA;
                }
                else
                {
                    hi = mid;
                    hiDistFromA = distFromA;
                }
            }

            // Add both sides of the edge for a sharp silhouette
            float absLo = Mathf.Abs(lo);
            float maxDistLo = absLo <= halfFOV ? farRadius : nearRadius;
            var dirLo = Quaternion.Euler(0f, lo, 0f) * forward;
            float distLo = maxDistLo;
            if (Physics.Raycast(rayOrigin, dirLo, out var hitLo, maxDistLo, layerMask))
                distLo = hitLo.distance;
            Endpoints.Add(playerPos + dirLo * distLo);

            float absHi = Mathf.Abs(hi);
            float maxDistHi = absHi <= halfFOV ? farRadius : nearRadius;
            var dirHi = Quaternion.Euler(0f, hi, 0f) * forward;
            float distHi = maxDistHi;
            if (Physics.Raycast(rayOrigin, dirHi, out var hitHi, maxDistHi, layerMask))
                distHi = hitHi.distance;
            Endpoints.Add(playerPos + dirHi * distHi);
        }
    }
}
