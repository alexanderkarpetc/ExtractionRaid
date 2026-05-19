# Archetype Differentiation — Session Plan

> Living doc for the "Maximal Archetype Differentiation" backlog item (from [`README.md`](README.md)).
> Goal: each of 6 archetypes (Ballistic + Laser × Pistol/Rifle/Shotgun) must feel mechanically distinct, not just stat reskin. Blindfolded test — player names the archetype from feel alone.

---

## Current state (2026-05-13 recon)

Strong differentiators today:
- **Delivery pattern** (Single semi / Auto hold / Scatter 7-pellet) — biggest divider.
- **Recoil magnitude** (F/S kick per delivery; Scatter has 6.0 side kick).
- **Charge mechanic** (Laser payload only).
- **Laser+Auto burst** — sole cross-product mechanic today (1–6 shots scaled by chargeRatio).
- **Beam flash VFX** (Laser only).
- **Casing + magazine drop** (Ballistic only).

Bland dimensions (shared across all 6):
- Hit pause + camera shake (one global config).
- Impact VFX (same blood/spark on all hits).
- Recoil **shape** (magnitude only, no direction curve).
- Reload feel (full-mag for all).
- Audio (no per-archetype layer at all).
- Beam flash equal across Laser pistol/rifle/shotgun (modulo pellet count).
- Charge curve identical across Laser pistol/rifle/shotgun.

---

## Plan

### Track A — View polish (concrete tunable work)

| # | Item | Scope | Status |
|---|---|---|---|
| A1 | Per-archetype camera shake profiles | 3 delivery shapes × 2 payload modifiers → 6 effective combos. World-space `KickDirOffset` shapes recoil direction (pistol snap-up, rifle climb, shotgun lateral shove). Payload modifies kick scale + tremor frequency (ballistic sharp/30Hz, laser smooth/18Hz). | ✅ shipped 2026-05-13 |
| A2 | Per-payload impact VFX signature | Laser branch у `ProjectilePresenter` swaps to programmatic `LaserBodyImpact` / `LaserHeadImpact` prefabs (flash + smoke + embers, created via MCP). `BloodDecalPresenter` suppresses blood pool на laser hits. `CharacterHitFx` rim flash blends toward warm orange. Wall decals untouched (deferred). | ✅ shipped 2026-05-14 |
| ~~A3~~ | ~~Per-delivery recoil shape~~ | **Cut 2026-05-14** — camera shake (A1) + crosshair bloom уже передають recoil feel достатньо; weapon-mesh kick direction would add complexity for marginal feel gain at top-down view distance. Don't revisit unless camera/aim model changes. | — |
| A4 | Per-archetype charge curve (Laser) | Per-delivery multiplier (Pistol 0.6, Rifle 1.0, Shotgun 1.5) on payload ChargeTime via `LaserConfig.ChargeTimeMultiplierFor(pattern)`. Runtime-tunable in DevCheats. Crosshair charge ring matches gameplay-effective time. | ✅ shipped 2026-05-14 |
| ~~A5~~ | ~~Per-archetype hit pause~~ | **Cut** — keep one global kill-feel pattern. | — |

### Track B — Per-combo signature mechanic

**Status (2026-05-13):** all 3 work items **shipped**. Track B complete.

**Work items:**
- B1: Ballistic Rifle heat-up spread (parabolic curve + crosshair bloom/tint + barrel emission glow) — ✅ shipped 2026-05-13
- B2: Laser Shotgun charge → focus + range — ✅ shipped 2026-05-13
- B3: Cross-cutting parabolic Laser charge damage curve — ✅ shipped 2026-05-13

Heat persists across reload + weapon swap (decay only via `WeaponHeatSystem.Tick` — no hard reset, per user decision Q8).

### Sequencing

1. Track B brainstorm (this doc) → lock 6 mechanics.
2. A2 impact VFX in parallel with B implementations.
3. Track B implementations one combo at a time (mirror Laser+Auto burst pattern: state field on WeaponEntityState, new WeaponPhase if needed, ShootingSystem branch, view feedback).
4. A1, A4 polish over working mechanics. (A3 cut — see table.)

**Status (2026-05-14):** Track A + B both closed. Pass complete.

---

## Decision log

| Combo | Winner | Why | Date |
|---|---|---|---|
| Ballistic Pistol | **No new mechanic** — keep baseline | Simple semi-auto is identity-by-simplicity. Pistol = clear, predictable sidearm. | 2026-05-13 |
| Ballistic Rifle | **BR-3 Heat-up spread** | Spread grows with sustained fire, 1s no-fire = reset. **Must NOT be topornо** — need smooth curve + clear visual telegraph (crosshair bloom + maybe barrel glow). | 2026-05-13 |
| Ballistic Shotgun | **No new mechanic** — keep baseline | Standard 7-pellet close-range shotgun is good identity. | 2026-05-13 |
| Laser Pistol | **No new mechanic** — keep baseline | Simple charged sniper-pistol works as identity. | 2026-05-13 |
| Laser Rifle | **No additional** — burst already covers it | Existing charge → 1–6 burst is the signature mechanic. | 2026-05-13 |
| Laser Shotgun | **LS-1 + LS-3 combined** — charge controls focus AND range | Low charge = wide cone + short range (close-range buckshot). Full charge = narrow cone + long range (sniper-cluster). Dual-axis charge utility. | 2026-05-13 |

### Cross-cutting decision: parabolic Laser charge damage curve

**Current:** linear `lerp(0.3, 1.0, chargeRatio)` — quick tap = 30% dmg.
**New:** parabolic with lower start, e.g. `min + (max - min) × chargeRatio²` з `min = 0.1` → quick tap ≈ 10%, mid ≈ 33%, full = 100%.
**Why:** make quick-spam laser shots clearly weak — user must commit to charge for meaningful damage. Applies to all 3 laser archetypes; Laser+Auto burst uses cached `BurstChargeRatio` so this propagates наturally.
**Tunables:** expose як DevCheats `LaserChargeDamageMin` (0.1) + `LaserChargeDamagePower` (2.0) — runtime feel-test friendly.

---

## Related

- [`README.md`](README.md) — gunplay shipped-state, where this item came from
- [`../weapon-builder/`](../weapon-builder/) — Tier 10 (Weapon Feel Polish) overlap
- [`../weapons.md`](../weapons.md) — weapon FSM, charge/burst details
