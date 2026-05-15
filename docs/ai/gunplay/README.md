# Combat Polish — Shipped State

> Living summary of the combat-feel layer. Original "Better Feel Gunplay" epic (2026-05-01..05-11) converged — most of what makes shooting feel weighty is shipped. This doc tracks what's live, what was tried-and-reverted, and what remains in backlog.

---

## Vision (carried over)

Top-down extraction shooter. Every shot must register physically — weight, hits, deaths, world-marks. AAA-quality moment-to-moment combat over a graybox playable.

**Architectural constraints (per [CLAUDE.md](../CLAUDE.md)):**
- VFX dispatched via `IRaidEvents`, never direct Unity API from systems
- Tunables live in `DevCheatsConfig` / `ViewCheatsConfig` SOs, read via `RaidContext.*Config` structs
- Systems stateless, view layer owns Unity refs
- No new singletons

---

## Shipped

### Hit feedback layer
- ✅ Crosshair hit/kill/headshot/ricochet X markers ([`crosshair.md`](../crosshair.md))
- ✅ Floating damage numbers with size-by-magnitude + flight direction + absorption tint
- ✅ Character rim flash (per hit kind: normal / headshot / kill / ricochet) — `View/CharacterHitFx.cs`
- ✅ Per-bone bullet decals on character body (survive ragdoll detach) — `CharacterHitFx`
- ✅ Hit pause / hitstop (`HitPausePresenter`, scaled per hit kind)
- ✅ Camera shake on fire + take-damage (`CameraShakePresenter`)
- ✅ Blood VFX (directional splash + mist + flash, ~14 particles/hit)
- ✅ Blood decals on floor / wall (`BloodDecalPresenter` + `DecalProjectorPool`)
- ✅ Bullet hole decals on walls (200 active cap, 90s lifetime)
- ✅ Helmet fly-off on armor break (`ArmorBreakHelper`)
- ✅ Defender armor HUD + healthbar armor stripe

### Weapon visual layer
- ✅ Procedural recoil kick on fire (`WeaponView.TriggerRecoilKick`)
- ✅ Multi-stage muzzle flash + real-time light pulse
- ✅ Casing ejection with hybrid auto-settle (linear damping ramp → kinematic freeze) — `CasingEjectorPresenter`
- ✅ Magazine drop on reload (ballistic-only physics drop with DropDelay + hybrid settle, Ragdoll-layer to avoid pushing player) — `MagazineDropPresenter`
- ✅ Tau-style beam flash for Laser archetype (per-pellet electric flicker LineRenderer)
- ✅ Modular weapon visualization (payload base + delivery barrel composition — Tier 8.x*)
- ✅ Weapon drop on ragdoll death (physics + impulse along shot direction)

### Death + reaction
- ✅ Full ragdoll with mass distribution (heavy hips), head joint limits, velocity inheritance, ground impact damping, random death twist
- ✅ Ragdoll layer isolation — corpses don't get pushed by walking characters
- ✅ Headshot vs bodyshot profile differentiation (impulse magnitude, hips scale, stagger window)
- ✅ Hit decals freeze on death + route to ragdoll body even on one-shot kills

### Combat dynamics
- ✅ Spine IK lean stagger + AI fire lockout (`FlinchPresenter` + `BotEntityState.StaggerEndTime`)
- ✅ Weapon barrel pullback on walls AND characters (`CharacterBody.LateUpdate` SphereCast)
- ✅ Projectile start-overlap probe (point-blank reliability when spawn inside enemy capsule)
- ✅ Lock-on convergence override (3D-accurate hits when cursor on damageable)
- ✅ Semi-auto trigger gate (Single / Scatter = one-press one-shot; Auto = held)
- ✅ Bot off-screen fire gate — player-centric radius (`BotEngagementConfig.MaxEngagementRadius`) caps bot fire range. Closes "damage from off-screen without telegraph" UX gap. ShootNode early-out, runtime-tunable in DevCheats. Trade-off (acknowledged): 16:9 vertical edge может пропустити case коли бот стріляє з-за кадру по вертикалі — tunable radius мінімізує.

### Test infrastructure
- ✅ 4 test scenes: `ShootingScene` (armored targets), `ShootingScene_KillFeel` (low-HP), `ShootingScene_Horde` (zombie waves), `ShootingScene_RangedRange` (ranged combat with cover)

