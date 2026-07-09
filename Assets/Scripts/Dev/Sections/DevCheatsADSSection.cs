using UnityEngine;

namespace Dev
{
    public class DevCheatsADSSection : ScriptableObject
    {
        public float AdsTransitionTime = 0.18f;
        public float AdsMoveSpeedMultiplier = 0.7f;
        public float AdsAimFollowMultiplier = 1.5f;
        public float AdsRecoilMultiplier = 0.6f;
        public float AdsRecoilRecoveryMultiplier = 1.5f;
        public float AdsZoomFactor = 0.85f;
        public float AdsCursorInfluenceMultiplier = 1.4f;
        public float AdsBaseGap = 8f;
        public float AdsBloomExtraGap = 15f;
        public float AdsVignetteIntensity = 0.55f;
        // Sniper-scope knobs moved to their own DevCheatsScopeSection (friendly grouped editor).
    }
}
