# Better Feel Gunplay — Roadmap

> Phase-decomposed plan для polish epic. Кожен phase виносить значний "feel great" payoff на playable build. Phases можна виконувати sequentially (recommended) або parallel якщо teamwork permits.
>
> **Пipple-метрика:** play 5 хвилин на існуючому 6×archetype matrix → "feels physical, not cardboard". Iterative — repeat after each phase.

---

## Огляд phases

| Phase | Theme | Items | Effort | Payoff |
|-------|-------|-------|--------|--------|
| **A** Foundation impact feel | hit pause, hit flash, camera shake, blood spray, muzzle polish, casings, world impact, ragdoll, decals | ~15-20h | 🔥🔥🔥 |
| **B** Significant juice | recoil polish, tracers, HUD hit feedback, enemy stagger, decal persistence, bleeding visual | ~10-15h | 🔥🔥 |
| **C** Wow moments | headshot special, multi-kill streaks, slow-mo, post-processing, close-miss | ~8-12h | 🔥🔥 |
| **D** Advanced polish | environment destruction, advanced physics, weapon heat, pen dual-impact | ~10-15h | 🔥 |

**Пorядок виконання:** A → B → C → D. Phase A — найбільший payoff per hour; Phase D — diminishing returns, можна skip частково.

**Parallel tracks:** будь-яка з phase'ів — programmer + artist (якщо є). Programmer-only path працює (primitive VFX OK для playtest, real art — Phase Z).

---

## Phase A — Foundation Impact Feel

> **Goal:** "feels physical" baseline. Кожен постріл + hit registers через camera + character + world reactions. Це **mandatory** baseline — без цього все інше — polish on top of nothing.

### Work items

#### A.1 Hit Pause / Hitstop (HIGH payoff per effort)

**What:** Brief pause of game time (~30-80ms) на момент successful hit. Returnal/Hades pattern. Дає вагу кожному успішному пострілу.

**Implementation sketch:**
- New `HitPauseSystem` (static): tracks `_pauseEndTime`. Тick checks if active → sets `Time.timeScale = 0.05` (or similar low value). Restores `1f` at end.
- Triggers: subscribe до `HitConfirmed` event у view layer. Per-event duration:
  - Normal hit: 30ms
  - Headshot: 60ms
  - Kill: 80ms
  - Critical kill (last enemy): 120ms
- DevCheats: `HitPauseConfig { NormalDuration, HeadshotDuration, KillDuration, CriticalKillDuration, GlobalScale }`
- ⚠️ Make sure не break input timings (player пальці на trigger continuously) — scaled animation/physics через TimeScale, не gameplay logic ticks (RaidSession.Tick uses `Time.unscaledDeltaTime` for input).

**Effort:** ~1-1.5h
**Payoff:** 🔥🔥🔥 — single biggest "feels great" multiplier

#### A.2 Hit Flash on Enemy

**What:** Character body briefly flashes white/red на damage tick. Universal feedback "this thing took damage".

**Implementation sketch:**
- View-layer `BotPresenter` / character body subscribes до `HitConfirmed.targetedEntityId` events.
- Material property block manipulation: temporarily boost `_EmissionColor` or `_Color` to bright white/red for 80-120ms.
- Per-target color: ricochet=blue, normal=white, headshot=gold/red.
- Coroutine-driven fade-out або scheduled timer.
- Not disrupted by simultaneous hits (latest event resets timer).

**Effort:** ~1-2h
**Payoff:** 🔥🔥🔥

#### A.3 Camera Shake System

**What:** Camera shake on fire (per-archetype intensity) + on take damage (different curve).

**Implementation sketch:**
- New `View/CameraShake.cs` MonoBehaviour on Main Camera. Public API:
  ```csharp
  public void Kick(Vector3 direction, float magnitude, float duration);
  public void Tremor(float intensity, float duration);  // omni-directional
  ```
