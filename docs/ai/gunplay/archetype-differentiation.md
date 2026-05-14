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

## Track B brainstorm

For each combo: 3 candidates → eval (interesting × fits theme × scoped × balance-safe) → pick winner. Decisions logged at bottom.

### Ballistic Pistol (Single, semi-auto, 12 mag, 1.5s reload)
Identity: precise sidearm, fast, low-commitment.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| BP-1 | **Tap rhythm bonus** | Each shot within 0.3s of previous = +stacking damage (cap 3). Encourages rhythmic tapping vs panic-spam. | Med — needs HUD telegraph |
| BP-2 | **Quick-draw shot** | First shot after equip-finish within 0.5s window = +damage + zero spread. Rewards reactive use. | Low — small state addition |
| BP-3 | **Last-round overcharge** | Final round in mag = 1.5× damage + brighter muzzle. Cinematic last-shot. | Low — purely stat branch |

### Ballistic Rifle (Auto, 30 mag, 2.0s reload)
Identity: workhorse, sustained engagement, discipline matters.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| BR-1 | **First-shot precision** | First bullet of any burst = 0 spread + bonus pen. Release trigger ≥0.2s resets. Punishes spray, rewards burst discipline. | Low — fits existing recoil model |
| BR-2 | **Suppressed cone** | Sustained fire (>5 shots, <0.5s gap) applies "suppressed" debuff on bots in cone — they flinch + can't return fire briefly. | High — new bot status effect, balance hazard |
| BR-3 | **Heat-up spread climb** | Continuous fire grows spread cone — visual barrel-glow telegraph. Cool: 1s no-fire = reset. Forces burst discipline. | Med — similar to BR-1 but inverse curve |

### Ballistic Shotgun (Scatter, 5 mag, 2.5s reload, 7 pellets)
Identity: close-range king, brutal, physical.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| BS-1 | **Slug alt-fire** | Hold alt-fire button = next shot is single high-dmg slug (no spread, +pen). Tactical mode switch. | Med — new input + WeaponStats branch |
| BS-2 | **Knockback impulse** | Hits at close range (<5m) apply impulse on bot rigidbody / ragdoll — physical shove. Stagger guaranteed. | Med — applies impulse via state event |
| BS-3 | **Per-shell reload** | 1 shell loaded at a time (~0.5s each). Reload cancellable mid-load to fire. Cooler reload UX. | Med-High — reload FSM rework |

### Laser Pistol (Single + Charge, 12 mag, 1.5s reload)
Identity: precise charge sniper, commitment shot.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| LP-1 | **Charge-mark target** | Charge animation paints laser dot on cursor target. If charge released while dot is on same target = +damage + headshot multiplier. Pre-aim setup. | Med — needs marker visual + dot raycast |
| LP-2 | **Bounce-beam at full charge** | Full charge (≥0.95) = beam ricochets once off wall. Skill shot trick. | High — new projectile branch (ricochet path) |
| LP-3 | **Soft-lock homing on tap** | No charge (<0.2) tap-shots have weak homing toward bot under cursor (15° max curve). Charged shots = straight beam. | High — projectile homing logic |

### Laser Rifle (Auto + Charge → Burst)
Identity: charge-and-unload artillery. Already has 1–6 burst.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| LR-1 | **Heat lockout** | Each burst shot adds heat. Heat=100% = forced 1.5s cooldown (audio whistle + glow). Forces gaps between bursts. | Low-Med — purely additive state field |
| LR-2 | **Burst chain to next target** | Each burst shot can arc to a nearby (≤3m) bot if line of sight. Multi-target sweep. | High — new projectile post-hit logic |
| LR-3 | **Overcharge penalty** | Holding charge past max time (>2× ChargeTime) auto-fires with WORSE damage (instead of best). Punishes greedy hold. | Low — single phase check |

### Laser Shotgun (Scatter + Charge, 5 mag, 2.5s reload, 7 pellets)
Identity: charge-controlled beam-buckshot. Hardest combo to differentiate.

| # | Idea | Core mechanic | Risk |
|---|---|---|---|
| LS-1 | **Charge controls focus** | Low charge = wide 30° cone (buckshot). Full charge = narrow 5° focused single-direction beam-cluster (slug-mode). Same 7 pellets, different spread. | Low — modulates pellet dir math |
| LS-2 | **Pellet chain-arc on hit** | Each pellet that hits a bot can arc lightning to 1 nearby bot (≤4m). Multi-kill potential, cluster combat. | High — chain raycast post-hit |
| LS-3 | **Charge controls range** | Low charge = short-range cone (low projectile lifetime). Full charge = long-range tight cluster (extended lifetime + speed). | Low — modulates stats per shot |

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

## Open questions

- Audio layer is missing entirely — should signature mechanics ship with placeholder SFX hooks now, or wait for audio epic?
- Some mechanics (BR-2 suppression, LR-2 chain) need balance pass — high risk of dominating one archetype. Want playtest after each, not all-then-test.
- A2/A3 view polish — should we author per-mechanic VFX during the mechanic implementation, or batch all view work after Track B is locked?

---

## Related

- [`README.md`](README.md) — gunplay shipped-state, where this item came from
- [`../weapon-builder/`](../weapon-builder/) — Tier 10 (Weapon Feel Polish) overlap
- [`../weapons.md`](../weapons.md) — weapon FSM, charge/burst details
