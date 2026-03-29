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
            if (player.AreHandsBusy) return;
            if (player.IsInMenu) return;

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

            var cfg = context.ShootingConfig;

            var spawnPos = input.MuzzleWorldPoint;
            // Lower projectile to near-ground level — drastically reduces camera parallax
            spawnPos.y = cfg.ProjectileSpawnHeight;

            // --- Compute two independent directions, then blend ---
            var groundAim = player.WeaponAimPoint;
            var convergence = input.ConvergencePoint;

            // 1. Parallax-corrected direction (visual: trail through crosshair)
            var toAimParallax = new Vector3(groundAim.x - spawnPos.x, 0f, groundAim.z - spawnPos.z);
            if (cfg.ParallaxCorrection && spawnPos.y > 0.01f)
            {
                var camPos = input.CameraWorldPosition;
                if (camPos.y > 0.1f)
                {
                    float ratio = spawnPos.y / camPos.y;
                    var corrected = Vector3.Lerp(groundAim, camPos, ratio);
                    toAimParallax = new Vector3(corrected.x - spawnPos.x, 0f, corrected.z - spawnPos.z);
                }
            }

            // 2. Convergence direction (accuracy: toward actual 3D target)
            var toAimConv = toAimParallax; // fallback = parallax
            float blend = 0f;
            if (convergence.HasValue && cfg.ConvergenceBlend > 0f)
            {
                var convXZ = new Vector3(convergence.Value.x, 0f, convergence.Value.z);
                toAimConv = new Vector3(convXZ.x - spawnPos.x, 0f, convXZ.z - spawnPos.z);
                blend = cfg.ConvergenceBlend;
            }

            // 3. Blend: 0 = full parallax (visual), 1 = full convergence (accuracy)
            var toAim = Vector3.Lerp(toAimParallax, toAimConv, blend);

            var dir = toAim.sqrMagnitude > 0.001f
                ? toAim.normalized
                : player.AimDirection;

            // Determine if convergence hit a character (for targeted shots + AimUp)
            var targetedEntityId = default(EId);
            var hitCollider = convergence.HasValue ? input.ConvergenceCollider : null;
            var targetDamageable = hitCollider != null
                ? hitCollider.GetComponentInParent<View.IDamageableView>()
                : null;

            if (targetDamageable != null)
                targetedEntityId = targetDamageable.EId;

            // When convergence hit a CHARACTER and AimUp is enabled,
            // angle the bullet slightly upward so it intersects the upper body of the target.
            if (cfg.ConvergenceAimUp && targetDamageable != null && hitCollider != null)
            {
                var bounds = hitCollider.bounds;
                float aimY = Mathf.Lerp(bounds.min.y, bounds.max.y, cfg.AimUpHeightRatio);
                float dy = aimY - spawnPos.y;
                dir = new Vector3(dir.x, dy / toAim.magnitude, dir.z).normalized;
            }

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
                    weapon.ProjectileSpeed * cfg.ProjectileSpeedMultiplier,
                    state.ElapsedTime, weapon.ProjectileLifetime,
                    weapon.ProjectileDamage * cfg.DamageMultiplier,
                    weapon.HeadshotDamageMultiplier,
                    targetedEntityId);

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir, weapon.ProjectileDamage);
            }

            context.Events.WeaponFired(spawnPos, dir);
            weapon.Phase = WeaponPhase.Firing;
            weapon.PhaseStartTime = state.ElapsedTime;
            weapon.LastFireTime = state.ElapsedTime;

            // Apply recoil — forward kick + sideways scatter
            // Both go through RecoilOffset so they survive smoothing and decay via RecoilRecoverySpeed
            if (!cfg.NoRecoil
                && (weapon.RecoilKickForward > 0f || weapon.RecoilKickSide > 0f))
            {
                float adsRecoilScale = Mathf.Lerp(1f, cfg.AdsRecoilMultiplier, player.AdsBlend);
                float recoilMul = cfg.RecoilMultiplier * adsRecoilScale;
                var aimDir = (player.WeaponAimPoint - player.Position).normalized;

                // Forward kick through RecoilOffset
                weapon.RecoilOffset += aimDir * (weapon.RecoilKickForward * recoilMul * cfg.RecoilForwardMultiplier);

                // Sideways scatter through RecoilOffset
                var right = new Vector3(aimDir.z, 0f, -aimDir.x);
                float sideAmount = Random.Range(-weapon.RecoilKickSide, weapon.RecoilKickSide);
                weapon.RecoilOffset += right * (sideAmount * recoilMul * cfg.RecoilSideMultiplier);
            }

            // Consume one round (shotgun: 1 shell = multiple pellets)
            if (usesAmmo && !cfg.InfiniteAmmo)
            {
                weapon.AmmoInMagazine -= 1;
            }
        }
    }
}
