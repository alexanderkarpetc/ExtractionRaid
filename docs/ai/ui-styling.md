# UI Styling Reference

Single source of truth for UI Toolkit panel sizing and theming. Match these
scales when adding new UI surfaces so the game reads as a consistent family.

Resolution baseline: **1920×1080 (1080p)**, scaleMode `ConstantPixelSize`,
referenceDpi 96.

---

## Scale tiers

The project uses two tiers for runtime UI Toolkit panels.

### Tier A — Modal (Weapon Builder, future inventory rebuild, Crafting)

Reference file: `Assets/Resources/UI/WeaponBuilder/WeaponBuilderWindow.{uxml,uss}`.

| Element | Size |
|---|---|
| Window width | `1040px` |
| Header padding | `24px 32px` |
| Body padding | `32px` |
| Window title (header) | `32px` bold |
| Section title (e.g. archetype label) | `28px` bold |
| Row label / dropdown text / button label | `22px` |
| Inline stat label | `20px` |
| Inline stat value | `20px` bold |
| Hint / italic note | `18px` italic |
| Button height | `56px`, min-width `200px` |
| Border radius | `8–12px` |
| Border width | `2px` (window) / `1px` (inset boxes) |

### Tier B — Compact panel (Tooltip overlay, popovers)

Reference file: `Assets/Resources/UI/Tooltip/TooltipOverlay.{uxml,uss}`.

| Element | Size |
|---|---|
| Card padding | `20px 24px` |
| Card min-width | `320px` |
| Card max-width | `540px` |
| Title | `28px` bold accent |
| Subtitle | `18px` muted |
| Section heading | `16px` bold |
| Row label | `20px` |
| Row value | `20px` bold |
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
