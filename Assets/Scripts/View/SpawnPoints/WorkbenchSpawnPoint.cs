using State;
using UnityEngine;

namespace View.SpawnPoints
{
    /// <summary>
    /// Authoring marker for any "interact + open dialogue" building. Class name kept
    /// for backwards compatibility with existing scene references — the
    /// <see cref="kind"/> field is what actually distinguishes Stash from Crafting,
    /// MedStation from Quest Terminal, etc.
    /// </summary>
    public class WorkbenchSpawnPoint : MonoBehaviour
    {
        [Tooltip("Drives the dialogue options shown when the player interacts. " +
                 "Each kind currently renders a kind-specific dialogue with placeholder " +
                 "actions; real logic is wired in per-kind tickets.")]
        public BuildingKind kind = BuildingKind.Crafting;

        [Header("Interaction Zone")]
        [Tooltip("Box center relative to this point, in meters. Local +Z is forward. Transform scale is ignored.")]
        public Vector3 interactionOffset = new Vector3(0f, 0f, 1f);

        [Tooltip("Box width (X), height (Y), depth (Z), in meters. Rotate the point to rotate the zone. Transform scale is ignored.")]
        public Vector3 interactionSize = new Vector3(2f, 3f, 2f);

        public WorkbenchState RuntimeState { get; private set; }

        public void Bind(WorkbenchState state) => RuntimeState = state;

        void OnDestroy() => RuntimeState = null;

        void OnValidate()
        {
            interactionSize = Vector3.Max(interactionSize, Vector3.one * 0.01f);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var tint = GizmoTintFor(kind);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));
            Gizmos.color = tint;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, $"Building ({kind})");

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.12f);
            Gizmos.DrawCube(interactionOffset, interactionSize);
            Gizmos.color = tint;
            Gizmos.DrawWireCube(interactionOffset, interactionSize);
            Gizmos.DrawRay(Vector3.zero, Vector3.forward);
            Gizmos.matrix = previousMatrix;
        }

        static Color GizmoTintFor(BuildingKind k)
        {
            switch (k)
            {
                case BuildingKind.WeaponBuilder:  return new Color(0.40f, 0.65f, 0.95f, 1f); // blue
                case BuildingKind.Stash:           return new Color(0.78f, 0.62f, 0.30f, 1f); // tan
                case BuildingKind.SupplyTerminal:  return new Color(0.45f, 0.85f, 0.45f, 1f); // green
                case BuildingKind.MedStation:      return new Color(0.95f, 0.45f, 0.45f, 1f); // red
                case BuildingKind.QuestTerminal:   return new Color(0.75f, 0.55f, 0.95f, 1f); // purple
                default:                           return new Color(0.90f, 0.60f, 0.10f, 1f); // orange (Crafting)
            }
        }
#endif
    }
}
