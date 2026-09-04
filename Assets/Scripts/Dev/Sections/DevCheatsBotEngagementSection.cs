using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Camera-space engagement gate. Bots approach the visible play area before ranged combat;
    /// hysteresis keeps camera motion from toggling fire every frame at the screen edge.
    /// </summary>
    public class DevCheatsBotEngagementSection : ScriptableObject
    {
        [Tooltip("Require ranged bots to enter the camera viewport before they can fire.")]
        public bool Enabled = true;

        [Tooltip("Normalized inset required before a bot starts ranged combat.")]
        [Range(0f, 0.49f)] public float ViewportEnterMargin = 0.12f;

        [Tooltip("Smaller inset used after entry; creates hysteresis near the screen edge.")]
        [Range(0f, 0.49f)] public float ViewportExitMargin = 0.05f;
    }
}
