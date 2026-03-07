namespace State
{
    public class ItemState
    {
        public EId Id;
        public string DefinitionId;

        public ItemDefinition Definition => ItemDefinition.Get(DefinitionId);
        public string DisplayName => Definition?.DisplayName ?? DefinitionId;

        public static ItemState Create(EId id, string definitionId)
        {
            return new ItemState { Id = id, DefinitionId = definitionId };
        }
    }
}
