using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class AimingSystem
    {
        public const float UnarmedConeHalfAngle = 60f;
        public const float UnarmedBodyRotationSpeed = 360f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var aimPoint = input.AimWorldPoint;
            var origin = player.Position;
            var dir = new Vector3(aimPoint.x - origin.x, 0f, aimPoint.z - origin.z);

            if (dir.sqrMagnitude < 0.001f) return;

            var aimDir = dir.normalized;

            player.AimDirection = aimDir;

            var weapon = player.EquippedWeapon;
            var coneHalfAngle = weapon != null ? weapon.ConeHalfAngle : UnarmedConeHalfAngle;
            var bodyRotationSpeed = weapon != null ? weapon.BodyRotationSpeed : UnarmedBodyRotationSpeed;

            var currentFacing = player.FacingDirection;
            if (currentFacing.sqrMagnitude < 0.001f)
            {
                player.FacingDirection = aimDir;
                return;
            }

            var angle = Vector3.Angle(currentFacing, aimDir);

            var t = angle / coneHalfAngle;
            var speed = bodyRotationSpeed * t;
            var maxStep = speed * context.DeltaTime * Mathf.Deg2Rad;
            player.FacingDirection = Vector3.RotateTowards(
                currentFacing, aimDir, maxStep, 0f).normalized;
        }
    }
}
