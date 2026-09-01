using System.Collections.Generic;
using State;

namespace View.UI.Inventory
{
    /// <summary>
    /// Picks the baseline weapon to compare a hovered loot/inventory weapon against.
    ///
    /// Our two hotbar weapon slots are generic (no primary/secondary role), so — unlike
    /// STALKER 2 / Division (same-slot compare) — we can't auto-pick by slot type. Instead the
    /// baseline defaults to the weapon in hand (selected slot) and the player flips to the other
    /// equipped weapon with a key (Alt). See docs/ai/weapons.md.
    ///
    /// Pure C# (no engine refs) — unit-tested.
    /// </summary>
    public static class WeaponCompareTarget
    {
        /// <summary>
        /// Equipped weapons eligible as a compare baseline, ordered selected-first then the rest.
        /// Excludes empty/non-weapon slots and the hovered instance itself (no compare-to-self).
        /// </summary>
        public static IReadOnlyList<ItemState> Candidates(
            IReadOnlyList<ItemState> weaponSlots, int selectedSlot, ItemState hovered)
        {
            var list = new List<ItemState>();
            if (weaponSlots == null) return list;

            TryAdd(list, weaponSlots, selectedSlot, hovered);
            for (int i = 0; i < weaponSlots.Count; i++)
                if (i != selectedSlot) TryAdd(list, weaponSlots, i, hovered);

            return list;
        }

        /// <summary>The baseline at <paramref name="flipStep"/> (wraps), or null when no candidates.</summary>
        public static ItemState Pick(IReadOnlyList<ItemState> candidates, int flipStep)
        {
            if (candidates == null || candidates.Count == 0) return null;
            int idx = ((flipStep % candidates.Count) + candidates.Count) % candidates.Count;
            return candidates[idx];
        }

        static void TryAdd(List<ItemState> list, IReadOnlyList<ItemState> slots, int i, ItemState hovered)
        {
            if (i < 0 || i >= slots.Count) return;
            var w = slots[i];
            if (w == null || !w.HasWeaponConfiguration) return;
            if (ReferenceEquals(w, hovered)) return; // hovering an equipped weapon → don't compare to itself
            if (!list.Contains(w)) list.Add(w);
        }
    }
}
