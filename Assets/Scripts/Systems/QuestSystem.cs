using System.Collections.Generic;
using Quests;
using State;

namespace Systems
{
    public static class QuestSystem
    {
        public static int AssignAvailableQuests(QuestProgressState progress, QuestDatabase db, int playerLevel)
        {
            if (db == null) return 0;

            var completed = new HashSet<string>();
            foreach (var kvp in progress.All)
                if (kvp.Value.Status == QuestStatus.Completed)
                    completed.Add(kvp.Key);

            int assigned = 0;
            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null || string.IsNullOrEmpty(entry.Quest.Id)) continue;

                var status = progress.GetStatus(entry.Quest.Id);
                if (status != QuestStatus.NotStarted) continue;

                if (!db.AreRequirementsMet(entry.Quest.Id, completed, playerLevel)) continue;

                progress.StartQuest(entry.Quest.Id, entry.Quest.Tasks?.Count ?? 0);
                assigned++;
            }

            return assigned;
        }

        public static bool TryComplete(QuestProgressState progress, string questId)
        {
            var p = progress.GetProgress(questId);
            if (p == null || p.Status != QuestStatus.Active) return false;
            progress.CompleteQuest(questId);
            return true;
        }
    }
}
