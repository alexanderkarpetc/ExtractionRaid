# Fog of War (FoW) — How It Works

## The Big Picture

FoW gives the player limited vision: they only see what's in their field of view.
Everything outside the FOV is darkened, desaturated and tinted.
The effect is **not** a simple circle — it hugs walls, hides behind corners, and feels like a real "flashlight cone" cast from the player's eyes.

The pipeline has 5 stages that run **every frame**:

```
  Player Position
        |
        v
  1. RAY SWEEP          "What can the player see?"
        |                 Raycasts from player outward
        v
  2. MESH BUILD          "Draw that visibility shape"
        |                 Triangle-fan mesh from ray endpoints
        v
  3. FOV CAMERA          "Render the shape to a texture"
        |                 Separate camera, renders white-on-black
        v
  4. POST-PROCESS        "Blur + smooth + darken the scene"
        |                 Blur → Temporal Blend → Composite
        v
  5. FINAL IMAGE         Dark fog where player can't see
```

---

## Stage 1: Ray Sweep (`FOVRaySweep.cs`)

**Goal:** Build a "visibility polygon" — a list of points that trace the outline of what the player can see.

### How it works

Imagine standing in a room and shining a laser pointer in every direction.
Where the laser hits a wall — that's a visibility boundary.
Where it doesn't hit anything — it reaches max range.

```
                Far radius (30m)
               /     FOV cone     \
              /                     \
         ----+--- wall ---+          \
              \   shadow  |           |
               \          |  Player   |
                \         |  (here)   |
                 Near radius (5m, 360)
```

**Two zones:**
- **FOV cone** (default 120): rays go up to `farRadius` (30m)
- **Behind player** (the other 240): rays only go `nearRadius` (5m) — peripheral awareness

### The two-pass algorithm

**Pass 1 — Coarse sweep:**
- Cast rays every `rayStep` degrees (default 2) in a full 360 circle
- Near the FOV cone edges, use finer steps (half of rayStep) for smoother transitions
- Store each ray's angle, distance, and whether it hit something

**Pass 2 — Edge-finding:**
- Look at every pair of consecutive rays
- If their distances differ by more than 0.5m, there's a shadow edge between them (e.g., a wall corner)
- Binary search (4 iterations) between those two angles to find the exact corner
- Insert those precise edge-points into the polygon

**Why edge-finding?**
Without it, 2 step means a wall corner could be ~2 off from its real position.
When the player rotates, rays shift and the shadow "jumps" between frames. Edge-finding locks the shadow precisely to corners, eliminating jitter.

```
    Without edge-finding:        With edge-finding:

    ray1 ----\                   ray1 ----\
              \  gap             edge1 ----+  (exact corner)
    ray2 ------+---              edge2 ---/
               |  shadow         ray2 ---/+---
               |  "jumps"                |  shadow stays
               |  between frames         |  locked in place
```

**Output:** `List<Vector3>` — endpoints[0] is the player position (fan center), endpoints[1..N] are perimeter points.

### Key constants
| Name | Value | Meaning |
|------|-------|---------|
| `EdgeThreshold` | 0.5m | Min distance difference to trigger edge-finding |
| `BinarySearchIterations` | 4 | Precision: rayStep/16 per iteration |
| `FineEdgeMargin` | 3 | Use finer steps within 3 of FOV cone edge |

---

## Stage 2: Mesh Build (`FOVMeshBuilder.cs`)

**Goal:** Turn the list of points into a triangle mesh so the GPU can render it.

### How it works

The simplest possible mesh: a **triangle fan**.

```
        2---3---4
       / \ / \ / \
      1---0---5---6      0 = player (center)
       \ / \ / \ /       1..N = perimeter points
        N---8---7
```

Each triangle connects: center(0) → point[i] → point[i+1].
The last triangle closes the fan: center(0) → point[N] → point[1].

The mesh lives on a child GameObject of the player, on the **"FOV" layer**.
This layer is special — only the FOV camera renders it.

### Performance notes
- `mesh.MarkDynamic()` — tells Unity this mesh changes every frame, optimizes GPU upload
- Arrays grow but never shrink — avoids GC allocations during gameplay
- World-to-local transform so the mesh moves with the player automatically

