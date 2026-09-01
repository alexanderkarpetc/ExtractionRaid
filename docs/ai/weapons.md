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
- `State/WeaponEntityState.cs` — composition refs (PayloadCore, DeliveryCore, ExoticMod?), cached `WeaponStats`, runtime phase fields, **`WeaponPrefab` + `PayloadPrefab` GameObject refs** (Tier 8 visualization)
- `State/PayloadCoreDefinition.cs` + subclasses (Ballistic/Laser/Rocket/Foam) — SO-driven payload stats per rarity tier; **`AttachmentPrefab` field** (Tier 8 Wave B)
- `State/DeliveryCoreDefinition.cs` — SO-driven delivery stats + `FiringPattern` enum; **`WeaponPrefab` field** (Tier 8 Wave A)
- `Systems/WeaponSyncSystem.cs` — assembly pipeline (config → runtime state); resolves visualization prefab refs
- `Systems/ShootingSystem.cs` — dispatch по `FiringPattern`, charge gate для Laser payload
- `Systems/WeaponStateMachineSystem.cs` — FSM: Ready ↔ Firing ↔ Cooldown ↔ Equipping ↔ Unequipping ↔ Reloading ↔ Charging
- `View/WeaponView.cs` — runtime visualization: PayloadMount socket, AttachPayload, procedural recoil kick on Fire (Tier 8 Waves B/D)
- `View/CharacterBody.cs` — `SwapWeaponModel(prefab, idForTracking, payloadPrefab)` instantiates delivery body + attaches payload mesh at socket

Бо всі stats приходять з SO assets, hardcoded таблиці з ваги у цьому файлі **немає** — значення редагуються через `Assets/Resources/WeaponBuilder/*` у Inspector. Див. [weapon-builder/README.md](./weapon-builder/README.md) для повного огляду.

---

## Weapon State Machine

`WeaponPhase` enum на `WeaponEntityState`:

| Phase | Trigger → next | Notes |
|-------|---------------|-------|
| `Ready` | AttackPressed → `Charging` (Laser) або `Firing` (other); ReloadPressed → `Reloading` | Idle, accepts input |
| `Charging` | AttackJustReleased → `Firing` / `Bursting` з charge-scaled stats; PendingSwap → `Unequipping` | Tier 2 + Tau-cannon mechanic (2026-05-06): hold-to-charge, fire-on-release |
| `Firing` | Next tick → `Cooldown` | 1-tick marker, ShootingSystem spawned projectiles |
| `Bursting` | LaserBurstInterval-paced auto-fire; BurstShotsRemaining=0 → `Cooldown`; PendingSwap → `Unequipping` | 2026-05-06 — laser+Auto delivery після release fires 1..6 shots scaled з chargeRatio |
| `Cooldown` | FireInterval elapsed → `Ready` | Inter-shot gap |
| `Equipping` / `Unequipping` | EquipTime/UnequipTime elapsed → `Ready` / swap | Weapon draw/holster |
| `Reloading` | ReloadTime elapsed → `Ready` + fill mag from inventory | AmmoSystem.CompleteReload |

**Laser charge-up mechanic** (2026-05-06, HL Tau cannon style):
- `AttackPressed` on laser → `Charging`, `ChargeStartTime = elapsed`
- Holding past `ChargeTime` keeps weapon у Charging at chargeRatio = 1.0 (no auto-fire — must release)
- `AttackJustReleased` → fires charged shot, `chargeRatio = clamp((elapsed - chargeStartTime) / chargeTime, 0, 1)`
- Damage scaling: `damage *= lerp(0.3, 1.0, chargeRatio)` (quick tap = 30%, full charge = 100%)
- VFX scaling: `BeamFlashPresenter` reads chargeRatio from `ProjectileSpawned.CurrentHp` → scales rim width / lifetime / jagged amplitude / color brightness

**Laser rifle burst** (laser + `Auto` delivery only):
- After release-fire, `burstCount = round(lerp(LaserBurstCountMin=1, LaserBurstCountMax=6, chargeRatio))`
- If burstCount > 1 → enter `Bursting` phase, `BurstShotsRemaining = burstCount - 1` (first shot fired immediately)
- `ShootingSystem.TickBurst` fires next shot every `LaserBurstInterval = 0.07s`, recomputes spawn/dir from current muzzle/aim (burst tracks player rotation)
- Each shot uses cached `BurstChargeRatio` for damage + VFX
- Out of ammo mid-burst → terminates early → Cooldown
- Other laser deliveries (SingleAction/Scatter) → single charged shot, no burst

