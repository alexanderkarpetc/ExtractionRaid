# Architecture Details

This file contains the detailed architectural model referenced by `CLAUDE.md`.
Use it for architecture-sensitive changes.

## 1) Layers

### Composition Root (Boot / App)
Responsibilities:
- read launch options
- initialize global services, configs, save/load, dev tools
- create player
- start and stop `RaidSession`
- handle scene transitions

`App.Instance` is allowed here.

### Session Layer (Player / RaidSession)
- `Player`: persistent profile, meta progression, save/load entry point
- `RaidSession`: runtime session for a raid/level

`RaidSession`:
- owns `RaidState` and `LevelState`
- constructs `RaidContext`
- orchestrates manager execution in a stable order

### Domain Logic (Managers)
- stateless static managers operate on explicit state and context
- gameplay rules live here
- managers accept inputs as arguments
- managers mutate state explicitly, typically via `ref`
- managers must not depend on hidden globals

### Adapters (Unity-facing ports)
All Unity subsystem access goes through interfaces passed via context, for example:
- navmesh queries
- physics queries
- time

### View / Presenter
- translates state and domain events into transforms, animation, VFX, SFX, and UI
- must not make gameplay decisions

## 2) Data flow primitives

### State
Primary state buckets:
- `PlayerProfileState` (persistent)
- `RaidState` (per run)
- `LevelState` (per loaded level/run)

State contains values and IDs only.
No Unity object references.

### Context
`RaidContext` is read-only and passed as `in`.
Typical contents:
- ports/adapters
- config references
- event sink/buffer
- constants (`dt`, masks, tuning)

### Events
Managers do not play VFX/SFX directly.
Managers emit domain-to-view intents through `IRaidEvents`.
Presenter consumes these intents and performs Unity-side work.

## 3) Tick model

High-level loop:
1. gather input and external signals
2. run managers in a deterministic order
3. produce state changes and domain events
4. present state and events in Unity

The execution order is part of the design and must remain stable.
Typical conceptual groups:
- movement / locomotion
- AI decisions / spawning
- combat resolution
- loot / interactions
- extraction / end conditions

## 4) Entry points

Supported entry points:
- Menu
- direct Level/Raid start for development
- test scenarios

All entry points must go through:
- `GameLauncher.Launch(LaunchOptions)`

Do not rely on directly loading arbitrary scenes.

## 5) Coding rules

### Must
- keep dependencies explicit via parameters
- isolate features by manager or manager sub-function
- prefer explicit IDs over object references
- preserve project conventions
- keep changes as minimal diffs

### Must not
- add new global singletons besides `App.Instance`
- call `App.Instance` from managers
- store Unity objects inside state
- hide dependencies in static fields

## 6) Shared terms

- **State**: mutable game-world data (values + IDs)
- **Context**: read-only dependencies (ports, adapters, config, events)
- **Manager**: stateless gameplay rule executor
- **Presenter/View**: Unity-only visualization layer
- **Entry Point**: launch mode routed through the launcher
