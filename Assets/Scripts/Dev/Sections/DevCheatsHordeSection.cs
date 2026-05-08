using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Runtime tuning для Horde test scene (level <c>"horde_range"</c>).
    /// HordeSpawnSystem reads these every tick — change values у Window → Dev Cheats
    /// while playing щоб calibrate without recompile.
    /// </summary>
    public class DevCheatsHordeSection : ScriptableObject
    {
        [Tooltip("Master switch. When false, no zombies spawn (use to freeze scene for inspection).")]
        public bool Enabled = true;

        [Tooltip("Bot type ID spawned by the horde system. Must exist in BotConstants.Registry.")]
        public string ZombieTypeId = "Zombie";

        [Header("Wave timing")]
        [Tooltip("Grace period at raid start before spawning begins (seconds). Lets player set up.")]
        [Range(0f, 30f)] public float GracePeriod = 5f;

        [Tooltip("Seconds between spawn attempts once grace period ends.")]
        [Range(0.1f, 10f)] public float SpawnInterval = 1.5f;

        [Tooltip("Spawn at most this many zombies per spawn tick (1 = trickle, 3-5 = wave bursts).")]
        [Range(1, 10)] public int SpawnBatchSize = 1;

        [Header("Population cap")]
        [Tooltip("Max alive zombies at any moment. Spawning blocks above this threshold.")]
        [Range(1, 100)] public int MaxAlive = 25;

        [Header("Spawn ring")]
        [Tooltip("Distance from the player where zombies spawn (meters). Should sit slightly " +
                 "outside camera view so they appear off-screen.")]
        [Range(5f, 50f)] public float SpawnRingRadius = 18f;

        [Tooltip("Random radius jitter (meters). Prevents perfect-circle spawn pattern.")]
        [Range(0f, 10f)] public float SpawnRingJitter = 3f;

        [Tooltip("Arc, in degrees, around the player where zombies can spawn. 360 = all sides.")]
        [Range(30f, 360f)] public float SpawnArc = 360f;
    }
}
