namespace State
{
    public class RaidState
    {
        public float ElapsedTime;
        public bool IsRunning;
        public PlayerEntityState PlayerEntity;

        int _nextEIdValue;

        public EId AllocateEId()
        {
            _nextEIdValue++;
            return new EId(_nextEIdValue);
        }

        public static RaidState Create()
        {
            return new RaidState
            {
                ElapsedTime = 0f,
                IsRunning = true,
                _nextEIdValue = 0,
            };
        }
    }
}
