using System.Collections.Generic;
using System.Linq;
using Editor.Quests;
using Quests;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Editor
{
    [ScriptedImporter(1, QuestPrerequisiteGraph.AssetExtension)]
    public class QuestGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<QuestPrerequisiteGraph>(ctx.assetPath);
            if (graph == null)
            {
                Debug.LogError($"Failed to load quest graph: {ctx.assetPath}");
                return;
            }

            var questNodes = graph.GetNodes().OfType<QuestNode>().ToList();
            var nodeToQuest = new Dictionary<QuestNode, QuestDefinition>(questNodes.Count);

            foreach (var node in questNodes)
            {
                node.GetNodeOptionByName("Quest").TryGetValue<QuestDefinition>(out var quest);
                if (quest != null && !string.IsNullOrEmpty(quest.Id))
                    nodeToQuest[node] = quest;
            }

            var entries = new List<QuestDatabaseEntry>(nodeToQuest.Count);
            var connectedPorts = new List<IPort>();

            foreach (var (node, quest) in nodeToQuest)
            {
                var requiresPort = node.GetInputPortByName("Requires");
                requiresPort.GetConnectedPorts(connectedPorts);

                var reqIds = new List<string>();
                foreach (var connectedPort in connectedPorts)
                {
                    var sourceNode = connectedPort.GetNode();
                    if (sourceNode is QuestNode srcQuest && nodeToQuest.TryGetValue(srcQuest, out var srcDef))
                        reqIds.Add(srcDef.Id);
                }

                entries.Add(new QuestDatabaseEntry
                {
                    Quest = quest,
                    RequiredQuestIds = reqIds.Count > 0 ? reqIds.ToArray() : null
                });
            }

            var db = ScriptableObject.CreateInstance<QuestDatabase>();
            db.SetEntries(entries);

            ctx.AddObjectToAsset("QuestDatabase", db);
            ctx.SetMainObject(db);
        }
    }
}
