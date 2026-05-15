# Aim Cursor v2 — Status Doc

> Living spec + plan for the v2 aim cursor pass started 2026-05-14.
> **Status**: Stage 1 shipped 2026-05-15 (tech foundation + 1:1 IMGUI port). **Ready for playtest validation → Stage 2 (recoil kick)**.
> Pickup point if context resets — read this doc top-to-bottom, then start Stage 1.

---

## Vision

Replace current IMGUI-based [`AimCursorOverlay`](../../../Assets/Scripts/View/AimCursorOverlay.cs) with a hybrid uGUI + SDF-shader stack that delivers:

- **EFD-style directional recoil kick** (XY offset on reticle + spring back) — the "juice beyond bloom" user explicitly requested
- **Focus blur edge** — continuous accuracy state via reticle sharpness
- **3-tier range color** (green/white/red) with hysteresis, by cursor-to-player distance
- **Same-pixel swap** to inventory pointer on Tab (no warp, no pause — genre contract from EFD/ZERO Sievert)
- **Bloom + low-ammo warning + reload/charge arcs** as polish

Restrained-tactical tone (Destiny 2 reference), NOT power-fantasy (Borderlands). Top-down extraction shooter is the genre — cursor reads each frame the player plays.

---

## Locked decisions (all approved by user)

### Tech stack — Hybrid uGUI + SDF shader

| Layer | Tech |
|---|---|
| **Reticle** | Single fullscreen `RawImage` with hand-written SDF URP shader. All visual params via `MaterialPropertyBlock`. |
| **Hit markers** (transient 4 kinds) | Pooled `Image` children, animate via coroutine/Update |
| **Inventory pointer** | Toggleable `Image` sibling, programmatic arrow sprite |
| **Canvas** | Screen-Space Overlay (NOT World Space — parallax/DPI bites) |

Reference: Unity 6 ships [UGUI Shaders Sample](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.2/manual/Shader-Graph-Sample-UGUI-Shaders.html). **No UI artist needed** — all visuals SDF-procedural.

**Rejected alternatives**:
- IMGUI: ceiling reached, no shader access for blur/bloom
- UI Toolkit: per-element custom-shader story still maturing in Nov 2025
- Pure procedural mesh: weak for boring parts (text labels, swap to pointer)

### Mechanics — locked

| Feature | Decision |
|---|---|
| **Recoil kick** | EFD path — per-shot 2D offset on reticle (radial along aim ray + perpendicular swing) + critically-damped spring back |
| **Focus blur** | Continuous `_BlurEdge` shader param, driven by recoil pressure + ADS settle |
| **Spread visualization** | **NO 1:1 cone** (Synthetik trap). Subtle bloom (gap expand) sustained-fire only as feel indicator. First-shot perfect center. |
| **Range tier color** | 3 discrete tiers (Green ≤80% / White 80-110% / Red >110% of effective range), 10% hysteresis, by cursor-to-player distance. NO smooth lerp. |
| **Hit markers** | Keep current 4 kinds (white X / red kill X / gold headshot / blue ricochet). Migrate to Image children. |
| **UI cursor swap** | Tab toggle. No pause. **Same-pixel** sprite swap (EFD path, anti-Tarkov-warp). Fire-on-close 1-frame debounce. |
| **Reload ring** | Arc fill in SAME SDF shader (`_RingFill = elapsed/reloadTime`). Crosshair lines hide during reload. |
| **Charge ring** (laser) | Similar arc fill, separate color param (`_ChargeArcColor`). |

---

## State matrix

### A. Primary weapon FSM phase

