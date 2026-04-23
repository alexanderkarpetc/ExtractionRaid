# Weapons

> **Source of truth для weapon customization, composition, і content:**
> [`docs/ai/weapon-builder/README.md`](./weapon-builder/README.md).
>
> Зброя збирається з Payload × Delivery (+ optional Exotic). Цей файл — про **runtime behaviour**
> (FSM, aiming, ADS, convergence), який однаково діє на всі зібрані зброї.

---

## Quick reference

**Player weapons = assembled from Weapon Builder.** Композиція живе в `ItemState.WeaponConfiguration`, runtime state — у `WeaponEntityState` з cached `Stats` блоком.

Key files:
- `State/WeaponEntityState.cs` — composition refs (PayloadCore, DeliveryCore, ExoticMod?), cached `WeaponStats`, runtime phase fields
- `State/PayloadCoreDefinition.cs` + subclasses (Ballistic/Laser/Rocket/Foam) — SO-driven payload stats per rarity tier
- `State/DeliveryCoreDefinition.cs` — SO-driven delivery stats + `FiringPattern` enum
- `Systems/WeaponSyncSystem.cs` — assembly pipeline (config → runtime state)
- `Systems/ShootingSystem.cs` — dispatch по `FiringPattern`, charge gate для Laser payload
- `Systems/WeaponStateMachineSystem.cs` — FSM: Ready ↔ Firing ↔ Cooldown ↔ Equipping ↔ Unequipping ↔ Reloading ↔ Charging

Бо всі stats приходять з SO assets, hardcoded таблиці з ваги у цьому файлі **немає** — значення редагуються через `Assets/Resources/WeaponBuilder/*` у Inspector. Див. [weapon-builder/README.md](./weapon-builder/README.md) для повного огляду.

---

## Weapon State Machine

`WeaponPhase` enum на `WeaponEntityState`:

| Phase | Trigger → next | Notes |
|-------|---------------|-------|
| `Ready` | AttackPressed → `Charging` (Laser) або `Firing` (other); ReloadPressed → `Reloading` | Idle, accepts input |
| `Charging` | ChargeTime elapsed → `Firing`; AttackJustReleased → `Ready` (cancel); PendingSwap → `Unequipping` | Tier 2: charge-up payloads (Laser) |
| `Firing` | Next tick → `Cooldown` | 1-tick marker, ShootingSystem spawned projectiles |
| `Cooldown` | FireInterval elapsed → `Ready` | Inter-shot gap |
| `Equipping` / `Unequipping` | EquipTime/UnequipTime elapsed → `Ready` / swap | Weapon draw/holster |
| `Reloading` | ReloadTime elapsed → `Ready` + fill mag from inventory | AmmoSystem.CompleteReload |

Tick order: `Movement → WeaponEquip → WeaponStateMachine → Aiming → Shooting → …`

Events emitted: `WeaponFired`, `WeaponEquipStarted/Finished`, `WeaponUnequipStarted`, `WeaponReloadStarted/Finished`, `WeaponDryFired`, `WeaponChargeStarted/Completed/Cancelled`, `WeaponAssemblyFailed`.

Bots do NOT use this FSM — вони на `LastFireTime` cooldown у `BotCombatSystem`.

---

## Ammo & Reload

Weapons have magazine ammo + reserve ammo (з player inventory backpack).

- `weapon.AmmoType` — identifier на payload ("Ammo_Rifle", "Ammo_EnergyCell", null для ботів)
- `weapon.Stats.MagazineSize`, `weapon.AmmoInMagazine` — max / current
- `weapon.Stats.ReloadTime` — тривалість reload animation

`AmmoSystem` (static):
- `CountReserve(inventory, ammoType)` — сума matching items у backpack
- `ConsumeAmmo` / `CompleteReload` / `CanReload`

1 trigger pull = 1 ammo consumed (навіть при Scatter з N pellets).

Ammo modifiers (Penetration, ArmorDamage, BleedChance) приходять з `ItemDefinition.Get(weapon.AmmoType)` і додаються до weapon base stats **у ShootingSystem на fire** (окремий канал, не частина cached `Stats`).

---

## Projectile Stat Composition

При fire у `ShootingSystem`:

