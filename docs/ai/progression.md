# Character Progression (Skill Tree)

PoE-style skill web: four themed **disciplines**, each a colour-coded sector of one
shared tree, allocated with points. **Allocation is permanent — there is no refund**
(the design goal is that a player eventually unlocks everything). State persists on the
player profile across raids and death.

Concept mockup (source of the node data & layout): `Assets/Concepts/progression_tree_concept.html`.

---

## Files

| Concern | File |
|---|---|
| Config asset (data) | `Assets/Scripts/Progression/ProgressionTreeConfig.cs` |
| Default 96-node content | `Assets/Scripts/Progression/ProgressionTreeDefaults.cs` |
| Allocation rules (stateless) | `Assets/Scripts/Progression/ProgressionSystem.cs` |
| Runtime state | `Assets/Scripts/State/PlayerProgressionState.cs` |
| UI window (UI Toolkit) | `Assets/Scripts/View/UI/Progression/ProgressionWindow.cs` |
| Hotkey (**K** to toggle) | `Assets/Scripts/View/UI/Progression/ProgressionHotkey.cs` |
| UXML / USS / PanelSettings | `Assets/Resources/UI/Progression/Progression.{uxml,uss}` + `ProgressionPanelSettings.asset` (sort order 120) |
| Editor: create/seed asset | `Assets/Scripts/Editor/ProgressionConfigMenu.cs` |
| Save wiring | `Session/Player.cs` (`ToSaveData`/`LoadFrom`) + `Save/SaveData.cs` (`AllocatedNodes`, `ProgressionPoints`) |

Host is spawned in `AppBootstrap.Awake` next to the other UI Toolkit windows.

---

## Data model — the config asset

`ProgressionTreeConfig` is a `ScriptableObject` (`menuName: Progression/Progression Tree`).
Shape: **Disciplines → Branches → Nodes**.

- **Discipline**: `Id`, `DisplayName`, `Color`, `Tagline`, `AngleDeg` (sector direction), `Branches`.
- **Node** (`ProgressionNodeDef`): `Id` (stable, `"<disc>.<branch>.<index>"`), `DisplayName`,
  `Size` (`Minor`/`Notable`/`Keystone`), `Ring` + `Offset` (layout), numeric effect
  (`StatLabel` + `Magnitude` + `Unit`) **or** `Description` (keystone/special text), `PointCost`,
  and `DevHook` (a designer note naming the config field this node *should* drive — not auto-wired).
- Layout constants also live on the asset: `HubRadius`, `RingBase`, `RingStep`, `BranchSpread[]`, `ForkScale`.

**Ordering is list order** — reorder disciplines/branches/nodes freely in the inspector.

### Getting the asset

The asset is **not** checked in; create it in-editor (its script guid resolves correctly this way):

> **`Raid → Progression → Create & Seed Config Asset`**

This writes `Assets/Resources/Configs/ProgressionTree.asset` seeded with the full default tree.
Re-running **re-seeds (overwrites)** — edit *after* creating. `ProgressionTreeConfig.Instance`
prefers this asset and falls back to `ProgressionTreeDefaults.BuildRuntime()` when it's missing/empty,
so the window works even before the asset exists.

---

## State & persistence

`PlayerProgressionState` (on `Session.Player`, alongside `ProfileState`) holds only ids:
`List<string> AllocatedNodeIds` + `int AvailablePoints`. It round-trips through
`SaveData.AllocatedNodes` / `ProgressionPoints`, loaded at `App.Initialize` and saved at raid end.
KIA wipes inventory but not the profile, so progression survives death — matching Level/Credits.

Points source is out of scope for now (`AvailablePoints` is just a stored pool). The window has a
**DEV +5** button to grant test points.

---

## Rules — `ProgressionSystem` (stateless)

- `GetParents(branch, node)` — a node's nearest lower-ring neighbour(s) in its branch (keystones join
  all deepest). Ring-1 nodes have no parent and hang off the (free) discipline hub.
- `IsConnected` / `CanAllocate` (connected + enough points) / `Allocate` (**append only, no refund**).
- `Summarize` — sums numeric perks by `StatLabel` (helper; the window's build-summary panel was removed).
- **`ApplyAllocatedEffects` is a STUB.** No per-node gameplay effect is wired yet — this is the single
  seam to fill in. Drive each node id (or `Summarize` by `StatLabel`) into the config field named in the
  node's `DevHook` (e.g. Max-HP → `BotConstants.PlayerMaxHp`, MoveSpeed →
  `MovementConfig.MoveSpeedMultiplier`, loot → `ContainerTypeConfig.MaxDrops`, boss odds →
  `BotSpawnPoint.spawnChance`). Per the DevCheats rule, effects should flow through `RaidContext.*Config`
  structs, not be read from state directly inside systems.

The view holds **no rules** — it calls `ProgressionSystem` for allocate/query and renders the result.

---

## UI notes

- Opened with **K** (`ProgressionHotkey`); blocks gameplay input while open; Esc closes it (it's listed in
  `PauseMenuWindow.CanOpen()` so Esc won't open pause underneath).
- Radial web: 4 hubs (no central core), connecting lines drawn with `Painter2D`, drag-to-pan + scroll-zoom.
- Node visuals are built per-node in `AddNode` (the UI-Toolkit equivalent of a prefab): opaque core,
  a soft radial **glow** (a code-generated tinted texture — UI Toolkit has no blur), and a crisp **halo**
  ring on allocated nodes. Colours/glow/halo are set inline per discipline in `Refresh`.
- Tuning knobs if visuals need adjusting: glow size = wrapper padding (`+22f` in `AddNode`), glow softness =
  falloff exponent in `GlowTexture()`, glyph centering nudge = the minor-node `translate` in `AddNode`.

---

## Not surfaced in the Raid State Debugger

`PlayerProgressionState` is persistent profile/meta state, not raid state, so it is intentionally **not**
shown in `RaidStateDebuggerWindow` (see the scoped rule in `CLAUDE.md §5.7`).
