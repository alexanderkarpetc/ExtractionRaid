namespace State
{
    public class ItemState
    {
        public EId Id;
        public string DefinitionId;
        public int StackCount = 1;

        public ItemDefinition Definition => ItemDefinition.Get(DefinitionId);
        public string DisplayName => Definition?.DisplayName ?? DefinitionId;

        public static ItemState Create(EId id, string definitionId, int stackCount = 1)
        {
            return new ItemState { Id = id, DefinitionId = definitionId, StackCount = stackCount };
        }
    }
}
