using System;
using Unity.GraphToolkit.Editor;

namespace Editor.Quests
{
    [Serializable]
    public class QuestNode : Node
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("Id")
                .WithDisplayName("Quest ID")
                .WithTooltip("Unique identifier used by game systems at runtime.");

            context.AddOption<string>("DisplayName")
                .WithDisplayName("Display Name")
                .WithTooltip("Human-readable quest name shown to the player.");

            context.AddOption<string>("Description")
                .WithDisplayName("Description")
                .WithTooltip("Quest description text.")
                .ShowInInspectorOnly();

            context.AddOption<int>("RequiredLevel")
                .WithDisplayName("Required Level")
                .WithDefaultValue(0)
                .WithTooltip("Minimum player level to accept this quest.");
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
