using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class AimingSystem
    {
        public const float UnarmedConeHalfAngle = 60f;
        public const float UnarmedBodyRotationSpeed = 360f;
        public const float UnarmedAimFollowSharpness = 30f;

        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            var aimPoint = input.AimWorldPoint;

            // 1. Raw aim — instant from mouse
            player.RawAimPoint = aimPoint;

            // Min-aim-distance clamp: when cursor lands too close to the player (e.g. hovering
            // over the player's silhouette on screen), aim direction amplifies tiny mouse motion
            // and causes the weapon/body to flip wildly.
            //
            // Clamp DISTANCE only, keep DIRECTION from current cursor — so cursor "slides along
            // a circle of radius minAimDist around the player". Weapon still responds to mouse
            // movement inside the zone (no sticky feel), but aimPoint never gets close enough to
            // produce jitter. Fallback to previous AimDirection if cursor is exactly on player.
            // RawAimPoint (cursor overlay) is NOT clamped — the white dot keeps following the mouse.
            float minAimDist = context.AimConfig.MinAimDistance;
            if (minAimDist > 0f)
            {
                var toAim = new Vector3(aimPoint.x - player.Position.x, 0f, aimPoint.z - player.Position.z);
                float sqrDist = toAim.sqrMagnitude;
                if (sqrDist < minAimDist * minAimDist)
                {
                    Vector3 dir;
                    if (sqrDist > 0.0001f)
                        dir = toAim / Mathf.Sqrt(sqrDist); // cursor-to-player direction (updates live)
                    else
                        dir = player.AimDirection; // cursor exactly on player: fall back to last valid

                    if (dir.sqrMagnitude > 0.01f)
                    {
                        aimPoint = new Vector3(
                            player.Position.x + dir.x * minAimDist,
                            aimPoint.y,
                            player.Position.z + dir.z * minAimDist);
                    }
                }
            }

            var origin = player.Position;
            var toRaw = new Vector3(aimPoint.x - origin.x, 0f, aimPoint.z - origin.z);

            if (toRaw.sqrMagnitude < 0.001f) return;

            float rawDist = toRaw.magnitude;
            var rawDir = toRaw / rawDist;

            // 2. Weapon aim — position-based exponential smoothing with recoil
            var weapon = player.EquippedWeapon;

            {
                var aimCfg = context.AimConfig;
                float aimFollowSharpness = weapon != null ? weapon.Stats.AimFollowSharpness : UnarmedAimFollowSharpness;

                // When aim split disabled — skip smoothing (instant follow)
                if (!aimCfg.AimSplitEnabled)
                    aimFollowSharpness = 1000f;
                else
                {
                    aimFollowSharpness *= aimCfg.AimFollowMultiplier;
                    aimFollowSharpness *= Mathf.Lerp(1f, aimCfg.AdsAimFollowMultiplier, player.AdsBlend);
                }

                // Strip recoil to get clean base position
                var recoilOffset = weapon != null ? weapon.RecoilOffset : Vector3.zero;
                var prevClean = player.WeaponAimPoint - recoilOffset;

                // Base follow — exponential smoothing toward the cursor (hip / no-scope feel, unchanged).
                float smoothFactor = 1f - Mathf.Exp(-aimFollowSharpness * context.DeltaTime);
                var expClean = Vector3.Lerp(prevClean, aimPoint, smoothFactor);

                // Sniper scope — swap the base follow for a damped spring so the aim has WEIGHT:
                // low ergonomics = soft + underdamped (the aim lags, overshoots past the target on
                // a sudden stop, then bounces back and settles); high ergo = stiff + critically
                // damped (snaps, no bounce). Blended in by ScopeReveal so hip / no-scope aiming
                // keeps the crisp exponential feel. Everything (dot / scope circle / bullet) derives
                // from WeaponAimPoint, so the weight is honest and affects the shot.
                float reveal = weapon != null ? Mathf.Clamp01(player.ScopeReveal) : 0f;
                Vector3 cleanAim;
                if (reveal > 0.01f)
                {
                    float ergo01 = WeaponStatDisplay.ErgonomicsGoodness(weapon.Stats); // 0 bad .. 1 good, matches stat bar
                    float shaped = Mathf.Pow(Mathf.Clamp01(ergo01), Mathf.Max(0.01f, aimCfg.ScopeErgoImpact));
                    float stiffness = Mathf.Lerp(aimCfg.ScopeSpringStiffnessLow, aimCfg.ScopeSpringStiffnessHigh, shaped);
                    float dampingRatio = Mathf.Lerp(aimCfg.ScopeSpringDampingLow, aimCfg.ScopeSpringDampingHigh, shaped);
                    var springClean = SpringToward(prevClean, ref player.WeaponAimVelocity, aimPoint,
                                                   stiffness, dampingRatio, context.DeltaTime);
                    cleanAim = Vector3.Lerp(expClean, springClean, reveal);
                }
                else
                {
                    player.WeaponAimVelocity = Vector3.zero; // reset so re-engaging the scope starts settled
                    cleanAim = expClean;
                }

                // Decay recoil independently
                if (weapon != null && weapon.RecoilOffset.sqrMagnitude > 0.0001f)
                {
                    float adsRecovery = Mathf.Lerp(1f, aimCfg.AdsRecoilRecoveryMultiplier, player.AdsBlend);
                    float recoilDecay = 1f - Mathf.Exp(-weapon.Stats.RecoilRecoverySpeed * aimCfg.RecoilRecoveryMultiplier * adsRecovery * context.DeltaTime);
                    weapon.RecoilOffset = Vector3.Lerp(weapon.RecoilOffset, Vector3.zero, recoilDecay);
                }

                // Final aim = base + decayed recoil
                player.WeaponAimPoint = cleanAim + (weapon != null ? weapon.RecoilOffset : Vector3.zero);

                // Feed weapon aim screen position to input adapter for convergence raycast
                // (so convergence accounts for recoil — affects headshot detection)
                input.SetWeaponAimScreenPos(input.WorldToScreen(player.WeaponAimPoint));
            }

            // 3. AimDirection derived from weapon aim
            var weaponAimDir = player.WeaponAimPoint - origin;
            weaponAimDir.y = 0f;
            player.AimDirection = weaponAimDir.sqrMagnitude > 0.001f
                ? weaponAimDir.normalized
                : rawDir;

            // 4. FacingDirection — normally follows the raw cursor (body faces player intent).
            // While scoped, follow the (lagged) WEAPON aim instead, so the body points where the
            // gun actually shoots — otherwise the sluggish body + instant aim read as "facing
            // forward, firing from the back". Blended by ScopeReveal.
            var coneHalfAngle = weapon != null ? weapon.Stats.ConeHalfAngle : UnarmedConeHalfAngle;
            var bodyRotationSpeed = weapon != null ? weapon.Stats.BodyRotationSpeed : UnarmedBodyRotationSpeed;

            var facingTarget = rawDir;
            float scopedFacing = Mathf.Clamp01(player.ScopeReveal);
            if (scopedFacing > 0.01f && player.AimDirection.sqrMagnitude > 0.001f)
                facingTarget = Vector3.Slerp(rawDir, player.AimDirection, scopedFacing).normalized;

            var currentFacing = player.FacingDirection;
            if (currentFacing.sqrMagnitude < 0.001f)
            {
                player.FacingDirection = facingTarget;
                return;
            }

            var bodyAngle = Vector3.Angle(currentFacing, facingTarget);

            var t = bodyAngle / coneHalfAngle;
            var speed = bodyRotationSpeed * t;
            var maxStep = speed * context.DeltaTime * Mathf.Deg2Rad;
            player.FacingDirection = Vector3.RotateTowards(
                currentFacing, facingTarget, maxStep, 0f).normalized;
        }

        // Damped-spring step toward a target (mass = 1). stiffness = pull toward target;
        // dampingRatio ζ = resistance to velocity (ζ<1 → overshoot + bounce, ζ=1 → critical, no
        // overshoot). Semi-implicit Euler, substepped to a fixed max step so it stays stable and
        // framerate-consistent regardless of the tick dt. Velocity is carried in state.
        static Vector3 SpringToward(Vector3 pos, ref Vector3 vel, Vector3 target,
                                    float stiffness, float dampingRatio, float dt)
        {
            if (dt <= 0f) return pos;
            float k = Mathf.Max(0.0001f, stiffness);
            float c = 2f * dampingRatio * Mathf.Sqrt(k); // damping coefficient for mass = 1

            const float maxStep = 1f / 120f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxStep), 1, 8);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 accel = -k * (pos - target) - c * vel;
                vel += accel * h;
                pos += vel * h;
            }
            return pos;
        }
    }
}
