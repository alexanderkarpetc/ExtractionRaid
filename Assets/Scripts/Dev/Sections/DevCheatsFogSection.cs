using UnityEngine;

namespace Dev
{
    public class DevCheatsFogSection : ScriptableObject
    {
        public bool FogOfWarEnabled = true;
        public float FogBlurRadius = 10f;
        public int FogBlurIterations = 3;
        public float FogIntensity = 0.6f;
        public float FogDesaturation;
        public Color FogColor = new(0.02f, 0.02f, 0.05f, 1f);
        public int FoWRTScale = 256;
        public float FOVRayStep = 2f;
        public float FogTemporalBlend = 0.2f;
    }
}