---

## Stage 3: FOV Camera (`FogOfWarController.cs`)

**Goal:** Render the visibility mesh into a black-and-white texture (RenderTexture).

### The setup

```
    Main Camera (what player sees)          FOV Camera (hidden, renders FoW data)
    ┌────────────────────────┐              ┌────────────────────────┐
    │  Renders everything    │              │  Only renders "FOV"    │
    │  EXCEPT the FOV layer  │              │  layer (the mesh)      │
    │  (culling mask)        │              │  Black background      │
    │                        │              │  White mesh = visible  │
    └────────────────────────┘              └────────────────────────┘
                                                       |
                                                       v
                                              RenderTexture (_rawRT)
                                              ┌──────────────┐
                                              │ ░░░░████░░░░ │
                                              │ ░░████████░░ │  white = can see
                                              │ ░░░░████░░░░ │  black = can't see
                                              └──────────────┘
```

**Key details:**
- FOV camera copies position, rotation, and projection settings from the main camera every frame
- Both cameras must be perfectly synced so the black/white mask aligns with the scene
- RT resolution is configurable (`FoWRTScale`, default 256) — lower = faster but blurrier edges
- RT aspect ratio matches the screen to prevent stretching

### Three RenderTextures
| Name | Format | Purpose |
|------|--------|---------|
| `_rawRT` | R8, depth=16 | FOV camera target (needs depth for URP rendering) |
| `_rawColorRT` | R8, depth=0 | Color-only copy of `_rawRT` (for RenderGraph import, see DX12 note below) |
| `_blurredRT` | R8, depth=0 | Previous frame's blurred result (for temporal blend) |

`_rawColorRT` and `_blurredRT` are exposed as **global shader textures** so the post-process shaders can access them.

> **Why `_rawColorRT`?** `renderGraph.ImportTexture()` rejects textures that have both color and depth formats. Since the FOV camera needs depth for rendering, we blit to a color-only copy in LateUpdate: `Graphics.Blit(_rawRT, _rawColorRT)`.

---

## Stage 4: Post-Processing (`FogOfWarFeature.cs` + Shaders)

**Goal:** Take the raw black/white mask and turn it into a nice-looking fog effect on the actual scene.

This is a **URP ScriptableRendererFeature** — it injects custom render passes into Unity's rendering pipeline.

### DX12 Compatibility: ImportTexture Pattern

The post-process pass uses `renderGraph.ImportTexture()` to wrap external RTs (`_rawColorRT`, `_blurredRT`) as `TextureHandle`s. This ensures all `cmd.Blit()` calls operate on `TextureHandle↔TextureHandle`.

**Why?** `cmd.Blit(Texture, TextureHandle, Material)` silently fails on DX12 — the source `Texture` is never bound as `_MainTex`. By importing external RTs into the RenderGraph, the API handles resource barriers and binding correctly on all backends (Metal, DX12, Vulkan).

```
Without ImportTexture (broken on DX12):
  cmd.Blit(externalRT, tempHandle, material)  ← _MainTex not bound!

With ImportTexture (works everywhere):
  var handle = renderGraph.ImportTexture(RTHandles.Alloc(externalRT))
  cmd.Blit(handle, tempHandle, material)       ← RenderGraph binds _MainTex correctly
```

RTHandle wrappers are cached (`GetOrCreateRTHandle()`) to avoid GC allocations every frame. They only re-allocate if the underlying RT changes (e.g., resolution change via DevCheats).

### The three-step pipeline

```
   _rawRT (sharp B&W mask)
        |
        v
   ┌─────────────────┐
   │  BLUR            │  FogOfWarBlur.shader
   │  (Gaussian 9-tap)│  Softens the harsh edges
   │  H pass → V pass │  Repeated N times (default 3)
   └────────┬────────┘
            v
   ┌─────────────────┐
   │  TEMPORAL BLEND  │  FogOfWarTemporalBlend.shader
   │                  │  Smooths flickering between frames
   │  lerp(prev,curr) │  Saves result for next frame
   └────────┬────────┘
            v
   ┌─────────────────┐
   │  COMPOSITE       │  FogOfWarComposite.shader
   │                  │  Applies fog to the actual scene:
   │  desaturate      │  1. Desaturate (color → grayscale)
   │  + darken        │  2. Darken toward fog color
   └────────┬────────┘
            v
      Final scene image
```

