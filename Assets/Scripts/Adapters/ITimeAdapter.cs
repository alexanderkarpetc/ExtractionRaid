namespace Adapters
{
    public interface ITimeAdapter
    {
        float DeltaTime { get; }
        float FixedDeltaTime { get; }
        float Time { get; }
    }
}
