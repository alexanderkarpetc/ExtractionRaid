# Better Feel Gunplay — Status

> **Living doc.** Tracks open questions, decisions, blockers per гри для polish epic. Updated after each work item or design call.

---

## Current phase

**🎯 Phase A — IN PROGRESS.** Foundation impact feel.

Track-wise:
- ✅ **A.1 Hit Pause / Hitstop** (2026-05-01)
- ✅ **A.2 Hit Flash on Enemy** (2026-05-01)
- ✅ **A.3 Camera Shake System** (2026-05-01)
- ✅ **A.4 Blood Spray + Floor/Wall Decals** (2026-05-02)
- ✅ **A.5 Muzzle Flash + Real-time Light** (2026-05-01)
- ✅ **A.6 Casing Ejection** (2026-05-02) — primitive brass shell з hybrid auto-settle (linear damping ramp + kinematic freeze); player-walks-through after settle. Filtered by payload archetype ("Ballistic" only — laser/foam/rocket get future presenters).
- ⏸ **A.7 Material-Specific Impact VFX — DEFERRED** (2026-05-02). Scope removed — engaged later when (a) scene має real material variety (concrete/metal/wood/dirt zones), AND (b) per-material impact prefabs authored. Currently scene = uniform ProBuilder geometry, no payoff to taggng + per-material routing prematurely.
- ✅ **A.8 Bullet Hole Decals** (2026-05-02)
- ⏳ A.9 Ragdoll Death + Directional Knockback
- ⏳ A.10 Blood Pool Decal Under Body — could merge з A.4 floor decal pipeline

**Phase A 7/9 effective** (A.7 out of scope). Visible "feels visceral" baseline solid + reactive: hits register weight, characters glow on damage, weapons kick light + camera, blood marks ground/walls, casings tumble + settle. Remaining: A.10 (death pool) + A.9 (ragdoll).

