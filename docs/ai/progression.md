# Character Progression

The player has a permanent node web split into Warden, Phantom, Predator and Prospector. Normal play
has no skill points and no refund: a node is unlocked by consuming its authored material/weapon cost,
and eventually every node may be acquired.

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

## Play Mode testing

`PlayerProgressionState.DevUnlockPoints` is a temporary testing currency available through
`Raid → Dev Cheats → Progression test points`. One point replaces the material cost of one connected
node; it does not bypass connectivity and is consumed only after a successful allocation. Dev points
are deliberately omitted from save data and reset when the profile is loaded.

While dev points are available, the progression header and node tooltip show their availability.
This does not change the normal material-cost progression model.

## Gameplay effects

`ProgressionSystem.ApplyAllocatedEffects` is the single extension seam. Effects must modify explicit
`RaidContext.*Config` values before systems run; systems must not read progression state directly.
`ProgressionNodeDef.Effect` is the stable typed binding; node-ID/stat-label fallback keeps legacy
seeded assets functional.

Current runtime coverage is Predator phase 1:

- weapon damage, penetration, armor damage and headshot damage;
- recoil, recoil recovery, reload time, equip time and barrel-heat buildup;
- maximum HP and bleed application.

`RaidSession` resolves these as player-only `PlayerProgressionConfig` values, so shared combat configs
do not buff bots. Maximum HP is also synchronized into the live player `HealthState` while preserving
the current health ratio. `PlayerPresenter` initializes and updates `WorldHealthBar` from that
authoritative state rather than the base `100 HP` constant.

Aim sway, heal/stamina per kill, Predator boss/credit effects, Predator special nodes and all nodes in
the other three disciplines remain data-only. Their implementation status and acceptance live in
[`tasks.md`](./tasks.md).

Focused EditMode coverage lives in `ProgressionSystemTests`, `ProgressionCostSystemTests`,
`ShootingSystemTests`, `WeaponStateMachineSystemTests` and `PlayerSpawnSystemTests`.

Progression is persistent profile state, so it is intentionally outside the raid-only State Debugger.
