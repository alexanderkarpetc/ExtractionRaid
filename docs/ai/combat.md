# Combat Rules

Stable armor, damage and bleeding contract. Work status lives only in [`tasks.md`](./tasks.md).

## Resolution ownership

Each shot composes weapon and ammo data, then `DamageSystem` and `ArmorSystem` produce the resolved
health, armor, bleed and ricochet result. Values, formulas, caps, thresholds and catalogs live in
code/config and are intentionally not mirrored here.

## Armor model

- Two equipment zones: helmet and body armor. There are no limb hitboxes or plates.
- Protection and durability are continuous values, not discrete armor classes.
- Body armor may absorb part of a hit; it does not create a separate “blocked” state.
- A helmet may ricochet a hit, producing no HP damage while still consuming durability.
- At zero durability armor becomes ineffective and its equipped visual is hidden/detached; the item
  remains in state/inventory at zero durability. Helmets receive a physical fly-off effect.
- Looted armor preserves current/max durability through inventory and save flows.
- Armor weight affects movement through the resolved gameplay configuration.
- The resolved hit records the values and flags presenters need, so feedback never recomputes combat.

## Bleeding

- Bleeding has two severity levels and is rolled independently from penetration.
- Reapplying L1 upgrades it to L2; L2 does not stack further.
- Bleed ticks bypass armor and emit ordinary damage feedback.
- One bandage removes one severity level after its cast completes.

## Player feedback

- Every hit communicates the result through the crosshair, damage number, VFX/SFX and armor HUD.
- `WorldHealthBar` reads authoritative current/max health. The dev-only
  `Tools & Cheats → Show numeric HP on health bars` toggle overlays `current / max` on world bars and
  is off by default.
- Blood/spark intensity follows flesh/absorption ratios.
- Ricochet has a distinct marker, sound and physical deflection.
- Armor state uses readable healthy/damaged/critical presentation rather than exposing raw formulas.

## Architectural constraints

- `DamageSystem` and `ArmorSystem` are stateless.
- Tunables arrive through `RaidContext` configs; systems do not read `DevCheats` directly.
- Damage events carry values needed by presenters; view code does not recompute gameplay outcomes.
- Inventory durability is written back when equipment changes or the raid persists.
- Equipment sync rebuilds raid armor from item durability and writes runtime wear back before swap.

## Impact and armor FX

FX consumes the resolved event; it must not infer penetration or damage from particle parameters.

| Result | Visual direction |
|---|---|
| Flesh hit | Directional blood with a restrained flash. |
| Armor absorption | Blend blood and sparks from the resolved result. |
| Helmet ricochet | Distinct deflection aligned to the reflected direction. |
| Armor break | One readable break event; a helmet may detach physically. |
| World surface | Material-appropriate impact without character blood. |

Keep effects readable from the top-down camera, pool-safe, and driven by supplied direction/normal.
Do not encode gameplay values or formulas in VFX graphs. Validate pooled replay, ragdoll transition,
simultaneous hits and the active post-processing profile.
