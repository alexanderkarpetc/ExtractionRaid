# Battle HUD — Status Doc

> Living spec + plan for the Battle HUD pass started 2026-05-21.
> **Status**: design locked, pre-implementation. Tier 1 implementation pending.
> Pickup if context resets — read this doc top-to-bottom.

---

## Vision

Replace debug-only armor/helmet overlays (which currently render on top of UI + hotbar) with a coherent battle HUD that closes the ZERO-Sievert-style gap. Restrained-tactical aesthetic (Destiny 2 / Escape From Duckov), NOT power-fantasy juice.

**Anti-patterns to avoid** (per design research 2026-05-21):
- Tarkov-style numeric clutter (we lean Hunt: hide-by-default)
- Information that duplicates the worldspace HP bar (the camera already shows the player)
- Center-of-screen UI blocking the cursor area
- More than 4 saturated colors on persistent HUD

---

## Locked decisions (all approved by user 2026-05-21)

| # | Decision | Notes |
|---|---|---|
| 1 | **No separate HP bar on HUD** | Worldspace `WorldHealthBar` (existing, over player head) carries HP. No duplication in TL corner. |
| 2 | ~~**Armor: procedural paper-doll**~~ — ✂️ CUT 2026-05-21 after Stage 2a visual test. Did not read well at HUD size in restrained-tactical tone. Armor stays on existing `WorldHealthBar` armor stripe (by-analogy-with-HP), shipped earlier. |
| 3 | **Status effects HUD row** — horizontal, right of paper-doll | World of Warcraft debuff-row reference. Hover → tooltip. Procedural SDF icons. |
| 3b | **Status effects worldspace** — universal pattern (player + all bots) | Mini-icons row UNDER the existing `WorldHealthBar`. No tooltips (peripheral signal only). Lives on the same MonoBehaviour as the HP bar so all chars get it. |
| 4 | **Stamina: radial ring** in worldspace, offset to the side of the player (revised 2026-05-26 from "under feet" — offset is a tunable `Vector3`, default left). Zelda BotW reference. Spring-follow ("rubber-band" lag while sprinting). Gray track + green→orange→red fill gradient by ratio. Fade out when full after a delay (only visible while depleting/regenerating). **Logic too:** exhaustion hysteresis — empty stamina locks sprint until it recovers past a configurable threshold (default 10%); ring blinks while locked. |
| 5 | **Hotbar weapon slots** — distinct visual treatment | Slots 1-2 (weapons) rendered with warm bg tint + "1/2" key, separator gap before slots 3-9 (consumables). UI Toolkit, extends `HotbarOverlay`. **NOTE (2026-05-26):** weapons are NOT "bound" like quick slots — no picker. Interaction = **click to equip/holster** (writes `PendingHotbarSlot`, mirrors keys 1-2) + **drag weapon↔weapon to swap** (`HotbarWeaponSystem.SwapWeaponSlots`). No icons in project → tint + name only. |
| 6 | **Minimap** — out of scope (already exists as separate feature) | Don't touch. |
| 7 | **Survival meters** (hydration/energy) — skip | Re-evaluate when survival design pass starts. |
| 8 | **Bone/limb HP** — skip | Global HP only via worldspace bar. Tarkov's 7-bone too sim for our tone. |
| 9 | **In-raid loot value tally** — skip | Universal genre anti-pattern — kills "should I push or extract" tension. |

---

## Layout sketch

```
┌─────────────────────────────────────────────────────────┐
│  ⬡  🩸 💔 🤕                       [MINIMAP existing]    │  ← TL: paper-doll
│  ⬢                                                       │   + WoW-style debuff row (hover tooltips)
│                                                          │
│                                                          │
│                                                          │
│                 [CROSSHAIR + VFX existing]               │
│                 [HUD damage vignette existing]           │
│                                                          │
│                   👤 player sprite                       │
│                   🩸💔  ← status mini-icons (worldspace) │
│                   ━━━━░  ← HP bar (existing)             │
│                     ⊙   ← stamina radial (under feet)    │
│                                                          │
│                                                          │
│       [⚔1][⚔2] | [3][4][5][6][7][8][9]                   │
│        weapons    consumables           AMMO 30/120 🔫    │
└─────────────────────────────────────────────────────────┘
```

