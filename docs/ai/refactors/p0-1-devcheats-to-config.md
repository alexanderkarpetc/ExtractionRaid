# P0-1 — DevCheats → Config structs refactor

> **Status:** planned (2026-05-05). Ready to execute.
> **Scope:** route `ArmorSystem`, `PlayerFOVSystem`, `MovementSystem` через `RaidContext.*Config` structs. Closes 3 latent CLAUDE.md §6.7 violations.
> **Driver doc:** [`tests-review.md`](../tests-review.md) §P0-1 / P3-α / P4-α / P5-α.
> **Estimate:** ~1 day (refactor) + ~2h (gap tests).

---

## 🚨 Critical: zero data loss requirement

**The user-tuned DevCheats SO assets MUST survive this refactor untouched.** They hold values calibrated through playtest sessions (armor K, FOV radii, ricochet chance, etc.) — losing them is a regression worse than the leak бутs we're fixing.

### Hard rules (non-negotiable)

1. **DO NOT modify SO asset files** under `Assets/Resources/Configs/DevCheats/` ні `Assets/Resources/Configs/ViewCheats/`. They stay byte-identical (verifiable via `git status`).
2. **DO NOT rename or delete** any field у `DevCheatsArmorSection`, `DevCheatsFOVSection`, `DevCheatsPlayerSection`, `DevCheatsADSSection`. Renaming a `[SerializeField]` field у Unity drops the serialized value silently. Rename of public/property is safe.
3. **DO NOT modify `DevCheats.cs` accessors** (e.g., `DevCheats.ArmorK`, `DevCheats.FOVNearRadius`). They remain canonical entry points для `RaidSession.Tick` reads + `DevCheatsWindow` UI bindings.
4. **DO NOT touch `DevCheatsConfig.cs` or `DevCheatsWindow.cs`.** UI flow stays identical.
5. **Config struct `Default` values are documentation only** — in production, `RaidSession.Tick` reads from DevCheats (which reads from SO assets з playtest-tuned values). Defaults only used у unit tests.

### Verification checklist

**Before starting** (to baseline):
- [x] 455/455 tests green (verified 2026-05-05).
- [ ] `git status` clean.
- [ ] Snapshot DevCheats SO contents (script нижче) — saved до `.refactor-baseline/` (gitignored). Provides post-refactor diff target.

**After each step:**
- [ ] `mcp__unityMCP__run_tests` EditMode → 455/455 (or 461/461 если додано gap tests).
- [ ] `git status` shows ONLY expected files modified (none у `Resources/Configs/DevCheats/`).

**Final verification:**
- [ ] Manual playtest у `ShootingScene_KillFeel.unity` — armor visually behaves identically (helmet ricochet, body absorption, bleeding); FOV visually identical (bot reveal radii, occlusion); player movement (speed, ADS slowdown) feels identical.
- [ ] Open `Window → Dev Cheats` — all sections render з previously-tuned values.
- [ ] `DevCheats SO snapshot` matches baseline (no field migrations corrupted data).

### Snapshot script

Run before refactor (creates plain-text dump of all SO field values для post-refactor diff):

```bash
mkdir -p .refactor-baseline && \
for f in Assets/Resources/Configs/DevCheats/*.asset; do \
  name=$(basename "$f" .asset); \
  cp "$f" ".refactor-baseline/$name.asset.txt"; \
done && \
echo "✅ Baseline saved to .refactor-baseline/"
```

After refactor, diff:
```bash
for f in Assets/Resources/Configs/DevCheats/*.asset; do \
  name=$(basename "$f" .asset); \
  diff "$f" ".refactor-baseline/$name.asset.txt" || echo "⚠️ $name CHANGED"; \
done
```

Expected output: zero `CHANGED` lines.

---

## Why

CLAUDE.md §6.7: *"Systems must not read `DevCheats.X` directly. Tunable values go through `RaidContext.*Config` structs."*

Three systems violate this rule today:

| System | DevCheats reads | File |
|---|---|---|
| `ArmorSystem.Calculate` | `ForceNoArmor`, `ForceMaxArmor`, `ArmorK` | [ArmorSystem.cs:64,88,91](../../Assets/Scripts/Systems/ArmorSystem.cs) |
| `PlayerFOVSystem.Tick` | `FOVEnabled`, `ForceShowAllBots`, `FOVNearRadius`, `FOVFarRadius`, `FOVAngle`, `FOVOcclusionEnabled` | [PlayerFOVSystem.cs:18-30](../../Assets/Scripts/Systems/PlayerFOVSystem.cs) |
| `MovementSystem.Tick` | `MoveSpeedMultiplier`, `AdsMoveSpeedMultiplier` | [MovementSystem.cs:34-35](../../Assets/Scripts/Systems/MovementSystem.cs) |

