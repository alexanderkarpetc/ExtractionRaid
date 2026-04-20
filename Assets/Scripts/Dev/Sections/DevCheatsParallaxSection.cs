using UnityEngine;

namespace Dev
{
    public class DevCheatsParallaxSection : ScriptableObject
    {
        public float ProjectileSpawnHeight = 0.3f;
        public bool ParallaxCorrection = true;
        public bool WeaponPivotParallaxCorrection = true;
        public float ConvergenceBlend = 0.3f;
        public bool ConvergenceAimUp = true;
        public float AimUpHeightRatio = 0.85f;
        public float ProjectileHitRadius = 0.15f;
    }
}
