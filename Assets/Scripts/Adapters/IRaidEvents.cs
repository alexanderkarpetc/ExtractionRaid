using State;
using UnityEngine;

namespace Adapters
{
    public interface IRaidEvents
    {
        void RaidStarted();
        void RaidEnded();
        void PlayerSpawned(EId id);
        void ProjectileSpawned(EId id, Vector3 position, Vector3 direction, float damage);
        void ProjectileDespawned(EId id);
        void DestructibleDamaged(EId id, float currentHp, float maxHp);
        void DestructibleDestroyed(EId id);
        void GroundItemSpawned(EId id, Vector3 position, string definitionId);
        void GroundItemDespawned(EId id);
    }
}