| Phase | Visual | Shader params |
|---|---|---|
| **Ready** | Baseline crosshair `──  ●  ──` (4 arms, gap=base, dot center) | `_Gap=base, _RingFill=0, _Bloom=0, _BlurEdge=sharp` |
| **Charging** (laser) | Arc grows around dot, color = energy cyan, fills counter-clockwise | `_ChargeFill=0..1, _ChargeArcColor=cyan` |
| **Firing** (1 frame) | Max bloom flash + recoil kick offset begins | `_Gap=base+max_bloom, _Bloom=1, _RecoilOffset=kick` |
| **Cooldown** | Bloom decays + recoil springs back over FireInterval | `_Gap=base+bloom×(1-t), _RecoilOffset=spring`, easing |
| **Bursting** (laser+Auto) | Each burst shot triggers Firing visual; gap stays bloomed | Continuous Firing/Cooldown chain |
| **Reloading** | Crosshair lines hide, ring progress draws 0→360° | `_RingFill=elapsed/reloadTime, _LinesHidden=1` |
| **DryFire** | Quick red flash, reticle pulses warning color | `_Color=warningRed, _PulseT=0..1 decay` |
| **Equipping** | Fades in over equipTime, gap → base | `_Alpha=t/equipTime` |
| **Unequipping** | Fades out | `_Alpha=1-t/unequipTime` |
| **Unarmed** (no weapon) | Single dim circle, no arms | `_LinesHidden=1, _DotRadius=large, _Alpha=0.5` |

### B. Orthogonal modifiers (stack on top of primary phase)

| Modifier | Trigger | Visual effect |
|---|---|---|
| **Range tier: Green** | Cursor-to-player distance ≤ 80% × effective | All-cursor green tint (`_TierColor=#7AE08A`) |
| **Range tier: White** | 80%-110% effective (sweet spot) | Default white tint (`_TierColor=#FFFFFF`) |
| **Range tier: Red** | > 110% (falloff zone) | Red tint (`_TierColor=#E06060`) |
| **Recoil kick offset** | After Firing | Reticle whole shifts XY in kick direction, springs back over Cooldown |
| **Focus blur** | Recoil active OR ADS not settled | `_BlurEdge` increases — reticle edges soften / fuzzy |
| **ADS** | Player holding ADS | Gap smaller (tighter), lerp toward `_Gap=adsGap`, follow sharpness ↑ |
| **Rolling** | Player dodge | Alpha drops to 30%, dimmer everything |
| **Low ammo warning** | `AmmoInMagazine ≤ 25%` | Subtle yellow tint pulse (`_AmmoWarnTint`) |

### C. Transient overlays (spawned, fade, recycle)

| Overlay | Trigger | Visual |
|---|---|---|
| **Hit X (white)** | EntityHit normal | Small white X flies outward 30px, fades 0.4s |
| **Kill X (red)** | EntityHit + isKill | Larger red X, longer fade 0.6s |
| **Headshot double-X (gold)** | EntityHit + isHeadshot | Two gold Xs offset by 8px |
| **Ricochet spark (blue)** | EntityHit + isRicochet | Blue spark/dot, no X, short fade 0.3s |

### D. Mode states

| Mode | Trigger | Visual |
|---|---|---|
| **Gameplay aim** (default) | Default | Full crosshair as above |
| **Inventory navigation** | Tab pressed | Crosshair → arrow pointer at SAME pixel, no warp |
| **In menu / cutscene** | Modal open | Cursor hidden OR pointer-only |
| **Dead** | Player dies | Cursor hidden, fade out |

### ASCII sketches

Ready baseline:
```
              ╷
              │
              │
        ───       ───
              ●
        ───       ───
              │
              │
              ╵
```

Recoil kick (vertical shot — radial outward + slight side):
```
                  ╷
                  │ ← shifted up + slight right
            ───       ───
                  ●
            ───       ───
                  │
                  ╵
```

Charging laser:
```
              ╷
            ╱⌒╲
        ───●───
            ╲⌐╱   ← arc growing counter-clockwise
              │
              ╵
```

Reloading:
```
            ╱⌒⌒⌒⌒⌒╲
          ╱   60%     ╲   ← progress arc
         ⌐  [no lines] ⌐
          ╲___________╱
```

Inventory pointer mode:
```
              ◤    ← arrow at same pixel
              ◢      crosshair was hidden, pointer takes over
```

---

## Implementation plan — Stage breakdown

Each Stage = independent ship + validate. Toggle `UseV2Crosshair` in DevCheats during transition. Final Stage 7 deletes legacy.

### Stage 1 — Tech foundation + 1:1 port (~6h)

**Goal**: new tech stack works, visually identical to current IMGUI.

