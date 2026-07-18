# EditMode Tests — Review

> **Status:** Analysis from 2026-04-24 (the plan below is that snapshot — treat as historical).
> **Scope (then):** 387 EditMode tests across 26 files in `Assets/Tests/EditMode/`.
> **Goal:** identify duplicates, weak/tautological tests, wrong assumptions, obsolete tests, coverage gaps — and produce a prioritized fix plan.
>
> **Currency note (2026-07-16):** suite is now **~660 EditMode green** (attachments epic, sniper scope, bot/heal, etc. added tests since). Some flagged debt already resolved — notably the **DevCheats-leak refactor** for `ArmorSystem` / `PlayerFOVSystem` / `MovementSystem` (P0-1, done 2026-05-05, tunables now via `RaidContext.*Config`). Re-audit before acting on the older items below; the P1-K exotic gap stays valid (Tier 5 unimplemented).

**Legend:**
- 🔁 **DUP** — duplication / redundancy
- 🪶 **WEAK** — tautological / asserts nothing meaningful / fragile
- ❓ **WRONG** — wrong assumption or stale assertion
- ⛔ **OBSOLETE** — tests behavior that changed
- 🕳 **GAP** — doc promises X, no test validates X
- 🏗 **STRUCT** — structural / hygiene issue (not a bug, but worth cleanup)

---

## Executive summary

### Headline findings

1. **🚨 Three DevCheats leaks into systems under test** — `ArmorSystem`, `PlayerFOVSystem`, `MovementSystem` all read `DevCheats.*` directly, violating `testing-and-workflow.md §1`. Tests pass because defaults match constants, but **production breaks silently** if someone toggles DevCheats. Fix = one bundled refactor ("Config structs via RaidContext").

2. **🕳 Biggest coverage debt: BT framework + priority ordering** (Phase 4). Bot AI is the most complex system in the project, but `BTSelector`, `BTSequence`, `BTCondition`, `BTCooldown` — zero unit tests. Priority ordering (Heal > Dodge > Combat > Patrol) — the single most important AI design rule — never locked by tests.

3. **🕳 Core gameplay paths missing coverage** (Phase 5):
   - `TryPickUp` / `TryDrop` — 0 tests for the primary loot interaction
   - `CreateContainer` — 0 tests (3 documented container types)
   - `FindNearestInteractable` — 0 tests (core interaction scoring)
   - `WriteBackDurability` — 0 tests (armor persistence across equip swap)
   - Ammo modifiers / convergence pipeline — 0 tests in ShootingSystem

4. **🔁 ~900 LOC of boilerplate duplication** across 15+ files. Three consolidation helpers unlock everything else:
   - `WeaponBuilderTestFactory` (9 SO factory copies → 1)
   - `TestContextFactory` (15+ `CreateContext` helpers → 1)
   - `SetupHitScenario` DSL (cuts `DamageSystemTests` in half)

5. **🪶 ~26 weak/redundant tests** safe to drop (tautological checks, loop-based flaky ricochet test, triple null-safety on same invariant, misplaced fixtures).

### Final numbers

| Metric | Value |
|---|---|
| Tests reviewed | 387 |
| Weak/duplicate/obsolete tests to drop | **−26** |
| New critical coverage tests to add | **+58** |
| Net delta | **+32 tests** |
| LOC consolidation | **~900 LOC** |
| DevCheats leaks | **3 systems** (one refactor) |

### Consolidated prioritized fix plan

Grouped by priority and logical bundle. Items inside a bundle share setup cost — do together.

#### P0 — Blocking / highest value

**[P0-1] DevCheats → Config structs refactor** (3 systems, ~1 day)
- Create `ArmorConfig`, `FOVConfig`, `MovementConfig` structs (or extend `AimConfig` for movement).
- Add to `RaidContext` populated from DevCheats in `RaidSession.Tick`.
- Route `ArmorSystem.Calculate`, `PlayerFOVSystem.Tick`, `MovementSystem.Tick` through context.
- Tests use `*Config.Default` — truly deterministic.
- Covers: P3-α, P4-α, P5-α. Also unlocks P3-J (cap tests), P4-N (DevCheats pollution), P5-E/P5-G (ADS blend tests).

**[P0-2] Test factory consolidation — ✅ SHIPPED**
- `Assets/Tests/EditMode/Fakes/TestContextFactory.cs` — single RaidContext builder (replaces 15+ ad-hoc `CreateContext` helpers).
- `Assets/Tests/EditMode/Fakes/WeaponBuilderTestFactory.cs` — single SO factory (replaces 9 per-file copies).
- All P1-A / P2-B / P2-C / P3-B / P4-A / P5-β references in this doc were closed by these consolidations.

**[P0-3] BT framework primitives tests** (P4-C, ~0.5 day)
- New `BT/BTSelectorTests.cs`, `BT/BTSequenceTests.cs`, `BT/BTConditionTests.cs`, `BT/BTCooldownTests.cs`.
- ~8-10 tests covering status propagation, short-circuit evaluation, cooldown timer reset.
- Foundation for all BT node work; without it any BT change is a guess.

**[P0-4] Priority order tests** (P4-D, ~0.25 day)
- Add 3-4 tests to `BotBrainSystemTests`: heal preempts combat, dodge preempts shoot, grenade preempts shoot when target behind cover.
- Locks the single most important AI design rule per `bot-ai.md` §3.

**[P0-5] WriteBackDurability coverage** (P3-Q, ~0.25 day)
- Add 3 tests to `EquipmentSystemTests`: copies armor map → inventory, null-safety, equip-swap cycle preserves combat damage.
- Closes critical persistence gap.

#### P1 — High-value coverage gaps

**[P1-1] ShootingSystem ammo + convergence tests** (P2-G, P2-H, ~0.5 day)
- 3 ammo-modifier tests (Penetration / ArmorDamage / BleedChance composition).
- 1 convergence-direction test (parallax + convergence + AimUp blend produces expected direction).
- Closes the biggest gap in Phase 2 (core firing pipeline).

**[P1-2] Damage pipeline skip checks + caps** (P3-I, P3-J, P3-K, ~0.5 day)
- 3 skip-check tests (dead target, rolling i-frames, god mode).
- 3 cap tests (Penetration/Armor/ArmorDamage caps) — depends on P0-1.
- 1 bleed-ignores-armor integration test.

**[P1-3] InventorySystem pickup/drop tests** (P5-M, ~0.5 day)
- 3-4 tests for `TryPickUp` (partial stack fill, overflow, non-stackable, no-space).
- 1-2 tests for `TryDrop`.
- Move out of `LootSystemTests` into new `InventorySystemTests.cs`.

**[P1-4] LootSystem container + interactable tests** (P5-K, P5-L, ~0.5 day)
- 2-3 tests for `CreateContainer` (MedContainer/AmmoBox/RandomLootBox).
- 2-3 tests for `FindNearestInteractable` (facing + distance scoring).

**[P1-5] Aiming missing features** (P5-C, P5-D, ~0.25 day)
- 2 `MinAimDistance` tests (replaces obsolete test from session 1 task).
- 2 recoil decay tests (RecoilOffset flow via AimingSystem).

**[P1-6] Missing bot nodes + combat** (P4-E, P4-F, P4-I, ~0.5 day)
- 2-3 tests each for `DodgeNode`, `ThrowGrenadeNode`.
- 1-2 `GrenadeThrow` tests in `BotCombatSystemTests`.
- 1 damage-alert detection path test in `BotPerceptionSystemTests`.

**[P1-7] WeaponSyncSystem.Tick branches** (P1-U, ~0.25 day)
- 3 tests: inventory slot cleared, inventory slot replaced, assembly failure → ghost weapon.
- Closes the D6 re-assembly trigger gap.

**[P1-8] Damage scenario DSL** (P3-C, ~0.5 day)
- Introduce `SetupHitScenario(...)` helper.
- Migrate existing `DamageSystemTests` (19 tests, ~700 LOC → ~300 LOC).
- Dependent on P0-2.

#### P2 — Hygiene / minor coverage

**[P2-1] Drop weak/redundant tests** (~0.25 day, touching 6 files)
- P1-C (CoreInstance constructor trivia ×3), P1-D (Exotic nullable trivia), P1-M (presenter duplicate), P1-T (WSSIT round-trip duplicate).
- P2-F (shooting phase tests → TestCase), P2-M (equip press-slot 4→1), P2-N (equip negative-space duplicate), P2-O (wall clipping call-count tests).
- P3-D (ItemDefinition smoke × 8 → 1 parameterized), P3-E, P3-F (relocation cleanup).
- P4-H (`Scav_CannotHeal` duplicate).

**[P2-2] Relocate misplaced tests** (P3-A, ~0.25 day)
- Split `ArmorStateTests` → new `ProjectileEntityStateTests`, `ItemDefinitionRegistryTests`.

**[P2-3] Small coverage additions**
- P1-I (AmmoInMagazine propagation), P1-Q (ballistic E2E shoot-through), P1-O (invalid payload ID in presenter).
- P2-I (FireInterval gate scenario), P2-A (ConfigureWeaponPhase helper for StateMachineTests).
- P4-J (perception tick interval), P4-K (CanSeeTarget vs HasTarget), P4-L (spawn medkit/grenade counts).
- P5-E, P5-G (ADS blend tests — depends on P0-1), P5-H (sprint multiplier), P5-N (TryMove within inv), P5-O, P5-P (spawn loadout gating).

#### P3 — Cleanup / stylistic

- P1-G (decide `WeaponArchetypeLabel.Compose` null-safety contract), P1-E (parameterize dup-id tests), P1-F, P1-H, P1-L, P1-N, P1-R, P1-S.
- P2-D, P2-E, P2-K, P2-L, P2-P.
- P3-G, P3-H, P3-L, P3-M, P3-N, P3-O, P3-P, P3-R.
- P4-G, P4-M, P4-O.
- P5-F, P5-I, P5-J, P5-Q.

