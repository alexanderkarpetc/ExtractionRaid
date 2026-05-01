# Better Feel Gunplay — Status

> **Living doc.** Tracks open questions, decisions, blockers per гри для polish epic. Updated after each work item or design call.

---

## Current phase

**🎯 Phase A — IN PROGRESS.** Foundation impact feel.

Track-wise:
- ✅ **A.1 Hit Pause / Hitstop** (2026-05-01)
- ✅ **A.2 Hit Flash on Enemy** (2026-05-01)
- ⏳ A.3 Camera Shake System
- ⏳ A.4 Blood Spray on Character Impact
- ✅ **A.5 Muzzle Flash + Real-time Light** (2026-05-01)
- ⏳ A.6 Casing Ejection
- ⏳ A.7 Material-Specific Impact VFX
- ⏳ A.8 Bullet Hole Decals
- ⏳ A.9 Ragdoll Death + Directional Knockback
- ⏳ A.10 Blood Pool Decal Under Body

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

1. ⏳ **Manual playtest A.1 + A.2 + A.5 у ShootingScene** — verify "feels different" baseline; tune DevCheats values if needed
2. After playtest signal: continue Phase A:
   - A.3 Camera Shake System (separate commit, ~2-3h)
   - A.4 Blood Spray on Character Impact (~3-4h with decal projection)
   - A.6 Casing Ejection (~3-4h with pool)
   - A.7 Material-Specific Impact VFX (~4-5h, includes scene tagging pass)
   - A.8 Bullet Hole Decals (~3-4h)
   - A.9 Ragdoll Death (~5-7h, biggest single item, own commit)
   - A.10 Blood Pool Decal (~2-3h)

3. Phase A exit criteria — see [`roadmap.md`](./roadmap.md#phase-a-exit-criteria).

---

## Related docs

- [`../README.md`](../README.md) — epic overview + current state audit
- [`./roadmap.md`](./roadmap.md) — phase decomposition, work items
- [`../../weapon-builder/`](../../weapon-builder/) — paused parent feature
