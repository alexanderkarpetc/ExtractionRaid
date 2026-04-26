using Adapters;
using State;

namespace Systems
{
    /// <summary>
    /// Resolves the human-readable label for an <see cref="ItemState"/> in inventory UIs.
    /// For weapon items (built via Weapon Builder) this composes the archetype label
    /// (e.g. "Ballistic Pistol", "Laser Rifle") via <see cref="WeaponArchetypeLabel"/>.
    /// For non-weapon items it falls through to <see cref="ItemState.DisplayName"/>.
    ///
    /// Items with a <see cref="WeaponConfiguration"/> whose modules are no longer
    /// resolvable in the registry (e.g. removed Payload SO, save-game from older
    /// content) render as <see cref="BrokenLabel"/> so the player has a visible
    /// signal something is wrong with that slot.
    ///
    /// View-layer helper: callers (InventorySlotView, EquipmentSlotView, hotbar) pass
    /// <c>App.Instance.CoreDefinitions</c>. Pure C#, registry-only — no Unity refs.
    /// </summary>
    public static class WeaponDisplayName
    {
        public const string BrokenLabel = "[Broken Weapon]";

        public static string For(ItemState item, ICoreDefinitionRegistry registry)
        {
            if (item == null) return string.Empty;
            if (!item.HasWeaponConfiguration) return item.DisplayName;
            if (registry == null) return BrokenLabel;

            var payloadId  = item.WeaponConfiguration.Payload.DefinitionId;
            var deliveryId = item.WeaponConfiguration.Delivery.DefinitionId;

            registry.TryGetPayload(payloadId,   out var payloadDef);
            registry.TryGetDelivery(deliveryId, out var deliveryDef);

            if (payloadDef == null || deliveryDef == null) return BrokenLabel;
            return WeaponArchetypeLabel.Compose(payloadDef, deliveryDef);
        }
    }
}
