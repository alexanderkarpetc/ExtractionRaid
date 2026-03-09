using Constants;
using Session;
using State;

namespace Systems.Bot.BT
{
    public interface IBTNode
    {
        BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config);
    }
}