```
totalPenetration = weapon.Stats.BasePenetration + ammoDef.Penetration
totalArmorDamage = weapon.Stats.BaseArmorDamage + ammoDef.ArmorDamage
totalBleedChance = weapon.Stats.BaseBleedChance + ammoDef.BleedChance
```

Damage та projectile speed додатково скальовуться `ShootingConfig.DamageMultiplier` / `ProjectileSpeedMultiplier` (DevCheats).

Результат лягає в `ProjectileEntityState` поля + emit `ProjectileSpawned`.

---

## Convergence & Parallax Correction

Projectile direction — blend двох напрямків у `ShootingSystem`:

1. **Parallax-corrected** — adjust for camera height so trail passes через crosshair на екрані
2. **Convergence** — від `input.ConvergencePoint` (де raycast hit 3D collider)
3. **Blend** — `Lerp(parallaxDir, convergenceDir, ConvergenceBlend)`

**AimUp:** якщо convergence hit character, projectile angles slightly up (`AimUpHeightRatio`) щоб інтерсектити upper body. Це вмикає headshot detection з top-down камери.

Spawn Y — forced до `cfg.ProjectileSpawnHeight` для зменшення parallax.

DevCheats (`ShootingConfig` section): `ParallaxCorrection`, `ConvergenceBlend`, `ConvergenceAimUp`, `AimUpHeightRatio`, `ProjectileSpawnHeight`.

---

## ADS (Aim Down Sights)

Continuous blend `player.AdsBlend` (0 = hip, 1 = ADS). Lerped each tick based on `input.AdsPressed` та `AdsTransitionTime`.

**Aiming** (`AimingSystem`):
- `AimFollowSharpness *= Lerp(1, AdsAimFollowMultiplier, AdsBlend)`
- Recoil decay `*= Lerp(1, AdsRecoilRecoveryMultiplier, AdsBlend)`

**Shooting** (`ShootingSystem`):
- Recoil kick `*= Lerp(1, AdsRecoilMultiplier, AdsBlend)` (default 0.6x)

**Movement** (`MovementSystem`):
- Move speed `*= Lerp(1, AdsMoveSpeedMultiplier, AdsBlend)` (default 0.7x)

**Crosshair** (`AimCursorOverlay`): gap / bloom lerp toward ADS-specific values; top crosshair line fades out during ADS.

DevCheats (ADS section): `AdsTransitionTime`, `AdsMoveSpeedMultiplier`, `AdsAimFollowMultiplier`, `AdsRecoilMultiplier`, `AdsRecoilRecoveryMultiplier`, `AdsBaseGap`, `AdsBloomExtraGap`.

---

## Dual-Layer Aiming

Player aiming has two layers:
1. **RawAimPoint** — instant mouse world position (player intent)
2. **WeaponAimPoint** — smoothed toward RawAim + recoil offset

Key fields:
- `PlayerEntityState`: `RawAimPoint`, `WeaponAimPoint`, `AimDirection` (derived), `FacingDirection`, `IsADS`, `AdsBlend`
- `WeaponEntityState.Stats`: `AimFollowSharpness`, `RecoilKickForward`, `RecoilKickSide`, `RecoilRecoverySpeed`
- `WeaponEntityState` runtime: `RecoilOffset`

Smoothing: position-based exponential `Lerp(current, target, 1 - exp(-sharpness * dt))`.

Recoil — forward kick + sideways scatter, through `RecoilOffset`. Subtract-apply pattern у AimingSystem separates tracking (AimFollowSharpness) from recoil decay (RecoilRecoverySpeed). Деталі у [`crosshair.md`](./crosshair.md).

DevCheats:
- `AimSplitEnabled` — коли false, weapon aim instant-follows mouse (sharpness=1000); recoil працює
- `AimFollowMultiplier` — scales sharpness
- `RecoilMultiplier`, `RecoilForwardMultiplier`, `RecoilSideMultiplier`, `RecoilRecoveryMultiplier` — kick/decay strength

Key files: `Systems/AimingSystem.cs`, `Systems/ShootingSystem.cs`.

---

## Related docs

- [**weapon-builder/README.md**](./weapon-builder/README.md) — Weapon Builder feature: composition, UI, content, plan
- [`crosshair.md`](./crosshair.md) — Crosshair rendering, recoil visuals, hit markers
- [`armor-research.md`](./armor-research.md) / `battle-design-status.md` — Armor penetration pipeline