### Recommended execution order

```
✅ P0-1 (DevCheats refactor)         — shipped 2026-05-05
✅ P0-2 (factories consolidation)   — shipped (TestContextFactory + WeaponBuilderTestFactory у Fakes/)
   ↓
1. P0-3 (BT primitives) + P0-4 (priority order) — close biggest coverage debt
2. P0-5 (WriteBackDurability) — critical persistence gap
   ↓
3. P1-* in any order, bundled by file for locality
   ↓
4. P2-1 (drop weak tests) — after new coverage in, safe to prune
5. P2-2, P2-3 — polish
```

**Rough total effort:** ~6 working days if done sequentially. But most P1 items are independent — can be parallelized or spread over multiple sessions.

### What NOT to touch (intentionally high coverage)

These files are exemplars of good testing and need minimal work:
- **AimingSystemTests** (Phase 5) — 24 tests covering dual-layer model deeply. Only add missing features (P5-C, P5-D), don't refactor.
- **BotHealTests** (Phase 4) — 15 tests with fixture helper `CreatePMCSafe` pattern. Deep coverage of complex sub-feature. Only drop duplicate `Scav_CannotHeal` (P4-H).
- **WeaponPullbackMathTests** (Phase 2) — 9 tests, perfect pure-math unit test. No changes.
- **AmmoSystemTests** (Phase 2) — 18 tests, clean coverage. No changes beyond optional P2-L.

### Living-doc note

This review reflects the codebase at 2026-04-24 after the three test fixes from this session (aim test deletion, PlayerFOVSystem IPhysicsAdapter routing, CoreDefinitions registry injection). As the codebase evolves, revisit Phase 4 (BT framework) and Phase 3 (DevCheats leaks) first — they have the highest architectural debt.

---

## Cleanup-only plan (current scope, 2026-04-24)

> **Scope narrowed:** improve/delete existing tests only. **No new tests in this pass.** Adding coverage (all GAP items, DevCheats refactor) is deferred to a separate work session.
>
> **Net effect:** 387 → 361 tests (−26), ~900 LOC consolidation, 0 tests added.

### Batch A — Infrastructure helpers (unblocks everything)

Do these FIRST — each reduces the diff size of subsequent batches. All additive, zero risk to existing tests.

**A1. `TestContextFactory`** (consolidates 15+ copies)
- Covers: P2-B, P3-B, P4-A, P5-β
- New file: `Assets/Tests/EditMode/Fakes/TestContextFactory.cs`
- Exposes `Create(FakeInputAdapter input=null, IRaidEvents events=null, IPhysicsAdapter physics=null, AimConfig? aim=null, ShootingConfig? shooting=null, float dt=1/60f)`
- Migrate every test file's local `CreateContext` → `TestContextFactory.Create(...)`
- Affected: AimingSystemTests, MovementSystemTests, ShootingSystemTests, WeaponStateMachineSystemTests, WeaponEquipSystemTests, WeaponWallClippingTests, ProjectileSystemTests, DamageSystemTests, StatusEffectSystemTests, BotBrainSystemTests, BotCombatSystemTests, BotHealTests, BotPerceptionSystemTests, PlayerFOVSystemTests, EquipmentSystemTests — **15 files**

**A2. `WeaponBuilderTestFactory`** (consolidates 9 copies)
- Covers: P1-A, P2-C
- New file: `Assets/Tests/EditMode/Fakes/WeaponBuilderTestFactory.cs`
- Exposes: `MakeBallistic`, `MakeLaser` (with `chargeTime`), `MakeDelivery` (with `pattern`/`formFactor`/`stats`), `MakeExotic`, `MakeDatabase`, `MakeRegistry`, `SetPrivateField`
- Migrate: CoreDefinitionRegistryTests, WeaponArchetypeLabelTests, WeaponAssemblySystemTests, WeaponStatComposerTests, WeaponBuilderPresenterTests, WeaponBuilderEndToEndTests, WeaponChargeFlowEndToEndTests, WeaponSyncSystemIntegrationTests, WeaponStateMachineSystemTests, ShootingSystemTests, EditModeTestsUtils — **11 files**
- Also delete the ad-hoc MakeLaser copy added in session 1 task 3 to `EditModeTestsUtils` and refactor it to call the new factory

**A3. `SetupHitScenario` DSL** (cuts DamageSystemTests ~half)
- Covers: P3-C
- Add to `Tests.EditMode.Fakes` or as static helper in `DamageSystemTests`
- Signature: `(RaidState state, EId targetId, FakeRaidEvents events) SetupHitScenario(float targetHp=100, ArmorSlotState armor=null, float damage=25, float pen=30, float armorDmg=10, bool isHeadshot=false, float headshotMul=1, float bleedChance=0)`
- Migrate all 19 tests in `DamageSystemTests.cs`. Expected ~700 → ~350 LOC.

**A4. `ConfigureWeaponPhase` helper** (for StateMachineTests)
- Covers: P2-A
- Add to `EditModeTestsUtils`: `ConfigureWeaponPhase(weapon, phase, phaseStart=0, elapsedTime=0, fireInterval=null, equipTime=null, ...)`
- Migrate 28 tests in `WeaponStateMachineSystemTests.cs`. Expected ~70 LOC reduction.

---

### Batch B — Drop weak/redundant tests (−26 tests)

Safe to do after Batch A; diff is smaller with helpers in place.