---

## Tried + Reverted

### B.1 Recoil pattern polish (2026-05-03)
**What:** Per-archetype recoil compounding (per-shot accumulator × kick magnitude, perfect-first-shot pattern, archetype defaults Auto 0.15/1.0 / SingleAction 0.08/0.5 / Scatter 0/0).

**Why reverted:** No learnable skill ceiling at our top-down + cursor-aim setup. Player can't compensate pull-down patterns — cursor stays on target. Base random side scatter already organically degrades sustained accuracy. Multiplicative ramp invisible.

**Recorded decision:** Don't revisit without camera/aim model change (first-person, over-shoulder, OR tracer system that visualizes drift cycles).

### Weapon heat haze (2026-05-11)
**What:** Billboarded refraction quad over MuzzlePoint, procedural noise UV-offset of `_CameraOpaqueTexture`, heat accumulator with cool decay.

**Why reverted:** Refraction reads poorly at top-down camera angle on graybox scenes (no high-contrast backgrounds). Plume rises 30-50 screen pixels, competes with muzzle flash + casing eject for screen attention. Marginal feel contribution.

**Recorded decision:** Don't revisit. Future barrel-hot feedback should prefer material emission glow on the barrel mesh, not screen-space refraction.

---

## Backlog (active candidates)

### Visual feedback / readability

- ~~**Floating damage numbers v2**~~ — ✅ shipped 2026-05-14. uGUI + TextMeshPro World-Space Canvas (Distance Field Overlay shader → renders over geometry), Oswald-Bold SDF, 6 per-tier HDR-boosted material presets. Per-tier trajectory modes (FloatUp / FloatUpDrift / Knockback / ArcGravity — kill defaults to ArcGravity for cinematic punctuation, ricochet to FloatUpDrift). Same-target 200ms consolidation (Hades-style anti-spam). Sub-label format `30\nHEAD/KILL` для headshot/kill, "RICOCHET" word для deflections, bleed tick emits popup. Legacy IMGUI overlay fully removed. Live-tunable via `🔢 Damage Numbers v2 (TMP)` section.
- **Aim cursor v2** — 🚧 Stage 1 shipped 2026-05-15 (tech foundation + ADS top-arm cutoff + outline + EFD-style hit pulse (4 per-type profiles) + flame charge fill + overheat tremble + tunable charge curve (`ChargeRatioPower` + `ChargeTimeOverrideSeconds`) + **per-archetype cursor**: Ballistic 4-arm, Laser segmented ring (12 slices, clockwise fill)). Toggle Y in Play for A/B with legacy IMGUI. **Next**: Stage 2 (directional recoil kick). EFD-style directional recoil kick + focus blur + 3-tier range color + same-pixel UI swap. Hybrid uGUI + SDF shader (no UI artist needed). 7-stage incremental plan. **Status doc**: [`aim-cursor-v2.md`](aim-cursor-v2.md) — fully self-contained handoff doc.
- **HUD damage feedback** — vignette pulse on take-damage, low-HP edge glow, directional damage indicator. Player-side gap; revisit when "I lost HP and don't know why" becomes a playtest signal.

### Weapon identity

- ~~**Maximal archetype differentiation**~~ — ✅ shipped 2026-05-13/14. Two-track pass closed. Track B (mechanic uniqueness): Ballistic Rifle heat-up spread, Laser Shotgun charge → focus+range, parabolic laser charge damage curve. Track A (view polish): per-archetype camera shake profiles, per-payload impact VFX, per-archetype charge time multiplier. A3 (per-delivery recoil shape) cut — camera shake + crosshair sufficient. See [`archetype-differentiation.md`](archetype-differentiation.md).

All above are pure additive — no architectural risk if/when picked up.

---

## Related docs

- [`../weapons.md`](../weapons.md) — runtime weapon FSM, trigger semantics, aiming, pullback, projectile collision
- [`../crosshair.md`](../crosshair.md) — crosshair / hit markers / recoil visuals
- [`../battle-design-status.md`](../battle-design-status.md) — armor system + bleeding mechanics
- [`../bot-ai.md`](../bot-ai.md) — bot AI (BT + melee + horde)
- [`../weapon-builder/README.md`](../weapon-builder/README.md) — weapon composition + content
