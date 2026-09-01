# Weapon Builder and Attachments

A weapon is composed from:

- **Payload** — what is launched;
- **Delivery** — how it fires;
- optional **Exotic** — behavior hook;
- typed **Attachments** — stat/feel tuning without replacing core identity.

Rarity belongs to payload/delivery cores. Persistent configuration stores stable definition IDs,
rarity and installed attachments; runtime assembly resolves definitions and caches final stats.

## Pipeline

```text
ItemState.WeaponConfiguration
    → definition registry
    → WeaponStatComposer / WeaponAssemblySystem
    → WeaponEntityState
    → firing, aiming and presentation
```

Inventory owns the configuration. `WeaponSyncSystem` rebuilds equipped runtime state when the source
item/version changes. Gameplay systems read cached stats; views resolve meshes and sockets.

## Attachments

- Slots and compatibility are data-driven and associated with the appropriate core domain.
- Core rarity controls available slot count.
- Install/remove is authoritative in `AttachmentInstallSystem` and preserves the item instance.
- Mutations bump configuration/inventory versions for equipped sync and UI refresh.
- Inventory supports edit-existing and compatible drag/drop; UI previews but does not decide
  compatibility or stat application.
- Most attachments are readable sidegrades; unique attachments may target one core.

## Extension workflow

1. Add/extend explicit definition and behavior data.
2. Register the definition in `CoreDefinitionDatabase`.
3. Implement behavior in a stateless system and route tunables through `RaidContext`.
4. Author prefabs/assets in Unity Editor; do not reconstruct them from YAML.
5. Add composition, behavior, ammo-availability and persistence tests.
6. Update this contract only if system boundaries changed; update work status in `tasks.md`.

Do not add string-based dispatch, Resources loads in systems, view-owned weapon rules or another
parallel stat-composition path.
