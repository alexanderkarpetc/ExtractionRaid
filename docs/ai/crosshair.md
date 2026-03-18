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
| `Cooldown` | Post-shot delay | Bloom gap contracting back | White→Green lerp |
| `Reloading` | Reload in progress | Ring of 12 dots (no crosshair) | Orange filled / gray empty |
| `Equipping` | Drawing weapon | Crosshair fading in | Green, alpha 0→1 |
| `Unequipping` | Holstering weapon | Crosshair fading out | Green, alpha 1→0 |
| Unarmed | `EquippedWeapon == null` | Single gray dot (5px) | Gray `(0.7, 0.7, 0.7, 0.6)` |

Rolling (`IsRolling == true`) applies 0.3 alpha multiplier to any state above.

## Crosshair Geometry

```
        ║          <- top bar
        ║
   ═══  ·  ═══    <- left bar, center dot, right bar
        ║
        ║          <- bottom bar
```

- Line thickness: 2px, length: 8px
- Base gap (center to inner edge): 5px
- Center dot: 3x3 px
- Bloom extra gap: +10px (total 15px at max bloom)

## Bloom Animation

Triggered by Firing→Cooldown. Gap starts expanded (15px), contracts to base (5px).

```
progress = SmoothStep(0, 1, elapsed / weapon.FireInterval)
currentGap = BaseGap + BloomExtra * (1 - progress)
color = Lerp(white, green, progress)
```

## Reload Ring

12 dots arranged in circle (radius 14px), starting from 12 o'clock, clockwise.

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
aimDir = normalize(WeaponAimPoint - PlayerPosition)
RecoilOffset += aimDir * RecoilKickForward * RecoilMultiplier * RecoilForwardMultiplier
right = perpendicular(aimDir)  // 90° CW on XZ
RecoilOffset += right * Random(-RecoilKickSide, +RecoilKickSide) * RecoilMultiplier * RecoilSideMultiplier
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

## Technical Notes

- Single `Texture2D(1,1)` white pixel; all colors via `GUI.color`
- `AmmoSystem.CountReserve()` called as read-only query for no-reserve detection
- Progress calculations: `elapsed = RaidState.ElapsedTime - weapon.PhaseStartTime`
- World-to-GUI: `cam.WorldToScreenPoint()` + Y-flip (`Screen.height - screenPos.y`)

## Key Files

- `Assets/Scripts/View/AimCursorOverlay.cs` — crosshair rendering
- `Assets/Scripts/Systems/AimingSystem.cs` — recoil decay (subtract-apply pattern)
- `Assets/Scripts/Systems/ShootingSystem.cs` — recoil kick application
