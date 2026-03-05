using System.Collections.Generic;
using State;
using UnityEngine;

namespace Adapters
{
    public struct ProjectileSpawnedEvent
    {
        public EId Id;
        public Vector3 Position;
        public Vector3 Direction;
    }

    public class RaidEventBuffer : IRaidEvents
    {
        public bool HasRaidStarted { get; private set; }
        public bool HasRaidEnded { get; private set; }
        public bool HasPlayerSpawned { get; private set; }
        public EId SpawnedPlayerId { get; private set; }
        public List<ProjectileSpawnedEvent> SpawnedProjectiles { get; } = new();
        public List<EId> DespawnedProjectileIds { get; } = new();

        public void RaidStarted() => HasRaidStarted = true;
        public void RaidEnded() => HasRaidEnded = true;

        public void PlayerSpawned(EId id)
        {
            HasPlayerSpawned = true;
            SpawnedPlayerId = id;
        }

        public void ProjectileSpawned(EId id, Vector3 position, Vector3 direction)
        {
            SpawnedProjectiles.Add(new ProjectileSpawnedEvent
            {
                Id = id,
                Position = position,
                Direction = direction,
            });
        }

        public void ProjectileDespawned(EId id)
        {
            DespawnedProjectileIds.Add(id);
        }

        public void Clear()
        {
            HasRaidStarted = false;
            HasRaidEnded = false;
            HasPlayerSpawned = false;
            SpawnedPlayerId = EId.None;
            SpawnedProjectiles.Clear();
            DespawnedProjectileIds.Clear();
        }
    }
}