Bonus leak (cross-system, same fix shape): `DamageSystem.cs:54` reads `DevCheats.ArmorRicochetChance` and passes it як 4th arg до `ArmorSystem.ShouldRicochet(...)`. Folds into `ArmorConfig`.

**Why this matters now:**
- All ~93 armor + 15 FOV + 8 movement tests pass *because defaults match constants*. Toggle DevCheats у Editor → production silently breaks → tests still green. False confidence.
- Test pollution: `PlayerFOVSystemTests.SetUp` sets DevCheats values without `TearDown`. Other fixtures running after see polluted state.
- 6 GAP tests blocked: 3 caps tests (PenetrationCap / ArmorPointsCap / ArmorDamageCap), 1 ADS-blend movement test, 1 sprint-multiplier test.

---

## Pattern (matches existing `AimConfig` / `ShootingConfig` / `StaggerConfig`)

[`Session/RaidContext.cs`](../../Assets/Scripts/Session/RaidContext.cs) already has 3 production config structs. Add 3 more following identical shape:
- `public struct *Config` with `[field]` defaults у nested `static *Config Default => new ...`
- `RaidContext` constructor takes `*Config? = null` parameter, falls back до `*Config.Default`
- `RaidSession.Tick` reads DevCheats once, builds context, passes to systems

System signature change pattern:
- `public static void Tick(RaidState state, in RaidContext ctx)` — already takes ctx, just shift reads
- `public static T Method(...args)` (pure helpers like `ArmorSystem.Calculate`) — add `in *Config cfg` parameter

---

## Scope details

### 1. `ArmorConfig`

**Struct fields:**
```csharp
public struct ArmorConfig
{
    public bool ForceNoArmor;
    public bool ForceMaxArmor;
    public float DamageReductionK;        // = DevCheats.ArmorK
    public float RicochetChance;           // = DevCheats.ArmorRicochetChance

    public static ArmorConfig Default => new ArmorConfig
    {
        ForceNoArmor = false,
        ForceMaxArmor = false,
        DamageReductionK = 30f,            // matches DevCheatsArmorSection.DamageReductionK
        RicochetChance = 0.4f,             // matches DevCheatsArmorSection.RicochetChance
    };
}
```

**Signature changes:**
- `ArmorSystem.Calculate(rawDamage, penetration, armorDamage, armorSlots, isHeadshot)` → add `in ArmorConfig cfg` параметер
- `ArmorSystem.CalcDamageMultiplier(armor, pen)` → add `in ArmorConfig cfg` параметер OR keep current `(armor, pen, k)` signature (today already accepts `k` як optional float arg per ArmorSystemTests:83-110)
- `ArmorSystem.ShouldRicochet(helmet, pen, roll, ricochetChance)` → already accepts `ricochetChance` parameter — leave як є, just stop reading from DevCheats у DamageSystem call site

**Call sites to update:**
- [DamageSystem.cs:54](../../Assets/Scripts/Systems/DamageSystem.cs) — replace `DevCheats.ArmorRicochetChance` з `context.ArmorConfig.RicochetChance`
- [DamageSystem.cs:103](../../Assets/Scripts/Systems/DamageSystem.cs) — `ArmorSystem.Calculate(..., in context.ArmorConfig)`

**Test updates (~5 call sites у [ArmorSystemTests.cs](../../Assets/Tests/EditMode/ArmorSystemTests.cs)):**
- All `ArmorSystem.Calculate(...)` calls add `in ArmorConfig.Default` (or named test config for cap tests)
- Existing `CalcDamageMultiplier` overload tests — already pass `k` explicitly, no change needed

### 2. `FOVConfig`

**Struct fields:**
```csharp
public struct FOVConfig
{
    public bool Enabled;
    public bool ForceShowAllBots;
    public float NearRadius;
    public float FarRadius;
    public float Angle;                    // degrees, full cone
    public bool OcclusionEnabled;

    public static FOVConfig Default => new FOVConfig
    {
        Enabled = true,
        ForceShowAllBots = false,
        NearRadius = 6f,
        FarRadius = 25f,
        Angle = 130f,
        OcclusionEnabled = true,
    };
}
```

