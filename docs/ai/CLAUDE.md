# CLAUDE.md

This file is the repo-level operating contract for Claude Code.
Read it first. Follow it by default.

## Reference game

**Escape from Duckov** — extraction shooter. Core mechanics (grenade throwing, looting, inventory, raids) use this game as the gameplay reference.

## 1) Primary goal

Build fast without turning the project into spaghetti.

Architecture priorities:
- multiple entry points (Menu, direct Raid/Level, test scenarios)
- gameplay logic must be testable without scenes
- clear separation of state, logic, adapters, and view
- model is allowed to use Unity value types

## 2) Non-goals

Do not introduce these unless explicitly requested:
- pure domain architecture that avoids Unity types at all costs
- full ECS/DOTS as the base architecture
- heavy DI framework

## 3) Top rules (must follow)

1. The only global singleton is `App.Instance`.
2. Global access is allowed only through `App.Instance.Player` and `App.Instance.RaidSession`.
3. Gameplay rules live in stateless static systems.
4. Systems must not call `App.Instance`.
5. Systems must not keep hidden mutable static state.
6. State stores values and IDs only - never Unity object references.
7. Model/logic may use Unity value types (`Vector3`, `Quaternion`, `Bounds`, `LayerMask`, `Mathf`, `Unity.Mathematics`).
8. Model/logic must not store `MonoBehaviour`, `GameObject`, `Transform`, `Animator`, `Rigidbody`, `Collider` references.
9. Unity-facing access must go through ports/adapters passed via context.
10. View/Presenter must not contain gameplay rules.
11. New gameplay logic should be added in a system or system sub-function, not scattered across views.
12. Never add new singletons.
13. Keep diffs small and local.
14. Do not introduce new frameworks unless explicitly requested.
15. **Reload Domain is OFF** (`EditorSettings.m_EnterPlayModeOptions=1`) — static fields/events survive Play→Stop→Play. Reset any static cache via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (ref: `UiPanelHitTest.ResetCacheOnPlay`).

## 4) Core architecture

- `App` is the composition root.
- `RaidSession` is the runtime orchestrator.
- `RaidSession` owns runtime state and runs systems in a stable order.
- Systems contain gameplay rules and mutate explicit state.
- `RaidContext` carries read-only dependencies (ports, configs, events, constants).
- Presenter/View converts state and domain events into Unity visuals.

Detailed rules live here:
- `docs/ai/architecture.md`
- `docs/ai/entity-lifecycle.md`
- `docs/ai/testing-and-workflow.md`

## 5) Standard implementation workflow

When implementing a change:
1. Identify the affected state.
2. Identify required dependencies and add them to context via ports if needed.
3. Implement or adjust logic in systems.
4. Emit domain events for VFX/SFX/UI instead of calling Unity APIs directly.
5. Update presenter/view only for visualization, bindings, and callback routing.
6. Add or update tests when logic changes.
7. If new fields were added to any state class, update the Raid State Debugger
   (`Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`) to display them.
8. Show a file-level plan before editing.
9. Keep the change incremental.

## 6) DevCheats (runtime tuning)

DevCheats provides runtime-tunable parameters via ScriptableObject assets.

**Architecture:**
- `DevCheats.cs` — static accessor (thin wrapper, no state)
- `DevCheatsConfig.cs` — root SO at `Resources/Configs/DevCheatsConfig.asset`, holds `[SerializeField]` references to section SOs
- `Assets/Scripts/Dev/Sections/` — one file per section SO (21 DevCheats sections + 14 ViewCheats counterparts in the same folder)
- `DevCheatsWindow.cs` — Editor UI (`Raid → Dev Cheats`)

**Rules:**
1. Each section is a separate ScriptableObject class in its own file (Unity requirement — one SO class per file, filename = classname).
2. Section files live in `Assets/Scripts/Dev/Sections/`.
3. Section assets live in `Assets/Resources/Configs/DevCheats/`.
4. When adding a new section: create the SO class file, add `[SerializeField]` + property in `DevCheatsConfig.cs`, add accessors in `DevCheats.cs`, add `CreateSectionIfMissing` call in `DevCheatsWindow.CreateSectionAssets()`, add UI in `DevCheatsWindow.OnGUI()`.
5. After adding/renaming sections, run `Raid → Dev Cheats — Create Section Assets` to generate assets and apply migrated values.
6. All gameplay-tunable parameters should go through DevCheats, not hardcoded constants.
7. **Systems must not read `DevCheats.X` directly.** Tunable values go through `RaidContext.*Config` structs (`AimConfig`, `ShootingConfig`, …). `RaidSession.Tick` populates those from DevCheats when building the context. See `testing-and-workflow.md §1` for the testing rationale. Dev/test cheats (e.g. `GodMode`) flow through `CheatsConfig` — add new cheat toggles there as they appear (single point of plumbing, no per-cheat refactor). Known latent violations (2026-04-24, flagged for refactor): `ArmorSystem`, `PlayerFOVSystem`, `MovementSystem`. **Resolved 2026-05-15**: `DamageSystem` (now reads `context.CheatsConfig.GodMode`).

