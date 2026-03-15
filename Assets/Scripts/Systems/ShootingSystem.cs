using Dev;
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

            var spawnPos = input.MuzzleWorldPoint;

            // Parallax correction: find where camera ray through crosshair
            // intersects the muzzle-height plane (so bullets visually pass through crosshair)
            var camPos = input.CameraWorldPosition;
            var groundAim = player.WeaponAimPoint;
            Vector3 correctedAim;
            if (camPos.y > 0.01f && spawnPos.y > 0.01f)
            {
                float ratio = spawnPos.y / camPos.y;
                correctedAim = Vector3.Lerp(groundAim, camPos, ratio);
            }
            else
            {
                correctedAim = groundAim;
            }

            var toAim = correctedAim - spawnPos;
            toAim.y = 0f;
            var dir = toAim.sqrMagnitude > 0.001f
                ? toAim.normalized
                : player.AimDirection;

            if (dir.sqrMagnitude < 0.001f) return;
            var count = Mathf.Max(1, weapon.ProjectilesPerShot);
            var halfSpread = weapon.SpreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var pelletDir = halfSpread > 0f
                    ? Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f) * dir
                    : dir;

                var projectileId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projectileId, player.Id, spawnPos, pelletDir,
                    weapon.ProjectileSpeed * DevCheats.ProjectileSpeedMultiplier,
                    state.ElapsedTime, weapon.ProjectileLifetime,
                    weapon.ProjectileDamage * DevCheats.DamageMultiplier);

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir, weapon.ProjectileDamage);
            }

            context.Events.WeaponFired(spawnPos, dir);
            weapon.Phase = WeaponPhase.Firing;
            weapon.PhaseStartTime = state.ElapsedTime;
            weapon.LastFireTime = state.ElapsedTime;

            // Apply recoil — forward kick + sideways scatter
            if (!DevCheats.NoRecoil
                && (weapon.RecoilKickForward > 0f || weapon.RecoilKickSide > 0f))
            {
                float recoilMul = DevCheats.RecoilMultiplier;
                var aimDir = (player.WeaponAimPoint - player.Position).normalized;

                // Forward: push WeaponAimPoint directly (AimFollowSharpness handles recovery)
                player.WeaponAimPoint += aimDir * (weapon.RecoilKickForward * recoilMul);

                // Sideways: through RecoilOffset (RecoilRecoverySpeed handles recovery)
                var right = new Vector3(aimDir.z, 0f, -aimDir.x);
                float sideAmount = Random.Range(-weapon.RecoilKickSide, weapon.RecoilKickSide);
                weapon.RecoilOffset += right * (sideAmount * recoilMul);
            }

            // Consume one round (shotgun: 1 shell = multiple pellets)
            if (usesAmmo && !DevCheats.InfiniteAmmo)
            {
                weapon.AmmoInMagazine -= 1;
            }
        }
    }
}
