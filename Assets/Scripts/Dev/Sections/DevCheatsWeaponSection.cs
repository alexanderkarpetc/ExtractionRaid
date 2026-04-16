using UnityEngine;

namespace Dev
{
    public class DevCheatsWeaponSection : ScriptableObject
    {
        [Header("Damage & Speed")]
        public float DamageMultiplier = 1f;
        public float ProjectileSpeedMultiplier = 1f;
        public float FireRateMultiplier = 1f;

        [Header("Muzzle Block (Solution 2)")]
        [Tooltip("Raycast from player chest to muzzle before spawning a bullet. " +
                 "If a wall is hit, clamp projectile spawn to the player-side of the wall.")]
        public bool MuzzleBlockEnabled = true;

        [Range(0.05f, 0.3f)]
        [Tooltip("How far back from the wall hit point the projectile spawns.")]
        public float MuzzleBlockBackoff = 0.1f;

        [Header("Weapon Pullback (Solution 3a)")]
        [Tooltip("Smoothly pull WeaponPivot toward the body when a wall is within weapon length. " +
                 "Muzzle point moves back with the pivot — muzzle flash and projectile origin follow.")]
        public bool WeaponPullbackEnabled = true;

        [Range(0.5f, 4.0f)]
        [Tooltip("Forward detection distance along WeaponPivot.forward.")]
        public float WeaponLength = 1.2f;

        [Range(0f, 1f)]
        [Tooltip("Max local-Z pullback when a wall is flush with the muzzle.")]
        public float WeaponPullbackAmount = 0.4f;

        [Range(1f, 30f)]
        [Tooltip("Lerp sharpness for retract and recovery (higher = snappier).")]
        public float WeaponPullbackSpeed = 12f;

        [Range(0f, 0.2f)]
        [Tooltip("SphereCast radius for detecting walls. Small value (~0.05) matches barrel width, " +
                 "and reliably registers overlap when the pivot is already inside a wall.")]
        public float WeaponPullbackRadius = 0.05f;

        [Tooltip("Draw the pullback ray and hit point in Scene view (Selected Gizmos). Debug only.")]
        public bool WeaponPullbackDebugGizmos = false;

        [Header("Bot Pullback Throttle / LOD")]
        [Range(1f, 30f)]
        [Tooltip("Physics-check rate (Hz) for bots. Player always checks every frame; " +
                 "bots only run SphereCast at this rate. Lerp still runs every frame for smoothness.")]
        public float BotPullbackCheckRateHz = 12f;

        [Range(5f, 100f)]
        [Tooltip("Bots farther than this from the main camera skip pullback entirely (retracts to rest).")]
        public float BotPullbackLodDistance = 25f;
    }
}
