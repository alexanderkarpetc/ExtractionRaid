# Aim Cursor v2 — Status Doc

> Spec + shipping log for the v2 aim cursor pass — started 2026-05-14, ✅ EPIC CLOSED 2026-05-18.
> **Final status**: Stages 1 + 3 + 5 + 7 SHIPPED. Stages 2 + 4 + 6 CUT (rationale in respective sections below). Legacy IMGUI `AimCursorOverlay` deleted. Live v2 features: 1:1 IMGUI port + ADS top arm cutoff + outline + EFD-style hit pulse (4 per-event-type profiles) + flame charge fill (ballistic) + segmented ring (laser, 12 slices clockwise) + overheat tremble + tunable charge curve (`ChargeRatioPower` + `ChargeTimeOverride`) + laser firing animation (chargeFill cooldown decay + radial pulse) + focus blur edge (recoil + ADS driven). UI cursor swap via existing `PointerOverUiTracker` infrastructure. See [`docs/ai/crosshair.md`](../crosshair.md) for the living system reference; this file is archived as historical spec + status log.
> Pickup point if context resets — read this doc top-to-bottom, then start Stage 1.

---

## Vision

Replace current IMGUI-based `AimCursorOverlay` (deleted Stage 7 — see git history pre-2026-05-18) with a hybrid uGUI + SDF-shader stack that delivers:

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

### ~~Stage 2 — Directional recoil kick~~ — ✂️ CUT 2026-05-15

**Reason**: scouting before implementation found that directional recoil kick is **already fully implemented at the gameplay layer**, not view-only:
- `WeaponEntityState.RecoilOffset` (Vector3 world-space) is the single source of truth.
- `ShootingSystem` applies impulse on fire: `aimDir × RecoilKickForward` (radial) + `right × Random(±RecoilKickSide)` (perpendicular).
- `AimingSystem` decays it exponentially via `RecoilRecoverySpeed` (per-weapon stat) + ADS modifier (`AdsRecoilRecoveryMultiplier`).
- `player.WeaponAimPoint = cleanAim + RecoilOffset` — affects headshot detection + projectile direction AND propagates to the cursor naturally via `cam.WorldToScreenPoint(player.WeaponAimPoint)` in `CrosshairPresenter`.

Implementing the planned view-only `_kickPosition` would have **doubled** the visual shift (gameplay shifts WeaponAimPoint already; presenter would add a second offset on top) and desynced hit detection from cursor visual. Existing impl is gameplay-rooted and consistent — exactly what EFD-style asks for. Recoil polish (saturation cap, per-archetype tuning, laser chargeRatio scaling) could be a future pass, but is not blocking the Aim Cursor v2 epic.

**Outcome**: skipped, plan continues to Stage 3 (focus blur). Existing recoil system stays as-is.

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

### ~~Stage 4 — Range tier color~~ — ✂️ CUT 2026-05-18

**Reason**: there is **no concept of effective range in our weapon model**. Weapon stats don't carry `EffectiveRange` field; falloff exists only as projectile lifetime / spread but isn't a designer-tuned per-weapon distance. Adding the feature would require gameplay-layer work (introducing EffectiveRange stat, tying it to falloff math) before the cursor visual could read meaningful data. Without that backbone, a 3-tier color would be hard-coded by metric we don't ship — premature UX.

**Outcome**: skipped. If/when EffectiveRange becomes a real weapon stat, revisit this as a polish pass — shader path is trivial (`_TierColor` multiplier already-easy add).

---

### Stage 5 — UI cursor mode swap — ✅ SHIPPED 2026-05-18 (via existing infrastructure)

**Goal**: Tab → inventory pointer, same-pixel swap, no pause, no warp.

**Outcome**: Stage 5 scope re-evaluated during scouting — most of the planned work was **already present** in the codebase, predating Aim Cursor v2. Existing infrastructure satisfies all primary acceptance criteria. The remaining nice-to-have items (fire-on-close debounce, custom procedural pointer sprite, section toggle) were deemed not worth the build cost vs. current feel — playtest showed OS-cursor swap + UiPanelHitTest is "good enough".

**What was already in place**:
- `IInputAdapter.InventoryTogglePressed` — bound to Tab у `UnityInputAdapter:142-149`.
- `IInputAdapter.IsPointerOverUi` — broadcast flag set by `AimCursorOverlay.Update` from `UiPanelHitTest.IsScreenPointOverUi(mouseScreen)`. Drives Attack/ADS gating directly on input adapter (`UnityInputAdapter:46-47`).
- `UiPanelHitTest.IsScreenPointOverUi` — central UI Toolkit panel hit test (iterates `UIDocument`s, `panel.Pick()` per pixel). Shared by `AimCursorOverlay` + `InventoryWindow` drop-cancel logic.
- `View/InventoryUI.cs` — Tab state machine: toggles `InventoryWindow.Open/Close()` + sets `player.IsInventoryOpen`. Mutually-exclusive з craft. Detects external close (X button). **Does NOT block gameplay input** — player keeps walking, fires when cursor's off UI.
- `AimCursorOverlay` (legacy): hides crosshair + shows OS cursor on `_pointerOverUi`.
- `CrosshairPresenter` (v2): mirror — hides canvas on `App.Instance.IsPointerOverUi`.
- Same-pixel swap is natural — OS cursor appears at the exact mouse position, no warp.

