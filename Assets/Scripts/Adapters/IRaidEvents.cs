using State;

namespace Adapters
{
    public interface IRaidEvents
    {
        void RaidStarted();
        void RaidEnded();
        void PlayerSpawned(EId id);
    }
}
