using System.Collections.Generic;
using Adapters;
using Systems;
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
        readonly List<HitSignal> _hitInbox = new();

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
            PlayerSpawnSystem.SpawnPlayer(RaidState, _eventBuffer);
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

            // Systems run here in deterministic order.
            MovementSystem.Tick(RaidState, in context);
            WeaponEquipSystem.Tick(RaidState, in context);
            AimingSystem.Tick(RaidState, in context);
            ShootingSystem.Tick(RaidState, in context);
            ProjectileSystem.Tick(RaidState, in context);
            DamageSystem.Tick(RaidState, _hitInbox, in context);
            _hitInbox.Clear();

            RaidState.ElapsedTime += context.DeltaTime;
        }

        public RaidEventBuffer ConsumeEvents() => _eventBuffer;

        public void ClearEvents() => _eventBuffer.Clear();

        public void ReportHit(HitSignal signal)
        {
            _hitInbox.Add(signal);
        }

        public void End()
        {
            RaidState.IsRunning = false;
            _eventBuffer.RaidEnded();
        }
    }
}