**Cut from scope** (judged not worth the cost):
- ❌ Fire-on-close 1-frame debounce — playtest didn't flag it as а real problem (Tab toggling has its own slight latency that naturally serves as a debounce in practice).
- ❌ Custom procedural pointer sprite via `Image` child — OS default cursor reads fine для inventory mode; no UI artist + no Quality bar set for custom sprite design.
- ❌ `EnableInventoryPointer` section toggle — no fallback needed.

If/when one of these becomes a real playtest problem (e.g. user accidentally fires LMB right after Tab-close), revisit as а focused polish PR; the infrastructure is ready.

---

### Stage 6 — Polish — ✂️ CUT 2026-05-18 (most items already shipped, low-ammo dropped)

**Original plan** had 4 sub-items:

| Item | Outcome |
|---|---|
| **Bloom** (gap expansion sustained-fire) | ✅ already shipped in Stage 1 — `gap = adsGap + adsBloomExtra` on Firing, decays over `FireInterval` in Cooldown |
| **Reload arc** (unified у same SDF, not separate overlay) | ✅ already shipped in Stage 1 — `_RingFill` shader prop у `CrosshairSDF.shader`, single composite path |
| **Charge arc** (unified for laser) | ✅ already shipped in Stages 1.5+1.7+1.8 — ballistic flame bars + laser segmented ring with `chargeFill` driving fill |
| **Low-ammo pulse** (outline pulses amber as magazine drains) | ❌ **CUT** — user decided не варто, HUD ammo counter sufficient; pulse adds visual noise without clear "I saved the moment" payoff |

**Outcome**: only low-ammo pulse remained as actual new work; user judged it not worth the cost. Stage closed. UX design pass for low-ammo pulse documented above (hybrid threshold, outline color channel, severity-ramped frequency) — captured if someone reopens this in future.

---

### Stage 7 — Legacy cleanup (~30 min)

After all stages validated + locked:
- Delete `Assets/Scripts/View/AimCursorOverlay.cs` + meta
- Remove `gameObject.AddComponent<AimCursorOverlay>()` from `AppBootstrap.cs`
- Remove `UseV2Crosshair` toggle field (always-on)
- Update `docs/ai/crosshair.md`
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

---

## Pickup instructions for next agent

1. Read this doc top-to-bottom (you are here)
2. Verify current state — check if `UseV2Crosshair` exists in `ViewCheatsCrosshairV2Section`. If yes → Stage 1 is done, identify next Stage from git history or section state.
3. If at Stage 1 start — read `AimCursorOverlay.cs` from git history (deleted Stage 7) to understand v1 behavior + `WeaponEntityState.Phase` enum from `weapons.md`.
4. Begin Stage 1 — build SDF shader + presenter + section. Toggle `UseV2Crosshair` for A/B testing during development.
5. After each stage: run EditMode tests, playtest manually, commit.
6. Update this doc's **Status** field at the top + check completed stages.

---

## Status log

