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
- attach IMGUI overlays and presenters

`App.Instance` is allowed here.

**Bootstrap flow** (`AppBootstrap.cs`, `[DefaultExecutionOrder(1000)]`):
1. `Awake`: guard against double-init, call `App.Initialize()`, `DontDestroyOnLoad`, attach MonoBehaviour overlays:
   - `HotbarDebugOverlay`, `InventoryUI`, `QuestUI`, `AimCursorOverlay`, `DamageNumberOverlay`
   - `StatusEffectOverlay`, `CraftingUI`, `StaminaBarOverlay`, `DefenderArmorHUD`
   - `DeployUI`, `NpcUI`
2. `Start`: build `LaunchOptions` from inspector `_launchMode` (Menu / Raid / TestScenario / Hideout), call `GameLauncher.Launch(options)`
3. `Update` → `App.Instance.Tick()`
4. `LateUpdate` → `App.Instance.LateTick()`
5. `OnApplicationQuit` / `OnDestroy` → `App.Shutdown()`

**App singleton** (`App.cs`):
- Owns adapters, presenters, `Player`, `RaidSession`, `QuestDatabase`
- `Initialize()`: creates App instance, loads save via `SaveManager`
- `StartRaid(levelId)`: creates `RaidSession`, calls `Start()`, sets camera
- `EnterHideout()`: starts raid with levelId `"hideout"`
- `DeployToRaid(sceneName, levelId)`: ends current raid, disposes presenters, loads scene, starts new raid
- `Tick()` → `RaidSession.Tick()`
- `LateTick()` → ticks all presenters in order, then `RaidSession.ClearEvents()`
- `Shutdown()`: disposes presenters, ends raid, disposes input adapter

