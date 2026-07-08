using Constants;
using UnityEngine;

namespace View.SpawnPoints
{
    public class BotSpawnPoint : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Probability this bot spawns (0 = never, 1 = always)")]
        public float spawnChance = 1f;

        [Tooltip("BotTypeConfig asset. Defines bot type, stats, visuals, and behavior. Registered into BotConstants at spawn time.")]
        public BotTypeConfigAsset config;

        [Tooltip("Patrol waypoint transforms. If empty, bot patrols around its spawn position.")]
        public Transform[] patrolWaypoints;

        [Range(5f, 20f)]
        [Tooltip("Radius for auto-generated patrol points when no waypoints are set")]
        public float patrolRadius = 10f;

        // NavMesh validation for patrol points. A point is usable only if it snaps to
        // the mesh AND a complete path exists from the (snapped) spawn position —
        // partial paths mean "walk into a wall and get stuck-skipped", so they're
        // rejected too.
        const float NavSnapDistance = 1.5f;
        const int   AutoPointAttempts = 8;

        // Lazy — Unity forbids NavMeshPath construction in field initializers/static ctors
        // of MonoBehaviours ("InitializeNavMeshPath is not allowed...").
        static UnityEngine.AI.NavMeshPath _scratchPath;

        public Vector3[] GetPatrolPositions()
        {
            var origin = transform.position;
            bool originOnMesh = UnityEngine.AI.NavMesh.SamplePosition(
                origin, out var originHit, NavSnapDistance * 2f, UnityEngine.AI.NavMesh.AllAreas);
            if (originOnMesh) origin = originHit.position;

            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                return GetAuthoredPositions(origin, originOnMesh);

            // Auto-generated ring: re-roll each point until it lands somewhere the bot
            // can actually reach (walls/rooms around the spawn eat naive ring points).
            int count = Random.Range(3, 5);
            var pts = new System.Collections.Generic.List<Vector3>(count);
            float angleStep = 360f / count;
            float baseAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < AutoPointAttempts; attempt++)
                {
                    float angle = (baseAngle + angleStep * i + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                    float dist = patrolRadius * Random.Range(0.5f, 1f);
                    var candidate = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                    if (TrySnapReachable(origin, originOnMesh, candidate, out var valid))
                    {
                        pts.Add(valid);
                        break;
                    }
                }
            }

            if (pts.Count == 0)
                pts.Add(origin); // boxed-in spawn: idle in place instead of wall-grinding

            return pts.ToArray();
        }

        Vector3[] GetAuthoredPositions(Vector3 origin, bool originOnMesh)
        {
            var positions = new System.Collections.Generic.List<Vector3>(patrolWaypoints.Length);
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                var raw = patrolWaypoints[i] != null ? patrolWaypoints[i].position : transform.position;
                if (TrySnapReachable(origin, originOnMesh, raw, out var valid))
                {
                    positions.Add(valid);
                }
                else
                {
                    Debug.LogWarning(
                        $"BotSpawnPoint '{name}': patrol waypoint {i} at {raw} is off-navmesh or " +
                        "unreachable from the spawn — dropped.", this);
                }
            }
            if (positions.Count == 0)
                positions.Add(origin);
            return positions.ToArray();
        }

        static bool TrySnapReachable(Vector3 origin, bool originOnMesh, Vector3 candidate, out Vector3 result)
        {
            result = candidate;
            if (!UnityEngine.AI.NavMesh.SamplePosition(
                    candidate, out var hit, NavSnapDistance, UnityEngine.AI.NavMesh.AllAreas))
                return false;

            // No baked navmesh under the spawn (test scenes) → snap-only validation.
            if (!originOnMesh)
            {
                result = hit.position;
                return true;
            }

            _scratchPath ??= new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(
                    origin, hit.position, UnityEngine.AI.NavMesh.AllAreas, _scratchPath)
                || _scratchPath.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                return false;

            result = hit.position;
            return true;
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

            string label = config != null ? config.TypeId : "<no config>";
            UnityEditor.Handles.Label(transform.position + Vector3.up,
                $"Bot: {label} ({spawnChance:P0})");
        }
#endif
    }
}

