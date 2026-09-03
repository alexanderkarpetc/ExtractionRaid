using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quests
{
    [Serializable]
    public struct QuestReward
    {
        public string ItemId;
        public int Count;
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "Quests/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;

        [Header("Requirements")]
        public int RequiredLevel;
        public string NpcId;

        [Header("Tasks")]
        [SerializeReference] public List<QuestTask> Tasks = new();

        [Header("Rewards")]
        public List<QuestReward> Rewards = new();

        /// <summary>
        /// Optional non-item reward, shown as an extra chip in the quest's Rewards row
        /// (e.g. "Unlocks Trading"). Purely informational — the actual unlock is driven by
        /// quest status elsewhere (see ShopDefinitionAsset.RequiredQuestId).
        /// </summary>
        public string UnlockNote;
    }
}
