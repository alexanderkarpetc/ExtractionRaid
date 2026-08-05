# Handoff — current state

> Snapshot for next-session agent. Combined state + recent landings. Updated 2026-08-05.
>
> **📍 Canonical plan:** [`release-scope.md`](./release-scope.md) (feature-map + gaps + 🔒 locked decisions) + [`v1.0-roadmap.md`](./v1.0-roadmap.md) (milestones **M1–M4**; statuses reconciled with the repo 2026-08-05, incl. a list of parallel work outside M1–M4). These supersede the ad-hoc "Next candidates" below.
>
> **Baseline: 700 EditMode tests green** (2026-08-05).

## Context

Top-down extraction shooter (Unity 6000.3.10f1, URP 17.3). 5-layer architecture (App → Session → Systems → Adapters → View/Presenter) per [`CLAUDE.md`](./CLAUDE.md). Ukrainian-language collaboration, tight iterations.

---

## Most recent — Raid timer (M1.2, 2026-08-05)

The raid clock, the missing half of the risk loop: gear-loss (M1.1) made death cost something, this
makes *staying* cost something. **Time out → KIA**, deliberately through the ordinary death path —
`RaidTimerSystem` → `DamageSystem.KillEntity` → `EntityDied` → `ProcessDeathEvents` → `RaidOutcome.KIA`
→ gear wipe in `App.EndRaid`. No second exit-from-raid route to keep in sync.

- **No timer state.** The countdown is derived (`RaidState.RaidDurationSeconds − ElapsedTime`), so
  there is nothing to reset or desync. `RaidTimerSystem.TimeRemaining/HasClock` are the read helpers
  the HUD, the debugger and the end-of-raid screen share.
- **0 = no clock**, which is how the hideout, `test_level` (TestScene) and the shooting ranges opt out
  — see `RaidSession.ResolveRaidDuration`. `test_level` is checked there rather than folded into
  `PlayerSpawnSystem.IsTestRange`, because that predicate also hands out the 6-weapon cheat loadout.
- **`DamageSystem.KillEntity`** is new and reusable for future environment kills (gas, out-of-bounds):
  attacker-less lethal damage that emits `EntityDied` but no `EntityHit` — there is no impact to
  render, and blood from a nonexistent bullet reads as a bug.
- **Ignores GodMode on purpose** (it is a combat cheat, not a clock cheat). The escape hatch for long
  playtests is duration `0` in `Dev Cheats → ⏱ Raid clock` (also holds warn/critical thresholds).
- **UX:** `MM:SS` pill top-centre in `BattleHudOverlay` with warn (120s) / critical (30s) looks, hidden
  on clock-less levels; the death screen says **"TIME'S UP"** instead of "YOU DIED" when the clock ran
  out — no extra state, the KIA path leaves the session live so the presenter can just read it.
- Extraction completed in the same frame wins the tie (`ExtractionSystem` ticks first).
- **7 tests** (`RaidTimerSystemTests`) → **700 EditMode green**.

---

## Most recent — Ammo audit + render fixes (2026-07-27 → 08-05)

**Ammo audit (committed `remove legacy ammos`).** After the Weapon Builder landed, the item registry
still carried calibers no payload core declares. `AmmoSystem` chambers by **exact id** — so those were
unloadable loot the game actively handed out: the starting Ammo Box gave 30-40 `Ammo_Pistol`, PMCAuto
bots dropped 25% `Ammo_Rifle_AP` (`ammoApWeight: 0.25`), and Rifle AP was craftable for
Military_Components. All five orphan calibers (`Ammo_Pistol`, `Ammo_Pistol_AP`, `Ammo_Pistol_HP`,
`Ammo_Rifle_AP`, `Ammo_Rifle_HP`) are **deleted end-to-end**, together with the machinery that could
turn them back on:

- `AmmoLootRule` lost `StandardWeight/ApWeight/HpWeight` → now just `MinRounds/MaxRounds`;
  `LootSystem.DropAmmoVariants` → **`DropCaliberAmmo`** (caliber resolved from the payload, never
  authored); same simplification mirrored in `Systems/Meta/RegionLootSimulator` (**owner's file —
  mechanical edit, flagged**).
- Shop `Ammo_Pistol` → **`Ammo_EnergyCell`**, starting boxes → `Ammo_Rifle`, `RifleAPAmmo` recipe
  dropped, **new `EnergyCellAmmo` recipe** (laser ammo previously had *no* buy/craft source at all).