| # | Test(s) to drop | Ref | File | Count |
|---|---|---|---|---|
| B1 | `PayloadCoreInstance/DeliveryCoreInstance/ExoticModInstance_ConstructorStoresFields` | P1-C | CoreInstanceTests | −3 |
| B2 | `WeaponConfiguration_ExoticSetterTogglesFlag` | P1-D | CoreInstanceTests | −1 |
| B3 | `Compose_ExoticNull_UsesBaseStats` | P1-J | WeaponStatComposerTests | −1 |
| B4 | `FullBackpack_CanBuildFalseEvenWithValidSelection` (merge into `TryBuild_BackpackFull_FailsWithReason` with joint assertions) | P1-M | WeaponBuilderPresenterTests | −1 |
| B5 | `GroundItemRoundTrip_PreservesWeaponConfiguration` (WBEEE version already covers via player flow) | P1-T / P1-B | WeaponSyncSystemIntegrationTests | −1 |
| B6 | `Tick_InCooldownPhase_DoesNotSpawn` + `Tick_InReadyPhase_SpawnsProjectile` + `Tick_DuringEquipping_DoesNotSpawn` + `Tick_DuringUnequipping_DoesNotSpawn` → merge into 1 `[TestCase(WeaponPhase.X)]` parameterized test | P2-F | ShootingSystemTests | −3 |
| B7 | `Tick_PressSlot_SetsPendingHotbarSlot` + `Tick_PressCurrentSlot_SetsPendingToSameSlot` + `Tick_PressEmptySlot_SetsPending` + `Tick_AlreadyHasPending_Overwrites` → merge into 1 `[TestCase(0)][TestCase(1)][TestCase(3)][TestCase(5)]` | P2-M | WeaponEquipSystemTests | −3 |
| B8 | `Tick_WeaponRemainsInHotbarSlot` (duplicate of `Tick_DoesNotChangeEquippedWeapon`) | P2-N | WeaponEquipSystemTests | −1 |
| B9 | `Tick_MuzzleBlockDisabled_DoesNotCallPhysics` + `Tick_MuzzleBlockEnabled_CallsPhysicsOnce` (fragile how-not-what) | P2-O | WeaponWallClippingTests | −2 |
| B10 | `ItemDefinition_HelmetBasic_HasArmorStats` + `_ArmorBasic_HasArmorStats` + `_AmmoRifle_HasPenetration` + `_AmmoRifleAP_HasHigherPenetration` + `_AmmoRifleHP_HasBleedChance` + `_HelmetBasic_HasArmorPrefabId` + `_ArmorBasic_HasArmorPrefabId` + `_StandardAmmo_NoBleedChance` → consolidate into 1 parameterized `ItemDefinition_CoreItems_HaveExpectedShape` | P3-D | ArmorStateTests (→ ItemDefinitionRegistryTests after D1) | −7 |
| B11 | `ArmorSlotState_DefaultSlotsAreNull` (tautological C# default) | P3-E | ArmorStateTests | −1 |
| B12 | `ProjectileCreate_DefaultPenetration_Zero` (compiler-guaranteed) | P3-F | ArmorStateTests | −1 |
| B13 | `Tick_Scav_CannotHeal` (duplicate — keep only the BotHealTests version) | P4-H | BotBrainSystemTests | −1 |

**Total: −26 tests, ~200 LOC.**

---

### Batch C — Refactor existing tests (no count change)

| # | Action | Ref | File |
|---|---|---|---|
| C1 | Parameterize 3 happy-path archetype tests with `[TestCase]` (BallisticPistol / LaserRifle / FoamShotgun → one parametric test) | P1-H | WeaponArchetypeLabelTests |
| C2 | Parameterize duplicate-id warn test across Payload/Delivery/Exotic (currently only Payload) | P1-E | CoreDefinitionRegistryTests |
| C3 | Move `new DatabaseCoreDefinitionRegistry(_db)` from every test body into `SetUp` as `_registry` | P1-F | CoreDefinitionRegistryTests |
| C4 | `Assert.AreEqual(3, callCount)` → `Assert.GreaterOrEqual(callCount, 3)` + assert end-state in `SelectionChanges_FireStateChangedEvent` | P1-N | WeaponBuilderPresenterTests |
| C5 | Fix misleading comment `// half-way` (0.4s with 1.0s charge ≠ half) | P1-R | WeaponChargeFlowEndToEndTests |
| C6 | Merge `CreateContext` and `CreateContextWithInput` via default-param input (after Batch A1) | P2-E | WeaponStateMachineSystemTests |
| C7 | Replace `Tick_BodyshotNeverRicochets` 20-iteration loop with deterministic rand `() => 0.1f` + assert no ricochet event | P3-M | DamageSystemTests |
| C8 | Add TearDown to restore DevCheats defaults (fixes test pollution until P4-α refactor) | P4-N | PlayerFOVSystemTests |

---

### Batch D — Relocate / restructure

**D1. Split `ArmorStateTests`** (P3-A)
- Keep in `ArmorStateTests.cs`: 8 core tests (Create, IsBroken, DurabilityPercent variants) + `RaidState_Create_HasArmorMap` = 9 tests
- Move to new `ProjectileEntityStateTests.cs`: `ProjectileCreate_WithPenetration_CarriesValues` (1 test — after B12 drop)
- Move to new `ItemDefinitionRegistryTests.cs`: the parameterized test from B10 (1 test covering all shape expectations)
- Net in ArmorStateTests: 20 → 9 tests. New files absorb the rest.

**D2. `WeaponArchetypeLabel.Compose` null-safety contract decision** (P1-G)
- Two options — **decide before executing**:
  - (a) Document tolerant behavior in `weapon-builder/architecture.md §D8`: "null/empty segments fall back to remaining segment; both empty → string.Empty". Keep 5 null/empty tests. **0 test changes.**
  - (b) Drop 5 null/empty tests (Compose_NullPayload / NullDelivery / BothNull / EmptyDisplayName / EmptyFormFactor / BothEmpty = 6 tests). Contract becomes "callers must provide non-null non-empty SO refs from registry". **−6 tests.**
- **Recommend (a)** — tolerant fallback is already safer and costs nothing.

---

### Execution order

```
Day 1 (infrastructure, ~0.5 day):
  1. A1 — TestContextFactory
  2. A2 — WeaponBuilderTestFactory (deletes ad-hoc copy in EditModeTestsUtils)

Day 2 (infrastructure, ~0.5 day):
  3. A3 — SetupHitScenario DSL
  4. A4 — ConfigureWeaponPhase helper

Day 3 (drops, ~0.5 day):
  5. B1–B13 — delete 26 weak/redundant tests
  6. C1–C8 — refactor touches

Day 4 (restructure, ~0.5 day):
  7. D1 — split ArmorStateTests
  8. D2 — decide and apply WeaponArchetypeLabel null-safety

Verify: Test Runner green at every checkpoint. Each batch can be a separate commit.
```

### What stays deferred

- **All GAP items** (new test coverage) — separate work
- **DevCheats refactor** (P3-α, P4-α, P5-α) — production code, not test cleanup
- **Optional hygiene** (P1-S, P2-D, P2-J, P2-P, P3-H, P3-L, P3-R, P4-G, P4-M, P4-O, P5-F, P5-J) — deprioritized until someone touches related area

---

## Phase 1 — Weapon Builder cluster

**Files reviewed:** CoreDefinitionRegistryTests, CoreInstanceTests, WeaponArchetypeLabelTests, WeaponAssemblySystemTests, WeaponStatComposerTests, WeaponBuilderPresenterTests, WeaponBuilderEndToEndTests, WeaponChargeFlowEndToEndTests, WeaponSyncSystemIntegrationTests — **82 tests**.

**Docs used:** `weapon-builder/design.md` (v0.7), `weapon-builder/architecture.md` (D1–D14, Q1–Q7).

### 1.1 Cross-cutting issues

#### 🔁 P1-A. Massive SO-factory duplication — HIGH priority
`MakeBallistic / MakeLaser / MakeDelivery / MakeExotic / SetPrivateField` repeated in 8 files, **~600 LOC of near-identical boilerplate**. My recent `EditModeTestsUtils` edit added a **9th copy**. Any SO field rename breaks all of them in lockstep.

**Fix plan:** extract `Tests.EditMode.Fakes.WeaponBuilderTestFactory` with:
- `MakeBallistic(id, displayName=null, ammoType=null, stats=default)`
- `MakeLaser(id, chargeTime, ...)`
- `MakeDelivery(id, formFactor, pattern, stats=default)`
- `MakeExotic(id)`
- `MakeDatabase(payloads, deliveries, exotics)`
- `MakeRegistry(db)` wrapper

All existing tests migrate to `WeaponBuilderTestFactory.MakeBallistic(...)`. Estimated diff: −500 LOC net after migration, single point of future maintenance. **Do BEFORE any further WB feature work.**

#### 🏗 P1-B. End-to-end tests overlap on assembly pipeline
Three fixtures exercise `WeaponSyncSystem.BuildWeaponForItem` on BallisticRound+SingleAction/Auto assembly:
- `WeaponSyncSystemIntegrationTests` — via `WeaponItemFactory.SpawnItem("Rifle"/"Pistol")` (legacy factory path)
- `WeaponBuilderEndToEndTests` — via Presenter path (new player flow)
- `WeaponChargeFlowEndToEndTests` — via Presenter path for Laser

Each has its own `SetUp` + own SO factories + own `DatabaseCoreDefinitionRegistry`. Overlap is partial (different entry points), but both WSSIT and WBEEE have a **`GroundItemRoundTrip` test that's structurally identical** (drop→pickup→still assembles).

**Fix plan:** keep the 3 fixtures (they legitimately test different entry points) BUT:
- Consolidate the common round-trip scenario into ONE test — pick WBEEE because it's closer to player flow.
- Drop `WeaponSyncSystemIntegrationTests.GroundItemRoundTrip_PreservesWeaponConfiguration` (duplicate).

### 1.2 Per-file findings

#### CoreInstanceTests.cs (13 tests)
🪶 **P1-C. ConstructorStoresFields** (Payload, Delivery, Exotic) × 3 — tests that constructor sets fields. For a `readonly struct` this is tautological — compiler-generated. **Action:** drop 3 tests. Keep equality tests.

🪶 **P1-D. WeaponConfiguration_ExoticSetterTogglesFlag** — tests that `cfg.Exotic = x; cfg.Exotic.HasValue == true`. This is `Nullable<T>` framework behavior. **Action:** drop; already covered by `WithExotic_ExoticRoundtrips`.

✅ Keep: `RarityTier_IntValuesMatchOrder` (not tautological — enum values are used as array indices; regression guard justified — see architecture `_statsByTier[(int)tier]`).

✅ Keep: equality tests (structural equality is hand-written via `IEquatable<T>`, worth guarding).

**Net:** 13 → 9 tests.

#### CoreDefinitionRegistryTests.cs (13 tests)
✅ Generally solid. Covers Get/TryGet × Payload/Delivery/Exotic + missing ID + All* lists + null DB + duplicate warn.

🏗 **P1-E. Duplicate-handling only for Payload**. `DuplicatePayloadIds_LogWarningAndLastWins` has no counterpart for Delivery/Exotic despite identical code path in registry. **Action:** convert to `[TestCase]` parameterized over {Payload, Delivery, Exotic} or add 2 tests. Low priority — same code path.

🔁 **P1-F. `new DatabaseCoreDefinitionRegistry(_db)` created fresh inside every test body** instead of once in SetUp. 11 lines of repetition. **Action:** move to SetUp as `_registry`. Trivial.

#### WeaponArchetypeLabelTests.cs (9 tests)
❓ **P1-G. Null/empty behavior not specified by D8.** Doc §D8: `"{payload.DisplayName} {delivery.FormFactor}"` — pure template. Tests verify graceful fallback (null→string.Empty, null payload→formFactor only). This is **tolerant behavior not mandated by design.** Either (a) document it as contract in D8 or (b) drop null/empty tests — `Compose` is only called with resolved non-null SO refs from registry.

**Action:** decide intent, then either update D8 doc (1 line) or drop 5 null/empty tests (4 happy-path tests enough).

🔁 **P1-H.** Three happy-path tests (BallisticPistol / LaserRifle / FoamShotgun) — classic `[TestCase]` candidate.

#### WeaponAssemblySystemTests.cs (6 tests)
✅ Strong fixture. Covers D7 strict failure on missing Payload / Delivery / Exotic, + registry null. All assertions match architecture doc.

🕳 **P1-I. No coverage for `AmmoInMagazine` propagation** — doc D6 says magazine persists across re-assembly. `TryAssemble` writes `result.AmmoInMagazine = config.AmmoInMagazine`? Check implementation; if so, add 1 test.

#### WeaponStatComposerTests.cs (7 tests)
✅ Solid for D1 (Payload 8 / Delivery 13 field split). `DeliveryDoesNotOverridePayloadFields` / `PayloadDoesNotOverrideDeliveryFields` are excellent invariant guards.

🪶 **P1-J. `Compose_ExoticNull_UsesBaseStats`** — asserts Damage=10 when exotic=null. Already covered by `Compose_CommonTier_PopulatesAllPayloadFields` (which also has exotic=null default). **Action:** drop; redundant.

🕳 **P1-K. Zero exotic coverage.** `Compose` signature takes `exotic: ExoticModDefinition?`. All tests pass null or default. Per architecture comment "TODO (Tier 5): apply exotic.StatsModifier here when shape is defined." — this is OK for now (Tier 5 not implemented). **No action** but flag for Tier 5 scope.

🕳 **P1-L. No test for Common + payload-specific (Laser ChargeTime)**. `LaserPayloadDefinition.SpecificByTier` is read only in charge-flow E2E. Add a unit test on composer / resolver to verify `LaserSpecificStats` indexing by tier works. Minor.

#### WeaponBuilderPresenterTests.cs (12 tests)
✅ Solid coverage of presenter surface — CanBuild gating, partial selection, preview updates, TryBuild fail modes.

🔁 **P1-M. `FullBackpack_CanBuildFalseEvenWithValidSelection` + `TryBuild_BackpackFull_FailsWithReason`** — same scenario tested twice through different properties. **Action:** merge into one test that asserts both `CanBuild==false` and `TryBuild(out _)==false` with reason.

🪶 **P1-N. `SelectionChanges_FireStateChangedEvent`** asserts `callCount == 3` exactly. Fragile — any internal refactor that fires an extra event (e.g. preview recompute notification) breaks this. **Action:** assert `>= 3` and verify end state (HasPayload, HasDelivery, CanBuild == expected after ClearSelection) — more resilient intent.

🕳 **P1-O. No test for invalid payload ID** — `SelectPayload("NonExistent")` — what does presenter do? Silently set to null? Throw? Doc doesn't say. Check implementation; add 1 test to lock behavior.

#### WeaponBuilderEndToEndTests.cs (5 tests)
✅ Excellent — true vertical-slice tests. `FullFlow_BuildBallisticPistol_ProducesRuntimeWeaponWithMatchingStats` is the D14 Tier 1 demo in code form.

🔁 **P1-P. `FullFlow_BuildThenDropToGroundThenPickUp_ConfigSurvives`** — covers same scenario as `WeaponSyncSystemIntegrationTests.GroundItemRoundTrip_PreservesWeaponConfiguration`. **Action:** keep WBEEE version (closer to player flow), drop WSSIT duplicate (see P1-B).

🕳 **P1-Q. D14 step 10 ("Shoot — стріляє як pistol") not covered here.** WBEEE stops at runtime WeaponEntityState. Ballistic fire-through isn't E2E tested (ShootingSystemTests cover firing in isolation; Laser WCEEE covers fire-through). **Action:** add 1 test `FullFlow_BuildPistol_ShootingSystemTickSpawnsProjectile` — closes D14 contract.

#### WeaponChargeFlowEndToEndTests.cs (9 tests)
✅ Exemplary Tier 2 coverage. Variant B (Laser charges regardless of Delivery — D14 R2) verified across all 3 deliveries. StateMachine_LaserCharging_CancelOnRelease closes the release-cancel path.

🏗 **P1-R. Minor comment drift**: line 198 `state.ElapsedTime = 0.4f; // half-way` but ChargeTime=1.0s → 0.4 isn't half. Cosmetic; doesn't affect test validity. **Action:** fix comment or change to 0.5f.

🕳 **P1-S. Only Laser+{SingleAction,Auto,Scatter} tested.** Laser+{Rotary,Swarm} not in Tier 2 scope, OK. But **Ballistic+Scatter fires 7 pellets test exists here while BallisticScatter charge-up is not relevant** — the test `BuildBallisticShotgun_RuntimeWeapon_UsesScatterDelivery` is really a Scatter-coverage test, not charge-flow. Slightly misplaced; could move to WeaponBuilderEndToEndTests. **Low priority.**

#### WeaponSyncSystemIntegrationTests.cs (7 tests)
✅ Covers D7 ghost-weapon paths thoroughly — item without config / null registry / unknown payload / unknown delivery.

🔁 **P1-T. `GroundItemRoundTrip_PreservesWeaponConfiguration`** — duplicate of WBEEE.FullFlow_BuildThenDropToGroundThenPickUp (via different entry point, but same assertions). **Action:** drop this one, keep WBEEE's.

🕳 **P1-U. `WeaponSyncSystem.Tick` path untested.** All 7 tests hit `BuildWeaponForItem` (the helper). But `Tick` has non-trivial branching:
- `invItem == null && hotbarWeapon != null` → clear hotbar slot, handle SelectedHotbarSlot/PendingHotbarSlot
- `invItem != null && hotbarWeapon != null && hotbarWeapon.Id != invItem.Id` → rebuild + swap equipped

This is the **D6 re-assembly trigger path** — core gameplay flow (pick up new weapon → auto-equip on slot change). **Action:** add `WeaponSyncSystemIntegrationTests` section or new `WeaponSyncSystemTickTests` with 3 scenarios:
1. Inventory slot cleared → hotbar slot cleared + EquippedWeapon=null if that was the selected slot
2. Inventory slot changed to different ItemState → hotbar rebuilt, EquippedWeapon re-points if selected
3. Assembly fails on new item → ghost-weapon: hotbar slot stays null, inventory item untouched, event emitted

#### WeaponAssemblySystemTests.cs — extra thought
The test uses `WeaponAssemblySystem.TryAssemble(config, registry, out result, out reason)`. Good contract testing. No issues beyond P1-I.

### 1.3 Phase 1 summary

**82 tests → proposed 73 after cleanup + 5 new tests added → net 78.**

| Priority | Action | Files affected |
|---|---|---|
| **P0 (do first)** | P1-A Extract `WeaponBuilderTestFactory` helper | 9 files, −500 LOC |
| **P1** | P1-J, P1-C, P1-D, P1-M: drop 6 redundant/tautological tests | CoreInstance, StatComposer, Presenter |
| **P1** | P1-U: add 3 tests for `WeaponSyncSystem.Tick` branches | new fixture |
| **P1** | P1-Q: add 1 test Ballistic E2E shoot-through | WBEEE |
| **P2** | P1-G: decide Compose null-safety contract; update doc OR drop 5 tests | ArchetypeLabel |
| **P2** | P1-B, P1-T, P1-P: drop 1 duplicate round-trip test | WSSIT |
| **P2** | P1-I: add AmmoInMagazine propagation test | WeaponAssembly |
| **P3** | P1-F, P1-E, P1-H, P1-L, P1-N, P1-O, P1-R, P1-S: minor hygiene | various |

**Net effect:** −9 redundant/weak tests, +5 coverage tests, ~600 LOC of test boilerplate consolidated.

---

## Phase 2 — Weapon runtime

**Files reviewed:** WeaponStateMachineSystemTests (28), ShootingSystemTests (31), AmmoSystemTests (18), WeaponEquipSystemTests (8), WeaponPullbackMathTests (9), WeaponWallClippingTests (7), ProjectileSystemTests (9) — **110 tests**.

**Docs used:** `weapons.md` (FSM, ammo, convergence/parallax, ADS, dual-layer aiming), `crosshair.md` (visual/recoil reference).

### 2.1 Cross-cutting issues

#### 🔁 P2-A. Phase-setup boilerplate in WeaponStateMachineSystemTests — HIGH
28 tests, nearly every one has the same 4-5 lines: `weapon.Phase = X; weapon.PhaseStartTime = 0f; weapon.Stats.Y = Z; state.ElapsedTime = W;`. ~100 LOC of repetition.

**Fix plan:** helper `EditModeTestsUtils.ConfigureWeaponPhase(weapon, phase, phaseStart=0, elapsedTime=0, extraStatField=null)` or a small test DSL. Drops ~70 LOC, improves readability.

#### 🔁 P2-B. `CreateContext` helper duplicated in every test file
7 runtime-weapon test files each define their own `CreateContext` static helper (~10 LOC each). Same for AimingSystem, Movement, etc. Pattern:
```csharp
new RaidContext(deltaTime, events ?? new RaidEventBuffer(), new FakeTimeAdapter{...}, input, new FakeNavMeshAdapter(), ...)
```

**Fix plan:** centralize `TestContextFactory.Create(input, events=null, physics=null, shootingConfig=null, deltaTime=1/60f)` in `Tests.EditMode.Fakes/`. Drops ~70 LOC total, single source for future `RaidContext` ctor changes. **Do together with P1-A (WeaponBuilderTestFactory).**

#### 🔁 P2-C. Laser SO factory in 3 files
`MakeLaserPayloadSO(chargeTime)` appears in `WeaponStateMachineSystemTests` + `ShootingSystemTests` + `WeaponChargeFlowEndToEndTests`. Third instance of same pattern. Folds into P1-A.

### 2.2 Per-file findings

#### WeaponStateMachineSystemTests.cs (28 tests)
✅ Most comprehensive coverage in the project. Covers all phase transitions, swap interruption of every phase (Ready/Cooldown/Equipping/Reloading/Charging), toggle-off vs switch, guards.

🕳 **P2-D. `PhaseStartTime` not asserted on all transitions.** Several tests assert phase change but not `PhaseStartTime` update. Per weapons.md convention, every phase transition should stamp the start time. Audit — if system skips this on some transitions (e.g., swap interrupt), it's a latent bug the tests don't catch. **Action:** scan tests, add assertion `Assert.AreEqual(state.ElapsedTime, weapon.PhaseStartTime)` where phase changes. Low priority unless a bug surfaces.

🏗 **P2-E. `CreateContext` vs `CreateContextWithInput` — two helpers differ only by input param.** Redundant. Merge via default parameter (`input = null` → new FakeInputAdapter()).

#### ShootingSystemTests.cs (31 tests)
✅ Solid coverage of firing conditions + phase gates + charge gate + ammo flow + scatter pattern. Uses `ShootingConfig.Default` correctly (follows testing-and-workflow.md rule).

🔁 **P2-F. `Tick_InCooldownPhase_DoesNotSpawn` / `Tick_DuringEquipping_DoesNotSpawn` / `Tick_DuringUnequipping_DoesNotSpawn` / `Tick_InReadyPhase_SpawnsProjectile`** — classic 4-test `[TestCase]` candidate over `WeaponPhase`. 4 tests → 1.

🕳 **P2-G. No test for ammo-type modifier on projectile stats.** weapons.md §"Projectile Stat Composition" documents the pipeline:
```
totalPenetration = weapon.Stats.BasePenetration + ammoDef.Penetration
totalArmorDamage = weapon.Stats.BaseArmorDamage + ammoDef.ArmorDamage
totalBleedChance = weapon.Stats.BaseBleedChance + ammoDef.BleedChance
```
All tests use the default weapon (no ammo modifiers). Core gameplay surface untested. **Action:** add 3 tests — `Tick_AmmoModifiers_AddPenetrationToProjectile`, `_AddArmorDamage`, `_AddBleedChance`.

🕳 **P2-H. No test for convergence blend / parallax / AimUp affecting projectile direction.** weapons.md §"Convergence & Parallax Correction" documents a complex direction pipeline (parallax + convergence + AimUp blend). Tests only verify `proj.Direction == player.AimDirection` which matches zero-convergence hip-fire scenario. **Action:** at minimum 1 test verifying `ConvergencePoint` shift produces non-zero X/Z deviation from aim — locks the code path. Weight: medium (complex math, but critical for headshot detection).

🕳 **P2-I. No test for `FireInterval` gate between consecutive shots.** Inter-shot timing tested only indirectly via `InCooldownPhase_DoesNotSpawn`. Worth a scenario: fire → assert cooldown → advance elapsedTime < FireInterval → fire blocked → advance ≥ FireInterval + transition → fire succeeds. **Action:** add 1 scenario test.

🪶 **P2-J. `Tick_LaserPayload_ChargingWithTimeRemaining_DoesNotFire`** — edge case already fully covered by `WeaponChargeFlowEndToEndTests.ShootingSystem_LaserPistolBuild_FirstTick_EntersChargingNoProjectile`. Redundant but useful as focused unit. **Keep.**

#### AmmoSystemTests.cs (18 tests)
✅ Excellent coverage — CountReserve / ConsumeAmmo / CompleteReload / CanReload all covered with null / zero / partial / full / spanning / mixed cases. Static pure helpers tested in the cleanest possible way.

🔁 **P2-K. Three "null AmmoType" tests** (`CountReserve_NullAmmoType_ReturnsZero`, `CompleteReload_NullAmmoType_DoesNothing`, `CanReload_NullAmmoType_ReturnsFalse`). Guard is worth testing once per entry point; triple is OK but slightly verbose. **Action:** none — keep. Explicit trumps DRY for null safety.

🕳 **P2-L. `ConsumeAmmo` with ammoType not present in inventory** not explicitly tested. **Action:** add 1 test `ConsumeAmmo_AmmoTypeNotInInventory_ReturnsZero`. Minor.

#### WeaponEquipSystemTests.cs (8 tests)
✅ Correctly tests the intent-only nature of the system (PendingHotbarSlot only, no side effects).

🔁 **P2-M. Four tests are `SetValue` → `AssertEqual` trivia.** `Tick_PressSlot_SetsPendingHotbarSlot`, `Tick_PressCurrentSlot_SetsPendingToSameSlot`, `Tick_PressEmptySlot_SetsPending`, `Tick_AlreadyHasPending_Overwrites`. All verify input slot N → `PendingHotbarSlot == N`. The system is literally `player.PendingHotbarSlot = input.HotbarSlotPressed` (2 lines). **Action:** collapse into 1 `[TestCase(0)][TestCase(1)][TestCase(3)][TestCase(5)] Tick_PressSlot_Echoes(int slot)`. 4 → 1.

🔁 **P2-N. `Tick_DoesNotChangeEquippedWeapon` + `Tick_WeaponRemainsInHotbarSlot`** — both negative-space tests. System is intent-only by design. One is enough. **Action:** drop `Tick_WeaponRemainsInHotbarSlot` — it's even weaker (Hotbar isn't touched at all).

