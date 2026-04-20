using UnityEngine;

namespace Dev
{
    public class DevCheatsPlayerSection : ScriptableObject
    {
        public float MoveSpeedMultiplier = 1f;

        [Tooltip("Damp time (seconds) for locomotion blend params (SpeedX/SpeedY). Higher = smoother but more input lag.")]
        public float LocomotionBlendDampTime = 0.12f;
    }
}