### 4a. Blur (`FogOfWarBlur.shader`)

**Why blur?** The raw mask has hard pixel edges. Blurring makes the fog boundary soft and natural.

**Separable Gaussian blur** — one of the cheapest ways to blur an image:
1. Blur horizontally (9 pixel samples in a row)
2. Blur vertically (9 pixel samples in a column)
3. Repeat steps 1-2 for `FogBlurIterations` times (default 3)

Each pass uses a weighted average (Gaussian curve) — center pixels matter more, far pixels matter less.

```
   Weights: [0.227, 0.195, 0.122, 0.054, 0.016]
             center ←-------- edges --------→

   Before blur:          After blur:
   ████████░░░░░░░░      ████████▓▓▒▒░░░░
   (sharp edge)          (soft gradient)
```

### 4b. Temporal Blend (`FogOfWarTemporalBlend.shader`)

**Why?** Even with edge-finding, tiny sub-pixel jitter can cause flickering between frames.
Temporal blend smooths this by mixing the current frame with the previous frame.

```
   result = lerp(previousFrame, currentFrame, blendFactor)
```

- `blendFactor = 1.0` → no smoothing (only current frame)
- `blendFactor = 0.05` → heavy smoothing (95% previous frame, 5% current)
- Default: `0.2` (20% current, 80% previous)

The blurred result is saved into `_blurredRT` for use as `previousFrame` next frame.

**Trade-off:** Lower blend = smoother but the fog "lags" behind fast rotation. Default 0.2 is a good balance.

### 4c. Composite (`FogOfWarComposite.shader`)

**The final step:** Apply the fog effect to the scene that the player actually sees.

For each pixel on screen:
1. Read `visibility` from the blurred mask (0 = hidden, 1 = visible)
2. Calculate `fogFactor = (1 - visibility) * fogIntensity`
3. **Desaturate:** blend the color toward grayscale proportionally to fogFactor
4. **Darken:** blend toward the fog color (dark blue-black) proportionally to fogFactor

```
   fogFactor = 0 (fully visible):
   [Original colors, no change]

   fogFactor = 0.5 (partially hidden):
   [Washed out, slightly dark]

   fogFactor = 1.0 (fully hidden):
   [Grayscale, dark fog color overlay]
```

---

## Stage 5: The Result

The player sees a colorful, well-lit area within their FOV.
Everything outside smoothly fades to dark, desaturated fog.
Shadows hug walls precisely. Rotating the camera feels smooth, no flickering.

---

## File Map

```
Assets/
 Scripts/
   View/FogOfWar/
     FOVRaySweep.cs          ← Stage 1: Raycasts + edge-finding
     FOVMeshBuilder.cs       ← Stage 2: Triangle fan mesh
     FogOfWarController.cs   ← Stage 3: FOV camera + RT management + orchestration
     FogOfWarFeature.cs      ← Stage 4: URP render feature (blur→temporal→composite)
 Shaders/
   FogOfWarVisibility.shader ← FOV mesh shader (outputs pure white, FOV layer)
   FogOfWarBlur.shader       ← Stage 4a: Gaussian blur (H + V passes)
   FogOfWarTemporalBlend.shader ← Stage 4b: Frame-to-frame smoothing
   FogOfWarComposite.shader  ← Stage 4c: Apply fog to scene
 Dev/
   DevCheats.cs              ← Runtime-tweakable parameters
 Editor/
   DevCheatsWindow.cs        ← Inspector UI for tweaking
```

---

## DevCheats Parameters

