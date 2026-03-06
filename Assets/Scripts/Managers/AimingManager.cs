using Session;
using State;
using UnityEngine;

namespace Managers
{
    public static class AimingManager
    {
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

            player.FacingDirection = dir.normalized;
        }
    }
}
