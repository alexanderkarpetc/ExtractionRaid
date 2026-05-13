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

        /// <summary>
        /// Burst fire window — used by laser+Auto delivery. After release-fire спалює
        /// N projectiles at fixed interval, then transitions до Cooldown. Player can't
        /// re-trigger fire during burst — locked until burst exhausts.
        /// </summary>
        Bursting,
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
        /// <summary>
        /// Stable string identifier for this weapon's hand prefab. Used as event payload
        /// (WeaponDryFired/WeaponReloadStarted/...) and by Raid State Debugger UI.
        /// Tier 8.x* — mirrors <see cref="BasePrefab"/>.name (payload prefab name).
        /// Used as event payload string (WeaponDryFired/Reload/...) and Raid State Debugger UI.
        /// </summary>
        public string PrefabId;
        /// <summary>
        /// Tier 8.x* — payload base prefab (weapon root). Instantiated як child of
        /// CharacterBody.WeaponPivot at equip time. Resolved from
        /// <see cref="PayloadDefinition"/>.BasePrefab.
        /// </summary>
        public UnityEngine.GameObject BasePrefab;
        /// <summary>
        /// Tier 8.x* — delivery barrel prefab (ствол / emitter). Instantiated як child of
        /// payload's DeliverySocket at equip time. Resolved from
        /// <see cref="DeliveryDefinition"/>.BarrelPrefab.
        /// </summary>
        public UnityEngine.GameObject BarrelPrefab;

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

        /// <summary>
        /// Burst state (laser+Auto). Number of shots remaining in current burst —
        /// decrements per shot; transitions to Cooldown коли reaches 0.
        /// Meaningful only while Phase == Bursting.
        /// </summary>
        public int         BurstShotsRemaining;
        /// <summary>
        /// Charge ratio captured at burst trigger time — все shots у burst use this
        /// value for damage scaling + VFX intensity. Меaningful only while Bursting.
        /// </summary>
        public float       BurstChargeRatio;
        /// <summary>
        /// Time of last burst shot — driver for inter-shot interval pacing within Bursting.
        /// </summary>
        public float       LastBurstShotTime;

        /// <summary>
        /// Ballistic Rifle signature mechanic (B1) — sustained-fire "barrel heat" 0..1.
        /// Each Ballistic+Auto shot increments; <see cref="Systems.WeaponHeatSystem"/> decays
        /// continuously. Heat multiplies spread via parabolic curve у ShootingSystem. Persists
        /// across reload + weapon swap (only decays through time). 0 for non-Ballistic+Auto
        /// archetypes (no increment path).
        /// </summary>
        public float       HeatLevel;
    }
}
