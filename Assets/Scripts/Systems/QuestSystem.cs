using System.Collections.Generic;
using Quests;
using State;

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
        /// Returns quests that are active and belong to the given NPC (for turn-in).
        /// </summary>
        public static List<QuestDefinition> GetCompletableQuests(
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

        /// <summary>
        /// Returns active quests owned by this NPC (to show progress while talking).
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
