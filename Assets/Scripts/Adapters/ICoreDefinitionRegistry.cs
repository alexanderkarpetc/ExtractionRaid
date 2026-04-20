using State;

namespace Adapters
{
    /// <summary>
    /// Port providing lookup of Weapon Builder core definitions by string Id.
    /// Systems use this to resolve <see cref="PayloadCoreInstance"/> and friends
    /// back to their full <see cref="PayloadCoreDefinition"/> at stat-composition
    /// and handler-dispatch time.
    ///
    /// Consistency:
    ///   - Get* throws KeyNotFoundException when id is missing.
    ///   - TryGet* returns bool and populates out parameter (default on miss).
    ///
    /// See docs/ai/weapon-builder/architecture.md §D3.
    /// </summary>
    public interface ICoreDefinitionRegistry
    {
        PayloadCoreDefinition  GetPayload(string id);
        DeliveryCoreDefinition GetDelivery(string id);
        ExoticModDefinition    GetExotic(string id);

        bool TryGetPayload(string id, out PayloadCoreDefinition definition);
        bool TryGetDelivery(string id, out DeliveryCoreDefinition definition);
        bool TryGetExotic(string id, out ExoticModDefinition definition);
    }
}
