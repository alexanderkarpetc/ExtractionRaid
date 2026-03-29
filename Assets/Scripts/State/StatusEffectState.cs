namespace State
{
    public enum StatusEffectType : byte
    {
        Bleeding,
    }

    public class StatusEffectInstance
    {
        public StatusEffectType Type;
        public int Level = 1; // 1 = light, 2 = heavy (for Bleeding)
        public float AppliedTime;
        public float LastTickTime;
    }
}
