# UI Styling Reference

Single source of truth for UI Toolkit panel sizing and theming. Match these
scales when adding new UI surfaces so the game reads as a consistent family.

Resolution baseline: **1920×1080 (1080p)**, scaleMode `ScaleWithScreenSize`,
match 0.5, screenMatchMode `MatchWidthOrHeight`. All sizes in this doc
assume 1080p reference — UI scales proportionally on 4K (×2.0) and
1366×768 (×0.71) without layout breakage.

---

## Resolution scaling (mandatory baseline)

All player-facing `PanelSettings` MUST use:

```
scaleMode:        ScaleWithScreenSize
referenceResolution: 1920 × 1080
screenMatchMode:  MatchWidthOrHeight
match:            0.5
```

**Why ScaleWithScreenSize, not ConstantPixelSize:**
- `ConstantPixelSize` means 1 CSS px = 1 screen px regardless of resolution.
  At 4K a 1280px modal occupies 33% of width; at 1366×768 it nearly fills it
  and content overflows vertically. No real game uses this for player UI —
  Tarkov, Hunt: Showdown and friends all scale-with-screen.
- `ScaleWithScreenSize` keeps proportions identical across the supported
  resolution range (720p → 4K). Players see the modal occupy the same
  fraction of their screen on any monitor.

**`ConstantPixelSize` is allowed only for:**
- Editor-only tooling (Dev Cheats window, Raid State Debugger)
- Debug overlays not shown to players (HotbarDebugOverlay)

**Auto-provision in editor bootstraps.** Every `PanelSettings` asset created
by an `[InitializeOnLoad]` bootstrap (e.g. `WeaponBuilderAssetsBootstrap`,
`TooltipAssetsBootstrap`) must set the four scale fields above. New
overlays should follow the same pattern so panels stay consistent without
remembering inspector values.

---

## Sizing rules (mandatory for any new modal/panel)

These rules complement scale-with-screen — they protect against layout
breakage when resolution scaling alone isn't enough (e.g. 720p, ultrawide,
content that legitimately exceeds reference height).

1. **Set `min-height` + `flex-shrink: 0` on rows that contain readable
   text** (stat rows, list items). Prevents text overlap when something
   else in the layout misbehaves. Example:
   `.wb-stat-row { min-height: 22px; flex-shrink: 0; }`

2. **Wrap variable-height body content in `ScrollView`.** Modals taller
   than the reference 1080p height should not assume they fit on every
   monitor. Scale-with-screen handles ratio scaling, but a 1500px-tall
   modal at 0.71× still wants ~1066px of vertical space — fine on 1080p,
   tight on 768p. Scroll fallback is cheap insurance.

3. **Cap window max-height to the root visual element's resolved height.**
   `ScaleWithScreenSize` makes the panel proportional, but a modal whose
   natural content height exceeds the 1080 ref-px panel height will still
   overflow vertically — only the *pixel mapping* changes with scale, the
   *layout space* doesn't grow. Pattern (see `WeaponBuilderWindow.UpdateWindowMaxHeight`):

   ```csharp
   _root.RegisterCallback<GeometryChangedEvent>(_ => {
       _window.style.maxHeight = _root.resolvedStyle.height; // panel coords, scale-correct
   });
   ```

   ⚠️ Use `_root.resolvedStyle.height` (panel/reference coords). Do NOT
   use `Screen.height` (actual screen pixels) — that would double-shrink
   on 4K where scale = 2.

4. **Test the modal at 1080p baseline + 4K + 1366×768.** Unity's "16:9
   Aspect" Game-view mode is misleading — it renders at the editor window
   size, which on a typical dev monitor is much taller than 1080p. Layout
   that "looks fine" there may break at fixed resolutions.

5. **Modals taller than ~1100px reference content height are a smell.**
   Split into tabs, scroll regions, or rethink layout — the user's eye
   shouldn't have to scan a 1300px column.

---

## Design tokens (single source of truth)

All canonical sizes and colors live as USS custom properties in:

> **[`Assets/Resources/UI/Theme/_tokens.uss`](../../Assets/Resources/UI/Theme/_tokens.uss)**

Pulled into every runtime PanelSettings via the shared theme:

> **[`Assets/Resources/UI/Crafting/CraftingMockupTheme.tss`](../../Assets/Resources/UI/Crafting/CraftingMockupTheme.tss)**
> *(file path is legacy — repurposed as project-wide theme; preserved to keep
> existing PanelSettings GUID references intact)*

**Rules:**

1. **Don't hardcode `rgb()` or `px` values in panel USS.** Use `var(--token)`.
   Hard-coding silently drifts the design system — every magic number is a
   future merge conflict between panels.
2. **If you need a value that has no token**, add the token to `_tokens.uss`
   first (with a semantic name — `--color-window-bg`, not `--gray-1`), then
   use it. Update the relevant Tier section below.
3. **Two-value shorthand split per axis** (`--space-modal-pad-x`,
   `--space-modal-pad-y`) — avoids USS variable parser edge cases.
4. **Naming:** semantic role > visual hue. The right-hand side may change;
   token names should remain stable.

**Canonical reference panels:**

