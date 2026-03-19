using Constants;
using UnityEngine;

namespace View.SpawnPoints
{
    [System.Serializable]
    public struct LooseLootDrop
    {
        [Tooltip("Item definition ID (e.g. Medkit, Ammo_Rifle, Rifle)")]
        public string definitionId;
        public int minCount;
        public int maxCount;
    }

    public class LooseLootSpawnPoint : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Probability this loot spawns (0 = never, 1 = always)")]
        public float spawnChance = 1f;

        [Tooltip("Use a predefined item group instead of the custom list below")]
        public bool useItemGroup = true;

        [Tooltip("Predefined item group (only when useItemGroup is true)")]
        public ItemGroup itemGroup = ItemGroup.Mixed;

        [Tooltip("Custom item pool (only when useItemGroup is false)")]
        public LooseLootDrop[] customItems;

        public (string definitionId, int count) RollItem()
        {
            if (useItemGroup)
            {
                var drops = ItemGroups.GetDrops(itemGroup);
                var drop = drops[Random.Range(0, drops.Length)];
                return (drop.DefinitionId, Random.Range(drop.MinCount, drop.MaxCount + 1));
            }

            if (customItems != null && customItems.Length > 0)
            {
                var pick = customItems[Random.Range(0, customItems.Length)];
                int count = Mathf.Max(1, Random.Range(pick.minCount, pick.maxCount + 1));
                return (pick.definitionId, count);
            }

            return (null, 0);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.35f);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            var label = useItemGroup
                ? $"Loot: {itemGroup} ({spawnChance:P0})"
                : $"Loot: Custom ({spawnChance:P0})";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, label);
        }
#endif
    }
}
