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

## Scale tiers

The project uses two tiers for runtime UI Toolkit panels.

### Tier A — Modal (Weapon Builder, future inventory rebuild, Crafting)

Reference file: `Assets/Resources/UI/WeaponBuilder/WeaponBuilderWindow.{uxml,uss}`.

| Element | Size |
|---|---|
| Window width | `1280px` |
| Header padding | `24px 32px` |
| Body padding | `24px 32px` |
| Window title (header) | `26px` bold |
| Section heading (column header — "AVAILABLE MODULES") | `18px` bold |
| Inline group label (e.g. "Payload"/"Delivery") | `14px` |
| Card | `144×80`, padding `10/12`, title `18px` bold, kind `14px` |
| Slot | height `72px`, padding `12/16`, title `18px` bold, kind `14px`, empty placeholder `16px` italic |
| Preview archetype label | `22px` bold |
| Preview flavor / hint | `14px` italic |
| Preview stat group heading | `14px` bold |
| Inline stat label | `16px` |
| Inline stat value | `16px` bold |
| Inline stat row padding | `2px 0`, min-height `22px` |
| Backpack tile | `130×72`, padding `8/10`, name `14px` bold, count `12px` |
| Button | height `44px`, min-width `160px`, font `18px` bold |
| Border radius | `8–12px` |
| Border width | `2px` (window) / `1px` (inset boxes) |

### Tier B — Compact panel (Tooltip overlay, popovers)

Reference file: `Assets/Resources/UI/Tooltip/TooltipOverlay.{uxml,uss}`.

| Element | Size |
|---|---|
| Card padding | `16px 20px` |
| Card min-width | `280px` |
| Card max-width | `460px` |
| Title | `20px` bold accent |
| Subtitle | `14px` muted |
| Description (flavor) | `14px` italic |
| Section heading | `14px` bold |
| Row label | `15px` |
| Row value | `15px` bold |
| Row padding | `2px 0` |
| Border radius | `8px` |
| Border width | `2px` |

---

## Color palette (UI Toolkit panels)

| Role | Color |
|---|---|
| Window background | `rgb(26, 28, 34)` |
| Header / footer background | `rgb(30, 33, 40)` |
| Inset / preview background | `rgb(22, 24, 30)` |
| Border | `rgb(70, 75, 85)` |
| Sub-border / inset border | `rgb(50, 55, 65)` |
| Title text | `rgb(235, 240, 250)` |
| Body text | `rgb(210, 215, 225)` |
| Muted text | `rgb(150, 155, 165)` |
| Accent (gold — titles) | `rgb(230, 200, 140)` |
| Hint / energy blue | `rgb(120, 195, 235)` |
| Section heading blue | `rgb(140, 175, 210)` |
| Action button (Build) | `rgb(60, 130, 90)` bg / `rgb(80, 160, 110)` border |
| Cancel button | `rgb(55, 60, 70)` bg / `rgb(70, 75, 85)` border |

---

## Panel sort order

Multiple `PanelSettings` assets coexist at runtime. Sorting orders established
so panels stack predictably:

| Panel | Sort order | File |
|---|---|---|
| Crafting mockup | `100` | `Assets/Resources/UI/Crafting/CraftingMockupPanelSettings.asset` |
| Weapon Builder modal | `110` | `Assets/Resources/UI/WeaponBuilder/WeaponBuilderPanelSettings.asset` |
| Tooltip overlay | `1000` | `Assets/Resources/UI/Tooltip/TooltipPanelSettings.asset` |

Tooltip is always on top — it must float over every other UI Toolkit surface,
including the open modal.

---

## When adding a new UI surface

1. Pick the closest tier (Modal vs Compact). Do not introduce a third tier.
2. Reuse the color palette above; add a new role only if the existing list
   genuinely doesn't cover it.
3. Use `picking-mode="Ignore"` on overlay layers that should pass clicks
   through to underlying UI (tooltips, hint badges, decorative elements).
4. New `PanelSettings` assets should be auto-provisioned by an editor
   bootstrap (`InitializeOnLoad`) so the runtime never errors on a missing
   asset. Mirror `WeaponBuilderAssetsBootstrap.cs` / `TooltipAssetsBootstrap.cs`.
5. If you genuinely need a different size, propose updating this doc rather
   than diverging silently.

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
