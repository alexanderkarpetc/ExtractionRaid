using Adapters;

namespace Session
{
    public readonly struct RaidContext
    {
        public readonly float DeltaTime;
        public readonly IRaidEvents Events;
        public readonly ITimeAdapter Time;
        public readonly IInputAdapter Input;
        public readonly INavMeshAdapter NavMesh;
        public readonly IPhysicsAdapter Physics;
        public readonly IGrenadePositionAdapter GrenadePositions;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time,
            IInputAdapter input, INavMeshAdapter navMesh, IPhysicsAdapter physics = null,
            IGrenadePositionAdapter grenadePositions = null)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
            Input = input;
            NavMesh = navMesh;
            Physics = physics;
            GrenadePositions = grenadePositions;
        }
    }
}