Detailed work: [`roadmap.md` Phase A](./roadmap.md#phase-a--foundation-impact-feel).

---

## Open questions

### Design

- [ ] **Hit pause duration tuning.** Industry baseline 30-80ms, але це варіює по game-feel target. Start з recommended values (30/60/80/120 для normal/headshot/kill/critical), adjust per playtest.
- [ ] **Camera shake intensity scaling per archetype.** Small Pistol vs heavy Shotgun — наскільки різні? Recommend: Auto (2x base), Single (1.5x), Scatter (3x). Tune iteratively.
- [ ] **Blood spray gore level.** Stylized blood (red splash, no chunks) vs realistic gore (chunks, mist). Project art style — top-down з cute-character ("Player Spawn" з reference screenshot is anime-style chibi). Recommend stylized — heavy gore feels off для art direction.
- [ ] **Ragdoll persistence.** До end of raid? Або timer (30s, 60s, 5 min)? Trade-off: visceral feedback vs perf cost (ragdolls = many active rigidbodies). Recommend: 30s active physics → fade to static decoration → cleanup on raid end.
- [ ] **Decal limit per scene.** 200 active bullet holes? 500? Performance ceiling — perf testing needed на target hardware.

### Architecture

- [ ] **`IRaidEvents.HitConfirmed` extension — projectile direction.** Currently event has hitPoint + targetedEntityId, missing projectileDirection. Adding direction valuable for blood spray (A.4) + decal projection (A.8). Backward-compat: optional parameter з default.
- [ ] **Material tag system — runtime vs editor authoring.** Tag every collider у scenes manually (тedious) OR derive material kind from physics layer / texture name. Recommend: explicit `MaterialTag.cs` MonoBehaviour, manual tagging pass (~30-60 min), defaults to `Default` when missing.
- [ ] **Ragdoll setup per character.** One-time Editor wizard run per character prefab. Manual rig pose + Joint configuration. Fragile якщо new characters added — document workflow.
- [ ] **Decal pipeline — URP Decal Renderer Feature?** Unity URP має built-in decal projector з 2022.2+. Confirm project's URP version supports it. Alternative: quad-mesh-with-cutout shader.

### Production

- [ ] **Animation clip availability.** B.4 Stagger animation потрібен flinch clip (4-direction blend tree ideal). Якщо немає у project — procedural fallback (Transform lean) як interim. Real anim — Tier 9 / animator pass.
- [ ] **VFX art replacement timeline.** Phase A uses primitive shapes / existing PolygonApocalypse particles. Real artist VFX — separate track після Phase A converged.
- [ ] **Performance budget.** Particles + decals + ragdolls + post-processing — combined FPS impact на target hardware. Profile during Phase A, set caps if needed (already у DevCheats configs as MaxActive* fields).

---

## Decisions log

### 2026-05-02 — A.7 Material-Specific Impact DEFERRED out of Phase A scope

**Контекст:** після A.6 casings landed, обговорили скоп A.7 (concrete chunks vs metal sparks vs wood splinters per surface material). Conclusion: **engage later, не зараз.**

**Why deferred:**
- Scene currently uniform — usePlace ProBuilder primitives + Cubes на single default material. No material variety to differentiate.
- Зеро per-material impact prefab assets authored — would need creating new BulletImpact_Concrete/_Metal/_Wood prefab triplets (or quintuplets) before routing made sense.
- "Premature optimization" — building MaterialTag.cs scene tagging system + ProjectilePresenter routing without payoff data.

**Re-engage criteria:**
1. Scene has real material variety зон (e.g., concrete walls + metal sheet panels + wooden crates), AND
2. Per-material impact VFX prefabs authored (artist track), OR
3. Polygon pack discovered to уже have differentiated impact prefabs.

**Effort saved:** ~4-5h. Phase A scope shrinks 10 → 9 items; effective progress 7/9 = 78% done.

### 2026-05-02 — A.6 Casing Ejection shipped (with hybrid settle)

**Code:** `View/CasingEjectorPresenter.cs` listens `WeaponFired` event, spawns brass shell prefab `Resources/Prefabs/Casings/Casing.prefab` (primitive cylinder + brass URP Lit material + Rigidbody, scaled 10× for top-down camera visibility — final scale (0.12, 0.18, 0.12)).

**Hybrid settle pattern:**
- 0..SettleDelay (1.5s default): full juice — bouncy physics, base damping low
- SettleDelay..SettleDelay+SettleTimeout (1.5..2.5s): linear damping ramp base→max (30) — natural exponential decay
- After ramp: kinematic + collider disable → casing parked, player walks through without disturbance
- After Lifetime (6s): scale shrink fade у last 30% → despawn

**Filter:** event `WeaponFired` extended з optional `string payloadArchetype` parameter (filtered up через RaidEventBuffer.StringPayload). CasingEjectorPresenter spawns only on `"Ballistic"` archetype — laser/foam/rocket get future per-archetype ejection presenters (energy crackle, capsule drop, etc.).

**Live tunable у `Window → View Cheats → 🥃 Casings`:** mass, damping (base + max), settle delay/timeout, eject port offset, velocity components (lateral/up/back), spin, lifetime, max active.

### 2026-05-02 — A.8 Bullet Hole Decals shipped

**Reused** `DecalProjectorPool` infrastructure от A.4 — kind=3 для bullet holes, separate capacity (default 200 active, 90s lifetime). Авторовані prefabs `PolygonApocalypse/Prefabs/Props/SM_Prop_BulletHoles_01..03` (variants 04 і 05 — multi-hole "spray" patterns, excluded бо мatch'ать 1-per-shot logic).

**Event schema extension:**
- `CollisionSignal` → added `Vector3 Normal`
- `ProjectileView.ReportHit` passes `hit.normal` from SphereCast
- `IRaidEvents.ProjectileHit(id, position, normal, hitType)` — packed normal у `Direction` field
- `RaidSession`/`DamageSystem` emit sites updated; `FakeRaidEvents` interface conformance
- DamageSystem character-hit calls pass `Vector3.zero` normal (presenter filters by zero check + hitType prefix)

**Bug fix landed:**
- *"Trail line" cluster*: top-down camera shoots horizontal → all wall hits land на same Y → bullet holes form perfect horizontal track. Fixed via `ComputeSurfaceJitter(normal, upJitter, rightJitter)` — random offset projected onto surface plane, vertical-biased (default UpJitter=0.15m, RightJitter=0.05m). Same fix applied до blood wall splatter (BloodDecalPresenter.SpawnWallDecal).

**Throttle:** per-bucket (10cm grid) з 0.05s window — prevents auto fire stacking decals в same spot.

**ViewCheats:** new `🔫 Bullet Holes` section з 200/90s defaults. Live tunable.

### 2026-05-02 — A.4 Blood Decals shipped + ViewCheats infrastructure + layer convention

**Standalone commit після A.3 (Camera Shake) landed.** A.4 turned out to require less than planned — particle layer вже existed (ProjectilePresenter spawns BodyImpact/HeadImpact на ProjectileHit event since prior work). Only decal layer needed.

**New code:**
- `View/DecalProjectorPool.cs` — reusable pool, kind-keyed (FloorBlood=1, WallBlood=2; future BulletHole/BloodPool kinds use same API). Bounded active count (oldest replaced). **Ease-out-cubic scale shrink у last 30% lifetime** замість alpha fade — universal across opaque + transparent shaders.
- `View/BloodDecalPresenter.cs` — listens `EntityHit`, resolves character center via `RaidState` lookup (PlayerEntity / Bots[]), raycasts down (floor) + forward (wall) using layer mask. Per-target throttle + spawn chance gate prevent decal spam from auto fire.
- `View/LayerUtils.cs` — runtime-authoritative layer assignment. Constants для project layers (Player=6, Bot=7, FOV=8 etc) + `SetLayerRecursively` helper. PlayerPresenter/BotPresenter override prefab-baked layer per-instance (CharacterBody prefab is shared).
- `Dev/Sections/ViewCheatsBloodDecalSection.cs` — config (Enabled, MaxFloor=100, MaxWall=30, Lifetime=30s, throttle 0.3s, spawn chance 0.7, FloorRandomRadius 0.4m, scale ranges, layer offsets).

**Tuning landed:**
- `ViewCheats.CameraShake.GlobalScale` tuned до **0.2** during playtest (1.0 default felt too aggressive for the new blood/hit feedback стек).

**Iterative refinements caught у playtest:**
1. Decals у початку spawned grey (FBX models import з default Lit material). Switched до authored prefabs at `PolygonApocalypse/Prefabs/Props/SM_Prop_BloodPool_01..05` — proper blood material/texture.
2. Decals спочатку stuck до characters (fcrer `attachedRigidbody != null` failed for NavMeshAgent-based bots). Replaced runtime filter з layer mask exclusion (Player/Bot/FOV/UI/IgnoreRaycast). Added `LayerUtils.SetLayerRecursively` calls у Player/Bot presenter spawn — runtime authoritative.
3. Decals slightly tilted + sometimes submerged. Pool's spin rotation was around forward axis (horizontal); fixed by rotating around surface normal через `Quaternion.AngleAxis(angle, surfaceNormal) * Quaternion.FromToRotation(Vector3.up, surfaceNormal)`. Bumped FloorOffset 5mm → 20mm.
4. Decals popped instantly при cleanup. Pool's alpha fade didn't work bc material is Opaque (URP Simple Lit ignores alpha component). Switched до **scale shrink** (ease-out cubic): visually smooth, shader-agnostic.
5. Decal spawned at hit point (upper body) — visually weird. Now resolves character center from RaidState + adds random XZ offset (FloorRandomRadius 0.4m default) for organic placement.

**Architecture note: DecalProjectorPool ready для reuse.** A.8 (bullet holes), A.10 (blood pools під трупами), B.6 (bleeding floor trail) — all hookable via same API (`Spawn(kind, prefabs, position, rotation, lifetime, scale)`). Capacity per kind set independently через `SetCapacity(kind, n)`.

### 2026-05-01 — Phase A bundle 1 shipped: A.1 + A.2 + A.5

**Bundled commit:** highest-impact code-side polish landed first для immediate "feels different" baseline. Manual playtest pending (ShootingScene available).

**A.1 — Hit Pause / Hitstop (`View/HitPausePresenter.cs`):**
- Stateful presenter registered у `App.LateTick` після existing presenters
- Listens до `RaidEventType.HitConfirmed`, schedules pause end via `Time.unscaledTime`
- Sets `Time.timeScale = cfg.PausedTimeScale` while active, restores to `1f` on expire
- Per-event durations (DevCheats): Normal 30ms / Headshot 60ms / Kill 80ms / Ricochet 20ms
- `Dispose()` releases timeScale on shutdown — prevents stuck slow time on scene swap

**A.2 — Hit Flash (`View/BotView.TriggerHitFlash` + `BotPresenter.ApplyHitFlash`):**
- New `IRaidEvents.EntityHit(targetEid, hitPoint, projectileDirection, isHeadshot, isRicochet, isKill, absorptionRatio)` event
- DamageSystem emits EntityHit at all hit sites (regardless of projectile owner) — separate from HitConfirmed (player crosshair)
- BotView caches `Renderer[]` + `MaterialPropertyBlock`; tints `_BaseColor` + `_Color` toward flash color, ease-out-quad decay over `cfg.Duration`
- Color priority: Ricochet (blue) > Kill (red) > Headshot (gold) > Normal (white)
- Uses `Time.unscaledDeltaTime` so flash decay continues during hit pause

**A.5 — Muzzle Light Pulse (`View/WeaponView`):**
- New `[SerializeField] Light _muzzleLight` — auto-creates Point Light child of MuzzlePoint at runtime if prefab didn't wire one (zero-config visible pulse)
- DevCheats config drives intensity (12 default), color (warm orange), range (3m), duration (60ms)
- Ease-out-quad decay у same `Update()` as recoil kick
- Uses `Time.unscaledDeltaTime` for crisp pulse during hit pause

**Cross-cutting:**
- 3 new DevCheats sections: `DevCheatsHitPauseSection`, `DevCheatsHitFlashSection`, `DevCheatsMuzzleVfxSection` (per CLAUDE.md §6)
- Wired у `DevCheatsConfig`, `DevCheatsWindow` (DrawSection + CreateSectionIfMissing)
- 3 SO assets created у `Resources/Configs/DevCheats/` via Window menu
- `RaidEvent` packs EntityHit fields without struct extension (Id=targetEid, Position=hitPoint, Direction=projDir, Damage=absorption, CurrentHp/MaxHp/KillerId.Value=flags)
- `FakeRaidEvents` extended з `EntityHits` list для test fixture

**Test status:** 434/434 EditMode tests pass (no test changes required — view-only polish, no logic regression).

**Manual playtest pending:** ShootingScene → перевірити hit pause "feels weighty", flash visible on Row 1-3 immortal targets з different damage tiers, Row 8 weak bots для kill flash, Row 9 armor for ricochet feedback. Tune DevCheats values if перевідчуття "too soft" / "too aggressive".

### 2026-05-01 — Epic spawned, polish-first pivot from Weapon Builder

**Контекст:** Weapon Builder Tier 6 + Tier 8 closed core "raid → loot → build" loop + visible 2-module composition. Discussion про next steps revealed: real "feels great" goal — це cross-cutting gunplay polish (hit pause, camera shake, blood, decals, ragdoll, recoil polish, headshot juice, etc.) — не Weapon Builder feature work.

**Decision:** Pause Weapon Builder roadmap. Spawn separate **Better Feel Gunplay epic.** Comprehensive list of polish items decomposed по 4 phases (A foundation → B juice → C wow → D advanced).

**User quote:** "взагалі всі пункти офігенні! Давай тоді зробимо паузу в роботі з weapon builder і почнемо роботу над іншим epic - better feel gunplay."

**Scope:**
- ✅ In scope: hit feedback, camera, blood, ragdoll, decals, recoil polish, HUD damage feedback, headshot juice, post-processing
- ❌ Out of scope: sound design (defer), new weapons/content (Weapon Builder Tier 3 still deferred), animation rigging beyond what's needed
- ❌ Strategic: stay focused on existing 6 archetypes (Ballistic/Laser × Pistol/Rifle/Shotgun) — не scope для hypothetical 4×5 matrix

**Brainstorm anchor list:** [README.md current state](../README.md#current-state-2026-05-01--what-already-works) details what already exists у project (HitConfirmed events, AimCursorOverlay markers, DamageNumberOverlay, helmet fly-off, ArmorBreakHelper, impact prefabs у `Resources/Vfx/Prefabs/Impacts/`) → не дублюємо work, building над foundation.

**Re-engage Weapon Builder:** після Phase A-B converge ("feels physical" baseline shipped) → revisit Tier 8.x follow-ups + Tier 4a bot migration з gunplay-aware lens. Може деякі items уже covered (e.g. Tier 8.x reload procedural motion overlap з Phase A camera/recoil work).

---

## Blockers

*Нічого блокуючого не зафіксовано. Якщо з'явиться — додаємо сюди з контекстом і owner'ом.*

---

## Next actions

Phase A 7/9 effective (A.7 deferred). Remaining 2 items:

1. ⭐ **A.10 Blood Pool Under Body** (~1-2h) — extends `DecalProjectorPool` з new kind. Triggers on `EntityDied`. Larger persistent pool at character feet — death "marker" distinct від A.4 splashes (which are per-hit small splatters).
2. **A.9 Ragdoll Death** (~5-7h, biggest item) — manual character rigging (Joint setup per Character01 prefab — one-time tedious work) + `RagdollController` toggle on death event. Force-based ragdoll з shot direction.

Recommend A.10 first (quick win), then A.9 (Phase A finale).

Phase A exit criteria — see [`roadmap.md`](./roadmap.md#phase-a-exit-criteria).

---

## Related docs

- [`../README.md`](../README.md) — epic overview + current state audit
- [`./roadmap.md`](./roadmap.md) — phase decomposition, work items
- [`../../weapon-builder/`](../../weapon-builder/) — paused parent feature
