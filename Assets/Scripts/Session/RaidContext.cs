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

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time,
            IInputAdapter input, INavMeshAdapter navMesh)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
            Input = input;
            NavMesh = navMesh;
        }
    }
}
