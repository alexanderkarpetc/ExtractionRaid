# FX Artist Guide — Impact & Armor VFX

## Overview
When a bullet hits a target, the VFX system spawns particles proportionally based on how much
damage armor absorbed. More armor absorption = more sparks, less blood. Full penetration = only blood.

---

## Prefab Locations

All prefabs live in `Assets/Resources/Vfx/Prefabs/Impacts/`:

| Prefab | File | When Used |
|--------|------|-----------|
| **BodyImpact** | `BodyImpact.prefab` | Bullet hits body (flesh damage) |
| **HeadImpact** | `HeadImpact.prefab` | Bullet hits head (flesh damage) |
| **BulletImpact** | `BulletImpact.prefab` | Bullet hits surface (walls, floor). Also fallback for missing prefabs |
| **ArmorImpact** | `ArmorImpact.prefab` | ⭐ NEW — Bullet hits armor (sparks, metal debris) |
| **RicochetSpark** | `RicochetSpark.prefab` | ⭐ NEW — Helmet ricochet (bright spark, bullet deflects) |

---

## When Each FX Fires

### 1. Unarmored Hit (absorption = 0%)
```
Bullet → Unarmored Target
→ BodyImpact or HeadImpact at full scale
→ No ArmorImpact
```
**Look:** Full blood splatter, no sparks.

### 2. Partially Armored Hit (absorption 10-95%)
```
Bullet → Armored Target (armor reduces some damage)
→ BodyImpact/HeadImpact at REDUCED scale (less blood)
→ ArmorImpact at SCALED UP size (more sparks)
→ Both spawn simultaneously at same position
```

**Scaling rules:**
- Blood scale: `1.0 - absorption × 0.7` (at 50% absorption → blood is 65% size)
- Spark scale: `0.3 + absorption × 0.7` (at 50% absorption → sparks are 65% size)

**Look:** Mix of blood and sparks. More armor = more sparks, less blood.

| Absorption | Blood Size | Spark Size | Visual |
|-----------|-----------|-----------|--------|
| 10% | 93% | 37% | Mostly blood, tiny sparks |
| 30% | 79% | 51% | Blood with visible sparks |
| 50% | 65% | 65% | Equal mix |
| 70% | 51% | 79% | Mostly sparks, some blood |
| 90% | 37% | 93% | Almost all sparks |

### 3. Near-Full Block (absorption > 95%)
```
Bullet → Heavy Armor, barely penetrates
→ NO blood (BodyImpact skipped)
→ ArmorImpact only at near-full scale
```
**Look:** Pure sparks/metal, no blood at all.

### 4. Helmet Ricochet (separate event)
```
Bullet → Helmet, pen too low, 40% chance triggers
→ RicochetSpark ONLY (no blood, no ArmorImpact)
→ Bullet physically deflects away
→ Lifetime: 1.5s (shorter than regular impacts)
```
**Look:** Bright, sharp spark flash. One-time burst, not sustained. Should feel like
metal-on-metal deflection. Think: bullet pinging off a hard surface.

### 5. Armor Break (helmet flies off)
```
Armor durability → 0
→ Helmet: unparents from skeleton, gets Rigidbody, flies off with impulse + spin
→ Body armor: ClearArmorModel() — disappears (shatter VFX planned for Iteration 6)
```
**No particle prefab needed for helmet fly-off** — it uses the actual helmet mesh with physics.
Body armor shatter VFX → future `Vfx/Prefabs/ArmorShatter.prefab`.

### 6. Surface Hit (no character)
```
Bullet → Wall / Floor / Object
→ BulletImpact at full scale
```
**Look:** Standard bullet impact (dust, debris). No blood, no sparks.

---

## Art Direction Per Prefab

| Prefab | Color Palette | Shape | Duration | Notes |
|--------|-------------|-------|----------|-------|
| **BodyImpact** | Red/dark red | Splatter outward | 2s | Blood. Bigger = more damage got through |
| **HeadImpact** | Red/dark red, brighter | Splatter + burst | 2s | Same as body but more dramatic |
| **ArmorImpact** | Orange/yellow sparks | Directional sparks | 2s | Metal sparks. Bigger = more absorbed by armor |
| **RicochetSpark** | White/bright blue | Sharp flash + trails | 1.5s | Distinct from ArmorImpact. Brighter, shorter, more focused |
| **BulletImpact** | Gray/brown dust | Puff + debris | 2s | Neutral surface hit |

---

## Scale Reference

All prefabs are spawned at position with `Quaternion.identity`. Scale is modified at runtime:
- Default: `localScale = Vector3.one`
- Scaled by absorption: `localScale *= scaleFactor`
- Scale factor range: 0.3 to 1.0

**Design for scale = 1.0 as maximum.** The system will scale down, never up beyond 1.0.

---

## Testing

On **Shooting Range** scene, Row 9 (z=28) has armored targets:
- **TargetLightArmor** — helmet only → headshots show sparks, bodyshots show blood
- **TargetHeavyArmor** — full armor → both body and head show spark+blood mix
- **TargetGlassCannon** — 50 HP + full armor → armor breaks fast, then pure blood
- **TargetTank** — vest only, no helmet → bodyshots spark, headshots pure blood

Use **Standard ammo** (low pen) for maximum spark visibility.
Use **AP ammo** (high pen) to see more blood (penetrates armor).
