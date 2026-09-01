# Weapons

This file owns weapon composition, runtime firing, ammo, collision, aiming and crosshair contracts.

## Composition and attachments

A persistent weapon configuration stores stable payload, delivery, exotic and attachment IDs.
Inventory owns that configuration; the definition registry and stateless assembly systems resolve it
into cached runtime stats. `WeaponSyncSystem` rebuilds equipped state when the source item changes.

- Payload defines what is launched; delivery defines how it fires; an exotic adds an optional
  behavior hook. Rarity belongs to the cores.
- Attachment slots and compatibility are data-driven. Installation is authoritative in
  `AttachmentInstallSystem`; UI may preview but cannot decide compatibility or stat application.
- Mutations preserve the item instance and bump versions used by equipped sync and UI refresh.
- Add new definitions through the registry and explicit stateless behavior. Do not add string
  dispatch, view-owned weapon rules or a parallel stat-composition path.

## Runtime state machine

`WeaponEntityState` caches composed stats and mutable phase/ammo/recoil data. Phase transitions are
driven by `WeaponStateMachineSystem`; shooting requests do not skip equip, reload, charge, burst or
cooldown timing.

- Single/Scatter fire once per press.
- Auto may fire while held subject to cadence.
- Charge payloads capture charge on release; burst delivery owns its remaining-shot state until done.
- Empty magazines dry-fire and require reload; switching weapons preserves the source item state.

## Ammo and composition

The payload selects the accepted ammo ID. At fire time, composed weapon stats and the loaded ammo
definition produce projectile damage, penetration, armor damage and bleed chance. Magazine capacity
is a hard invariant on all spawn/sync/reload paths. Ammo availability tests ensure every offered
caliber is usable and restockable.

## Projectile collision

- Ignore the projectile owner in start-overlap and sweep paths.
- Probe initial overlap before sphere-casting so point-blank shots cannot tunnel from inside a body.
- Resolve the earliest valid hit and emit one authoritative hit result.
- Systems calculate damage; projectile views/adapters report geometry and render the outcome.

## Aiming

Player intent is `RawAimPoint`; `WeaponAimPoint` is the smoothed, recoil-affected point used by
shooting, crosshair and scope reveal. `AimDirection` is derived from weapon origin to that point.
This keeps projectile convergence and visuals on the same model.

ADS blends authored weapon handling/vision values. Low ergonomics may lag and overshoot through the
aim spring; views must not add a second smoothing layer.

## Barrel pullback

Weapon presentation may retract against nearby geometry/characters to avoid clipping. This is a
view-only pose adjustment and must not change projectile origin, range or damage rules.

## Crosshair and pointer

`CrosshairPresenter` renders resolved weapon phases, charge, recoil and hit events. Ballistic and
laser weapons may use different visuals, but the presenter never recalculates accuracy, hit type,
damage or range. Presentation animation state must reset across play/scene lifecycles.

`PointerOverUiTracker` is the shared authority for pointer-over-UI and OS cursor visibility. When UI
owns the pointer, gameplay aim/fire is gated and the cursor uses the same screen position. Windows
must not implement local cursor rules.

## Configuration and extension

Gameplay values enter through `RaidContext` configs and are not duplicated here. When extending the
weapon model, register definitions, author assets in Unity Editor, and cover composition, behavior,
ammo availability and persistence. Update this contract only when system boundaries change.
