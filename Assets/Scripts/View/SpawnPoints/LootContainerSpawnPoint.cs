using Constants;
using UnityEngine;

namespace View.SpawnPoints
{
    public class LootContainerSpawnPoint : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Probability this container spawns (0 = never, 1 = always)")]
        public float spawnChance = 1f;

        [Tooltip("ContainerTypeConfig asset. Defines slot count, min/max drops, and weighted drop pool. " +
                 "Registered into ContainerConstants at spawn time.")]
        public ContainerTypeConfigAsset config;

        [Tooltip("Optional visual prefab instantiated by LootablePresenter when this " +
                 "container spawns. If empty, falls back to the procedural cube.")]
        public GameObject visualPrefab;

        public string ContainerTypeId => config != null ? config.TypeId : null;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 0.6f));
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.2f, new Vector3(0.6f, 0.4f, 0.6f));

            string label = config != null ? config.TypeId : "<no config>";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f,
                $"Container: {label} ({spawnChance:P0})");
        }
#endif
    }
}
