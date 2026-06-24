using System;
using System.Collections.Generic;
using Adapters;
using State;

namespace Systems
{
    /// <summary>
    /// Stateless rules for installing / removing weapon attachments on a built weapon's
    /// <see cref="WeaponConfiguration.Attachments"/>, with a loot-gated, recoverable supply:
    /// installing consumes one unit of the mod from the backpack; removing or swapping returns
    /// the displaced mod (blocked when the backpack is full). Shared by the attachment editor
    /// (<c>AttachmentEditorPresenter</c>) and inventory drag-drop install so the behaviour is
    /// single-sourced.
    ///
    /// MVP scope (see docs/ai/weapon-builder/attachments): the slot is derived from the mod
    /// definition; <see cref="CanInstall"/> accepts any built weapon — CompatibleArchetype
    /// gating arrives in P3. Mutations bump <see cref="ItemState.WeaponConfigVersion"/> (D6
    /// live equipped-weapon resync) and <see cref="InventoryState.Version"/>.
    /// </summary>
    public static class AttachmentInstallSystem
    {
        /// <summary>Resolves an item/mod id to its attachment definition (item id == SO id), or null.</summary>
        public static AttachmentDefinition Resolve(ICoreDefinitionRegistry registry, string modId)
        {
            if (registry == null || string.IsNullOrEmpty(modId)) return null;
            return registry.TryGetAttachment(modId, out var def) ? def : null;
        }

        /// <summary>
        /// Whether <paramref name="weapon"/> can currently accept <paramref name="modDef"/> —
        /// the drop-target / cross-highlight predicate. MVP: any built weapon accepts any mod;
        /// archetype compatibility is enforced from P3.
        /// </summary>
        public static bool CanInstall(ItemState weapon, AttachmentDefinition modDef)
        {
            if (weapon == null || !weapon.HasWeaponConfiguration || modDef == null) return false;
            // P3: gate on modDef.CompatibleArchetype vs the weapon's payload/delivery archetype.
            return true;
        }

        /// <summary>
        /// Like <see cref="CanInstall"/> but additionally requires the mod's slot to be
        /// <b>empty</b> — i.e. the mod can be added without displacing an existing one.
        /// Used by the inventory cross-highlight ("can fill" vs "would swap").
        /// </summary>
        public static bool CanInstallIntoFreeSlot(ItemState weapon, AttachmentDefinition modDef)
        {
            return CanInstall(weapon, modDef) && !InstalledIn(weapon, modDef.Slot).HasValue;
        }

        /// <summary>The attachment installed in <paramref name="slot"/> on this weapon, or null.</summary>
        public static AttachmentInstance? InstalledIn(ItemState weapon, AttachmentSlot slot)
        {
            if (weapon == null || !weapon.HasWeaponConfiguration) return null;
            var arr = weapon.WeaponConfiguration.Attachments;
            if (arr == null) return null;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i].Slot == slot && !string.IsNullOrEmpty(arr[i].DefinitionId))
                    return arr[i];
            return null;
        }

        /// <summary>
        /// Installs <paramref name="modId"/> (slot derived from its definition), consuming one
        /// unit from the backpack and swapping + returning any displaced mod. Returns false
        /// (with a reason in <paramref name="error"/>) when the weapon/mod is invalid, the
        /// player doesn't own the mod, the mod is already installed, or a swap can't be returned.
        /// </summary>
        public static bool Install(ItemState weapon, ICoreDefinitionRegistry registry,
                                   InventoryState inventory, Func<EId> allocateEId,
                                   string modId, out string error)
        {
            error = null;
            if (weapon == null || !weapon.HasWeaponConfiguration) return false;
            var def = Resolve(registry, modId);
            if (def == null) return false;

            var slot = def.Slot;
            var prev = InstalledIn(weapon, slot);
            if (prev.HasValue && prev.Value.DefinitionId == modId) return false; // already installed

            // Loot-gated: must own the mod to install it.
            if (!ConsumeFromBackpack(inventory, modId)) { error = "You don't own this mod."; return false; }

            // Swap: the displaced mod goes back to the backpack. If there's no room, roll the
            // consume back and abort so nothing is lost.
            if (prev.HasValue && !string.IsNullOrEmpty(prev.Value.DefinitionId))
            {
                if (!ReturnToBackpack(inventory, prev.Value.DefinitionId, allocateEId))
                {
                    ReturnToBackpack(inventory, modId, allocateEId); // undo the consume
                    error = "Backpack full — free a slot to swap this mod.";
                    return false;
                }
            }

            weapon.WeaponConfiguration.Attachments =
                WithSlot(weapon.WeaponConfiguration.Attachments, slot, new AttachmentInstance(slot, modId));
            weapon.WeaponConfigVersion++;
            return true;
        }

        /// <summary>
        /// Removes the mod in <paramref name="slot"/> and returns it to the backpack
        /// (recoverable). Returns false when the slot is empty or the backpack is full.
        /// </summary>
        public static bool Remove(ItemState weapon, InventoryState inventory,
                                  Func<EId> allocateEId, AttachmentSlot slot, out string error)
        {
            error = null;
            if (weapon == null || !weapon.HasWeaponConfiguration) return false;

            var prev = InstalledIn(weapon, slot);
            if (!prev.HasValue || string.IsNullOrEmpty(prev.Value.DefinitionId)) return false;

            // Recoverable: the mod returns to the backpack. Blocked when there's no room.
            if (!ReturnToBackpack(inventory, prev.Value.DefinitionId, allocateEId))
            {
                error = "Backpack full — free a slot to detach this mod.";
                return false;
            }

            weapon.WeaponConfiguration.Attachments =
                WithSlot(weapon.WeaponConfiguration.Attachments, slot, null);
            weapon.WeaponConfigVersion++;
            return true;
        }

        /// <summary>
        /// New attachment array with <paramref name="slot"/> set to <paramref name="set"/>
        /// (replacing any existing entry in that slot), or removed when null. Other slots
        /// preserved. Pure — used for both mutation and non-mutating stat preview.
        /// </summary>
        public static AttachmentInstance[] WithSlot(AttachmentInstance[] current, AttachmentSlot slot, AttachmentInstance? set)
        {
            var list = new List<AttachmentInstance>();
            if (current != null)
                for (int i = 0; i < current.Length; i++)
                    if (current[i].Slot != slot && !string.IsNullOrEmpty(current[i].DefinitionId))
                        list.Add(current[i]);
            if (set.HasValue) list.Add(set.Value);
            return list.ToArray();
        }

        // Removes one unit of modId from the backpack (decrement a stack, or null the slot
        // when it hits zero). Returns false when the player owns none. Bumps inventory Version.
        static bool ConsumeFromBackpack(InventoryState inventory, string modId)
        {
            if (inventory == null || string.IsNullOrEmpty(modId)) return false;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inventory.Backpack[i];
                if (item == null || item.DefinitionId != modId) continue;
                if (item.StackCount > 1) item.StackCount--;
                else inventory.Backpack[i] = null;
                inventory.Version++;
                return true;
            }
            return false;
        }

        // Adds one unit of modId back to the backpack (stack-then-overflow). Returns false
        // when the backpack is full. Bumps inventory Version (via InventorySystem).
        static bool ReturnToBackpack(InventoryState inventory, string modId, Func<EId> allocateEId)
        {
            if (inventory == null || allocateEId == null || string.IsNullOrEmpty(modId)) return false;
            return InventorySystem.AddToBackpack(inventory, modId, 1, allocateEId) > 0;
        }
    }
}
