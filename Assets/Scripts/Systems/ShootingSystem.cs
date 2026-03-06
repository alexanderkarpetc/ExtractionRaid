using Session;
using State;

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

            var projectileId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projectileId, spawnPos, dir, weapon.ProjectileSpeed,
                state.ElapsedTime, weapon.ProjectileLifetime,
                weapon.ProjectileDamage);

            state.Projectiles.Add(projectile);
            weapon.LastFireTime = state.ElapsedTime;

            context.Events.ProjectileSpawned(projectileId, spawnPos, dir);
        }
    }
}
