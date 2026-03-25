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
            var nodeToId = new Dictionary<QuestNode, string>(questNodes.Count);

            foreach (var node in questNodes)
            {
                node.GetNodeOptionByName("Id").TryGetValue<string>(out var id);
                if (!string.IsNullOrEmpty(id))
                    nodeToId[node] = id;
            }

            var entries = new List<QuestDefinition>(questNodes.Count);
            var connectedPorts = new List<IPort>();

            foreach (var node in questNodes)
            {
                if (!nodeToId.TryGetValue(node, out var id)) continue;

                node.GetNodeOptionByName("DisplayName").TryGetValue<string>(out var displayName);
                node.GetNodeOptionByName("Description").TryGetValue<string>(out var description);
                node.GetNodeOptionByName("RequiredLevel").TryGetValue<int>(out var requiredLevel);

                var requiresPort = node.GetInputPortByName("Requires");
                requiresPort.GetConnectedPorts(connectedPorts);

                var reqIds = new List<string>();
                foreach (var connectedPort in connectedPorts)
                {
                    var sourceNode = connectedPort.GetNode();
                    if (sourceNode is QuestNode srcQuest && nodeToId.TryGetValue(srcQuest, out var srcId))
                        reqIds.Add(srcId);
                }

                entries.Add(new QuestDefinition
                {
                    Id = id,
                    DisplayName = displayName ?? id,
                    Description = description ?? "",
                    RequiredLevel = requiredLevel,
                    RequiredQuestIds = reqIds.Count > 0 ? reqIds.ToArray() : null
                });
            }

            var db = ScriptableObject.CreateInstance<QuestDatabase>();
            db.SetQuests(entries);

            ctx.AddObjectToAsset("QuestDatabase", db);
            ctx.SetMainObject(db);
        }
    }
}
