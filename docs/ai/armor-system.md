# Armor System

## 1. Overview

The armor system provides ballistic protection via helmet and body armor slots.
It uses a hyperbolic penetration curve to determine damage reduction, parabolic
durability degradation to weaken armor over time, and helmet-only ricochet for
complete shot deflection.

Design goals:
- Ammo choice matters: high-pen ammo defeats armor, HP ammo bleeds unarmored targets.
- Armor degrades predictably: effective protection drops as durability falls below a threshold.
- Transparent feedback: players see cursor hit pulse (per-event-type), armor bars, and break VFX.
- Fully parameterized: all constants are tunable at runtime via DevCheats.

## 2. State

### ArmorState (`State/ArmorState.cs`)

Per-piece armor data:

| Field              | Type    | Description                                     |
|--------------------|---------|-------------------------------------------------|
| `ArmorPoints`      | `float` | Nominal armor rating (e.g. 30 for basic helmet) |
| `CurrentDurability`| `float` | Current durability points (decreases on hits)    |
| `MaxDurability`    | `float` | Maximum durability points                        |

Derived properties:
- `IsBroken` -- `CurrentDurability <= 0`
- `DurabilityPercent` -- `CurrentDurability / MaxDurability` (0..1)

Factory: `ArmorState.Create(armorPoints, maxDurability)` sets `CurrentDurability = MaxDurability`.

### ArmorSlotState (`State/ArmorState.cs`)

Holds two optional armor pieces per entity:

```csharp
public class ArmorSlotState
{
    public ArmorState Helmet;
    public ArmorState BodyArmor;
}
```

### ArmorMap in RaidState (`State/RaidState.cs`)

```csharp
public Dictionary<EId, ArmorSlotState> ArmorMap;
```

Keyed by entity ID. Entities without armor have no entry (removed by `EquipmentSystem`
when both slots are empty).

## 3. Penetration Formula

Damage reduction uses a **hyperbolic curve**:

```
diff = EffectiveArmor - Penetration
if diff <= 0:  DamageMultiplier = 1.0  (full damage, armor is outclassed)
else:          DamageMultiplier = K / (K + diff)
```

Where `K` = `ArmorConstants.DamageReductionK` (default **30**).

`DamageMultiplier` is the fraction of raw damage that passes through to HP.
`AbsorptionRatio = 1 - DamageMultiplier` is the fraction absorbed by armor.

### Examples (K = 30)

| EffectiveArmor | Penetration | diff | DamageMultiplier | Absorbed |
|----------------|-------------|------|------------------|----------|
| 40             | 40          | 0    | 1.00 (100%)      | 0%       |
| 40             | 20          | 20   | 0.60 (60%)       | 40%      |
| 40             | 10          | 30   | 0.50 (50%)       | 50%      |
| 40             | 0           | 40   | 0.43 (43%)       | 57%      |
| 30             | 0           | 30   | 0.50 (50%)       | 50%      |
| 30             | 50          | -20  | 1.00 (100%)      | 0%       |

Key property: when `diff = K`, damage is halved. Increasing penetration beyond armor
gives no extra bonus (multiplier caps at 1.0).

## 4. Durability

Armor effectiveness degrades when durability falls below a threshold.

### Effective Durability Multiplier

```
durPercent = CurrentDurability / MaxDurability

if durPercent >= Threshold:   multiplier = 1.0
elif durPercent <= 0:         multiplier = 0.0
else:                         multiplier = (durPercent / Threshold) ^ Power
```

Defaults: `Threshold = 0.7`, `Power = 2.0` (parabolic curve).

### Effective Armor Points

```
EffectiveArmor = ArmorPoints * EffectiveDurabilityMultiplier(durPercent)
```

Above 70% durability, armor is at full strength. Below 70%, protection drops
parabolically. At 0% durability, the armor is broken (`IsBroken = true`) and
provides no protection.

### Examples (ArmorPoints = 40, Threshold = 0.7, Power = 2)

| Durability % | Multiplier | Effective Armor |
|--------------|------------|-----------------|
| 100%         | 1.00       | 40.0            |
| 70%          | 1.00       | 40.0            |
| 50%          | 0.51       | 20.4            |
| 30%          | 0.18       | 7.3             |
| 10%          | 0.02       | 0.8             |
| 0%           | 0.00       | 0.0 (broken)    |