- Guard: **`AmmoAvailabilityTests`** (5 cases, reads shipped assets) — shop/containers/craft may only
  offer chamberable ammo, every ammo definition must be chamberable, and every chamberable caliber
  needs a restock source. The usable set is derived from `CoreDefinitionDatabase` payloads, so it
  widens by itself when a payload (or ammo selection) is added.

**Quest indicator stuck-on fix** (`quest giver update`). `NpcQuestIndicator.SetVisible` early-outs when
the state matches, so the initial hide in `Build()` never reached the just-created (active) children —
an indicator rebuilt with no offer pending (hideout reload after a raid) stayed lit forever: badge +
glow with no quest behind it. Fixed with a first-application flag.

**Foliage bloom blowout fix** (`back face fill fix`). `BushWindCutout` added `tex.rgb * _BackFaceLight`
on top of the finished PBR result — unbounded, so sunlit back-faces crossed the bloom threshold and
wind-animated leaf quads popped as drifting coloured blobs. The term now fades as the lit result
approaches white; Bloom `threshold` 1.0 → 1.4 in `SampleSceneProfile`.

**From other contributors (per commits, not deep-reviewed):** loot rework + `LootBalanceTests`
(Олександр), weapon presets `Configs/Guns/*_T1..T3`, bot behavior variety (`ShootNode`), progression
**material-cost** system, meta auto-raid simulator (`RaidCombatSimulator`, `MetaNeeds`), map #1 content
build-out (Vudmaster), vegetation/fog shader work (Denis). See the roadmap's "Паралельні роботи" list.

---

## Most recent — New-player onboarding + risk loop (2026-07-18 → 07-23)

M1 core-loop + onboarding batch. All committed.

