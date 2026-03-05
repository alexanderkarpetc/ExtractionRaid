using Adapters;
using State;

namespace Session
{
    public class RaidSession
    {
        public RaidState RaidState { get; private set; }
        public LevelState LevelState { get; private set; }
        public bool IsActive => RaidState.IsRunning;

        readonly RaidEventBuffer _eventBuffer;
        readonly ITimeAdapter _timeAdapter;

        public RaidSession(string levelId, ITimeAdapter timeAdapter)
        {
            _timeAdapter = timeAdapter;
            _eventBuffer = new RaidEventBuffer();
            RaidState = RaidState.Create();
            LevelState = LevelState.Create(levelId);
        }

        public void Start()
        {
            _eventBuffer.RaidStarted();
        }

        public void Tick()
        {
            if (!RaidState.IsRunning) return;

            var context = new RaidContext(
                deltaTime: _timeAdapter.DeltaTime,
                events: _eventBuffer,
                time: _timeAdapter
            );

            // Managers run here in deterministic order.
            // None yet — this is the extension point.

            RaidState.ElapsedTime += context.DeltaTime;
        }

        public RaidEventBuffer ConsumeEvents() => _eventBuffer;

        public void ClearEvents() => _eventBuffer.Clear();

        public void End()
        {
            RaidState.IsRunning = false;
            _eventBuffer.RaidEnded();
        }
    }
}