## 7) Unity Editor via MCP

When the Unity Editor for this project is open and the MCP for Unity bridge is listening on `127.0.0.1:6400`, prefer `mcp__unityMCP__*` tools over reading `.unity` / `.prefab` / `.asset` files as text.

Quick reference (full doc: `docs/ai/unity-mcp.md`):
- `read_console` — Unity console; **always call this first** for diagnostics.
- `find_gameobjects` — search by component / name / path. ⚠️ no wildcards; `by_path "/"` returns 0. Use `by_component "Transform"` with `include_inactive: true` to enumerate the scene.
- `manage_scene` / `manage_gameobject` / `manage_components` — scene + GO + component CRUD.
- `manage_scriptable_object` — read/write DevCheats SOs without opening the Editor manually.
- `apply_text_edits` / `script_apply_edits` — surgical C# edits with recompile signal.
- `run_tests` (async, returns `job_id`) + `get_test_job` polling — EditMode/PlayMode runs.
- `execute_menu_item` / `execute_code` — escape hatches for things not covered by typed tools.

Preconditions:
- Verify the bridge with `lsof -nP -iTCP:6400 -sTCP:LISTEN`. If down, **stop and ask the user** — do not fall back to scraping `.unity` files.
- Modifying actions (play/stop, edits, code execution, deploy_package, run_tests in PlayMode) require **explicit user confirmation** before each call.

When delegating Unity work to a subagent (Task / custom agent), pass the bridge-up assumption + a pointer to `docs/ai/unity-mcp.md` in the prompt — subagents inherit the tools but not this conversation's context.

## 8) Documentation

Project docs live в `docs/ai/`. Update the relevant doc when changing the system it describes — section 9 lists which doc maps to which area.

## 9) Task routing (read only what is relevant)

Read extra docs depending on the task:
- Architecture changes / new systems -> `docs/ai/architecture.md`
- Spawn/despawn, entity binding, callbacks, presenter wiring -> `docs/ai/entity-lifecycle.md`
- Tests, feature implementation flow, launch flow -> `docs/ai/testing-and-workflow.md`
- Weapons, ammo, reload, aiming, weapon stats -> `docs/ai/weapons.md`
- Weapon Builder (composition, modules, builder UI) — paused -> `docs/ai/weapon-builder/`
- **Combat polish (shipped state — hit feedback, camera shake, blood, ragdoll, decals)** -> `docs/ai/gunplay/`
- Crosshair / cursor overlay, weapon state visualization -> `docs/ai/crosshair.md`
- Fog of War, visibility, ray sweep, post-processing -> `docs/ai/fog-of-war.md`
- Interactable outline/highlight and material-property tweening -> `docs/ai/interactable-highlight.md`
- Armor system, penetration, durability, bleeding, feedback -> `docs/ai/battle-design-status.md`
- Need a competitor/reference game (pick by attribute, by UX/feel reputation, or by how much knowledge exists) -> `docs/ai/competitor-reference-db.md`
- Armor research (competitor analysis) -> `docs/ai/armor-research.md`
- Impact/armor VFX guide for artists -> `docs/ai/fx-artist-guide.md`
- UI Toolkit panel sizing / theme / sort order -> `docs/ai/ui-styling.md`
- Adding a new UI Toolkit window/overlay (files, C# skeleton, registration, Esc/pause gating) -> `docs/ai/ui-window-recipe.md`
- DevCheats sections, runtime tuning parameters -> section 6 above + `Assets/Scripts/Dev/`
- Unity Editor automation (read console / find GOs / run tests / edit scripts via bridge) -> `docs/ai/unity-mcp.md`

Do not load all docs unless the task spans multiple areas.
Prefer the smallest relevant context.
