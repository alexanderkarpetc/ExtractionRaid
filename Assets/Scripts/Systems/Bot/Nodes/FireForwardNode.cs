using Constants;
using Session;
using State;
using Systems.Bot.BT;
using UnityEngine;

namespace Systems.Bot.Nodes
{
    /// <summary>
    /// Continuous-fire BT node — sets <c>WantsToFire</c> + aim-point straight ahead in the
    /// bot's current <see cref="BotEntityState.FacingDirection"/>. Ignores target, ignores
    /// vision, ignores reaction time. Pairs з <see cref="BotBehaviorFlags.FireForward"/> and
    /// is intended for stationary test turrets (FeedbackRange playtest scene).
    ///
    /// Fire rate is owned by <c>BotCombatSystem.ProcessFire</c> via the weapon's
    /// <c>FireInterval</c>, so this node can safely return Success every tick.
    /// </summary>
    public class FireForwardNode : IBTNode
    {
        const float AimDistance = 100f; // arbitrary far-forward aim point

        public string Name => "FireForward";

        public BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config)
        {
            var forward = bot.FacingDirection;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            bot.DesiredAimPoint = bot.Position + forward.normalized * AimDistance;
            bot.WantsToFire = true;
            bot.Blackboard.DebugStatus = "FireForward";
            return this.Traced(bot, BTStatus.Success);
        }
    }
}
