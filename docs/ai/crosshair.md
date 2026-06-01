# Crosshair System

In-game reticle rendered via **uGUI + custom SDF shader** in `View/CrosshairPresenter.cs`.
Replaced the legacy IMGUI `AimCursorOverlay` overlay in 2026-05-18 (Aim Cursor v2 epic, Stage 7).

For the design rationale, stage-by-stage shipping log, and cut/deferred items, see
[`docs/ai/gunplay/aim-cursor-v2.md`](gunplay/aim-cursor-v2.md).

## Architecture

| Layer | Tech | Notes |
|---|---|---|
| **Reticle** | One fullscreen `RawImage` on Screen-Space Overlay Canvas з SDF shader `CrosshairSDF` | All visuals procedural — no UI artist sprites |
| **Presenter** | `CrosshairPresenter` (plain class, NOT MonoBehaviour) | Lives in App; `LateTick(RaidSession)` after damage numbers, before event-buffer clear |
| **Pointer tracking** | `PointerOverUiTracker` MonoBehaviour on `AppBootstrap` GO | Update() polls `UiPanelHitTest.IsScreenPointOverUi`, broadcasts `App.IsPointerOverUi`, drives `Cursor.visible` |
| **Material** | `Resources/Vfx/Materials/Crosshair.mat` → explicit `new Material(...)` instance (uGUI `Image/RawImage.material` does NOT auto-instance — fixed 2026-05-31) | Per-instance writes — `MaterialPropertyBlock` doesn't work on UI elements. Without the explicit instance, per-frame `_CenterPx` writes mutated the shared `.mat` → git churn. |

Shader has a single fragment pass that branches on `_LaserMode` to pick rendering style.

## Phase-driven visual states

`WeaponEntityState.Phase` drives the reticle look. `CrosshairPresenter.UpdateReticle()` switches на phase + reads `weapon.Stats.FireInterval/ReloadTime/EquipTime/UnequipTime` + `weapon.PhaseStartTime` for progress.

| Phase | Visual | Notes |
|---|---|---|
| `Ready` (ammo > 0) | Baseline reticle (4-arm + dot для ballistic; segmented ring dim silhouette + dot для laser) | `NormalColor` |
| `Ready` (ammo = 0) | Same shape, warning red tint | `WarningColor` |
| `Firing` (1 frame) | Ballistic: gap expansion + bloom color. Laser: full ring + radial pulse | Captures `chargeRatio` for laser cooldown decay |
| `Cooldown` | Ballistic: bloom decays over `FireInterval`. Laser: ring chargeFill drains + pulse springs back | `SmoothStep(0..1)` curve |
| `Bursting` (laser+Auto burst) | Sustained Firing-style; each burst shot re-triggers pulse | `gap = adsGap + adsBloomExtra × 0.8` |
| `Reloading` | Lines/segments hidden, animated reload arc (`_RingFill = elapsed/ReloadTime`) | Same SDF shader, single composite path |
| `Charging` (laser) | Segmented ring fills clockwise from 12 o'clock as charge ratio grows | Shaped via `EvaluateChargeRatio` — see Charge curve |
| `Equipping` / `Unequipping` | Alpha fade in/out | Driven by `EquipTime`/`UnequipTime` |
| Unarmed | Dot only, dim alpha | `linesHidden = 1`, alpha 0.5 |

`IsRolling == true` applies `RollingAlpha` (default 0.3) multiplier on top of any state.

## Per-archetype rendering

Detected via `weapon.PayloadDefinition?.Archetype`. Single shader, branched paths.

### Ballistic — 4-arm + dot
- 4 arms (top/bottom/left/right) anchored at `_Gap` from center, length `_LineLength`, thickness `_LineThickness`.
- Center dot `_DotRadius`.
- ADS: top arm hides binary cutoff via `_adsAmount >= AdsTopArmFadeStart` (3-arm T-shape).
- Charge fill (only payload з charge mechanic): flame gradient bars overlay arms, growing from inner edge to outer tip as `chargeRatio` increases. Color gradient `ChargeColorCold → ChargeColorMid → ChargeColorHot` along arm length.

### Laser — segmented ring
- N slices (default 12, range 4..24), clockwise from 12 o'clock.
- Inner radius `LaserRingInnerRadius`, outer `LaserRingOuterRadius`, gap between slices `LaserSegmentGapDeg`.
- Empty silhouette always visible at `LaserInactiveAlpha` dim (face × inactive alpha, outline full strength — reads as anchor shape).
- Active segments at full alpha з gradient color cold→hot за their position in ring (`segIdx / N`).
- Implementation: analytical O(1) SDF per pixel — `segIdx = floor(ang / segWidth)` directly identifies pixel's slice (no fragment-shader loop).
- Reload hides ring same as it hides arms (`_LinesHidden` gates both paths).

