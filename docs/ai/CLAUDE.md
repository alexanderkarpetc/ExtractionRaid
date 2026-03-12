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

## 5) Domain boundaries

Allowed in model/state/logic:
- value types and math helpers from Unity

Forbidden in model/state/logic:
- direct scene object references
- gameplay logic inside MonoBehaviours
- hidden dependencies through globals or static fields

## 6) Standard implementation workflow

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

## 7) Definition of done

A change is done when:
- it compiles
- it follows this contract
- tests are added or updated when applicable
- no hidden dependencies were introduced
- the diff is minimal and focused
- there is no unrelated formatting churn
- regression risk was checked

## 8) Documentation sync

AI docs exist in two places that must stay in sync:
- `docs/ai/` — for Claude Code (`CLAUDE.md`, `architecture.md`, `entity-lifecycle.md`, `testing-and-workflow.md`, `crosshair.md`)
- `.cursor/rules/` — for Cursor (`architecture-contract.mdc`, `architecture-details.mdc`, `entity-lifecycle.mdc`, `testing-workflow.mdc`, `crosshair.mdc`)

When updating any AI doc, apply the same change to the corresponding Cursor doc.

## 9) Weapon State Machine (V1)

Player weapons use an enum-based FSM (`WeaponPhase` in `WeaponEntityState`).
Phases: `Ready`, `Firing`, `Cooldown`, `Equipping`, `Unequipping`.

Key files:
- `State/WeaponEntityState.cs` — `WeaponPhase` enum, `Phase`, `PhaseStartTime`, `EquipTime`, `UnequipTime`
- `State/PlayerEntityState.cs` — `PendingHotbarSlot` (swap intent written by WeaponEquipSystem)
- `Systems/WeaponStateMachineSystem.cs` — FSM orchestrator (runs after WeaponEquipSystem, before AimingSystem)
- `Systems/WeaponEquipSystem.cs` — writes `PendingHotbarSlot` only (no instant swap)
- `Systems/ShootingSystem.cs` — fires only when `Phase == Ready`, sets `Phase = Firing`

Bots do NOT use the FSM — they remain on `LastFireTime` cooldown in `BotCombatSystem`.

Tick order: `Movement → WeaponEquip → WeaponStateMachine → Aiming → Shooting → ...`

Events: `WeaponEquipStarted`, `WeaponUnequipStarted`, `WeaponEquipFinished` (for future animations).

## 11) Ammo & Reload

Weapons have magazine ammo and reserve ammo (from inventory backpack).

Key fields on `WeaponEntityState`:
- `AmmoType` — `"Ammo_Rifle"` | `"Ammo_Shotgun"` | `null` (infinite, used by bots)
- `MagazineSize`, `AmmoInMagazine` — current/max rounds in magazine
- `ReloadTime` — seconds for reload animation

FSM phase `Reloading` added to `WeaponPhase` enum.

Transition rules:
- Ready + attack + empty mag → DryFire event + auto-reload (if reserve > 0)
- Ready + R key → Reloading (if `CanReload`)
- Cooldown → Ready → Reloading (same tick, if R pressed)
- Reloading timer done → Ready + fill magazine from reserve
- Reloading + swap intent → Unequipping (interrupt)

`AmmoSystem` (stateless static system in `Systems/AmmoSystem.cs`):
- `CountReserve(inventory, ammoType)` — sums matching items in backpack
- `ConsumeAmmo(inventory, ammoType, amount)` — drains from backpack, nulls empty slots
- `CompleteReload(weapon, inventory)` — fills magazine from reserve
- `CanReload(weapon, inventory)` — has room AND has reserve

Ammo values:
| Weapon | AmmoType | MagSize | ReloadTime |
|--------|----------|---------|------------|
| Rifle | Ammo_Rifle | 30 | 2.0s |
| Shotgun | Ammo_Shotgun | 5 | 2.5s |

1 trigger pull = 1 ammo consumed (shotgun: 1 shell = 7 pellets).

Items are stackable: `ItemState.StackCount`, `ItemDefinition.MaxStackSize`.
Pickup merges into existing partial stacks first, then overflows to free slots.

## 12) Dual-Layer Aiming

Player aiming has two layers:
1. **Raw Aim** (`RawAimPoint`) — instant world position from mouse, no smoothing
2. **Weapon Aim** (`WeaponAimPoint`) — follows Raw Aim with per-weapon exponential smoothing + recoil

Key fields on `PlayerEntityState`:
- `RawAimPoint` — instant mouse world position (player intent)
- `WeaponAimPoint` — smoothed world position + recoil offset (weapon tracking)
- `AimDirection` — derived from WeaponAimPoint (normalized, used by ShootingSystem)
- `FacingDirection` — body rotation, follows raw aim (unchanged behavior)

Key fields on `WeaponEntityState`:
- `AimFollowSharpness` — exponential smoothing rate (higher = faster tracking)
- `RecoilKickForward` — world units forward displacement per shot (away from player)
- `RecoilKickSide` — world units max sideways displacement per shot (perpendicular scatter)
- `RecoilRecoverySpeed` — independent recoil decay rate
- `RecoilOffset` — runtime accumulated recoil displacement (Vector3)

| Weapon | AimFollowSharpness | RecoilKickForward | RecoilKickSide | RecoilRecoverySpeed |
|--------|--------------------|------------------|----------------|---------------------|
| Rifle | 10 | 2 | 2 | 6 |
| Shotgun | 5 | 6 | 6 | 6 |
| Unarmed | 30 (const) | — | — | — |

Smoothing method: position-based exponential (`Vector3.Lerp(current, target, 1 - exp(-sharpness * dt))`).

Recoil: forward kick + sideways scatter (displaces WeaponAimPoint away from player). Subtract-apply pattern in AimingSystem separates base tracking (AimFollowSharpness) from recoil decay (RecoilRecoverySpeed). See `docs/ai/crosshair.md` for details.

Key files: `Systems/AimingSystem.cs`, `Systems/ShootingSystem.cs`

## 10) Task routing (read only what is relevant)

Read extra docs depending on the task:
- Architecture changes / new systems -> `docs/ai/architecture.md`
- Spawn/despawn, entity binding, callbacks, presenter wiring -> `docs/ai/entity-lifecycle.md`
- Tests, feature implementation flow, launch flow -> `docs/ai/testing-and-workflow.md`
- Crosshair / cursor overlay, weapon state visualization -> `docs/ai/crosshair.md`

Do not load all docs unless the task spans multiple areas.
Prefer the smallest relevant context.