| Tier | Panel | Files |
|---|---|---|
| A — Modal | Weapon Builder | [`WeaponBuilderWindow.uss`](../../Assets/Resources/UI/WeaponBuilder/WeaponBuilderWindow.uss) |
| B — Compact | Tooltip overlay | [`TooltipOverlay.uss`](../../Assets/Resources/UI/Tooltip/TooltipOverlay.uss) |
| HUD strip | Hotbar overlay | [`HotbarOverlay.uss`](../../Assets/Resources/UI/Hotbar/HotbarOverlay.uss) |
| Full overlay | Death screen | [`DeathScreen.uss`](../../Assets/Resources/UI/Death/DeathScreen.uss) |

When uncertain how to style a new panel: open one of these two and copy the
patterns. They are kept token-clean on purpose.

> Note: `MainMenu.uss`, `NpcDialogue.uss`, `CraftingMockupWindow.uss` are not
> yet token-migrated — they live in their own visual contexts (in-game
> dialogue bubble, mockup, splash menu) and use private color sets. They
> read tokens from the same shared theme but don't currently consume them;
> migrate opportunistically when those panels get their next polish pass.

---

## Scale tiers (semantic guidance)

### Tier A — Modal (Weapon Builder, future inventory rebuild, Crafting)

Use for full-screen modal windows that take user focus.

| Concern | Tokens |
|---|---|
| Window width | `--size-window-width` |
| Window background / border / radius | `--color-window-bg`, `--color-border`, `--border-window`, `--radius-window` |
| Header / footer | `--color-header-bg`, `--space-modal-pad-x`/`-y`, `--color-border-inset` |
| Window title | `--font-window-title`, `--color-text-title` |
| Section heading (column header) | `--font-section-heading`, `--color-section-heading` |
| Inline group label ("Payload"/"Delivery") | `--font-group-label`, `--color-text-muted` |
| Card | `--size-card-width`/`-height`, `--space-card-pad-x`/`-y`, `--color-card-bg`, `--color-card-bg-hover`, `--color-card-bg-selected`, `--color-card-bg-disabled`, `--font-card-title`, `--font-card-kind` |
| Slot | `--size-slot-height`, `--space-slot-pad-x`/`-y`, `--color-state-fill-border`/`-bg` |
| Slot empty placeholder | `--font-empty-placeholder`, `--color-text-empty` |
| Drag-target state | `--color-drag-valid-border`/`-bg`, `--color-drag-invalid-border`/`-bg` |
| Preview archetype label | `--font-preview-archetype`, `--color-accent` |
| Preview flavor / hint | `--font-tooltip-flavor`, `--color-text-flavor` / `--color-hint` |
| Inline stat row | `--space-row-pad-y`, `--size-stat-row-mh`, `--font-stat-label`, `--font-stat-value`, `--color-text-stat-label`, `--color-text-body-strong` |
| Error message | `--color-error` |
| Button (primary "Build") | `--size-button-height`, `--size-button-min-width`, `--font-button`, `--color-btn-build-bg`, `--color-btn-build-bg-hover`, `--color-btn-build-bg-disabled`, `--color-btn-build-border`, `--color-btn-build-border-disabled`, `--color-btn-build-text`, `--color-btn-build-text-disabled` |
| Button (secondary "Cancel") | `--color-btn-cancel-bg`, `--color-btn-cancel-bg-hover`, `--color-btn-cancel-border`, `--color-text-button-cancel` |
| Inset boxes (palette / build panel / preview) | `--color-inset-bg`, `--border-inset`, `--radius-inset` |

### Tier B — Compact panel (Tooltip overlay, popovers)

Use for floating, non-blocking panels.

| Concern | Tokens |
|---|---|
| Card | `--size-tooltip-min-width`, `--size-tooltip-max-width`, `--space-tooltip-pad-x`/`-y`, `--color-inset-bg`, `--color-border`, `--border-tooltip`, `--radius-tooltip` |
| Title | `--font-tooltip-title`, `--color-accent` |
| Subtitle | `--font-tooltip-flavor`, `--color-text-muted` |
| Description (flavor) | `--font-tooltip-flavor`, `--color-text-secondary` |
| Section heading | `--font-tooltip-flavor`, `--color-section-heading` |
| Row | `--space-row-pad-y`, `--font-tooltip-row`, `--color-text-stat-label`, `--color-text-body-strong` |

---

## Panel sort order

Multiple `PanelSettings` assets coexist at runtime. Sorting orders established
so panels stack predictably:

| Panel | Sort order | File |
|---|---|---|
| Hotbar HUD | `50` | `Assets/Resources/UI/Hotbar/HotbarPanelSettings.asset` |
| Crafting mockup | `100` | `Assets/Resources/UI/Crafting/CraftingMockupPanelSettings.asset` |
| Weapon Builder modal | `110` | `Assets/Resources/UI/WeaponBuilder/WeaponBuilderPanelSettings.asset` |
| Death screen | `500` | `Assets/Resources/UI/Death/DeathScreenPanelSettings.asset` |
| Tooltip overlay | `1000` | `Assets/Resources/UI/Tooltip/TooltipPanelSettings.asset` |

