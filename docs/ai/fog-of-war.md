# Fog of War

## Pipeline

```text
player origin + aim/config
    → FOV ray sweep
    → visibility mesh
    → FOV camera texture
    → temporal memory + blur
    → fullscreen composite
```

The simulation owns player vision parameters; the view builds/renderers the visibility mask. Fog is
disabled in the hideout.

## Visibility

`FOVRaySweep` samples the configured arc and raycasts against the occluder mask. A second pass around
hit-angle discontinuities resolves obstacle corners. `FOVMeshBuilder` triangulates the ordered sample
points in local XZ space. Avoid per-frame allocations: buffers and mesh data are reused.

Visibility is visual, not an AI oracle. Bot perception performs its own physics/FOV checks.

## Rendering

`FogOfWarController` owns cameras and render textures. `FogOfWarFeature` imports those textures into
URP RenderGraph, applies blur/temporal memory and composites fog over the camera color. The view owns
all Unity resources and releases/recreates them on resolution or lifecycle changes.

### RenderGraph invariant

External `Texture`/`RenderTexture` values must be wrapped in `RTHandle` and imported as
`TextureHandle` before a RenderGraph blit. Raw `cmd.Blit(Texture, TextureHandle)` paths may appear to
work on one backend but fail on DX12. Keep blur/composite resources linear; unintended sRGB fallback
can make the fog disappear or invert.

## Sniper scope

ADS scope reveal is a second screen-space SDF circle centered on `WeaponAimPoint`. Gameplay computes
the reveal strength from ADS blend and aim distance; the composite shader renders that resolved
value. Scope visuals must follow the same smoothed/recoiling weapon aim as the crosshair.

## Debugging

- No mask: verify FOV camera, layer mask and imported texture handles.
- Mask works but composite does not: inspect renderer-feature ordering and camera color handles.
- Flicker/ghosting: verify temporal buffers reset on scene/resolution change.
- Scope offset: compare screen projection of `WeaponAimPoint` with cursor/camera coordinates.