- **TL**: armor paper-doll + horizontal status row
- **TR**: minimap (existing, untouched)
- **BC**: hotbar з visually-distinct weapon slots
- **BR**: ammo + weapon icon
- **Worldspace cluster**: HP bar + status mini-icons under it (universal — all chars) + radial stamina under feet (player only)

---

## Status effect catalog (initial)

Current gameplay statuses (from `StatusEffectType` enum, `ArmorSystem` doc):

| Status | Icon (procedural SDF) | Tooltip text |
|---|---|---|
| Bleeding L1 | single drop shape | "Light bleed — −1 HP/sec for Xs. Bandage to stop." |
| Bleeding L2 | double drop or larger | "Heavy bleed — −3 HP/sec for Xs. Bandage to stop." |
| Fracture *(if/when added)* | cross / break shape | "Fracture — sprint disabled. Splint to heal." |
| Pain *(deferred)* | spiral shape | "Pain — handshake. Painkiller to alleviate." |

Procedural SDF icons match cursor v2 / HUD damage feedback shader aesthetic — no sprite assets needed. Icon builder pattern (`StatusEffectIconBuilder`) lets new statuses register icon + tooltip in code without authoring.

---

## Universal worldspace status row

**Reference**: WoW-style debuff icons over enemy frames + Tarkov over-character indicators.

`WorldHealthBar.cs` (existing MonoBehaviour on player + bots) extends to render a small horizontal icon row under the HP bar:
- Same shader/icon builder as HUD row.
- ~14px size (vs ~36px on HUD).
- No tooltips — peripheral signal only ("that bot is bleeding").
- Auto-hide row when no active statuses.
- Driven by `StatusEffectSystem` state read each frame.

Bonus: gameplay reads more legibly — player sees a bleeding bot и understands "I just need to wait it out" without diving menus.

---

## Hotbar weapon slots

`HotbarOverlay.uxml` currently spawns 7 slots in a row (`QuickSlotCount=7`, keys 3-9). Weapon slots 1-2 (`HotbarSize=2`) exist only in state — not rendered.

**Extension**:
- Add 2 weapon slots prepended to the strip
- Different USS class (e.g. `.hb-slot--weapon`) — distinct background tint, larger size, weapon icon watermark
- Inserted gap (separator) between weapon strip (1-2) and consumable strip (3-9)
- Click/drag/bind UX matches existing slot pattern (`InventorySlotElement` reuse)
- Highlight active weapon slot when `player.SelectedHotbarSlot == i`

---

## Implementation plan — Tier 1

### Files (new)

| File | Role |
|---|---|
| `Assets/Scripts/View/BattleHudPresenter.cs` | Plain class у App. LateTick. Drives TL HUD (paper-doll + status row + tooltips). |
| `Assets/Shaders/ArmorPaperDoll.shader` | Procedural SDF — char outline + 2 region tints (helmet + body durability). |
| `Assets/Resources/Vfx/Materials/ArmorPaperDoll.mat` | Material. |
| `Assets/Resources/Vfx/Prefabs/UI/BattleHud.prefab` | Canvas (Screen-Space Overlay, sortOrder 850 — under HudDamage 900) + paper-doll RawImage + status row container. |
| `Assets/Shaders/StatusEffectIcon.shader` | Procedural SDF — one icon style (input `_IconShape` int: 0=Bleed L1, 1=Bleed L2, 2=Fracture …). Reused for HUD + worldspace. |
| `Assets/Scripts/View/UI/StatusEffectIconBuilder.cs` | Helper: status type → (icon shape int, tooltip text). Single source of truth. |
| `Assets/Scripts/View/WorldStatusIcons.cs` | MonoBehaviour on `CharacterBody`. Worldspace icon row under HP bar. Universal (player + bots). |
| `Assets/Scripts/View/WorldStaminaRing.cs` | MonoBehaviour on player only. `Image.fillMethod = Radial360` driven by player.Stamina ratio. Fades when ≥ threshold. |
| `Assets/Scripts/Dev/Sections/ViewCheatsBattleHudSection.cs` | Tunables. |

