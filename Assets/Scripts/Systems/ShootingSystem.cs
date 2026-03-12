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
            if (player.IsInGrenadeMode) return;

            var weapon = player.EquippedWeapon;
            if (weapon == null) return;

            var input = context.Input;
            if (input == null) return;

            if (!input.AttackPressed) return;

            if (weapon.Phase != WeaponPhase.Ready) return;

            // Ammo check: dry fire if magazine empty
            bool usesAmmo = !string.IsNullOrEmpty(weapon.AmmoType);
            if (usesAmmo && weapon.AmmoInMagazine <= 0)
            {
                context.Events.WeaponDryFired(weapon.PrefabId);
                if (AmmoSystem.CanReload(weapon, state.Inventory))
                {
                    weapon.Phase = WeaponPhase.Reloading;
                    weapon.PhaseStartTime = state.ElapsedTime;
                    context.Events.WeaponReloadStarted(weapon.PrefabId);
                }
                return;
            }

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
                    projectileId, player.Id, spawnPos, pelletDir, weapon.ProjectileSpeed,
                    state.ElapsedTime, weapon.ProjectileLifetime,
                    weapon.ProjectileDamage);

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir, weapon.ProjectileDamage);
            }

            context.Events.WeaponFired(spawnPos, dir);
            weapon.Phase = WeaponPhase.Firing;
            weapon.PhaseStartTime = state.ElapsedTime;
            weapon.LastFireTime = state.ElapsedTime;

            // Apply recoil — backward kick + sideways scatter
            if (weapon.RecoilKickBack > 0f || weapon.RecoilKickSide > 0f)
            {
                var aimDir = (player.WeaponAimPoint - player.Position).normalized;

                // Backward: pull toward player
                var backward = -aimDir * weapon.RecoilKickBack;

                // Sideways: perpendicular scatter
                var right = new Vector3(aimDir.z, 0f, -aimDir.x);
                float sideAmount = Random.Range(-weapon.RecoilKickSide, weapon.RecoilKickSide);
                var sideways = right * sideAmount;

                weapon.RecoilOffset += backward + sideways;
            }

            // Consume one round (shotgun: 1 shell = multiple pellets)
            if (usesAmmo)
            {
                weapon.AmmoInMagazine -= 1;
            }
        }
    }
}
