namespace State
{
    public class ArmorState
    {
        public float ArmorPoints;
        public float CurrentDurability;
        public float MaxDurability;

        public bool IsBroken => CurrentDurability <= 0f;
        public float DurabilityPercent => MaxDurability > 0f ? CurrentDurability / MaxDurability : 0f;

        public static ArmorState Create(float armorPoints, float maxDurability)
        {
            return new ArmorState
            {
                ArmorPoints = armorPoints,
                CurrentDurability = maxDurability,
                MaxDurability = maxDurability,
            };
        }
    }

    public class ArmorSlotState
    {
        public ArmorState Helmet;
        public ArmorState BodyArmor;

        // ItemDefinition ids of the equipped armor, recorded at spawn so death loot can
        // reconstruct the exact items — including armor rolled from an equipment pool,
        // which is not present on the static BotTypeConfig fields.
        public string HelmetDefinitionId;
        public string BodyArmorDefinitionId;
    }
}
