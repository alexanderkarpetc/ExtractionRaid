using State;
using UnityEngine;

namespace View.SpawnPoints
{
    public class WorkbenchSpawnPoint : MonoBehaviour
    {
        [Tooltip("Drives the dialogue options shown when the player interacts. " +
                 "Crafting = recipe popup. WeaponBuilder = Weapon Builder modal.")]
        public WorkbenchKind kind = WorkbenchKind.Crafting;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var tint = kind == WorkbenchKind.WeaponBuilder
                ? new Color(0.4f, 0.65f, 0.95f, 1f)
                : new Color(0.9f, 0.6f, 0.1f, 1f);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));
            Gizmos.color = tint;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, $"Workbench ({kind})");
        }
#endif
    }
}