Tooltip is always on top — it must float over every other UI Toolkit surface,
including the open modal. Death screen sits above gameplay HUD + modals so it
reads as terminal state. Hotbar sits below modals so opening the Builder
visually covers the HUD strip.

---

## When adding a new UI surface

1. **Read [`_tokens.uss`](../../Assets/Resources/UI/Theme/_tokens.uss) first.** Your USS should consume `var(--token)` references — never hardcode `rgb()` or `px` for shared design concerns (colors, type scale, padding, radius).
2. Pick the closest tier (Modal vs Compact). Do not introduce a third tier.
3. Reuse existing tokens; add a new token only if the existing list genuinely doesn't cover the role. New tokens land in `_tokens.uss` AND get listed in the Tier table above in this doc.
4. Use `picking-mode="Ignore"` on overlay layers that should pass clicks through to underlying UI (tooltips, hint badges, decorative elements).
5. New `PanelSettings` assets should be auto-provisioned by an editor bootstrap (`InitializeOnLoad`) so the runtime never errors on a missing asset. Mirror `WeaponBuilderAssetsBootstrap.cs` / `TooltipAssetsBootstrap.cs`. The shared theme (`CraftingMockupTheme.tss`) is already wired into existing PanelSettings via `themeUss`; new ones should follow the same pattern.
6. If you genuinely need a different size or color, propose updating `_tokens.uss` + this doc rather than diverging silently.

---

## When adding new UI logic

- View MonoBehaviours that need cross-component access follow the
  `WeaponBuilderWindow.Instance` / `TooltipController.Instance` pattern —
  static `Instance` set in `Awake`, cleared in `OnDestroy`. CLAUDE.md §3.12
  ("never add new singletons") covers gameplay state; view-layer service
  locators are tolerated.
- Don't add new accessors to `App.Instance` — view singletons go on the
  view component itself.

---

## Runtime gotchas (learned the hard way)

### 1. Override `PanelSettings` scale fields in code, don't trust the asset

Unity aggressively caches `PanelSettings` assets — editing the YAML doesn't
always propagate to the running editor instance, even after Domain Reload.
Always re-apply the scale config in `BuildDocument` *before* assigning the
panel to the `UIDocument`:

```csharp
var panel = Resources.Load<PanelSettings>(...);
panel.scaleMode          = PanelScaleMode.ScaleWithScreenSize;
panel.referenceResolution = new Vector2Int(1920, 1080);
panel.screenMatchMode     = PanelScreenMatchMode.MatchWidthOrHeight;
panel.match               = 0.5f;

_doc.panelSettings = panel;
```

See `WeaponBuilderWindow.ApplyResponsiveScale` and `TooltipController.BuildDocument`
for live examples.

### 2. `resolvedStyle` is zero while `display: None`

When a `VisualElement` has `display: None`, no layout pass runs and
`resolvedStyle.width / height` return 0. Reading those values inside the
same call frame as `display = Flex` gives stale zeros — the layout hasn't
caught up yet.

Fix: defer the read into `_root.schedule.Execute(..).StartingIn(0)`. By
next-frame layout has run.

```csharp
_root.style.display = DisplayStyle.Flex;
_root.schedule.Execute(() =>
{
    var h = _root.resolvedStyle.height; // now valid
    // ...positioning math here
}).StartingIn(0);
```

This bit us in `TooltipController.Show` — cursor→tooltip offset was wrong
on every non-1080p display because we read `resolvedStyle.height` before
layout, fell back to `Screen.height`, and ended up with `scale = 1`.

---

## Cross-stack coordinates (uGUI ↔ UI Toolkit)

The project mixes uGUI (Inventory, Hotbar) with UI Toolkit (Builder,
Tooltip overlay, future modals). Pointer event coordinate systems differ:

| Stack | Source | Origin | Units |
|---|---|---|---|
| uGUI | `PointerEventData.position`, `Input.mousePosition` | bottom-left | actual screen pixels |
| UI Toolkit | `PointerEnterEvent.position` (and friends) | top-left | panel (reference) pixels under `ScaleWithScreenSize` |

When passing positions between stacks (e.g. uGUI inventory hovers a slot
and asks `TooltipController` to show the tooltip in UI Toolkit space), do
**not** assume `Screen.height` equals `_root.resolvedStyle.height` — under
`ScaleWithScreenSize` they differ by the active scale factor.

Conversion (screen → panel):

```csharp
float panelHeight = _root.resolvedStyle.height;     // panel/ref coords (~1080)
float scale = Screen.height / panelHeight;          // 0.71 / 1.0 / 2.0 / …
Vector2 panelPos = new Vector2(
    screenPos.x / scale,
    panelHeight - screenPos.y / scale);             // also flips Y origin
```

API pattern — give callers the entry point that fits their stack so neither
side has to know the math:

- `Show(model, screenPos)` — uGUI flavour. Input is screen pixels
  (bottom-left). Conversion happens inside.
- `ShowFromPanel(model, panelPos)` — UI Toolkit flavour. Input is panel
  coords (top-left). No conversion.

Implementation in `TooltipController` — both funnel into a single
panel-coord positioning routine.