🪶 Net for file: 8 → 4 tests after cleanup, same coverage.

#### WeaponPullbackMathTests.cs (9 tests)
✅ **Perfect unit test.** Pure math helper, all boundaries explicitly covered (no hit / at pivot / behind pivot / at weapon length / beyond / half / zero origin / zero length / zero origin with length). No changes needed.

#### WeaponWallClippingTests.cs (7 tests)
✅ Clean — all physics routed through `IPhysicsAdapter` via `FakePhysicsAdapter`. Tests Solution 2 (pre-fire muzzle raycast / clamp) — behavior + edge cases (wall flush with player).

🪶 **P2-O. `Tick_MuzzleBlockEnabled_CallsPhysicsOnce` + `Tick_MuzzleBlockDisabled_DoesNotCallPhysics`** — assert call count on adapter. Tests **how** system works, not **what**. Fragile to refactors adding auxiliary raycasts. The behavioral tests (clamp / skip-fire) already lock the observable contract. **Action:** drop both unless there's a perf contract to enforce; keep the outcome-based tests.

#### ProjectileSystemTests.cs (9 tests)
✅ Clean coverage of movement + lifetime expiry + reverse-iteration safety.

🔁 **P2-P. Three movement tests are slices of `pos += dir * speed * dt`.** `Tick_MovesProjectileAlongDirection`, `Tick_RespectsSpeed`, `Tick_RespectsDeltaTime`. Could consolidate into `Tick_MovesByDirectionSpeedAndDeltaTime` with multi-assert, but fine-grained is also legitimate (localized failure messages). **Action:** stylistic only — leave as is.

