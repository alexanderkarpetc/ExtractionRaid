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

        [Tooltip("Enable procedural weapon sway that follows a body bone (Spine/Chest).")]
        public bool WeaponSwayEnabled = true;

        [Range(0f, 2f)]
        [Tooltip("Sway amount. 0 = weapon fully static, 1 = full bone delta, >1 = exaggerated.")]
        public float WeaponSwayWeight = 1f;

        [Range(0.05f, 2f)]
        [Tooltip("Time constant (sec) for EMA baseline. Smaller = sway decays faster; larger = more 'alive'.")]
        public float WeaponSwayAvgTime = 0.4f;
    }
}
