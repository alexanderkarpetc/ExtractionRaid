namespace Adapters
{
    public class UnityTimeAdapter : ITimeAdapter
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        public float Time => UnityEngine.Time.time;
    }
}
