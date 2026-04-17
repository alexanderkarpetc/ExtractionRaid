using UnityEngine;

namespace Dev
{
    public class DevCheatsAimSection : ScriptableObject
    {
        public bool AimSplitEnabled;
        public float AimFollowMultiplier = 1f;

        [Range(0f, 5f)]
        [Tooltip("If cursor is closer than this to the player (in world XZ), aim is projected " +
                 "outward along the last valid direction. 0 = disabled. Prevents weapon flip when " +
                 "cursor hovers over the player silhouette.")]
        public float MinAimDistance = 1.5f;
    }
}