Tick order: `Movement → WeaponEquip → WeaponStateMachine → Aiming → Shooting → …`

Events emitted: `WeaponFired`, `WeaponEquipStarted/Finished`, `WeaponUnequipStarted`, `WeaponReloadStarted/Finished`, `WeaponDryFired`, `WeaponChargeStarted/Completed/Cancelled`, `WeaponAssemblyFailed`.

Bots do NOT use this FSM — вони на `LastFireTime` cooldown у `BotCombatSystem`.

### Trigger semantics — semi-auto vs full-auto (2026-05-10)

Player input is gated per `FiringPattern` after the pattern dispatch у `ShootingSystem.Tick`:

| Pattern | Trigger | Behaviour |
|---|---|---|
| `Auto` (Rifle) | `AttackPressed` (held) | Full-auto — fires every `FireInterval` while LMB held |
| `Single` (Pistol) | `AttackJustPressed` (rising edge) | One click = one shot. Holding LMB through Cooldown does NOT re-fire — must release + press |
| `Scatter` (Shotgun) | `AttackJustPressed` (rising edge) | Same as Single — single blast per click |
| Any + Laser payload | `AttackJustReleased` (release) | Charge-release path; semi-auto gate bypassed via `releaseFire` flag |

Implementation: `IInputAdapter.AttackJustPressed` (rising-edge, `WasPressedThisFrame()`). In `ShootingSystem`:

```csharp
bool semiAuto = pattern == FiringPattern.Single || pattern == FiringPattern.Scatter;
if (semiAuto && !releaseFire && !input.AttackJustPressed) return;
```

Bots run the legacy `LastFireTime` cooldown path — not affected.

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

## Projectile Collision (hit detection)

`View/ProjectileView.SyncFromState` resolves hits per-frame in two stages:

1. **Start-overlap probe** (2026-05-09) — `Physics.OverlapSphereNonAlloc(oldPos, hitRadius)`. Unity's `SphereCast` returns no hit when the sphere already overlaps a collider at the start (`queriesHitBackfaces = false`). At point-blank distances `CharacterBody.LateUpdate` weapon pullback (see below) retracts the muzzle **inside** the target capsule — without the probe, the bullet would silently fly through. Probe registers the damageable immediately, hit point = `oldPos`, normal = `-direction`.
2. **SphereCast** along movement path (`oldPos → newPos`, `Physics.DefaultRaycastLayers`, `QueryTriggerInteraction.Collide`). Existing flight-collision path. Skip projectile-vs-projectile hits.

On hit: route via `ReportHit` → `IDamageableView` lookup → `RaidSession.ReportHit(HitSignal)` for characters, `RaidSession.ReportCollision(CollisionSignal)` for walls.

---

## Weapon Barrel Pullback (2026-05-09)

When the player's barrel pokes into a wall or another character, the WeaponPivot retracts smoothly along `-forward` so the muzzle (and therefore the projectile spawn point) stays clear of the obstacle. Without this, bullets would spawn behind cover or past an enemy at point-blank.

`View/CharacterBody.LateUpdate` (execution order 2000, runs after presenters rotate the pivot):

- **Cast:** `Physics.SphereCastNonAlloc` from body-XZ at `ProjectileSpawnHeight` toward `MuzzlePoint`, distance = body→muzzle + `WeaponLength`.
- **Mask:** `BotConstants.VisionBlockingMask | (1 << LayerUtils.Player) | (1 << LayerUtils.Bot)` — walls + live character shells. Ragdoll layer (9) excluded → corpses don't twitch the barrel.
- **Filter:** skip self root + skip any root with `RagdollController` (defense-in-depth corpse skip).
- **Retract math:** `WeaponPullbackMath.ComputeRetract(closestDist, pivotDistFromOrigin, weaponLength)` = `1 - distFromPivot/weaponLength`, clamped 0..1.
- **Apply:** `pivot.localPosition = Lerp(rest, rest + back × Amount × retract, lerpAlpha)`. Muzzle is a grandchild → retracts automatically.

Combined effect at point-blank into a character:
- Pivot retracts → muzzle sits inside enemy capsule → spawn position inside enemy.
- `ProjectileView` start-overlap probe (above) registers the hit on first sync → enemy reliably takes the shot.

