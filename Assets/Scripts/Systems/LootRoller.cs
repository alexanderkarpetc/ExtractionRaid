using System.Collections.Generic;
using Constants;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// The dice behind every loot roll. Loot configs supply the SHAPE (which buckets, how many
    /// entries, which hardcoded items); this resolves it against
    /// <see cref="ItemBalanceAsset"/> — the item that comes out of a bucket, and the stack size
    /// it comes out as. Stateless; used by <see cref="LootSystem"/> (live raid),
    /// <see cref="Meta.RegionLootSimulator"/> (editor sim) and loose-loot spawn points.
    ///
    /// An item with an explicit DropWeight of 0 in the balance table is invisible here — that's
    /// how a designer retires something from loot without deleting it from the game.
    /// </summary>
    public static class LootRoller
    {
        /// <summary>
        /// Rolls one entry out of a container pool and resolves it to a concrete item id.
        /// False when the pool is empty or every candidate is weighted out.
        /// </summary>
        public static bool TryRollPool(LootPoolEntry[] pool, out string definitionId)
        {
            definitionId = null;
            if (pool == null || pool.Length == 0) return false;

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].Weight > 0f) total += pool[i].Weight;
            if (total <= 0f) return false;

            float r = Random.value * total;
            float acc = 0f;
            int chosen = -1;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].Weight <= 0f) continue;
                acc += pool[i].Weight;
                chosen = i;
                if (r <= acc) break;
            }
            if (chosen < 0) return false;

            var entry = pool[chosen];
            definitionId = entry.IsCategory ? PickFromCategory(entry.Category) : entry.DefinitionId;
            return !string.IsNullOrEmpty(definitionId) && ItemDefinition.Get(definitionId) != null;
        }

        /// <summary>
        /// Balance-weighted pick out of one <see cref="LootCategory"/> bucket. Ids listed in
        /// <paramref name="exclude"/> are skipped, which is how a caller gets DISTINCT picks
        /// across several rolls. Null when nothing in the bucket can drop.
        /// </summary>
        public static string PickFromCategory(LootCategory category, List<string> exclude = null)
        {
            var candidates = LootConstants.CandidatesFor(category);
            if (candidates.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (exclude != null && exclude.Contains(candidates[i].Id)) continue;
                total += ItemBalanceAsset.DropWeightOf(candidates[i].Id);
            }
            if (total <= 0f) return null;

            float r = Random.value * total;
            string chosen = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (exclude != null && exclude.Contains(candidates[i].Id)) continue;
                float w = ItemBalanceAsset.DropWeightOf(candidates[i].Id);
                if (w <= 0f) continue;
                r -= w;
                chosen = candidates[i].Id;
                if (r <= 0f) break;
            }
            return chosen;
        }

        /// <summary>Balance-weighted pick out of an explicit id list (a curated pool).</summary>
        public static string PickWeighted(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < ids.Count; i++) total += ItemBalanceAsset.DropWeightOf(ids[i]);
            if (total <= 0f) return null;

            float r = Random.value * total;
            string chosen = null;
            for (int i = 0; i < ids.Count; i++)
            {
                float w = ItemBalanceAsset.DropWeightOf(ids[i]);
                if (w <= 0f) continue;
                r -= w;
                chosen = ids[i];
                if (r <= 0f) break;
            }
            return chosen;
        }

        /// <summary>Stack size for one drop of this item, from the balance table.</summary>
        public static int RollCount(string definitionId)
        {
            ItemBalanceAsset.DropCountRangeOf(definitionId, out int min, out int max);
            return Random.Range(min, max + 1);
        }

        /// <summary>Stack size for a hardcoded drop: its own range, or the balance table's
        /// when the drop opted into it.</summary>
        public static int RollCount(in LootDrop drop)
        {
            if (drop.CountFromBalance) return RollCount(drop.DefinitionId);
            return Random.Range(drop.MinCount, drop.MaxCount + 1);
        }
    }
}