### 2.3 Phase 2 summary

**110 tests → proposed 100 after cleanup + 5 new coverage tests → net ~105.**

| Priority | Action | Files affected |
|---|---|---|
| **P0 (bundle with P1-A)** | P2-B: centralize `TestContextFactory`. P2-C: fold Laser SO helper into `WeaponBuilderTestFactory` | 7+ files |
| **P1** | P2-F, P2-M, P2-N, P2-O: drop 8 tautological/redundant tests via TestCase + deletion | Shooting, Equip, WallClipping |
| **P1** | P2-G: add 3 ammo-modifier projectile-stat tests | ShootingSystem |
| **P1** | P2-H: add 1 convergence-direction test | ShootingSystem |
| **P2** | P2-I: add 1 FireInterval gate test | ShootingSystem |
| **P2** | P2-A: helper `ConfigureWeaponPhase` — drops ~70 LOC from WeaponStateMachineSystemTests | WeaponStateMachine |
| **P3** | P2-D, P2-E, P2-K, P2-L, P2-P: minor hygiene / optional audits | various |

**Net effect:** −8 redundant, +5 coverage, ~150 LOC boilerplate consolidated alongside P1-A.

### 2.4 Phase 1 + 2 running total

- **Reviewed:** 192 tests (9 WB + 7 runtime files)
- **Proposed deltas:** −17 dropped, +10 added = net **−7 tests, +much-reduced LOC**
- **Biggest win** still P1-A / P2-B / P2-C consolidation — touches 12+ files, ~700 LOC net reduction.

---

## Phase 3 — Combat & armor

**Files reviewed:** ArmorStateTests (20), ArmorSystemTests (34), DamageSystemTests (19), StatusEffectSystemTests (9), EquipmentSystemTests (7) — **89 tests**.

**Docs used:** `armor-system.md` (full pipeline), `battle-design-status.md` (design intent, hard rules).

### 3.1 Critical architecture violation

#### ❓ P3-α. ArmorSystem.Calculate reads DevCheats directly — HIGHEST concern
Per `testing-and-workflow.md` §1: **"MUST: no DevCheats in systems under test."**

`ArmorSystem.Calculate` at [ArmorSystem.cs:64,88,91](Assets/Scripts/Systems/ArmorSystem.cs:64) directly reads `DevCheats.ForceNoArmor`, `DevCheats.ForceMaxArmor`, `DevCheats.ArmorK`. All 34 `ArmorSystemTests` pass only because DevCheats defaults match `ArmorConstants`. If a dev toggles `ForceNoArmor=true` in the editor, every `Calculate_*` test silently breaks production invariants (armor stops reducing damage) but **tests still pass** because they don't go through `Calculate` with stateful DevCheats.

**Fix plan (separate task, scope = ArmorSystem refactor):**
1. Create `ArmorConfig` struct (`DamageReductionK`, `ForceNoArmor`, `ForceMaxArmor`, `DurabilityThreshold`, `DurabilityParabolicPower`, `RicochetChance`).
2. Add `ArmorConfig ArmorConfig` field to `RaidContext`; populate from DevCheats in `RaidSession.Tick`.
3. Change `ArmorSystem.Calculate` signature to accept `in ArmorConfig`.
4. Tests use `ArmorConfig.Default` (deterministic).

**Flagged as blocking dependency for any future armor work.** Not a test issue per se — but tests give false confidence until this is fixed.

### 3.2 Cross-cutting issues

#### 🏗 P3-A. `ArmorStateTests` file mixes 4 unrelated subjects
20 tests in the file actually test:
- **ArmorState core** (8): Create, IsBroken, DurabilityPercent — legitimate
- **ArmorSlotState default init** (1) — minor
- **ProjectileEntityState.Create** (2) — **wrong fixture** (nothing to do with armor state)
- **ItemDefinition registry** (8) — **wrong fixture**
- **RaidState.ArmorMap init** (1) — belongs in RaidState tests if any

**Fix plan:** rename / split:
- Keep `ArmorStateTests` with 8 core + 1 slot tests (9 tests)
- Move ProjectileEntityState tests → new `ProjectileEntityStateTests.cs` (or fold into `ProjectileSystemTests`)
- Move ItemDefinition tests → new `ItemDefinitionRegistryTests.cs`
- Move RaidState.ArmorMap → new `RaidStateTests.cs`

#### 🔁 P3-B. Context-creation boilerplate (again)
DamageSystemTests, StatusEffectSystemTests, EquipmentSystemTests all have their own `CreateContext` helper. Folds into P2-B's `TestContextFactory`.

