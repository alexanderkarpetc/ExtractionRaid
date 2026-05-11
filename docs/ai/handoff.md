# Handoff — current state

> Snapshot for next-session agent. Combined state + recent landings (last ~2 weeks). Updated 2026-05-12.

## Context

Top-down extraction shooter (Unity 6000.3.10f1, URP 17.3). 5-layer architecture (App → Session → Systems → Adapters → View/Presenter) per [`CLAUDE.md`](./CLAUDE.md). Ukrainian-language collaboration, tight iterations.

---

## Earlier landings (2026-05-04..06) — feature-level summary

**Tier 4a — bot weapon migration (2026-05-04):** All bots (Scav/PMC/Boss/Target*) go through Builder pipeline. `BotConstants.BotTypeConfig.WeaponConfig` (struct) replaces 7 legacy fields + `WeaponPrefabId` string. Bot projectiles carry full stat set (Penetration, ArmorDamage, BleedChance, HeadshotMultiplier). Bot kills drop weapons with current ammo state.

**Tier 8.x* — weapon asset architecture rebuild (2026-05-05):** Payload/delivery prefab roles inverted from original intuition. New shape:
```
Module_Payload_*.prefab (root has Animator + WeaponView)
├── KickGroup
│   ├── PayloadBaseMesh
│   ├── DeliverySocket (delivery instantiates here at runtime)
│   └── RightHandGrip
Module_Delivery_*.prefab (no MonoBehaviours)
├── DeliveryBarrelMesh
└── MuzzlePoint (Y synced with ProjectileSpawnHeight)
```
5 prefabs total (2 payloads × 3 deliveries via `Tools → Weapon Builder → Create Module Prefabs`). + Weapon-on-death drop physics: `RagdollPresenter.TryDropWeapon` reparents to `[WeaponDropPool]` with Rigidbody + impulse along shot direction.

**P0-1 — DevCheats → Config refactor (2026-05-05):** `ArmorSystem`, `PlayerFOVSystem`, `MovementSystem` no longer read `DevCheats.X` directly. Tunables flow through `RaidContext.*Config` structs populated in `RaidSession.Tick`. Per CLAUDE.md §6.7.

