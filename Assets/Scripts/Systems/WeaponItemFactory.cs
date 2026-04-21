using State;

namespace Systems
{
    /// <summary>
    /// Central helper for spawning weapon <see cref="ItemState"/> instances with an
    /// attached <see cref="WeaponConfiguration"/>. Replaces the compat dictionary that
    /// previously lived inside <see cref="WeaponSyncSystem"/> — configuration is now
    /// decided at spawn time and travels with the item (inventory → ground → inventory).
    ///
    /// Once the in-game Weapon Builder UI (Tier 1) lets the player assemble custom
    /// builds, they will take this same path with player-chosen configurations.
    ///
    /// See docs/ai/weapon-builder/architecture.md §7.
    /// </summary>
    public static class WeaponItemFactory
    {
        /// <summary>
        /// Default <see cref="WeaponConfiguration"/> for a legacy weapon type
        /// (Rifle, Pistol). Returns a default struct for unknown ids — caller should
        /// check <see cref="IsKnownWeaponDefinition"/> first.
        /// </summary>
        public static WeaponConfiguration DefaultConfigFor(string definitionId) => definitionId switch
        {
            "Rifle" => new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("Auto",          RarityTier.Common),
                exotic: null,
                ammoInMagazine: 30),

            "Pistol" => new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12),

            _ => default,
        };

        public static bool IsKnownWeaponDefinition(string definitionId)
            => definitionId is "Rifle" or "Pistol";

        /// <summary>
        /// Spawns an <see cref="ItemState"/> for a legacy weapon definition, attaching the
        /// default <see cref="WeaponConfiguration"/>. Returns null for unknown ids.
        /// </summary>
        public static ItemState SpawnItem(EId id, string definitionId)
        {
            if (!IsKnownWeaponDefinition(definitionId)) return null;
            return ItemState.CreateWeapon(id, definitionId, DefaultConfigFor(definitionId));
        }
    }
}