#### 🔁 P3-C. Damage scenario setup repeated 15+ times in DamageSystemTests
Each test: allocate EIds → health → armor → projectile → HitSignal → context → tick. ~20 LOC × 15 tests = ~300 LOC boilerplate.

**Fix plan:** DSL helper in `Tests.EditMode.Fakes`:
```csharp
static (RaidState, EId targetId, FakeRaidEvents) SetupHitScenario(
    float targetHp = 100f,
    ArmorSlotState armor = null,
    float damage = 25f, float pen = 30f, float armorDmg = 10f,
    bool isHeadshot = false, float headshotMul = 1f, float bleedChance = 0f);
```
Cuts DamageSystemTests from ~700 LOC to ~300 LOC.

### 3.3 Per-file findings

#### ArmorStateTests.cs (20 tests)

🪶 **P3-D. ItemDefinition smoke tests are weak** (8 tests). `ItemDefinition_HelmetBasic_HasArmorStats` just checks `> 0`; `ItemDefinition_HelmetBasic_HasArmorPrefabId` checks `!= null`. These are **fixture validity assertions**, not logic tests. Failing means someone broke the SO data, but a single parameterized test or a one-line registry-integrity check would do the same job.

**Fix plan after relocation (P3-A):** replace 8 fixture-smoke tests with:
- 1 parameterized `Registry_CoreItems_HaveExpectedShape` over {Helmet_Basic, Armor_Basic, Ammo_Rifle, Ammo_Rifle_AP, Ammo_Rifle_HP}.
- Keep `AmmoRifleAP_HasHigherPenetration_ThanStandard` — this asserts a **design contract** ("AP > Standard"), worth its own test.
- Drop `ArmorPrefabId != null` tests (trivial field-loaded check; prefab existence is tested by Unity at load time).

🪶 **P3-E. `ArmorSlotState_DefaultSlotsAreNull`** — tests that reference fields default to null. Guaranteed by C#. **Drop.**

🪶 **P3-F. `ProjectileCreate_DefaultPenetration_Zero`** — tests that default params pass as 0. Compiler-guaranteed. **Drop after move (P3-A).**

#### ArmorSystemTests.cs (34 tests)
✅ Most thorough math coverage in the codebase. Pen curve, durability multiplier, durability damage scaling, ricochet conditions, `Calculate` orchestrator — every branch covered.

❓ **P3-G. Literal expected values couple to DevCheats / ArmorConstants defaults**. `EffectiveDurabilityMultiplier_BelowThreshold_ReturnsParabolic` hardcodes `0.510f`. If threshold changes from 0.7 → 0.6, test fails even though system is correct at new config. **Pragmatic hybrid fix:** keep literal checks but add 1-2 "formula invariant" tests that parameterize the constants (e.g., "at threshold, multiplier == 1; at 0, multiplier == 0; monotonic decrease below threshold").

🔁 **P3-H. `Calculate_HeadshotUsesHelmet` + `Calculate_BodyshotUsesVest`** — two parallel tests covering the same `GetArmorForHit` routing already tested directly above. Minor redundancy — they do verify `Calculate`'s integration not just `GetArmorForHit`, so keep.

#### DamageSystemTests.cs (19 tests)
✅ Strong integration coverage of the full `DamageSystem.Tick` pipeline. Covers ricochet deterministic flow, armor degradation → subsequent hits stronger, multi-event firing (ricochet + armor break).

🕳 **P3-I. Skip-check branches uncovered.** armor-system.md §5 lists skip checks: self-hit / dead target / rolling (i-frames) / god mode. Only self-hit is tested. **Action:** add 3 scenarios — dead target doesn't take damage, rolling target has i-frames, god mode blocks damage.

🕳 **P3-J. No cap tests.** Docs reference `PenetrationCap=100`, `ArmorPointsCap=100`, `ArmorDamageCap=30` as hard caps via DevCheats. Zero tests verify clamping. **Action:** 3 tests covering cap application (pen input 150 → effective 100, etc.). Also flags P3-α work (caps live in DevCheats).

🕳 **P3-K. Bleed-ignores-armor design rule not explicitly tested.** Design doc: "HP ammo vs heavy armor = low direct damage but bleed still works". Current bleed tests use unarmored targets. **Action:** add 1 test — target with heavy body armor + bleed-heavy projectile → zero HP damage (armor absorbs) + Bleeding effect applied. Closes design-intent gap explicitly.

🔁 **P3-L. `Tick_ArmorBreaksDuringHit_EventFired` + `Tick_HeadshotHelmetBreakEvent_IsHelmetTrue`** — body break + helmet break. Both useful (verify `IsHelmet` field). Minor redundancy. Keep.

🏗 **P3-M. `Tick_BodyshotNeverRicochets`** — runs 20 iterations in a for-loop. Probabilistic test. Fragile if random implementation changes. Better: inject rand `() => 0.1f` (which would force ricochet if any helmet present), assert no ricochet event. Deterministic + faster.

#### StatusEffectSystemTests.cs (9 tests)
✅ Clean bleed L1/L2 coverage — apply, upgrade, downgrade, tick.

🕳 **P3-N. GAP: No test for `0 HP` entity receiving bleed tick.** Edge case — bleed tick shouldn't apply to corpse. Doc unclear; current behavior unknown. Low priority but worth locking.

🕳 **P3-O. GAP: No test for simultaneous bleeding on multiple entities.** `Dictionary<EId, List<StatusEffectInstance>>` indexing bugs would not be caught. 1 test suffices.

🕳 **P3-P. GAP: No test for tick interval.** Design says "per-tick damage" — tests use `ElapsedTime = 1.5f` past one tick, then assert damage applied once. No test that `ElapsedTime = 2.5f` triggers 2 ticks (or 1 — depending on impl). Depending on system design this may be fine, but uncovered.

#### EquipmentSystemTests.cs (7 tests)
✅ Solid on `SyncArmorFromInventory` — equipped/unequipped/custom durability/default/non-armor.

🕳 **P3-Q. WriteBackDurability COMPLETELY UNTESTED.** armor-system.md §8: "Must be called before `SyncArmorFromInventory`" — critical for persisting combat damage across equip swaps. 0 tests. **Highest-priority gap in Phase 3.** Add 3 tests:
1. `WriteBackDurability_CopiesArmorMapToInventory`
2. `WriteBackDurability_NullSlots_NoThrow`
3. `EquipSwapCycle_DurabilityPreserved` — full WriteBack + re-Sync, combat damage survives.

🏗 **P3-R. `SyncArmor_ItemWithDefaultDurability_UsesDefinition`** — tight coupling to `ItemDefinition.Get("Helmet_Basic").MaxDurability == 100f`. Acceptable (real registry integration) but flag if data tuning destabilizes tests.

### 3.4 Phase 3 summary

**89 tests → proposed 79 after cleanup + 12 new coverage → net ~91.**

| Priority | Action | Files affected |
|---|---|---|
| **P0 CRITICAL** | P3-α: refactor ArmorSystem.Calculate to read from `ArmorConfig` in `RaidContext` (not DevCheats) | ArmorSystem, RaidContext, RaidSession |
| **P0** | P3-Q: add 3 WriteBackDurability tests | EquipmentSystem |
| **P1** | P3-A: split ArmorStateTests — move 11 tests to correct fixtures | ArmorState, new files |
| **P1** | P3-I: add 3 skip-check tests (dead/rolling/godmode) | DamageSystem |
| **P1** | P3-K: add bleed-ignores-armor integration test | DamageSystem |
| **P1** | P3-C: introduce `SetupHitScenario` DSL — ~300 LOC cut | DamageSystem |
| **P2** | P3-D, P3-E, P3-F: drop 8 weak tests after relocation | ArmorState |
| **P2** | P3-J: 3 cap tests (after P3-α refactor) | DamageSystem / ArmorSystem |
| **P2** | P3-M: replace loop-based ricochet test with deterministic rand | DamageSystem |
| **P3** | P3-G, P3-H, P3-L, P3-N, P3-O, P3-P, P3-R: minor hygiene / optional gaps | various |

**Net effect:** −8 weak/obsolete, +12 critical coverage. P3-α refactor unblocks proper Phase 3 test isolation.

### 3.5 Phase 1–3 running total

- **Reviewed:** 281 tests (16 files)
- **Proposed deltas:** −25 dropped, +22 added = net **−3 tests, +cleaner foundation**
- **Critical finding:** P3-α (DevCheats leak into ArmorSystem) — prerequisites for proper test isolation
- **Biggest refactor still:** P1-A + P2-B + P3-C — centralize SO factories, context factory, damage-scenario DSL

---

## Phase 4 — Bots & perception

**Files reviewed:** BotBrainSystemTests (6), BotCombatSystemTests (6), BotHealTests (15), BotPerceptionSystemTests (5), BotSpawnSystemTests (9), PlayerFOVSystemTests (15) — **56 tests**.

**Docs used:** `bot-ai.md` (BT framework + nodes + perception + combat pipeline + spawn + bot types).

### 4.1 Critical architecture violation (duplicate of P3-α)

#### ❓ P4-α. PlayerFOVSystem reads DevCheats directly — same pattern as ArmorSystem
Same violation as P3-α. `PlayerFOVSystem.Tick` reads `DevCheats.FOVEnabled / ForceShowAllBots / FOVNearRadius / FOVFarRadius / FOVAngle / FOVOcclusionEnabled`. Tests manage this by setting DevCheats in `SetUp` — **explicit but still a rule violation** per `testing-and-workflow.md`.

**Side effect:** test pollution. `PlayerFOVSystemTests.SetUp` sets DevCheats values but `TearDown` doesn't restore. Any test fixture that runs after `PlayerFOVSystemTests` sees non-default FOV values. Low-impact today (most tests don't touch FOV), but latent risk.

