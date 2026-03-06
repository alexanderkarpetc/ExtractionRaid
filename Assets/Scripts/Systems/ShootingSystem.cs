using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class ShootingSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var weapon = player.EquippedWeapon;
            if (weapon == null) return;

            var input = context.Input;
            if (input == null) return;

            if (!input.AttackPressed) return;

            if (state.ElapsedTime - weapon.LastFireTime < weapon.FireInterval) return;

            var dir = player.AimDirection;

            if (dir.sqrMagnitude < 0.001f) return;

            var spawnPos = input.MuzzleWorldPoint;
            var count = Mathf.Max(1, weapon.ProjectilesPerShot);
            var halfSpread = weapon.SpreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var pelletDir = halfSpread > 0f
                    ? Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f) * dir
                    : dir;

                var projectileId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projectileId, spawnPos, pelletDir, weapon.ProjectileSpeed,
                    state.ElapsedTime, weapon.ProjectileLifetime,
                    weapon.ProjectileDamage);

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir);
            }

            weapon.LastFireTime = state.ElapsedTime;
        }
    }
}
