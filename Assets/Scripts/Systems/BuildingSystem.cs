using System.Collections.Generic;
using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Building level + upgrade rules. Pure helpers — no Unity APIs, no events.
    /// Levels live on <see cref="Player.BuildingLevels"/> (persistent across raids);
    /// materials are consumed from <see cref="Player.Stash"/> first and then from
    /// <see cref="Player.Inventory"/>'s backpack, so the player's raid loadout is
    /// preserved when stash supply is enough.
    /// </summary>
    public static class BuildingSystem
    {
        public readonly struct UpgradeRequirement
        {
            public readonly string ItemId;
            public readonly int Available;
            public readonly int Required;

            public bool IsMet => Available >= Required;

            public UpgradeRequirement(string itemId, int available, int required)
            {
                ItemId = itemId;
                Available = available;
                Required = required;
            }
        }

        public static int GetLevel(Player player, BuildingKind kind) =>
            player?.GetBuildingLevel(kind) ?? 0;

        public static bool IsMaxLevel(Player player, BuildingKind kind) =>
            GetLevel(player, kind) >= BuildingConstants.MaxLevel;

        /// <summary>
        /// Recipe for the upgrade from current level → current+1. Null if at max or
        /// the kind has no recipe table.
        /// </summary>
        public static BuildingIngredient[] GetNextUpgradeRecipe(Player player, BuildingKind kind)
        {
            if (player == null) return null;
            return BuildingConstants.GetUpgradeRecipe(kind, GetLevel(player, kind));
        }

        /// <summary>UI-ready material counts for the player's next upgrade step.</summary>
        public static List<UpgradeRequirement> GetNextUpgradeRequirements(
            Player player, BuildingKind kind)
        {
            var result = new List<UpgradeRequirement>();
            var recipe = GetNextUpgradeRecipe(player, kind);
            if (recipe == null) return result;

            for (int i = 0; i < recipe.Length; i++)
                result.Add(new UpgradeRequirement(
                    recipe[i].ItemId,
                    GetAvailable(player, recipe[i].ItemId),
                    recipe[i].Count));

            return result;
        }

        /// <summary>
        /// True if Stash + Backpack combined hold enough of every material in the
        /// next-level recipe. False at max level (no recipe).
        /// </summary>
        public static bool CanAffordUpgrade(Player player, BuildingKind kind)
        {
            var recipe = GetNextUpgradeRecipe(player, kind);
            if (recipe == null || recipe.Length == 0) return false;
            if (player.Inventory == null) return false;

            for (int i = 0; i < recipe.Length; i++)
                if (CountAvailable(player, recipe[i].ItemId) < recipe[i].Count)
                    return false;
            return true;
        }

        /// <summary>
        /// Consumes the next-level recipe (Stash first, Backpack as fallback) and
        /// increments the building's level by 1. Returns false (no state changes) if
        /// at max level or materials are missing.
        /// </summary>
        public static bool TryUpgrade(Player player, BuildingKind kind)
        {
            if (!CanAffordUpgrade(player, kind)) return false;

            var recipe = GetNextUpgradeRecipe(player, kind);

            for (int i = 0; i < recipe.Length; i++)
                ConsumeMaterial(player, recipe[i].ItemId, recipe[i].Count);

            int newLevel = GetLevel(player, kind) + 1;
            player.SetBuildingLevel(kind, newLevel);
            if (ApplicationCore.App.IsInitialized)
                QuestSystem.OnBuildingUpgraded(player.QuestProgress,
                    ApplicationCore.App.Instance.QuestDatabase, kind, newLevel);
            Debug.Log($"[BuildingSystem] Upgraded {kind} → Lv. {newLevel}.");
            return true;
        }

        // ── Counting / consuming across both containers ────────────────────

        /// <summary>
        /// Total stack count of <paramref name="itemId"/> across the player's stash
        /// AND backpack. Exposed for HUD/dialogue code that needs to show "have / need".
        /// </summary>
        public static int GetAvailable(Player player, string itemId)
        {
            if (player == null) return 0;
            return CountInStash(player.Stash, itemId) + CountInBackpack(player.Inventory, itemId);
        }

        /// <summary>
        /// Takes <paramref name="amount"/> of an item from the Stash first, then the Backpack.
        /// Assumes the caller already checked supply (see <see cref="GetAvailable"/>) — it
        /// consumes whatever it can find and silently stops when both containers run dry.
        /// Shared with <see cref="ProgressionCostSystem"/> so both spend materials the same way.
        /// </summary>
        public static void ConsumeMaterial(Player player, string itemId, int amount)
        {
            if (player == null || amount <= 0) return;
            int remaining = ConsumeFromStash(player.Stash, itemId, amount);
            if (remaining > 0 && player.Inventory != null)
                ConsumeFromBackpack(player.Inventory, itemId, remaining);
        }

        static int CountAvailable(Player player, string itemId) =>
            CountInStash(player.Stash, itemId) + CountInBackpack(player.Inventory, itemId);

        static int CountInBackpack(InventoryState inv, string itemId)
        {
            int count = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var slot = inv.Backpack[i];
                if (slot != null && slot.DefinitionId == itemId)
                    count += slot.StackCount;
            }
            return count;
        }

        static int CountInStash(List<ItemState> stash, string itemId)
        {
            if (stash == null) return 0;
            int count = 0;
            for (int i = 0; i < stash.Count; i++)
            {
                var item = stash[i];
                if (item != null && item.DefinitionId == itemId)
                    count += item.StackCount;
            }
            return count;
        }

        // Backpack uses fixed slots — null out drained stacks in place.
        static void ConsumeFromBackpack(InventoryState inv, string itemId, int amount)
        {
            int remaining = amount;
            for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
            {
                var slot = inv.Backpack[i];
                if (slot == null || slot.DefinitionId != itemId) continue;
                if (slot.StackCount <= remaining)
                {
                    remaining -= slot.StackCount;
                    inv.Backpack[i] = null;
                }
                else
                {
                    slot.StackCount -= remaining;
                    remaining = 0;
                }
            }
        }

        // Stash is a List — remove drained entries so the container stays compact.
        // Returns whatever the caller still needs to consume after the stash dries up.
        static int ConsumeFromStash(List<ItemState> stash,
            string itemId, int amount)
        {
            if (stash == null) return amount;
            int remaining = amount;
            for (int i = stash.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var item = stash[i];
                if (item == null || item.DefinitionId != itemId) continue;
                if (item.StackCount <= remaining)
                {
                    remaining -= item.StackCount;
                    stash.RemoveAt(i);
                }
                else
                {
                    item.StackCount -= remaining;
                    remaining = 0;
                }
            }
            return remaining;
        }
    }
}
