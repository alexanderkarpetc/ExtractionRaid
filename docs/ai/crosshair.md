# Crosshair System

Weapon-state crosshair rendered via IMGUI `OnGUI()` in `View/AimCursorOverlay.cs`.

## Two Cursors

| Cursor | Source | Visual | Purpose |
|--------|--------|--------|---------|
| **Raw** | `player.RawAimPoint` | Small white dot (6px) | Player intent, instant from mouse |
| **Weapon** | `player.WeaponAimPoint` | Crosshair / state indicator | Where weapon actually aims, carries state info |

Raw cursor is always a dot and never changes. Weapon cursor shape/color/animation reflects weapon state.

## Weapon Cursor States

| WeaponPhase | Condition | Visual | Color |
|-------------|-----------|--------|-------|
| `Ready` | `AmmoInMagazine > 0` | 4-line crosshair + center dot | Green `(0.2, 1, 0.3, 0.9)` |
| `Ready` | Empty mag + no reserve | 4-line crosshair + center dot | Red `(1, 0.25, 0.2, 0.9)` |
| `Firing` | Just shot (1 tick) | Crosshair with max bloom gap | White `(1, 1, 1, 0.95)` |
| `Cooldown` | Post-shot delay | Bloom gap contracting back | White->Green lerp |
| `Reloading` | Reload in progress | Ring of 12 dots (no crosshair) | Orange filled / gray empty |
| `Equipping` | Drawing weapon | Crosshair fading in | Green, alpha 0->1 |
| `Unequipping` | Holstering weapon | Crosshair fading out | Green, alpha 1->0 |
| Unarmed | `EquippedWeapon == null` | Single gray dot (15px) | Gray `(0.7, 0.7, 0.7, 0.6)` |

Rolling (`IsRolling == true`) applies 0.3 alpha multiplier to any state above.

## Crosshair Geometry

```
        |          <- top bar (fades out during ADS)
        |
   ===  .  ===    <- left bar, center dot, right bar
        |
        |          <- bottom bar
```

All values configurable via DevCheats (Crosshair section):
- Line thickness: `CrosshairLineThickness` (default 6px)
- Line length: `CrosshairLineLength` (default 24px)
- Base gap (center to inner edge): `CrosshairBaseGap` (default 15px)
- Center dot: `CrosshairCenterDotSize` (default 9px)
- Bloom extra gap: `CrosshairBloomExtraGap` (default 30px)

## ADS Crosshair

During ADS (`player.AdsBlend` 0->1), crosshair interpolates toward tighter values:
- Gap lerps from `CrosshairBaseGap` to `AdsBaseGap`
- Bloom lerps from `CrosshairBloomExtraGap` to `AdsBloomExtraGap`
- Top crosshair line fades out (`alpha *= 1 - adsBlend`) — creates a 3-line T-shape

## Bloom Animation

Triggered by Firing->Cooldown. Gap starts expanded, contracts to base.

```
progress = SmoothStep(0, 1, elapsed / weapon.FireInterval)
currentGap = adsGap + adsBloomExtra * (1 - progress)
color = Lerp(white, green, progress)
```

## Reload Ring

12 dots arranged in circle (radius 42px), starting from 12 o'clock, clockwise. Dot size 9px.

```
progress = Clamp01(elapsed / weapon.ReloadTime)
filledCount = Floor(progress * 12)
```

Filled dots = orange, unfilled = dim gray. Center dot in orange. Crosshair lines hidden.

## Recoil

Firing displaces the crosshair (WeaponAimPoint) away from the player. The gap between raw dot and crosshair = recoil magnitude.

**Two components per shot:**
1. **Forward kick** (`RecoilKickForward`) — pushes aim away from player along `+AimDirection` (main recoil)
2. **Sideways scatter** (`RecoilKickSide`) — random perpendicular displacement (spread)

**Subtract-apply pattern** in AimingSystem prevents double-recovery:
```
cleanAim = WeaponAimPoint - RecoilOffset       // strip recoil
cleanAim = Lerp(cleanAim, mouse, smoothFactor)  // base tracking (AimFollowSharpness)
RecoilOffset = Lerp(RecoilOffset, zero, decay)  // recoil decay (RecoilRecoverySpeed)
WeaponAimPoint = cleanAim + RecoilOffset         // combine
```

ShootingSystem applies kick after firing (both components go through `RecoilOffset`):
```
adsRecoilScale = Lerp(1, AdsRecoilMultiplier, AdsBlend)  // reduced in ADS
recoilMul = RecoilMultiplier * adsRecoilScale
aimDir = normalize(WeaponAimPoint - PlayerPosition)
RecoilOffset += aimDir * RecoilKickForward * recoilMul * RecoilForwardMultiplier
right = perpendicular(aimDir)  // 90deg CW on XZ
RecoilOffset += right * Random(-RecoilKickSide, +RecoilKickSide) * recoilMul * RecoilSideMultiplier
```

DevCheats multipliers (all stack):
- `RecoilMultiplier` — global kick scale
- `RecoilForwardMultiplier` — forward channel only
- `RecoilSideMultiplier` — side channel only
- `RecoilRecoveryMultiplier` — decay speed

