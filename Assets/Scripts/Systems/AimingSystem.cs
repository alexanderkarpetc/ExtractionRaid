using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class AimingSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var weapon = player.EquippedWeapon;
            if (weapon == null) return;

            var input = context.Input;
            if (input == null) return;

            var aimPoint = input.AimWorldPoint;
            var origin = player.Position;
            var dir = new Vector3(aimPoint.x - origin.x, 0f, aimPoint.z - origin.z);

            if (dir.sqrMagnitude < 0.001f) return;

            var aimDir = dir.normalized;

            player.AimDirection = aimDir;

            var currentFacing = player.FacingDirection;
            if (currentFacing.sqrMagnitude < 0.001f)
            {
                player.FacingDirection = aimDir;
                return;
            }

            var angle = Vector3.Angle(currentFacing, aimDir);

            var t = angle / weapon.ConeHalfAngle;
            var speed = weapon.BodyRotationSpeed * t;
            var maxStep = speed * context.DeltaTime * Mathf.Deg2Rad;
            player.FacingDirection = Vector3.RotateTowards(
                currentFacing, aimDir, maxStep, 0f).normalized;
        }
    }
}