## Firing animation (laser)

Stage 1.8 — `CrosshairPresenter` consumes `WeaponFired` events filtered by `e.StringPayload == "Laser"`:

- **chargeFill decay**: snapshot `chargeRatio` (packed in `e.Damage`) → drains `captured × (1 - cooldownT)` over `FireInterval`. Burst phase holds at captured.
- **Radial pulse**: `_firePulseT` ramps to 1 on shot, decays in lockstep. Inner radius shrinks / outer grows by `LaserFirePulseRadiusPx × _firePulseT`.
- Reset on Ready / Reloading / Charging.

Ballistic ignored (event filter), so flame-bars path doesn't accidentally light up over ballistic arms.

## Charge curve

`DevCheatsLaserSection.EvaluateChargeRatio(linearT)` shapes the raw `elapsed / chargeTime` progression:

- `ratio = Pow(clamp01(linearT), ChargeRatioPower)`
- Power = 1 → linear (legacy)
- Power > 1 → ease-in (slow start, fast finish — "build tension")
- Power < 1 → ease-out (fast 60-70%, slow trail to max)

Same math in `LaserConfig.EvaluateChargeRatio` in `RaidContext` → gameplay (damage/burst/spread) and cursor fill stay in lockstep. `WeaponChargeResolver.GetChargeTime(weapon, deliveryMult, overrideSeconds)` allows DevCheats baseline override (per-rarity payload values bypassed when override > 0).

## Hit pulse

EFD-style 4 diagonal stubs spreading outward + alpha fade. Replaces legacy flying X-markers (deleted in Stage 1.4).

- Driven by `HitConfirmed` event. Single-slot animation (latest hit restarts).
- Snapshot `HitPulseProfile` at trigger — animation continues з locked values even if user tweaks DevCheats mid-pulse.
- 3-phase envelope: burst (ease-out scale from 50% to full inner anchor) → hold (max alpha, slow drift) → decay (ease-out outward spread + alpha fade + thickness taper + optional rotation drift).
- 4 per-event-type profiles in `ViewCheatsCrosshairV2Section`: `NormalProfile` / `KillProfile` / `HeadshotProfile` / `RicochetProfile` (`HitPulseProfile` struct з Color / Duration / InnerStart / InnerEnd / Length / Thickness / BurstPhaseEnd / HoldPhaseEnd / RotationRad / ThicknessTaperStart / ThicknessTaperEnd).
- Priority: Ricochet > Kill > Headshot > Normal.
- Event packing note: `HitConfirmed` packs `Damage=isKill, Direction.x=isHeadshot, CurrentHp=absorptionRatio, MaxHp=isRicochet` (≠ EntityHit packing).

## Focus blur

Continuous edge softness driven by accuracy state. `_EdgeSoftness` shader param dynamically pushed по frame, applied to ALL SDF groups (main + charge + hit pulse).

```
recoilPressure = clamp01(weapon.RecoilOffset.magnitude / BlurRecoilSaturation)
adsContribution = (1 - player.AdsBlend) × BlurHipFireAmount
deficit = max(recoilPressure × BlurRecoilWeight, adsContribution)
blurPx = lerp(BlurMinPx, BlurMaxPx, deficit)
```

Master toggle `FocusBlurEnabled` (default ON). OFF falls back to static `EdgeSoftness` value (Stage 1 behavior — no regression).

## Overheat tremble

Perlin-noise jitter on cursor `_CenterPx` when `chargeFill ≥ ChargeOverheatThreshold` (default 0.85). Intensity scales linearly з overheat fraction. Default 2.5px @ 35Hz frequency.

## Recoil

Recoil is **gameplay-rooted**, not view-only. Single source of truth = `WeaponEntityState.RecoilOffset` (Vector3 world-space).

**Pipeline**:
- `ShootingSystem` adds impulse on fire: `aimDir × RecoilKickForward` (radial) + `right × Random(±RecoilKickSide)` (perpendicular).
- `AimingSystem` decays exponentially via per-weapon `RecoilRecoverySpeed` × ADS modifier (`AdsRecoilRecoveryMultiplier`).
- `player.WeaponAimPoint = cleanAim + RecoilOffset` — affects headshot detection + projectile direction.
- Cursor naturally follows via `cam.WorldToScreenPoint(player.WeaponAimPoint)`.

