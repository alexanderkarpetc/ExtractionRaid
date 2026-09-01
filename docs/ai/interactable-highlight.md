# Interactable Highlight

## Stack

- `InteractableOutlineTarget` registers eligible renderers and drives per-object state.
- `InteractableOutlineRegistry` is a view-layer registry consumed by the renderer feature.
- `InteractableOutlineFeature` renders mask and composite passes.
- `MaterialPropertyTweener` optionally animates material properties through
  `MaterialPropertyBlock`.

The renderer is presentation-only, but current `InteractableOutlineTarget.ShouldBeVisible` still
computes proximity in View from `App.Instance` and its activation radius. Moving that rule to
resolved gameplay state is tracked in `tasks.md`.

## Lifecycle

Targets register on enable and unregister on disable/destroy. Snapshot creation prunes destroyed or
inactive renderers. The static registry currently lacks the required subsystem-registration reset;
that Reload-Domain-off gap is tracked in `tasks.md`.

## Setup

1. Add `InteractableOutlineTarget` to the visual root.
2. Let it discover child renderers or provide an explicit renderer set.
3. Ensure the active URP renderer includes `InteractableOutlineFeature`.
4. Use the tween component only when emission/material feedback is also required.

Never clone materials per hover. Use property blocks so shared materials remain shared and authored
assets are not dirtied.

## Trade-offs

The outline mask is screen-space and renderer-based: overlapping highlighted objects merge, and
transparent/custom shader objects may need explicit pass handling. Per-object opacity requires
separate mask information; do not add that complexity without a concrete visual requirement.