- Maintains internal `_offset` Vector3 lerped per-frame. Camera reads `transform.localPosition += _offset` (or wrapper structure).
- Triggers from view layer:
  - `WeaponFired` event → directional kick from player toward weapon direction (small) + omni tremor (proportional to recoil stat)
  - Player's `HealthChanged`/`DamageTaken` event → tremor (proportional to damage)
  - Explosion → strong tremor + radial kick from explosion source
- DevCheats: `CameraShakeConfig { FireIntensityScale, FireDurationScale, DamageIntensityScale, GlobalScale, EnableShake }`
- Per-archetype tuning via SO field on `DeliveryCoreDefinition.RecoilCameraShake` (re-use existing `RecoilKickForward`/`Side` magnitudes? або new field).

**Effort:** ~2-3h
**Payoff:** 🔥🔥🔥

#### A.4 Blood Spray on Character Impact

**What:** Particle burst at hit point on character. Direction = projectile vector. Intensity scales з damage / penetration.

**Implementation sketch:**
- Reuse existing `BodyImpact.prefab` як base — or create new `BloodSpray.prefab`. Verify particles mimic real blood (red, fast initial velocity, fade).
- Spawn at `HitConfirmed.hitPoint` (already у event) під angle aligned з projectile direction (need projectile-direction у event, currently absent — extend event signature OR use PlayerView lookup).
- Variants based on damage tier:
  - Tap: small puff
  - Kill: spray + chunks
  - Headshot: extra particles + gore
- Decal projection: blood splatter behind target (raycast back along projectile direction → decal on wall behind).
- DevCheats: `BloodVfxConfig { SprayIntensity, ChunksOnKill, EnableDecals, DecalLifetime }`

**Effort:** ~3-4h (з decal projection — найбільший компонент)
**Payoff:** 🔥🔥🔥

#### A.5 Muzzle Flash + Real-time Light

**What:** Multi-stage muzzle flash particle + brief Point Light pulse при кожному пострілі.

**Implementation sketch:**
- Existing `WeaponView.PlayMuzzleFlash` (placeholder) — extend з:
  - Layered particles: hot core (orange/white sphere flash 50ms) + cool smoke wisp (300ms fade)
  - Pulse Light component (Range=2-3m, Intensity=10-20 для flash моменту, decays over 50ms)
