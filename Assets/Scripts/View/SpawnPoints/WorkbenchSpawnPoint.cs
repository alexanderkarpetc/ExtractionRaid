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

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var tint = GizmoTintFor(kind);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));
            Gizmos.color = tint;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, $"Building ({kind})");
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
