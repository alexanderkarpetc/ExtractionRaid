# Better Feel Gunplay — Epic

> Make moment-to-moment combat **feel visceral and impactful**. Кожний постріл повинен фізично відчуватись на gravity & inertia level — не просто number tick. Game feel без ваги — найшвидший шлях до "shooter feels like cardboard".

> 🎯 **Active 2026-05-01.** Поточний focus dev'у. Weapon Builder polish track ([`../weapon-builder/`](../weapon-builder/README.md)) paused до re-engage коли gunplay phase A-B converged.

---

## Vision

Top-down extraction shooter (reference: Escape from Duckov) живе або помирає на feel of every shot. У нашого проекту вже є solid foundation:
- Composition-based weapons (Weapon Builder Tier 0-2)
- 6 archetypes з distinct visual + mechanical profile (Tier 8)
- Real loot economy (Tier 6)
- Armor system з penetration / ricochet / bleeding feedback
- Crosshair + hit markers + damage numbers + helmet fly-off

Цей epic не додає нових систем — він **прокачує feel layer над existing gameplay** так, щоб гра відчувалась AAA-quality за моменти стрільби. Без звуку (deferred у наступну сесію).

**Hard goals:**
- Player feels physical weight per shot (camera, hitstop, reactions)
- Hits register visually на target (flash, blood, stagger)
- Death is satisfying (ragdoll, gibs, persistence)
- World remembers your impact (decals, blood pools, dust)
- Each archetype має distinct kinetic personality

**Hard non-goals (для цього epic):**
- Sound design — окремий epic
- New weapons / archetypes — Weapon Builder Tier 3 deferred
- New game mechanics — це pure polish, не feature
- AAA-quality art assets — primitive VFX OK поки artist drop-in pipeline reusable

---

## Quick resume для нової сесії

1. **Цей файл** — vision + current state
2. [`plan/roadmap.md`](./plan/roadmap.md) — phase structure (A-D), work items, effort/payoff per item
3. [`plan/status.md`](./plan/status.md) — decisions log, open questions, blockers

---

## Current state (2026-05-01) — what already works

### Hit feedback (basic)
- ✅ `Adapters/IRaidEvents.HitConfirmed(isKill, isHeadshot, absorptionRatio, isRicochet, hitPoint, targetedEntityId)` event pipeline
- ✅ `View/AimCursorOverlay.cs` — IMGUI crosshair з hit markers (white X), kill markers (red X), headshot (gold double-X), ricochet (blue spark). Bloom + reload ring. Charge dot ring (Laser).
- ✅ `View/DamageNumberOverlay.cs` — floating damage numbers з flight direction, headshot/kill flags, absorption ratio scaling
- ✅ `Systems/DamageSystem.cs` — emits `HitConfirmed` events with full context

### Impact VFX (assets exist, partially wired)
- ✅ Prefabs: `Assets/Resources/Vfx/Prefabs/Impacts/` — `ArmorImpact`, `BodyImpact`, `BulletImpact`, `HeadImpact`, `RicochetSpark`
- ✅ Decal model assets: `SM_Prop_BulletHoles_*.fbx`, `SM_Prop_BloodPool_*.fbx` (geometry exists, not yet projected as decals)

### Armor break feedback
- ✅ `View/ArmorBreakHelper.FlyOffHelmet` — physics-based helmet fly-off
- ✅ Proportional blood/sparks particles per armor absorption ratio
- ✅ Defender HUD + armor bar on healthbar + tooltip

### Recoil
- ✅ `WeaponView` procedural recoil kick (Tier 8 Wave D — body kicks back -Z, ease-out-quad recovery scaled to fire interval)
- ✅ Per-prefab `_recoilKickDistance` `[SerializeField]` — Inspector tunable

### What's missing (this epic builds it)
- ❌ Camera shake system (no impl)
- ❌ Hit pause / hitstop
- ❌ Hit flash on character (shader-based)
- ❌ Blood spray live impact (only armor break has particles)
- ❌ Casing ejection
- ❌ Material-specific impact VFX (single BulletImpact prefab — not material-aware)
- ❌ Bullet hole decal projection (assets exist but not used as decals)
- ❌ Ragdoll death system
- ❌ Stagger/flinch enemy animation на hit
- ❌ HUD damage feedback (vignette pulse, directional damage indicator, edge red glow)
- ❌ Real-time muzzle light
- ❌ Tracer / projectile trail VFX
- ❌ Recoil pattern polish (per-archetype tuning + DevCheats)
- ❌ Headshot special VFX (gibs, screen flash)
- ❌ Multi-kill / streak UI
- ❌ Slow-mo on critical kill
- ❌ Post-processing layers (chromatic aberration spike, vignette polish)
- ❌ Bleeding state visual (blood drips while alive, floor trail)
- ❌ Environment destruction (glass shatter, wood splinters, exploding barrels)

---

## Phase progress

| Phase | Scope | Status |
|------|-------|--------|
| **A** Foundation impact feel | Hit pause, hit flash, camera shake, blood spray, muzzle polish, casings, world impact, ragdoll death, decals | ⏳ NEXT |
| **B** Significant juice | Recoil polish, tracers, HUD hit feedback, enemy stagger, decal persistence, bleeding visual | ⏳ planned |
| **C** Wow moments | Headshot special, multi-kill streaks, slow-mo, post-processing, close-miss visual | ⏳ planned |
| **D** Advanced polish | Environment destruction, advanced physics, weapon heat states, pen dual-impact | ⏳ planned |

Detailed work items per phase: [`plan/roadmap.md`](./plan/roadmap.md).

---

## Архітектурні constraints (per CLAUDE.md)

- **VFX dispatch via events** (`IRaidEvents`) — не direct Unity API calls з systems. Events feed view layer які instantiate particles / shake camera / etc.
- **DevCheats parameterization** — кожна tunable величина (camera shake intensity, hit pause duration, recoil curves, blood spray amount) живе у `DevCheatsConfig` SO. View systems read через `RaidContext.*Config` structs (per CLAUDE.md §6).
- **Stateless systems** — gunplay systems (e.g. `CameraShakeSystem`, `HitFeedbackSystem`) — pure static. State (current shake offset, pending hitstop end-time) зберігається у `RaidState` чи view-local.
- **State не зберігає Unity refs** — particle instances, ragdoll bones — у view layer (`MonoBehaviour` containers), referenced by `EId` mapping.
- **No new singletons** — all coordination через `App.Instance.RaidSession` flows.
- **Test coverage** — pure-logic systems unit-tested. View polish — verified manually (це iterative tune work).

---

## Related docs

- [`plan/roadmap.md`](./plan/roadmap.md) — phase decomposition, work items, effort/payoff matrix
- [`plan/status.md`](./plan/status.md) — decisions log, open questions, blockers
- [`../weapon-builder/README.md`](../weapon-builder/README.md) — paused parent feature
- [`../battle-design-status.md`](../battle-design-status.md) — armor system + bleeding mechanics (already implemented)
- [`../crosshair.md`](../crosshair.md) — existing crosshair / hit marker / recoil visuals
- [`../weapons.md`](../weapons.md) — runtime weapon FSM, ADS, dual-layer aiming
