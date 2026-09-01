using Adapters;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Pure function that composes <see cref="WeaponStats"/> from a Payload + Delivery
    /// pair at chosen rarity tiers. Caller is responsible for already having resolved
    /// the definitions (typically via <see cref="Adapters.ICoreDefinitionRegistry"/>).
    ///
    /// Field mapping:
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

        /// <summary>
        /// Applies installed attachment stat deltas on top of composed Payload+Delivery
        /// stats (delta option A — player-facing axes mapped to raw fields here). Unknown
        /// or empty attachment instances are skipped (attachments are non-critical, unlike
        /// cores which fail the assembly). Returns a new <see cref="WeaponStats"/>.
        /// See docs/ai/weapons.md.
        /// </summary>
        public static WeaponStats ApplyAttachments(
            WeaponStats stats,
            in WeaponConfiguration config,
            ICoreDefinitionRegistry registry)
        {
            var mods = config.Attachments;
            if (mods == null || registry == null) return stats;

            for (int i = 0; i < mods.Length; i++)
            {
                if (string.IsNullOrEmpty(mods[i].DefinitionId)) continue;
                if (!registry.TryGetAttachment(mods[i].DefinitionId, out var def) || def == null) continue;

                var deltas = def.Modifiers;
                for (int j = 0; j < deltas.Count; j++)
                    stats = ApplyAxisDelta(stats, deltas[j].Axis, deltas[j].Percent);
            }
            return stats;
        }

        // Maps a player-facing axis delta (whole percent; raw-change semantics) onto the
        // raw WeaponStats field(s) it drives. Factor clamped > 0 to stay safe past -100%.
        static WeaponStats ApplyAxisDelta(WeaponStats s, WeaponStatAxis axis, float percent)
        {
            float f = Mathf.Max(0.01f, 1f + percent / 100f);
            switch (axis)
            {
                case WeaponStatAxis.Damage:
                    s.Damage *= f;
                    break;
                case WeaponStatAxis.RateOfFire:
                    if (s.FireInterval > 0f) s.FireInterval /= f; // faster fire = lower interval
                    break;
                case WeaponStatAxis.MagazineSize:
                    s.MagazineSize = Mathf.Max(1, Mathf.RoundToInt(s.MagazineSize * f));
                    break;
                case WeaponStatAxis.ReloadTime:
                    s.ReloadTime *= f;
                    break;
                case WeaponStatAxis.Recoil:
                    s.RecoilKickForward *= f;
                    s.RecoilKickSide    *= f;
                    break;
                case WeaponStatAxis.Spread:
                    s.SpreadAngle *= f;
                    break;
                case WeaponStatAxis.Ergonomics:
                    // Higher ergonomics = better handling: faster draw/holster, faster turn.
                    s.EquipTime         /= f;
                    s.UnequipTime       /= f;
                    s.BodyRotationSpeed *= f;
                    break;
                case WeaponStatAxis.SightRange:
                    // Additive raw meters (base is 0 — a scope grants reveal radius outright),
                    // so the percent value is read as meters, not a multiplier.
                    s.SightRangeBonus += percent;
                    break;
                case WeaponStatAxis.ProjectileSpeed:
                    s.ProjectileSpeed *= f;
                    break;
                case WeaponStatAxis.Headshot:
                    s.HeadshotDamageMultiplier *= f;
                    break;
            }
            return s;
        }
    }
}
