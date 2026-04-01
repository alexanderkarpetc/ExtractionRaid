# Weapons

## Weapon State Machine (V1)

Player weapons use an enum-based FSM (`WeaponPhase` in `WeaponEntityState`).
Phases: `Ready`, `Firing`, `Cooldown`, `Equipping`, `Unequipping`, `Reloading`.

Key files:
- `State/WeaponEntityState.cs` — `WeaponPhase` enum, `Phase`, `PhaseStartTime`, `EquipTime`, `UnequipTime`
- `State/PlayerEntityState.cs` — `PendingHotbarSlot` (swap intent written by WeaponEquipSystem)
- `Systems/WeaponStateMachineSystem.cs` — FSM orchestrator (runs after WeaponEquipSystem, before AimingSystem)
- `Systems/WeaponEquipSystem.cs` — writes `PendingHotbarSlot` only (no instant swap)
- `Systems/ShootingSystem.cs` — fires only when `Phase == Ready`, sets `Phase = Firing`

Bots do NOT use the FSM — they remain on `LastFireTime` cooldown in `BotCombatSystem`.

Tick order: `Movement → WeaponEquip → WeaponStateMachine → Aiming → Shooting → ...`

Events: `WeaponEquipStarted`, `WeaponUnequipStarted`, `WeaponEquipFinished` (for future animations).

## Ammo & Reload

Weapons have magazine ammo and reserve ammo (from inventory backpack).

Key fields on `WeaponEntityState`:
- `AmmoType` — `"Ammo_Rifle"` | `"Ammo_Shotgun"` | `"Ammo_Pistol"` | `null` (infinite, used by bots)
- `MagazineSize`, `AmmoInMagazine` — current/max rounds in magazine
- `ReloadTime` — seconds for reload animation

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

1 trigger pull = 1 ammo consumed (shotgun: 1 shell = 7 pellets).

Items are stackable: `ItemState.StackCount`, `ItemDefinition.MaxStackSize`.
Pickup merges into existing partial stacks first, then overflows to free slots.

## Ammo Types

Three calibers, three variants each (Standard, AP, HP). Defined in `ItemDefinition.BuildRegistry()`.

### Standard Ammo

Default ammo for each weapon. Moderate penetration and armor damage, no bleed chance.

| Item ID | Caliber | MaxStack | Penetration | ArmorDamage | BleedChance |
|---------|---------|----------|-------------|-------------|-------------|
| Ammo_Rifle | Rifle | 60 | 10 | 5 | 0 |
| Ammo_Shotgun | Shotgun | 20 | 8 | 4 | 0 |
| Ammo_Pistol | Pistol | 36 | 12 | 6 | 0 |

### AP Ammo (Armor-Piercing)

High penetration and armor damage, no bleed. Best against armored targets. Only Rifle and Pistol have AP variants.

| Item ID | Caliber | MaxStack | Penetration | ArmorDamage | BleedChance |
|---------|---------|----------|-------------|-------------|-------------|
| Ammo_Rifle_AP | Rifle | 60 | 35 | 8 | 0 |
| Ammo_Pistol_AP | Pistol | 36 | 30 | 7 | 0 |

### HP Ammo (Hollow Point)

Zero penetration and armor damage, high bleed chance. Ineffective against armor, deadly against unarmored.

| Item ID | Caliber | MaxStack | Penetration | ArmorDamage | BleedChance |
|---------|---------|----------|-------------|-------------|-------------|
| Ammo_Rifle_HP | Rifle | 60 | 0 | 0 | 0.30 |
| Ammo_Shotgun_HP | Shotgun | 20 | 0 | 0 | 0.08 (per pellet, 7 pellets ~ 44%/shot) |
| Ammo_Pistol_HP | Pistol | 36 | 0 | 0 | 0.25 |

## Projectile Stat Composition

When firing, `ShootingSystem` composes final projectile stats from weapon base + loaded ammo:

```
totalPenetration = weapon.BasePenetration + ammoDef.Penetration   // + weaponMod + charTree (future)
totalArmorDamage = weapon.BaseArmorDamage + ammoDef.ArmorDamage
totalBleedChance = weapon.BaseBleedChance + ammoDef.BleedChance
```

