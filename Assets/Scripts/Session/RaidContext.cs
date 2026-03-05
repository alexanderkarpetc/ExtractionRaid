using Adapters;

namespace Session
{
    public readonly struct RaidContext
    {
        public readonly float DeltaTime;
        public readonly IRaidEvents Events;
        public readonly ITimeAdapter Time;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
        }
    }
}
