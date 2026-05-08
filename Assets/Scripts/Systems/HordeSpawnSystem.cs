using Adapters;
using Constants;
using Dev;
using Session;
using State;
using Systems.Bot;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Continuous wave spawner для the Horde test level. Drops zombies onto a
    /// ring around the player at a fixed cadence, capped by a max-alive count.
    /// Tunable у <see cref="DevCheatsHordeSection"/> (Window → Dev Cheats → 🧟 Horde):
    /// grace period, interval, batch size, cap, ring radius/jitter, arc.
    ///
    /// Stateless system — internal cadence stored on <see cref="RaidState"/>
    /// (timestamp of next spawn). All counts derived from <c>state.Bots</c>.
    /// </summary>
    public static class HordeSpawnSystem
    {
        public static void Tick(RaidState state, in RaidContext context, IRaidEvents events,
            ICoreDefinitionRegistry coreDefinitions)
        {
            if (state == null || events == null) return;

            var cfg = DevCheats.Config?.Horde;
            if (cfg == null || !cfg.Enabled) return;
            if (!BotConstants.TryGetConfig(cfg.ZombieTypeId, out _)) return;

            // Grace period at raid start — no spawning until elapsed exceeds it.
            if (state.ElapsedTime < cfg.GracePeriod) return;

            // Initial schedule = первая spawn ON THE TICK that grace ends.
            if (state.HordeNextSpawnTime <= 0f)
                state.HordeNextSpawnTime = state.ElapsedTime;

            if (state.ElapsedTime < state.HordeNextSpawnTime) return;

            int aliveZombies = CountAliveOfType(state, cfg.ZombieTypeId);
            int budget = Mathf.Min(cfg.SpawnBatchSize, cfg.MaxAlive - aliveZombies);

            for (int i = 0; i < budget; i++)
            {
                var origin = ResolveSpawnAnchor(state);
                var pos    = PickSpawnPosition(origin, cfg);
                BotSpawnSystem.SpawnBot(state, cfg.ZombieTypeId, pos,
                    new[] { origin }, events, coreDefinitions);
            }

            state.HordeNextSpawnTime = state.ElapsedTime + cfg.SpawnInterval;
        }

        static Vector3 ResolveSpawnAnchor(RaidState state)
        {
            return state.PlayerEntity != null ? state.PlayerEntity.Position : Vector3.zero;
        }

        static Vector3 PickSpawnPosition(Vector3 origin, DevCheatsHordeSection cfg)
        {
            float halfArc = cfg.SpawnArc * 0.5f;
            float angleDeg = Random.Range(-halfArc, halfArc);
            float radius = cfg.SpawnRingRadius + Random.Range(-cfg.SpawnRingJitter, cfg.SpawnRingJitter);
            radius = Mathf.Max(0.5f, radius);

            float rad = angleDeg * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius;
            return origin + offset;
        }

        static int CountAliveOfType(RaidState state, string typeId)
        {
            int count = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                if (bot.TypeId != typeId) continue;
                if (!state.HealthMap.TryGetValue(bot.Id, out var hp)) continue;
                if (!hp.IsAlive) continue;
                count++;
            }
            return count;
        }
    }
}
