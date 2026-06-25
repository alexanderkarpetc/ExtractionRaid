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
    ///   • Loot-gated supply: candidates are mods the player actually owns in the backpack
    ///     (slot-match; CompatibleArchetype gating is P3). Install/remove mutation lives in
    ///     <see cref="AttachmentInstallSystem"/> (shared with inventory drag-drop install);
    ///     this presenter is a thin per-weapon view that adds the StateChanged event +
    ///     UI-facing LastError on top.
    ///   • Composition (incl. attachment deltas) reuses WeaponAssemblySystem.TryAssemble
    ///     so editor stats match the runtime weapon exactly.
    /// </summary>
    public class AttachmentEditorPresenter
    {
        // Slot taxonomy + which slots are unlocked (rarity-scaled) lives in
        // Systems.AttachmentSlots — shared with the inventory/tooltip.

        readonly ICoreDefinitionRegistry _registry;
        readonly InventoryState _inventory;
        readonly Func<EId> _allocateEId;
        ItemState _weapon;

        public event Action StateChanged;

        public AttachmentEditorPresenter(ICoreDefinitionRegistry registry,
                                         InventoryState inventory,
                                         Func<EId> allocateEId)
        {
            _registry    = registry;
            _inventory   = inventory;
            _allocateEId = allocateEId;
        }

        public ItemState Weapon => _weapon;
        public bool HasWeapon => _weapon != null && _weapon.HasWeaponConfiguration;

        /// <summary>
        /// Human-readable reason the last <see cref="Install"/>/<see cref="Remove"/> returned
        /// false (e.g. backpack full). Null after a successful action. UI-only feedback.
        /// </summary>
        public string LastError { get; private set; }

        public void Load(ItemState weapon)
        {
            _weapon = weapon;
            StateChanged?.Invoke();
        }

        /// <summary>The attachment currently installed in <paramref name="slot"/>, or null.</summary>
        public AttachmentInstance? InstalledIn(AttachmentSlot slot) =>
            AttachmentInstallSystem.InstalledIn(_weapon, slot);

        /// <summary>
        /// Attachments the player can put in <paramref name="slot"/> right now: the mod
        /// already installed there (so it can be removed/swapped) plus every slot-matching
        /// mod currently in the backpack (loot-gated supply, deduped). MVP: slot-match only —
        /// archetype compatibility (<see cref="AttachmentDefinition.CompatibleArchetype"/>)
        /// is enforced from P3 once unique mods exist.
        /// </summary>
        public IReadOnlyList<AttachmentDefinition> CompatibleMods(AttachmentSlot slot)
        {
            var list = new List<AttachmentDefinition>();
            if (_registry == null) return list;

            var seen = new HashSet<string>();

            // The installed mod always shows (even though it's no longer in the backpack)
            // so the player can detach or replace it.
            var installed = InstalledIn(slot);
            if (installed.HasValue
                && _registry.TryGetAttachment(installed.Value.DefinitionId, out var insDef) && insDef != null)
            {
                list.Add(insDef);
                seen.Add(insDef.Id);
            }

            // Everything else must be owned (present in the backpack).
            if (_inventory != null)
            {
                for (int i = 0; i < InventoryState.BackpackSize; i++)
                {
                    var item = _inventory.Backpack[i];
                    if (item == null || string.IsNullOrEmpty(item.DefinitionId)) continue;
                    if (seen.Contains(item.DefinitionId)) continue;
                    if (_registry.TryGetAttachment(item.DefinitionId, out var def) && def != null && def.Slot == slot
                        && AttachmentInstallSystem.ArchetypeMatches(_weapon, def, _registry))
                    {
                        list.Add(def);
                        seen.Add(def.Id);
                    }
                }
            }
            return list;
        }

        /// <summary>Total units of <paramref name="modId"/> the player owns in the backpack (sums stacks).</summary>
        public int CountInBackpack(string modId)
        {
            if (_inventory == null || string.IsNullOrEmpty(modId)) return 0;
            int total = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = _inventory.Backpack[i];
                if (item != null && item.DefinitionId == modId)
                    total += item.StackCount <= 0 ? 1 : item.StackCount;
            }
            return total;
        }

        /// <summary>
        /// Installs <paramref name="modId"/> into <paramref name="slot"/> via
        /// <see cref="AttachmentInstallSystem"/> (consume + swap-return). The editor installs
        /// into the focused slot, so a mod that belongs to a different slot is ignored.
        /// </summary>
        public bool Install(AttachmentSlot slot, string modId)
        {
            LastError = null;
            if (!HasWeapon || string.IsNullOrEmpty(modId) || _registry == null) return false;
            // Editor installs into the focused slot — ignore a mod that belongs elsewhere.
            if (!_registry.TryGetAttachment(modId, out var def) || def == null || def.Slot != slot) return false;

            bool ok = AttachmentInstallSystem.Install(_weapon, _registry, _inventory, _allocateEId, modId, out var err);
            LastError = err;
            if (ok) StateChanged?.Invoke();
            return ok;
        }

        /// <summary>
        /// Removes the mod in <paramref name="slot"/> (returns it to the backpack, recoverable).
        /// Returns false when the slot is empty or the backpack is full.
        /// </summary>
        public bool Remove(AttachmentSlot slot)
        {
            LastError = null;
            bool ok = AttachmentInstallSystem.Remove(_weapon, _inventory, _allocateEId, slot, out var err);
            LastError = err;
            if (ok) StateChanged?.Invoke();
            return ok;
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
            cfg.Attachments = AttachmentInstallSystem.WithSlot(cfg.Attachments, slot, set);
            return Compose(cfg);
        }

        // ── internals ─────────────────────────────────────────

        WeaponStats? Compose(WeaponConfiguration cfg)
        {
            if (WeaponAssemblySystem.TryAssemble(cfg, _registry, out var result, out _))
                return result.Stats;
            return null;
        }
    }
}
