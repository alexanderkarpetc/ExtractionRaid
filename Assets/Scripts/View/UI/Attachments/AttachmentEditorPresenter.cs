using System;
using System.Collections.Generic;
using Adapters;
using State;
using Systems;

namespace View.UI.Attachments
{
    /// <summary>
    /// Plain-C# presenter for the attachment editor (Option B — edit an existing weapon's
    /// attachments, invoked from the inventory, available anywhere). Unit-tested without
    /// the engine; the UI (P2.2b) is a thin view over this.
    ///
    /// MVP scope (see docs/ai/weapon-builder/attachments/edit-access.md):
    ///   • Edits the loaded weapon's <see cref="WeaponConfiguration.Attachments"/> live
    ///     (no separate Apply/Cancel — install/remove are immediate, like Division/Duckov).
    ///   • Fixed slot set (Payload: Optic/Magazine/Buttstock; Delivery: Muzzle/Grip).
    ///   • Infinite mod supply: candidates come from the registry (slot-match only;
    ///     CompatibleArchetype gating is P3). No backpack consume yet — loot-gating later.
    ///   • Composition (incl. attachment deltas) reuses WeaponAssemblySystem.TryAssemble
    ///     so editor stats match the runtime weapon exactly.
    /// </summary>
    public class AttachmentEditorPresenter
    {
        // Fixed MVP slot layout — grouped by the core that conceptually grants them
        // (rarity-scaled slot count is a later refinement; see slots.md).
        public static readonly AttachmentSlot[] PayloadSlots =
            { AttachmentSlot.Optic, AttachmentSlot.Magazine, AttachmentSlot.Buttstock };
        public static readonly AttachmentSlot[] DeliverySlots =
            { AttachmentSlot.Muzzle, AttachmentSlot.Grip };

        readonly ICoreDefinitionRegistry _registry;
        ItemState _weapon;

        public event Action StateChanged;

        public AttachmentEditorPresenter(ICoreDefinitionRegistry registry)
        {
            _registry = registry;
        }

        public ItemState Weapon => _weapon;
        public bool HasWeapon => _weapon != null && _weapon.HasWeaponConfiguration;

        public void Load(ItemState weapon)
        {
            _weapon = weapon;
            StateChanged?.Invoke();
        }

        /// <summary>The attachment currently installed in <paramref name="slot"/>, or null.</summary>
        public AttachmentInstance? InstalledIn(AttachmentSlot slot)
        {
            if (!HasWeapon) return null;
            var arr = _weapon.WeaponConfiguration.Attachments;
            if (arr == null) return null;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i].Slot == slot && !string.IsNullOrEmpty(arr[i].DefinitionId))
                    return arr[i];
            return null;
        }

        /// <summary>
        /// Registry attachments that fit <paramref name="slot"/>. MVP: slot-match only —
        /// archetype compatibility (<see cref="AttachmentDefinition.CompatibleArchetype"/>)
        /// is enforced from P3 once unique mods exist.
        /// </summary>
        public IReadOnlyList<AttachmentDefinition> CompatibleMods(AttachmentSlot slot)
        {
            var list = new List<AttachmentDefinition>();
            if (_registry == null) return list;
            var all = _registry.AllAttachments;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def != null && def.Slot == slot)
                    list.Add(def);
            }
            return list;
        }

        public void Install(AttachmentSlot slot, string modId)
        {
            if (!HasWeapon || string.IsNullOrEmpty(modId) || _registry == null) return;
            // Validate: the mod must exist and actually belong to this slot.
            if (!_registry.TryGetAttachment(modId, out var def) || def == null || def.Slot != slot) return;

            _weapon.WeaponConfiguration.Attachments =
                WithSlot(_weapon.WeaponConfiguration.Attachments, slot, new AttachmentInstance(slot, modId));
            StateChanged?.Invoke();
        }

        public void Remove(AttachmentSlot slot)
        {
            if (!HasWeapon) return;
            _weapon.WeaponConfiguration.Attachments =
                WithSlot(_weapon.WeaponConfiguration.Attachments, slot, null);
            StateChanged?.Invoke();
        }

        /// <summary>Composed stats of the weapon as currently configured (null if not loaded / unresolvable).</summary>
        public WeaponStats? CurrentStats => HasWeapon ? Compose(_weapon.WeaponConfiguration) : (WeaponStats?)null;

        /// <summary>
        /// Stats the weapon WOULD have if <paramref name="modId"/> were installed in
        /// <paramref name="slot"/> (null modId previews the slot emptied). Does NOT mutate
        /// the weapon — for live green/red delta on hover.
        /// </summary>
        public WeaponStats? PreviewWith(AttachmentSlot slot, string modId)
        {
            if (!HasWeapon) return null;
            var cfg = _weapon.WeaponConfiguration;
            AttachmentInstance? set = string.IsNullOrEmpty(modId)
                ? (AttachmentInstance?)null
                : new AttachmentInstance(slot, modId);
            cfg.Attachments = WithSlot(cfg.Attachments, slot, set);
            return Compose(cfg);
        }

        // ── internals ─────────────────────────────────────────

        WeaponStats? Compose(WeaponConfiguration cfg)
        {
            if (WeaponAssemblySystem.TryAssemble(cfg, _registry, out var result, out _))
                return result.Stats;
            return null;
        }

        // Returns a new array with the given slot set to `set` (replacing any existing entry
        // in that slot), or removed when `set` is null. Other slots preserved.
        static AttachmentInstance[] WithSlot(AttachmentInstance[] current, AttachmentSlot slot, AttachmentInstance? set)
        {
            var list = new List<AttachmentInstance>();
            if (current != null)
                for (int i = 0; i < current.Length; i++)
                    if (current[i].Slot != slot && !string.IsNullOrEmpty(current[i].DefinitionId))
                        list.Add(current[i]);
            if (set.HasValue) list.Add(set.Value);
            return list.ToArray();
        }
    }
}