**Presenters** (plain C# classes, ticked by App in `LateTick`):
- `DestructiblePresenter`
- `PlayerPresenter`
- `BotPresenter`
- `ProjectilePresenter`
- `GrenadePresenter`
- `GroundItemPresenter`
- `CorpsePresenter`

### Session Layer (Player / RaidSession)
- `Player`: persistent profile, meta progression, save/load entry point
- `RaidSession`: runtime session for a raid/level

`RaidSession`:
- owns `RaidState` and `LevelState`
- constructs `RaidContext` each tick
- orchestrates system execution in a stable order
- manages hit/collision inboxes
- handles interaction dispatch (loot, craft, deploy, NPC)
- processes damage alerts and death events (lootable creation, bot removal)

### Domain Logic (Systems)
- stateless static systems operate on explicit state and context
- gameplay rules live here
- systems accept inputs as arguments
- systems mutate state explicitly, typically via `ref`
- systems must not depend on hidden globals

**All systems** (in `Assets/Scripts/Systems/`):
| System | Purpose |
|---|---|
| `StaminaSystem` | sprint drain/regen, stamina gate |
| `RollSystem` | dodge roll state machine |
| `MovementSystem` | player locomotion, speed, facing |
| `WeaponSyncSystem` | syncs weapon state from inventory/equipment |
| `WeaponEquipSystem` | writes PendingHotbarSlot (intent only) |
| `WeaponStateMachineSystem` | FSM: Ready/Firing/Cooldown/Equipping/Unequipping/Reloading |
| `AimingSystem` | dual-layer: RawAimPoint (instant) → WeaponAimPoint (smoothed) → AimDirection |
| `QuickSlotSystem` | quick slot usage (grenades, meds from quick slots) |
| `GrenadeSystem` | grenade throw + trajectory; also `TickExplosions` for detonation + area damage |
| `MedkitSystem` | healing consumable usage |
| `StatusEffectSystem` | bleed L1/L2, buffs, per-tick damage/heal |
| `BandageSystem` | bandage healing, bleed downgrade |
| `ShootingSystem` | fires only when Phase == Ready; ammo gate + dry fire + auto-reload |
| `PlayerFOVSystem` | visibility cone queries |
| `BotPerceptionSystem` | bot perception of player/threats |
| `BotBrainSystem` | bot AI decision-making (behavior tree) |
| `BotMovementSystem` | bot pathfinding and locomotion |
| `BotCombatSystem` | bot shooting (uses LastFireTime, no FSM) |
| `ProjectileSystem` | projectile movement + lifetime |
| `DamageSystem` | consumes hit inbox, applies damage/armor, emits death events |
| `ArmorSystem` | hyperbolic pen curve, durability, absorption (called by DamageSystem) |
| `AmmoSystem` | ammo consumption helpers |
| `EquipmentSystem` | syncs InventoryState → ArmorMap, durability write-back |
| `PlayerSpawnSystem` | spawns player entity at spawn point |
| `BotSpawnSystem` | spawns bot entities (incl. shooting range targets) |
| `LootSystem` | lootable creation, container creation, nearest interactable search |
| `InventorySystem` | pick up, drop, move items |
| `CraftingSystem` | crafting recipes |
| `QuestSystem` | quest progress tracking |
| `ShootingSystem` | (see above) |

### Adapters (Unity-facing ports)
All Unity subsystem access goes through interfaces passed via context:

| Interface | Implementation | Purpose |
|---|---|---|
| `ITimeAdapter` | `UnityTimeAdapter` | `DeltaTime`, `FixedDeltaTime`, `Time` |
| `IInputAdapter` | `UnityInputAdapter` | move, aim, hotbar, ADS, convergence, muzzle point |
| `INavMeshAdapter` | `UnityNavMeshAdapter` | `SamplePosition` for navmesh queries |
| `IPhysicsAdapter` | `UnityPhysicsAdapter` | `Linecast` for LOS/occlusion |
| `IGrenadePositionAdapter` | `GrenadePositionAdapter` | grenade world position by EId |
| `IRaidEvents` | `RaidEventBuffer` | domain event emission (31 event types) |

### View / Presenter
- translates state and domain events into transforms, animation, VFX, SFX, and UI
- must not make gameplay decisions

**View MonoBehaviours**:
- `PlayerView`, `BotView`, `ProjectileView`, `GrenadeView`, `GroundItemView`, `DestructibleView`
- `WeaponView` (Mecanim-driven fire/equip/unequip/reload/dryfire animations)
- `ArmorBreakHelper` (helmet fly-off VFX on armor break)
- `RaidCameraController` (camera follow + ADS zoom)
- `BotDebugLabel` (3D TextMesh above bots)

**IMGUI Overlays** (attached by AppBootstrap):
- `AimCursorOverlay` — crosshair lines + bloom + reload ring + hit/kill/headshot/ricochet markers
- `HotbarDebugOverlay` — 9 hotbar slots at bottom of game view (mag/reserve ammo)
- `InventoryUI` — inventory screen (Tab key) with drag/drop, context menu, stack counts, item tooltip
- `QuestUI` — quest log and tracking
- `DamageNumberOverlay` — floating damage numbers with trajectory modes
- `StatusEffectOverlay` — bleed/buff status icons
- `CraftingUI` — workbench crafting interface
- `StaminaBarOverlay` — stamina bar HUD
- `DefenderArmorHUD` — player armor status (helmet + body)
- `DeployUI` — deploy point interaction
- `NpcUI` — NPC dialogue/interaction
- `GrenadeTrajectoryOverlay` — grenade throw preview arc

**Fog of War** (`View/FogOfWar/`):
- `FogOfWarController`, `FogOfWarFeature` — URP render feature
- `FOVRaySweep` — visibility ray sweep
- `FOVMeshBuilder` — builds FOV mesh for rendering

## 2) Data flow primitives

### State
Primary state buckets:
- `PlayerProfileState` (persistent)
- `RaidState` (per run)
- `LevelState` (per loaded level/run)

State contains values and IDs only.
No Unity object references.

**RaidState fields** (all in `Assets/Scripts/State/RaidState.cs`):
```
float ElapsedTime
bool IsRunning
PlayerEntityState PlayerEntity
List<ProjectileEntityState> Projectiles
List<GrenadeEntityState> Grenades
Dictionary<EId, HealthState> HealthMap
List<GroundItemState> GroundItems
List<BotEntityState> Bots
List<LootableContainerState> Lootables
List<WorkbenchState> Workbenches
List<DeployPointState> DeployPoints
List<NpcState> Npcs
InventoryState Inventory
Dictionary<EId, List<StatusEffectInstance>> StatusEffects
Dictionary<EId, ArmorSlotState> ArmorMap
```

**Other state classes** (`Assets/Scripts/State/`):
- `EId` — entity identifier (int wrapper)
- `PlayerEntityState` — position, aim, ADS, roll, hands-busy, menus, weapon, hotbar, stamina
- `BotEntityState` — position, typeId, patrol, weapon + `BotBlackboard` (perception, intents)
- `ProjectileEntityState` — position, direction, speed, damage, penetration, owner
- `GrenadeEntityState` — position, velocity, fuse timer
- `HealthState` — current/max HP
- `GroundItemState` — position, defId, count
- `InventoryState` — hotbar + backpack + equipment slots
- `ItemState` — defId, count, durability
- `ItemDefinition` — static item data (via `ItemGroups`)
- `WeaponEntityState` — weapon FSM phase, mag, reserve, stats (factory: `CreateRifle/CreateShotgun/CreatePistol`)
- `ArmorState` — ArmorPoints, Durability per piece
- `ArmorSlotState` — Helmet + BodyArmor slots (references into ArmorState)
- `StatusEffectState` / `StatusEffectInstance` — bleed L1/L2, buffs, per-entity list
- `LootableContainerState` — lootable corpse/container with inventory grid
- `WorkbenchState` — crafting workbench position + id
- `DeployPointState` — deploy point for scene transitions
- `NpcState` — NPC position, npcId for dialogue
- `QuestProgressState` — quest completion tracking
- `LevelState` — level metadata (levelId)
- `HitSignal` — damageable hit data (damage, hitPoint, targetId, penetration, isHeadshot)
- `CollisionSignal` — wall/surface collision data
- `BTTrace` — behavior tree debug trace
- `InventorySlotRef` — slot reference (container + index)

### Context
`RaidContext` is a readonly struct passed as `in`.
Contents:
- `float DeltaTime`
- `IRaidEvents Events`
- `ITimeAdapter Time`
- `IInputAdapter Input`
- `INavMeshAdapter NavMesh`
- `IPhysicsAdapter Physics`
- `IGrenadePositionAdapter GrenadePositions`
- `AimConfig AimConfig` — aim split, follow multipliers, recoil recovery (sourced from DevCheats)
- `ShootingConfig ShootingConfig` — parallax, convergence, speed/damage multipliers, recoil, infinite ammo (sourced from DevCheats)

### Events
Systems do not play VFX/SFX directly.
Systems emit domain-to-view intents through `IRaidEvents`.
`RaidEventBuffer` stores events as a single `List<RaidEvent>` where `RaidEvent` is a struct with `RaidEventType` enum + flat payload (Id, Position, Direction, CurrentHp, MaxHp, Damage, StringPayload). Zero-alloc after warm-up.
Presenter iterates `buffer.All`, filters by `Type`, and performs Unity-side work.

**RaidEventType** (31 event types):
```
RaidStarted, RaidEnded, PlayerSpawned,
ProjectileSpawned, ProjectileDespawned, ProjectileHit,
EntityDamaged, EntityDied,
GroundItemSpawned, GroundItemDespawned,
BotSpawned, BotDespawned,
WeaponFired, WeaponEquipStarted, WeaponUnequipStarted, WeaponEquipFinished,
WeaponReloadStarted, WeaponReloadFinished, WeaponDryFired,
GrenadeSpawned, GrenadeExploded, GrenadeDespawned,
MedkitUseStarted, MedkitUseStopped,
HitConfirmed, StatusEffectApplied, StatusEffectRemoved,
LootableSpawned, LootableDespawned,
DamageNumber, ArmorBroken, ProjectileRicochet
```

## 3) Tick model

High-level loop:
1. gather input and external signals
2. run systems in a deterministic order
3. produce state changes and domain events
4. present state and events in Unity

The execution order is part of the design and must remain stable.

Actual system tick order in `RaidSession.Tick()`:
```
// ── Pre-movement ────────────────────────────────────
ADS state + blend                // inline in Tick(), before movement so speed is affected this frame
StaminaSystem                    // sprint drain/regen, stamina gate
RollSystem                       // dodge roll state machine
MovementSystem                   // player locomotion

// ── Weapon pipeline ─────────────────────────────────
WeaponSyncSystem                 // syncs weapon state from inventory/equipment changes
WeaponEquipSystem                // writes PendingHotbarSlot (intent only)
WeaponStateMachineSystem         // FSM: Ready/Firing/Cooldown/Equipping/Unequipping/Reloading
AimingSystem                     // dual-layer: RawAimPoint → WeaponAimPoint → AimDirection

// ── Consumables + status effects ────────────────────
QuickSlotSystem                  // quick slot usage (grenades, meds from quick slots)
GrenadeSystem                    // grenade throw + trajectory
MedkitSystem                     // healing consumable
StatusEffectSystem               // bleed L1/L2, buffs, etc.
BandageSystem                    // bandage healing

// ── Combat ──────────────────────────────────────────
ShootingSystem                   // fires only when Phase == Ready; ammo gate + dry fire + auto-reload

// ── AI ──────────────────────────────────────────────
PlayerFOVSystem                  // visibility cone queries
BotPerceptionSystem              // bot perception
BotBrainSystem                   // bot AI decisions (behavior tree)
BotMovementSystem                // bot pathfinding
BotCombatSystem                  // bots use LastFireTime, no FSM

// ── Resolution ──────────────────────────────────────
ProjectileSystem                 // projectile movement + lifetime
GrenadeSystem.TickExplosions     // grenade detonation + area damage
DamageSystem                     // consumes hit inbox, armor calc, applies damage, emits death events
_hitInbox.Clear()
ProcessCollisions                // wall/surface collision → projectile removal
_collisionInbox.Clear()
ProcessDamageAlerts              // sets bot blackboard WasDamaged flags
ProcessDeathEvents               // creates lootables, removes dead bots, ends raid on player death

// ── Interaction ─────────────────────────────────────
Interaction dispatch             // inline: PickUp → NPC / Deploy / Craft / Loot / GroundItem pickup
ElapsedTime += dt
```

## 4) Entry points

Supported entry points via `LaunchMode` enum:
- `Menu` — main menu (not yet implemented)
- `Raid` — direct level/raid start for development
- `TestScenario` — test scenarios (currently same as Raid)
- `Hideout` — persistent hideout with crafting, NPCs, deploy points

All entry points must go through:
- `GameLauncher.Launch(LaunchOptions)` (async UniTaskVoid)

`LaunchOptions` is a struct with `Mode` and `LevelId`.

Do not rely on directly loading arbitrary scenes.

## 5) Debug tools

### Raid State Debugger (EditorWindow)
- `Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`
- Opens via **Window → Raid State Debugger**
- Readonly view of entire `RaidState` updated every frame during Play Mode
- Shows: Player (position, aim, health, weapon, hotbar, stamina, ADS), Bots (with blackboard + intents), Projectiles (with owner + age), Ground Items, Inventory, Health Map, Status Effects, Armor Map, Workbenches, Deploy Points, NPCs, Lootables
- **Rule**: when adding new fields to any state class, add corresponding display in the debugger window

### Dev Cheats (EditorWindow + ScriptableObject)
- `Assets/Scripts/Editor/DevCheatsWindow.cs`
- Opens via **Raid → Dev Cheats**
- SO-based architecture: `DevCheats.cs` (static accessor) → `DevCheatsConfig.cs` (root SO) → section SOs
- Root asset at `Resources/Configs/DevCheatsConfig.asset`
- Section assets at `Assets/Resources/Configs/DevCheats/`
- **14 sections** (`Assets/Scripts/Dev/Sections/`):

| Section SO | Key parameters |
|---|---|
| `DevCheatsCheatsSection` | GodMode, InfiniteAmmo |
| `DevCheatsWeaponSection` | DamageMultiplier, ProjectileSpeedMultiplier, FireRateMultiplier |
| `DevCheatsRecoilSection` | NoRecoil, RecoilMultiplier, Forward/Side multipliers, Recovery |
| `DevCheatsAimSection` | AimSplitEnabled, AimFollowMultiplier |
| `DevCheatsPlayerSection` | MoveSpeedMultiplier |
| `DevCheatsFOVSection` | FOVEnabled, Near/Far radius, Angle, Occlusion, ForceShowAllBots |
| `DevCheatsFogSection` | FogOfWarEnabled, BlurRadius/Iterations, Intensity, Desaturation, Color, RT scale, RayStep, TemporalBlend |
| `DevCheatsCrosshairSection` | CrosshairEnabled, line/gap/dot params, colors, hit marker params, headshot params, armor/ricochet colors |
| `DevCheatsADSSection` | AdsTransitionTime, MoveSpeedMul, AimFollowMul, RecoilMul, ZoomFactor, VignetteIntensity, BaseGap, BloomGap, CursorInfluence |
| `DevCheatsHealthBarSection` | Bar dimensions, trail/flash/shake params, segment lines, colors |
| `DevCheatsParallaxSection` | ProjectileSpawnHeight, ParallaxCorrection, ConvergenceBlend/AimUp, ProjectileHitRadius |
| `DevCheatsDamageNumberSection` | Enabled, TrajectoryMode, Duration, FlySpeed, Gravity, PopOvershoot, FontSize, Colors |
| `DevCheatsStatusEffectsSection` | ForceBleedPlayer |
| `DevCheatsArmorSection` | DamageReductionK, DurabilityThreshold/Power, RicochetChance, ArmorDamageCap, PenetrationCap, ArmorPointsCap, ForceNoArmor/MaxArmor, HUD params |

### Bot BT Debugger (EditorWindow)
- `Assets/Scripts/Editor/BotBTDebuggerWindow.cs`
- Behavior tree visualization for bot AI debugging

### Raid Tools Menu
- `Assets/Scripts/Editor/RaidToolsMenu.cs`
- **Raid → Delete Save** — clears save file

### Quest Editor Tools
- `Assets/Scripts/Editor/Quests/QuestDefinitionEditor.cs` — custom inspector for quest definitions
- `Assets/Scripts/Editor/Quests/QuestGraphImporter.cs` — imports quest graph assets
- `Assets/Scripts/Editor/Quests/QuestPrerequisiteGraph.cs` — quest prerequisite visualization
- `Assets/Scripts/Editor/Quests/QuestNode.cs` — graph node for quest editor

### Other debug tools
- `HotbarDebugOverlay` (`View/HotbarDebugOverlay.cs`) — IMGUI overlay showing 9 hotbar slots at bottom of game view (includes mag/reserve ammo display)
- `BotDebugLabel` (`View/BotDebugLabel.cs`) — 3D TextMesh above bots showing TypeId, AI status, HP, distance
- `InventoryUI` (`View/InventoryUI.cs`) — IMGUI inventory screen (Tab key) with drag/drop, context menu, stack counts
- `GrenadeTrajectoryOverlay` (`View/GrenadeTrajectoryOverlay.cs`) — grenade throw preview arc in game view

## 6) Shared terms

- **State**: mutable game-world data (values + IDs)
- **Context**: read-only dependencies (ports, adapters, config, events)
- **System**: stateless gameplay rule executor
- **Presenter/View**: Unity-only visualization layer
- **Entry Point**: launch mode routed through the launcher
- **DevCheats**: SO-backed runtime tuning parameters accessible via static `DevCheats.X`
- **EId**: entity identifier (int wrapper), allocated by `RaidState.AllocateEId()`

## 7) Data Flow Reference

### Tick lifecycle

```
Update
  └─ App.Tick()
       └─ RaidSession.Tick()
            ├─ build RaidContext (readonly struct, passed as in)
            │    └─ populates AimConfig + ShootingConfig from DevCheats
            ├─ ADS state + blend (inline)
            ├─ StaminaSystem.Tick(state, in context)
            ├─ RollSystem.Tick(state, in context)
            ├─ MovementSystem.Tick(state, in context)
            ├─ WeaponSyncSystem.Tick(state, in context)
            ├─ ... (all systems in deterministic order, see section 3)
            ├─ DamageSystem.Tick(state, hitInbox, in context)
            ├─ ProcessCollisions / ProcessDamageAlerts / ProcessDeathEvents
            ├─ Interaction dispatch (inline)
            └─ state.ElapsedTime += dt

LateUpdate
  └─ App.LateTick()
       ├─ DestructiblePresenter.LateTick(session)
       ├─ PlayerPresenter.LateTick(session)
       ├─ BotPresenter.LateTick(session)
       ├─ ProjectilePresenter.LateTick(session)
       ├─ GrenadePresenter.LateTick(session)
       ├─ GroundItemPresenter.LateTick(session)
       ├─ CorpsePresenter.LateTick(session)
       └─ RaidSession.ClearEvents()
```

Systems read adapters from context, write into state.
Presenters read state + events, write into Unity transforms.
Events are cleared after all presenters have consumed them.

### Object ownership

```
App (singleton, composition root)
  ├─ Adapters
  │    ├─ UnityTimeAdapter        (ITimeAdapter)
  │    ├─ UnityInputAdapter       (IInputAdapter)
  │    ├─ UnityNavMeshAdapter     (INavMeshAdapter)
  │    ├─ UnityPhysicsAdapter     (IPhysicsAdapter)
  │    └─ GrenadePositionAdapter  (IGrenadePositionAdapter)
  ├─ Presenters (plain C# classes)
  │    ├─ DestructiblePresenter
  │    ├─ PlayerPresenter
  │    ├─ BotPresenter
  │    ├─ ProjectilePresenter
  │    ├─ GrenadePresenter
  │    ├─ GroundItemPresenter
  │    └─ CorpsePresenter
  ├─ Player (persistent profile + inventory)
  ├─ QuestDatabase (ScriptableObject, loaded from Resources)
  ├─ SaveManager (static, load/save PlayerProfileState)
  └─ RaidSession (created per raid)
       ├─ RaidEventBuffer (IRaidEvents)
       ├─ HitInbox (List<HitSignal>)
       ├─ CollisionInbox (List<CollisionSignal>)
       ├─ RaidState
       │    ├─ PlayerEntityState
       │    ├─ List<BotEntityState>
       │    ├─ List<ProjectileEntityState>
       │    ├─ List<GrenadeEntityState>
       │    ├─ Dictionary<EId, HealthState> HealthMap
       │    ├─ Dictionary<EId, List<StatusEffectInstance>> StatusEffects
       │    ├─ Dictionary<EId, ArmorSlotState> ArmorMap
       │    ├─ List<GroundItemState>
       │    ├─ List<LootableContainerState>
       │    ├─ List<WorkbenchState>
       │    ├─ List<DeployPointState>
       │    ├─ List<NpcState>
       │    └─ InventoryState
       └─ LevelState
```

### Data direction

```
Input → Adapter → Context → System → State → Presenter → View
                                ↓
                          EventBuffer → Presenter (spawn/destroy intents)
```

No reverse links. State does not know about View.
Systems do not know about App. View does not write into State.

### Scene spawn points
Spawn points are MonoBehaviours placed in scenes, read once during `RaidSession.Start()`:
- `PlayerSpawnPoint` — exactly 1 per scene
- `BotSpawnPoint` — enemy spawn with chance + patrol waypoints
- `LooseLootSpawnPoint` — ground item spawn with loot table
- `LootContainerSpawnPoint` — container spawn (MedContainer, AmmoBox, RandomLootBox)
- `WorkbenchSpawnPoint` — crafting workbench
- `DeploySpawnPoint` — deploy/extraction point
- `NpcSpawnPoint` — NPC with npcId

### Constants
Static gameplay data lives in `Assets/Scripts/Constants/`:
- `ArmorConstants` — armor piece definitions
- `BotConstants` — bot type configs (HP, speed, loot tables)
- `ContainerConstants` — container type configs
- `CraftConstants` — crafting recipes
- `DodgeConstants` — roll parameters
- `GrenadeConstants` — grenade stats
- `ItemGroups` — item definition registry
- `MedConstants` — medkit/bandage parameters
- `StaminaConstants` — stamina drain/regen rates
- `StatusEffectConstants` — bleed/buff parameters
