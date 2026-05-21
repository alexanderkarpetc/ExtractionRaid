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
- ✅ HUD damage feedback — **directional vignette pulse on hit + low-HP edge glow** (single SDF shader, fullscreen overlay). 4 concurrent hit slots з round-robin allocation, sector arc on screen edge pointing where shot came from (camera-local projection of `projectileDirection`). Hit kind tier intensity (Ricochet/Normal/Headshot/Kill = 0.35/0.7/0.95/1.0). Low-HP layer: heartbeat sine pulse (0.8Hz) when HP ratio ≤ 35%. Chebyshev radial gate (square-aligned to screen edges, symmetric on all sides). See `View/HudDamagePresenter.cs`, `Shaders/HudDamageDirectional.shader`, live-tunable via `🩸 HUD damage feedback` section.

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
- ✅ GodMode visual passthrough (2026-05-19) — `DamageSystem` no longer early-returns on player victim under GodMode. Mutations (HP / armor durability / bleeding) gated by `godModePlayerVictim` flag while all VFX/events (HitConfirmed, EntityHit, ProjectileRicochet, DamageNumber, etc.) fire normally. Lets us playtest hit feedback without dying.
- ✅ Projectile own-owner filter (2026-05-19) — `ProjectileView` now skips its own shooter's capsule in both Start-overlap probe + SphereCast collision branches. Fixes silent-fail case where bot's projectile spawned inside its own capsule and self-hit on frame 0 (visible симптом: bots "didn't shoot" because bullets froze on spawn; lasers showed beam VFX but missed player).

### Test infrastructure
- ✅ 5 test scenes: `ShootingScene` (armored targets), `ShootingScene_KillFeel` (low-HP), `ShootingScene_Horde` (zombie waves), `ShootingScene_RangedRange` (ranged combat with cover), **`ShootingScene_Feedback`** (HUD damage feedback playtest — 6 stationary turrets in a row firing -Z + 3 side turrets firing -X, all 6 weapon archetypes covered)
- ✅ `BotBehaviorFlags.FireForward` + `FireForwardNode` (2026-05-19) — stationary-turret behavior. Continuous fire in current facing direction, no target tracking, no rotation toward player. Top-level BT branch, not gated by `HasTarget?`. Skips face-target rotation in `BotMovementSystem`. Used by `FeedbackTarget_*` configs in FeedbackRange scene.

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
- ~~**Aim cursor v2**~~ — ✅ SHIPPED 2026-05-18. Hybrid uGUI + SDF shader stack replaced legacy IMGUI overlay. Final feature set: 1:1 IMGUI port + ADS top-arm cutoff + outline + EFD-style hit pulse (4 per-event-type profiles) + flame charge fill for ballistic / **segmented ring for laser** (12 slices clockwise fill, analytical O(1) SDF) + overheat tremble + tunable charge curve (`ChargeRatioPower` + `ChargeTimeOverride`) + laser firing animation (chargeFill bleed + radial pulse over FireInterval) + focus blur edge (recoil pressure + ADS settle driven). Recoil stays gameplay-rooted (no view-only duplication). UI cursor swap via existing `PointerOverUiTracker` + `IsPointerOverUi` infrastructure (same-pixel OS cursor). Legacy `AimCursorOverlay.cs` deleted in Stage 7. See [`aim-cursor-v2.md`](aim-cursor-v2.md) for stage-by-stage shipping log + cut items (Stage 2 directional kick / Stage 4 range tier color / Stage 6 low-ammo pulse).
- ~~**HUD damage feedback**~~ — ✅ SHIPPED 2026-05-21 (see Shipped → Hit feedback layer above). Combined "directional vignette pulse" + "low-HP edge glow" into a single SDF shader instead of separate elements. Side-channel deliverables: GodMode visual passthrough in DamageSystem, projectile own-owner filter in ProjectileView, FireForward bot behavior + FeedbackRange test scene to drive playtest.
- **Battle HUD** — 🚧 spec locked 2026-05-21. Replace debug-style armor/helmet overlays з coherent procedural HUD: armor paper-doll (TL) + status effects row з tooltips (WoW-style, right of paper-doll) + worldspace status mini-icons UNIVERSAL for all characters (under existing HP bar) + Zelda-style radial stamina ring under player feet + hotbar weapon slot redesign (UI Toolkit extension з distinct treatment for slots 1-2). Restrained-tactical tone — procedural SDF (no UI artist). HP bar stays worldspace-only (no HUD duplication). See [`battle-hud.md`](battle-hud.md) for full spec + implementation plan (~7h Tier 1).

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
