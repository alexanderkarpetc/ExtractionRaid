using System.Collections.Generic;
using Constants;
using Progression;
using Quests;
using Session;
using State;
using UnityEngine;

namespace Systems.Meta
{
    /// <summary>
    /// "What does the player actually need RIGHT NOW?" — the shopping list the DevCheats
    /// <c>🌍 Meta → Region raid simulator</c> loots against, so a sim raid brings home
    /// progression materials first and only then fills the leftover slots with valuables.
    ///
    /// Deliberately scoped to the <b>current</b> step of each track, never the whole game:
    /// <list type="bullet">
    ///   <item>Quests — hand-in items for ACTIVE quests only (plus craft-task ingredients).</item>
    ///   <item>Hideout — the current→next level recipe of each building, not the whole ladder.</item>
    ///   <item>Skill tree — only nodes that are connected and unallocated (unlockable today).</item>
    /// </list>
    /// Everything already owned (stash + backpack) is subtracted, so a covered line stops
    /// asking. Stateless, no <c>App</c> / no editor deps — callers pass what they hold.
    /// </summary>
    public static class MetaNeeds
    {
        /// <summary>Which track wants an item. Lower value = looted first.</summary>
        public enum NeedSource { Quest = 0, Hideout = 1, Skill = 2 }

        /// <summary>One outstanding line of the shopping list.</summary>
        public struct Need
        {
            public string ItemId;
            public int Count;           // still missing after stash + backpack
            public NeedSource Source;
            public string Reason;       // quest / building / node it belongs to
        }

        /// <summary>
        /// Everything the player is short of for their current quests, next hideout
        /// upgrades and reachable skill nodes. Lines for the same item from different
        /// tracks stay separate (see <see cref="ToQuotas"/> for the merged view).
        /// </summary>
        public static List<Need> Collect(Player player, QuestDatabase questDb, ProgressionTreeConfig tree)
        {
            var needs = new List<Need>();
            if (player == null) return needs;

            // Running demand per item, so two tracks asking for the same material don't
            // both get told "you already own it" off the same single pile.
            var demand = new Dictionary<string, int>();

            CollectQuestNeeds(player, questDb, needs, demand);
            CollectHideoutNeeds(player, needs, demand);
            CollectSkillNeeds(player, tree, needs, demand);
            return needs;
        }

        /// <summary>
        /// Merged per-item quota for the loot filler: how many units to grab, and the
        /// most urgent track asking for them.
        /// </summary>
        public static Dictionary<string, RegionLootSimulator.NeedQuota> ToQuotas(List<Need> needs)
        {
            var quotas = new Dictionary<string, RegionLootSimulator.NeedQuota>();
            if (needs == null) return quotas;

            foreach (var n in needs)
            {
                if (string.IsNullOrEmpty(n.ItemId) || n.Count <= 0) continue;
                if (!quotas.TryGetValue(n.ItemId, out var q))
                    q = new RegionLootSimulator.NeedQuota { Priority = (int)n.Source };
                q.Remaining += n.Count;
                q.Priority = Mathf.Min(q.Priority, (int)n.Source);
                quotas[n.ItemId] = q;
            }
            return quotas;
        }

        /// <summary>One-line-per-track summary for the DevCheats readout.</summary>
        public static string Describe(List<Need> needs)
        {
            if (needs == null || needs.Count == 0) return "Nothing needed — quests, hideout and skills are all covered.";

            var sb = new System.Text.StringBuilder();
            int quest = 0, hideout = 0, skill = 0;
            foreach (var n in needs)
                switch (n.Source)
                {
                    case NeedSource.Quest: quest++; break;
                    case NeedSource.Hideout: hideout++; break;
                    default: skill++; break;
                }
            sb.Append($"Needs now: {quest} quest, {hideout} hideout, {skill} skill line(s).");

            // Show the most urgent handful so the row stays readable.
            int shown = 0;
            foreach (var n in needs)
            {
                if (shown++ >= 6) { sb.Append(" …"); break; }
                sb.Append(shown == 1 ? "\n" : ", ");
                sb.Append($"{NameOf(n.ItemId)}×{n.Count}");
            }
            return sb.ToString();
        }

        // ───────────────────────────────────────────────── Quests ──