- Per-archetype variant prefab via `WeaponView._muzzleFlashPrefab` field.
- DevCheats: `MuzzleVfxConfig { FlashIntensity, LightIntensity, EnableLight }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

#### A.6 Casing Ejection

**What:** Кожен постріл випльовує physics shell з gun. Adds visceral "machine working" feel.

**Implementation sketch:**
- New `Resources/Prefabs/VFX/Casing.prefab` — small Rigidbody з MeshRenderer (use `SM_Prop_Casing_*` from PolygonApocalypse чи primitive cylinder).
- New `View/CasingEjector.cs` component on weapon prefabs — `[SerializeField] Transform _ejectPort`, `_ejectForce`, `_ejectDirection`.
- On `WeaponFired` event → `CasingEjector.Eject()`: instantiate, AddForce у eject direction + random spin, schedule destroy у 5-10s.
- Pool casings (limit ~30-50 active worldwide) — new ones replace oldest. Avoids physics overhead.
- DevCheats: `CasingConfig { EjectVelocity, MaxActiveCasings, FadeOutTime, EnableCasings }`

**Effort:** ~3-4h (з pool)
**Payoff:** 🔥🔥

#### A.7 Material-Specific Impact VFX

**What:** Bullet hits concrete = chunks + dust; metal = sparks; wood = splinters; dirt = puff. Without this усі стіни feel однаково.

**Implementation sketch:**
- Tag system: each `Collider` carries `MaterialKind` enum (Concrete / Metal / Wood / Dirt / Default) — via `MonoBehaviour` `MaterialTag.cs` component.
- `ProjectileView` SphereCast on impact → resolve material → spawn corresponding `BulletImpact_<kind>.prefab`. Default fallback to existing `BulletImpact.prefab`.
- 4-5 prefab variants — each з color-coded particle burst + appropriate decal sprite.
- DevCheats: `MaterialImpactConfig { EnablePerMaterial, FallbackPrefab }`
- Manual tagging pass: scan existing scenes for major surfaces → add MaterialTag (~30-60 min).

**Effort:** ~4-5h (включно з manual tagging pass)
**Payoff:** 🔥🔥🔥

#### A.8 Bullet Hole Decals (persistent)

**What:** Bullet hits leave persistent decal на стінах/підлогах. Cumulative impact на world.

**Implementation sketch:**
- New `View/DecalProjectorPool.cs` — manages limited pool (e.g. 200 active decals world-wide). New decals replace oldest.
- On wall hit (HitSignal event for non-character target) → pool spawns decal projector at hit point, oriented along surface normal.
- Decal asset: existing `SM_Prop_BulletHoles_01.fbx` mesh — convert to URP Decal material (or Quad-mesh-with-cutout shader).
- Material-specific decal sprites: shared base, optional color tint per material.
- DevCheats: `DecalConfig { MaxBulletHoles, MaxBloodPools, DecalLifetime, EnableDecals }`

**Effort:** ~3-4h
**Payoff:** 🔥🔥

#### A.9 Ragdoll Death + Directional Knockback

**What:** Bot dies → activates ragdoll. Force applied at hit point у напрямку shot direction. Magnitude scales з damage.

**Implementation sketch:**
- Per-character: `Character01.prefab` Skinned Mesh має skeleton — need ragdoll setup (Rigidbody + Joint per bone, Collider per limb). Unity Editor "Ragdoll Wizard" (`GameObject → 3D Object → Ragdoll`) — manual one-time setup.
- New `View/RagdollController.cs` — toggles between Animator-driven and physics-driven on death.
- On bot death event:
  - Disable Animator
  - Enable all Rigidbodies on bones
  - Apply `AddForceAtPosition(shotDirection * damage * scale, hitPoint)` to nearest bone
- Integration: `BotPresenter` listens до bot death → calls `RagdollController.Activate(hitPoint, shotDirection, damage)`.
- Cleanup: ragdoll persists for ~30s, then sinks/fades. Loot drop стається at ragdoll's eventual position.
- DevCheats: `RagdollConfig { ForceScale, FadeOutTime, EnableRagdoll }`

**Effort:** ~5-7h (rigging setup найбільша частина)
**Payoff:** 🔥🔥🔥

#### A.10 Blood Pool Decal Under Body

**What:** Blood pool grows under dead body over time. Persistent.

**Implementation sketch:**
- On bot death → start coroutine spawning blood pool decals beneath body progressively.
- Decal pool (reuse `DecalProjectorPool` від A.8 з separate `BloodPool` category).
- Existing `SM_Prop_BloodPool_*.fbx` assets used as decal sprites.
- Visual growth: spawn small initial pool, scale up over ~5-10s.
- DevCheats: `BloodPoolConfig { GrowthDuration, MaxPoolSize, EnableBloodPools }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

### Phase A exit criteria

- ✅ Кожен hit triggers hit pause + character flash
- ✅ Camera shakes на fire та damage taken (per-archetype intensity)
- ✅ Blood sprays on character impact, splatters on wall behind
- ✅ Muzzle flash polished (multi-stage + light pulse)
- ✅ Casings eject on every shot (pooled)
- ✅ Walls show material-appropriate impact VFX
- ✅ Bullet holes persist on walls/floors
- ✅ Bots ragdoll on death з directional knockback
- ✅ Blood pool grows under dead bodies
- ✅ All 6 archetypes leverage these uniformly (Ballistic/Laser × Pistol/Rifle/Shotgun)

### Phase A estimated effort

**~20-25h programmer-side.** Найбільші items: A.7 material system (~5h), A.9 ragdoll (~7h), A.4 blood + decal (~4h). Smaller items (A.1, A.2, A.3) — quick wins (~1-3h each).

---

## Phase B — Significant Juice