- **M1.1 gear-loss + baseline floor (task #39).** KIA → `Player.Inventory.ClearAll()` in `App.EndRaid`
  before save (full wipe; stash safe; KIA only, not extraction / hideout-exit). **Baseline floor** =
  Common Ballistic pistol (exactly one full mag via `WeaponStatComposer` — never overfilled) + spare
  ammo (36 rounds total) + bandage + medkit, **NO armor** (`PlayerSpawnSystem.GiveBaselineFloor`),
  granted when inventory is empty (fresh save / post-KIA-wipe) → never soft-locked, but death costs
  your GOOD gear. Test ranges keep the 6-weapon cheat kit (`GiveTestRangeLoadout`). The M1.1 grant-once
  flag was reverted/removed. Pure bits tested (`GearLossTests`, `PlayerSpawnSystemTests`).
- **Quest marker — world + minimap (task #61).** NPCs with an offer show a floating SDF "!" badge +
  additive ground light pool + camera-billboarded beam (`NpcQuestIndicator`; shaders `VFX/QuestBeam` +
  `VFX/QuestGroundGlow`, both soft-particle depth-faded) + a procedural "!" on the minimap. Tunable in
  `Dev Cheats → ❗ Quest Marker`.
- **Deploy wayfinding + first-quest gate (task #62).** The hideout's exit-to-raid **deploy point** gets a
  `WorldBeacon` (new shared VFX = pool + billboarded beam) + a **pulsing screen-edge direction arrow** +
  a minimap `Deploy` marker (`DeployBeaconPresenter` / `DeployArrowPresenter`). **All gated behind
  accepting the first quest** (`QuestSystem.HasAcceptedAnyQuest` — also gates the deploy interaction +
  prompt). Interacting deploys **straight to Main Map** (the IMGUI SELECT MAP popup was removed). Beacon
  knobs → `Dev Cheats → 🧭 Deploy Marker`; arrow knobs (incl. pulse) → `❗ Quest Marker`.
- **FoW off in the hideout** (`FogOfWarController` skips when `App.IsInHideout` — combat mechanic,
  pointless/dark in the safe hub). Quest NPCs also gain the container-style interact outline
  (`InteractableOutlineTarget`).
- **New shared/support:** `View/WorldBeacon.cs`; ViewCheats sections `ViewCheatsQuestMarkerSection` +
  `ViewCheatsDeployMarkerSection`; `MinimapMarkerType.Deploy`.
- **Tests:** ~**671** EditMode green (last run this session).

> ✅ **Resolved (was: "progression OUT — reconcile with maintainer").** Progression is **IN** since
> 2026-07-21 and owned by Олександр. Model: **no skill points** — a node's price is looted materials
> (`ProgressionCostSystem`, 2026-07-26). Effects are still **not wired**:
> `ProgressionSystem.ApplyAllocatedEffects` is an empty seam with a TODO (roadmap M2.7). Details in
> [`progression.md`](./progression.md). Item durability persists in `SaveData`
> (CurrentDurability/MaxDurability).

**Still-open new-player gap:** task #63 — initial difficulty ramp (weak bots first; playtest: "3 riflemen killed me in 20s").

---

## Most recent — Weapon Attachments epic + Sniper Scope (2026-05 → 07-16)

Big landing since this doc was last current. **Weapon Attachments epic — ✅ complete** (P1–P4). Sidegrade "mod" layer on Builder weapons; full record in [`weapon-builder/attachments/README.md`](./weapon-builder/attachments/README.md).

- **Editor + inventory:** right-click weapon → **Modify** (modal, focus-slot → mod list, live green/red delta); OR drag mod↔weapon in inventory with compatible-slot cross-highlight. Mods are **loot-gated items** (backpack-consume, recoverable) that **drop from loot** (`ContainerConstants.AttachmentModDrops`).
- **Depth:** slot count = **f(core rarity)**; **unique mods** (`CompatibleArchetype`); **parabolic rarity balance**; **weapon-compare tooltip** (two-column diff incl. ammo + installed mods).
- **New stat axes:** `SightRange` (metres, additive), `ProjectileSpeed` + `Headshot` (mult) — composer + `WeaponStatDisplay` (Velocity always, Sight Range when scoped) + editor/compare.
- **Sniper Scope (P4):** vision = screen-space SDF reveal circle in `FogOfWarComposite.shader` glued to `WeaponAimPoint`; aim = **ergo-driven damped spring** in `AimingSystem` (dot/circle/bullet lag as one; low ergo overshoots+bounces; `WeaponAimVelocity` state); `PlayerVisionSystem` resolves `ScopeReveal = AdsBlend × aim-distance blend`. Ballistic identity: give Velocity/Headshot/pinpoint, take RoF/Recoil/Ergo. All tuning in **`Raid → Dev Cheats → 🎯 Scope`** (`DevCheatsScopeSection` + friendly custom editor). See `fog-of-war.md`.
- **Dropped:** Suppressor (Noise) removed from plan (2026-07-10) — too many unpolished features; unique mods stay on proxy axes.
- **Tests:** ~640 EditMode green (was 469 at this doc's prior footer).

**Next candidates** — superseded by the **[v1.0 roadmap](./v1.0-roadmap.md)** (milestones M1–M4, mirrored in the Task list #39–#60). Start point: **M1 — honest core loop** (gear-loss on death · raid timer · extraction UX · Tier 4a bot migration · audio scaffold). *(Tier 8.x reload/equip motion listed here previously — already shipped 2026-07-16.)*

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

- **700/700 EditMode tests green** (2026-08-05; was 671 at the 07-23 batch — +7 `RaidTimerSystemTests`,
  +5 `AmmoAvailabilityTests`, the rest from the loot rework).
- Compile clean (only pre-existing warnings — `FindObjectOfType` obsolete API, QuestDefinition missing type).
- 6 weapon archetypes feel coherent (Ballistic/Laser × Pistol/Rifle/Shotgun); semi-auto pistol/shotgun, full-auto rifle, laser charge-release.
- 4 test scenes: `ShootingScene` (armored), `ShootingScene_KillFeel` (low-HP), `ShootingScene_Horde` (zombie waves), `ShootingScene_RangedRange` (ranged combat with cover).
- All landings committed.

---

## What's deferred / open

### Gunplay backlog
See [`gunplay/README.md`](./gunplay/README.md) — combat-polish foundation shipped. **Most prior backlog items have shipped through 2026-05-21** — see "Shipped" / "Tried + Reverted" sections. Closed epics: Floating Damage Numbers v2, Aim Cursor v2, Maximal Archetype Differentiation, Bot off-screen fire gate, Magazine Drop Physics, **HUD Damage Feedback**. No active backlog candidates open at the moment — pick next direction with user.

### Battle design open
- Bleed L2 DPS values (playtest-driven)

### Known platform issue — terrain grass renders black on macOS/Metal (2026-08-05)
Terrain **detail meshes** (`VertexLit` + GPU instancing) whose material uses **any Shader Graph** render
solid black on Metal; the same project is correct on Windows/D3D11. Isolated on the maintainer's M3 Max:

- SG Lit (`S_Vegetation_Interactive`) → black · SG Simple → black · SG **Unlit** (`S_Particle`) → black
- The generated HLSL exported to a plain `.shader` → **also black** (so it is not the SG importer)
- `URP/Lit` and `URP/Unlit` on the same prototype → **correct**; the same SG material on a normal
  MeshRenderer → **correct**
- Ruled out: graph precision (Single), shader cache/reimport, material GPU-instancing flag,
  stray Built-In/HDRP targets in the graph, light probes. `useInstancing = false` → white (legacy
  built-in detail path); `renderMode = Grass` → details vanish.

No matching Unity issue in the tracker (closest: UUM-76696, fixed well before 6000.3). Options, none of
them free: hide details in the macOS editor as a local workaround, port the grass shader to a
hand-written URP one (`Assets/Shaders/BushWindCutout.shader` is a working template), or move grass off
terrain details onto scattered prefabs.

### Test debt
- P0-3 BT primitives 0 tests (BTSelector / BTSequence / BTCondition / BTCooldown)
- P0-4 Priority-order tests (Heal > Dodge > Combat > Patrol)
- P0-5 WriteBackDurability coverage
- Burst tests (`WeaponPhase.Bursting`) deferred

### Architecture debt
- ✅ DevCheats/ViewCheats UI fragmentation — resolved (unified window)

### Test scenes available
- `Assets/Scenes/HideoutScene.unity` — main hub
- `Assets/Scenes/ShootingScenes/ShootingScene.unity` — armored targets (10000 HP)
- `Assets/Scenes/ShootingScenes/ShootingScene_KillFeel.unity` — low-HP targets (10/25/50/75/100)
- `Assets/Scenes/ShootingScenes/ShootingScene_Horde.unity` — zombie wave spawner, 5s grace, 360° ring spawn
- `Assets/Scenes/ShootingScenes/ShootingScene_RangedRange.unity` — 7 RangedTarget bots across 4 zones with 13 cover cubes, NavMesh baked
- `Assets/Scenes/ShootingScenes/ShootingScene_Feedback.unity` — HUD damage feedback playtest. 6 stationary turrets in row firing -Z (all 6 archetypes) + 3 side turrets firing -X. Bots use `BotBehaviorFlags.FireForward` (no target tracking, fire continuously in fixed facing). Pairs з GodMode visual passthrough for damage-free playtest.

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
3. Verify EditMode tests are green (**700** baseline as of 2026-08-05) — through your editor/MCP bridge if you have one, else the Unity Test Runner / `-batchmode`. Don't scrape `.unity` files as text. (The maintainer's specific bridge setup is personal — see their `~/.claude/`.)
4. **Direction is locked — see [`v1.0-roadmap.md`](./v1.0-roadmap.md) (M1–M4).** Target = full v1.0; Weapon Builder = headline (3×4 + exotics); **progression IN** (material-cost nodes, since 2026-07-21); 2 maps + bunker; item icons must-have (generated); EN-only. **M1 is the active milestone:** M1.1 ✅, M1.2 ✅, M1.4 ✅; open are **M1.3 extraction UX**, **M1.5 audio scaffold**, plus **#63** initial difficulty ramp. Rationale + full gap analysis in [`release-scope.md`](./release-scope.md).
5. Check the roadmap's **"Паралельні роботи поза M1–M4"** before picking anything — maps, loot, bots,
   progression and the meta simulator are actively owned by other contributors.

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

**Status (2026-08-05):** All committed. **700** EditMode green. Latest: raid timer (M1.2), ammo audit (5 orphan calibers
deleted end-to-end + `AmmoAvailabilityTests` guard), quest-indicator stuck-on fix, foliage bloom fix.
M1: M1.1 ✅ M1.2 ✅ M1.4 ✅ — open M1.3 / M1.5 / #63. Parallel owners: progression + loot + meta simulator
(Олександр), map content (Vudmaster), vegetation shaders (Denis) — see the roadmap list before picking
work. Known Mac-only issue: terrain grass black on Metal (above). (Older: onboarding batch 2026-07-23,
Weapon Attachments epic + Sniper Scope 2026-07-16.)
