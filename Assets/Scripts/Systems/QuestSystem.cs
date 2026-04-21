using System.Collections.Generic;
using Quests;
using State;
using UnityEngine;

namespace Systems
{
    public static class QuestSystem
    {
        /// <summary>
        /// Returns quests that the given NPC can offer (requirements met, not yet started).
        /// </summary>
        public static List<QuestDefinition> GetAvailableQuests(
            QuestProgressState progress, QuestDatabase db, int playerLevel, string npcId)
        {
            if (db == null) return new List<QuestDefinition>();

            var completed = BuildCompletedSet(progress);
            var result = new List<QuestDefinition>();

            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;
                if (entry.Quest.NpcId != npcId) continue;
                if (progress.GetStatus(entry.Quest.Id) != QuestStatus.NotStarted) continue;
                if (!db.AreRequirementsMet(entry.Quest.Id, completed, playerLevel)) continue;

                result.Add(entry.Quest);
            }

            return result;
        }

        /// <summary>
        /// Returns active quests owned by this NPC.
        /// </summary>
        public static List<QuestDefinition> GetActiveQuestsForNpc(
            QuestProgressState progress, QuestDatabase db, string npcId)
        {
            if (db == null) return new List<QuestDefinition>();

            var result = new List<QuestDefinition>();

            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;
                if (entry.Quest.NpcId != npcId) continue;
                if (progress.GetStatus(entry.Quest.Id) != QuestStatus.Active) continue;

                result.Add(entry.Quest);
            }

