using Adapters;

namespace Session
{
    public readonly struct RaidContext
    {
        public readonly float DeltaTime;
        public readonly IRaidEvents Events;
        public readonly ITimeAdapter Time;
        public readonly IInputAdapter Input;

        public RaidContext(float deltaTime, IRaidEvents events, ITimeAdapter time, IInputAdapter input)
        {
            DeltaTime = deltaTime;
            Events = events;
            Time = time;
            Input = input;
        }
    }
}