## 5. Damage Pipeline

Full flow from hit detection to HP reduction, implemented in `DamageSystem.Tick`:

1. **HitSignal received** -- contains `Damage`, `Penetration`, `ArmorDamage`, `BleedChance`, `TargetedEntityId`.
2. **Skip checks** -- self-hit (own projectile), dead target, rolling (i-frames), god mode.
3. **Headshot detection** -- `isHeadshot = (TargetedEntityId == TargetId && TargetedEntityId != 0)`.
4. **Headshot multiplier** -- `damage *= projectile.HeadshotDamageMultiplier` (2x rifle, 2.5x pistol, 1.5x shotgun).
5. **Ricochet check** (helmet only, see section 6) -- if ricochet, 0 HP damage, full durability damage, skip to step 10.
6. **Armor lookup** -- `ArmorSystem.GetArmorForHit(slots, isHeadshot)` returns helmet for headshots, body armor otherwise.
7. **ArmorSystem.Calculate** -- computes `DamageResult { HpDamage, ArmorDurDamage, AbsorptionRatio, ArmorHit }`.
8. **Apply durability damage** -- `ArmorSystem.ApplyDurabilityDamage(armor, durDamage)`. If armor breaks, emit `ArmorBroken` event.
9. **Apply HP damage** -- `health.CurrentHp -= finalDamage`. If dead, emit `EntityDied`.
10. **Bleed roll** -- per-hit random roll against `BleedChance` (ignores armor). On success, apply `Bleeding` status effect.
11. **Feedback events** -- `HitConfirmed` (for crosshair markers), `DamageNumberSpawned`, `ProjectileHit`.

## 6. Ricochet

Ricochet is **helmet only**. When a headshot hits:

```
if helmet is null or broken:    no ricochet
if Penetration >= EffectiveArmor:  no ricochet  (pen outclasses armor)
else:  ricochet if randomRoll < RicochetChance
```

Default `RicochetChance = 0.40` (40%).

On ricochet:
- **0 HP damage** -- the shot is fully deflected.
- **Full durability damage** -- `CalcArmorDurabilityDamage(armorDmg, absorptionRatio=1.0)` = `armorDmg * 2`.
- **ArmorBroken event** if durability reaches 0.
- **ProjectileRicochet event** -- triggers spark VFX at hit point.
- **HitConfirmed** with `isRicochet=true` -- blue spark crosshair marker.
- **Projectile destroyed** -- removed from state, skip all HP/bleed logic.

The random provider is injectable (`System.Func<float>`) for deterministic tests.

## 7. Armor Durability Damage

Each projectile carries a base `ArmorDamage` stat. Actual durability damage scales
with how much the armor absorbed:

```
ArmorDurDamage = BaseArmorDamage * (1 + AbsorptionRatio)
```

- If armor absorbs 0% (pen >= armor): `durDmg = armorDmg * 1.0` (minimum).
- If armor absorbs 50%: `durDmg = armorDmg * 1.5`.
- If armor absorbs 100% (ricochet): `durDmg = armorDmg * 2.0` (maximum).

This means armor that blocks more also wears out faster.

## 8. Equipment Sync

`EquipmentSystem` (`Systems/EquipmentSystem.cs`) bridges inventory items and runtime armor state.

### SyncArmorFromInventory(state, entityId, inventory)

Reads `inventory.HelmetSlot` and `inventory.BodyArmorSlot`. For each:
- If item is null or has no `ArmorPoints` in its `ItemDefinition`, armor is null.
- If item has custom durability (`HasCustomDurability`), uses item's stored durability values.
- Otherwise, creates fresh armor via `ArmorState.Create(def.ArmorPoints, def.MaxDurability)`.

Updates `state.ArmorMap[entityId]`. Removes the entry if both slots are empty.

### WriteBackDurability(state, entityId, inventory)

Copies `ArmorMap` durability back to the inventory `ItemState`:

```csharp
inventory.HelmetSlot.CurrentDurability = slots.Helmet.CurrentDurability;
inventory.HelmetSlot.MaxDurability = slots.Helmet.MaxDurability;
```

**Must be called before `SyncArmorFromInventory`** to preserve combat damage when
re-syncing (e.g. after equipment swap).

## 9. Ammo Composition

