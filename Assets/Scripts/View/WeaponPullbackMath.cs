namespace View
{
    /// <summary>
    /// Pure functions for weapon pullback math. Extracted from CharacterBody
    /// for unit-testability — no Unity physics here, just arithmetic.
    /// </summary>
    public static class WeaponPullbackMath
    {
        /// <summary>
        /// Compute the retract fraction (0..1) given the nearest wall hit distance
        /// from the SphereCast origin and the pivot's position along the ray.
        /// </summary>
        /// <param name="closestCastDistance">
        ///     Distance from cast origin to the first non-ignored hit. Pass
        ///     <see cref="float.PositiveInfinity"/> if no hit.
        /// </param>
        /// <param name="pivotDistFromOrigin">
        ///     Scalar distance from cast origin to the weapon pivot, projected onto
        ///     the ray direction. Used as the reference point for retract math.
        /// </param>
        /// <param name="weaponLength">
        ///     Max forward detection distance (measured from the pivot along the ray).
        /// </param>
        /// <returns>
        ///     0 when wall is out of range (no retract).
        ///     1 when wall is at or behind the pivot (full retract).
        ///     Linear in between: 1 - distFromPivot/weaponLength.
        /// </returns>
        public static float ComputeRetract(float closestCastDistance, float pivotDistFromOrigin, float weaponLength)
        {
            if (float.IsPositiveInfinity(closestCastDistance)) return 0f;
            if (weaponLength <= 0f) return 0f;

            float distFromPivot = closestCastDistance - pivotDistFromOrigin;
            if (distFromPivot <= 0f) return 1f;
            if (distFromPivot >= weaponLength) return 0f;
            return 1f - (distFromPivot / weaponLength);
        }
    }
}
