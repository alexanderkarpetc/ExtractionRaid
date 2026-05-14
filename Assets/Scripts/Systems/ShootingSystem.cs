using ApplicationCore;
using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class ShootingSystem
    {
        // Burst params (laser + Auto delivery only). Hardcoded for first iteration —
        // expose via DevCheats якщо потрібно tuning.
        // Burst length scales з chargeRatio: quick tap → 1 shot (no follow-up),
        // full hold → LaserBurstCountMax shots fired у sequence.
        const int   LaserBurstCountMin = 1;
        const int   LaserBurstCountMax = 6;
        const float LaserBurstInterval = 0.07f;

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

            // Burst auto-fire: when у Bursting phase, ShootingSystem ticks shots at
            // fixed interval until shots remaining = 0. No input required — burst
            // self-completes after release-fire kicked it off below.
            if (weapon.Phase == WeaponPhase.Bursting)
            {
                TickBurst(state, weapon, player, in context);
                return;
            }

            // Two attack triggers:
            //   * AttackPressed — start charge (Ready) OR keep charging (Charging).
            //   * AttackJustReleased while Charging — fires a charged shot at current
            //     level (Tau cannon mechanic — quick tap = weak, hold = strong).
            bool releaseFire = weapon.Phase == WeaponPhase.Charging && input.AttackJustReleased;
            if (!input.AttackPressed && !releaseFire) return;

            // Phase gate: Ready starts a new shot (or charge); Charging waits for
            // release. Other phases ignore attack input.
            if (weapon.Phase != WeaponPhase.Ready && weapon.Phase != WeaponPhase.Charging)
                return;

            // Ammo check on Ready only — don't dry-fire repeatedly while Charging.
            bool usesAmmo = !string.IsNullOrEmpty(weapon.AmmoType);
            if (weapon.Phase == WeaponPhase.Ready && usesAmmo && weapon.AmmoInMagazine <= 0)
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

            // Charge gate. Laser payload requires a Charging window before every shot.
            //   * Ready + AttackPressed + needs charge → transition to Charging, no fire.
            //   * Charging + AttackPressed → keep charging (return).
            //   * Charging + AttackJustReleased → fire at current charge ratio.
            //   * Non-charge weapon (ballistic) → chargeRatio = 1, normal flow.
            float chargeRatio = 1f;
            if (weapon.Phase == WeaponPhase.Ready && WeaponChargeResolver.RequiresChargeUp(weapon))
            {
                weapon.Phase = WeaponPhase.Charging;
                weapon.PhaseStartTime = state.ElapsedTime;
                weapon.ChargeStartTime = state.ElapsedTime;
                context.Events.WeaponChargeStarted(weapon.PrefabId);
                return;
            }

            if (weapon.Phase == WeaponPhase.Charging)
            {
                if (!releaseFire) return; // still holding — wait for release
                // A4 — apply per-delivery charge multiplier (pistol fast, rifle baseline, shotgun slow).
                float deliveryMult = context.LaserConfig.ChargeTimeMultiplierFor(
                    weapon.DeliveryDefinition?.Pattern ?? FiringPattern.Single);
                float chargeTime = WeaponChargeResolver.GetChargeTime(weapon, deliveryMult);
                chargeRatio = chargeTime > 0f
                    ? Mathf.Clamp01((state.ElapsedTime - weapon.ChargeStartTime) / chargeTime)
                    : 1f;
                context.Events.WeaponChargeCompleted(weapon.PrefabId);
                // Fall through to fire pipeline.
            }

            // Dispatch by Delivery Core's FiringPattern.
            // Single / Auto / Scatter — parametric siblings: shared HandleParametricFire,
            // differ only in Stats (FireInterval, ProjectilesPerShot, SpreadAngle).
            // Rotary / Swarm — dedicated behaviours with their own state machine phases,
            // arriving in Tier 3.
            // See docs/ai/weapon-builder/architecture.md §2.
            var pattern = weapon.DeliveryDefinition != null
                ? weapon.DeliveryDefinition.Pattern
                : FiringPattern.Auto; // legacy/bot-path fallback — Stats drive behaviour
            switch (pattern)
            {
                case FiringPattern.Single:
                case FiringPattern.Auto:
                case FiringPattern.Scatter:
                    // Falls through to parametric fire logic below.
                    break;
                case FiringPattern.Rotary:
                case FiringPattern.Swarm:
                    // TODO (Tier 3): implement SpinUp/SpinDown (Rotary) and VolleyActive (Swarm).
                    return;
                default:
                    return;
            }

            // Semi-auto gate: Single / Scatter require a rising-edge trigger press.
            // The outer phase gate (Ready → Cooldown → Ready) already rate-limits via
            // FireInterval, but it doesn't observe trigger state — holding LMB through a
            // Cooldown would auto-fire on the next Ready frame. This check enforces
            // "one click = one shot" for pistol / shotgun while keeping full-auto (Auto)
            // and laser-charge (releaseFire path) paths untouched.
            bool semiAuto = pattern == FiringPattern.Single || pattern == FiringPattern.Scatter;
            if (semiAuto && !releaseFire && !input.AttackJustPressed)
                return;

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

            // Resolve targeted damageable up-front — used both для targetedEntityId
            // у projectile state та для blend-override below.
            var hitCollider = convergence.HasValue ? input.ConvergenceCollider : null;
            var targetDamageable = hitCollider != null
                ? hitCollider.GetComponentInParent<View.IDamageableView>()
                : null;

            var targetedEntityId = targetDamageable != null ? targetDamageable.EId : default;

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

            // 3. Blend: 0 = full parallax (visual), 1 = full convergence (accuracy).
            //
            // Lock-on override: when the cursor is sitting on a damageable (= player
            // explicitly aiming at an enemy), force blend = 1 so the bullet flies
            // toward the actual 3D capsule hit point. The mid-blend value (~0.3) gives
            // a pleasant "trail through cursor" feel for ground/wall shots, but at
            // certain camera-tilt + side-angle combinations XZ-only blend lands the
            // trajectory just past the capsule edge — bullet visually looks correct
            // but misses у 3D. Forcing convergence here trades a sub-pixel trail-end
            // shift on screen for guaranteed hits on point-and-click targets.
            // Non-damageable cases (ground, walls, empty space) keep the user-tuned
            // blend so the visual feel is preserved.
            if (convergence.HasValue && targetDamageable != null)
                blend = 1f;

            var toAim = Vector3.Lerp(toAimParallax, toAimConv, blend);

            var dir = toAim.sqrMagnitude > 0.001f
                ? toAim.normalized
                : player.AimDirection;

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

            // Compose combat stats: WeaponBase + Ammo. WeaponMod / CharTree sources
            // are architecturally documented (battle-design §1) but out of V0.1 scope.
            float ammoPen = 0f, ammoDmg = 0f, ammoArmorDmg = 0f, ammoBleedChance = 0f;
            if (!string.IsNullOrEmpty(weapon.AmmoType))
            {
                var ammoDef = ItemDefinition.Get(weapon.AmmoType);
                if (ammoDef != null)
                {
                    ammoPen = ammoDef.Penetration;
                    ammoDmg = ammoDef.DamageModifier;
                    ammoArmorDmg = ammoDef.ArmorDamage;
                    ammoBleedChance = ammoDef.BleedChance;
                }
            }
            // Hard caps documented у battle-design-status.md §4 — guards documented
            // invariant even though only WeaponBase + Ammo currently contribute (V0.1).
            float totalPen = Mathf.Min(ArmorConstants.PenetrationCap,
                weapon.Stats.BasePenetration + ammoPen);
            float totalDamage = Mathf.Max(0f, weapon.Stats.Damage + ammoDmg); // floor at 0 — AP penalty can't make damage negative
            float totalArmorDmg = Mathf.Min(ArmorConstants.ArmorDamageCap,
                weapon.Stats.BaseArmorDamage + ammoArmorDmg);
            float totalBleedChance = weapon.Stats.BaseBleedChance + ammoBleedChance;

            // Charge multiplier — parabolic curve через LaserConfig (default min=0.1, power=2).
            // Ballistic chargeRatio is always 1 → multiplier = 1, no behavior change.
            totalDamage *= context.LaserConfig.ChargeDamageMultiplier(chargeRatio);

            // Laser+Scatter signature: chargeRatio modulates BOTH spread cone width AND
            // projectile lifetime. Low charge → wide cone × short range (buckshot). Full charge →
            // narrow cone × long range (focused beam-cluster). Other archetypes unchanged.
            bool isLaserShotgun = weapon.PayloadDefinition is LaserPayloadDefinition
                                && weapon.DeliveryDefinition?.Pattern == FiringPattern.Scatter;
            float spreadMult   = isLaserShotgun
                ? Mathf.Lerp(context.LaserConfig.ShotgunMaxSpreadMult, context.LaserConfig.ShotgunMinSpreadMult, chargeRatio)
                : 1f;
            float lifetimeMult = isLaserShotgun
                ? Mathf.Lerp(context.LaserConfig.ShotgunMinLifetimeMult, context.LaserConfig.ShotgunMaxLifetimeMult, chargeRatio)
                : 1f;

            // Ballistic Rifle signature (B1): sustained Ballistic+Auto fire heats barrel,
            // heat multiplies spread по parabolic curve. Decay paths через WeaponHeatSystem.
            // Other archetypes leave HeatLevel = 0 → multiplier = 1.
            bool isBallisticAuto = weapon.PayloadDefinition is BallisticPayloadDefinition
                                && weapon.DeliveryDefinition?.Pattern == FiringPattern.Auto;
            float heatSpreadMult = (isBallisticAuto && context.BarrelHeatConfig.Enabled)
                ? context.BarrelHeatConfig.SpreadMultiplier(weapon.HeatLevel)
                : 1f;

            var count = Mathf.Max(1, weapon.Stats.ProjectilesPerShot);
            var halfSpread = weapon.Stats.SpreadAngle * 0.5f * spreadMult * heatSpreadMult;
            var lifetime   = weapon.Stats.ProjectileLifetime * lifetimeMult;

            for (int i = 0; i < count; i++)
            {
                var pelletDir = halfSpread > 0f
                    ? Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f) * dir
                    : dir;

                var projectileId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projectileId, player.Id, spawnPos, pelletDir,
                    weapon.Stats.ProjectileSpeed * cfg.ProjectileSpeedMultiplier,
                    state.ElapsedTime, lifetime,
                    totalDamage * cfg.DamageMultiplier,
                    weapon.Stats.HeadshotDamageMultiplier,
                    targetedEntityId,
                    penetration: totalPen,
                    armorDamage: totalArmorDmg,
                    bleedChance: totalBleedChance,
                    archetype: PayloadArchetypeKeyExt.FromArchetypeString(weapon.PayloadDefinition?.Archetype));

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir, totalDamage,
                    weapon.PayloadDefinition?.Archetype, chargeRatio);
            }

            context.Events.WeaponFired(spawnPos, dir, weapon.PayloadDefinition?.Archetype, chargeRatio,
                weapon.DeliveryDefinition?.Pattern ?? FiringPattern.Single);

            // Ballistic Rifle signature (B1): increment barrel heat. Decay runs continuously
            // in WeaponHeatSystem, so sustained fire pushes net upward; tap-burst lets decay catch up.
            if (isBallisticAuto && context.BarrelHeatConfig.Enabled)
                weapon.HeatLevel = Mathf.Min(1f, weapon.HeatLevel + context.BarrelHeatConfig.HeatPerShot);

            // Burst entry: laser + Auto delivery → Bursting phase queues N-1 follow-up
            // shots, fired automatically by TickBurst at fixed interval. Burst length
            // scales з chargeRatio: tap = 1 shot, full hold = LaserBurstCountMax shots.
            // Other weapons (single laser, all ballistic) → standard Firing → Cooldown.
            bool isLaserAuto = weapon.PayloadDefinition is LaserPayloadDefinition
                            && weapon.DeliveryDefinition?.Pattern == FiringPattern.Auto;
            int burstCount = isLaserAuto
                ? Mathf.RoundToInt(Mathf.Lerp(LaserBurstCountMin, LaserBurstCountMax, chargeRatio))
                : 1;
            if (burstCount > 1)
            {
                weapon.Phase = WeaponPhase.Bursting;
                weapon.BurstShotsRemaining = burstCount - 1; // first shot fired now
                weapon.BurstChargeRatio    = chargeRatio;     // captured for whole burst
                weapon.LastBurstShotTime   = state.ElapsedTime;
            }
            else
            {
                weapon.Phase = WeaponPhase.Firing;
            }
            weapon.PhaseStartTime = state.ElapsedTime;
            weapon.LastFireTime = state.ElapsedTime;

            // Apply recoil — forward kick + sideways scatter
            // Both go through RecoilOffset so they survive smoothing and decay via RecoilRecoverySpeed
            if (!cfg.NoRecoil
                && (weapon.Stats.RecoilKickForward > 0f || weapon.Stats.RecoilKickSide > 0f))
            {
                float adsRecoilScale = Mathf.Lerp(1f, cfg.AdsRecoilMultiplier, player.AdsBlend);
                float recoilMul = cfg.RecoilMultiplier * adsRecoilScale;
                var aimDir = (player.WeaponAimPoint - player.Position).normalized;

                // Forward kick through RecoilOffset
                weapon.RecoilOffset += aimDir * (weapon.Stats.RecoilKickForward * recoilMul * cfg.RecoilForwardMultiplier);

                // Sideways scatter through RecoilOffset
                var right = new Vector3(aimDir.z, 0f, -aimDir.x);
                float sideAmount = Random.Range(-weapon.Stats.RecoilKickSide, weapon.Stats.RecoilKickSide);
                weapon.RecoilOffset += right * (sideAmount * recoilMul * cfg.RecoilSideMultiplier);
            }

            // Consume one round (shotgun: 1 shell = multiple pellets)
            if (usesAmmo && !cfg.InfiniteAmmo)
            {
                weapon.AmmoInMagazine -= 1;
            }
        }

        // Auto-fire successive burst shots at fixed interval. Each shot reuses the
        // captured BurstChargeRatio (same damage + VFX intensity throughout burst).
        // Out-of-ammo terminates burst early. Burst exhaustion → Cooldown.
        static void TickBurst(RaidState state, WeaponEntityState weapon,
            PlayerEntityState player, in RaidContext context)
        {
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.Phase = WeaponPhase.Cooldown;
                weapon.PhaseStartTime = state.ElapsedTime;
                return;
            }
            if (state.ElapsedTime - weapon.LastBurstShotTime < LaserBurstInterval) return;

            bool usesAmmo = !string.IsNullOrEmpty(weapon.AmmoType);
            if (usesAmmo && weapon.AmmoInMagazine <= 0)
            {
                // End burst early — out of ammo mid-burst.
                weapon.BurstShotsRemaining = 0;
                weapon.Phase = WeaponPhase.Cooldown;
                weapon.PhaseStartTime = state.ElapsedTime;
                return;
            }

            FireBurstShot(state, weapon, player, in context);

            weapon.LastBurstShotTime = state.ElapsedTime;
            weapon.BurstShotsRemaining--;
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.Phase = WeaponPhase.Cooldown;
                weapon.PhaseStartTime = state.ElapsedTime;
            }
        }

        static void FireBurstShot(RaidState state, WeaponEntityState weapon,
            PlayerEntityState player, in RaidContext context)
        {
            var cfg = context.ShootingConfig;
            var input = context.Input;
            if (input == null) return;

            // Recompute spawn + direction from current muzzle/aim — burst tracks
            // player rotation during the volley.
            var spawnPos = input.MuzzleWorldPoint;
            spawnPos.y = cfg.ProjectileSpawnHeight;
            var aimPoint = player.WeaponAimPoint;
            aimPoint.y = cfg.ProjectileSpawnHeight;
            var toAim = aimPoint - spawnPos;
            if (toAim.sqrMagnitude < 0.001f) return;
            var dir = toAim.normalized;

            // Compose stats (mirrors initial fire flow — chargeRatio from cached burst value).
            float ammoPen = 0f, ammoDmg = 0f, ammoArmorDmg = 0f, ammoBleedChance = 0f;
            if (!string.IsNullOrEmpty(weapon.AmmoType))
            {
                var ammoDef = ItemDefinition.Get(weapon.AmmoType);
                if (ammoDef != null)
                {
                    ammoPen = ammoDef.Penetration;
                    ammoDmg = ammoDef.DamageModifier;
                    ammoArmorDmg = ammoDef.ArmorDamage;
                    ammoBleedChance = ammoDef.BleedChance;
                }
            }
            float totalPen = Mathf.Min(ArmorConstants.PenetrationCap,
                weapon.Stats.BasePenetration + ammoPen);
            float totalDamage = Mathf.Max(0f, weapon.Stats.Damage + ammoDmg);
            float totalArmorDmg = Mathf.Min(ArmorConstants.ArmorDamageCap,
                weapon.Stats.BaseArmorDamage + ammoArmorDmg);
            float totalBleedChance = weapon.Stats.BaseBleedChance + ammoBleedChance;

            // Same parabolic curve as initial fire — burst inherits cached chargeRatio.
            // No spread/lifetime modulation here: burst only triggers for Laser+Auto (single
            // pellet per shot), not Laser+Scatter. If that ever changes — mirror the modulation.
            totalDamage *= context.LaserConfig.ChargeDamageMultiplier(weapon.BurstChargeRatio);

            var count = Mathf.Max(1, weapon.Stats.ProjectilesPerShot);
            var halfSpread = weapon.Stats.SpreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var pelletDir = halfSpread > 0f
                    ? Quaternion.Euler(0f, Random.Range(-halfSpread, halfSpread), 0f) * dir
                    : dir;

                var projectileId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projectileId, player.Id, spawnPos, pelletDir,
                    weapon.Stats.ProjectileSpeed * cfg.ProjectileSpeedMultiplier,
                    state.ElapsedTime, weapon.Stats.ProjectileLifetime,
                    totalDamage * cfg.DamageMultiplier,
                    weapon.Stats.HeadshotDamageMultiplier,
                    targetedEntityId: default,
                    penetration: totalPen,
                    armorDamage: totalArmorDmg,
                    bleedChance: totalBleedChance,
                    archetype: PayloadArchetypeKeyExt.FromArchetypeString(weapon.PayloadDefinition?.Archetype));

                state.Projectiles.Add(projectile);
                context.Events.ProjectileSpawned(projectileId, spawnPos, pelletDir, totalDamage,
                    weapon.PayloadDefinition?.Archetype, weapon.BurstChargeRatio);
            }

            context.Events.WeaponFired(spawnPos, dir, weapon.PayloadDefinition?.Archetype,
                weapon.BurstChargeRatio,
                weapon.DeliveryDefinition?.Pattern ?? FiringPattern.Single);
            weapon.LastFireTime = state.ElapsedTime;

            // Recoil per burst shot.
            if (!cfg.NoRecoil
                && (weapon.Stats.RecoilKickForward > 0f || weapon.Stats.RecoilKickSide > 0f))
            {
                float adsRecoilScale = Mathf.Lerp(1f, cfg.AdsRecoilMultiplier, player.AdsBlend);
                float recoilMul = cfg.RecoilMultiplier * adsRecoilScale;
                var aimDir = (player.WeaponAimPoint - player.Position).normalized;
                weapon.RecoilOffset += aimDir
                    * (weapon.Stats.RecoilKickForward * recoilMul * cfg.RecoilForwardMultiplier);
                var right = new Vector3(aimDir.z, 0f, -aimDir.x);
                float sideAmount = Random.Range(-weapon.Stats.RecoilKickSide, weapon.Stats.RecoilKickSide);
                weapon.RecoilOffset += right * (sideAmount * recoilMul * cfg.RecoilSideMultiplier);
            }

            // Consume one round per burst shot.
            bool usesAmmo = !string.IsNullOrEmpty(weapon.AmmoType);
            if (usesAmmo && !cfg.InfiniteAmmo)
                weapon.AmmoInMagazine -= 1;
        }
    }
}
