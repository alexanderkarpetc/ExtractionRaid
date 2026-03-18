using UnityEngine;

namespace State
{
    public class LootableContainerState
    {
        public EId Id;
        public Vector3 Position;
        public string TypeId;
        public InventoryState Inventory;
        public bool IsContainer;

        public static LootableContainerState Create(EId id, Vector3 position, string typeId,
            InventoryState inventory, bool isContainer = false)
        {
            return new LootableContainerState
            {
                Id = id,
                Position = position,
                TypeId = typeId,
                Inventory = inventory,
                IsContainer = isContainer,
            };
        }
    }
}
