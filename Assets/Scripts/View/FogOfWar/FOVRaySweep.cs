using System.Collections.Generic;
using Constants;
using Dev;
using UnityEngine;

namespace View.FogOfWar
{
    /// <summary>
    /// Runtime ray sweep that produces a continuous visibility polygon.
    /// Unified sweep: 360° with varying maxDist (nearRadius outside FOV cone, farRadius inside).
    /// endpoints[0] = center (playerPos), endpoints[1..N] = perimeter.
    /// </summary>
    public static class FOVRaySweep
    {
        static readonly List<Vector3> Endpoints = new(256);

        const float FineEdgeMargin = 3f; // degrees of fine step around FOV edges

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
                float fineStep = Mathf.Max(rayStep * 0.5f, 0.25f);
                float angle = -180f;
                while (angle <= 180f)
                {
                    float absAngle = Mathf.Abs(angle);

                    // Determine max distance: inside FOV cone → far, outside → near
                    float maxDist = absAngle <= halfFOV ? farRadius : nearRadius;

                    // Raycast
                    var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                    float dist = maxDist;
                    if (Physics.Raycast(rayOrigin, dir, out var hit, maxDist, layerMask))
                        dist = hit.distance;

                    Endpoints.Add(playerPos + dir * dist);

                    bool nearEdge = Mathf.Abs(absAngle - halfFOV) < FineEdgeMargin;
                    angle += nearEdge ? fineStep : rayStep;
                }
            }
            finally
            {
                // Re-enable colliders even if an exception occurs
                for (int i = 0; i < collidersToDisable.Length; i++)
                    collidersToDisable[i].enabled = true;
            }

            return Endpoints;
        }
    }
}
