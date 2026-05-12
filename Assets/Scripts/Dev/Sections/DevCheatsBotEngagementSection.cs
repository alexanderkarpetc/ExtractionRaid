using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Bot engagement gate — global player-centric radius cap для bot fire eligibility.
    /// Solves "bots stream damage from off-screen" UX gap: top-down camera з vision range
    /// of 35-70m well exceeds visible screen extent, so player ловить damage without
    /// telegraph. Cap restricts fire to a radius slightly less than half-screen-width.
    ///
    /// Orthogonal to per-bot <c>BotTypeConfig.EngageRange</c> — that field is bot identity
    /// ("how far am I willing to fight"); this cap is camera-driven ("how close must they
    /// be для player to see them"). Effective range = <c>min(EngageRange, MaxEngagementRadius)</c>.
    ///
    /// Trade-off (acknowledged): на 16:9 верх/низ екрану може мати випадки де bot fires
    /// from just off-screen vertically. Tunable радіус мінімізує це до прийнятного.
    /// </summary>
    public class DevCheatsBotEngagementSection : ScriptableObject
    {
        [Tooltip("Master switch. False = no extra gate, bots fire purely on per-type EngageRange.")]
        public bool Enabled = true;

        [Tooltip("Max world-space distance from player at which a bot can fire. " +
                 "Tune so that ~half-screen-width у typical play. 0 = no cap (gate off).")]
        [Range(0f, 50f)] public float MaxEngagementRadius = 18f;
    }
}
