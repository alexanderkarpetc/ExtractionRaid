using System.Collections.Generic;

namespace State
{
    public enum QuestStatus
    {
        NotStarted,
        Active,
        Completed,
        Failed
    }

    public class TaskProgress
    {
        public int TaskIndex;
        public int CurrentCount;
    }

    public class QuestProgress
    {
        public string QuestId;
        public QuestStatus Status;
        public List<TaskProgress> Tasks = new();
    }

    public class QuestProgressState
    {
        readonly Dictionary<string, QuestProgress> _quests = new();

        public IReadOnlyDictionary<string, QuestProgress> All => _quests;

        public QuestProgress GetProgress(string questId)
        {
            _quests.TryGetValue(questId, out var progress);
            return progress;
        }

        public QuestStatus GetStatus(string questId)
        {
            return _quests.TryGetValue(questId, out var p) ? p.Status : QuestStatus.NotStarted;
        }

        public QuestProgress StartQuest(string questId, int taskCount)
        {
            if (_quests.TryGetValue(questId, out var existing) && existing.Status == QuestStatus.Active)
                return existing;

            var progress = new QuestProgress
            {
                QuestId = questId,
                Status = QuestStatus.Active
            };
            for (int i = 0; i < taskCount; i++)
                progress.Tasks.Add(new TaskProgress { TaskIndex = i, CurrentCount = 0 });

            _quests[questId] = progress;
            return progress;
        }

        public bool IncrementTask(string questId, int taskIndex, int amount = 1)
        {
            if (!_quests.TryGetValue(questId, out var p) || p.Status != QuestStatus.Active)
                return false;
            if (taskIndex < 0 || taskIndex >= p.Tasks.Count)
                return false;

            p.Tasks[taskIndex].CurrentCount += amount;
            return true;
        }

        public void CompleteQuest(string questId)
        {
            if (_quests.TryGetValue(questId, out var p))
                p.Status = QuestStatus.Completed;
        }

        public void FailQuest(string questId)
        {
            if (_quests.TryGetValue(questId, out var p))
                p.Status = QuestStatus.Failed;
        }

        public void Clear() => _quests.Clear();

        public void RestoreFrom(List<QuestProgress> list)
        {
            _quests.Clear();
            if (list == null) return;
            foreach (var p in list)
                if (p != null && !string.IsNullOrEmpty(p.QuestId))
                    _quests[p.QuestId] = p;
        }

        public List<QuestProgress> ToList()
        {
            return new List<QuestProgress>(_quests.Values);
        }
    }
}
