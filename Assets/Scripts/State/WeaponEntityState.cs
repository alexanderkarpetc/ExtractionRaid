using UnityEngine;

namespace State
{
    public enum WeaponPhase : byte
    {
        Ready,
        Firing,
        Cooldown,
        Equipping,
        Unequipping,
        Reloading,
    }

    /// <summary>
    /// Runtime state of a weapon: composition (what was assembled), cached computed
    /// stats, and mutable runtime fields (phase, ammo, recoil).
    ///
    /// Composition refs identify the modules from which the weapon was built.
    /// They are null-equivalent for weapons created directly by legacy factories
    /// or by <c>BotSpawnSystem</c> (those weapons still have valid <see cref="Stats"/>
    /// and runtime fields, just no builder identity).
    ///
    /// <see cref="Stats"/> is the single source of truth for gameplay reads —
    /// ShootingSystem, AimingSystem, etc. read from this cache. It is populated
    /// either by the assembly pipeline (player weapons via WeaponSyncSystem) or
    /// directly in factory code (legacy path, bots).
    ///
    /// See docs/ai/weapon-builder/architecture.md §1, §D1.
    /// </summary>
    public class WeaponEntityState
    {
        public EId Id;
        public string PrefabId;

        // ── Composition (identity) ─────────────────────────
        public PayloadCoreInstance  PayloadCore;
        public DeliveryCoreInstance DeliveryCore;
        public bool                 HasExotic;
        public ExoticModInstance    ExoticMod;

        // ── Resolved definition refs (cache; null for non-builder weapons) ──
        public PayloadCoreDefinition  PayloadDefinition;
        public DeliveryCoreDefinition DeliveryDefinition;
        public ExoticModDefinition    ExoticDefinition;

        // ── Cached computed stats ──────────────────────────
        public WeaponStats Stats;

        // ── Denormalized identifiers (set by assembly OR factory) ──
        /// <summary>
        /// Ammo type identifier used by <c>AmmoSystem</c> / <c>ShootingSystem</c>.
        /// For builder-assembled weapons, mirrors <see cref="PayloadDefinition"/>'s
        /// AmmoType. For bot weapons, may be null.
        /// </summary>
        public string AmmoType;

        // ── Runtime state ──────────────────────────────────
        public int         AmmoInMagazine;
        public float       LastFireTime;
        public WeaponPhase Phase;
        public float       PhaseStartTime;
        public Vector3     RecoilOffset;

        // ── Legacy factories (removed in Tier 0b Cluster E) ──

        public static WeaponEntityState CreateRifle(EId id)
        {
            return new WeaponEntityState
            {
                Id             = id,
                PrefabId       = "Weapon_Rifle",
                PayloadCore    = new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                DeliveryCore   = new DeliveryCoreInstance("Auto",          RarityTier.Common),
                AmmoType       = "Ammo_Rifle",
                AmmoInMagazine = 30,
                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
                Stats = new WeaponStats
                {
                    Damage                   = 10f,
                    ProjectileSpeed          = 20f,
                    ProjectileLifetime       = 3f,
                    HeadshotDamageMultiplier = 2f,
                    BasePenetration          = 20f,
                    BaseArmorDamage          = 5f,
                    BaseBleedChance          = 0f,
                    ProjectilesPerShot       = 1,
                    SpreadAngle              = 0f,
                    ConeHalfAngle            = 45f,
                    BodyRotationSpeed        = 270f,
                    AimFollowSharpness       = 10f,
                    RecoilKickForward        = 2f,
                    RecoilKickSide           = 1.5f,
                    RecoilRecoverySpeed      = 2f,
                    EquipTime                = 0.3f,
                    UnequipTime              = 0.2f,
                    MagazineSize             = 30,
                    ReloadTime               = 2.0f,
                },
            };
        }

        public static WeaponEntityState CreateShotgun(EId id)
        {
            return new WeaponEntityState
            {
                Id             = id,
                PrefabId       = "Weapon_Shotgun",
                PayloadCore    = new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                DeliveryCore   = new DeliveryCoreInstance("Scatter",       RarityTier.Common),
                AmmoType       = "Ammo_Shotgun",
                AmmoInMagazine = 5,
                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
                Stats = new WeaponStats
                {
                    Damage                   = 8f,
                    ProjectileSpeed          = 30f,
                    ProjectileLifetime       = 2f,
                    HeadshotDamageMultiplier = 1.5f,
                    BasePenetration          = 10f,
                    BaseArmorDamage          = 4f,
                    BaseBleedChance          = 0f,
                    ProjectilesPerShot       = 7,
                    SpreadAngle              = 30f,
                    ConeHalfAngle            = 20f,
                    BodyRotationSpeed        = 180f,
                    AimFollowSharpness       = 5f,
                    RecoilKickForward        = 3f,
                    RecoilKickSide           = 6f,
                    RecoilRecoverySpeed      = 3f,
                    EquipTime                = 0.4f,
                    UnequipTime              = 0.25f,
                    MagazineSize             = 5,
                    ReloadTime               = 2.5f,
                },
            };
        }

        public static WeaponEntityState CreatePistol(EId id)
        {
            return new WeaponEntityState
            {
                Id             = id,
                PrefabId       = "Weapon_Pistol",
                PayloadCore    = new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                DeliveryCore   = new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                AmmoType       = "Ammo_Pistol",
                AmmoInMagazine = 12,
                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
                Stats = new WeaponStats
                {
                    Damage                   = 15f,
                    ProjectileSpeed          = 25f,
                    ProjectileLifetime       = 2.5f,
                    HeadshotDamageMultiplier = 2.5f,
                    BasePenetration          = 15f,
                    BaseArmorDamage          = 6f,
                    BaseBleedChance          = 0f,
                    ProjectilesPerShot       = 1,
                    SpreadAngle              = 0f,
                    ConeHalfAngle            = 35f,
                    BodyRotationSpeed        = 300f,
                    AimFollowSharpness       = 15f,
                    RecoilKickForward        = 1.5f,
                    RecoilKickSide           = 1f,
                    RecoilRecoverySpeed      = 4f,
                    EquipTime                = 0.2f,
                    UnequipTime              = 0.15f,
                    MagazineSize             = 12,
                    ReloadTime               = 1.5f,
                },
            };
        }

        public static WeaponEntityState CreateFromDefinitionId(EId id, string definitionId)
        {
            return definitionId switch
            {
                "Rifle" => CreateRifle(id),
                "Shotgun" => CreateShotgun(id),
                "Pistol" => CreatePistol(id),
                _ => null,
            };
        }
    }
}
