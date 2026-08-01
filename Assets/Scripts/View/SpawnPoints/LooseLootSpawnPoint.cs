using Constants;
using Systems;
using UnityEngine;

namespace View.SpawnPoints
{
    [System.Serializable]
    public struct LooseLootDrop
    {
        [ItemIdPicker]
        [Tooltip("Item definition ID (e.g. Medkit, Ammo_Rifle)")]
        public string definitionId;

        [Tooltip("Leave Max Count at 0 to take the stack size from ItemBalance.")]
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

        // Rolling rules live in LootRoller (the view only forwards its authored config): the
        // group decides WHAT KIND of item lies here, ItemBalance decides which one and how many.
        public (string definitionId, int count) RollItem()
        {
            if (useItemGroup)
            {
                return LootRoller.TryRollPool(ItemGroups.GetPool(itemGroup), out var id)
                    ? (id, Mathf.Max(1, LootRoller.RollCount(id)))
                    : (null, 0);
            }

            if (customItems != null && customItems.Length > 0)
            {
                var pick = customItems[Random.Range(0, customItems.Length)];
                int count = pick.maxCount > 0
                    ? Mathf.Max(1, Random.Range(pick.minCount, pick.maxCount + 1))
                    : Mathf.Max(1, LootRoller.RollCount(pick.definitionId));
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