**Files (NEW)**:
- `Assets/Shaders/CrosshairSDF.shader` — SDF: 4 arms + center dot, params `_Gap, _LineLength, _LineThickness, _DotRadius, _Color, _Alpha, _RingFill, _ChargeFill`
- `Assets/Resources/Vfx/Materials/Crosshair.mat` — URP UI material
- `Assets/Resources/Vfx/Prefabs/UI/Crosshair.prefab` — Canvas (Screen-Space Overlay) + RawImage fullscreen
- `Assets/Resources/Vfx/Prefabs/UI/HitMarker.prefab` — Image child + `HitMarkerInstance` script
- `Assets/Scripts/View/CrosshairPresenter.cs` — plain class у App. LateTick reads weapon state, drives MaterialPropertyBlock. Spawns hit markers from pool.
- `Assets/Scripts/View/HitMarkerInstance.cs` — MonoBehaviour, animate fly-out + fade + recycle
- `Assets/Scripts/Dev/Sections/ViewCheatsCrosshairV2Section.cs` — v2 tunables + `UseV2Crosshair` toggle (default OFF)

**Files (TOUCH)**:
- `AimCursorOverlay.cs` — guard за `!UseV2Crosshair` (no-op when v2 enabled, fallback)
- `App.cs` — register `CrosshairPresenter` ctor + LateTick + Dispose
- `Dev/ViewCheatsConfig.cs` — accessor
- `Editor/DevCheatsWindow.cs` — section UI + CreateSectionIfMissing

**Phase coverage**: Ready / Firing / Cooldown / Reloading / Charging / Bursting / Equipping / Unequipping / Unarmed / DryFire — all existing v1 ports 1:1. Hit markers 4 kinds ported.

**Validation**: Toggle ON in Play. Visible crosshair, all phases, all 4 markers. Toggle OFF → IMGUI returns. NO regressions.

---

### Stage 2 — Directional recoil kick (~2h)

**Goal**: EFD-style XY offset on cursor per shot + spring back.

**Changes**:
- Shader `_RecoilOffset` (Vector2) — center at `0.5 + _RecoilOffset` instead of fixed 0.5
- `CrosshairPresenter`: maintain `Vector2 _kickVelocity, _kickPosition`. SmoothDamp recovery via weapon stat `RecoilRecoverySpeed`.
- On WeaponFired event:
  - Radial: `aimDir2D * verticalKickAmount` (outward along aim ray)
  - Perpendicular: `aimRight2D * Random(-sideKick, sideKick)`
  - Add to `_kickPosition` (impulse, accumulates)
- LateTick: `_kickPosition = SmoothDamp(_kickPosition, Vector2.zero, ref _kickVelocity, recoverTime)` → push to shader

**Section tunables**: `RecoilKickRadial`, `RecoilKickPerpendicular`, `RecoilRecoverTime`.

**Validation**: shot → cursor stretches outward, perp swings ±, returns. Sustained auto = accumulating kick reads visually.

---

### Stage 3 — Focus blur edge (~2h)

**Goal**: continuous blur amount on reticle edges per accuracy state.

**Changes**:
- Shader `_BlurEdge` (0..1) — smoothstep edge softness у SDF: `alpha = 1 - smoothstep(0, _BlurEdge × blurScale, sdf)`. Higher = fuzzier.
- `CrosshairPresenter`: blur driven by:
  - **Recoil pressure** — `magnitude(_kickPosition)` (just shot → blurry)
  - **ADS settle** — `1 - player.AdsBlend` (not ADS → blurry; ADS settled → sharp)
- Combine: `_BlurEdge = lerp(maxBlur, minBlur, accuracy)`

**Section**: `BlurMin`, `BlurMax`, `BlurRecoilWeight`, `BlurAdsWeight`.

**Validation**: cursor sharp ready, fuzzy during sustained fire, sharp again after recovery. ADS → sharper.

---

### Stage 4 — Range tier color (~2h)

**Goal**: 3 discrete tiers (Green/White/Red), 10% hysteresis, by distance cursor-to-player.

**Changes**:
- Shader `_TierColor` (Color) — multiplies face fill
- `CrosshairPresenter` LateTick:
  - `dist = (cursorWorldPos - playerPos).magnitude`
  - `effective = weapon.Stats.EffectiveRange`
  - `ratio = dist / effective`
  - Apply hysteresis: only flip tier if exceeded boundary + hysteresisBuffer
  - Color: ratio < 0.8 → green, 0.8-1.1 → white, > 1.1 → red