**Signature changes:** none — `PlayerFOVSystem.Tick` already takes `in RaidContext ctx`. Just shift reads:
- `DevCheats.FOVEnabled` → `ctx.FOVConfig.Enabled`
- `DevCheats.ForceShowAllBots` → `ctx.FOVConfig.ForceShowAllBots`
- etc.

**Test updates ([PlayerFOVSystemTests.cs](../../Assets/Tests/EditMode/PlayerFOVSystemTests.cs)):**
- `SetUp` currently sets DevCheats fields directly — replace з building `FOVConfig` per-test (or via fixture helper) and constructing `RaidContext` з explicit `fovConfig` param
- Drop `TearDown` need entirely (configs become per-context, no shared state)

### 3. `MovementConfig`

**Struct fields:**
```csharp
public struct MovementConfig
{
    public float MoveSpeedMultiplier;
    public float AdsMoveSpeedMultiplier;

    public static MovementConfig Default => new MovementConfig
    {
        MoveSpeedMultiplier = 1f,
        AdsMoveSpeedMultiplier = 0.7f,     // matches DevCheatsADSSection.AdsMoveSpeedMultiplier
    };
}
```

**Note:** tests-review.md §P5-α suggested folding into `AimConfig`. **Rejected:** AimConfig holds aim-related state (recoil recovery, aim follow); MovementSystem reading from AimConfig is awkward + future hooks (sprint multipliers, roll override speed) belong у MovementConfig. Keep separate.

**Signature changes:** none — `MovementSystem.Tick` already takes `in RaidContext context`. Shift reads:
- `DevCheats.MoveSpeedMultiplier` → `context.MovementConfig.MoveSpeedMultiplier`
- `DevCheats.AdsMoveSpeedMultiplier` → `context.MovementConfig.AdsMoveSpeedMultiplier`

**Test updates ([MovementSystemTests.cs](../../Assets/Tests/EditMode/MovementSystemTests.cs)):**
- 8 tests, all use `MovementConfig.Default` (no behavior change since defaults are 1.0/0.7) — touch each `Tick(state, in context)` site to pass explicit config when adding new ADS/sprint test cases

### 4. `RaidContext` extension

Add 3 fields + 3 constructor params (matches existing pattern):
```csharp
public readonly ArmorConfig ArmorConfig;
public readonly FOVConfig FOVConfig;
public readonly MovementConfig MovementConfig;

public RaidContext(...,
    ArmorConfig? armorConfig = null,
    FOVConfig? fovConfig = null,
    MovementConfig? movementConfig = null)
{
    ...
    ArmorConfig = armorConfig ?? ArmorConfig.Default;
    FOVConfig = fovConfig ?? FOVConfig.Default;
    MovementConfig = movementConfig ?? MovementConfig.Default;
}
```

### 5. `RaidSession.Tick` population

Extend the existing context-build block у [RaidSession.cs:421](../../Assets/Scripts/Session/RaidSession.cs):
```csharp
var context = new RaidContext(
    ...,
    aimConfig: new AimConfig { ... },
    shootingConfig: new ShootingConfig { ... },
    staggerConfig: new StaggerConfig { ... },
    armorConfig: new ArmorConfig
    {
        ForceNoArmor      = DevCheats.ForceNoArmor,
        ForceMaxArmor     = DevCheats.ForceMaxArmor,
        DamageReductionK  = DevCheats.ArmorK,
        RicochetChance    = DevCheats.ArmorRicochetChance,
    },
    fovConfig: new FOVConfig
    {
        Enabled            = DevCheats.FOVEnabled,
        ForceShowAllBots   = DevCheats.ForceShowAllBots,
        NearRadius         = DevCheats.FOVNearRadius,
        FarRadius          = DevCheats.FOVFarRadius,
        Angle              = DevCheats.FOVAngle,
        OcclusionEnabled   = DevCheats.FOVOcclusionEnabled,
    },
    movementConfig: new MovementConfig
    {
        MoveSpeedMultiplier    = DevCheats.MoveSpeedMultiplier,
        AdsMoveSpeedMultiplier = DevCheats.AdsMoveSpeedMultiplier,
    }
);
```

---

## Out of scope (intentional)

- **`RaidSession.cs:484` `DevCheats.AdsTransitionTime` read.** Inside the Tick body before MovementSystem runs. RaidSession itself is the boundary — DevCheats reads here are allowed per architecture rule (boundary system that builds context from external state). If we want strict separation, route through new `ADSConfig` struct in a follow-up. Marginal value, defer.
- **Cap tests behavior change.** P3-J / tests-review.md asks for 3 cap tests (Penetration, ArmorPoints, ArmorDamage). Those caps live у `DevCheatsArmorSection` but are not currently *applied* у any system code path — they're documented invariants без enforcement. Adding actual cap clamping is a behavior change, not a refactor — separate ticket.
- **`StaminaSystem.SprintSpeedMultiplier` (P5-H).** Lives у `StaminaConstants`, not DevCheats. Out of refactor scope; gap test addition only.
- **`DevCheatsCheatsSection`-side tunables** (e.g., `InfiniteAmmo`) уже у `ShootingConfig`. Done.

