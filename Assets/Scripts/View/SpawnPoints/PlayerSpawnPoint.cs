using UnityEngine;

namespace View.SpawnPoints
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [Min(0f)]
        [Tooltip("Relative chance of selecting this player spawn point. 0 = never selected unless all player spawn weights are 0.")]
        public float weight = 1f;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            UnityEditor.Handles.Label(transform.position + Vector3.up, $"Player Spawn (weight: {Mathf.Max(0f, weight):0.##})");
        }
#endif
    }

    public static class PlayerSpawnPointSelector
    {
        public static PlayerSpawnPoint Pick(PlayerSpawnPoint[] spawnPoints, float normalizedRoll)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            float totalWeight = 0f;
            int validCount = 0;
            PlayerSpawnPoint lastWeightedPoint = null;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = spawnPoints[i];
                if (point == null) continue;

                validCount++;
                float pointWeight = Mathf.Max(0f, point.weight);
                totalWeight += pointWeight;
                if (pointWeight > 0f) lastWeightedPoint = point;
            }

            if (validCount == 0) return null;

            float roll = Mathf.Clamp01(normalizedRoll);
            if (totalWeight <= 0f)
            {
                int selectedIndex = Mathf.Min(Mathf.FloorToInt(roll * validCount), validCount - 1);
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] == null) continue;
                    if (selectedIndex-- == 0) return spawnPoints[i];
                }
            }

            float targetWeight = roll * totalWeight;
            float accumulatedWeight = 0f;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = spawnPoints[i];
                if (point == null) continue;

                float pointWeight = Mathf.Max(0f, point.weight);
                if (pointWeight <= 0f) continue;

                accumulatedWeight += pointWeight;
                if (targetWeight < accumulatedWeight) return point;
            }

            return lastWeightedPoint;
        }
    }
}