### Files (touch)

| File | What |
|---|---|
| `Assets/Resources/UI/Hotbar/HotbarOverlay.uxml` + `.uss` | Add weapon-slot category styles. |
| `Assets/Scripts/View/UI/HotBar/HotbarOverlay.cs` | Render 2 weapon slots before strip; bind to `player.Hotbar[0..1]`; visual highlight active. |
| `Assets/Scripts/ApplicationCore/App.cs` | Wire `BattleHudPresenter` (ctor + LateTick + Dispose). |
| `Assets/Scripts/Dev/ViewCheatsConfig.cs` | Accessor `BattleHud`. |
| `Assets/Scripts/Editor/DevCheatsWindow.cs` | DrawSection + CreateSectionIfMissing. |
| `Assets/Resources/Vfx/Prefabs/Characters/...` (or wherever WorldHealthBar lives) | Add `WorldStatusIcons` component sibling on character prefab. |
| Player.prefab | Add `WorldStaminaRing` component (player-only). |

### Tunables (`ViewCheatsBattleHudSection`)

```csharp
[Header("Battle HUD master")]
public bool Enabled = true;

[Header("Armor paper-doll (TL corner)")]
public Vector2 PaperDollPosition = new Vector2(40, -40);
[Range(0.5f, 3f)] public float PaperDollScale = 1.0f;
public Color ArmorFullColor = new Color(0.85f, 0.9f, 1f, 1f);
public Color ArmorCrackedColor = new Color(1f, 0.7f, 0.2f, 1f);
public Color ArmorBrokenColor = new Color(0.4f, 0.4f, 0.45f, 0.6f);
[Range(0f, 1f)] public float CrackedThreshold = 0.6f;
[Range(0f, 0.3f)] public float BrokenThreshold = 0.05f;

[Header("Status row (HUD, with tooltips)")]
[Range(20f, 64f)] public float StatusIconSize = 36f;
[Range(2f, 12f)] public float StatusIconGap = 6f;

[Header("Worldspace status icons (universal — player + bots)")]
[Range(8f, 24f)] public float WorldStatusIconSize = 14f;
[Range(0f, 1.5f)] public float WorldStatusYOffset = 0.0f;  // negative = under HP bar
[Range(2f, 8f)] public float WorldStatusIconGap = 3f;

[Header("Stamina radial ring (worldspace, under feet)")]
public Color StaminaFullColor = new Color(0.5f, 1f, 0.5f, 0.85f);
public Color StaminaLowColor = new Color(1f, 0.7f, 0.2f, 1f);
[Range(0.3f, 2f)] public float StaminaRingRadiusWorld = 0.7f;
[Range(0.5f, 6f)] public float StaminaRingThicknessPx = 3f;
[Range(0.5f, 1f)] public float StaminaHideThreshold = 0.98f;
[Range(0.05f, 0.5f)] public float StaminaFadeTime = 0.2f;

[Header("Hotbar weapon slots")]
[Range(0f, 40f)] public float HotbarWeaponSeparatorPx = 18f;
public Color WeaponSlotBgTint = new Color(0.7f, 0.5f, 0.3f, 0.5f);
public Color ConsumableSlotBgTint = new Color(0.3f, 0.4f, 0.5f, 0.5f);
```

### Implementation order

