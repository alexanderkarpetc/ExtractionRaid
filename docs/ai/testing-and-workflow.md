# Testing and Feature Workflow

Use this file for test tasks, implementation flow, and change acceptance.

## 1) Testing strategy

### Unit-level tests (EditMode)
Prefer testing systems with:
- synthetic state
- fake adapters / ports
- deterministic RNG
- event buffers

Systems should be testable without scenes.

**MUST: no DevCheats in systems under test.**
Systems must read tuning values from `RaidContext` config structs (`AimConfig`, `ShootingConfig`, etc.), never from `DevCheats.*` static accessors directly. This makes tests deterministic — they use `XxxConfig.Default` without depending on ScriptableObject state. Production code (`RaidSession`) populates these structs from `DevCheats` when creating the context. When adding new DevCheats-dependent logic to a system, add the field to the appropriate config struct (or create a new one) and wire it in `RaidSession.Tick()`.

### Pre-test architectural audit

Before writing (or extending) tests for a system, grep the system file for two red flags:

1. **`DevCheats.X` reads** — violates the rule above. Tests will pass via default-value alignment but silently diverge from production when a toggle changes. Refactor the system through a `RaidContext.*Config` struct first, then write tests (dev/test cheats like `GodMode` flow through `CheatsConfig` — add new cheats there). Known-violating systems as of 2026-04-24 and their symptoms are in `docs/ai/tests-review.md §Phase 3/4/5`. **Resolved 2026-05-15**: `DamageSystem` now reads `context.CheatsConfig.GodMode`.
2. **Direct Unity API calls** in `Tick` — `Physics.*`, `Time.*`, `GameObject.Find/FindObjectOfType`, `Resources.Load`, etc. This violates `CLAUDE.md §3` rule 9. Fake adapters in tests are bypassed → tests become semi-deterministic (pass locally, fail when real Unity state shifts). Route through an existing adapter port (`IPhysicsAdapter`, `ITimeAdapter`, …) or add a new one before writing the test.

If a violation is found, fixing the architecture takes priority over adding coverage — otherwise the new tests lock in false-green behaviour.

### Common test-hygiene pitfalls

- **Static state must be save+restored.** Tests that mutate `DevCheats.X`, `App.Instance` fields, or any module-level static need `SetUp`/`TearDown` to snapshot and restore, else the next fixture in the run inherits the altered state. Example: `PlayerFOVSystemTests` snapshots ~6 DevCheats flags and restores them.
- **One fixture = one subject.** `XxxTests.cs` tests `Xxx` only. When the file starts drifting into adjacent subjects (e.g. `ArmorStateTests` accumulating `ItemDefinition` + `ProjectileEntityState` tests), split into sibling fixtures. Makes ownership and failures obvious.
- **Extract shared helpers on the second copy, not the fifth.** Copy-paste an SO factory, context builder, or scenario setup once — fine. On the second occurrence, move it into `Assets/Tests/EditMode/Fakes/` (see `TestContextFactory`, `WeaponBuilderTestFactory` for the pattern). ~900 LOC of duplication had accumulated across 15+ files before the 2026-04-24 cleanup; don't let it rebuild.
- **Prefer `[TestCase]` when 3+ tests differ only by input.** Stop when the test body starts diverging. Don't write tests for things the compiler already guarantees (reference-field-defaults-to-null, `readonly struct` constructor assigns fields).
- **Asserting observables: use end-state, not exact counts.** `Assert.AreEqual(3, callCount)` on event/callback invocations breaks on any internal refactor that legitimately fires an extra event. Use `Assert.GreaterOrEqual(callCount, N)` plus an end-state check (`state.HasX`, `presenter.CanBuild`, etc.). Same for adapter call-count assertions — they lock implementation detail, not behaviour.

### Integration tests (PlayMode)
Use PlayMode tests only for Unity integration, such as:
- navmesh validation
- bullet collisions
- spawn / extraction triggers
- launch flow wiring

## 2) How to add a feature

When implementing a feature:
1. define the affected state (`RaidState`, `LevelState`, `PlayerProfileState`)
2. define required dependencies and expose them through context ports if needed
3. implement logic in a stateless system function
4. emit domain events for VFX/SFX/UI where needed
5. update presenter/view to visualize results or route callbacks
6. add or update unit tests
7. add PlayMode coverage only where Unity integration matters

