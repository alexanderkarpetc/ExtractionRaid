using Constants;
using UnityEngine;

namespace View.SpawnPoints
{
    /// <summary>
    /// Authoring marker for an extraction zone on the level. RaidSession reads these
    /// at startup and creates matching <see cref="State.ExtractionPointState"/> entries
    /// in <c>RaidState.ExtractionPoints</c>. ExtractionSystem checks player distance
    /// against the registered radius each frame to tick or reset progress.
    /// </summary>
    public class ExtractionPointSpawnPoint : MonoBehaviour
    {
        [Tooltip("Radius in meters around the spawn point that counts as the extraction zone.")]
        public float radius = ExtractionConstants.DefaultZoneRadius;

        [Tooltip("Optional zone label shown in the extraction HUD (e.g. 'North Pier').")]
        public string label = "Extraction Point";

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var center = transform.position + Vector3.up * 0.1f;
            Gizmos.color = new Color(0.53f, 0.90f, 1f, 0.18f);
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = new Color(0.53f, 0.90f, 1f, 1f);
            Gizmos.DrawWireSphere(center, radius);

            UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 0.4f),
                $"Extract: {label} (r {radius:0.0})");
        }
#endif
    }
}
