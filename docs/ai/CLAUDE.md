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
7. If new fields were added to a **raid state** class (state owned by `RaidSession`/`RaidState`),
   update the Raid State Debugger (`Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`) to display
   them. Persistent profile/meta state on `Player` (e.g. `PlayerProfileState`,
   `PlayerProgressionState`) is out of scope — the debugger inspects the live raid only.
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

## 7) Unity Editor access

Do **not** read `.unity` / `.prefab` / `.asset` files as text to reconstruct scene/prefab state — they're large and brittle to parse. Inspect them in the Unity Editor.

If you have an **editor/MCP bridge available**, prefer it over file-scraping for: reading the Unity console, enumerating/CRUD-ing GameObjects & components, reading/writing ScriptableObjects (e.g. DevCheats), surgical C# edits with a recompile signal, and running EditMode/PlayMode tests. Otherwise edit the `.cs` directly and run tests via the Unity Test Runner (or `-batchmode -runTests`), or hand the change to the maintainer to run.

Bridge etiquette:
- Verify the bridge is actually up before relying on it (e.g. `lsof -nP -iTCP:<port> -sTCP:LISTEN`). If it's down, **stop and ask** — do not fall back to scraping `.unity` files as text.
- Modifying actions (play/stop, edits, code execution, deploys, PlayMode test runs) require **explicit user confirmation** before each call.
- When delegating to a subagent, spell out the bridge assumption + which tools it may call (subagents inherit tools, not this conversation's context).

> The maintainer's specific bridge (server, exact tool names, port) is personal and lives in their `~/.claude/` config, not in this repo.

## 8) Documentation

Project docs live в `docs/ai/`. Update the relevant doc when changing the system it describes — section 9 lists which doc maps to which area.

**This file is canonical.** The repo-root [`AGENTS.md`](../../AGENTS.md) is a thin pointer that directs `AGENTS.md`-reading agents (e.g. Codex) to read this file and follow §9. It carries only a few always-on rules as a safety net + does not duplicate §9, so it needs no upkeep when §9 changes.

**📍 Current direction / release plan (read early):**
- [`release-scope.md`](./release-scope.md) — full feature-map + gap analysis + 🔒 locked release decisions (what's shipped vs what's left).
- [`v1.0-roadmap.md`](./v1.0-roadmap.md) — the execution plan to v1.0 (milestones **M1–M4**, mirrored in the Task list). **This is the canonical "what do we build next" doc.**

## 9) Task routing (read only what is relevant)

Read extra docs depending on the task:
- **Release scope / current priorities / what's left to v1.0** -> `docs/ai/release-scope.md` + `docs/ai/v1.0-roadmap.md`
- Architecture changes / new systems -> `docs/ai/architecture.md`
- Spawn/despawn, entity binding, callbacks, presenter wiring -> `docs/ai/entity-lifecycle.md`
- Tests, feature implementation flow, launch flow -> `docs/ai/testing-and-workflow.md`
- Weapons, ammo, reload, aiming, weapon stats -> `docs/ai/weapons.md`
- Weapon Builder (composition, modules, builder UI) — **v1.0 headline; 3×4 archetypes + exotics in scope** -> `docs/ai/weapon-builder/`
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
- Character progression / skill tree (config asset, node data, allocation rules, permanent no-refund, effects seam) -> `docs/ai/progression.md`
- DevCheats sections, runtime tuning parameters -> section 6 above + `Assets/Scripts/Dev/`
- Unity Editor access (scenes/prefabs, tests, editor/MCP bridge etiquette) -> section 7 above

Do not load all docs unless the task spans multiple areas.
Prefer the smallest relevant context.
