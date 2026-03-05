namespace State
{
    public class RaidState
    {
        public float ElapsedTime;
        public bool IsRunning;

        int _nextEntityIdValue;

        public EntityId AllocateEntityId()
        {
            _nextEntityIdValue++;
            return new EntityId(_nextEntityIdValue);
        }

        public static RaidState Create()
        {
            return new RaidState
            {
                ElapsedTime = 0f,
                IsRunning = true,
                _nextEntityIdValue = 0,
            };
        }
    }
}
