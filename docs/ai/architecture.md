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
- orchestrates system execution in a stable order

### Domain Logic (Systems)
- stateless static systems operate on explicit state and context
- gameplay rules live here
- systems accept inputs as arguments
- systems mutate state explicitly, typically via `ref`
- systems must not depend on hidden globals

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
Systems do not play VFX/SFX directly.
Systems emit domain-to-view intents through `IRaidEvents`.
`RaidEventBuffer` stores events as a single `List<RaidEvent>` where `RaidEvent` is a struct with `RaidEventType` enum + flat payload (Id, Position, Direction). Zero-alloc after warm-up.
Presenter iterates `buffer.All`, filters by `Type`, and performs Unity-side work.

## 3) Tick model

High-level loop:
1. gather input and external signals
2. run systems in a deterministic order
3. produce state changes and domain events
4. present state and events in Unity

The execution order is part of the design and must remain stable.
Typical conceptual groups:
- movement / locomotion
- weapon equip intent + weapon state machine
- aiming / shooting
- AI decisions / spawning
- combat resolution
- loot / interactions
- extraction / end conditions

Actual system tick order in `RaidSession.Tick()`:
```
RollSystem                 // dodge roll state machine
MovementSystem
WeaponEquipSystem          // writes PendingHotbarSlot (intent only)
WeaponStateMachineSystem   // FSM: Ready/Firing/Cooldown/Equipping/Unequipping/Reloading
AimingSystem               // dual-layer: RawAimPoint (instant) → WeaponAimPoint (smoothed) → AimDirection
GrenadeSystem              // grenade throw + trajectory
MedkitSystem               // healing consumable
StatusEffectSystem         // bleed, buffs, etc.
BandageSystem              // bandage healing
ShootingSystem             // fires only when Phase == Ready; ammo gate + dry fire + auto-reload
PlayerFOVSystem            // visibility cone queries
BotPerceptionSystem
BotBrainSystem
BotMovementSystem
BotCombatSystem            // bots use LastFireTime, no FSM
ProjectileSystem
GrenadeSystem.TickExplosions  // grenade detonation + area damage
DamageSystem               // consumes hit inbox, applies damage, emits death events
```

## 4) Entry points

Supported entry points:
- Menu
- direct Level/Raid start for development
- test scenarios

All entry points must go through:
- `GameLauncher.Launch(LaunchOptions)`

Do not rely on directly loading arbitrary scenes.

## 5) Debug tools

### Raid State Debugger (EditorWindow)
- `Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`
- Opens via **Window → Raid State Debugger**
- Readonly view of entire `RaidState` updated every frame during Play Mode
- Shows: Player (position, aim, health, weapon, hotbar), Bots (with blackboard + intents), Projectiles (with owner + age), Ground Items, Inventory, Health Map
- **Rule**: when adding new fields to any state class, add corresponding display in the debugger window

### Other debug tools
- `HotbarDebugOverlay` (`View/HotbarDebugOverlay.cs`) — IMGUI overlay showing 9 hotbar slots at bottom of game view (includes mag/reserve ammo display)
- `BotDebugLabel` (`View/BotDebugLabel.cs`) — 3D TextMesh above bots showing TypeId, AI status, HP, distance
- `InventoryUI` (`View/InventoryUI.cs`) — IMGUI inventory screen (Tab key) with drag/drop, context menu, stack counts

## 6) Shared terms

- **State**: mutable game-world data (values + IDs)
- **Context**: read-only dependencies (ports, adapters, config, events)
- **System**: stateless gameplay rule executor
- **Presenter/View**: Unity-only visualization layer
- **Entry Point**: launch mode routed through the launcher

## 7) Data Flow Reference

### Tick lifecycle

```
Update
  └─ App.Tick()
       └─ RaidSession.Tick()
            ├─ build RaidContext (readonly struct, passed as in)
            ├─ System_A.Tick(state, in context)   // e.g. MovementSystem
            ├─ System_B.Tick(state, in context)   // deterministic order
            └─ state.ElapsedTime += dt

LateUpdate
  └─ App.LateTick()
       ├─ Presenter.LateTick(session)              // consume events + sync views
       └─ RaidSession.ClearEvents()                // reset event buffer
```

Systems read adapters from context, write into state.
Presenters read state + events, write into Unity transforms.
Events are cleared after all presenters have consumed them.

### Object ownership

```
App (singleton, composition root)
  ├─ Adapters (ITimeAdapter, IInputAdapter, ...)   → passed into RaidSession
  ├─ Presenters (plain C# classes)                 → ticked + disposed by App
  ├─ Player (persistent profile)
  └─ RaidSession (created per raid)
       ├─ RaidEventBuffer
       ├─ RaidState        → entity states, EIds, elapsed time
       └─ LevelState       → level metadata
```

### Data direction

```
Input → Adapter → Context → System → State → Presenter → View
                                ↓
                          EventBuffer → Presenter (spawn/destroy intents)
```

No reverse links. State does not know about View.
Systems do not know about App. View does not write into State.
