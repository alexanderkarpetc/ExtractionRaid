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
        void EntityDamaged(EId id, float currentHp, float maxHp);
        void EntityDied(EId id);
        void GroundItemSpawned(EId id, Vector3 position, string definitionId);
        void GroundItemDespawned(EId id);

        void BotSpawned(EId id, Vector3 position, string typeId);
        void BotDespawned(EId id);
    }
}