> **Goal:** depth layer over Phase A foundation. Recoil не feels generic; HUD reacts to player state; enemies feel alive coли hit; world remembers.

### Work items

#### B.1 Recoil Pattern Polish

**What:** Per-archetype recoil curves з vertical+horizontal pattern + dampening + ADS reduction.

**Implementation sketch:**
- Existing `RecoilKickForward/Side` stats — extend з:
  - `RecoilPattern` enum: `Vertical / DiagonalRight / DiagonalLeft / Random / SprayThenStable`
  - `RecoilStandPlateau` — secondary kick magnitude after first 3 shots
- `AimingSystem` applies recoil offset per fire — scale by ADS blend (existing).
- Visual: `WeaponView._recoilKickDistance` (Wave D) extended з rotation kick (pitch-up sway).
- DevCheats: `RecoilConfig { GlobalScale, VerticalScale, HorizontalScale, ADSReductionScale, PatternEnabled }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

#### B.2 Tracers / Projectile Trails

**What:** Visible glowing line from muzzle to target. Adds projectile presence.

**Implementation sketch:**
- `ProjectileView` — extend з LineRenderer / TrailRenderer component.
- Per-payload styling:
  - Ballistic: thin yellow/orange tracer, fades over 100ms
  - Laser: thicker red/orange beam, persists через projectile lifetime, brighter intensity
- Spawn lifetime trail mid-flight, не just hit-line.
- DevCheats: `TracerConfig { EnableTracers, BallisticColor, LaserColor, ThicknessScale }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

#### B.3 HUD Hit Feedback (vignette + pulse + directional indicator)

**What:** Player UI reacts до taking damage. Red vignette pulse on hit, edge red glow на low HP, directional damage indicator (arrow showing where shot came from).

**Implementation sketch:**
- New `View/UI/HUD/DamageHud.cs` (Canvas Group + uGUI Image overlays).
- 3 layers:
  - Vignette pulse: full-screen Image with red gradient. On damage event → fade in to alpha 0.3 → fade out 200ms.
  - Low HP edge glow: pulsing red на edges коли HP < 30%. Heart-rate vibe.
  - Directional indicator: arrow image rotates toward damage source. Lifecycle 1-2s. Multi-source = multi-arrow.
- Damage source extraction: extend `IRaidEvents.DamageTaken(victim, source, position, damage)` (or use existing).
- DevCheats: `DamageHudConfig { VignetteIntensity, LowHpThreshold, IndicatorDuration, EnableHud }`

**Effort:** ~3-4h
**Payoff:** 🔥🔥

#### B.4 Enemy Stagger / Flinch Animation

**What:** Bots show physical reaction to being shot. Without this all hits feel like shooting cardboard.

**Implementation sketch:**
- Animator parameter `IsHit` (trigger). Animator state: brief flinch animation (lean back 0.1s, return).
- More elaborate: hit direction → 4-direction flinch (front/back/left/right) via Animator blend tree.
- На big hits (>= 30% HP damage у one tick) → stagger animation (stagger back step, longer animation 0.3-0.5s).
- AI temporarily can't fire during stagger (flag in BotState).
- Source: `BotPresenter` listens до HitConfirmed targeting bot ID → triggers animator.
- Will need fresh animation clips чи simple procedural lean using `Transform` (interim).
- DevCheats: `StaggerConfig { FlinchDuration, StaggerThreshold, StaggerDuration, EnableStagger }`

**Effort:** ~4-5h (з procedural fallback if no real anim clips)
**Payoff:** 🔥🔥🔥

#### B.5 Body Persistence + Cumulative World Marks

**What:** Dead bodies stay long term (until extraction); blood pools persist; bullet holes accumulate; cumulative wall darkening при концентрованому fire.

**Implementation sketch:**
- Body persistence — extend ragdoll lifetime (B.5 default 30s → ∞ until raid ends). Loot extraction can sync з body position freezing.
- Blood pool persistence → already done у A.10, extend lifetime.
- Bullet hole pool → already done у A.8, extend.
- Cumulative darkening — material instance per "shooting zone"; track damage density; apply darken multiplier. **DEFER as polish overkill** — лишити для Phase D якщо потрібно.