            return result;
        }

        public static List<QuestDefinition> GetAllActiveQuests(
            QuestProgressState progress, QuestDatabase db)
        {
            if (db == null) return new List<QuestDefinition>();

            var result = new List<QuestDefinition>();
            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;
                if (progress.GetStatus(entry.Quest.Id) != QuestStatus.Active) continue;
                result.Add(entry.Quest);
            }
            return result;
        }

        public static List<QuestDefinition> GetAllCompletedQuests(
            QuestProgressState progress, QuestDatabase db)
        {
            if (db == null) return new List<QuestDefinition>();

            var result = new List<QuestDefinition>();
            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;
                if (progress.GetStatus(entry.Quest.Id) != QuestStatus.Completed) continue;
                result.Add(entry.Quest);
            }
            return result;
        }

        public static List<QuestDefinition> GetCompletedQuestsForNpc(
            QuestProgressState progress, QuestDatabase db, string npcId)
        {
            if (db == null) return new List<QuestDefinition>();

            var result = new List<QuestDefinition>();
            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;
                if (entry.Quest.NpcId != npcId) continue;
                if (progress.GetStatus(entry.Quest.Id) != QuestStatus.Completed) continue;
                result.Add(entry.Quest);
            }
            return result;
        }

        /// <summary>
        /// Credits kill progress on every active quest whose <see cref="KillEnemyTask"/>
        /// matches the given bot type. Caller is responsible for verifying the player
        /// was the killer. Returns true if any task progressed.
        /// </summary>
        public static bool OnEnemyKilled(
            QuestProgressState progress, QuestDatabase db, string killedBotTypeId,
            bool wasHeadshot = false)
        {
            if (progress == null || db == null || string.IsNullOrEmpty(killedBotTypeId))
                return false;

            bool any = false;

            foreach (var kvp in progress.All)
            {
                var qp = kvp.Value;
                if (qp.Status != QuestStatus.Active) continue;
                if (!db.TryGet(qp.QuestId, out var entry) || entry.Quest?.Tasks == null) continue;

                var tasks = entry.Quest.Tasks;
                for (int i = 0; i < tasks.Count && i < qp.Tasks.Count; i++)
                {
                    if (tasks[i] is not KillEnemyTask kill) continue;
                    if (!kill.EnemyType.Matches(killedBotTypeId)) continue;
                    if (kill.HeadshotsOnly && !wasHeadshot) continue;

                    var tp = qp.Tasks[i];
                    if (tp.CurrentCount >= kill.RequiredCount) continue;

                    tp.CurrentCount++;
                    any = true;
                }
            }

            return any;
        }

        public static bool AreAllTasksDone(QuestDefinition quest, QuestProgress p)
        {
            if (quest.Tasks == null || quest.Tasks.Count == 0) return true;
            for (int i = 0; i < quest.Tasks.Count; i++)
            {
                var tp = i < p.Tasks.Count ? p.Tasks[i] : null;
                int current = tp?.CurrentCount ?? 0;
                if (current < quest.Tasks[i].RequiredCount) return false;
            }
            return true;
        }

        public static bool TryAccept(QuestProgressState progress, QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id)) return false;
            if (progress.GetStatus(quest.Id) != QuestStatus.NotStarted) return false;

            progress.StartQuest(quest.Id, quest.Tasks?.Count ?? 0);
            return true;
        }

        public static bool TryComplete(QuestProgressState progress, string questId)
        {
            var p = progress.GetProgress(questId);
            if (p == null || p.Status != QuestStatus.Active) return false;
            progress.CompleteQuest(questId);
            return true;
        }

        /// <summary>
        /// Maxes out all task progress so the quest becomes ready to claim at the NPC.
        /// Quest stays Active — the player must still visit the NPC to claim the reward.
        /// </summary>
        public static bool TryFulfillTasks(QuestProgressState progress, QuestDatabase db, string questId)
        {
            var p = progress.GetProgress(questId);
            if (p == null || p.Status != QuestStatus.Active) return false;

            if (!db.TryGet(questId, out var entry) || entry.Quest == null) return false;
            var tasks = entry.Quest.Tasks;
            if (tasks == null) return true;

            for (int i = 0; i < tasks.Count && i < p.Tasks.Count; i++)
                p.Tasks[i].CurrentCount = tasks[i].RequiredCount;

            return true;
        }

        /// <summary>
        /// Completes a quest and grants reward items to the inventory.
        /// Returns false if the quest can't be completed or there's no room for rewards.
        /// </summary>
        public static bool TryCompleteAndGrantRewards(
            QuestProgressState progress, QuestDefinition quest,
            RaidState raidState, InventoryState inventory)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id)) return false;
            var p = progress.GetProgress(quest.Id);
            if (p == null || p.Status != QuestStatus.Active) return false;

            if (!CanFitRewards(quest.Rewards, inventory)) return false;

            GrantRewards(quest.Rewards, raidState, inventory);
            progress.CompleteQuest(quest.Id);
            return true;
        }

        public static bool CanFitRewards(List<QuestReward> rewards, InventoryState inventory)
        {
            if (rewards == null || rewards.Count == 0) return true;

            int slotsNeeded = 0;
            foreach (var reward in rewards)
            {
                var def = ItemDefinition.Get(reward.ItemId);
                if (def == null) continue;

                int remaining = reward.Count;

                if (def.IsStackable)
                {
                    for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
                    {
                        var slot = inventory.Backpack[i];
                        if (slot != null && slot.DefinitionId == reward.ItemId)
                            remaining -= (def.MaxStackSize - slot.StackCount);
                    }
                }

                if (remaining > 0)
                {
                    if (def.IsStackable)
                        slotsNeeded += Mathf.CeilToInt((float)remaining / def.MaxStackSize);
                    else
                        slotsNeeded += remaining;
                }
            }

            int freeSlots = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                if (inventory.Backpack[i] == null) freeSlots++;

            return freeSlots >= slotsNeeded;
        }

        static void GrantRewards(List<QuestReward> rewards, RaidState raidState, InventoryState inventory)
        {
            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                var def = ItemDefinition.Get(reward.ItemId);
                if (def == null) continue;

                int remaining = reward.Count;

                if (def.IsStackable)
                {
                    for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
                    {
                        var slot = inventory.Backpack[i];
                        if (slot == null || slot.DefinitionId != reward.ItemId) continue;
                        int canAdd = def.MaxStackSize - slot.StackCount;
                        if (canAdd <= 0) continue;
                        int add = remaining < canAdd ? remaining : canAdd;
                        slot.StackCount += add;
                        remaining -= add;
                    }
                }

                while (remaining > 0)
                {
                    int free = inventory.FindFreeBackpackSlot();
                    if (free < 0) break;
                    int count = def.IsStackable
                        ? (remaining < def.MaxStackSize ? remaining : def.MaxStackSize)
                        : 1;
                    inventory.Backpack[free] = WeaponItemFactory.IsKnownWeaponDefinition(reward.ItemId)
                        ? WeaponItemFactory.SpawnItem(raidState.AllocateEId(), reward.ItemId)
                        : ItemState.Create(raidState.AllocateEId(), reward.ItemId, count);
                    remaining -= count;
                }
            }
        }

        static HashSet<string> BuildCompletedSet(QuestProgressState progress)
        {
            var completed = new HashSet<string>();
            foreach (var kvp in progress.All)
                if (kvp.Value.Status == QuestStatus.Completed)
                    completed.Add(kvp.Key);
            return completed;
        }
    }
}
