using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Brings a ranged bot clearly into the player's camera before tactics may fire.
    /// Uses separate enter/exit margins so camera motion cannot flicker combat at the edge.
    /// </summary>
    public sealed class EnterEngagementViewNode : IBTNode
    {
        readonly ChaseNode _chase = new();

        public string Name => "EnterEngagementView";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx,
            in BotTypeConfig config)
        {
            var bb = bot.Blackboard;
            var engagement = ctx.BotEngagementConfig;
            if (!engagement.Enabled || ctx.CombatViewport == null
                || !bb.HasTarget || !bb.CanSeeTarget)
                return this.Traced(bot, BTStatus.Failure);

            float enterMargin = engagement.ViewportEnterMargin;
            float exitMargin = Mathf.Min(engagement.ViewportExitMargin, enterMargin);
            float margin = bb.IsInsideEngagementView ? exitMargin : enterMargin;
            var samplePosition = bot.Position + Vector3.up * BotConstants.PlayerEyeHeight;
            if (ctx.CombatViewport.IsInside(samplePosition, margin))
            {
                bb.IsInsideEngagementView = true;
                return this.Traced(bot, BTStatus.Failure);
            }

            bb.IsInsideEngagementView = false;
            if (bb.CoverPhase != CoverPhase.None)
                bb.ResetCover();

            _chase.Tick(bot, state, in ctx, in config);
            bb.DebugStatus = "Enter view";
            return this.Traced(bot, BTStatus.Running);
        }
    }
}
