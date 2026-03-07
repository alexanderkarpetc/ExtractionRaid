using System.Collections.Generic;
using State;
using UnityEngine;

namespace Adapters
{
    public enum RaidEventType : byte
    {
        RaidStarted,
        RaidEnded,
        PlayerSpawned,
        ProjectileSpawned,
        ProjectileDespawned,
        DestructibleDamaged,
        DestructibleDestroyed,
        GroundItemSpawned,
        GroundItemDespawned,
    }

    public struct RaidEvent
    {
        public RaidEventType Type;
        public EId Id;
        public Vector3 Position;
        public Vector3 Direction;
        public float CurrentHp;
        public float MaxHp;
        public float Damage;
        public string StringPayload;
    }

    public class RaidEventBuffer : IRaidEvents
    {
        readonly List<RaidEvent> _events = new();

        public IReadOnlyList<RaidEvent> All => _events;

        public void RaidStarted()
        {
            _events.Add(new RaidEvent { Type = RaidEventType.RaidStarted });
        }

        public void RaidEnded()
        {
            _events.Add(new RaidEvent { Type = RaidEventType.RaidEnded });
        }

        public void PlayerSpawned(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.PlayerSpawned, Id = id });
        }

        public void ProjectileSpawned(EId id, Vector3 position, Vector3 direction, float damage)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.ProjectileSpawned,
                Id = id,
                Position = position,
                Direction = direction,
                Damage = damage,
            });
        }

        public void ProjectileDespawned(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.ProjectileDespawned, Id = id });
        }

        public void DestructibleDamaged(EId id, float currentHp, float maxHp)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.DestructibleDamaged,
                Id = id,
                CurrentHp = currentHp,
                MaxHp = maxHp,
            });
        }

        public void DestructibleDestroyed(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.DestructibleDestroyed, Id = id });
        }

        public void GroundItemSpawned(EId id, Vector3 position, string definitionId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.GroundItemSpawned,
                Id = id,
                Position = position,
                StringPayload = definitionId,
            });
        }

        public void GroundItemDespawned(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.GroundItemDespawned, Id = id });
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
