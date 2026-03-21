using Constants;
using State;

namespace Systems
{
    public static class CraftingSystem
    {
        public static int CountIngredient(InventoryState inv, string definitionId)
        {
            int total = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inv.Backpack[i];
                if (item != null && item.DefinitionId == definitionId)
                    total += item.StackCount;
            }
            return total;
        }

        public static bool CanCraft(InventoryState inv, in CraftRecipe recipe)
        {
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                if (CountIngredient(inv, recipe.Ingredients[i].DefinitionId) < recipe.Ingredients[i].Count)
                    return false;
            }
            return inv.FindFreeBackpackSlot() >= 0;
        }

        public static bool TryCraft(RaidState state, string recipeId)
        {
            if (!CraftConstants.TryGet(recipeId, out var recipe))
                return false;

            var inv = state.Inventory;
            if (!CanCraft(inv, in recipe))
                return false;

            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                ConsumeIngredient(inv, recipe.Ingredients[i].DefinitionId, recipe.Ingredients[i].Count);
            }

            int freeSlot = inv.FindFreeBackpackSlot();
            if (freeSlot < 0) return false;

            var resultId = state.AllocateEId();
            inv.Backpack[freeSlot] = ItemState.Create(resultId, recipe.ResultItemId, recipe.ResultCount);
            return true;
        }

        static void ConsumeIngredient(InventoryState inv, string definitionId, int amount)
        {
            int remaining = amount;
            for (int i = 0; i < InventoryState.BackpackSize && remaining > 0; i++)
            {
                var item = inv.Backpack[i];
                if (item == null || item.DefinitionId != definitionId) continue;

                if (item.StackCount <= remaining)
                {
                    remaining -= item.StackCount;
                    inv.Backpack[i] = null;
                }
                else
                {
                    item.StackCount -= remaining;
                    remaining = 0;
                }
            }
        }
    }
}
