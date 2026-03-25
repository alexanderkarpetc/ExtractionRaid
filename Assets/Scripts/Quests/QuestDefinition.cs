using System;
using UnityEngine;

namespace Quests
{
    [Serializable]
    public struct QuestDefinition
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public int RequiredLevel;
        public string[] RequiredQuestIds;
    }
}
