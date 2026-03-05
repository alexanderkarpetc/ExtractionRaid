using Session;
using State;
using UnityEngine;

namespace Managers
{
    public static class ShootingManager
    {
        public const float FireInterval = 0.2f;
        public const float ProjectileSpeed = 20f;
        public const float ProjectileLifetime = 3f;
        public const float MuzzleOffset = 0.5f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            if (!input.AttackPressed) return;

            if (state.ElapsedTime - player.Combat.LastFireTime < FireInterval) return;

            var aimPoint = input.AimWorldPoint;
            var origin = player.Position;
            var dir = new Vector3(aimPoint.x - origin.x, 0f, aimPoint.z - origin.z);

            if (dir.sqrMagnitude < 0.001f) return;

            dir.Normalize();

            var spawnPos = origin + dir * MuzzleOffset;
            spawnPos.y = origin.y;

            var projectileId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projectileId, spawnPos, dir, ProjectileSpeed,
                state.ElapsedTime, ProjectileLifetime);

            state.Projectiles.Add(projectile);
            player.Combat.LastFireTime = state.ElapsedTime;

            context.Events.ProjectileSpawned(projectileId, spawnPos, dir);
        }
    }
}