| Weapon | RecoilKickForward | RecoilKickSide | RecoilRecoverySpeed | Behavior |
|--------|------------------|----------------|---------------------|----------|
| Rifle | 2 | 1.5 | 2 | Moderate forward, slight scatter. Full-auto accumulates. |
| Shotgun | 3 | 6 | 3 | Heavy forward kick, noticeable scatter. Mostly recovers between shots. |
| Pistol | 1.5 | 1 | 4 | Light kick, minimal scatter. Fast recovery between semi-auto shots. |

## Hit Marker System

COD-style X-markers on the crosshair, driven by `HitConfirmed` events from `DamageSystem`.

### HitMarker Struct

```csharp
struct HitMarker {
    float time;              // Time.time when created
    bool isKill;             // target died from this hit
    bool isHeadshot;         // headshot hit
    float absorptionRatio;   // 0 = full pen, 1 = full absorption by armor
    bool isRicochet;         // bullet ricocheted off helmet
}
```

Markers are collected in `LateUpdate()` from `HitConfirmed` events and rendered each `OnGUI()`. Each marker fades out over its duration (alpha = 1 - age/duration).

### Marker Types

**Regular hit** (white X):
- 4-arm diagonal X drawn at crosshair center
- Line length: `HitLineLength` (default 14px) scaled by `HitMarkerScale`
- Gap starts at `HitGapStart`, expands by `HitGapExpand * t` over lifetime
- Duration: `HitDuration` (default 0.3s)
- Color: `HitColor` (default white)

**Kill** (red X):
- Same shape as hit but larger: `KillLineLength` (default 18px)
- Duration: `KillDuration` (default 0.5s)
- Color: `KillColor` (default red `(1, 0.15, 0.15, 1)`)

**Headshot** (gold double-X):
- Inner X same as kill (uses `KillLineLength`)
- Outer X at `HeadshotOuterScale` (default 1.25x) with `HeadshotOuterExpandMul` (default 1.62x) faster expansion
- Outer X alpha = inner alpha * 0.7
- Duration: `HeadshotDuration` (default 0.5s)
- Color: `HeadshotColor` (default gold `(1, 0.85, 0.2, 1)`)

**Ricochet** (blue spark):
- Smaller X: length = `HitLineLength * 0.5`, gap = `HitGapStart * 0.6`, thickness = `HitMarkerThickness * 0.8`
- No gap expansion (static shape, just fades)
- Duration: `RicochetDuration` (default 0.2s) — shortest of all markers
- Color: `RicochetColor` (default blue `(0.5, 0.7, 1, 1)`)

### Proportional Hit Markers (Armor Absorption)

Hit markers scale by armor `absorptionRatio` (0 = full penetration, 1 = full absorption):

**Size scaling**:
```
absScale = 1 - absorptionRatio * 0.5   // 1.0 at full pen, 0.5 at full absorption
lineLen = baseLineLen * scale * absScale
```

**Color blending** (regular hits only, not kill/headshot):
```
color = Lerp(HitColor, ArmorHitColor, absorptionRatio)
```
- `ArmorHitColor` default: gray-blue `(0.6, 0.65, 0.7, 1)` — heavily armored hits appear muted and small
- Kill and headshot colors are never blended (always their distinct color)

### DevCheats Hit Marker Parameters

All in the Crosshair section of `DevCheatsCrosshairSection`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `HitMarkerScale` | 1.0 | Global scale multiplier for all markers |
| `HitDuration` | 0.3s | Regular hit fade duration |
| `KillDuration` | 0.5s | Kill marker fade duration |
| `HeadshotDuration` | 0.5s | Headshot marker fade duration |
| `RicochetDuration` | 0.2s | Ricochet spark fade duration |
| `HitLineLength` | 14px | Regular hit X arm length |
| `KillLineLength` | 18px | Kill/headshot X arm length |
| `HitGapStart` | 8px | Initial gap from center |
| `HitGapExpand` | 14px | Gap expansion over lifetime |
| `HitMarkerThickness` | 4px | X arm thickness |
| `HitColor` | White | Regular hit color |
| `KillColor` | Red `(1, 0.15, 0.15)` | Kill marker color |
| `HeadshotColor` | Gold `(1, 0.85, 0.2)` | Headshot marker color |
| `ArmorHitColor` | Gray-blue `(0.6, 0.65, 0.7)` | Armor absorption tint |
| `RicochetColor` | Blue `(0.5, 0.7, 1)` | Ricochet spark color |
| `HeadshotOuterScale` | 1.25 | Outer X scale relative to inner |
| `HeadshotOuterExpandMul` | 1.62 | Outer X gap expansion speed multiplier |

## Technical Notes

- Single `Texture2D(1,1)` white pixel; all colors via `GUI.color`
- `AmmoSystem.CountReserve()` called as read-only query for no-reserve detection
- Progress calculations: `elapsed = RaidState.ElapsedTime - weapon.PhaseStartTime`
- World-to-GUI: `cam.WorldToScreenPoint()` + Y-flip (`Screen.height - screenPos.y`)

## Key Files

- `Assets/Scripts/View/AimCursorOverlay.cs` — crosshair rendering
- `Assets/Scripts/Systems/AimingSystem.cs` — recoil decay (subtract-apply pattern)
- `Assets/Scripts/Systems/ShootingSystem.cs` — recoil kick application
- `Assets/Scripts/Dev/Sections/DevCheatsCrosshairSection.cs` — hit marker DevCheats params
- `Assets/Scripts/Dev/Sections/DevCheatsADSSection.cs` — ADS crosshair params
