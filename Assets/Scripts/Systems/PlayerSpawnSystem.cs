using Adapters;
using State;
using UnityEngine;

namespace Systems
{
    public static class PlayerSpawnSystem
    {
        static readonly Vector3 DefaultSpawnPosition = Vector3.zero;

        public static void SpawnPlayer(RaidState state, IRaidEvents events)
        {
            if (state.PlayerEntity != null) return;

            var id = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(id, DefaultSpawnPosition);
            events.PlayerSpawned(id);
        }
    }
}