Ammo stats come from `ItemDefinition.Get(weapon.AmmoType)`. If no ammo type (bots), ammo contribution is zero.

These totals are written into `ProjectileEntityState` fields: `Penetration`, `ArmorDamage`, `BleedChance`.

Damage and speed are also modified by `ShootingConfig` multipliers: `DamageMultiplier`, `ProjectileSpeedMultiplier`.

## Convergence & Parallax Correction

Projectile direction is computed via a two-direction blend in `ShootingSystem`:

1. **Parallax-corrected direction** — adjusts for camera height so projectile trail passes through the crosshair on screen. Uses `spawnPos.y / camPos.y` ratio to lerp between ground aim point and camera position.

2. **Convergence direction** — toward the actual 3D point the crosshair ray hits (from `input.ConvergencePoint`). Provides accuracy against targets at varying distances.

3. **Blend** — `Vector3.Lerp(parallaxDir, convergenceDir, ConvergenceBlend)`. Blend=0 is pure visual, blend=1 is pure accuracy.

**AimUp**: When convergence hits a character, the projectile is angled slightly upward (`AimUpHeightRatio` lerp between collider bounds min/max Y) to intersect the upper body. This enables headshot detection on 3D characters from a top-down camera.

**TargetedEntityId**: Stored on the projectile when convergence ray hits a damageable character.

Projectile spawn height is forced to `cfg.ProjectileSpawnHeight` to reduce parallax.

DevCheats controls (`ShootingConfig`):
- `ParallaxCorrection` (bool) — enable/disable parallax correction
- `ConvergenceBlend` (float) — 0..1 blend between parallax and convergence
- `ConvergenceAimUp` (bool) — enable Y-angle toward upper body
- `AimUpHeightRatio` (float) — where on the target to aim (0=feet, 1=top)
- `ProjectileSpawnHeight` (float) — forced Y for projectile spawn

## ADS (Aim Down Sights)

ADS is a continuous blend controlled by `player.AdsBlend` (0 = hip, 1 = fully ADS). Lerped each tick based on `input.AdsPressed` and `AdsTransitionTime`.

**Effects on aiming** (in `AimingSystem`):
- `AimFollowSharpness *= Lerp(1, AdsAimFollowMultiplier, AdsBlend)` — faster tracking in ADS
- Recoil recovery: `decay *= Lerp(1, AdsRecoilRecoveryMultiplier, AdsBlend)` — faster recovery in ADS

**Effects on shooting** (in `ShootingSystem`):
- Recoil kick: `recoilMul *= Lerp(1, AdsRecoilMultiplier, AdsBlend)` — reduced recoil in ADS (default 0.6x)

**Effects on movement** (in `MovementSystem`):
- Move speed: `speed *= Lerp(1, AdsMoveSpeedMultiplier, AdsBlend)` — slower movement in ADS (default 0.7x)

**Effects on crosshair** (in `AimCursorOverlay`):
- Gap and bloom lerp toward ADS-specific values (`AdsBaseGap`, `AdsBloomExtraGap`)
- Top crosshair line fades out during ADS (`alpha *= 1 - adsBlend`)

DevCheats controls (ADS section):
- `AdsTransitionTime` (float, default 0.18s) — blend duration
- `AdsMoveSpeedMultiplier` (float, default 0.7) — movement penalty
- `AdsAimFollowMultiplier` (float, default 1.5) — aim tracking boost
- `AdsRecoilMultiplier` (float, default 0.6) — recoil reduction
- `AdsRecoilRecoveryMultiplier` (float, default 1.5) — recovery boost
- `AdsBaseGap` (float) — crosshair gap when fully ADS
- `AdsBloomExtraGap` (float) — bloom gap when fully ADS

## Dual-Layer Aiming

Player aiming has two layers:
1. **Raw Aim** (`RawAimPoint`) — instant world position from mouse, no smoothing
2. **Weapon Aim** (`WeaponAimPoint`) — follows Raw Aim with per-weapon exponential smoothing + recoil

