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

        /// <summary>
        /// Credits sale value to every active <see cref="SellItemsTask"/>. Called by the
        /// vendor/sell flow once a transaction commits — pass the total currency the
        /// player just earned (sum of per-item prices for the batch). v1 has no item
        /// filter, so every sale ticks every active SellItemsTask. Returns true if any
        /// task progressed; values above the remaining-required cap are capped so a
        /// single huge sale doesn't over-fill the bar.
        /// </summary>
        public static bool OnItemSold(
            QuestProgressState progress, QuestDatabase db, int currencyEarned)
        {
            if (progress == null || db == null || currencyEarned <= 0) return false;

            bool any = false;

            foreach (var kvp in progress.All)
            {
                var qp = kvp.Value;
                if (qp.Status != QuestStatus.Active) continue;
                if (!db.TryGet(qp.QuestId, out var entry) || entry.Quest?.Tasks == null) continue;

                var tasks = entry.Quest.Tasks;
                for (int i = 0; i < tasks.Count && i < qp.Tasks.Count; i++)
                {
                    if (tasks[i] is not SellItemsTask sell) continue;

                    var tp = qp.Tasks[i];
                    int remaining = sell.RequiredCount - tp.CurrentCount;
                    if (remaining <= 0) continue;

                    int credit = currencyEarned < remaining ? currencyEarned : remaining;
                    tp.CurrentCount += credit;
                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Snaps every active <see cref="UpgradeBuildingTask"/> for the given building
        /// kind to the new level. Called from <see cref="BuildingSystem.TryUpgrade"/>
        /// right after the level increments. Cap is the task's RequiredCount so the
        /// progress bar can't overshoot a 1/3 task into 3/3 from a single +1 bump.
        /// </summary>
        public static bool OnBuildingUpgraded(
            QuestProgressState progress, QuestDatabase db,
            State.BuildingKind kind, int newLevel)
        {
            if (progress == null || db == null) return false;
            bool any = false;
            foreach (var kvp in progress.All)
            {
                var qp = kvp.Value;
                if (qp.Status != QuestStatus.Active) continue;
                if (!db.TryGet(qp.QuestId, out var entry) || entry.Quest?.Tasks == null) continue;

                var tasks = entry.Quest.Tasks;
                for (int i = 0; i < tasks.Count && i < qp.Tasks.Count; i++)
                {
                    if (tasks[i] is not UpgradeBuildingTask up) continue;
                    if (up.Kind != kind) continue;

                    var tp = qp.Tasks[i];
                    int capped = newLevel < up.RequiredCount ? newLevel : up.RequiredCount;
                    if (capped <= tp.CurrentCount) continue;
                    tp.CurrentCount = capped;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// Credits a Weapon Builder commit to every active <see cref="BuildWeaponTask"/>
        /// whose payload + delivery IDs match the build (empty string on either field on
        /// the task means "any"). Called from <see cref="View.UI.WeaponBuilder.WeaponBuilderPresenter.TryBuild"/>
        /// right after the new weapon lands in the inventory.
        /// </summary>
        public static bool OnWeaponBuilt(
            QuestProgressState progress, QuestDatabase db,
            string payloadId, string deliveryId)
        {
            if (progress == null || db == null) return false;
            bool any = false;
            foreach (var kvp in progress.All)
            {
                var qp = kvp.Value;
                if (qp.Status != QuestStatus.Active) continue;
                if (!db.TryGet(qp.QuestId, out var entry) || entry.Quest?.Tasks == null) continue;

                var tasks = entry.Quest.Tasks;
                for (int i = 0; i < tasks.Count && i < qp.Tasks.Count; i++)
                {
                    if (tasks[i] is not BuildWeaponTask build) continue;
                    if (!string.IsNullOrEmpty(build.PayloadId) && build.PayloadId != payloadId) continue;
                    if (!string.IsNullOrEmpty(build.DeliveryId) && build.DeliveryId != deliveryId) continue;

                    var tp = qp.Tasks[i];
                    if (tp.CurrentCount >= build.RequiredCount) continue;
                    tp.CurrentCount++;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// Ticks every active <see cref="ExtractTask"/> whose level matches the one the
        /// player just extracted from. An empty <c>LevelId</c> on the task means "any
        /// level". Called from <c>App.EndRaid</c> only on the Extracted outcome.
        /// </summary>
        public static bool OnPlayerExtracted(
            QuestProgressState progress, QuestDatabase db, string levelId)
        {
            if (progress == null || db == null) return false;
            bool any = false;
            foreach (var kvp in progress.All)
            {
                var qp = kvp.Value;
                if (qp.Status != QuestStatus.Active) continue;
                if (!db.TryGet(qp.QuestId, out var entry) || entry.Quest?.Tasks == null) continue;

                var tasks = entry.Quest.Tasks;
                for (int i = 0; i < tasks.Count && i < qp.Tasks.Count; i++)
                {
                    if (tasks[i] is not ExtractTask ex) continue;
                    if (!string.IsNullOrEmpty(ex.LevelId) && ex.LevelId != levelId) continue;

                    var tp = qp.Tasks[i];
                    if (tp.CurrentCount >= ex.RequiredCount) continue;
                    tp.CurrentCount++;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// Called from <see cref="ApplicationCore.App.EndRaid"/> at the end of every
        /// raid (extract or KIA). Resets <see cref="KillEnemyTask"/> progress on every
        /// active quest where the task has <c>InOneRaid = true</c> and the player
        /// hadn't yet reached the kill count — they must do it in a single raid.
        /// Tasks already at <see cref="QuestTask.RequiredCount"/> are preserved so the
        /// player can still claim the reward at the NPC after returning to hideout.
        /// Returns true if any task was reset.
        /// </summary>
        public static bool OnRaidEnded(QuestProgressState progress, QuestDatabase db)
        {
            if (progress == null || db == null) return false;

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
                    if (!kill.InOneRaid) continue;

                    var tp = qp.Tasks[i];
                    if (tp.CurrentCount <= 0) continue;
                    if (tp.CurrentCount >= kill.RequiredCount) continue; // already satisfied — keep it

                    tp.CurrentCount = 0;
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

        public static bool TryAccept(QuestProgressState progress, QuestDefinition quest,
            Session.Player player = null)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id)) return false;
            if (progress.GetStatus(quest.Id) != QuestStatus.NotStarted) return false;

            progress.StartQuest(quest.Id, quest.Tasks?.Count ?? 0);

            // Seed already-satisfied prerequisites. Currently relevant for
            // UpgradeBuildingTask — if the building is already at or past the
            // target level when the player accepts, the task should land as
            // done instead of waiting for a future upgrade that may never come.
            if (player != null && quest.Tasks != null)
            {
                var qp = progress.GetProgress(quest.Id);
                if (qp != null)
                {
                    for (int i = 0; i < quest.Tasks.Count && i < qp.Tasks.Count; i++)
                    {
                        if (quest.Tasks[i] is not UpgradeBuildingTask up) continue;
                        int current = player.GetBuildingLevel(up.Kind);
                        int capped = current < up.RequiredCount ? current : up.RequiredCount;
                        if (capped > qp.Tasks[i].CurrentCount)
                            qp.Tasks[i].CurrentCount = capped;
                    }
                }
            }
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

        /// <summary>
        /// One pending hand-in for a FindAndTransfer / FindItem task on an active
        /// quest owned by the given NPC, where the player has at least one of the
        /// required item in their backpack. <see cref="DeliverableNow"/> is what would
        /// actually transfer this click — capped by both the remaining task count and
        /// what's in the inventory, so partial hand-ins work when the player is short.
        /// </summary>
        public struct HandoverOpportunity
        {
            public string QuestId;
            public int TaskIndex;
            public string ItemId;
            public int RequiredRemaining; // task RequiredCount - CurrentCount
            public int Available;          // total of ItemId in backpack
            public int DeliverableNow;     // min(RequiredRemaining, Available)
        }

        /// <summary>
        /// Scans every active quest belonging to <paramref name="npcId"/> for transfer-style
        /// tasks (FindAndTransfer / FindItem) that aren't fully complete and where the
        /// player has at least one matching item in their backpack. Used by the NPC dialogue
        /// to surface a "Hand over X" choice per task without the player having to open the
        /// quest journal.
        /// </summary>
        public static List<HandoverOpportunity> GetHandoverOpportunities(
            QuestProgressState progress, QuestDatabase db, InventoryState inventory, string npcId,
            List<ItemState> stash = null)
        {
            var result = new List<HandoverOpportunity>();
            if (progress == null || db == null || inventory == null || string.IsNullOrEmpty(npcId))
                return result;

            foreach (var entry in db.Entries)
            {
                var quest = entry.Quest;
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                if (quest.NpcId != npcId) continue;

                var p = progress.GetProgress(quest.Id);
                if (p == null || p.Status != QuestStatus.Active) continue;
                if (quest.Tasks == null) continue;

                int i = 0;
                for (; i < quest.Tasks.Count && i < p.Tasks.Count; i++)
                {
                    string itemId = ExtractHandoverItemId(quest.Tasks[i]);
                    if (string.IsNullOrEmpty(itemId)) continue;

                    int remainingTask = quest.Tasks[i].RequiredCount - p.Tasks[i].CurrentCount;
                    if (remainingTask <= 0) continue;

                    int available = CountItemInBackpack(inventory, itemId)
                                  + CountItemInStash(stash, itemId);

                    // Surface every incomplete task, even when the player has nothing —
                    // dialogue renders it as a disabled "Y/X" hint so the player knows
                    // what's missing.
                    int deliverable = remainingTask < available ? remainingTask : available;
                    result.Add(new HandoverOpportunity
                    {
                        QuestId = quest.Id,
                        TaskIndex = i,
                        ItemId = itemId,
                        RequiredRemaining = remainingTask,
                        Available = available,
                        DeliverableNow = deliverable,
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Consumes up to <see cref="HandoverOpportunity.DeliverableNow"/> items from
        /// the backpack and increments the corresponding task's progress by the same
        /// amount. Re-resolves remaining counts against current inventory to stay safe
        /// if state shifted between detection and click. Returns the number actually
        /// transferred (0 on failure).
        /// </summary>
        public static int HandOver(
            QuestProgressState progress, QuestDatabase db, InventoryState inventory,
            HandoverOpportunity opportunity, List<ItemState> stash = null)
        {
            if (progress == null || db == null || inventory == null) return 0;
            if (string.IsNullOrEmpty(opportunity.QuestId) || string.IsNullOrEmpty(opportunity.ItemId)) return 0;

            var p = progress.GetProgress(opportunity.QuestId);
            if (p == null || p.Status != QuestStatus.Active) return 0;
            if (opportunity.TaskIndex < 0 || opportunity.TaskIndex >= p.Tasks.Count) return 0;

            if (!db.TryGet(opportunity.QuestId, out var entry) || entry.Quest?.Tasks == null) return 0;
            var task = entry.Quest.Tasks[opportunity.TaskIndex];
            if (task == null) return 0;

            int remainingTask = task.RequiredCount - p.Tasks[opportunity.TaskIndex].CurrentCount;
            if (remainingTask <= 0) return 0;

            int available = CountItemInBackpack(inventory, opportunity.ItemId)
                          + CountItemInStash(stash, opportunity.ItemId);
            int amount = Mathf.Min(remainingTask, available);
            if (amount <= 0) return 0;

            // Drain backpack first so the player's raid loadout is preserved when
            // stash supply alone covers the delivery.
            int leftover = ConsumeFromBackpackReturning(inventory, opportunity.ItemId, amount);
            if (leftover > 0) ConsumeFromStash(stash, opportunity.ItemId, leftover);

            p.Tasks[opportunity.TaskIndex].CurrentCount += amount;
            return amount;
        }

        static string ExtractHandoverItemId(QuestTask task)
        {
            switch (task)
            {
                case FindAndTransferTask t: return t.QuestItemId;
                case FindItemTask        t: return t.ItemId;
                default: return null;
            }
        }

        static int CountItemInBackpack(InventoryState inventory, string itemId)
        {
            int count = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var slot = inventory.Backpack[i];
                if (slot != null && slot.DefinitionId == itemId)
                    count += slot.StackCount;
            }
            return count;
        }

        // Returns leftover (unspent) amount so the caller can fall back to another container.
        static int ConsumeFromBackpackReturning(InventoryState inventory, string itemId, int amount)
        {
            int remaining = amount;
            for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
            {
                var slot = inventory.Backpack[i];
                if (slot == null || slot.DefinitionId != itemId) continue;

                if (slot.StackCount <= remaining)
                {
                    remaining -= slot.StackCount;
                    inventory.Backpack[i] = null;
                }
                else
                {
                    slot.StackCount -= remaining;
                    remaining = 0;
                }
            }
            inventory.Version++;
            return remaining;
        }

        static int CountItemInStash(List<ItemState> stash, string itemId)
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

        static void ConsumeFromStash(List<ItemState> stash, string itemId, int amount)
        {
            if (stash == null) return;
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
