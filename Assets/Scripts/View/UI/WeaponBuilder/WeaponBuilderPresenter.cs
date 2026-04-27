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

        /// <summary>
        /// True when the currently selected payload triggers a charge-up phase before
        /// every shot. Hint shown in the preview pane before the player builds, so they
        /// don't get surprised by "click attack → nothing fires" after equip.
        /// </summary>
        public bool PreviewRequiresCharge =>
            WeaponChargeResolver.RequiresChargeUp(TryGetPayloadDefinition());

        /// <summary>
        /// Charge duration (seconds) of the currently selected payload at its selected
        /// rarity. 0 for non-charge-up payloads — caller should check
        /// <see cref="PreviewRequiresCharge"/> first.
        /// </summary>
        public float PreviewChargeTime
        {
            get
            {
                var def = TryGetPayloadDefinition();
                return def != null
                    ? WeaponChargeResolver.GetChargeTime(def, State.SelectedPayload.Rarity)
                    : 0f;
            }
        }

        /// <summary>
        /// One-line flavor text describing how the current archetype feels in play
        /// (e.g. "Reliable single-shot sidearm"). Empty when one of the slots is
        /// unselected or the combination is not yet mapped in
        /// <see cref="WeaponArchetypeFlavor"/>.
        /// </summary>
        public string PreviewArchetypeFlavor
        {
            get
            {
                if (!State.HasPayload || !State.HasDelivery) return string.Empty;
                return WeaponArchetypeFlavor.For(
                    State.SelectedPayload.DefinitionId,
                    State.SelectedDelivery.DefinitionId);
            }
        }

        /// <summary>
        /// Human-readable reason why the Build action is unavailable. Empty string when
        /// <see cref="CanBuild"/> is true. Shown as the disabled Build button's tooltip
        /// so the player can see what's blocking them without trial-and-error.
        /// Order: missing payload → missing delivery → missing slot.
        /// </summary>
        public string DisabledReason
        {
            get
            {
                if (!State.HasPayload)  return "Select a payload";
                if (!State.HasDelivery) return "Select a delivery";
                if (!TryResolveSelection(out _, out _)) return "Selected modules unavailable";
                if (_inventory.FindFreeBackpackSlot() < 0) return "Backpack is full";
                return string.Empty;
            }
        }

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

            // Grant a reserve of matching ammo so the build is usable immediately —
            // without this the player builds a Laser, fires the full magazine, then
            // the first reload finds 0 EnergyCells in inventory and the weapon is dead.
            // 2× MagazineSize is enough for one full reload + a partial. Silent if
            // there's no inventory room — Build itself does not fail.
            GrantAmmoReserve(payloadDef.AmmoType, stats.MagazineSize * 2);

            failReason = null;
            return true;
        }

        /// <summary>
        /// Stack-then-overflow ammo grant. Mirrors <c>InventorySystem.TryPickUp</c>'s
        /// pattern: phase 1 fills partial stacks of the same definition, phase 2 takes
        /// remaining free slots. Returns silently when ammo type is unknown or there's
        /// no room — this is a UX courtesy, not a Build precondition.
        /// </summary>
        void GrantAmmoReserve(string ammoType, int amount)
        {
            if (string.IsNullOrEmpty(ammoType) || amount <= 0) return;
            var def = ItemDefinition.Get(ammoType);
            if (def == null || def.MaxStackSize <= 0) return;

            // Phase 1: fill existing partial stacks of this ammo type.
            for (int i = 0; i < InventoryState.BackpackSize && amount > 0; i++)
            {
                var slot = _inventory.Backpack[i];
                if (slot == null || slot.DefinitionId != ammoType) continue;
                int space = def.MaxStackSize - slot.StackCount;
                if (space <= 0) continue;
                int add = amount < space ? amount : space;
                slot.StackCount += add;
                amount -= add;
            }

            // Phase 2: overflow into free slots, capped at MaxStackSize per slot.
            while (amount > 0)
            {
                int freeSlot = _inventory.FindFreeBackpackSlot();
                if (freeSlot < 0) return;
                int add = amount < def.MaxStackSize ? amount : def.MaxStackSize;
                _inventory.Backpack[freeSlot] = ItemState.Create(_allocateEId(), ammoType, add);
                amount -= add;
            }
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
