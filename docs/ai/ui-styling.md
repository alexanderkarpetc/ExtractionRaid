# UI Toolkit Guidelines

## Scaling and sizing

- Runtime panels use `ScaleWithScreenSize` with the shared reference resolution and match policy.
- Apply the project PanelSettings baseline in code when creating a window; serialized assets are not
  trusted to stay synchronized.
- Full-screen roots fill the panel. Modal content uses shared width/height limits and edge padding
  rather than per-window magic numbers.
- Use tokens from `Assets/Resources/UI/Theme/_tokens.uss`; do not copy token values into docs or
  local stylesheets.
- Scroll views need an explicit bounded height/flex relationship or they resolve unpredictably.

## Window lifecycle

Each window owns one root GameObject/UIDocument and exposes a small `Open/Close/IsOpen` surface.
Creation loads UXML/USS/PanelSettings from Resources, attaches the root once and starts hidden.

On open:

1. Validate required session/state.
2. Refresh authoritative data.
3. Show and focus the panel.
4. Gate gameplay input through the shared window policy.

On close, release callbacks/transient drag state and restore input only if no other modal owns it.
Escape closes the topmost eligible window before opening pause. Do not implement independent Escape
logic that competes with `PauseMenuWindow`.

Reload Domain is off: reset static `Instance`/cache/event state on subsystem registration and clear
it on destroy.

## Layering

Sort order is owned by the concrete PanelSettings/assets and registration code. Use semantic tiers:

- world/HUD;
- standard windows;
- modal editors/builders;
- tooltip/drag/notification overlays;
- pause and blocking confirmation.

When adding a panel, inspect existing assets rather than copying a numeric table from documentation.

## Input and coordinates

Views may read UI navigation/escape/pointer input but must route gameplay mutations to systems.
Pointer-over-UI state controls gameplay cursor visibility and aim suppression.

uGUI and UI Toolkit use different scaling/origins. Convert through panel/runtime APIs; do not assume
raw screen pixels equal panel coordinates. Validate drag/drop at non-reference resolutions.

## Runtime gotchas

- `resolvedStyle` is zero while an element is `display: none`; show/measure on a later layout pass.
- Reusing one `PanelSettings` across windows can couple scale and sort behavior; clone/configure when
  isolation is required.
- Register callbacks once and unregister symmetric callbacks on rebuild/destroy.
- UI caches should invalidate from state/version changes, not poll and rebuild every frame.

## Review checklist

- Uses shared tokens and scaling baseline.
- Has bounded modal/scroll sizing.
- Participates in shared input/Escape gating.
- Contains no gameplay rules.
- Handles reload-domain-off lifecycle.
- Works at multiple resolutions and panel scales.
