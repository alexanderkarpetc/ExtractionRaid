using System;
using System.Collections.Generic;
using Adapters;
using State;
using Systems;

namespace View.UI.WeaponBuilder
{
    /// <summary>
    /// Plain-C# presenter for the Weapon Builder UI (no MonoBehaviour, no UnityEngine
    /// types in its signatures — fully unit-testable). Owns transient selection state,
    /// exposes the list of available modules pulled from the registry, composes a live
    /// preview via <see cref="WeaponStatComposer"/>, and commits a built weapon to
    /// the player inventory on <see cref="TryBuild"/>.
    ///
    /// See docs/ai/weapon-builder/architecture.md §D11, §D12, §D14.
    /// </summary>
    public class WeaponBuilderPresenter
    {
        readonly ICoreDefinitionRegistry _registry;
        readonly InventoryState _inventory;
        readonly Func<EId> _allocateEId;

        public WeaponBuilderPresenter(
            ICoreDefinitionRegistry registry,
            InventoryState inventory,
            Func<EId> allocateEId)
        {
            _registry    = registry    ?? throw new ArgumentNullException(nameof(registry));
            _inventory   = inventory   ?? throw new ArgumentNullException(nameof(inventory));
            _allocateEId = allocateEId ?? throw new ArgumentNullException(nameof(allocateEId));
            State        = WeaponBuilderState.Empty;
        }

        /// <summary>Fires whenever the selection state changes.</summary>
        public event Action StateChanged;

        public WeaponBuilderState State { get; private set; }

        public IReadOnlyList<PayloadCoreDefinition>  AllPayloads   => _registry.AllPayloads;
        public IReadOnlyList<DeliveryCoreDefinition> AllDeliveries => _registry.AllDeliveries;

        // ── Selection mutators ────────────────────────────────

        public void SelectPayload(string definitionId)
        {
            var next = State;
            next.SelectedPayload = string.IsNullOrEmpty(definitionId)
                ? default
                : new PayloadCoreInstance(definitionId, RarityTier.Common);
            Apply(next);
        }

        public void SelectDelivery(string definitionId)
        {
            var next = State;
            next.SelectedDelivery = string.IsNullOrEmpty(definitionId)
                ? default
                : new DeliveryCoreInstance(definitionId, RarityTier.Common);
            Apply(next);
        }

        public void ClearSelection()
        {
            Apply(WeaponBuilderState.Empty);
        }

        void Apply(WeaponBuilderState next)
        {
            State = next;
            StateChanged?.Invoke();
        }

        // ── Preview (derived) ─────────────────────────────────

        /// <summary>
        /// Composed <see cref="WeaponStats"/> preview. Null if either core slot is
        /// unselected or the registry can't resolve the selection.
        /// </summary>
        public WeaponStats? PreviewStats
        {
            get
            {
                if (!TryResolveSelection(out var payloadDef, out var deliveryDef))
                    return null;
                return WeaponStatComposer.Compose(
                    payloadDef,  State.SelectedPayload.Rarity,
                    deliveryDef, State.SelectedDelivery.Rarity);
            }
        }

        /// <summary>
        /// Archetype label (e.g. "Ballistic Pistol"). Empty string when both slots are empty;
        /// otherwise returns whichever part is available (via <see cref="WeaponArchetypeLabel.Compose"/>).
        /// </summary>
        public string PreviewArchetype
        {
            get
            {
                var payloadDef  = TryGetPayloadDefinition();
                var deliveryDef = TryGetDeliveryDefinition();
                return WeaponArchetypeLabel.Compose(payloadDef, deliveryDef);
            }
        }

        /// <summary>
        /// True when both cores are selected, resolvable via registry, and backpack
        /// has at least one free slot to receive the built weapon.
        /// </summary>
        public bool CanBuild =>
            TryResolveSelection(out _, out _) &&
            _inventory.FindFreeBackpackSlot() >= 0;

        // ── Commit ────────────────────────────────────────────

        /// <summary>
        /// Materialises the current selection into an <see cref="ItemState"/> and drops
        /// it into the first free backpack slot. Magazine starts full.
        /// Returns false on any failure (no selection, invalid config, no free slot).
        /// </summary>
        public bool TryBuild(out string failReason)
        {
            if (!State.HasPayload)
            {
                failReason = "Select a payload before building.";
                return false;
            }
            if (!State.HasDelivery)
            {
                failReason = "Select a delivery before building.";
                return false;
            }
            if (!TryResolveSelection(out var payloadDef, out var deliveryDef))
            {
                failReason = "Selected modules are not present in the registry.";
                return false;
            }

            int freeSlot = _inventory.FindFreeBackpackSlot();
            if (freeSlot < 0)
            {
                failReason = "Backpack is full.";
                return false;
            }

            var stats = WeaponStatComposer.Compose(
                payloadDef,  State.SelectedPayload.Rarity,
                deliveryDef, State.SelectedDelivery.Rarity);

            var config = new WeaponConfiguration(
                State.SelectedPayload,
                State.SelectedDelivery,
                exotic: null,
                ammoInMagazine: stats.MagazineSize);

            // DefinitionId is set to "Weapon" as a generic marker — identity lives in
            // WeaponConfiguration. DisplayName is derived via WeaponArchetypeLabel.
            _inventory.Backpack[freeSlot] = ItemState.CreateWeapon(_allocateEId(), "Weapon", config);

            failReason = null;
            return true;
        }

        // ── Helpers ───────────────────────────────────────────

        PayloadCoreDefinition TryGetPayloadDefinition() =>
            State.HasPayload && _registry.TryGetPayload(State.SelectedPayload.DefinitionId, out var def)
                ? def
                : null;

        DeliveryCoreDefinition TryGetDeliveryDefinition() =>
            State.HasDelivery && _registry.TryGetDelivery(State.SelectedDelivery.DefinitionId, out var def)
                ? def
                : null;

        bool TryResolveSelection(
            out PayloadCoreDefinition payloadDef,
            out DeliveryCoreDefinition deliveryDef)
        {
            payloadDef  = TryGetPayloadDefinition();
            deliveryDef = TryGetDeliveryDefinition();
            return payloadDef != null && deliveryDef != null;
        }
    }
}
