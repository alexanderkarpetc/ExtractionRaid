using UnityEngine;

namespace Dev
{
    public class DevCheatsFOVSection : ScriptableObject
    {
        public bool FOVEnabled = true;
        public float FOVNearRadius = 6f;
        public float FOVFarRadius = 25f;
        public float FOVAngle = 130f;
        public bool ForceShowAllBots;
        public bool FOVOcclusionEnabled = true;
    }
}
