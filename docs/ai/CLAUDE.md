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

## 6) Documentation sync

AI docs exist in two places that must stay in sync:
- `docs/ai/` — for Claude Code (`CLAUDE.md`, `architecture.md`, `entity-lifecycle.md`, `testing-and-workflow.md`, `crosshair.md`, `weapons.md`, `fog-of-war.md`)
- `.cursor/rules/` — for Cursor (`architecture-contract.mdc`, `architecture-details.mdc`, `entity-lifecycle.mdc`, `testing-workflow.mdc`, `crosshair.mdc`, `weapons.mdc`, `fog-of-war.mdc`)

When updating any AI doc, apply the same change to the corresponding Cursor doc.

## 7) Task routing (read only what is relevant)

Read extra docs depending on the task:
- Architecture changes / new systems -> `docs/ai/architecture.md`
- Spawn/despawn, entity binding, callbacks, presenter wiring -> `docs/ai/entity-lifecycle.md`
- Tests, feature implementation flow, launch flow -> `docs/ai/testing-and-workflow.md`
- Weapons, ammo, reload, aiming, weapon stats -> `docs/ai/weapons.md`
- Crosshair / cursor overlay, weapon state visualization -> `docs/ai/crosshair.md`
- Fog of War, visibility, ray sweep, post-processing -> `docs/ai/fog-of-war.md`

Do not load all docs unless the task spans multiple areas.
Prefer the smallest relevant context.
