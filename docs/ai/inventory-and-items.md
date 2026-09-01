# Inventory, Items and Meta Systems

## Ownership

- `ItemDefinition` is immutable catalog data; `ItemState` is a concrete owned instance/stack.
- Player inventory owns equipped slots, backpack and quick-slot bindings.
- Stash is persistent meta inventory; loot containers and ground items are raid state.
- Weapon Builder configuration and armor durability travel with the item instance.

Catalog entries, recipes, container types and balancing values live in code/assets and are not
mirrored here.

## Inventory invariants

- Slot compatibility is validated by systems, never by UI alone.
- Stack operations respect definition identity and maximum stack size.
- Cross-inventory transfers are atomic: destination acceptance is established before source removal.
- Swap/move/drop operations preserve instance data, including weapon configuration and durability.
- Quick-slot bindings refer to inventory positions/IDs and are invalidated when their item disappears.
- Every mutation increments the inventory version used by UI and weapon synchronization.

## Equipment and durability

Equipping projects item data into raid runtime state. Before replacement, raid durability is written
back to the owned item; the newly equipped item then rebuilds runtime armor/weapon state. KIA and
extraction apply persistence policy after this synchronization.

## Loot

Loot generation is data-driven by `ItemBalance`/container configuration. Systems create item state;
views only display and route transfer requests. Corpse loot is produced from the victim's actual
equipment/inventory. Ammo drops resolve from the weapon payload caliber, preventing unusable ammo.

## Crafting, shop and building

These systems share the same ownership rule: validate the entire cost first, consume through system
helpers, then grant the result. UI previews costs but never performs authoritative accounting.
Progression/building material consumption prefers stash before backpack so prepared raid gear is not
silently consumed when persistent storage covers the cost.

## Quests

Quest definitions are immutable data; player quest state stores accepted/completed progress and
reward status. Gameplay systems emit progress signals; `QuestSystem` interprets them and grants
rewards. NPC/UI code only presents offers and routes accept/hand-in actions.

## UI contract

Inventory UI is UI Toolkit. It calls system operations for move, split, transfer, equip, shop and
craft actions, then refreshes from inventory versions. Inventory intentionally allows movement and
combat while open; Attack/ADS are blocked when the pointer is over UI. Blocking modals use the shared
window policy. Visual drag state is never authoritative.

## Persistence

Save data includes owned item instances, stash, weapon configurations, durability, quest/building
progress and progression allocation. Any new instance field needs explicit save/load coverage and a
migration decision; the release migration task is tracked in `tasks.md`.
