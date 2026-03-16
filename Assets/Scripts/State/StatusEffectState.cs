namespace State
{
    public enum StatusEffectType : byte
    {
        Bleeding,
    }

    public class StatusEffectInstance
    {
        public StatusEffectType Type;
        public float AppliedTime;
        public float LastTickTime;
    }
}
