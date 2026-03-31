using UnityEngine;

namespace Dev
{
    public class DevCheatsArmorSection : ScriptableObject
    {
        [Header("Penetration Curve")]
        public float DamageReductionK = 30f;
        public float PenetrationCap = 100f;
        public float ArmorPointsCap = 100f;

        [Header("Durability Degradation")]
        public float DurabilityThreshold = 0.7f;
        public float DurabilityParabolicPower = 2f;

        [Header("Helmet Ricochet")]
        public float RicochetChance = 0.4f;

        [Header("Armor Damage")]
        public float ArmorDamageCap = 30f;

        [Header("Armor HUD")]
        public bool ArmorHUDEnabled = true;
        public float ArmorHUDMarginX = 16f;
        public float ArmorHUDMarginY = 40f; // top-left, below stamina bar
        public float ArmorHUDBarWidth = 220f;
        public float ArmorHUDBarHeight = 30f;

        [Header("Debug")]
        public bool ForceNoArmor;
        public bool ForceMaxArmor;
    }
}
