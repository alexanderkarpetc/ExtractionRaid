using UnityEngine;

namespace Constants
{
    public static class GrenadeConstants
    {
        public const float FuseTime = 3.5f;
        public const float UpwardAngle = 30f;
        public const float LaunchHeight = 1.2f;
        public const float MaxThrowRange = 20f;
        public const float MinThrowRange = 1.5f;
        public const float Damage = 120f;
        public const float ExplosionRadius = 5f;
        public const int StartingCount = 30;

        public const float TrajectoryTimeStep = 0.05f;
        public const float MaxTrajectoryTime = 3f;
        public const int MaxTrajectorySegments = 60;

        /// <summary>
        /// Computes the launch speed needed to hit a target at the given horizontal
        /// distance, using a fixed launch angle and height offset above the target.
        /// </summary>
        public static float ComputeThrowSpeed(float horizontalDistance, float gravity)
        {
            if (horizontalDistance < 0.1f) return 0f;

            float rad = UpwardAngle * Mathf.Deg2Rad;
            float cosA = Mathf.Cos(rad);
            float tanA = Mathf.Tan(rad);
            float denom = LaunchHeight + horizontalDistance * tanA;
            if (denom <= 0.01f) return 0f;

            float vSq = gravity * horizontalDistance * horizontalDistance /
                         (2f * cosA * cosA * denom);
            return Mathf.Sqrt(Mathf.Max(0f, vSq));
        }
    }
}