| Step | What | ~Time |
|---|---|---|
| 1 | Section + asset auto-create | 15 min |
| 2 | `StatusEffectIconBuilder` + `StatusEffectIcon.shader` (1 icon shape stub) | 1h |
| 3 | `BattleHudPresenter` skeleton + wiring у App | 20 min |
| 4 | `ArmorPaperDoll.shader` + material + prefab | 1h |
| 5 | Status row у HUD prefab + hover-tooltip pattern | 1h |
| 6 | `WorldStaminaRing` (UI Image radial fill, no shader needed) | 30 min |
| 7 | `WorldStatusIcons` (sibling component on CharacterBody — universal) | 45 min |
| 8 | Hotbar weapon slots — UXML/USS extend + HotbarOverlay.cs render | 1.5h |
| 9 | Playtest у `feedback_range` — tune defaults | 30 min |
| **Total** | | **~7h** |

### Validation strategy

1. EditMode tests 524/524 (view-only changes, gameplay untouched)
2. Manual playtest `feedback_range`:
   - Take hits → armor regions colorize → cracked → broken (helmet fly-off existing)
   - Bleeding L1/L2 applied → icon appears у HUD row + worldspace mini-row over player
   - Other bots з bleeding → also show icon row (universal pattern)
   - Sprint → stamina ring appears under feet, fades back when full
   - Hotbar shows weapon slots 1-2 distinct, slots 3-9 unchanged
3. Hover tooltip — HUD status icon → hover → text appears, doesn't block gameplay

### Status log

