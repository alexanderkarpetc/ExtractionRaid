using UnityEngine;

namespace Quests
{
    [CreateAssetMenu(fileName = "NewQuest", menuName = "Quests/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;

        [Header("Requirements")]
        public int RequiredLevel;
    }
}