        static void CollectQuestNeeds(Player player, QuestDatabase db, List<Need> needs,
            Dictionary<string, int> demand)
        {
            if (db == null || player.QuestProgress == null) return;

            foreach (var entry in db.Entries)
            {
                var quest = entry.Quest;
                if (quest?.Tasks == null || string.IsNullOrEmpty(quest.Id)) continue;

                var p = player.QuestProgress.GetProgress(quest.Id);
                if (p == null || p.Status != QuestStatus.Active) continue;

                for (int i = 0; i < quest.Tasks.Count && i < p.Tasks.Count; i++)
                {
                    var task = quest.Tasks[i];
                    if (task == null) continue;
                    int remaining = task.RequiredCount - p.Tasks[i].CurrentCount;
                    if (remaining <= 0) continue;

                    switch (task)
                    {
                        case FindAndTransferTask t:
                            Add(needs, demand, player, t.QuestItemId, remaining, NeedSource.Quest, quest.DisplayName);
                            break;

                        case FindItemTask t:
                            Add(needs, demand, player, t.ItemId, remaining, NeedSource.Quest, quest.DisplayName);
                            break;

                        // Crafting the result is on the player — the raid can only bring
                        // back the ingredients, so that's what we ask for.
                        case CraftTask t:
                            foreach (var ing in IngredientsFor(t.ItemId))
                                Add(needs, demand, player, ing.DefinitionId, ing.Count * remaining,
                                    NeedSource.Quest, quest.DisplayName);
                            break;
                    }
                }
            }
        }

        static IEnumerable<CraftIngredient> IngredientsFor(string resultItemId)
        {
            if (string.IsNullOrEmpty(resultItemId)) yield break;
            foreach (var r in CraftConstants.GetAll())
            {
                if (r.ResultItemId != resultItemId || r.Ingredients == null) continue;
                foreach (var ing in r.Ingredients) yield return ing;
                yield break; // first matching recipe wins — one way to make a thing
            }
        }

        // ─────────────────────────────────────────────── Hideout ──

        static void CollectHideoutNeeds(Player player, List<Need> needs, Dictionary<string, int> demand)
        {
            foreach (BuildingKind kind in System.Enum.GetValues(typeof(BuildingKind)))
            {
                // Current → next level only. Later levels are explicitly out of scope.
                var recipe = BuildingConstants.GetUpgradeRecipe(kind, player.GetBuildingLevel(kind));
                if (recipe == null) continue;

                string label = $"{kind} Lv.{player.GetBuildingLevel(kind) + 1}";
                for (int i = 0; i < recipe.Length; i++)
                    Add(needs, demand, player, recipe[i].ItemId, recipe[i].Count, NeedSource.Hideout, label);
            }
        }

        // ──────────────────────────────────────────── Skill tree ──

        static void CollectSkillNeeds(Player player, ProgressionTreeConfig tree, List<Need> needs,
            Dictionary<string, int> demand)
        {
            if (tree?.Disciplines == null || player.Progression == null) return;

            foreach (var disc in tree.Disciplines)
                foreach (var branch in disc.Branches)
                    foreach (var node in branch.Nodes)
                    {
                        // Only what's unlockable TODAY: unallocated and hanging off
                        // something already taken. Deeper rings aren't our problem yet.
                        if (node?.Cost == null || node.Cost.Count == 0) continue;
                        if (ProgressionSystem.IsAllocated(player.Progression, node.Id)) continue;
                        if (!ProgressionSystem.IsConnected(player.Progression, branch, node)) continue;

                        string label = string.IsNullOrEmpty(node.DisplayName) ? node.Id : node.DisplayName;
                        foreach (var cost in node.Cost)
                        {
                            // Weapon lines want a specific assembled gun (delivery + payload
                            // + rarity) — the loot roll can't be steered to produce one, so
                            // they're left to the value pass.
                            if (cost == null || cost.IsWeapon) continue;
                            Add(needs, demand, player, cost.ItemId, cost.Quantity, NeedSource.Skill, label);
                        }
                    }
        }

        // ─────────────────────────────────────────────── Shared ──

        // Adds the SHORTFALL only — what stash + backpack already cover isn't a need.
        // Supply is applied against the RUNNING demand for that item, so a pile of 10
        // Cloth covers a 5-Cloth quest and a 5-Cloth recipe once, not twice: the third
        // line asking for Cloth is the one that starts reporting a shortfall.
        static void Add(List<Need> needs, Dictionary<string, int> demand, Player player,
            string itemId, int count, NeedSource source, string reason)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;
            if (ItemDefinition.Get(itemId) == null) return;

            demand.TryGetValue(itemId, out int before);
            int after = before + count;
            demand[itemId] = after;

            int owned = BuildingSystem.GetAvailable(player, itemId);
            int missing = Mathf.Max(0, after - owned) - Mathf.Max(0, before - owned);
            if (missing <= 0) return;

            needs.Add(new Need { ItemId = itemId, Count = missing, Source = source, Reason = reason });
        }

        static string NameOf(string itemId)
            => ItemDefinition.Get(itemId)?.DisplayName ?? itemId;
    }
}
