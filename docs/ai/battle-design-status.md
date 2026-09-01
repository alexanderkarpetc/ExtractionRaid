# Combat Rules

Stable armor, damage and bleeding contract. Work status lives only in [`tasks.md`](./tasks.md).

## Damage composition

Each shot composes payload weapon stats with ammo modifiers. The important axes are:

- health damage;
- penetration;
- armor durability damage;
- bleed chance;
- headshot multiplier.

Caps are enforced by `ArmorConstants`. Ammo modifies projectile damage axes at fire time; the
currently shipped ammo catalog is defined in code and is not mirrored here.

## Armor model

- Two equipment zones: helmet and body armor. There are no limb hitboxes or plates.
- Protection and durability are continuous values, not discrete armor classes.
- Body absorption follows a smooth penetration curve; it never creates a separate “blocked” state.
- A helmet may ricochet a low-penetration hit, producing no HP damage but consuming durability.
- Durability damage is flat points scaled by absorption; protection that stops more damage wears faster.
- At zero durability armor becomes ineffective and its equipped visual is hidden/detached; the item
  remains in state/inventory at zero durability. Helmets receive a physical fly-off effect.
- Looted armor preserves current/max durability through inventory and save flows.
- Armor weight derives from protection plus durability and reduces movement speed with a hard floor.

### Penetration and durability

Body absorption is a smooth function of `armor protection − projectile penetration`; over-penetration
does not increase damage above the unarmored result. Effective protection falls as durability moves
below its healthy threshold. Concrete curve constants are tunable and belong in config/code, not
this document.

Durability damage is based on projectile armor damage and absorption. The resolved hit records
health damage, absorbed ratio, ricochet and armor break so downstream feedback never recomputes the
formula.

## Bleeding

- Bleeding has two severity levels and is rolled independently from penetration.
- Reapplying L1 upgrades it to L2; L2 does not stack further.
- Bleed ticks bypass armor and emit ordinary damage feedback.
- One bandage removes one severity level after its cast completes.
- Concrete tuning belongs to the release balance task, not to this design contract.

## Player feedback

- Every hit communicates the result through the crosshair, damage number, VFX/SFX and armor HUD.
- Blood/spark intensity follows flesh/absorption ratios.
- Ricochet has a distinct marker, sound and physical deflection.
- Armor state uses readable healthy/damaged/critical presentation rather than exposing raw formulas.

## Architectural constraints

- `DamageSystem` and `ArmorSystem` are stateless.
- Tunables arrive through `RaidContext` configs; systems do not read `DevCheats` directly.
- Damage events carry values needed by presenters; view code does not recompute gameplay outcomes.
- Inventory durability is written back when equipment changes or the raid persists.
- Equipment sync rebuilds raid armor from item durability and writes runtime wear back before swap.