**Cap enforcement (2026-05-05):** `ArmorConstants.PenetrationCap` / `ArmorDamageCap`. Hardcoded constants, no config layer (won't change often).

**Weight → mobility coupling (2026-05-05):** `weight = ArmorPts + MaxDur` per slot summed; multiplier = `max(0.5, 1 - weight × 0.0005)`. Applies in MovementSystem after sprint/ADS scales.

**Combat polish day (2026-05-06):**
- VibeCharacterShader (~330 LOC hand-written URP, replaces Amplify-generated MainBase) — 4 passes + rim flash + bullet decals via MaterialPropertyBlock
- `CharacterHitFx` — survives ragdoll detach; rim flash + per-bone bullet decals (ring buffer 8, follows live animation AND ragdoll physics)
- Blood VFX rework — 4 sub-emitters (center splat / cone splash / mist / wound flash), `DamageSystem` passes `proj.Direction`, ~14 particles/hit
- Tau cannon charge mechanic: AttackPressed → Charging; AttackJustReleased → fires at `chargeRatio = clamp((elapsed - chargeStart) / chargeTime, 0, 1)`; damage scales `lerp(0.3, 1.0, chargeRatio)`
- Laser rifle burst (Laser + Auto): `burstCount = round(lerp(1, 6, chargeRatio))`, interval 0.07s; `WeaponPhase.Bursting`
- `BeamFlashPresenter` — Tau-style electric LineRenderer per pellet (laser shotgun gets 7 beams), 10-vertex jagged path + sin envelope + per-frame re-randomization
- Laser trail swap — `TrailBullet02` for archetype `"Laser"`
- Cheat starting loadout — all 6 archetypes equipped + helmet/armor + grenade/medkit/bandage

---

## Recent landings (2026-05-09..12)

### Weapon pullback extended to characters (2026-05-09)

`View/CharacterBody.LateUpdate` weapon-barrel pullback (Solution 3a, lands shots inside walls) was originally filtering OUT other characters (`PlayerView`/`BotView` layers excluded). Pain point: barrel poking visually through enemies + projectile spawning past them at point-blank.

**Fix:** local pullback mask = `VisionBlockingMask | (1 << Player) | (1 << Bot)`. Replaced the `PlayerView`/`BotView` skip with a `RagdollController` skip (corpses don't twitch the barrel). Live characters now retract the muzzle the same way walls do.

Files: `View/CharacterBody.cs` (mask + filter changes).

### ProjectileView start-overlap probe (2026-05-09)

Combined with the above pullback, point-blank shots spawn the bullet INSIDE the target capsule. Unity `SphereCast` returns no hit when the sphere starts inside a collider (`queriesHitBackfaces = false`) → silently flew past.

**Fix:** `Physics.OverlapSphereNonAlloc(oldPos, hitRadius)` probe before the existing SphereCast. First non-projectile damageable found → register hit immediately (hit point = oldPos, normal = -direction). Static `Collider[8]` buffer, no GC.

Files: `View/ProjectileView.cs`.

### Ragdoll layer isolation (2026-05-09)

Live ragdoll bones were physics-pushed by the walking player → corpses slid like rag dolls for ~5s after death. Now ragdolls move to a dedicated `Ragdoll` layer (9) at activation; runtime `Physics.IgnoreLayerCollision(Ragdoll, Player)` + `(Ragdoll, Bot)` configured in `LayerUtils.InitCollisionMatrix()` (called from `App.Initialize`).

Result: walking through fresh corpses is no-collide. Body still falls + lands on Default-layer ground. Bullets still hit corpses (DefaultRaycastLayers includes layer 9). Casings still bounce off (Default × Ragdoll collision intentional — natural-looking).

Files: `View/LayerUtils.cs` (Ragdoll const + matrix init), `View/RagdollController.cs` (layer switch on Activate), `ApplicationCore/App.cs` (call site), `ProjectSettings/TagManager.asset` (layer 9 named).

### Hit decals on one-shot kills + freeze on corpses (2026-05-09)

Two related bugs:

**Bug 1 — no decal on one-shot kill.** DamageSystem emits `EntityHit` + `EntityDied` same tick. RagdollPresenter runs before BotPresenter in `App.LateTick` → releases body + removes from `_views` → BotPresenter's EntityHit handler can't find the bot → decal dropped.

**Bug 2 — decals on corpses fade after 8s.** `CharacterHitFx.Update` decayed intensity over `DecalLifetime` regardless of life state.

**Fixes:**
- `BotPresenter._releasedHitFx[EId → CharacterHitFx]` registry. `TryReleaseCharacterBody` captures the body's `CharacterHitFx` before destroying the shell. EntityHit handler falls back to the registry when `_views` misses.
- `CharacterHitFx.FreezeDecals()` — disables intensity decay (decals stay until body destroys). Called from `RagdollController.Activate`.

Files: `View/BotPresenter.cs`, `View/CharacterHitFx.cs`, `View/RagdollController.cs`.

### Bot melee + Zombie type + horde test scene (2026-05-10)

Crowd-shooting test infrastructure. New ingredients:

- **`BotBehaviorFlags.MeleeAttack`** (bit 7).
- **`BotTypeConfig.MeleeAttackRadius / Damage / Cooldown`** fields.
- **`MeleeAttackNode`** — sits before Shoot/Chase in Engage selector. Sets `WantsToMeleeAttack = true` when target inside radius. Wrapping `BTCooldown` rate-limits via `MeleeAttackCooldownTimer`.
- **`BotCombatSystem.ProcessMeleeAttack`** — reads intent, calls `DamageSystem.ApplyMeleeDamage(state, target, dmg, attacker, hitPoint, dir, ctx)`.
- **`DamageSystem.ApplyMeleeDamage`** — direct HP damage (no projectile / armor pipeline for V0.1). Emits `EntityDamaged` / `EntityDied` / `EntityHit` matching projectile path.
- **`BotConstants.Zombie`** config — `Chase | MeleeAttack`, vision 999/360°, chaseSpeed 2.8, melee radius 1.6, damage 12, cooldown 1.0s. Carries PistolWeapon as visual placeholder.
- **`HordeSpawnSystem`** — runtime wave spawner gated on `LevelId == "horde_range"`. Ring spawn around player, grace period, max-alive cap, runtime HP override. Driver: `DevCheatsHordeSection`.
- **`ShootingScene_Horde.unity`** — cloned from KillFeel. AppBootstrap level id = `horde_range`.

Files: `BotConstants.cs`, `BotEntityState.cs`, `BotBlackboard.cs`, `Systems/Bot/Nodes/MeleeAttackNode.cs`, `Systems/Bot/BotTreeBuilder.cs`, `Systems/Bot/BotCombatSystem.cs`, `Systems/DamageSystem.cs`, `Systems/HordeSpawnSystem.cs`, `Systems/PlayerSpawnSystem.cs`, `Session/RaidSession.cs`, `State/RaidState.cs`, `Dev/Sections/DevCheatsHordeSection.cs`, `Dev/DevCheatsConfig.cs`, `Editor/DevCheatsWindow.cs`, `Tests/EditMode/MeleeAttackTests.cs`, `Tests/EditMode/HordeSpawnSystemTests.cs`.

### Lock-on convergence override (2026-05-10)

Bug: at certain top-down + side-angle configurations, the XZ-blended direction (`Lerp(parallax, convergence, 0.317)`) landed the trajectory just past the enemy capsule edge — bullet looked right on screen but missed in 3D.

**Fix:** `if (convergence.HasValue && targetDamageable != null) blend = 1f;` in `ShootingSystem.Tick`. When cursor is on a damageable, direction snaps to full convergence (3D-accurate). Non-damageable (walls, ground, empty) keeps user-tuned blend → visual feel preserved.

Files: `Systems/ShootingSystem.cs` (lookup of `targetDamageable` moved up, blend override added with detailed comment).

### Semi-auto trigger gate (2026-05-10)

`AttackPressed` is held-state — pistol/shotgun fired continuously while holding LMB (cooldown only). Now Single/Scatter patterns require `AttackJustPressed` (rising edge).

**Implementation:**
- New `IInputAdapter.AttackJustPressed` — `WasPressedThisFrame()` in `UnityInputAdapter`, auto-property in `FakeInputAdapter`.
- In `ShootingSystem.Tick` after pattern dispatch:
  ```csharp
  bool semiAuto = pattern == FiringPattern.Single || pattern == FiringPattern.Scatter;
  if (semiAuto && !releaseFire && !input.AttackJustPressed) return;
  ```
- `releaseFire` bypass keeps laser-charge release path working (laser pistol/shotgun = Single/Scatter pattern, but fire trigger is AttackJustReleased, not AttackJustPressed).

All 469 existing tests stayed green — `EditModeTestsUtils.NewPistolLikeWeapon` hand-builds state without `DeliveryDefinition` SO, so dispatch falls back to Auto and skips the gate. Real-game path through `WeaponSyncSystem.BuildWeaponForItem` resolves the SO → gate is active.

Files: `Adapters/IInputAdapter.cs`, `Adapters/UnityInputAdapter.cs`, `Tests/EditMode/Fakes/FakeInputAdapter.cs`, `Systems/ShootingSystem.cs`.

### DevCheats / ViewCheats window unification (2026-05-10)

User pain: knowing which window has a given tunable. Decision: keep on-disk asset split (DevCheats = gameplay, ViewCheats = visual), unify the editor UI.

- `DevCheatsWindow` now renders both DevCheats sections + ViewCheats sections in one scrollable view, separated by a `🎨 View polish` banner.
- `ViewCheatsWindow.cs` deleted (zero external refs).
- `Raid/Dev Cheats — Create Section Assets` menu bootstraps BOTH Dev + View sections (so no orphan menu after deleting ViewCheatsWindow).
- All custom editor menus moved from `Window/` to `Raid/` namespace: Dev Cheats, Dev Cheats — Create Section Assets, Raid State Debugger, BT Debugger.

Files: `Editor/DevCheatsWindow.cs`, `Editor/RaidStateDebuggerWindow.cs`, `Editor/BotBTDebuggerWindow.cs`, `Editor/ViewCheatsWindow.cs` (deleted), plus stale-path string fixups across docs.

### Ranged-combat test scene (2026-05-12)

New `RangedTarget` bot type (streamlined PMC — vision 70m, engage 50m, Chase+Shoot only, no grenade/heal/dodge). `RaidSession.SpawnRangedRangeTargets()` spawns 7 bots across 4 distance zones at level id `"ranged_range"`. Scene `Assets/Scenes/ShootingScenes/ShootingScene_RangedRange.unity` — Plane 200×200, NavMesh baked, 13 cover cubes for varied combat scenarios (close open / mid lane-split / mid-far corner / long range with central wall).

Files: `BotConstants.cs` (`RangedTarget` config + Registry entry), `Session/RaidSession.cs` (Spawn method + level branch), `Systems/PlayerSpawnSystem.cs` (starting-loadout gate).

### Doc consolidation (2026-05-12)

Removed all references to indefinitely-deferred backlog items per user call: Char skill tree, WeaponMod stat composition, Sound design, and gunplay items A.7/A.10/B.2/B.5/B.6/C.1-5/D.3. Deleted `docs/ai/rpg-modifier-system.md` (file's purpose was the 4-source modifier pipeline = deferred). `gunplay/plan/roadmap.md` + `status.md` deleted; content collapsed into `gunplay/README.md` as state summary. Battle-design formulas simplified to actual `WeaponBase + AmmoMod` shipped path.

### Bot debug overlay split (2026-05-10)

`BotDebugLabel` floating text "[Type] Status [SEE] Dist: X / HP: x/y" was monolithic. Split into two ViewCheats toggles:

- `ViewCheatsBotDebugSection.ShowHpText` — the HP line
- `ViewCheatsBotDebugSection.ShowStatus` — the brain status + SEE + Dist line

Plus defensive corpse hide: when `currentHp <= 0` the label hides even if the shell hasn't despawned yet. StringBuilder-based text assembly — eliminated per-frame string allocs from the previous `$"..."` interpolation.

HP bar (`WorldHealthBar`) now follows FOV visibility too — `SetVisible(bool)` driven by the same `IsVisibleToPlayer` flag that gates the 3D mesh renderers. Previously the bar showed through fog-of-war occlusion (Image uses CanvasRenderer, not Renderer, so it wasn't caught by the existing `GetComponentsInChildren<Renderer>()` loop).

Files: `View/BotDebugLabel.cs`, `View/WorldHealthBar.cs`, `View/BotView.cs`, `Dev/Sections/ViewCheatsBotDebugSection.cs`, `Dev/ViewCheatsConfig.cs`.

---

## Current state

- **469/469 EditMode tests green.** (+13 since 2026-05-06: 6 MeleeAttackTests + 7 HordeSpawnSystemTests.)
- Compile clean (only pre-existing warnings — `FindObjectOfType` obsolete API, QuestDefinition missing type).
- 6 weapon archetypes feel coherent (Ballistic/Laser × Pistol/Rifle/Shotgun); semi-auto pistol/shotgun, full-auto rifle, laser charge-release.
- 4 test scenes: `ShootingScene` (armored), `ShootingScene_KillFeel` (low-HP), `ShootingScene_Horde` (zombie waves), `ShootingScene_RangedRange` (ranged combat with cover).
- All landings committed.

---

## What's deferred / open

### Gunplay backlog
See [`gunplay/README.md`](./gunplay/README.md) — combat-polish epic converged. Remaining active candidates: HUD damage feedback, magazine drop physics.

### Battle design open
- Bleed L2 DPS values (playtest-driven)

### Test debt (unchanged)
- BT primitives 0 tests
- Burst tests deferred
- P0-2 test factory consolidation

### Architecture debt
- ✅ DevCheats/ViewCheats UI fragmentation — resolved (unified window)
- ✅ `.cursor/rules/bot-ai.mdc` mirror — created 2026-05-12

### Test scenes available
- `Assets/Scenes/HideoutScene.unity` — main hub
- `Assets/Scenes/ShootingScenes/ShootingScene.unity` — armored targets (10000 HP)
- `Assets/Scenes/ShootingScenes/ShootingScene_KillFeel.unity` — low-HP targets (10/25/50/75/100)
- `Assets/Scenes/ShootingScenes/ShootingScene_Horde.unity` — zombie wave spawner, 5s grace, 360° ring spawn
- `Assets/Scenes/ShootingScenes/ShootingScene_RangedRange.unity` — 7 RangedTarget bots across 4 zones with 13 cover cubes, NavMesh baked

---

## User profile reminders (carried forward)

- Ukrainian communication, terse iterations
- Quick tweak → playtest loop. Commits frequently.
- Wants "feel" iteration with many recursive micro-tweaks
- Will revert speculative complexity readily (recoil patterns, heat haze — both tried-and-reverted in prior sessions; recorded in gunplay/README.md "Tried + Reverted")
- Pragmatic — accepts hardcoded constants where DevCheats overhead doesn't pay
- DOES NOT want speculative tests for non-feature work
- Wants to know cost+value before agreeing to architecturally-large items
- Prefers single-window UX (DevCheats unification 2026-05-10) over conceptual purity that fragments tools

---

## How to start the next session

1. Read this + `gunplay/README.md` + `battle-design-status.md`.
2. `git log -15` for recent commits.
3. Verify `mcp__unityMCP__run_tests` EditMode → 469/469.
4. Sync with user on direction. Likely candidates:
   - Remaining gunplay backlog (HUD damage feedback, magazine drop physics)
   - Bleed L2 DPS tuning (playtest-driven)
   - Test debt closure (P0-2/3/4/5 — see `tests-review.md`)
5. Doc sync: `docs/ai/*.md` mirrors `.cursor/rules/*.mdc` per CLAUDE.md §8.

---

## Architectural rules to keep tight

- Systems must not call `App.Instance` (only `Player`/`RaidSession` excepted). Tunables travel via `RaidContext.*Config` structs populated in `RaidSession.Tick`.
- State stores values + IDs only — no Unity refs.
- View/Presenter no gameplay rules.
- Stateless static systems.
- VibeCharacterShader property names stable — don't rename without material edits.
- `CharacterHitFx` excluded from RagdollController's MB-disable loop (preserved decals on ragdoll bones).
- ProjectileView OverlapSphere probe must precede SphereCast (point-blank reliability).
- Ragdoll layer (9) must remain excluded from weapon-pullback mask + collision-ignored with Player/Bot.

---

**Status as of session end:** All committed. 469/469 green. Heat-haze experiment reverted. Docs synced (weapons.md, weapons.mdc, bot-ai.md). Ready for fresh session.
