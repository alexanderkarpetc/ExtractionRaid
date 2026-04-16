using ApplicationCore;
using Constants;
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
                if (AmmoSystem.CanReload(weapon, App.Instance.Player.Inventory))
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

            // --- Solution 2: Pre-fire muzzle block ---
            // Raycast from the player's XZ (at projectile flight height) to the muzzle point.
            // If a wall blocks the line, clamp the projectile spawn to the player-side of the wall.
            // Prevents bullets from spawning behind cover when the barrel clips through geometry.
            //
            // Ray endpoints are both at ProjectileSpawnHeight so the check matches the Y the
            // bullet will actually travel — detects waist-high walls that a chest-height ray would miss.
            // Hierarchy filter (IsChildOf) inside RaycastFirstWallHit skips the player's own colliders
            // robustly even for tiled brick walls where proximity-based filters would misclassify bricks.
            if (cfg.MuzzleBlockEnabled && context.Physics != null)
            {
                var playerAtSpawnY = new Vector3(player.Position.x, cfg.ProjectileSpawnHeight, player.Position.z);
                if (context.Physics.RaycastFirstWallHit(
                        playerAtSpawnY, spawnPos,
                        BotConstants.VisionBlockingMask,
                        input.IgnoreCollisionRoot,
                        out var wallHitPoint))
                {
                    var toMuzzle = spawnPos - playerAtSpawnY;
                    float distToMuzzle = toMuzzle.magnitude;
                    float distToWall = Vector3.Distance(playerAtSpawnY, wallHitPoint);

                    // P1-5 safety: if the wall is touching the player (no clearance for a bullet),
                    // skip the shot silently. Prevents a projectile from being spawned at/inside
                    // the wall for one frame before the collision system despawns it — that looks
                    // like a misfire (bullet appears then disappears immediately).
                    const float minClearance = 0.03f;
                    if (distToWall < minClearance) return;

                    if (distToMuzzle > 0.001f)
                    {
                        var rayDir = toMuzzle / distToMuzzle;
                        // Bound backoff so clamped spawn never ends up BEHIND the player origin
                        // (would produce bullets coming out of the player's back).
                        float maxBackoff = Mathf.Max(0f, distToWall - 0.02f);
                        float safeBackoff = Mathf.Min(cfg.MuzzleBlockBackoff, maxBackoff);
                        spawnPos = wallHitPoint - rayDir * safeBackoff;
                        spawnPos.y = cfg.ProjectileSpawnHeight;
                    }
                }
            }

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

            // Compose combat stats: WeaponBase + Ammo (+ WeaponMod + CharTree placeholders)
            float ammoPen = 0f, ammoArmorDmg = 0f, ammoBleedChance = 0f;
            if (!string.IsNullOrEmpty(weapon.AmmoType))
            {
                var ammoDef = ItemDefinition.Get(weapon.AmmoType);
                if (ammoDef != null)
                {
                    ammoPen = ammoDef.Penetration;
                    ammoArmorDmg = ammoDef.ArmorDamage;
                    ammoBleedChance = ammoDef.BleedChance;
                }
            }
            float totalPen = weapon.BasePenetration + ammoPen; // + weaponMod + charTree (future)
            float totalArmorDmg = weapon.BaseArmorDamage + ammoArmorDmg;
            float totalBleedChance = weapon.BaseBleedChance + ammoBleedChance;

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
                    targetedEntityId,
                    penetration: totalPen,
                    armorDamage: totalArmorDmg,
                    bleedChance: totalBleedChance);

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