**Fix plan:** same shape as P3-α — create `FOVConfig` struct in `RaidContext`, route PlayerFOVSystem through it, tests use `FOVConfig.Default`. Bundle with P3-α fix as **"DevCheats → Config structs refactor"** task.

### 4.2 Cross-cutting issues

#### 🔁 P4-A. `CreateContext` duplicated in all 6 bot test files
Same pattern as P2-B. 6 more instances of the boilerplate. **Part of `TestContextFactory` consolidation.**

#### 🏗 P4-B. Per-file state-setup helpers are inconsistent
- `BotBrainSystemTests.CreateStateWithBot(typeId, pos, waypoints)`
- `BotCombatSystemTests.CreateStateWithBotWantingToFire(typeId)`
- `BotHealTests.CreatePMCSafe(hpRatio, elapsedTime, lastDamageTime)`
- `BotPerceptionSystemTests.CreateStateWithPlayerAndBot(playerPos, botPos, botType)`

Each test file has its own bot-setup helper with different signature. Since `BotSpawnSystem.SpawnBot` itself is stable, one fluent `BotTestBuilder` helper would simplify. Low priority — each current helper is scoped to its fixture's needs.

### 4.3 Per-file findings

#### BotBrainSystemTests.cs (6 tests) — **SEVERELY UNDER-TESTED**

🕳 **P4-C. HUGE coverage gap — BT framework primitives are ZERO-tested.**
Per `bot-ai.md` §2, the BT framework has 4 composite/decorator node types:
- **BTSelector** — priority fallback (ticks children left-to-right, returns first non-Failure)
- **BTSequence** — all-must-succeed
- **BTCondition** — predicate gate
- **BTCooldown** — rate limiter (wraps child with timer)

**None of these have tests.** This is the foundation of bot AI — `Selector` priority ordering is the entire reason the tree works. **Action:** add 8-10 dedicated tests for BT primitives (4 nodes × 2-3 scenarios each). Critical for any future BT authoring confidence.

🕳 **P4-D. Priority order not tested.**
Doc: "Priority order: Heal > Dodge > Combat (Grenade > Shoot > Chase) > Patrol." This ordering is **the most important design rule** in the system. Zero tests verify that, e.g.:
- Heal preempts combat when both conditions satisfy
- Dodge preempts shoot when damaged
- ThrowGrenade preempts shoot when target behind cover
- Patrol fires only when no higher-priority branch succeeds

**Action:** add 3-4 priority tests covering key conflicts.

🕳 **P4-E. Dodge, ThrowGrenade nodes untested.**
Docs describe both with specific conditions and state transitions. `DodgeNode` / `ThrowGrenadeNode` have 0 tests. **Action:** 2-3 tests each.

✅ Current 6 tests: decent smoke coverage (patrol, shoot, chase, dead, heal).

#### BotCombatSystemTests.cs (6 tests)

🕳 **P4-F. GrenadeThrow untested in CombatSystem.** Doc §7: "Runs every frame, processes three intent flags" — Fire / Heal / Grenade. Grenade path has 0 tests. If BotBrainSystem sets `WantsToThrowGrenade`, does CombatSystem spawn a grenade at correct velocity? Unverified. **Action:** 1-2 tests.

🪶 **P4-G. `Tick_EmitsProjectileSpawnedEvents`** asserts `>= 1` events. Could verify exact count matches `state.Projectiles.Count`. Minor stylistic.

✅ Otherwise solid — fire/heal/cooldown/multi-pellet covered.

#### BotHealTests.cs (15 tests)
✅ **Exemplary deep coverage of a complex sub-feature.** All branches of heal decision tree: emergency threshold, safe threshold, damage-recency gate, visibility gate, distance gate, reloading gate, no-medkits, cooldown, Scav-can't-heal, emergency-vs-normal cooldown duration. Good fixture pattern with `CreatePMCSafe` helper.

🔁 **P4-H. `Scav_CannotHeal` duplicated** in both `BotHealTests` and `BotBrainSystemTests.Tick_Scav_CannotHeal`. Keep the one in BotHealTests (domain-specific file); drop from BotBrainSystemTests.

#### BotPerceptionSystemTests.cs (5 tests) — UNDER-TESTED

🕳 **P4-I. Damage alert detection path untested.** Doc §5 detection sources:
| Source | Condition |
| Vision | … |
| Hearing | … |
| Damage alert | `WasDamaged` flag set externally |

Only vision (+ negative cases) and hearing tested. **Damage alert is the third pillar** — if player shoots from behind cover, bot should react via WasDamaged flag. **Action:** add 1 test.

🕳 **P4-J. `PerceptionTickInterval` (0.2s) not tested.** Doc says perception runs on fixed interval, not every frame. No test verifies that back-to-back Ticks don't re-run perception inside the interval. Perf-relevant contract untested. **Action:** 1 test — two ticks 0.1s apart should only process once.

🕳 **P4-K. `CanSeeTarget` vs `HasTarget` distinction weak.** These are different (doc §5: `HasTarget` = memory, `CanSeeTarget` = current LOS). Only `TargetLostAfterMemoryExpires` touches both. Worth: player hides → `CanSeeTarget = false` while `HasTarget = true` until memory expires. **Action:** 1 test locking this distinction.

#### BotSpawnSystemTests.cs (9 tests)
✅ Excellent — covers all spawn contract items: entity / health / event / weapon / waypoints / armor variants by type.

🕳 **P4-L. `MedkitsRemaining` / `GrenadesRemaining` spawn values untested.** Doc §8 step 4: "Sets MedkitsRemaining and GrenadesRemaining from config." No direct test. (Indirectly validated via `BotHealTests.Heal_NoMedkitsRemaining_DoesNotHeal` needing medkits to exist.) **Action:** 1 test — PMC spawn has 2 medkits, 2 grenades per config.

🪶 **P4-M. `SpawnBot_SetsPatrolWaypoints`** only asserts length. Could check values match input. Minor.

