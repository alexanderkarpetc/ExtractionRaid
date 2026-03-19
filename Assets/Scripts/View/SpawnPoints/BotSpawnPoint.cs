using UnityEngine;

namespace View.SpawnPoints
{
    public class BotSpawnPoint : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Probability this bot spawns (0 = never, 1 = always)")]
        public float spawnChance = 1f;

        [Tooltip("Bot type ID from BotConstants: Scav, PMC, Boss")]
        public string botTypeId = "Scav";

        [Tooltip("Patrol waypoint transforms. If empty, bot patrols around its spawn position.")]
        public Transform[] patrolWaypoints;

        [Range(5f, 20f)]
        [Tooltip("Radius for auto-generated patrol points when no waypoints are set")]
        public float patrolRadius = 10f;

        public Vector3[] GetPatrolPositions()
        {
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                var positions = new Vector3[patrolWaypoints.Length];
                for (int i = 0; i < patrolWaypoints.Length; i++)
                    positions[i] = patrolWaypoints[i] != null ? patrolWaypoints[i].position : transform.position;
                return positions;
            }

            var origin = transform.position;
            int count = Random.Range(3, 5);
            var pts = new Vector3[count];
            float angleStep = 360f / count;
            float baseAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + angleStep * i + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                float dist = patrolRadius * Random.Range(0.5f, 1f);
                pts[i] = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            }
            return pts;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            if (patrolWaypoints != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    if (patrolWaypoints[i] == null) continue;
                    Gizmos.DrawSphere(patrolWaypoints[i].position, 0.2f);
                    var from = i == 0
                        ? transform.position
                        : (patrolWaypoints[i - 1] != null
                            ? patrolWaypoints[i - 1].position
                            : transform.position);
                    Gizmos.DrawLine(from, patrolWaypoints[i].position);
                }
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up,
                $"Bot: {botTypeId} ({spawnChance:P0})");
        }
#endif
    }
}

