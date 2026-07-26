# Character Progression (Skill Tree)

PoE-style skill web: four themed **disciplines**, each a colour-coded sector of one
shared tree. **There are no skill points — a node's price is looted materials**, so as soon as
you own what a node asks for you can take it. **Allocation is permanent — there is no refund**
(the design goal is that a player eventually unlocks everything). State persists on the
player profile across raids and death.

Concept mockup (source of the node data & layout): `Assets/Concepts/progression_tree_concept.html`.

---

## Files

| Concern | File |
|---|---|
| Config asset (data) | `Assets/Scripts/Progression/ProgressionTreeConfig.cs` |
| Default 96-node content | `Assets/Scripts/Progression/ProgressionTreeDefaults.cs` |
| Material-cost curve (seed) | `Assets/Scripts/Progression/ProgressionCostDefaults.cs` |
| Allocation rules (stateless) | `Assets/Scripts/Progression/ProgressionSystem.cs` |
| Material cost rules | `Assets/Scripts/Systems/ProgressionCostSystem.cs` |
| Runtime state | `Assets/Scripts/State/PlayerProgressionState.cs` |
| UI window (UI Toolkit) | `Assets/Scripts/View/UI/Progression/ProgressionWindow.cs` |
| Hotkey (**K** to toggle) | `Assets/Scripts/View/UI/Progression/ProgressionHotkey.cs` |
| UXML / USS / PanelSettings | `Assets/Resources/UI/Progression/Progression.{uxml,uss}` + `ProgressionPanelSettings.asset` (sort order 120) |
| Editor: create/seed asset | `Assets/Scripts/Editor/ProgressionConfigMenu.cs` |
| Save wiring | `Session/Player.cs` (`ToSaveData`/`LoadFrom`) + `Save/SaveData.cs` (`AllocatedNodes`) |

Host is spawned in `AppBootstrap.Awake` next to the other UI Toolkit windows.

---

## Data model — the config asset

`ProgressionTreeConfig` is a `ScriptableObject` (`menuName: Progression/Progression Tree`).
Shape: **Disciplines → Branches → Nodes**.

- **Discipline**: `Id`, `DisplayName`, `Color`, `Tagline`, `AngleDeg` (sector direction), `Branches`.
- **Node** (`ProgressionNodeDef`): `Id` (stable, `"<disc>.<branch>.<index>"`), `DisplayName`,
  `Size` (`Minor`/`Notable`/`Keystone`), `Ring` + `Offset` (layout), numeric effect
  (`StatLabel` + `Magnitude` + `Unit`) **or** `Description` (keystone/special text),
  and `Cost` (up to 3 `ProgressionCostEntry` lines — the items charged on unlock, and the node's
  only price).
- **Cost entry** (`ProgressionCostEntry`): `Kind` + `Quantity`, then either
  - `Kind = Item` → `ItemId` (an `ItemDefinition` id, drawn as the shared item-picker dropdown), or
  - `Kind = Weapon` → `DeliveryId` + `PayloadId` + `MinRarity` — an **assembled weapon**: that core
    combination with *both* cores at `MinRarity` or better.

  Rarity gates **weapons only**: plain items carry no runtime rarity in this project (only weapon
  cores do, on `PayloadCoreInstance`/`DeliveryCoreInstance`), so an item line is just id × quantity.
- Layout constants also live on the asset: `HubRadius`, `RingBase`, `RingStep`, `BranchSpread[]`, `ForkScale`.

**Ordering is list order** — reorder disciplines/branches/nodes freely in the inspector.

### Getting the asset

The asset is **not** checked in; create it in-editor (its script guid resolves correctly this way):

> **`Raid → Progression → Create & Seed Config Asset`**

This writes `Assets/Resources/Configs/ProgressionTree.asset` seeded with the full default tree.
Re-running **re-seeds (overwrites)** — edit *after* creating. To retune only the material economy
without losing hand-edited node effects, use **`Raid → Progression → Reseed Node Costs`** (also on
the asset's context menu as *Reseed Node Costs*). `ProgressionTreeConfig.Instance`
prefers this asset and falls back to `ProgressionTreeDefaults.BuildRuntime()` when it's missing/empty,
so the window works even before the asset exists.

---

## State & persistence

`PlayerProgressionState` (on `Session.Player`, alongside `ProfileState`) holds only
`List<string> AllocatedNodeIds` — there is no point pool to persist. It round-trips through
`SaveData.AllocatedNodes`, loaded at `App.Initialize` and saved at raid end. KIA wipes inventory but
not the profile, so progression survives death — matching Level/Credits.

Legacy saves may still carry a `ProgressionPoints` value; the field is gone and any such value is
ignored on load.

---

## Rules — `ProgressionSystem` (stateless)

- `GetParents(branch, node)` — a node's nearest lower-ring neighbour(s) in its branch (keystones join
  all deepest). Ring-1 nodes have no parent and hang off the (free) discipline hub.
