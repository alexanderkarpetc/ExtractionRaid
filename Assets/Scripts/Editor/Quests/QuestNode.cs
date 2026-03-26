using System;
using Quests;
using Unity.GraphToolkit.Editor;

namespace Editor.Quests
{
    [Serializable]
    public class QuestNode : Node
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<QuestDefinition>("Quest")
                .WithDisplayName("Quest Asset")
                .WithTooltip("Reference to the QuestDefinition ScriptableObject.");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("Requires")
                .WithDisplayName("Requires")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort("Unlocks")
                .WithDisplayName("Unlocks")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
