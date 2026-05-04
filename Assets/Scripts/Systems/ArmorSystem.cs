using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    public struct DamageResult
    {
        public float HpDamage;
        public float ArmorDurDamage;
        public float AbsorptionRatio;
        public bool ArmorHit;
    }

    public static class ArmorSystem
    {
        public static float EffectiveDurabilityMultiplier(float durabilityPercent,
            float threshold = ArmorConstants.DurabilityThreshold,
            float power = ArmorConstants.DurabilityParabolicPower)
        {
            if (durabilityPercent >= threshold)
                return 1f;
            if (durabilityPercent <= 0f)
                return 0f;

            float t = durabilityPercent / threshold;
            return Mathf.Pow(t, power);
        }

        public static float EffectiveArmorPoints(ArmorState armor)
        {
            if (armor == null || armor.IsBroken)
                return 0f;

            return armor.ArmorPoints * EffectiveDurabilityMultiplier(armor.DurabilityPercent);
        }

        public static float CalcDamageMultiplier(float effectiveArmor, float penetration,
            float K = ArmorConstants.DamageReductionK)
        {
            float diff = effectiveArmor - penetration;
            if (diff <= 0f)
                return 1f;

            return K / (K + diff);
        }

        public static float CalcArmorDurabilityDamage(float baseArmorDmg, float absorptionRatio)
        {
            return baseArmorDmg * (1f + absorptionRatio);
        }

        public static ArmorState GetArmorForHit(ArmorSlotState slots, bool isHeadshot)
        {
            if (slots == null) return null;
            return isHeadshot ? slots.Helmet : slots.BodyArmor;
        }

        public static DamageResult Calculate(float rawDamage, float penetration, float armorDamage,
            ArmorSlotState armorSlots, bool isHeadshot, in ArmorConfig cfg)
        {
            // Debug cheats
            if (cfg.ForceNoArmor)
            {
                return new DamageResult
                {
                    HpDamage = rawDamage,
                    ArmorDurDamage = 0f,
                    AbsorptionRatio = 0f,
                    ArmorHit = false,
                };
            }

            var armor = GetArmorForHit(armorSlots, isHeadshot);

            if (armor == null || armor.IsBroken)
            {
                return new DamageResult
                {
                    HpDamage = rawDamage,
                    ArmorDurDamage = 0f,
                    AbsorptionRatio = 0f,
                    ArmorHit = false,
                };
            }

            float effectiveArmor = cfg.ForceMaxArmor
                ? armor.ArmorPoints  // ignore durability degradation
                : EffectiveArmorPoints(armor);
            float multiplier = CalcDamageMultiplier(effectiveArmor, penetration, cfg.DamageReductionK);
            float absorptionRatio = 1f - multiplier;

            return new DamageResult
            {
                HpDamage = rawDamage * multiplier,
                ArmorDurDamage = CalcArmorDurabilityDamage(armorDamage, absorptionRatio),
                AbsorptionRatio = absorptionRatio,
                ArmorHit = true,
            };
        }

        public static bool ShouldRicochet(ArmorState helmet, float penetration, float ricochetRoll,
            float ricochetChance = ArmorConstants.RicochetChance)
        {
            if (helmet == null || helmet.IsBroken)
                return false;

            float effectiveArmor = EffectiveArmorPoints(helmet);
            if (penetration >= effectiveArmor)
                return false;

            return ricochetRoll < ricochetChance;
        }

        public static void ApplyDurabilityDamage(ArmorState armor, float durDamage)
        {
            if (armor == null) return;
            armor.CurrentDurability = Mathf.Max(0f, armor.CurrentDurability - durDamage);
        }
    }
}
