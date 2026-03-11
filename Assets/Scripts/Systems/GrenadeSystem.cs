using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class GrenadeSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            var input = context.Input;
            if (input == null) return;

            if (input.GrenadePressed)
            {
                if (player.IsInGrenadeMode)
                {
                    ExitGrenadeMode(player);
                    return;
                }

                if (player.GrenadeCount > 0 && !player.IsRolling)
                {
                    player.IsInGrenadeMode = true;
                    player.GrenadeThrowCharging = false;
                }

                return;
            }

            if (!player.IsInGrenadeMode) return;

            UpdateTargetDistance(player, input);

            if (input.AttackPressed && !player.GrenadeThrowCharging)
            {
                player.GrenadeThrowCharging = true;
            }

            if (input.AttackJustReleased && player.GrenadeThrowCharging)
            {
                ThrowGrenade(state, player, context);
                ExitGrenadeMode(player);
            }
        }

        static void UpdateTargetDistance(PlayerEntityState player, Adapters.IInputAdapter input)
        {
            var aimPoint = input.AimWorldPoint;
            var playerFlat = new Vector3(player.Position.x, 0f, player.Position.z);
            var aimFlat = new Vector3(aimPoint.x, 0f, aimPoint.z);
            float dist = Vector3.Distance(playerFlat, aimFlat);
            player.GrenadeTargetDistance = Mathf.Clamp(dist,
                GrenadeConstants.MinThrowRange, GrenadeConstants.MaxThrowRange);
        }

        static Vector3 ComputeThrowVelocity(Vector3 horizontalDir, float distance)
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            float speed = GrenadeConstants.ComputeThrowSpeed(distance, gravity);

            float rad = GrenadeConstants.UpwardAngle * Mathf.Deg2Rad;
            var throwDir = (horizontalDir * Mathf.Cos(rad) +
                            Vector3.up * Mathf.Sin(rad)).normalized;
            return throwDir * speed;
        }

        static void ThrowGrenade(RaidState state, PlayerEntityState player, in RaidContext context)
        {
            var aimDir = player.AimDirection;
            if (aimDir.sqrMagnitude < 0.001f)
                aimDir = player.FacingDirection;

            var horizontalDir = new Vector3(aimDir.x, 0f, aimDir.z).normalized;
            var velocity = ComputeThrowVelocity(horizontalDir, player.GrenadeTargetDistance);
            var spawnPos = player.Position + Vector3.up * GrenadeConstants.LaunchHeight + horizontalDir * 0.5f;

            var id = state.AllocateEId();
            var grenade = GrenadeEntityState.Create(
                id, player.Id, velocity, state.ElapsedTime,
                GrenadeConstants.FuseTime, GrenadeConstants.Damage,
                GrenadeConstants.ExplosionRadius);

            state.Grenades.Add(grenade);
            player.GrenadeCount--;

            context.Events.GrenadeSpawned(id, spawnPos, velocity);
        }

        static void ExitGrenadeMode(PlayerEntityState player)
        {
            player.IsInGrenadeMode = false;
            player.GrenadeThrowCharging = false;
        }

        public static void TickExplosions(RaidState state, in RaidContext context)
        {
            for (int i = state.Grenades.Count - 1; i >= 0; i--)
            {
                var grenade = state.Grenades[i];
                if (state.ElapsedTime - grenade.SpawnTime < grenade.FuseTime)
                    continue;

                var pos = context.GrenadePositions?.GetPosition(grenade.Id);
                if (!pos.HasValue)
                {
                    state.Grenades.RemoveAt(i);
                    context.Events.GrenadeDespawned(grenade.Id);
                    continue;
                }

                var explosionPos = pos.Value;
                ApplyExplosionDamage(state, grenade, explosionPos, context);

                context.Events.GrenadeExploded(grenade.Id, explosionPos);
                context.Events.GrenadeDespawned(grenade.Id);
                state.Grenades.RemoveAt(i);
            }
        }

        static void ApplyExplosionDamage(
            RaidState state, GrenadeEntityState grenade, Vector3 center, in RaidContext context)
        {
            if (state.PlayerEntity != null)
                TryDamageEntity(state, grenade.OwnerId, state.PlayerEntity.Id,
                    state.PlayerEntity.Position, center, grenade, context);

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                TryDamageEntity(state, grenade.OwnerId, bot.Id,
                    bot.Position, center, grenade, context);
            }
        }

        static void TryDamageEntity(
            RaidState state, EId ownerId, EId targetId, Vector3 targetPos,
            Vector3 center, GrenadeEntityState grenade, in RaidContext context)
        {
            var dist = Vector3.Distance(center, targetPos);
            if (dist > grenade.ExplosionRadius) return;

            if (!state.HealthMap.TryGetValue(targetId, out var health)) return;
            if (!health.IsAlive) return;

            if (context.Physics != null)
            {
                var eyePos = targetPos + Vector3.up * 1f;
                var explosionEye = center + Vector3.up * 0.2f;
                int wallMask = LayerMask.GetMask("Default");
                if (context.Physics.Linecast(explosionEye, eyePos, wallMask))
                    return;
            }

            float falloff = 1f - (dist / grenade.ExplosionRadius);
            float damage = grenade.Damage * falloff;

            DamageSystem.ApplyDamage(health, damage);

            if (health.IsAlive)
                context.Events.EntityDamaged(targetId, health.CurrentHp, health.MaxHp);
            else
                context.Events.EntityDied(targetId);
        }
    }
}