- `IsConnected` / `CanAllocate` (connected, not already taken) / `Allocate` (**append only, no refund**)
  / `AllocatedCount` (drives the "n / 96" header).
- **Connectivity only.** `Allocate` does *not* charge the node's items — call
  `Systems.ProgressionCostSystem.TryUnlock(cfg, player, id)` instead (the window does).
- `Summarize` — sums numeric perks by `StatLabel` (helper; the window's build-summary panel was removed).
- **`ApplyAllocatedEffects` is a STUB.** No per-node gameplay effect is wired yet — this is the single
  seam to fill in. Drive each node id (or `Summarize` by `StatLabel`) into the config field named in the
  node's `DevHook` (e.g. Max-HP → `BotConstants.PlayerMaxHp`, MoveSpeed →
  `MovementConfig.MoveSpeedMultiplier`, loot → `ContainerTypeConfig.MaxDrops`, boss odds →
  `BotSpawnPoint.spawnChance`). Per the DevCheats rule, effects should flow through `RaidContext.*Config`
  structs, not be read from state directly inside systems.

The view holds **no rules** — it calls `ProgressionSystem` for allocate/query and renders the result.

---

## Material cost — `ProgressionCostSystem`

A node costs exactly the lines in `ProgressionNodeDef.Cost` — nothing else gates it. Supply is
**Stash + Backpack**, consumed **stash-first** (so the raid loadout survives an unlock when the stash
covers it) — the same policy as `BuildingSystem`, whose `ConsumeMaterial` helper it reuses. Equipped
weapons/armour are never counted or taken.

- `Owned(player, entry)` / `Missing(...)` / `MissingLineCount(player, node)` — the have/need readout.
- `CanPay(player, node)` — every line covered (a node with an empty `Cost` list is free).
- `CanUnlock(cfg, player, id)` — connected + every line covered.
- `TryUnlock(cfg, player, id)` — all-or-nothing: charges items, then allocates.

A weapon line matches an `ItemState` with `HasWeaponConfiguration` whose `Delivery`/`Payload` core ids
equal the requested ones and whose **both** core rarities are ≥ `MinRarity`. When several match, the
cheapest (lowest core rarity) is taken.

### The curve (`ProgressionCostDefaults`)

Cost climbs outward, and within a ring it climbs with node size:

| Node | Asks for |
|---|---|
| ring 1-2 minor | 1 common material, small stack |
| ring 3 minor | common + uncommon module |
| ring 4-5 minor | common + uncommon + 1 rare component |
| notable (ring 3) | uncommon (bigger stack) + rare — no cheap scrap |
| notable (ring 4) | + an assembled **Rare** weapon |
| notable (ring 5) | + an assembled **Epic** weapon |
| keystone | bulk uncommon + 4-6 rare + an **Epic/Legendary** weapon |

Materials come from `ItemDefinition`'s crafting-material tiers via a per-discipline palette (Warden
burns structural plate, Phantom optics, Predator mechanics, Prospector power parts). Quantities are
rolled deterministically from the node id, so re-seeding is stable and inspector edits are the only
source of drift.

---

## UI notes

- Opened with **K** (`ProgressionHotkey`); blocks gameplay input while open; Esc closes it (it's listed in
  `PauseMenuWindow.CanOpen()` so Esc won't open pause underneath).
- Radial web: 4 hubs (no central core), connecting lines drawn with `Painter2D`, drag-to-pan + scroll-zoom.
- Node visuals are built per-node in `AddNode` (the UI-Toolkit equivalent of a prefab): opaque core,
  a soft radial **glow** (a code-generated tinted texture — UI Toolkit has no blur), and a crisp **halo**
  ring on allocated nodes. Colours/glow/halo are set inline per discipline in `Refresh`.
- The tooltip's **COST** block lists each line with a `have / need` readout — green when covered, red
  when short — plus a `READY` / `MISSING n OF m` status. Weapon lines are tinted by required rarity
  (`RarityVisuals.Color`) and carry a `Delivery + Payload` sub-line. Rows are rebuilt per hover.
- Node states are four, not three: **on**, **open** (reachable *and* affordable — glows),
  **unaffordable** (reachable but short on materials — accent ring, dimmed, no glow), and
  **locked** (parents not allocated).
- The header shows a single **UNLOCKED n / 96** counter; there is no points panel and no DEV-grant
  button (materials come from raids / the stash).
- Tuning knobs if visuals need adjusting: glow size = wrapper padding (`+22f` in `AddNode`), glow softness =
  falloff exponent in `GlowTexture()`, glyph centering nudge = the minor-node `translate` in `AddNode`.

---

## Not surfaced in the Raid State Debugger

`PlayerProgressionState` is persistent profile/meta state, not raid state, so it is intentionally **not**
shown in `RaidStateDebuggerWindow` (see the scoped rule in `CLAUDE.md §5.7`).
