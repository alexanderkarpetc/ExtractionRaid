using UnityEngine;

namespace Dev
{
    public class DevCheatsPlayerSection : ScriptableObject
    {
        public float MoveSpeedMultiplier = 1f;

        [Tooltip("Damp time (seconds) for locomotion blend params (SpeedX/SpeedY). Higher = smoother but more input lag.")]
        public float LocomotionBlendDampTime = 0.12f;

        [Tooltip("Enable right-hand IK (attach hand to weapon's RightHandGrip transform).")]
        public bool HandIKEnabled = true;

        [Range(0f, 1f)]
        [Tooltip("Global IK weight multiplier. 0 = off, 1 = full attach.")]
        public float HandIKWeight = 1f;
    }
}