Key fields on `PlayerEntityState`:
- `RawAimPoint` — instant mouse world position (player intent)
- `WeaponAimPoint` — smoothed world position + recoil offset (weapon tracking)
- `AimDirection` — derived from WeaponAimPoint (normalized, used by ShootingSystem)
- `FacingDirection` — body rotation, follows raw aim (unchanged behavior)
- `IsADS` — whether ADS button is held
- `AdsBlend` — continuous 0..1 interpolant, lerped each tick

Key fields on `WeaponEntityState`:
- `AimFollowSharpness` — exponential smoothing rate (higher = faster tracking)
- `RecoilKickForward` — world units forward displacement per shot (away from player)
- `RecoilKickSide` — world units max sideways displacement per shot (perpendicular scatter)
- `RecoilRecoverySpeed` — independent recoil decay rate
- `RecoilOffset` — runtime accumulated recoil displacement (Vector3)

Smoothing method: position-based exponential (`Vector3.Lerp(current, target, 1 - exp(-sharpness * dt))`).

Recoil: forward kick + sideways scatter. Both go through `RecoilOffset` (not directly into `WeaponAimPoint`). Subtract-apply pattern in AimingSystem separates base tracking (AimFollowSharpness) from recoil decay (RecoilRecoverySpeed). See `docs/ai/crosshair.md` for details.

DevCheats controls:
- `AimSplitEnabled` (bool) — when false, weapon aim follows mouse instantly (sharpness=1000), recoil still works
- `AimFollowMultiplier` (float) — scales `AimFollowSharpness` when aim split is enabled
- `RecoilMultiplier`, `RecoilForwardMultiplier`, `RecoilSideMultiplier` — kick strength
- `RecoilRecoveryMultiplier` — decay speed

Key files: `Systems/AimingSystem.cs`, `Systems/ShootingSystem.cs`

## Weapon Stats

### Combat Stats

| Weapon | PrefabId | FireInterval | Damage | HeadshotMul | ProjPerShot | SpreadAngle | ProjSpeed | ProjLifetime |
|--------|----------|-------------|--------|-------------|-------------|-------------|-----------|-------------|
| Rifle | Weapon_Rifle | 0.2 | 10 | 2.0x | 1 | 0 | 20 | 3.0 |
| Shotgun | Weapon_Shotgun | 0.6 | 8 | 1.5x | 7 | 30 | 30 | 2.0 |
| Pistol | Weapon_Pistol | 0.4 | 15 | 2.5x | 1 | 0 | 25 | 2.5 |

### Armor Penetration Stats (weapon base values)

| Weapon | BasePenetration | BaseArmorDamage | BaseBleedChance |
|--------|-----------------|-----------------|-----------------|
| Rifle | 20 | 5 | 0 |
| Shotgun | 10 | 4 | 0 |
| Pistol | 15 | 6 | 0 |

### Ammo & Equip

| Weapon | AmmoType | MagSize | ReloadTime | EquipTime | UnequipTime |
|--------|----------|---------|------------|-----------|-------------|
| Rifle | Ammo_Rifle | 30 | 2.0s | 0.3s | 0.2s |
| Shotgun | Ammo_Shotgun | 5 | 2.5s | 0.4s | 0.25s |
| Pistol | Ammo_Pistol | 12 | 1.5s | 0.2s | 0.15s |

### Aiming & Recoil

| Weapon | AimFollowSharpness | ConeHalfAngle | BodyRotationSpeed | RecoilKickForward | RecoilKickSide | RecoilRecoverySpeed |
|--------|--------------------|---------------|--------------------|--------------------|----------------|---------------------|
| Rifle | 10 | 45 | 270 | 2 | 1.5 | 2 |
| Shotgun | 5 | 20 | 180 | 3 | 6 | 3 |
| Pistol | 15 | 35 | 300 | 1.5 | 1 | 4 |
| Unarmed | 30 (const) | 60 (const) | 360 (const) | — | — | — |

Factory methods: `WeaponEntityState.CreateRifle(id)`, `CreateShotgun(id)`, `CreatePistol(id)`.
