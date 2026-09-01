# Impact and Armor FX Guide

Audience: artists authoring hit, armor and break effects. Gameplay selects the event/result; FX must
not infer penetration from particle parameters.

## Event language

| Result | Visual direction |
|---|---|
| Flesh hit | Directional blood core, brief mist and restrained wound flash. |
| Partial armor absorption | Mix blood with sparks proportional to resolved absorption. |
| Heavy absorption | Sparks/metal debris dominate; blood remains only for health damage that passed. |
| Helmet ricochet | Sharp deflection burst aligned to reflected direction; distinct from absorption. |
| Armor break | One readable break event; helmet may detach physically. |
| World surface | Material-appropriate impact/decal without character blood. |

## Authoring rules

- Keep scale readable from the top-down camera; avoid effects that cover the target silhouette.
- Use event direction/normal supplied by the presenter.
- Prefer short bright cores plus slower low-opacity debris over large opaque clouds.
- Blood and decals should preserve combat history without unlimited emitters/decals.
- Prefabs/materials belong to view resources and must be pool-safe.
- Do not encode gameplay thresholds or damage formulas in VFX graphs.

## Validation

Test unarmored, partial absorption, heavy absorption, ricochet, break and one-shot death at typical
camera distance. Verify pooled replay, ragdoll transition, multiple simultaneous hits and color under
the active post-processing profile. Use DevCheats only to produce known resolved outcomes.
