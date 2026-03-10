using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class AimingSystem
    {
        public const float UnarmedConeHalfAngle = 60f;
        public const float UnarmedBodyRotationSpeed = 360f;
        public const float UnarmedAimFollowSharpness = 30f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var aimPoint = input.AimWorldPoint;

            // 1. Raw aim — instant from mouse
            player.RawAimPoint = aimPoint;

            var origin = player.Position;
            var toRaw = new Vector3(aimPoint.x - origin.x, 0f, aimPoint.z - origin.z);

            if (toRaw.sqrMagnitude < 0.001f) return;

            float rawDist = toRaw.magnitude;
            var rawDir = toRaw / rawDist;

            // 2. Weapon aim — position-based exponential smoothing
            var weapon = player.EquippedWeapon;
            float aimFollowSharpness = weapon != null ? weapon.AimFollowSharpness : UnarmedAimFollowSharpness;

            float smoothFactor = 1f - Mathf.Exp(-aimFollowSharpness * context.DeltaTime);
            player.WeaponAimPoint = Vector3.Lerp(player.WeaponAimPoint, aimPoint, smoothFactor);

            // 3. AimDirection derived from weapon aim
            var weaponAimDir = player.WeaponAimPoint - origin;
            weaponAimDir.y = 0f;
            player.AimDirection = weaponAimDir.sqrMagnitude > 0.001f
                ? weaponAimDir.normalized
                : rawDir;

            // 4. FacingDirection — follows raw aim (body faces player intent)
            var coneHalfAngle = weapon != null ? weapon.ConeHalfAngle : UnarmedConeHalfAngle;
            var bodyRotationSpeed = weapon != null ? weapon.BodyRotationSpeed : UnarmedBodyRotationSpeed;

            var currentFacing = player.FacingDirection;
            if (currentFacing.sqrMagnitude < 0.001f)
            {
                player.FacingDirection = rawDir;
                return;
            }

            var bodyAngle = Vector3.Angle(currentFacing, rawDir);

            var t = bodyAngle / coneHalfAngle;
            var speed = bodyRotationSpeed * t;
            var maxStep = speed * context.DeltaTime * Mathf.Deg2Rad;
            player.FacingDirection = Vector3.RotateTowards(
                currentFacing, rawDir, maxStep, 0f).normalized;
        }
    }
}