**Section**: `RangeColorGreen`, `RangeColorWhite`, `RangeColorRed`, `RangeBoundaryNear` (0.8), `RangeBoundaryFar` (1.1), `RangeHysteresisRatio` (0.05).

**Tests**: pure logic test on hysteresis math (deterministic).

**Validation**: walk closer to bot — green at close range, white at mid, red at far. No strobing on boundary.

---

### Stage 5 — UI cursor mode swap (~3h)

**Goal**: Tab → inventory pointer, same-pixel swap, no pause, no warp.

**Changes**:
- Add `_InventoryCursorActive` state в `CrosshairPresenter`
- On Tab pressed: hide crosshair RawImage, show pointer Image
- Pointer follows mouse via `RectTransformUtility.ScreenPointToLocalPointInRectangle`
- Game keeps running. Weapon stops tracking cursor while inventory mode active — cache last aim.
- On Tab close:
  - Pointer hidden, crosshair restored at SAME pixel
  - **Fire-on-close debounce**: skip LMB until first release-after-close OR 1 frame

**Files**:
- `CrosshairPresenter` extends — pointer state
- `Resources/Vfx/Prefabs/UI/InventoryPointer.prefab` — single Image (procedural arrow or Unity built-in)
- `Adapters/IInputAdapter.cs` — add `InventoryTogglePressed` event if missing
- Section: `EnableInventoryPointer` toggle (default ON)

**Validation**:
- Tab open → crosshair → pointer, same pixel, no warp
- Pointer over UI → click consumes UI event
- Click in inventory не fires weapon
- Tab close → fire blocked 1 frame, pointer → crosshair, no warp
- Game runs під час inventory open

---

### Stage 6 — Polish: bloom + low-ammo + reload/charge arcs unified (~3h)

**Goal**: last visual surfaces для final feel.

**Changes**:
- **Bloom**: `_BloomStrength` shader param. Additive secondary layer (soft halo). Sustained-fire only, decays over Cooldown.
- **Low-ammo pulse**: `AmmoInMagazine ≤ 25%` → subtle yellow tint pulse (`_AmmoWarnTint`) blended over `_TierColor`. ~2Hz frequency.
- **Reload arc**: instead of separate ring overlay, draw arc in SAME SDF (`_RingFill = elapsed/reloadTime`). Crosshair lines hide during reload.
- **Charge arc** (laser): similar `_ChargeFill` param.

**Section**: `BloomMaxStrength`, `BloomDecayRate`, `LowAmmoThreshold`, `LowAmmoPulseFreq`, `LowAmmoTint`, `ReloadArcColor`, `ChargeArcColor`.

**Validation**: sustained auto = soft glow bloom. Low ammo pulses subtle yellow. Reload = animated arc fills 0→full. Charge = similar для laser.

---

### Stage 7 — Legacy cleanup (~30 min)

After all stages validated + locked:
- Delete `Assets/Scripts/View/AimCursorOverlay.cs` + meta
- Remove `gameObject.AddComponent<AimCursorOverlay>()` from `AppBootstrap.cs`
- Remove `UseV2Crosshair` toggle field (always-on)
- Update `docs/ai/crosshair.md` + `.cursor/rules/crosshair.mdc` (mirror per CLAUDE.md §8)
- Update [`README.md`](README.md) — mark Aim cursor v2 ✅ shipped

---

## Validation strategy per stage

Кожен Stage ship-имо:
1. Code + asset changes
2. EditMode tests run — 506+ pass (current baseline)
3. Toggle `UseV2Crosshair` ON у DevCheats
4. Manual playtest — verify Stage's specific behavior
5. If feel-OK → ship + commit + next Stage
6. If issues → iterate within Stage, doesn't block next stages

---

## Open questions (still open)

| # | Q | Default |
|---|---|---|
| **Q1** | Inventory toggle key — Tab чи I чи existing keybind? Decided in Stage 5. | Tab (genre standard) |
| **Q2** | Per-archetype variants — defer to v3, evaluate after Stage 6 ships? | Yes — assess after playtest of v2 baseline |

---

## Deferred / cut items

