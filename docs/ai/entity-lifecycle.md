# Entity Lifecycle and Binding Rules

Use this file when changing spawn, despawn, binding, callback routing, or presenter wiring.

## 1) Three representations of one runtime entity

Each runtime entity exists in three layers:

1. **State entity (source of truth)**
   - lives in `RaidState` / `LevelState`
   - identified by stable `EntityId`
   - stores values and IDs only
   - never stores Unity scene references

2. **Binding (registry / glue)**
   - maps `EntityId` to a Unity view instance
   - this is the only place where model and Unity objects are connected

3. **Unity view (MonoBehaviour / visual)**
   - prefab instance for rendering, animation, VFX/SFX, and Unity callbacks
   - stores its own `EntityId`
   - must not contain gameplay rules

## 2) Ownership

- `RaidSession` owns entity state and gameplay decisions.
- Presenter owns Unity view instances and the binding registry.
- Managers own gameplay rules.

## 3) Spawn contract

Spawn is always two-step:

### A) Domain spawn
- a manager creates the entity in state
- a manager generates an `EntityId`
- a manager emits a domain-to-view spawn intent

### B) View spawn
- presenter receives the spawn intent
- presenter instantiates the prefab
- presenter assigns `EntityId` to the view
- presenter registers the binding

Rules:
- managers must not instantiate prefabs
- presenter must not create entities in state

## 4) Update contract

### Simulation
- `RaidSession` runs managers in stable order
- managers read and mutate state
- managers may emit presentation intents

### Presentation
- presenter applies state-driven transforms and animation parameters
- presenter handles one-shot events such as VFX, SFX, spawn, despawn, UI feedback

Rules:
- MonoBehaviours must not run AI or combat rules in `Update` / `FixedUpdate`
- all decision-making lives in managers inside `RaidSession.Tick`

## 5) Despawn contract

Despawn is also two-step:

### A) Domain despawn
- a manager removes or marks the entity in state
- a manager emits a despawn intent with the `EntityId`

### B) View despawn
- presenter unbinds the entity
- presenter may play final VFX/SFX
- presenter destroys the GameObject immediately or after a short delay

Rules:
- managers must not destroy GameObjects
- presenter must not delete entities from state

## 6) Unity callbacks -> domain inbox

Unity callbacks (collisions, triggers, projectile hits, interaction volumes) must be routed as external signals.

Flow:
1. a view or sensor MonoBehaviour receives a Unity callback
2. it packages a domain event
3. it pushes the event into a `RaidSession`-owned inbox
4. managers consume inbox events on the next tick

Rules:
- Unity callbacks must not directly modify gameplay state
- views must not call managers directly

## 7) Allowed directions only

Allowed:
1. Unity -> Domain via inbox
2. Domain -> Unity via `IRaidEvents` and state-driven presentation

Forbidden:
- managers calling Unity APIs directly
- MonoBehaviours modifying domain state directly
- presenter making gameplay decisions

## 8) Identity and binding stability

- `EntityId` is the only stable gameplay identity
- Unity instance identity is never a domain key
- binding must support lookups both ways when needed
- logic must still work if an entity has no active view
- a stale view without a state entity must not produce gameplay effects

## 9) What views may do

Allowed:
- visual interpolation / smoothing
- animator parameter updates
- VFX / SFX playback
- purely visual hit reactions
- collecting Unity callbacks and forwarding them to the inbox

Not allowed:
- AI decisions
- damage formulas
- loot rules
- spawn / despawn decisions
- direct mutation of `RaidState` / `LevelState`
