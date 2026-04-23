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
        /// <summary>
        /// Charge-up window before a shot can be released. Used by charge-up payloads
        /// (e.g. Laser). Weapon transitions Ready → Charging → Firing once
        /// ChargeTime elapses, or Charging → Ready if attack is released early.
        /// See docs/ai/weapon-builder/architecture.md §D2.
        /// </summary>
        Charging,
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
        /// <summary>
        /// Time at which the current <see cref="WeaponPhase.Charging"/> window began.
        /// Meaningful only while Phase == Charging; otherwise untouched.
        /// </summary>
        public float       ChargeStartTime;
    }
}
