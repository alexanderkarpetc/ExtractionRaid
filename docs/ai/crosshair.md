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

## Technical Notes

- Single `Texture2D(1,1)` white pixel; all colors via `GUI.color`
- `AmmoSystem.CountReserve()` called as read-only query for no-reserve detection
- Progress calculations: `elapsed = RaidState.ElapsedTime - weapon.PhaseStartTime`
- World-to-GUI: `cam.WorldToScreenPoint()` + Y-flip (`Screen.height - screenPos.y`)

## Key File

`Assets/Scripts/View/AimCursorOverlay.cs`
