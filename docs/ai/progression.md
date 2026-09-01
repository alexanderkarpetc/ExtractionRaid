# Character Progression

The player has a permanent node web split into themed disciplines. There are no skill points and no
refund: a node is unlocked by consuming its authored material/weapon cost, and eventually every node
may be acquired.

## Data and authoring

`ProgressionTreeConfig` is the definition asset; player state stores only allocated stable node IDs.
Node list order and authored ring/offset values define layout and connectivity.

Create the asset through `Raid → Progression → Create & Seed Config Asset`. Full reseed overwrites
authored data. Use the cost-only reseed action when retuning economy without replacing node effects.
The runtime default tree is a fallback, not a second manually maintained document.

## Allocation contract

- A node must exist, be unallocated and connect to an allocated parent (first-ring nodes connect to
  the discipline hub).
- `ProgressionCostSystem` validates the entire cost before consuming anything.
- Material consumption uses stash then backpack; equipped items are excluded.
- Weapon costs match both core IDs and minimum rarity.
- Successful allocation is append-only and persists across raids/death.

UI calls the systems for connectivity, affordability and unlock; it does not own those rules.

## Gameplay effects

`ProgressionSystem.ApplyAllocatedEffects` is the single extension seam. Effects must modify explicit
`RaidContext.*Config` values before systems run; systems must not read progression state directly.
Implementation status and acceptance live in `tasks.md`.

Progression is persistent profile state, so it is intentionally outside the raid-only State Debugger.
