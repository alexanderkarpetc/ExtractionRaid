using State;

namespace Adapters
{
    public class RaidEventBuffer : IRaidEvents
    {
        public bool HasRaidStarted { get; private set; }
        public bool HasRaidEnded { get; private set; }
        public bool HasPlayerSpawned { get; private set; }
        public EId SpawnedPlayerId { get; private set; }

        public void RaidStarted() => HasRaidStarted = true;
        public void RaidEnded() => HasRaidEnded = true;

        public void PlayerSpawned(EId id)
        {
            HasPlayerSpawned = true;
            SpawnedPlayerId = id;
        }

        public void Clear()
        {
            HasRaidStarted = false;
            HasRaidEnded = false;
            HasPlayerSpawned = false;
            SpawnedPlayerId = EId.None;
        }
    }
}
