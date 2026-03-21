using UnityEngine;

namespace View.SpawnPoints
{
    public class WorkbenchSpawnPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));
            Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.6f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, "Workbench");
        }
#endif
    }
}
