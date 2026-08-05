using Session;
using State;

namespace Systems
{
    /// <summary>
    /// The raid clock (M1.2). Runs out → the player is KIA'd, which is the genre's time pressure:
    /// looting one more room has to cost something.
    ///
    /// Deliberately thin — the countdown is derived from <see cref="RaidState.ElapsedTime"/> against
    /// <see cref="RaidState.RaidDurationSeconds"/>, so there is no timer state to tick, reset or
    /// desync. Death goes through <see cref="DamageSystem.KillEntity"/> so the existing pipeline
    /// (ragdoll → <c>ProcessDeathEvents</c> → <c>RaidOutcome.KIA</c> → gear wipe) applies unchanged;
    /// this system never touches <c>App</c> (CLAUDE.md rule 4).
    ///
    /// A duration of 0 means no clock — the hideout and the shooting ranges take that path.
    /// </summary>
    public static class RaidTimerSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            if (state.RaidDurationSeconds <= 0f) return;          // no clock on this level
            if (state.ElapsedTime < state.RaidDurationSeconds) return;

            var player = state.PlayerEntity;
            if (player == null) return;

            // Extraction that completed this frame wins the tie: ExtractionSystem ticks earlier in
            // the frame, so a player who reached 1.0 is already on their way out — killing them here
            // would turn a successful run into a KIA on the last tick.
            if (player.ExtractionProgress01 >= 1f) return;

            DamageSystem.KillEntity(state, player.Id, in context);
        }

        /// <summary>
        /// Seconds left on the clock, floored at 0. Returns 0 when the level has no clock — callers
        /// that care about the difference check <see cref="HasClock"/> first (the HUD does).
        /// </summary>
        public static float TimeRemaining(RaidState state)
        {
            if (state == null || state.RaidDurationSeconds <= 0f) return 0f;
            float remaining = state.RaidDurationSeconds - state.ElapsedTime;
            return remaining > 0f ? remaining : 0f;
        }

        public static bool HasClock(RaidState state) => state != null && state.RaidDurationSeconds > 0f;
    }
}
