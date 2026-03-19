using UnityEngine;

namespace View.SpawnPoints
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            UnityEditor.Handles.Label(transform.position + Vector3.up, "Player Spawn");
        }
#endif
    }
}
