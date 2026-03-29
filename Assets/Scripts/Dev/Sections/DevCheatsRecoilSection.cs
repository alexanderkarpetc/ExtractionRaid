using UnityEngine;

namespace Dev
{
    public class DevCheatsRecoilSection : ScriptableObject
    {
        public bool NoRecoil;
        public float RecoilMultiplier = .5f;
        public float RecoilForwardMultiplier = 1f;
        public float RecoilSideMultiplier = 1f;
        public float RecoilRecoveryMultiplier = 1f;
    }
}
