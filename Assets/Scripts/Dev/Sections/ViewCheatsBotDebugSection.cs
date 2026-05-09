using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Toggles for the floating debug overlay drawn above each bot
    /// (<c>BotDebugLabel</c>). Two independent flags so HP can stay visible while
    /// the noisier brain status / SEE / Dist debug line is hidden — common scenario
    /// for playtesting feel without text clutter. View-layer only — no gameplay impact.
    /// </summary>
    public class ViewCheatsBotDebugSection : ScriptableObject
    {
        [Tooltip("Show \"HP: current/max\" line above each bot.")]
        public bool ShowHpText = true;

        [Tooltip("Show \"[TypeId] BrainStatus [SEE]  Dist: X\" debug line. Internals — flip off " +
                 "for clean playtest screenshots.")]
        public bool ShowStatus = true;
    }
}
