# ExtractionRaid — Release Scope

> Product boundary for v1.0. Task status and execution order live only in
> [`tasks.md`](./tasks.md).

## Release target

ExtractionRaid v1.0 is a complete single-player top-down extraction loop:

`hideout → prepare → deploy → fight/loot → extract or die → persist → improve loadout`

The release is EN-only and targets two authored combat maps plus the hideout.

## Product pillars

### Extraction loop

- Death removes carried gear while the stash survives.
- An empty inventory receives a minimal anti-soft-lock loadout.
- Combat raids have a time limit and explicit extraction points.
- Extraction, KIA, inventory, stash, quests and upgrades persist coherently.

### Weapon Builder

- Headline feature: a weapon is `Payload Core + Delivery Core` with rarity and typed attachments.
- Release content target: 3 payloads × 4 deliveries, plus 3–4 exotics.
- Modules enter through the raid/loot economy; the Builder must clearly explain the resulting weapon.
- Full 4×5 coverage, Foam/Swarm and composite backpack icons are not release requirements.

### Progression and economy

- Progression is a permanent node web paid with looted materials; there are no skill points or refunds.
- Node effects must modify gameplay through explicit `RaidContext.*Config` paths.
- Shops, crafting, stash, quests and building upgrades form the meta economy.

### Content and presentation

- Two authored combat maps and one hideout.
- First quests serve as onboarding; there is no separate tutorial campaign.
- Player-facing items require readable icons.
- Combat, UI, ambience and music need a coherent audio mix.
- Standalone performance and save compatibility are release gates.

## Explicitly outside v1.0

- Procedural maps.
- Localization beyond English.
- Secure container and insurance.
- Full 4×5 Weapon Builder matrix.
- Broad status-effect catalog beyond the existing bleeding baseline.
- Deep simulated bot life such as factions, autonomous looting and extraction.

For work status and ownership conflicts, use [`tasks.md`](./tasks.md).