**Subtract-apply pattern** in AimingSystem prevents double-recovery:
```
cleanAim = WeaponAimPoint - RecoilOffset       // strip recoil
cleanAim = Lerp(cleanAim, mouse, smoothFactor)  // base tracking (AimFollowSharpness)
RecoilOffset = Lerp(RecoilOffset, zero, decay)  // recoil decay (RecoilRecoverySpeed)
WeaponAimPoint = cleanAim + RecoilOffset         // combine
```

**DevCheats multipliers** (all stack): `RecoilMultiplier` / `RecoilForwardMultiplier` / `RecoilSideMultiplier` / `RecoilRecoveryMultiplier` / `NoRecoil` toggle.

| Weapon | RecoilKickForward | RecoilKickSide | RecoilRecoverySpeed | Behavior |
|--------|------------------|----------------|---------------------|----------|
| Rifle | 2 | 1.5 | 2 | Moderate forward, slight scatter. Full-auto accumulates. |
| Shotgun | 3 | 6 | 3 | Heavy forward kick, noticeable scatter. Mostly recovers between shots. |
| Pistol | 1.5 | 1 | 4 | Light kick, minimal scatter. Fast recovery between semi-auto shots. |

## UI cursor swap (inventory mode)

OS cursor takes over when pointer is over а UI Toolkit panel (inventory window, sub-panel, builder palette, etc.). Game keeps running — player walks, fires when cursor's off UI.

| Component | Role |
|---|---|
| `PointerOverUiTracker.Update` | Polls `UiPanelHitTest.IsScreenPointOverUi(mouseScreen)` → broadcasts `App.SetPointerOverUi(bool)` → sets `Cursor.visible` |
| `UiPanelHitTest.IsScreenPointOverUi` | Central hit test, iterates all `UIDocument`s, asks `panel.Pick()` |
| `UnityInputAdapter.AttackPressed/JustPressed/JustReleased/AdsPressed` | All gated on `!IsPointerOverUi` — clicks on UI never fire weapon |
| `CrosshairPresenter.LateTick` | Hides v2 reticle canvas when `IsPointerOverUi == true` |
| `View/InventoryUI.cs` | Tab state machine — toggles `InventoryWindow.Open/Close()` + sets `player.IsInventoryOpen`. Mutually-exclusive з craft. Does NOT block gameplay input. |

Same-pixel swap is natural — OS cursor appears at the exact mouse position, no warp.

## Tunables

- **`ViewCheatsCrosshairV2Section`** (`Raid → Dev Cheats → View Cheats → Crosshair v2 (SDF)`) — geometry, colors, ADS thresholds, outline, charge gradient (cold/mid/hot), overheat tremble, focus blur, laser ring (segment count / radii / gap / inactive alpha / fire pulse radius), hit pulse profiles (Normal/Kill/Headshot/Ricochet).
- **`DevCheatsLaserSection`** — `ChargeRatioPower` (curve shape), `ChargeTimeOverrideSeconds` (baseline override), per-delivery charge multipliers, charge damage min/power, shotgun spread/lifetime tunables.

## Key Files

- `Assets/Shaders/CrosshairSDF.shader` — single fragment SDF shader (4-arm + dot + reload arc + flame bars + laser segmented ring + hit pulse stubs + outline; branched on `_LaserMode`)
- `Assets/Resources/Vfx/Materials/Crosshair.mat` — cloned to a runtime instance via `new Material(...)` in `CrosshairPresenter` (NOT auto-instanced by `Image.material`)
- `Assets/Resources/Vfx/Prefabs/UI/Crosshair.prefab` — Screen-Space Overlay Canvas + fullscreen RawImage
- `Assets/Scripts/View/CrosshairPresenter.cs` — plain class, LateTick from `App.LateTick`
- `Assets/Scripts/View/PointerOverUiTracker.cs` — MonoBehaviour on AppBootstrap; pointer-over-UI broadcast + OS cursor visibility
- `Assets/Scripts/Dev/Sections/ViewCheatsCrosshairV2Section.cs` — tunables, `HitPulseProfile` struct
- `Assets/Scripts/Dev/Sections/DevCheatsLaserSection.cs` — laser charge tunables + `EvaluateChargeRatio` helper
- `Assets/Scripts/Systems/AimingSystem.cs` — recoil decay (subtract-apply)
- `Assets/Scripts/Systems/ShootingSystem.cs` — recoil kick application + Firing/Cooldown phase transitions
- `Assets/Scripts/Systems/WeaponChargeResolver.cs` — charge time resolution з override support
- `Assets/Scripts/View/UI/UiPanelHitTest.cs` — UI panel pick utility
