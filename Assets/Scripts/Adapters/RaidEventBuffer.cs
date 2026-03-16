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
        EntityDamaged,
        EntityDied,
        GroundItemSpawned,
        GroundItemDespawned,
        BotSpawned,
        BotDespawned,
        WeaponFired,
        WeaponEquipStarted,
        WeaponUnequipStarted,
        WeaponEquipFinished,
        WeaponReloadStarted,
        WeaponReloadFinished,
        WeaponDryFired,
        GrenadeSpawned,
        GrenadeExploded,
        GrenadeDespawned,
        ProjectileHit,
        MedkitUseStarted,
        MedkitUseStopped,
        HitConfirmed,
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

        public void ProjectileHit(EId id, Vector3 position)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.ProjectileHit,
                Id = id,
                Position = position,
            });
        }

        public void EntityDamaged(EId id, float currentHp, float maxHp)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.EntityDamaged,
                Id = id,
                CurrentHp = currentHp,
                MaxHp = maxHp,
            });
        }

        public void EntityDied(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.EntityDied, Id = id });
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

        public void BotSpawned(EId id, Vector3 position, string typeId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.BotSpawned,
                Id = id,
                Position = position,
                StringPayload = typeId,
            });
        }

        public void BotDespawned(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.BotDespawned, Id = id });
        }

        public void WeaponFired(Vector3 position, Vector3 direction)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponFired,
                Position = position,
                Direction = direction,
            });
        }

        public void WeaponEquipStarted(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponEquipStarted,
                StringPayload = prefabId,
            });
        }

        public void WeaponUnequipStarted(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponUnequipStarted,
                StringPayload = prefabId,
            });
        }

        public void WeaponEquipFinished(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponEquipFinished,
                StringPayload = prefabId,
            });
        }

        public void WeaponReloadStarted(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponReloadStarted,
                StringPayload = prefabId,
            });
        }

        public void WeaponReloadFinished(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponReloadFinished,
                StringPayload = prefabId,
            });
        }

        public void WeaponDryFired(string prefabId)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.WeaponDryFired,
                StringPayload = prefabId,
            });
        }

        public void GrenadeSpawned(EId id, Vector3 position, Vector3 velocity)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.GrenadeSpawned,
                Id = id,
                Position = position,
                Direction = velocity,
            });
        }

        public void GrenadeExploded(EId id, Vector3 position)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.GrenadeExploded,
                Id = id,
                Position = position,
            });
        }

        public void GrenadeDespawned(EId id)
        {
            _events.Add(new RaidEvent { Type = RaidEventType.GrenadeDespawned, Id = id });
        }

        public void MedkitUseStarted()
        {
            _events.Add(new RaidEvent { Type = RaidEventType.MedkitUseStarted });
        }

        public void MedkitUseStopped()
        {
            _events.Add(new RaidEvent { Type = RaidEventType.MedkitUseStopped });
        }

        public void HitConfirmed(bool isKill)
        {
            _events.Add(new RaidEvent
            {
                Type = RaidEventType.HitConfirmed,
                Damage = isKill ? 1f : 0f,
            });
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
