using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quests
{
    [Serializable]
    public struct QuestDatabaseEntry
    {
        public QuestDefinition Quest;
        public string[] RequiredQuestIds;
    }

    public class QuestDatabase : ScriptableObject
    {
        [SerializeField] List<QuestDatabaseEntry> _entries = new();

        public IReadOnlyList<QuestDatabaseEntry> Entries => _entries;

        Dictionary<string, int> _index;

        void BuildIndex()
        {
            _index = new Dictionary<string, int>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                var q = _entries[i].Quest;
                if (q != null && !string.IsNullOrEmpty(q.Id))
                    _index[q.Id] = i;
            }
        }

        public bool TryGet(string questId, out QuestDatabaseEntry entry)
        {
            if (_index == null) BuildIndex();
            if (_index.TryGetValue(questId, out int i))
            {
                entry = _entries[i];
                return true;
            }
            entry = default;
            return false;
        }

        public bool AreRequirementsMet(string questId, HashSet<string> completedQuestIds, int playerLevel)
        {
            if (!TryGet(questId, out var entry)) return false;
            if (entry.Quest.RequiredLevel > playerLevel) return false;
            if (entry.RequiredQuestIds == null) return true;

            foreach (var req in entry.RequiredQuestIds)
                if (!completedQuestIds.Contains(req))
                    return false;

            return true;
        }

#if UNITY_EDITOR
        public void SetEntries(List<QuestDatabaseEntry> entries)
        {
            _entries = entries;
            _index = null;
        }
#endif
    }
}