Projectile penetration stats are composed from weapon base stats + ammo item stats.

### Weapon Base Stats (`WeaponEntityState`)

| Weapon  | BasePenetration | BaseArmorDamage | BaseBleedChance |
|---------|-----------------|-----------------|-----------------|
| Rifle   | 20              | 5               | 0               |
| Shotgun | 10              | 4               | 0               |
| Pistol  | 15              | 6               | 0               |

### Ammo Stats (`ItemDefinition`)

| Ammo             | Penetration | ArmorDamage | BleedChance |
|------------------|-------------|-------------|-------------|
| Ammo_Rifle       | 10          | 5           | 0           |
| Ammo_Shotgun     | 8           | 4           | 0           |
| Ammo_Pistol      | 12          | 6           | 0           |
| Ammo_Rifle_AP    | 35          | 8           | 0           |
| Ammo_Pistol_AP   | 30          | 7           | 0           |
| Ammo_Rifle_HP    | 0           | 0           | 0.30        |
| Ammo_Shotgun_HP  | 0           | 0           | 0.08/pellet |
| Ammo_Pistol_HP   | 0           | 0           | 0.25        |

### Composition into ProjectileEntityState

The shooting system combines weapon base + ammo stats into each projectile's
`Penetration`, `ArmorDamage`, and `BleedChance` fields. These flow into `HitSignal`
on collision and are consumed by `DamageSystem`.

AP ammo: high penetration, defeats armor effectively.
HP (Hollow Point) ammo: zero penetration, zero armor damage, high bleed chance --
devastating against unarmored targets, useless against armor.

## 10. Visual Feedback

### Hit Pulse on Crosshair (`CrosshairPresenter` v2 — see [`crosshair.md`](crosshair.md))

EFD-style 4-stub spread on the reticle, triggered by `HitConfirmed` event. Per-event-type profiles (`HitPulseProfile` snapshot at trigger) drive Color / Duration / InnerStart / InnerEnd / Length / Thickness / phase envelopes:
- **Normal**: white short pulse.
- **Kill**: red, larger / longer.
- **Headshot**: gold tint.
- **Ricochet**: blue, short flash.
- Priority: Ricochet > Kill > Headshot > Normal.

Note: absorption-driven scaling (size + color blend by `absorptionRatio`) was a legacy IMGUI-overlay feature; current v2 SDF hit pulse uses event-type profiles instead. `HitConfirmed` still carries `absorptionRatio` (packed in `CurrentHp`) for other consumers (damage numbers, blood VFX intensity).

### Damage Numbers

`DamageNumberSpawned` event includes `absorptionRatio` for visual scaling of
floating damage text.

### Armor Bar on World Health Bar (`View/WorldHealthBar.cs`)

A thin stripe above each entity's health bar, split into two halves:
- **Left half**: helmet durability fill (cyan, `Color(0.3, 0.7, 1.0)`).
- **Right half**: vest durability fill (same color).
- 1px divider at center.
- Hidden when no armor is equipped.

Updated via `WorldHealthBar.UpdateArmor(helmetDurPercent, vestDurPercent)`.

### Defender Armor HUD (`View/DefenderArmorHUD.cs`)

IMGUI overlay in the player's screen corner showing equipped armor status:
- Two stacked bars: **H** (helmet, top) and **V** (vest, bottom).
- Color zones: green (>=70%), yellow (40-70%), red pulsing (<40%).
- White flash on damage (0.15s duration).
- "BROKEN" text overlay on armor break (1s fade-out).
- Stats text: `"30pts 75%"` (armor points + durability percent).
- Controlled by `DevCheats.ArmorHUDEnabled`.

### Helmet Break VFX (`View/ArmorBreakHelper.cs`)

On `ArmorBroken` event for helmet:
- Helmet mesh is detached from skeleton (unparented).
- Rigidbody added: mass 0.5, upward + random impulse (force 4), random torque (8).
- Auto-destroyed after 3s.

### Armor Mesh Visuals

Armor items specify `ArmorPrefabId` in their `ItemDefinition`. Meshes are loaded
from `Resources/Prefabs/Armor/{ArmorPrefabId}` and attached to skeleton bones
(Helmet01 for helmets, Spine02 for body armor).

## 11. DevCheats