#### PlayerFOVSystemTests.cs (15 tests)
✅ Thorough angle / distance / occlusion coverage after recent fix (session's task 2+3). Good structure: near-radius / sector / occlusion clusters.

❓ **P4-N. DevCheats pollution in SetUp without TearDown.** Tests set DevCheats values but never restore. Latent test-order bug. **Action:** add TearDown that restores defaults OR fix via P4-α refactor (becomes moot when config is per-context).

🏗 **P4-O. `FOVDisabled_AllBotsVisible` + `ForceShowAllBots_AllVisible`** — two tests flipping different DevCheats flags for the same outcome. OK as coverage but verifies both paths. Keep.

### 4.4 Phase 4 summary

**56 tests → proposed 55 after cleanup + 19 new coverage → net ~74.**

| Priority | Action | Files affected |
|---|---|---|
| **P0 (bundle with P3-α)** | P4-α: route PlayerFOVSystem through `FOVConfig` in RaidContext (same fix pattern) | PlayerFOV, RaidContext, RaidSession |
| **P0** | P4-C: add ~8-10 BT framework primitive tests (Selector/Sequence/Condition/Cooldown) | new BT*Tests file |
| **P0** | P4-D: add 3-4 priority-order tests (Heal > Dodge > Combat > Patrol) | BotBrainSystem |
| **P1** | P4-E: add 2-3 tests each for DodgeNode and ThrowGrenadeNode | BotBrainSystem |
| **P1** | P4-F: add GrenadeThrow tests in CombatSystem | BotCombat |
| **P1** | P4-I: damage-alert detection test | BotPerception |
| **P2** | P4-H: drop duplicate `Scav_CannotHeal` | BotBrainSystem |
| **P2** | P4-J: perception tick interval test | BotPerception |
| **P2** | P4-K: CanSeeTarget vs HasTarget distinction test | BotPerception |
| **P2** | P4-L: MedkitsRemaining / GrenadesRemaining spawn test | BotSpawn |
| **P3** | P4-N: TearDown DevCheats restore (or fold into P4-α) | PlayerFOV |
| **P3** | P4-G, P4-M, P4-O: minor hygiene | various |

**Net effect:** −1 duplicate, +19 critical new tests for BT framework + priority ordering + untested nodes + untested detection paths. Phase 4 exposes the **biggest coverage debt** in the project — BT framework and priority ordering are core systems with essentially zero unit tests.

### 4.5 Phase 1–4 running total

- **Reviewed:** 337 tests (22 files)
- **Proposed deltas:** −26 dropped, +41 added = net **+15 tests**, significantly better coverage
- **Biggest gap:** Phase 4 BT framework / priority ordering (0 tests for critical AI logic)
- **Critical architectural debt:** P3-α + P4-α = bundled **"DevCheats → Config refactor"** task, unblocks proper isolation for armor + FOV
- **LOC consolidation remains:** P1-A (SO factories) + P2-B (context factory) + P3-C (damage scenario DSL) ≈ 900 LOC of boilerplate reduction

---

## Phase 5 — Player & world

**Files reviewed:** AimingSystemTests (24), MovementSystemTests (8), LootSystemTests (10), PlayerSpawnSystemTests (8) — **50 tests** (down from 51 after session's P1-G fix).

**Docs used:** `inventory-and-items.md` (inventory / loot / equipment / quick slots / crafting / status effects / healing / stamina / quests), `weapons.md` (dual-layer aiming / ADS / recoil), earlier docs for spawn contract.

### 5.1 Architecture violation — third DevCheats leak

#### ❓ P5-α. MovementSystem reads DevCheats directly
Third instance of the pattern from P3-α / P4-α. `MovementSystem.Tick` at [MovementSystem.cs:34-35](Assets/Scripts/Systems/MovementSystem.cs:34) reads `DevCheats.MoveSpeedMultiplier` and `DevCheats.AdsMoveSpeedMultiplier`. All 8 `MovementSystemTests` pass because defaults are 1.0 (no-op), but any DevCheats toggle breaks production silently.

**Bundle with P3-α + P4-α:** single **"DevCheats → Config structs refactor"** task covering ArmorConfig, FOVConfig, and a new **MovementConfig** (or fold into existing AimConfig — shares ADS-related state).

### 5.2 Cross-cutting issues

#### 🔁 P5-β. CreateContext boilerplate (fourth mention)
All 4 Phase 5 files define their own `CreateContext`. Same consolidation pattern as P2-B / P3-B / P4-A — feeds into `TestContextFactory`.

### 5.3 Per-file findings

#### AimingSystemTests.cs (24 tests)
✅ **Arguably the best-tested system in the project.** Thorough coverage of the dual-layer aim pipeline (RawAim instant / WeaponAim smoothed / AimDirection derived), FacingDirection cone-snap behavior, AimFollowSharpness dynamics, convergence over many ticks, straight-line position lerp.

🕳 **P5-C. MinAimDistance clamp untested** (the new feature that obsoleted the deleted test from this session's task 1). weapons.md / AimingSystem.cs:35-56 documents: cursor within minAimDist of player → clamp direction, keeping weapon responsive but avoiding jitter. **Zero tests locked this behavior.** **Action:** add 2 tests — (a) aim on exact player position falls back to `AimDirection`, (b) aim within clamp range produces aimPoint at `minAimDist` radius.

🕳 **P5-D. Recoil RecoilOffset flow untested.** weapons.md §"Dual-Layer Aiming" + crosshair.md §"Subtract-apply pattern" document core recoil decay. AimingSystem applies recoil decay every tick. Zero tests exercise `weapon.RecoilOffset` evolution. **Action:** 2 tests — (a) non-zero recoil decays toward zero over time, (b) WeaponAimPoint = cleanAim + RecoilOffset is maintained across tick.

🕳 **P5-E. ADS blend affecting aim sharpness untested.** Doc: `AimFollowSharpness *= Lerp(1, AdsAimFollowMultiplier, AdsBlend)`. No test varies `AdsBlend` and verifies sharpness change. ADS is core combat feel. **Action:** 1 test comparing ADS=0 vs ADS=1 convergence speeds.

🪶 **P5-F. `Tick_AimAtExactConeEdge_BodyLerpsNotSnaps`** — uses 45° (default `ConeHalfAngle`). Asserts `angle > 0.1f` — not a strict boundary test. Could tighten to "angle in (startAngle, endAngle)" bracket. Minor.

#### MovementSystemTests.cs (8 tests)
✅ Clean basic coverage — cardinal inputs, zero, diagonal normalization, deltatime, velocity, null guard, accumulation.

🕳 **P5-G. ADS move speed blend untested.** MovementSystem scales speed by `Lerp(1, AdsMoveSpeedMultiplier, AdsBlend)`. weapons.md §ADS documents this. **Zero coverage of important combat feel.** **Action:** 1 test comparing ADS=0 vs ADS=1 position after same input. Blocked by P5-α fix (move multiplier config needs plumbing).

🕳 **P5-H. Sprint speed multiplier path untested.** MovementSystem has `sprintScale` (integrates with `StaminaSystem.IsSprinting`). Stamina docs mention `SprintSpeedMultiplier = 1.6x`. **Zero tests verify sprint actually multiplies speed.** **Action:** 1 test — IsSprinting=true doubles-ish movement distance per second.

🕳 **P5-I. Roll override untested here.** bot-ai.md §6 mentions roll override in BotMovement, but player MovementSystem may have similar. If player rolling overrides velocity direction, it's untested. **Low priority** (RollSystem would be the right test home — but there's no `RollSystemTests.cs`).

🪶 **P5-J. `Tick_DiagonalInput_IsNormalized`** uses `Vector2(1,1)` (non-normalized input). Could also test partial input (`Vector2(0.5, 0.5)`) to cover thumbstick scenarios. Minor.

#### LootSystemTests.cs (10 tests)
✅ Covers corpse loot creation, armor preservation (3 tests), transfer + swap + AllowedSlots validation, FindNearestLootable range.

🕳 **P5-K. `CreateContainer` completely untested.** Doc `inventory-and-items.md` §7 fully documents container creation flow: rolls `MinDrops..MaxDrops`, picks random entries from `PossibleDrops`, clamps counts. 3 container types (MedContainer, AmmoBox, RandomLootBox) — all untested. **Action:** add 2-3 tests — container spawns N items in range, items respect `MaxStackSize`, events fire.

🕳 **P5-L. `FindNearestInteractable` completely untested.** Doc: "scans all interactable types and scores by distance + facing direction dot product: `score = distance * (1 - 0.5 * dot)`". Core interaction routing — zero tests. **Action:** 2-3 tests — facing-aware scoring picks centered target over off-angle, multiple types filter correctly.

🕳 **P5-M. `TryPickUp` + `TryDrop` completely untested** here (only cross-inventory `TryTransfer`). Ground-item pickup with stacking has complex two-phase logic per doc §4. **Critical gap** for a core gameplay loop. **Action:** 3-4 tests — stackable partial stack fills, overflow into new slots, non-stackable goes to first free slot, no space → returns false.

🕳 **P5-N. `TryMove` (within same inventory) untested.** Doc §4 describes bidirectional swap validation. Only `TryTransfer` (cross-inv) tested. **Action:** 2 tests.

✅ `CreateLootable_BotBrokenArmor_NotInLoot` is a nice design-intent lock.

#### PlayerSpawnSystemTests.cs (8 tests)
✅ After session 1's fix (registry injection), tests cover player entity creation, spawn event, weapon hotbar slots, double-spawn protection, armor in backpack, empty ArmorMap.

🕳 **P5-O. "Inventory already populated" path untested.** PlayerSpawnSystem only gives starting loadout if `IsInventoryEmpty` OR `levelId == "shooting_range"`. Non-empty-inventory case (real raid re-spawn scenario) untested. **Action:** 1 test — spawn with pre-populated inventory → items preserved, no loadout overwrite.

🕳 **P5-P. `levelId == "shooting_range"` special case untested.** Shooting range should always clear inventory and give fresh loadout. **Action:** 1 test — spawn with non-empty inventory + `levelId="shooting_range"` → inventory reset + loadout.

🕳 **P5-Q. Starting-armor-in-backpack contract**: Tests correctly verify armor lands in backpack, ArmorMap is empty. But **no test verifies quick slot bindings** or **grenades/bandages/medkit** starting items (doc inventory-and-items.md implies starting loadout but doesn't enforce specific shape — PlayerSpawnSystem.GiveStartingLoadout does). Low priority — implementation detail, not hard contract.

### 5.4 Phase 5 summary

**50 tests → proposed 50 (no cleanup removals) + 17 new coverage → net ~67.**

| Priority | Action | Files affected |
|---|---|---|
| **P0 (bundle with P3-α + P4-α)** | P5-α: route MovementSystem through MovementConfig or AimConfig | Movement, RaidContext, RaidSession |
| **P1** | P5-C: add MinAimDistance clamp tests (2) — close session's gap | Aiming |
| **P1** | P5-D: add recoil decay / WeaponAimPoint = clean + recoil tests (2) | Aiming |
| **P1** | P5-K: add container creation tests (2-3) | Loot |
| **P1** | P5-M: add TryPickUp / TryDrop tests (3-4) — critical gameplay | Loot (possibly move to new InventorySystemTests) |
| **P1** | P5-L: add FindNearestInteractable tests (2-3) | Loot |
| **P2** | P5-E, P5-G: ADS-related blend tests (after P5-α fix) | Aiming, Movement |
| **P2** | P5-H: sprint multiplier test | Movement |
| **P2** | P5-N: TryMove within-inventory tests (2) | Loot |
| **P2** | P5-O, P5-P: player spawn loadout gating tests (2) | PlayerSpawn |
| **P3** | P5-F, P5-I, P5-J, P5-Q: minor hygiene | various |

**Net effect:** +17 critical new coverage. Phase 5 exposes that **core gameplay paths** (TryPickUp, CreateContainer, FindNearestInteractable, MinAimDistance clamp, recoil decay, ADS blend, sprint) have **zero unit tests**, despite being documented. Aiming is over-tested, Loot/Movement are under-tested.

### 5.5 Final Phase 1–5 running total

- **Reviewed:** 387 tests (26 files) — all Phase 0-scope EditMode tests
- **Proposed deltas:** −26 dropped, +58 added = **net +32 tests**
- **Three DevCheats leaks found:** P3-α (ArmorSystem) + P4-α (PlayerFOVSystem) + P5-α (MovementSystem) → **one bundled refactor task**
- **Critical coverage gaps identified:**
  - Phase 2 — ammo modifiers + convergence direction (ShootingSystem)
  - Phase 3 — `WriteBackDurability` (Equipment), skip checks + caps (Damage)
  - Phase 4 — BT framework primitives + priority ordering (bot AI, biggest debt)
  - Phase 5 — MinAimDistance / recoil / sprint / container creation / interactable routing / pickup
- **LOC consolidation**:
  - P1-A — WeaponBuilderTestFactory (~500 LOC)
  - P2-B — TestContextFactory (~80 LOC across 15 files)
  - P3-C — SetupHitScenario DSL (~300 LOC)
  - **Total ~900 LOC reduction**, one-time cost unblocks faster future test authoring

---

## Phase 6 — Final synthesis

See the **Executive summary** at the top of this document. This section is the reading order and the consolidated action plan — done in the exec summary rather than repeated here.
