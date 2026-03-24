using UnityEngine;

namespace View.SpawnPoints
{
    public class DeploySpawnPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f, 0.4f));
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f, 0.4f));

            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.3f, "Deploy Exit");
        }
#endif
    }
}
