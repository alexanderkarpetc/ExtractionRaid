using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Raid clock (M1.2). Applies to real levels only — the hideout and the shooting ranges resolve
    /// to "no clock" in <c>RaidSession.ResolveRaidDuration</c> regardless of what is set here.
    ///
    /// Set <see cref="RaidDurationSeconds"/> to 0 to play with no deadline: that is also the escape
    /// hatch for long playtests, since the timeout kill deliberately ignores GodMode.
    /// Takes effect on the next raid start (the duration is captured into RaidState at Start).
    /// </summary>
    public class DevCheatsRaidSection : ScriptableObject
    {
        [Tooltip("Seconds before the raid ends in a KIA. 0 = no limit.")]
        [Min(0f)] public float RaidDurationSeconds = Constants.RaidTimerConstants.DefaultDurationSeconds;

        [Tooltip("Remaining seconds at which the HUD timer switches to the warning look.")]
        [Min(0f)] public float RaidWarnAtSeconds = Constants.RaidTimerConstants.DefaultWarnAtSeconds;

        [Tooltip("Remaining seconds at which the HUD timer switches to the critical look.")]
        [Min(0f)] public float RaidCriticalAtSeconds = Constants.RaidTimerConstants.DefaultCriticalAtSeconds;
    }
}