| Parameter | Default | What it does |
|-----------|---------|-------------|
| **FOVEnabled** | true | Master on/off for the entire FOV system |
| **FogOfWarEnabled** | true | On/off for the fog post-processing |
| **FOVNearRadius** | 5 | See-behind-you radius (360, meters) |
| **FOVFarRadius** | 30 | Forward vision radius (FOV cone, meters) |
| **FOVAngle** | 120 | Width of the forward vision cone (degrees) |
| **FOVRayStep** | 2 | Angle between rays (degrees, lower = more precise = slower) |
| **FoWRTScale** | 256 | Visibility texture resolution (pixels wide) |
| **FogBlurRadius** | 1.74 | How far the blur reaches (shader units) |
| **FogBlurIterations** | 3 | How many blur passes (more = softer) |
| **FogIntensity** | 0.4 | How dark the fog gets (0 = invisible, 1 = pitch black) |
| **FogDesaturation** | 0.7 | How much color is removed in fog (0 = full color, 1 = grayscale) |
| **FogColor** | (0.02, 0.02, 0.05) | The color of the fog (dark blue-black) |
| **FogTemporalBlend** | 0.2 | Temporal smoothing strength (1 = off, 0.05 = max smooth) |
| **ForceShowAllBots** | false | Debug: ignore visibility for bots |
| **FOVOcclusionEnabled** | true | Gizmo: show raycast-based occlusion in Editor |

---

## How Data Flows (Simplified)

```
DevCheats (tweakable values)
    |
    ├──→ FOVRaySweep.Sweep()        reads: nearRadius, farRadius, fovAngle, rayStep
    |         |
    |         v
    |    FOVMeshBuilder.RebuildMesh()
    |         |
    |         v
    ├──→ FogOfWarController.LateUpdate()  creates cameras + RTs, reads: FoWRTScale
    |         |
    |         v
    |    Graphics.Blit(_rawRT, _rawColorRT)          strips depth for RenderGraph
    |    Shader.SetGlobalTexture("_FoWVisibility", _rawColorRT)
    |    Shader.SetGlobalTexture("_FoWPrevBlurred", _blurredRT)
    |         |
    |         v
    └──→ FogOfWarFeature (URP pass)  reads: blurRadius, iterations, intensity,
              |                             desaturation, fogColor, temporalBlend
              v
         Blur → Temporal → Composite → Screen
```

---

## Common Questions

**Q: Why a separate camera instead of just drawing fog on the main camera?**
A: The FOV camera renders a clean black-and-white mask at low resolution (256px). This is much cheaper than doing per-pixel raycasts in a full-screen shader, and it separates visibility logic from visual effects.

**Q: Why edge-finding instead of just more rays?**
A: 0.5 ray step without edge-finding needs 720 rays. 2 step with edge-finding uses ~180 base rays + ~20 extra for edges = ~200 total, but with pixel-perfect corners. 3.5x fewer rays for a better result.

**Q: What if I disable both FOVEnabled and FogOfWarEnabled?**
A: `FogOfWarController` sets the global texture to `Texture2D.whiteTexture` (all visible), so the composite shader becomes a no-op. No performance cost from the feature.

**Q: How do I add a new obstacle that blocks vision?**
A: Put a collider on it and make sure its layer is included in `BotConstants.VisionBlockingMask`. The raycasts will automatically stop at it.

**Q: The fog looks laggy when rotating fast — what to tweak?**
A: Increase `FogTemporalBlend` toward 1.0 (less smoothing, more responsive). Trade-off: more flicker at shadow edges.

**Q: The fog edges look too sharp/blocky — what to tweak?**
A: Increase `FogBlurRadius` or `FogBlurIterations`. Or increase `FoWRTScale` for higher resolution mask.

**Q: FoW works on Mac/Metal but the screen is fully dark on Windows/DX12 — why?**
A: Most likely a `cmd.Blit(Texture, TextureHandle)` call where the source is a raw `Texture`/`RenderTexture` instead of a `TextureHandle`. DX12 silently fails to bind `_MainTex` in this case. Solution: import external RTs via `renderGraph.ImportTexture(RTHandles.Alloc(rt))` so all blits are `TextureHandle↔TextureHandle`. Also ensure RTs use `RenderTextureReadWrite.Linear` — Windows may fall back from `R8` to `R8G8B8A8_SRGB` without it.

**Q: Why are `_FoWBlurred` and `_PrevTex` not in the shader Properties blocks?**
A: Per-material `Properties` override global values. Since the feature sets these via `cmd.SetGlobalTexture()` (needed for TextureHandle compatibility), having them in Properties would shadow the global binds. The HLSL `TEXTURE2D()` declarations remain — only the Properties block entries are removed.