**Effort:** ~1-2h (if just extending lifetimes from Phase A)
**Payoff:** 🔥🔥

#### B.6 Bleeding State Visual

**What:** Wounded characters drip blood while alive. Floor trail when moving while bleeding. Tier 1+2 bleeding мехаnіка вже існує — це visual layer над нею.

**Implementation sketch:**
- `BotPresenter` / `PlayerPresenter` reads `state.StatusEffects[id].BleedingStacks`.
- Per stack → enable a particle emitter на character body (drip particles, low rate).
- On move → spawn blood drop decal (small scale, low intensity) on floor at character position. Pool decal projection.
- DevCheats: `BleedingVfxConfig { DripRate, FloorDecalRate, EnableBleedingVfx }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

### Phase B exit criteria

- ✅ Each archetype має distinct recoil personality (felt, not just numerical)
- ✅ Tracers visible — Ballistic short fast, Laser persistent beam
- ✅ HUD reacts to damage (vignette, low HP, indicator)
- ✅ Bots stagger / flinch on hit, can't fire mid-stagger
- ✅ Bodies + decals persist until raid ends
- ✅ Bleeding characters visually bleed (drips, floor trail)

### Phase B estimated effort

**~14-18h programmer-side.** B.4 (stagger animation) — найбільший item, потенційно needs animation clip work — defer if no animator. B.1 + B.3 — quick wins. Animator content є blocker для some items.

---

## Phase C — Wow Moments

> **Goal:** memorable peak moments — headshot, multi-kill, critical kill. Add flair над solid baseline. Phase C = "screencaps will look amazing."

### Work items

#### C.1 Headshot Special VFX

**What:** Headshot kill triggers extra-juice: gibs, screen flash, optional slow-mo.

**Implementation sketch:**
- New `Resources/Prefabs/VFX/HeadshotGore.prefab` — particle burst (5-10 chunks, blood mist, brief flash).
- On `HitConfirmed.isHeadshot && isKill` → spawn `HeadshotGore` at head bone position + brief screen flash (300ms white pulse from edges).
- ✅ existing `HeadImpact.prefab` already у assets — extend or replace.
- Trigger Slow-mo on kill (C.3) для dramatic effect.
- ✅ existing helmet fly-off lasered properly з headshot pen.
- DevCheats: `HeadshotConfig { GibCount, FlashIntensity, EnableSlowMo }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

#### C.2 Multi-Kill / Streak UI

**What:** Counter та banners для chained kills. "DOUBLE KILL", streak counter.

**Implementation sketch:**
- New `View/UI/HUD/KillStreakHud.cs` Canvas overlay.
- Track time-since-last-kill global counter. Within 2-3s → kill chain. Display counter top-right.
- На threshold thresholds (2/3/5/10 kills у chain) → toast banner ("DOUBLE KILL", "TRIPLE KILL", "RAMPAGE").
- Subtle UI — не block gameplay center.
- DevCheats: `KillStreakConfig { ChainTimeout, ToastDuration, EnableStreaks }`

**Effort:** ~2-3h
**Payoff:** 🔥

#### C.3 Slow-Mo on Critical Kill

**What:** Last enemy у encounter dies → 0.3s slow-mo. Kill-cam vibe.

**Implementation sketch:**
- Same `HitPauseSystem` extended — but longer duration + heavy curve.
- Trigger criteria:
  - Last alive bot у raid → 500ms timeScale=0.2 → ramp back
  - Headshot kill at >5m distance → 300ms timeScale=0.3
  - "Pen kill" (penetrated through wall) → 400ms timeScale=0.25
- Configurable per-trigger duration.
- DevCheats: `SlowMoConfig { LastEnemyDuration, HeadshotDuration, PenetrationDuration, GlobalScale, EnableSlowMo }`

