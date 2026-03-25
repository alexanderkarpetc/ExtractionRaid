using System.Collections.Generic;
using UnityEngine;

namespace Quests
{
    public class QuestDatabase : ScriptableObject
    {
        [SerializeField] List<QuestDefinition> _quests = new();

        public IReadOnlyList<QuestDefinition> Quests => _quests;

        Dictionary<string, int> _index;

        void BuildIndex()
        {
            _index = new Dictionary<string, int>(_quests.Count);
            for (int i = 0; i < _quests.Count; i++)
                _index[_quests[i].Id] = i;
        }

        public bool TryGet(string questId, out QuestDefinition quest)
        {
            if (_index == null) BuildIndex();
            if (_index.TryGetValue(questId, out int i))
            {
                quest = _quests[i];
                return true;
            }
            quest = default;
            return false;
        }

        public bool AreRequirementsMet(string questId, HashSet<string> completedQuestIds, int playerLevel)
        {
            if (!TryGet(questId, out var quest)) return false;
            if (playerLevel < quest.RequiredLevel) return false;
            if (quest.RequiredQuestIds == null) return true;

            foreach (var req in quest.RequiredQuestIds)
                if (!completedQuestIds.Contains(req))
                    return false;

            return true;
        }

#if UNITY_EDITOR
        public void SetQuests(List<QuestDefinition> quests)
        {
            _quests = quests;
            _index = null;
        }
#endif
    }
}
