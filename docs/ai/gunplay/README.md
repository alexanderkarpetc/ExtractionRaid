# Combat Feel — Current System

This is the living summary of the shipped combat-feedback layer. Work status belongs only in
[`../tasks.md`](../tasks.md).

## Feedback pipeline

Gameplay systems emit `RaidEvent`s. Presenters consume them and own all Unity objects, materials,
audio and camera effects. Systems never trigger VFX/SFX directly.

```text
Damage / shooting / status systems
              ↓ RaidEvent
Projectile, hit, decal, audio, camera and HUD presenters
              ↓
Unity view objects and pooled effects
```

## Shipped layers

- Crosshair hit/kill/headshot/ricochet markers and weapon-state animation.
- Floating damage numbers with event-specific styling and short-window consolidation.
- Character rim flash, bullet decals, blood VFX and persistent world decals.
- Camera shake, hit pause, muzzle flash, casings, magazine drops and laser beam flash.
- Directional damage vignette, low-HP pulse, status row, stamina ring and ammo counter.
- Armor feedback, helmet fly-off, ragdoll death and weapon drop physics.
- Per-archetype recoil/charge behavior and modular weapon visuals.
- Spatial weapon/impact/reload/footstep/voice audio through `GameAudioPresenter`.

## Stable decisions

- Recoil remains gameplay-rooted; presenters visualize state rather than inventing a second model.
- Lasers remain projectiles: hitscan added little at the top-down camera scale.
- Helmet ricochet is the only full block; body armor uses continuous damage absorption.
- Heat-haze and learnable recoil-pattern experiments were reverted because they were unreadable or
  non-compensable in the current camera/aim model.
- UI and effects use pooled/programmatic assets where practical; tunables live in DevCheats/ViewCheats.

## Key references

- [`../weapons.md`](../weapons.md) — weapon FSM, aiming and projectile rules.
- [`../crosshair.md`](../crosshair.md) — cursor and hit-marker implementation.
- [`../battle-design-status.md`](../battle-design-status.md) — armor, penetration and bleeding rules.
- [`../bot-ai.md`](../bot-ai.md) — combat AI behavior.
- [`../weapon-builder/README.md`](../weapon-builder/README.md) — weapon composition.
