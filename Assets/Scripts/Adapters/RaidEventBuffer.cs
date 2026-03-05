namespace Adapters
{
    public class RaidEventBuffer : IRaidEvents
    {
        public bool HasRaidStarted { get; private set; }
        public bool HasRaidEnded { get; private set; }

        public void RaidStarted() => HasRaidStarted = true;
        public void RaidEnded() => HasRaidEnded = true;

        public void Clear()
        {
            HasRaidStarted = false;
            HasRaidEnded = false;
        }
    }
}
