using System;
using System.Collections.Generic;
using Adapters;
using State;

namespace Systems
{
    /// <summary>
    /// Non-stat "loadout" summary of a weapon for the compare panel: which ammo it feeds on
    /// (+ the player's reserve of that ammo) and the mods installed on it. Complements the
    /// numeric stat-diff (<see cref="WeaponStatComparison"/>) with the two non-numeric decision
    /// factors — "can I feed it?" and "what mods come with it?". Pure C# — unit-tested.
    /// </summary>
    public static class WeaponLoadoutSummary
    {
        public readonly struct ModEntry
        {
            public readonly string Slot;
            public readonly string Name;
            public ModEntry(string slot, string name) { Slot = slot ?? string.Empty; Name = name ?? string.Empty; }
        }

        public readonly struct Summary
        {
            public readonly string AmmoName;    // display name (e.g. "Rifle Ammo"); empty if unknown
            public readonly int AmmoLoaded;     // rounds currently in the weapon's magazine
            public readonly int AmmoReserve;    // player's reserve count of that ammo type
            public readonly IReadOnlyList<ModEntry> Mods;

            public Summary(string ammoName, int ammoLoaded, int ammoReserve, IReadOnlyList<ModEntry> mods)
            {
                AmmoName = ammoName ?? string.Empty;
                AmmoLoaded = ammoLoaded;
                AmmoReserve = ammoReserve;
                Mods = mods ?? Array.Empty<ModEntry>();
            }
        }

        /// <param name="loadedRounds">
        /// Rounds in the magazine. The caller supplies this because the LIVE count of an equipped
        /// weapon lives in the runtime <c>WeaponEntityState.Hotbar</c> (the config's stored value
        /// is stale mid-raid); for loot/backpack weapons it's the config's AmmoInMagazine.
        /// </param>
        public static Summary Build(ItemState weapon, ICoreDefinitionRegistry registry,
                                    InventoryState playerInventory, int loadedRounds)
        {
            string ammoName = string.Empty;
            int reserve = 0;
            var mods = new List<ModEntry>();

            if (weapon == null || !weapon.HasWeaponConfiguration)
                return new Summary(ammoName, 0, reserve, mods);

            var cfg = weapon.WeaponConfiguration;
            int loaded = loadedRounds;

            // Ammo type is a property of the payload core.
            if (registry != null
                && registry.TryGetPayload(cfg.Payload.DefinitionId, out var payload) && payload != null
                && !string.IsNullOrEmpty(payload.AmmoType))
            {
                var ammoType = payload.AmmoType;
                var ammoDef = ItemDefinition.Get(ammoType);
                ammoName = ammoDef != null ? ammoDef.DisplayName : ammoType;
                if (playerInventory != null)
                    reserve = AmmoSystem.CountReserve(playerInventory, ammoType);
            }

            // Installed mods (slot → display name).
            var atts = cfg.Attachments;
            if (atts != null)
            {
                for (int i = 0; i < atts.Length; i++)
                {
                    if (string.IsNullOrEmpty(atts[i].DefinitionId)) continue;
                    string name = registry != null
                                  && registry.TryGetAttachment(atts[i].DefinitionId, out var mdef) && mdef != null
                        ? mdef.DisplayName
                        : atts[i].DefinitionId;
                    mods.Add(new ModEntry(atts[i].Slot.ToString(), name));
                }
            }

            return new Summary(ammoName, loaded, reserve, mods);
        }
    }
}