| Item | Status | Reasoning |
|---|---|---|
| **Numeric range readout** (Duckov-style `23m` near cursor) | ❌ **cut** | Range tier color already carries info. Number breaks immersion в tactical tone. User decision. |
| **Per-archetype cursor variants** (Synthetik-style different shapes per weapon) | 🟡 **deferred** | 5th channel of archetype diff w/ diminishing return. Consistency wins over recognition for extraction tone. Re-evaluate post-v2 playtest. |
| **Hold-to-peek overlay** (Alt-hold, RoR2 style) | 🟡 **deferred** | Defer until inventory mode (Stage 5) ships. Then assess if quick-glance overlay needed. |
| **True Gaussian blur via ScriptableRenderFeature** | 🟡 **deferred** | SDF smoothstep edge softness should suffice. Revisit if blur not strong enough. |
| **Bots screen-gate hysteresis** (mentioned in earlier session) | ❌ **cut** | Speculative polish, current hard radius works fine. No playtest signal. |

---

## Research sources (5 background agents, 2026-05-14)

1. **EFD deep dive + top-down cursor cluster** — confirmed EFD recoil is **2D positional kick** (radial along aim ray + perpendicular swing), NOT sprite rotation. EFD also uses focus blur for accuracy state.
2. **Spread cone visualization cross-genre** — 1:1 cluster (CS, ZS, Synthetik) vs metaphorical (BL3, R6, Apex). Genre note: EFD specifically **decouples** spread from reticle.
3. **UI cursor mode switching** — genre contract (Duckov/ZS/Tarkov don't pause). Tarkov's center-warp = anti-pattern. EFD/ZS same-pixel swap is the recommended pattern.
4. **Tech choice (IMGUI/uGUI/UI Toolkit/Procedural mesh/Hybrid)** — hybrid uGUI RawImage + SDF shader recommended. Unity 6 UGUI Shaders Sample is direct reference.
5. **Range tier color (does anyone ship?)** — ZS + EFD ship it. 3 tiers + hysteresis recommended. Avoid Synthetik-style cluttered cone.

---

## Related docs

- [`README.md`](README.md) — gunplay shipped state + remaining backlog
- [`archetype-differentiation.md`](archetype-differentiation.md) — archetype design pass (closed)
- [`../crosshair.md`](../crosshair.md) — current crosshair behavior (will be updated Stage 7)
- [`../weapons.md`](../weapons.md) — weapon FSM, charge/burst details
- [`../CLAUDE.md`](../CLAUDE.md) §8 — doc mirror rule for crosshair.md ↔ crosshair.mdc

---

## Pickup instructions for next agent

1. Read this doc top-to-bottom (you are here)
2. Verify current state — check if `UseV2Crosshair` exists in `ViewCheatsCrosshairV2Section`. If yes → Stage 1 is done, identify next Stage from git history or section state.
3. If at Stage 1 start — read [`AimCursorOverlay.cs`](../../../Assets/Scripts/View/AimCursorOverlay.cs) to understand v1 behavior + `WeaponEntityState.Phase` enum from `weapons.md`.
4. Begin Stage 1 — build SDF shader + presenter + section. Toggle `UseV2Crosshair` for A/B testing during development.
5. After each stage: run EditMode tests, playtest manually, commit.
6. Update this doc's **Status** field at the top + check completed stages.

---

## Status log

| Date | Stage | Notes |
|---|---|---|
| 2026-05-14 | spec | Locked all design decisions. Plan approved. Ready for Stage 1. |
| 2026-05-15 | Stage 1 | Shipped tech foundation + 1:1 IMGUI port. SDF shader (`CrosshairSDF.shader`) + Crosshair.mat + Crosshair.prefab + HitMarker.prefab + `CrosshairPresenter.cs` + `HitMarkerInstance.cs` + `ViewCheatsCrosshairV2Section.cs` (`UseV2Crosshair=false` default). All 10 phase states wired (Ready/Charging/Firing/Cooldown/Bursting/Reloading/Equipping/Unequipping/Unarmed/DryFire). Hit markers (4 kinds) pooled. AimCursorOverlay guards on toggle. 506/506 tests pass. **Validation pending — toggle ON у DevCheats, manual playtest.** |
