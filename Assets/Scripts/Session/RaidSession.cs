using Adapters;
using Managers;
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
        readonly IInputAdapter _inputAdapter;
        readonly INavMeshAdapter _navMeshAdapter;

        public RaidSession(string levelId, ITimeAdapter timeAdapter, IInputAdapter inputAdapter,
            INavMeshAdapter navMeshAdapter)
        {
            _timeAdapter = timeAdapter;
            _inputAdapter = inputAdapter;
            _navMeshAdapter = navMeshAdapter;
            _eventBuffer = new RaidEventBuffer();
            RaidState = RaidState.Create();
            LevelState = LevelState.Create(levelId);
        }

        public void Start()
        {
            PlayerSpawnManager.SpawnPlayer(RaidState, _eventBuffer);
            _eventBuffer.RaidStarted();
        }

        public void Tick()
        {
            if (!RaidState.IsRunning) return;

            var context = new RaidContext(
                deltaTime: _timeAdapter.DeltaTime,
                events: _eventBuffer,
                time: _timeAdapter,
                input: _inputAdapter,
                navMesh: _navMeshAdapter
            );

            // Managers run here in deterministic order.
            MovementManager.Tick(RaidState, in context);

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
