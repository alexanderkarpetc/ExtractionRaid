using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class ShootingSystem
    {
        public const float FireInterval = 0.2f;
        public const float ProjectileSpeed = 20f;
        public const float ProjectileLifetime = 3f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            if (!input.AttackPressed) return;

            if (state.ElapsedTime - player.Combat.LastFireTime < FireInterval) return;

            var dir = player.FacingDirection;

            if (dir.sqrMagnitude < 0.001f) return;

            var spawnPos = input.MuzzleWorldPoint;

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