| Date | Stage | Notes |
|---|---|---|
| 2026-05-14 | spec | Locked all design decisions. Plan approved. Ready for Stage 1. |
| 2026-05-15 | Stage 1 | Shipped tech foundation + 1:1 IMGUI port. SDF shader (`CrosshairSDF.shader`) + Crosshair.mat + Crosshair.prefab + `CrosshairPresenter.cs` + `ViewCheatsCrosshairV2Section.cs` (`UseV2Crosshair=false` default → toggled on by user). All 10 phase states wired. AimCursorOverlay guards on toggle. 506/506 tests pass. |
| 2026-05-15 | Stage 1.1 (dev tool) | Added **Y hotkey toggle** в `CrosshairPresenter.LateTick` via `Keyboard.current.yKey.wasPressedThisFrame` for A/B compare у Play. Removed at Stage 7. |
| 2026-05-15 | Stage 1.2 (ADS) | Top arm hide on ADS — `_TopArmAlpha` shader prop, binary cutoff via `_adsAmount >= AdsTopArmFadeStart` (default 0.5). Matches v1 legacy behavior. |
| 2026-05-15 | Stage 1.3 (outline) | Black outline ring on all reticle shapes — `_OutlineColor` + `_OutlineWidth` shader props. **Critical fix**: HDR alpha matters — pure 1.0 face white renders gray after URP tonemap; HDR-boost face colors (×2.0) restore visible white. Default outline 1.8px black solid. |
| 2026-05-15 | Stage 1.4 (hit pulse) | Replaced flying X markers (deleted `HitMarkerInstance.cs` + `HitMarker.prefab`) з **EFD-style 4 diagonal stubs spreading outward + alpha fade**. SDF segment math (`sdSegment`) у same shader. Single-slot animation (latest hit restarts). 3-phase envelope: burst → hold → decay з ease-out outward spread (fix from earlier ease-in bug where spread happened during fade). **Per-event-type profiles** (`HitPulseProfile` struct): Normal/Kill/Headshot/Ricochet. Each profile snapshotted at trigger (immune to mid-pulse tweaks). Priority: Ricochet > Kill > Headshot > Normal. Color unpack fix: `e.Damage`=kill, `e.Direction.x`=headshot, `e.MaxHp`=ricochet (HitConfirmed packing ≠ EntityHit packing). |
| 2026-05-15 | Stage 1.5 (charge) | **Replaced** ring-based charge (which collided з reload ring) з **flame-gradient overlay on the 4 arm segments themselves**. Fill grows along arm length from inner edge (`_Gap`) toward outer edge (`_Gap + _LineLength`) as chargeRatio. Color gradient along bar: white (cold, inner) → yellow (mid) → red (hot, outer tip). `_ChargeBarThicknessRatio` (default 0.7) controls if flame inset inside arm or full overlay. **Overheat tremble**: Perlin noise jitter on cursor `_CenterPx` when chargeFill ≥ 0.85, scales linearly з overheat fraction. Default 2.5px @ 35Hz. Industry research validated — Warframe / Destiny 2 both migrated away from ring-on-ring after community complaints. |
| 2026-05-15 | Stage 1.6 (charge curve shape) | **Tunable charge curve via `DevCheatsLaserSection.ChargeRatioPower` (Range 0.1..6, default 1 = linear, backward compat)** — `chargeRatio = Pow(linearT, ChargeRatioPower)`. >1 = ease-in (slow start, fast finish, "build tension"); <1 = ease-out (fast 60-70%, slow trail to max). Drives gameplay damage/burst/spread AND cursor fill in lockstep — both call `LaserConfig.EvaluateChargeRatio` / `DevCheatsLaserSection.EvaluateChargeRatio` (mirror math). Also added `ChargeTimeOverrideSeconds` (Range 0..5, default 0 = use payload asset value) — runtime override of base charge duration without editing per-rarity payload SOs. A4 delivery multiplier (Single/Auto/Scatter) still composes on top. Override-aware `WeaponChargeResolver.GetChargeTime(weapon, deliveryMult, overrideSeconds)` overload. Touched: ShootingSystem, CrosshairPresenter, AimCursorOverlay (legacy now also reads shaped ratio + override → all 3 surfaces sync). |
| 2026-05-15 | Stage 1.7 (per-archetype cursor) | **Laser archetype gets its own cursor** — segmented ring replaces 4-arm + flame bars when `weapon.PayloadDefinition?.Archetype == "Laser"`. 12 slices (tunable 4..24), clockwise fill from 12 o'clock keyed off shaped chargeRatio. Empty silhouette always visible dim (face × `LaserInactiveAlpha`, outline full strength → reads as anchor). Color gradient cold→mid→hot за position у ring → "heat reads radially" even at partial fill. **Implementation: analytical O(1) SDF** (no loop in fragment shader) — `segIdx = floor(ang / segWidth)` directly identifies pixel's slice. Same composite path as flame bars (`dCharge` + `chargeGradient`) — single shader, branch by `_LaserMode > 0.5`. Reload hides ring same as it hides arms (`_LinesHidden` gates laser path too). Tunables: `LaserSegmentCount` / `LaserRingInnerRadius` / `LaserRingOuterRadius` / `LaserSegmentGapDeg` / `LaserInactiveAlpha` у `ViewCheatsCrosshairV2Section`. Reused `ChargeColorCold/Mid/Hot` palette (single source of truth for charge color across both cursor modes). |
| 2026-05-15 | ~~Stage 2~~ CUT | Scouted before implementing — directional recoil kick **already implemented at gameplay layer** through `WeaponEntityState.RecoilOffset` (set by `ShootingSystem`, decayed by `AimingSystem`, fed into `player.WeaponAimPoint` which the cursor follows via `cam.WorldToScreenPoint`). View-only kick would have doubled the shift + desynced from hit detection. Skipped to Stage 3. |
| 2026-05-16 | Stage 3 (focus blur) | `_EdgeSoftness` shader prop, fixed since Stage 1, now **driven by accuracy state** in `CrosshairPresenter`. Sources: recoil pressure (`weapon.RecoilOffset.magnitude / BlurRecoilSaturation`) + ADS deficit (`(1 - AdsBlend) × BlurHipFireAmount`), combined via `max()` (whichever larger drives blur). Maps deficit → `Lerp(BlurMinPx, BlurMaxPx, deficit)`. **Master toggle** `FocusBlurEnabled` (default ON) — OFF falls back to Stage 1 static behavior, no regression. Tunables: `BlurMinPx 0.6` / `BlurMaxPx 3.0` / `BlurRecoilSaturation 0.4` / `BlurRecoilWeight 1.0` / `BlurHipFireAmount 0.3`. Shader untouched — `_EdgeSoftness` already live param across all SDF groups (main + charge + hit pulse). 506/506 tests pass. |
| 2026-05-18 | Stage 1.8 (laser firing animation) | Laser segmented ring was statically dropping to empty on Firing/Cooldown — no animation. Added: (A) **chargeFill cooldown decay** — `_capturedChargeAtFire` snapshot on `WeaponFired` event (chargeRatio packed у `e.Damage`), ring drains `captured × (1 - cooldownT)` over `FireInterval`. Bursting holds at captured (sustained fire, staccato pulse per shot). (B) **Radial pulse** — inner shrinks / outer grows by `LaserFirePulseRadiusPx × _firePulseT` (default 5px). Both decay in lockstep over `cooldownT`. Reset on Ready/Reloading/Charging. **Regression fix same day**: event handler filtered by `e.StringPayload == "Laser"` — ballistic shot was leaking `chargeRatio = 1.0` into shader's flame-bars path, painting yellow segments over 4-arm + masking gap-expansion bloom. Now ballistic ignored entirely. 524/524 tests pass. |
| 2026-05-15 | Architecture refactor (CheatsConfig) | While fixing Stage 3 baseline tests — `DamageSystem` was reading `DevCheats.GodMode` directly (CLAUDE.md §7 violation, broke 2 MeleeAttackTests when user had GodMode ON in playtest). Created `RaidContext.CheatsConfig` struct (extensible — future cheats like `InfiniteStamina` go there too, single plumbing point). `RaidSession.Tick` copies from DevCheats; `TestContextFactory` defaults to `CheatsConfig.Default` (all off). `DamageSystem` now reads `context.CheatsConfig.GodMode`. Resolved 1 of 4 known latent §7 violations. Tests immune to user's playtest GodMode state. |
| 2026-05-18 | ~~Stage 4~~ CUT | Range tier color cut — no `EffectiveRange` concept у weapon model. Would need gameplay-layer EffectiveRange stat + falloff math before cursor could read meaningful data. Premature UX. Revisit if/when range becomes a designer-tuned stat. |
| 2026-05-18 | Stage 5 (UI cursor swap) | Closed via existing infrastructure: `IInputAdapter.InventoryTogglePressed` (Tab), `UiPanelHitTest.IsScreenPointOverUi`, `App.IsPointerOverUi` broadcast + Attack/ADS gating in input adapter, `View/InventoryUI.cs` Tab state machine, `AimCursorOverlay`/`CrosshairPresenter` both hide on `IsPointerOverUi`. OS cursor swap is natural same-pixel (no warp). Cut: fire-on-close debounce + custom procedural pointer sprite + section toggle — not worth build cost vs current feel. |
| 2026-05-18 | ~~Stage 6~~ CUT | Polish stage closed. 3 of 4 items (bloom gap expansion, unified reload arc, unified charge arc) already shipped in Stages 1.x. Remaining 4th item — low-ammo pulse — user decided not worth: HUD ammo counter sufficient, pulse adds visual noise without "I saved the moment" payoff. UX design pass for low-ammo (hybrid threshold, outline pulse channel, severity-ramped freq) documented в Stage 6 section above для potential reopen. |
| 2026-05-18 | Stage 7 (legacy cleanup) — **EPIC CLOSED** | Deleted `Assets/Scripts/View/AimCursorOverlay.cs` + .meta. Created `View/PointerOverUiTracker.cs` (focused MonoBehaviour with only the `_pointerOverUi` + `Cursor.visible` logic — was bundled inside legacy overlay Update()). `AppBootstrap.AddComponent<AimCursorOverlay>()` → `AddComponent<PointerOverUiTracker>()`. Removed `UseV2Crosshair` toggle field + Y hotkey + Stage 1 fallback gate from `CrosshairPresenter`. Updated docstring refs in `IInputAdapter`/`InventoryUI`/`UiPanelHitTest`/`CrosshairPresenter`/`ViewCheatsCrosshairV2Section`. Rewrote `docs/ai/crosshair.md` from legacy IMGUI description to v2 SDF architecture. `gunplay/README.md` Aim Cursor v2 entry → ✅ SHIPPED. 524/524 EditMode tests pass post-cleanup. |
