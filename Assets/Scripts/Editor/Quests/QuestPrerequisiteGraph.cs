using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Editor.Quests
{
    [Graph(AssetExtension)]
    [Serializable]
    public class QuestPrerequisiteGraph : Graph
    {
        public const string AssetExtension = "questgraph";

        [MenuItem("Assets/Create/Quests/Quest Graph", false)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<QuestPrerequisiteGraph>();
        }
    }
}