---

## Execution plan (incremental, tests green at each step)

| # | Step | Test gate |
|---|---|---|
| 1 | Add `ArmorConfig`, `FOVConfig`, `MovementConfig` struct definitions у [`Session/RaidContext.cs`](../../Assets/Scripts/Session/RaidContext.cs) + constructor params | compile clean |
| 2 | Populate all 3 у [`RaidSession.Tick`](../../Assets/Scripts/Session/RaidSession.cs) context-build block | 455/455 |
| 3 | Migrate `PlayerFOVSystem.Tick` reads — smallest, no signature change | 455/455 |
| 4 | Update `PlayerFOVSystemTests` SetUp/TearDown — drop DevCheats writes, build per-test FOVConfig | 455/455 |
| 5 | Migrate `MovementSystem.Tick` reads | 455/455 |
| 6 | Update `MovementSystemTests` (mostly no-op since defaults match) | 455/455 |
| 7 | Add `in ArmorConfig cfg` param до `ArmorSystem.Calculate`. Update `DamageSystem.Tick` call sites (2x: ricochet + Calculate) to pass `context.ArmorConfig` | compile clean |
| 8 | Update `ArmorSystemTests.Calculate` calls (~5 sites) — pass `ArmorConfig.Default` | 455/455 |
| 9 | Verify `DamageSystemTests` (19 tests) still green — they go through `DamageSystem.Tick(_, _, in context)` which now flows ArmorConfig | 455/455 |
| 10 | **Optional follow-up:** add 6 GAP tests → P3-J caps × 3 (если decide to enforce caps), P5-G ADS blend × 1, P5-H sprint mult × 1, P4-N FOV cleanup verification × 1 | 461/461 |

**Step ordering rationale:** struct definitions first (no behavior change) → wire population (still no behavior, defaults match) → migrate systems one by one (each is a small commit, easy to bisect if a test cracks). FOV first because zero signature changes; ArmorSystem last because Calculate signature ripple touches the most tests.

---

## Risk assessment

- **Low.** All defaults у new configs match current `DevCheats*` defaults. Tests pass before refactor → они pass after refactor by construction (assumption: DevCheats values у Editor not toggled — production assumption).
- **Mitigation:** if any single step regresses test count, revert that step без cascade (each step is independent).
- **Edge case:** if any test fixture relies on stale DevCheats state set by a *previous* test (P4-N pollution), migrating that fixture surfaces the bug — fix у same step.

---

## Files touched (estimate)

**Production:**
- `Session/RaidContext.cs` (+3 structs, +3 constructor params)
- `Session/RaidSession.cs` (~30 LOC у Tick context-build block)
- `Systems/ArmorSystem.cs` (~5 LOC, signature + body)
- `Systems/PlayerFOVSystem.cs` (~6 LOC, body only)
- `Systems/MovementSystem.cs` (~2 LOC, body only)
- `Systems/DamageSystem.cs` (~2 LOC, ricochet call site + Calculate call site)

**Tests:**
- `Tests/EditMode/ArmorSystemTests.cs` (~5 calls, add `ArmorConfig.Default`)
- `Tests/EditMode/PlayerFOVSystemTests.cs` (15 tests — replace SetUp DevCheats writes з per-test FOVConfig builder; drop TearDown if exists)
- `Tests/EditMode/MovementSystemTests.cs` (8 tests — no behavioral change but explicit configs added)
- `Tests/EditMode/DamageSystemTests.cs` (no expected change — context flows through)

**Total LOC estimate:** ~80 production + ~50 tests = ~130 LOC. Net: zero behavior change, +alignment з §6.7, −3 latent bugs.

---

## After-effects

- `tests-review.md` P0-1 / P3-α / P4-α / P5-α resolved.
- Unblocks P3-J cap tests, P5-G ADS blend test, P5-H sprint mult test (if those are pursued — separate gap-test pass).
- Unblocks P4-N (`PlayerFOVSystemTests` pollution) — fixture restoration becomes moot.
- Removes 3 false-confidence test layers (production silently broken if DevCheats toggled).
