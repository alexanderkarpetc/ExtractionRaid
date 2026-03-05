using Adapters;

namespace Tests.EditMode.Fakes
{
    public class FakeTimeAdapter : ITimeAdapter
    {
        public float DeltaTime { get; set; }
        public float FixedDeltaTime { get; set; }
        public float Time { get; set; }
    }
}