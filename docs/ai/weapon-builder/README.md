# Weapon Builder

Weapon Builder composes a weapon from two identity-defining cores:

- **Payload** — what the weapon launches (`Ballistic`, `Laser`, future `Rocket`).
- **Delivery** — how it fires (`SingleAction`, `Auto`, `Scatter`, future `Rotary`).

Typed attachments tune stats but do not replace the core identity. Rarity belongs to the two cores;
runtime weapon stats are composed and cached. Release tasks live only in [`../tasks.md`](../tasks.md).

## Current system

- Player and bots use `WeaponConfiguration` with payload/delivery core instances.
- `WeaponStatComposer` resolves core definitions, rarity scaling and installed attachments.
- `WeaponAssemblySystem` creates runtime weapon state from the configuration.
- Inventory items persist the configuration; `WeaponSyncSystem` keeps equipped state synchronized.
- Builder UI, module loot, compare tooltips, rarity-scaled attachment slots and edit-existing flow work.
- Modular runtime visuals attach the payload mesh to delivery-owned sockets.

## Architecture

```text
ItemState.WeaponConfiguration
    ├── PayloadCoreInstance (definition id + rarity)
    ├── DeliveryCoreInstance (definition id + rarity)
    └── installed attachments
             ↓
WeaponStatComposer + WeaponAssemblySystem
             ↓
WeaponEntityState (cached stats + mutable combat state)
             ↓
ShootingSystem / WeaponStateMachineSystem / WeaponSyncSystem
```

Rules:

- Core definitions are ScriptableObjects in the `CoreDefinitionDatabase`.
- State stores IDs, rarity and values, never Unity object references.
- Systems do not load assets and do not call `App.Instance`.
- Presentation resolves prefabs and sockets from authored core definitions.
- New behavior belongs in explicit systems/hooks, not string switches in views.

## Key files

| Concern | Path |
|---|---|
| Core definitions | `Assets/Scripts/WeaponBuilder/*CoreDefinition.cs` |
| Database | `Assets/Scripts/WeaponBuilder/CoreDefinitionDatabase.cs` |
| Persistent configuration | `Assets/Scripts/State/WeaponConfiguration.cs` |
| Stat composition | `Assets/Scripts/Systems/WeaponStatComposer.cs` |
| Runtime assembly | `Assets/Scripts/Systems/WeaponAssemblySystem.cs` |
| Equipped synchronization | `Assets/Scripts/Systems/WeaponSyncSystem.cs` |
| Builder UI | `Assets/Scripts/View/UI/WeaponBuilder/` |
| Attachments | [`attachments/README.md`](./attachments/README.md) |
| Runtime weapon behavior | [`../weapons.md`](../weapons.md) |

## Adding a core

1. Add or extend the explicit behavior enum/data shape.
2. Create the core definition ScriptableObject and register it in `CoreDefinitionDatabase`.
3. Add behavior in the relevant stateless system and configuration plumbing in `RaidContext` when tunable.
4. Add presentation assets through the Unity Editor; do not reconstruct prefabs from YAML.
5. Add composition, behavior, ammo-availability and persistence tests.
6. Update this reference only if the system contract changed; update task status in `tasks.md`.
