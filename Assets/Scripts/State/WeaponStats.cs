using System;

namespace State
{
    /// <summary>
    /// Common stats contributed by a Payload Core (shared across all payload types).
    /// Payload-specific stats (ChargeTime, ExplosionRadius, etc.) live on typed
    /// *PayloadDefinition subclasses — see PayloadSpecificStats.
    /// See docs/ai/weapon-builder/README.md
    /// </summary>
    [Serializable]
    public struct CommonPayloadStats
    {
        public float Damage;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float HeadshotDamageMultiplier;
        public float BasePenetration;
        public float BaseArmorDamage;
        public float BaseBleedChance;
        // NOTE: AmmoType is an identifier, not a stat — lives on PayloadCoreDefinition.
    }

    /// <summary>
    /// Stats contributed by a Delivery Core. Applies to all patterns uniformly;
    /// pattern-specific params (SpinUpTime, VolleyCount, etc.) live on DeliveryCoreDefinition.
    /// See docs/ai/weapon-builder/README.md
    /// </summary>
    [Serializable]
    public struct DeliveryStats
    {
        // Fire pattern
        public float FireInterval;
        public int   ProjectilesPerShot;
        public float SpreadAngle;

        // Aiming / weapon feel
        public float ConeHalfAngle;
        public float BodyRotationSpeed;
        public float AimFollowSharpness;

        // Recoil
        public float RecoilKickForward;
        public float RecoilKickSide;
        public float RecoilRecoverySpeed;

        // Equip lifecycle
        public float EquipTime;
        public float UnequipTime;

        // Ammo / reload
        public int   MagazineSize;
        public float ReloadTime;
    }

    /// <summary>
    /// Final computed weapon stats — result of composing Payload + Delivery
    /// at the chosen rarity tiers. Cached on WeaponEntityState at assembly/equip time.
    /// Ammo modifiers are applied separately in ShootingSystem at fire time.
    /// See docs/ai/weapon-builder/README.md
    /// </summary>
    [Serializable]
    public struct WeaponStats
    {
        // ---- From Payload (7 numeric; AmmoType identifier lives on PayloadCoreDefinition) ----
        public float Damage;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float HeadshotDamageMultiplier;
        public float BasePenetration;
        public float BaseArmorDamage;
        public float BaseBleedChance;

        // ---- From Delivery (13) ----
        public float FireInterval;
        public int   ProjectilesPerShot;
        public float SpreadAngle;
        public float ConeHalfAngle;
        public float BodyRotationSpeed;
        public float AimFollowSharpness;
        public float RecoilKickForward;
        public float RecoilKickSide;
        public float RecoilRecoverySpeed;
        public float EquipTime;
        public float UnequipTime;
        public int   MagazineSize;
        public float ReloadTime;

        // ---- From Attachments (no core baseline) ----
        // Sniper-scope reveal radius in meters, granted by an optic mod. 0 = no scope.
        // Consumed by PlayerVisionSystem → PlayerFOVSystem + camera/fog-of-war view.
        public float SightRangeBonus;
    }
}
