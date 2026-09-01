# Weapon Attachments

Attachments are recoverable inventory items installed into typed slots on a composed Weapon Builder
weapon. They tune stats and feel; Payload + Delivery remain the weapon's behavior identity.

## Rules

- Editing an existing weapon is available from inventory and is not restricted to the workbench.
- Installed attachments persist inside `WeaponConfiguration` and survive equip/unequip/save flows.
- Compatibility and slot ownership are data-driven.
- Core rarity controls available slot count.
- Most attachments are sidegrades with a visible give/take; unique attachments may target one core.
- Mutations bump configuration/inventory versions so equipped weapons and UI refresh immediately.

## Player flow

The inventory supports right-click **Modify** and drag/drop between compatible attachment slots and
the backpack. Compatible targets highlight; invalid drops are rejected. Tooltips and weapon compare
show installed mods and resulting stat deltas.

The Sniper Scope is the deepest shipped example: it modifies sight range, accuracy/velocity/headshot
trade-offs, drives an ADS reveal circle in Fog of War, and uses the existing ergonomics-based aim
spring. See [`../../fog-of-war.md`](../../fog-of-war.md).

## Key files

| Concern | Path |
|---|---|
| Definitions and registry | `Assets/Scripts/WeaponBuilder/Attachments/` |
| Install/remove rules | `Assets/Scripts/Systems/AttachmentInstallSystem.cs` |
| Stat application | `Assets/Scripts/Systems/WeaponStatComposer.cs` |
| Editor window | `Assets/Scripts/View/UI/Attachments/AttachmentEditorWindow.cs` |
| Inventory interaction | `Assets/Scripts/View/UI/Inventory/InventoryWindow.cs` |
| Compare display | `Assets/Scripts/View/UI/Compare/` |

Balance and remaining work are tracked only in [`../../tasks.md`](../../tasks.md).
