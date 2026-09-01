# Architecture

## Boundaries

```text
Input → Adapter → RaidContext → System → State → Presenter → View
                                      ↘ events ↗
```

- **App:** composition, lifetime and entry points. Only global singleton.
- **Session:** owns player/profile and active raid state; builds context and ticks systems.
- **Systems:** stateless gameplay transformations over explicit state/context.
- **Adapters:** Unity-facing ports for input, physics, navigation and time.
- **View/Presenter:** owns Unity references, consumes state/events and routes callbacks.

Dependencies flow to the right. Systems cannot reach App, scenes, Resources or presenters.

## State and context

State is authoritative gameplay data: values, collections and stable IDs. It must be serializable or
reconstructable and must not own Unity objects. Cached derived values are allowed when their rebuild
path is explicit.

`RaidContext` is a per-tick dependency bundle. Ports and config structs enter through it so systems
stay deterministic and testable. Tunables are copied from DevCheats by `RaidSession`; systems never
read global tuning directly.

## Tick contract

The exact method list lives in `RaidSession.Tick`; documentation preserves only ordering invariants:

1. Copy input and build context.
2. Update player intent/state machines.
3. Run movement and interaction decisions.
4. Run bot perception, brain, movement and combat in that order.
5. Resolve projectiles, damage, status effects, extraction and timeout.
6. Synchronize inventory/equipment and publish events.
7. Presenters render the completed state in LateTick.

When order matters, encode it in `RaidSession` and cover the dependency with a test.

## Entity lifecycle

An entity may have three representations:

- domain state keyed by `EId`;
- a presenter-owned binding from `EId` to view;
- a Unity object containing visuals/collision callbacks.

Spawn is domain-first: allocate ID/state, then let a presenter create/bind the view. Despawn is also
domain-first: mark/remove state, then let the presenter destroy or pool the binding. Views never add
or remove authoritative entities directly.

Unity callbacks enqueue IDs and values for the next simulation step. They do not mutate gameplay
state or call systems recursively. Bindings must tolerate missing/despawned IDs and scene reloads.

## Events

Systems emit domain events when presentation must react to a transition such as fire, hit, death,
reload or status application. Events carry the resolved gameplay result; presenters must not
recalculate damage, armor or eligibility.

## Entry points

Main menu, hideout, direct raid and test scenes all converge on the same App/Session composition.
Scene-specific bootstrap code may select launch options, but cannot create alternate gameplay
rules or hidden state.

## Interactable outline

The outline renderer is presentation-only: targets register renderers with the static view registry,
and the URP feature renders mask/composite passes. Registration must tolerate disabled/destroyed
objects and reset across play sessions because Reload Domain is off. Eligibility must arrive as
resolved gameplay state; view code must not query `App.Instance` or decide proximity. Current gaps
are tracked only in [`tasks.md`](./tasks.md).

Use `MaterialPropertyBlock` for per-object feedback so shared materials remain shared. The
screen-space mask can merge overlapping objects, and transparent/custom shaders may need explicit
pass handling; add more mask complexity only for a concrete visual requirement.

## Review checklist

- Is the rule in a stateless system?
- Are dependencies explicit in context?
- Does state avoid Unity refs?
- Does presentation consume resolved state/events rather than decide outcomes?
- Are spawn/despawn and callback routing ID-based?
- Is significant order dependence tested?
