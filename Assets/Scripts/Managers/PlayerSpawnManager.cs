using Adapters;
using State;
using UnityEngine;

namespace Managers
{
    public static class PlayerSpawnManager
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
