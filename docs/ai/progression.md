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

### System map

| Concern | Owner |
|---|---|
| Definition asset and node effect type | `ProgressionTreeConfig` |
| Built-in 96-node fallback | `ProgressionTreeDefaults` |
| Material/weapon cost generation | `ProgressionCostDefaults` |
| Connectivity, allocation and effect aggregation | `ProgressionSystem` |
| Authoritative payment and unlock | `ProgressionCostSystem` |
| Persistent allocated node IDs | `PlayerProgressionState` / `SaveData.AllocatedNodes` |
| Runtime UI | `ProgressionWindow`, opened with **K** |
| Production effect wiring | `RaidSession` → `RaidContext.PlayerProgressionConfig` |

### Disciplines

Each discipline contains four six-node branches. The authored values describe intended identity; only
the Predator effects listed under runtime coverage are currently connected to gameplay.

| Discipline | Identity | Branches |
|---|---|---|
| Warden | Survivability, armor and trophies | Flesh, Plating, Spoils, Presence |
| Phantom | Vision, stealth, rare loot and mobility | Sight, Sound, Fortune, Fleet |
| Predator | Weapon power, handling, kill momentum and boss hunting | Lethality, Handling, Bloodlust, The Hunt |
| Prospector | Carry capacity, loot economy, endurance and escape | Haul, Fortune, Endurance, Getaway |

Nodes use stable IDs in the form `<discipline>.<branchIndex>.<nodeIndex>`. Reordering labels or visual
layout must not silently change an existing node ID because saves persist those IDs.

## Allocation contract

- A node must exist, be unallocated and connect to an allocated parent (first-ring nodes connect to
  the discipline hub).
- `ProgressionCostSystem` validates the entire cost before consuming anything.
- Material consumption uses stash then backpack; equipped items are excluded.
- Weapon costs match both core IDs and minimum rarity.
- Successful allocation is append-only and persists across raids/death.

UI calls the systems for connectivity, affordability and unlock; it does not own those rules.
`ProgressionSystem.Allocate` checks structure only; gameplay and UI must call
`ProgressionCostSystem.TryUnlock` so payment cannot be skipped accidentally.

A normal node may cost item stacks and/or an assembled weapon with required payload core, delivery
core and minimum rarity. The whole cost is validated before anything is consumed. Materials are taken
stash-first, then backpack; equipped weapons and armor are excluded.

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

### Predator effect matrix

Percent values aggregate additively inside the tree and are then converted to multipliers. Max HP is
an additive flat value.

| Branch | Node effect | Value | Runtime |
|---|---|---:|---|
| Lethality | Weapon Damage | +6% twice | Active |
| Lethality | Penetration | +8% | Active |
| Lethality | Armor Damage | +15% | Active |
| Lethality | Headshot Damage (`Executioner`) | +45% | Active |
| Lethality | `Apex Predator` | boss/PMC damage and extra drop | Data-only |
| Handling | Recoil | -15% | Active |
| Handling | Recoil Recovery | +25% | Active |
| Handling | Reload Time | -15% | Active |
| Handling | Aim Sway (`Steady Hands`) | -30% | Data-only |
| Handling | Equip Time | -20% | Active |
| Handling | Heat Buildup (`Cold Barrel`) | -35% | Active |
| Bloodlust | Max HP | +8 and +10 | Active |
| Bloodlust | Heal per Kill | +5 HP | Data-only |
| Bloodlust | Bleed Applied | +25% | Active |
| Bloodlust | Stamina per Kill (`Adrenaline`) | +20% | Data-only |
| Bloodlust | `Berserk` | conditional fire rate and lifesteal | Data-only |
| The Hunt | Boss Spawn Chance | +20% and +15% | Data-only |
| The Hunt | Boss Kill Drops | +1 | Data-only |
| The Hunt | Credits from Loot | +10% | Data-only |
| The Hunt | `Tracker`, `Big Game` | reveal/spawn special behavior | Data-only |

`RaidSession` resolves these as player-only `PlayerProgressionConfig` values, so shared combat configs
do not buff bots. Maximum HP is also synchronized into the live player `HealthState` while preserving
the current health ratio. `PlayerPresenter` initializes and updates `WorldHealthBar` from that
authoritative state rather than the base `100 HP` constant.

The runtime path is:

1. `ProgressionCostSystem.TryUnlock` pays and appends the stable node ID.
2. `RaidSession` calls `ProgressionSystem.ApplyAllocatedEffects` every raid tick.
3. Player-only multipliers are copied into `RaidContext.PlayerProgressionConfig`.
4. Shooting, aiming and weapon-state systems consume the config without reading profile state.
5. Max HP is synchronized separately into `HealthState`; the current health ratio is preserved.

Aim sway, heal/stamina per kill, Predator boss/credit effects, Predator special nodes and all nodes in
the other three disciplines remain data-only. Their implementation status and acceptance live in
[`tasks.md`](./tasks.md).

Focused EditMode coverage lives in `ProgressionSystemTests`, `ProgressionCostSystemTests`,
`ShootingSystemTests`, `WeaponStateMachineSystemTests` and `PlayerSpawnSystemTests`.

Run it in Unity through `Window → General → Test Runner → EditMode`. Filter by
`ProgressionSystemTests` for aggregation/Max HP, or use **Run All** for the complete suite.

## UI and debugging

- Press **K** during Play Mode to open/close the tree.
- Node tooltips show authored effects and the full `have / need` cost.
- `Raid → Dev Cheats → Progression test points` grants temporary `+1`/`+10` unlock tokens.
- `Tools & Cheats → Show numeric HP on health bars` displays `current / max` on world health bars;
  it is disabled by default.
- There is no refund path or production skill-point pool.

Progression is persistent profile state, so it is intentionally outside the raid-only State Debugger.
