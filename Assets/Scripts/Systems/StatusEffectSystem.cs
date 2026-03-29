using System.Collections.Generic;
using Constants;
using Dev;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public static class StatusEffectSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            HandleCheatBleed(state);

            foreach (var kvp in state.StatusEffects)
            {
                var entityId = kvp.Key;
                var effects = kvp.Value;

                if (!state.HealthMap.TryGetValue(entityId, out var health))
                    continue;
                if (!health.IsAlive) continue;

                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    var effect = effects[i];
                    switch (effect.Type)
                    {
                        case StatusEffectType.Bleeding:
                            TickBleed(effect, health, entityId, state, context);
                            break;
                    }
                }
            }
        }

        static void TickBleed(StatusEffectInstance effect, HealthState health,
            EId entityId, RaidState state, in RaidContext context)
        {
            if (state.ElapsedTime - effect.LastTickTime < StatusEffectConstants.BleedTickInterval)
                return;

            effect.LastTickTime = state.ElapsedTime;
            float dmg = effect.Level >= 2
                ? StatusEffectConstants.BleedL2DamagePerTick
                : StatusEffectConstants.BleedL1DamagePerTick;
            DamageSystem.ApplyDamage(health, dmg);

            if (health.IsAlive)
                context.Events.EntityDamaged(entityId, health.CurrentHp, health.MaxHp);
            else
                context.Events.EntityDied(entityId);
        }

        static void HandleCheatBleed(RaidState state)
        {
            if (!DevCheats.ForceBleedPlayer) return;
            DevCheats.ForceBleedPlayer = false;

            if (state.PlayerEntity == null) return;
            ApplyEffect(state, state.PlayerEntity.Id, StatusEffectType.Bleeding);
        }

        public static void ApplyEffect(RaidState state, EId entityId, StatusEffectType type)
        {
            if (!state.StatusEffects.TryGetValue(entityId, out var effects))
            {
                effects = new List<StatusEffectInstance>();
                state.StatusEffects[entityId] = effects;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == type)
                {
                    // Upgrade: L1 → L2 (max level = 2)
                    if (effects[i].Level < 2)
                        effects[i].Level = 2;
                    return;
                }
            }

            effects.Add(new StatusEffectInstance
            {
                Type = type,
                Level = 1,
                AppliedTime = state.ElapsedTime,
                LastTickTime = state.ElapsedTime,
            });
        }

        public static void RemoveEffect(RaidState state, EId entityId, StatusEffectType type)
        {
            if (!state.StatusEffects.TryGetValue(entityId, out var effects))
                return;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i].Type == type)
                {
                    effects.RemoveAt(i);
                    break;
                }
            }
        }

        public static int GetBleedLevel(RaidState state, EId entityId)
        {
            if (!state.StatusEffects.TryGetValue(entityId, out var effects))
                return 0;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == StatusEffectType.Bleeding)
                    return effects[i].Level;
            }

            return 0;
        }

        public static void DowngradeBleed(RaidState state, EId entityId)
        {
            if (!state.StatusEffects.TryGetValue(entityId, out var effects))
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == StatusEffectType.Bleeding)
                {
                    if (effects[i].Level >= 2)
                        effects[i].Level = 1;
                    else
                        effects.RemoveAt(i);
                    return;
                }
            }
        }

        public static bool HasEffect(RaidState state, EId entityId, StatusEffectType type)
        {
            if (!state.StatusEffects.TryGetValue(entityId, out var effects))
                return false;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Type == type)
                    return true;
            }

            return false;
        }
    }
}