All armor parameters are tunable at runtime via `DevCheatsArmorSection`
(`Dev/Sections/DevCheatsArmorSection.cs`):

### Penetration Curve
| Parameter         | Default | Description                          |
|-------------------|---------|--------------------------------------|
| `DamageReductionK`| 30      | Hyperbolic curve K constant          |
| `PenetrationCap`  | 100     | Max penetration value (hard cap)     |
| `ArmorPointsCap`  | 100     | Max armor points value (hard cap)    |

### Durability Degradation
| Parameter                | Default | Description                              |
|--------------------------|---------|------------------------------------------|
| `DurabilityThreshold`    | 0.7     | Below this %, armor starts losing power  |
| `DurabilityParabolicPower`| 2.0    | Exponent for degradation curve           |

### Helmet Ricochet
| Parameter        | Default | Description                            |
|------------------|---------|----------------------------------------|
| `RicochetChance` | 0.4     | Probability of ricochet when pen < armor |

### Armor Damage
| Parameter       | Default | Description                       |
|-----------------|---------|-----------------------------------|
| `ArmorDamageCap`| 30      | Max durability damage per hit     |

### Armor HUD
| Parameter          | Default | Description                        |
|--------------------|---------|------------------------------------|
| `ArmorHUDEnabled`  | true    | Toggle defender HUD visibility     |
| `ArmorHUDMarginX`  | 16      | X offset from screen edge          |
| `ArmorHUDMarginY`  | 40      | Y offset from top (below stamina)  |
| `ArmorHUDBarWidth` | 220     | Width of durability bars           |
| `ArmorHUDBarHeight`| 30      | Height of durability bars          |

### Debug
| Parameter       | Default | Description                                    |
|-----------------|---------|------------------------------------------------|
| `ForceNoArmor`  | false   | Bypass all armor calculations (raw damage)     |
| `ForceMaxArmor` | false   | Ignore durability degradation (full ArmorPoints)|

## 12. Key Files

| File | Description |
|------|-------------|
| `Systems/ArmorSystem.cs` | Core formulas: penetration curve, durability multiplier, ricochet check, damage calculation |
| `Systems/DamageSystem.cs` | Full damage pipeline: ricochet, armor reduction, HP damage, bleed roll, events |
| `Systems/EquipmentSystem.cs` | Inventory-to-ArmorMap sync, durability write-back |
| `State/ArmorState.cs` | `ArmorState` + `ArmorSlotState` classes |
| `State/HitSignal.cs` | Per-hit data: damage, penetration, armorDamage, bleedChance |
| `State/ProjectileEntityState.cs` | Projectile with pen/armorDmg/bleedChance fields |
| `State/WeaponEntityState.cs` | Weapon base stats: BasePenetration, BaseArmorDamage |
| `State/ItemDefinition.cs` | Item registry: armor items (points, durability), ammo stats (pen, bleed) |
| `State/RaidState.cs` | `ArmorMap: Dictionary<EId, ArmorSlotState>` |
| `View/ArmorBreakHelper.cs` | Helmet fly-off physics on break |
| `View/DefenderArmorHUD.cs` | IMGUI player armor status bars |
| `View/WorldHealthBar.cs` | Armor bar stripe above entity health bars |
| `Dev/Sections/DevCheatsArmorSection.cs` | All runtime-tunable armor parameters |
| `Constants/ArmorConstants.cs` | Compile-time default constants |

## 13. Constants

All values from `Constants/ArmorConstants.cs`:

| Constant                  | Value | Usage                                          |
|---------------------------|-------|------------------------------------------------|
| `DamageReductionK`        | 30    | Hyperbolic pen curve: `K / (K + diff)`         |
| `PenetrationCap`          | 100   | Hard cap on penetration stat                   |
| `ArmorPointsCap`          | 100   | Hard cap on armor points stat                  |
| `DurabilityThreshold`     | 0.7   | Durability % below which armor weakens         |
| `DurabilityParabolicPower`| 2.0   | Exponent for durability degradation curve       |
| `RicochetChance`          | 0.4   | Helmet ricochet probability (pen < armor only) |
| `ArmorDamageCap`          | 30    | Max durability damage per single hit           |

These are compile-time defaults. At runtime, `DevCheats` overrides are used
(`DevCheats.ArmorK`, `DevCheats.ArmorRicochetChance`, etc.).
