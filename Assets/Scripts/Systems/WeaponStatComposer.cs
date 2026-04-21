using State;

namespace Systems
{
    /// <summary>
    /// Pure function that composes <see cref="WeaponStats"/> from a Payload + Delivery
    /// pair at chosen rarity tiers. Caller is responsible for already having resolved
    /// the definitions (typically via <see cref="Adapters.ICoreDefinitionRegistry"/>).
    ///
    /// Field mapping (see docs/ai/weapon-builder/architecture.md §D1):
    ///   - 7 numeric stats come from Payload
    ///   - 13 stats come from Delivery
    ///   - no overlap; each field is sourced from exactly one side
    ///
    /// Ammo modifiers are NOT applied here — they are a separate channel,
    /// composed into projectiles at <c>ShootingSystem</c> fire time.
    ///
    /// Exotic Mod stat modifiers (Tier 5) will compose as a final pass;
    /// for now the parameter is accepted and ignored.
    /// </summary>
    public static class WeaponStatComposer
    {
        public static WeaponStats Compose(
            PayloadCoreDefinition  payload,
            RarityTier             payloadRarity,
            DeliveryCoreDefinition delivery,
            RarityTier             deliveryRarity,
            ExoticModDefinition    exotic = null)
        {
            var ps = payload.StatsByTier(payloadRarity);
            var ds = delivery.StatsByTier(deliveryRarity);

            var stats = new WeaponStats
            {
                // From Payload (7 numeric)
                Damage                   = ps.Damage,
                ProjectileSpeed          = ps.ProjectileSpeed,
                ProjectileLifetime       = ps.ProjectileLifetime,
                HeadshotDamageMultiplier = ps.HeadshotDamageMultiplier,
                BasePenetration          = ps.BasePenetration,
                BaseArmorDamage          = ps.BaseArmorDamage,
                BaseBleedChance          = ps.BaseBleedChance,

                // From Delivery (13)
                FireInterval        = ds.FireInterval,
                ProjectilesPerShot  = ds.ProjectilesPerShot,
                SpreadAngle         = ds.SpreadAngle,
                ConeHalfAngle       = ds.ConeHalfAngle,
                BodyRotationSpeed   = ds.BodyRotationSpeed,
                AimFollowSharpness  = ds.AimFollowSharpness,
                RecoilKickForward   = ds.RecoilKickForward,
                RecoilKickSide      = ds.RecoilKickSide,
                RecoilRecoverySpeed = ds.RecoilRecoverySpeed,
                EquipTime           = ds.EquipTime,
                UnequipTime         = ds.UnequipTime,
                MagazineSize        = ds.MagazineSize,
                ReloadTime          = ds.ReloadTime,
            };

            // TODO (Tier 5): apply exotic.StatsModifier here when the shape is defined.
            _ = exotic;

            return stats;
        }
    }
}
