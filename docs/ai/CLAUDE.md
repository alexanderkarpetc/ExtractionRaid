# ExtractionRaid — Agent Contract

Read this before non-trivial work. Collaboration is in Ukrainian and concise.

## Goal and architecture

Unity 6 top-down extraction shooter. Reference game: **Escape from Duckov**.

```text
App → Session → Systems → Adapters → View / Presenter
```

- `App` is the composition root and the only singleton.
- `RaidSession` owns runtime state and deterministic system order.
- Gameplay rules live in stateless static systems.
- `RaidContext` carries read-only ports, configs and constants.
- Presenters translate state/events into Unity visuals and audio.

## Hard rules

1. Never add another singleton.
2. Systems never call `App.Instance` or keep hidden mutable static state.
3. State stores values and stable IDs, not Unity object references.
4. Unity-facing access goes through ports/adapters or view/presenter.
5. View/presenter contains no gameplay rules.
6. Unity value types are allowed in model code; `GameObject`, `Transform`, `MonoBehaviour`,
   `Rigidbody`, `Collider`, `Animator` and ScriptableObject refs are not.
7. New gameplay logic belongs in a system, not scattered across views.
8. Keep diffs small and local; add or update tests when logic changes.
9. Reload Domain is off. Reset static caches/events with
   `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

## Implementation workflow

1. Identify affected state and dependencies.
2. Put Unity dependencies behind a port and config/tuning behind `RaidContext`.
3. Change system logic and emit domain events for presentation.
4. Wire presenter/view only for rendering, bindings and callbacks.
5. Add focused tests and update the relevant living doc if its contract changed.
6. Show a file-level plan before editing.

When adding a field to raid-owned state, expose it in
`Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`. Persistent player/meta state is outside that
debugger.

## DevCheats

- Gameplay tunables live in section ScriptableObjects under `Assets/Scripts/Dev/Sections/`.
- Section assets live under `Assets/Resources/Configs/DevCheats/`.
- Systems do not read `DevCheats` directly. `RaidSession` copies values into
  `RaidContext.*Config`; tests provide configs explicitly.
- A new section requires its class, config reference/accessor, asset creation and editor UI wiring.
- Use `Raid → Dev Cheats — Create Section Assets` after section changes.

## Unity Editor access

Do not hand-edit or save `.unity`, `.prefab` or `.asset` files as text; make authored-content changes
through the Unity Editor/MCP bridge. Reading their serialized text for static analysis is allowed,
but it does not replace Editor or runtime validation. Verify the bridge is available before relying
on it. Play/stop, editor mutation, code execution and PlayMode test runs require explicit user
confirmation.

Without the bridge, edit source normally and use Unity Test Runner or batchmode. Never claim tests
passed unless they actually ran.

## Documentation

- [`tasks.md`](./tasks.md) is the **only** task/status/backlog tracker.
- [`release-scope.md`](./release-scope.md) defines the v1.0 product boundary.
- System docs contain only stable contracts, non-obvious invariants and operational gotchas.
- Gameplay values, formulas, curves, thresholds and catalogs belong in code/config, not docs.
- Do not mirror enums, registries, fields, methods, constants, file trees or test counts from code.
- Link instead of duplicating.

## Task routing

| Area | Read |
|---|---|
| Priorities / status | `tasks.md` |
| Release boundary | `release-scope.md` |
| Architecture / lifecycle / new systems | `architecture.md` |
| Tests and acceptance | `testing-and-workflow.md` |
| Weapons / builder / attachments / crosshair | `weapons.md` |
| Armor / penetration / bleeding | `combat.md` |
| Bot behavior | `bot-ai.md` |
| Inventory / loot / craft / quests | `inventory-and-items.md` |
| Progression | `progression.md` |
| Fog of War | `fog-of-war.md` |
| Interactable outline / lifecycle | `architecture.md` |
| UI Toolkit | `ui-styling.md` |
| Impact/armor FX authoring | `combat.md` |