**Effort:** ~1-2h (extends HitPauseSystem from A.1)
**Payoff:** 🔥🔥

#### C.4 Post-Processing Layers

**What:** URP post-processing volume: chromatic aberration spike on hits, vignette polish, brief saturation boost on critical events.

**Implementation sketch:**
- Camera має URP Volume з PostProcessProfile. New profile assets:
  - Default — minimal effects
  - DamageFlash — vignette pulse + chromatic aberration spike
  - CriticalKill — saturation boost briefly
- Code-driven profile blend via `Volume.weight` lerping.
- Triggers:
  - Damage taken → DamageFlash pulse 200ms
  - Critical kill → CriticalKill 400ms
  - Bleeding state → subtle persistent vignette desaturation
- DevCheats: `PostProcessConfig { ChromaticIntensity, VignetteIntensity, SaturationBoost, EnablePostFX }`

**Effort:** ~3-4h
**Payoff:** 🔥🔥

#### C.5 Bullet Whiz / Close-Miss Visual

**What:** Куля пролітає поряд з player camera → visual swish line + dust kick if near floor. "Bullet just missed me!" tension.

**Implementation sketch:**
- `ProjectileView` checks distance to player on update tick.
- If player camera within ~1.5m of projectile path → spawn `BulletWhiz.prefab` (LineRenderer fade fast).
- If projectile passes within 0.3m of floor → kick small dust puff at impact-projection point.
- Throttle: max 1 whiz per second. Spam protection.
- DevCheats: `WhizConfig { Distance, EnableWhiz, EnableFloorDust }`

**Effort:** ~2-3h
**Payoff:** 🔥🔥

### Phase C exit criteria

- ✅ Headshots feel like punctuation events (gibs, flash)
- ✅ Streaks tracked + celebrated UI-side
- ✅ Critical kills get cinematic slow-mo
- ✅ Post-processing reacts to combat moments
- ✅ Close-miss visual builds survival tension

### Phase C estimated effort

**~10-13h programmer-side.** Mostly UI/VFX polish layered over Phase A+B foundation.

---

## Phase D — Advanced Polish

> **Goal:** edge-case polish. Diminishing returns — likely partial execution; pick items by playtest signal.

### Work items

#### D.1 Environment Destruction

**What:** Glass shatters, wood splinters, explosive barrels detonate, lights swing.

**Implementation sketch:**
- New `MonoBehaviour` types: `Breakable`, `ExplosiveBarrel`, `SwingableLight`.
- On bullet hit (or HitSignal targeting them) → breakable trigger physics shatter (replace mesh with chunks prefab + add Rigidbodies).
- Explosive barrel — emit `ExplosionRequested` event → `ExplosionSystem` propagates damage.
- Manual tagging pass на existing scene props.

**Effort:** ~5-7h
**Payoff:** 🔥

#### D.2 Magazine Drop Physics + Spent Casings Persistence

**What:** Reload drops magazine з gun (physics object); spent casings stay on ground (limited count).

**Implementation sketch:**
- Extend `WeaponView.PlayReload` → spawn magazine prefab at gun position + AddForce down/back.
- Extend `CasingEjector` (A.6) → optional persistence flag (default to fade as before; persist mode for lazier playtest).
- Pool both — total < 100 active world.

**Effort:** ~2-3h
**Payoff:** 🔥

#### D.3 Weapon Heat State Visual

**What:** Sustained fire → barrel glows red gradient. Auto / Laser specific.

**Implementation sketch:**
- `WeaponView` tracks shots-per-second moving average.
- Above threshold → blend material `_EmissionColor` to red. Decay back to default when not firing.
- Steam puff particle when hot weapon stops firing.

**Effort:** ~2-3h
**Payoff:** 🔥

#### D.4 Penetration Dual-Impact

**What:** Bullet penetrates target → entry puff (small) + exit blood spray (larger, behind target). Wall pen dust on entry/exit sides.