DevCheats (`Weapon` section): `WeaponPullbackEnabled`, `WeaponLength`, `WeaponPullbackAmount`, `WeaponPullbackSpeed`, `WeaponPullbackRadius`, `BotPullbackCheckRateHz` (12 Hz for bots, every frame for player), `BotPullbackLodDistance` (skip pullback for far bots).

---

## Convergence & Parallax Correction

Projectile direction — blend двох напрямків у `ShootingSystem`:

1. **Parallax-corrected** — adjust for camera height so trail passes через crosshair на екрані
2. **Convergence** — від `input.ConvergencePoint` (де raycast hit 3D collider)
3. **Blend** — `Lerp(parallaxDir, convergenceDir, ConvergenceBlend)`

**AimUp:** якщо convergence hit character, projectile angles slightly up (`AimUpHeightRatio`) щоб інтерсектити upper body. Це вмикає headshot detection з top-down камери.

**Lock-on override (2026-05-10):** коли `convergence` hits a damageable (`IDamageableView` у hierarchy of hit collider), `blend` форсується до `1.0` — повний convergence, 3D-точний напрямок. Без overrides у певних top-down + side-angle конфігураціях змішаний blend ~0.3 промахував повз капсулу. Не-damageable кейси (ground, walls, empty space) лишаються на user-tuned `ConvergenceBlend` — візуальний "trail through cursor" feel preserved.

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

**Crosshair** (`CrosshairPresenter` v2 SDF — see [`crosshair.md`](crosshair.md)): gap / bloom lerp toward ADS-specific values; top crosshair arm hides binary cutoff during ADS.

DevCheats (ADS section): `AdsTransitionTime`, `AdsMoveSpeedMultiplier`, `AdsAimFollowMultiplier`, `AdsRecoilMultiplier`, `AdsRecoilRecoveryMultiplier`, `AdsBaseGap`, `AdsBloomExtraGap`.

---

## 3D Modular Visualization

Tier 8 (2026-04-30): kожна зброя — runtime composition двох візуальних модулів.

**Pipeline:**
```
WeaponConfiguration → WeaponSyncSystem.BuildWeaponForItem
  ↓ resolves DeliveryDefinition.WeaponPrefab + PayloadDefinition.AttachmentPrefab
WeaponEntityState { WeaponPrefab, PayloadPrefab }
  ↓ PlayerView.SyncFromState
CharacterBody.SwapWeaponModel(prefab, id, payloadPrefab)
  → Instantiate(delivery prefab) under WeaponPivot
  → WeaponView.AttachPayload(payloadPrefab) child of PayloadMount socket
```

**Symmetric composition.** Delivery prefab carries body silhouette + Animator + sockets:
- `DeliveryBody` (e.g. `SM_Wep_Mod_Body_05` mesh — receiver/stock/grip)
- `MuzzlePoint`, `RightHandGrip`, `PayloadMount` (empty Transforms)
- `WeaponView` component + Animator

Payload prefab carries барель/emitter mesh as wrapper + child mesh (e.g. `SM_Wep_Mod_Barrel_01` for Ballistic, `Mod_Barrel_15` для Laser). Spawned under `PayloadMount` on equip.

**Procedural recoil** (`WeaponView.TriggerRecoilKick`): на `PlayFire(duration)` body kicks back -Z on `_recoilKickDistance` (default 0.04m), eases out quad to rest over `max(0.06, duration*0.4)`. Per-prefab `[SerializeField]` distance — Inspector tuning. Replaces Mecanim animation paths що стали stale після symmetric pivot.

**Optional payload Animator** — payload prefab може мати власний Animator (e.g., Laser pulse, charged glow); not interfered by delivery's. Tier 9 territory.

**Adding new content (Tier 3+):**
- Create `<Name>.asset` SO + add у `CoreDefinitionDatabase`
- Run `Tools → Weapon Builder → Create Module Prefabs` → primitive prefab + wired refs
- Replace primitive content artist drop-in (no code/SO touch)
- Core-extension workflow: [weapon-builder/README.md](./weapon-builder/README.md)

Release polish for muzzle/socket alignment and stale Mecanim cleanup is tracked in
[`tasks.md`](./tasks.md). Procedural reload/equip/unequip motion is already shipped.

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

- [**weapon-builder/README.md**](./weapon-builder/README.md) — Weapon Builder composition, UI and core-extension contract
- [`crosshair.md`](./crosshair.md) — Crosshair rendering, recoil visuals, hit markers
- [`armor-research.md`](./armor-research.md) / `battle-design-status.md` — Armor penetration pipeline
