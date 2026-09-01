# Weapons

Weapon identity/configuration is documented in [`weapon-builder/README.md`](./weapon-builder/README.md).
This file owns runtime firing, ammo, collision and aiming contracts.

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

## Tuning and extension

Gameplay tunables enter through `RaidContext` configs. Add a payload/delivery through the explicit
Weapon Builder extension workflow; do not add string switches or view-owned firing rules.