**Implementation sketch:**
- `ProjectileView` після pen → spawn entry impact at hit point + exit impact at far side bound.
- Need exit point calculation (raycast forward until ray exits collider OR mesh-aware sampling).
- Cleaner integration с existing armor pen feedback.

**Effort:** ~3-4h
**Payoff:** 🔥

### Phase D exit criteria

- ✅ Environment destruction works on tagged props
- ✅ Reload drops magazine визуально
- ✅ Hot weapons show heat
- ✅ Penetration shows dual-impact

### Phase D estimated effort

**~10-15h.** Pick items based on playtest feedback after Phase A-C.

---

## Cross-cutting concerns

### DevCheats sections

Кожен phase introduces DevCheats config sections. Naming convention: `<FeatureName>Config` (e.g. `HitPauseConfig`, `CameraShakeConfig`). All sections нумеруються у `DevCheatsConfig.cs` + new SO file у `Assets/Scripts/Dev/Sections/` per CLAUDE.md §6.

### Ports / events extensions

- Possible new event signatures на `IRaidEvents`:
  - `DamageTaken(victim, source, position, damage)` — для DamageHud (B.3)
  - extension до `HitConfirmed` — add `Vector3 projectileDirection` parameter (для blood spray direction A.4)

### Test strategy

- **Unit tests:** logic systems (`HitPauseSystem`, `CameraShakeSystem`) — TestRunner verifiable
- **No automated tests:** view polish (particles, animations, HUD) — manual playtest only
- **Smoke tests after each phase:** "5 minutes of combat feels coherent."

### Reusable infrastructure

Items що можна reuse across phases:
- Particle pool helper (`A.4 → A.10 → A.6 → C.1`)
- Decal projector pool (`A.8 → A.10 → B.5 → B.6`)
- Material tag system (`A.7 → A.8 → D.1`)
- Camera shake (`A.3 → C.4 hooks via post-processing`)
- Time scale orchestrator (`A.1 → C.3`)

Build them once у Phase A; later phases extend.

---

## Dependencies + ordering rationale

```
A.1 (Hit Pause) ─────────────────────────────────────────► C.3 (Slow-Mo, extends)
A.3 (Camera Shake) ──────────────────────────────────────► B.1 (Recoil polish ties in)
A.4 (Blood Spray) ──┐
                    ├──► A.10 (Blood Pool) ──► B.5 (persistence) ──► B.6 (Bleeding visual)
A.8 (Bullet Holes) ─┘                                                          
A.7 (Material Tags) ──► A.8, D.1
A.9 (Ragdoll) ──► B.5 (body persistence)
B.4 (Stagger) — independent, async з animator content availability
C.1, C.2, C.5 — mostly independent of B
D.* — pick by playtest signal
```

Phase A items can mostly run у parallel after material system (A.7) lands. A.9 (ragdoll) — biggest single item, treat як side-quest.

---

## Estimated total effort

| Phase | Hours |
|-------|-------|
| A | 20-25 |
| B | 14-18 |
| C | 10-13 |
| D | 10-15 |
| **Total** | **54-71** |

Conservative estimate. Real timeline depends on art/animation availability for some items (B.4 stagger animation needs clips).

---

## Out of scope

- **Sound design** — explicit defer per user 2026-05-01. Treba окремий epic / Tier 9 audio track.
- **AAA-quality VFX art** — primitive-shape implementations are MVP. Real artist replacement — separate art track parallel.
- **Network / multiplayer feel** — single-player only.
- **Animation rigging deep dive** — using existing skeleton + procedural where possible.
- **Bot AI improvements** beyond hit reactions — own track.

---

## Related docs

- [`README.md`](../README.md) — epic overview + current state audit
- [`status.md`](./status.md) — decisions log, open questions, blockers
- [`../../weapon-builder/`](../../weapon-builder/) — paused parent feature
- [`../../battle-design-status.md`](../../battle-design-status.md) — armor / bleeding mechanics (already implemented)
- [`../../crosshair.md`](../../crosshair.md) — existing crosshair / hit marker / recoil visuals
