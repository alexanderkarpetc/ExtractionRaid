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
                var cleanAim = player.WeaponAimPoint - recoilOffset;

                // Smooth clean position toward mouse
                float smoothFactor = 1f - Mathf.Exp(-aimFollowSharpness * context.DeltaTime);
                cleanAim = Vector3.Lerp(cleanAim, aimPoint, smoothFactor);

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

            // 4. FacingDirection — follows raw aim (body faces player intent)
            var coneHalfAngle = weapon != null ? weapon.Stats.ConeHalfAngle : UnarmedConeHalfAngle;
            var bodyRotationSpeed = weapon != null ? weapon.Stats.BodyRotationSpeed : UnarmedBodyRotationSpeed;

            var currentFacing = player.FacingDirection;
            if (currentFacing.sqrMagnitude < 0.001f)
            {
                player.FacingDirection = rawDir;
                return;
            }

            var bodyAngle = Vector3.Angle(currentFacing, rawDir);

            var t = bodyAngle / coneHalfAngle;
            var speed = bodyRotationSpeed * t;
            var maxStep = speed * context.DeltaTime * Mathf.Deg2Rad;
            player.FacingDirection = Vector3.RotateTowards(
                currentFacing, rawDir, maxStep, 0f).normalized;
        }
    }
}