| Date | Item | Notes |
|---|---|---|
| 2026-05-21 | spec | Locked design decisions (paper-doll / procedural icons / radial stamina / worldspace status row universal / weapon-slot hotbar redesign). Plan approved. Ready for implementation. |
| 2026-05-21 | Stage 1 — Foundation | Shipped: `ViewCheatsBattleHudSection` + asset, `BattleHudPresenter` skeleton wired у App, empty `BattleHud.prefab` (Canvas Screen-Space Overlay, sortOrder 850, Scale-With-Screen-Size ref 1920×1080). DevCheats `⚔ Battle HUD` section visible. 525/525 tests. |
| 2026-05-21 | ~~Stage 2 — Armor paper-doll~~ | Tried Stage 2a (visual exploration via procedural SDF — humanoid silhouette з helmet/visor/shoulder/chest/diamond shapes, BL anchor, scale-with-screen). Form did not read well at HUD scale + did not match restrained-tactical tone. Reverted: shader/material/section tunables/prefab child deleted. Armor coverage stays on existing `WorldHealthBar` armor stripe (shipped earlier). Presenter kept as skeleton для Stage 3+. |
| 2026-05-26 | Stage 3 — Status row + tooltips | Shipped: UI Toolkit `BattleHudOverlay` (UXML/USS + `BattleHudPanelSettings`, sortOrder 60), status tiles synced per `StatusEffectVisualMap.KeyFor`. Reuses existing `TooltipController.ShowFromPanel` (no parallel tooltip system) + new `StatusEffectTooltipBuilder`. TR-corner anchor via `ApplyCornerAnchor` (inline edges, tunable corner). Tooltip auto-flip fixed via `GeometryChangedEvent` re-position. Cursor-flicker feedback loop fixed (removed IsPointerOverUi self-hide). Icon +70%, container +15%. Legacy IMGUI `StatusEffectOverlay`/`DefenderArmorHUD`/`StaminaBarOverlay` deleted. |
| 2026-05-26 | Stage 4 — Worldspace status mini-icons (universal) | Shipped: `WorldStatusIcons` (non-tooltip peripheral row) parented to `WorldHealthBar`, player + all bots. Procedural SDF `StatusEffectIcon.shader` (blood-drop shape, red-on-red palette matching HUD via `FgColorFor`). Canvas.enabled toggle (not SetActive — keeps LateUpdate alive). Left-aligned, reads `HBarWidth`. HP bar lifted (`HBarOffsetY` 2.2→2.4). |
| 2026-05-26 | Bleed mechanism polish | Baseline `BleedChance = 0.05` on all ammo defs (was 0 on standard → bleed never fired with cheat loadout). Bot weapons read ammo bleed too (parity, `BotCombatSystem`). GodMode now LETS bleed apply on player (icons show) but zeroes tick damage + popups; new `IgnoreBleed` cheat (Cheats section) = hard-off for both apply + damage. +tests: `Ammo_HasBleedChance` updated, `AmmoRifleHP_HasHigherBleedThanStandard` added. |
| 2026-05-26 | Stage 5 — Worldspace radial stamina ring + exhaustion gate | Shipped. **Logic:** `IsExhausted` hysteresis on `PlayerEntityState` (empty → locks sprint until recover ≥ `ExhaustionRecoveryRatio`, default 10%). Full stamina config migration → `DevCheatsStaminaSection` + `StaminaConfig` in context (`StaminaSystem`+`MovementSystem` read context, no more StaminaConstants direct). 6 hysteresis tests. **View:** `StaminaRing.shader` (annular SDF, fill-by-angle clockwise from 12 o'clock, 3-stop green→orange→red tint, blink = gentle opacity pulse to `BlinkMinAlpha`, ZTest Always = draws over geometry, outline + HDR `FillIntensity`), `StaminaRing.mat` in Resources. `WorldStaminaRing` — NON-parented (free world pos for spring `SmoothDamp` follow toward playerPos+offset), billboard, self-destroys on player despawn, hide-when-full after delay (starts hidden via `_fullTimer` seed). Tunables in `ViewCheatsBattleHudSection`. 532/532 tests. **Shader gotcha:** `const float` locals fail HLSL→Metal ("unexpected float constant") — use `#define`. |
| 2026-05-26 | Competitor HUD gap analysis | Post-epic recon vs ZERO Sievert / Escape from Duckov. Confirmed missing genre-standard combat-HUD elements: **ammo counter** (HIGH — flagged by user, → Stage 7) + generic interaction prompt (still open). Survival meters intentionally skipped (decision #7); fire-mode indicator + reload progress evaluated and **dropped** (not wanted). Ammo data already available: `WeaponEntityState.AmmoInMagazine` + `AmmoSystem.CountReserve`. |
| 2026-05-26 | Stage 7 — Ammo counter | Shipped. Bottom-right block in `BattleHudOverlay`: big mag count / reserve / ammo-type name. Reads `EquippedWeapon.AmmoInMagazine` + `AmmoSystem.CountReserve(inventory, AmmoType)` + `ItemDefinition.DisplayName`. Low-mag (≤ `AmmoLowThreshold`, default 25%) → gold; empty → red (USS class toggle). Hidden when holstered / weapon has no AmmoType. Tunables: `AmmoEnabled/Corner/Offset/LowThreshold`. Pure view. **Anti-jitter:** mag (`width:90px`, right-align) + reserve (`width:64px`, left-align) have fixed-width boxes so the row width is constant regardless of digit count — otherwise the variable-width mag reflowed the row and the block jittered every shot. |
| 2026-05-26 | Stage 6 — Hotbar weapon slots | Shipped. **Logic:** `HotbarWeaponSystem.SwapWeaponSlots` (static command from overlay) swaps `WeaponSlots[]` + `Hotbar[]` together (preserves mag/heat — no WeaponSyncSystem rebuild) + remaps Selected/Pending (selection follows weapon; holster -1 stays). 7 tests. **View:** `HotbarOverlay` — `weapon-strip` (2 slots, keys 1/2, warm tint) + separator + existing quick strip, combined-center bar. Click = equip/holster (writes `PendingHotbarSlot`, mirrors keys), drag weapon↔weapon = swap. NO bind/picker (weapons aren't bound). Tint+name (no icons). Tunables: separator px + weapon/active/consumable tints. 539/539 tests. |

---

## Related docs

- [`README.md`](README.md) — gunplay shipped state
- [`../crosshair.md`](../crosshair.md) — cursor v2 (recently shipped, similar SDF tech stack)
- [`../armor-system.md`](../armor-system.md) — armor model + status effects (Bleeding L1/L2)
- [`../battle-design-status.md`](../battle-design-status.md) — armor + bleeding mechanics
