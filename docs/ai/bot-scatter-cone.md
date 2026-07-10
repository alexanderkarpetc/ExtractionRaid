# Bot Scatter Cone — pseudorandom accuracy (shelved design, re-appliable)

**Status:** designed, implemented and fully tested on 2026-07-08, then **rolled back
after a playtest read as bad**. This doc preserves the complete design + exact code so
it can be re-applied (in whole or in parts) later. The live system it would replace is
the simpler *aim settle* multiplier from the Wave-1 humanization pass (see
`bot-humanization.md` §3, row "Aim settle + penalties").

---

## 1. Concept

Replace the flat accuracy multiplier with an **explicit aim-error cone half-angle in
degrees** (`bb.CurrentScatterDeg`) that everything pushes on:

| Force | Direction | Model reference |
|---|---|---|
| Continuous aiming at a visible target | cone **converges** exponentially toward per-type min (τ = 0.8 s) | Tarkov native `BETTER_PRICISING` |
| Each shot fired | **+kick** (~1.8°, worse for sloppy personalities) | SAIN `Recoil.cs`: `calcdRecoil = (weaponRecoil/baseline + add) × modifier`, decays by lerp |
| Taking a hit | **+3° flat kick** | SAIN arm-injury / suppression analog |
| Moving fast (fire time) | **×1.5** | SAIN `EnemyAim.cs` VelocityFactor; Tarkov `COEF_IF_MOVE` |
| Target unseen | **relaxes** back toward initial (τ = 1.5 s) | aim-memory fade; quick re-peek ≠ full reset |

Emergent behavior: spraying full-auto outruns convergence → long sprays get sloppy,
burst pauses genuinely re-settle aim. A fixed *angle* also scales miss distance with
range for free (what Tarkov fakes with `SCATTERING_DIST_MODIF`).

**Anti-luck sampling** (the "pseudorandom" part): while the cone is wider than
`min + margin`, shot deviations sample the **outer ring** `[0.6, 1] × scatter` —
guaranteed near-misses that telegraph "you're spotted"; never a lucky first-tick
headshot. Once converged, sampling is center-weighted (`scatter × rand × rand`).

Anchors from per-type config: `Min = (1-Accuracy) × 6°`, `Initial = Min + 8°`,
both divided by personality `AccuracyMult`. (Scav ≈ 3°/11°, PMC ≈ 1.5°/9.4°.)

Engagement arc: spot → react → first burst cracks past the player → pause, aim
settles → second burst dangerous → player breaks LoS → aim decays → repeat.

## 2. Why it may have felt bad in the playtest (check before re-applying)

The rollback verdict was "looks bad" without detail. Likely culprits, in order:

1. **Bots never hit at first contact.** `InitialScatterBonusDeg = 8°` + forced-miss
   ring means the entire first burst whiffs at any range. Fix: lower to 4–5°, or
   force-miss only the first 1–2 shots instead of ring-sampling while unconverged.
2. **Spray equilibrium too sloppy.** Kick 1.8°/shot vs converge τ 0.8 s: an auto
   rifle (~10 shots/s) settles at `min + ~14°` → bots visually "can't aim" in any
   sustained fight. Fix: kick 0.6–1.0°, or scale kick by `FireInterval` so fast
   weapons kick less per shot.
3. **Double penalty while strafing.** ShootNode always strafes during fire, so the
   ×1.5 moving mult applies almost constantly. Fix: drop the mult, or exclude the
   slow combat-strafe speed (threshold above `ChaseSpeed × ShootStrafeSpeedFraction`).
4. **Visible outer-ring pattern.** Ring sampling makes early shots form a donut
   around the player — can read as "aiming beside me on purpose". Fix: bias ring to
   `[0.4, 1]`, or blend ring→center as the cone converges instead of a hard threshold.

A safer re-application: keep the live aim-settle system AND add only the **recoil
kick** (+ its decay via the existing settle ramp) — the burst-pause interplay was the
strongest part of this design.

## 3. Exact implementation (as shipped before rollback)

Baseline for these diffs: the Wave-1 state (aim settle live). Fields/consts removed
here exist in that baseline; if the codebase has drifted, adapt names.

### 3.1 `Assets/Scripts/Constants/BotConstants.cs`

Remove the `--- Aim settle ---` block (AimSettleTime, AimSettleResetUnseenTime,
AimSettleStartAccuracyMult, MovingAccuracyMult, MovingAccuracySpeedThreshold,
RecentDamageAccuracyMult, RecentDamageAccuracyWindow) and add:

```csharp
// --- Scatter cone (pseudorandom accuracy; Tarkov "better precising" + SAIN recoil) ---
public const float MinScatterPerAccuracyDeg = 6f;
public const float InitialScatterBonusDeg   = 8f;    // suspect #1 — try 4-5
public const float ScatterConvergeTau       = 0.8f;  // s
public const float ScatterRelaxTau          = 1.5f;  // s
public const float ScatterRecoilKickDeg     = 1.8f;  // suspect #2 — try 0.6-1.0
public const float ScatterDamageKickDeg     = 3f;
public const float ScatterMaxDeg            = 16f;
public const float ScatterMovingMult        = 1.5f;  // suspect #3 — consider dropping
public const float ScatterMovingSpeedThreshold = 1.5f; // m/s
public const float ScatterForcedMissMarginDeg = 3f;
public const float ScatterForcedMissRingMin   = 0.6f; // suspect #4 — try 0.4

public static float MinScatterDeg(in BotTypeConfig config, float accuracyMult)
    => (1f - config.Accuracy) * MinScatterPerAccuracyDeg / Mathf.Max(0.5f, accuracyMult);

public static float InitialScatterDeg(in BotTypeConfig config, float accuracyMult)
    => MinScatterDeg(in config, accuracyMult) + InitialScatterBonusDeg / Mathf.Max(0.5f, accuracyMult);
```

### 3.2 `Assets/Scripts/State/BotBlackboard.cs`

Remove `AimSettle01`, `EffectiveAccuracy`, `LastCanSeeTime` (and their `Reset()` /
`ClearTarget()` lines); add:

```csharp
// Scatter cone — current aim-error half-angle in degrees. Set to Initial on first
// sight, converges while aiming (ShootNode), kicks per shot / on damage, relaxes
// while unseen (BotPerceptionSystem). 0 = model inactive → BotCombatSystem falls
// back to the flat (1-Accuracy)*10° spread (FireForward turrets, direct-intent tests).
public float CurrentScatterDeg;
```

Reset to `0f` in both `Reset()` and `ClearTarget()`.

### 3.3 `Assets/Scripts/Systems/Bot/BotPerceptionSystem.cs`

In the detected/`seen` branch (replacing the `LastCanSeeTime` aim-settle reset):

```csharp
if (seen)
{
    if (bb.CurrentScatterDeg <= 0f)  // first sight of this engagement
        bb.CurrentScatterDeg = BotConstants.InitialScatterDeg(in config, bb.AccuracyMult);
    bb.LastKnownTargetPos = player.Position;
}
```

After the `GrenadeThrowDelayTimer` reset, damage kick:

```csharp
if (alerted && bb.CurrentScatterDeg > 0f)
    bb.CurrentScatterDeg = Mathf.Min(BotConstants.ScatterMaxDeg,
        bb.CurrentScatterDeg + BotConstants.ScatterDamageKickDeg);
```

In the not-detected branch (before `TimeSinceTargetSeen` accumulation), relax:

```csharp
if (bb.CurrentScatterDeg > 0f)
{
    float initial = BotConstants.InitialScatterDeg(in config, bb.AccuracyMult);
    if (bb.CurrentScatterDeg < initial)
        bb.CurrentScatterDeg = Mathf.Lerp(bb.CurrentScatterDeg, initial,
            1f - Mathf.Exp(-BotConstants.PerceptionTickInterval / BotConstants.ScatterRelaxTau));
}
```

### 3.4 `Assets/Scripts/Systems/Bot/Nodes/ShootNode.cs`

Replace the aim-settle/EffectiveAccuracy block (keep strafe/sway/burst code around it):

```csharp
// Scatter convergence ("better precising")
if (bb.CurrentScatterDeg <= 0f)
    bb.CurrentScatterDeg = BotConstants.InitialScatterDeg(in config, bb.AccuracyMult);
float minScatter = BotConstants.MinScatterDeg(in config, bb.AccuracyMult);
bb.CurrentScatterDeg = Mathf.Lerp(bb.CurrentScatterDeg, minScatter,
    1f - Mathf.Exp(-ctx.DeltaTime / BotConstants.ScatterConvergeTau));
```

### 3.5 `Assets/Scripts/Systems/Bot/BotCombatSystem.cs` — `ProcessFire`

Replace the `EffectiveAccuracy` fallback + `accuracySpread` Euler rotation:

```csharp
var bb = bot.Blackboard; // hoist; remove the later duplicate declaration
bool scatterModel = bb.CurrentScatterDeg > 0f;
float scatterDeg = scatterModel ? bb.CurrentScatterDeg : (1f - config.Accuracy) * 10f;
if (bot.Velocity.magnitude > BotConstants.ScatterMovingSpeedThreshold)
    scatterDeg *= BotConstants.ScatterMovingMult;
scatterDeg = Mathf.Min(scatterDeg, BotConstants.ScatterMaxDeg);

float minScatter = BotConstants.MinScatterDeg(in config, bb.AccuracyMult);
bool converged = scatterDeg <= minScatter + BotConstants.ScatterForcedMissMarginDeg;
```

Per pellet (after the weapon `halfSpread` yaw, replacing the accuracy Euler block):

```csharp
if (scatterDeg > 0f)
{
    float deviation = converged
        ? scatterDeg * (Random.value * Random.value) // center-weighted
        : scatterDeg * Random.Range(BotConstants.ScatterForcedMissRingMin, 1f); // outer ring
    var ortho = Vector3.Cross(pelletDir, Vector3.up);
    if (ortho.sqrMagnitude < 0.0001f) ortho = Vector3.right;
    var axis = Quaternion.AngleAxis(Random.Range(0f, 360f), pelletDir) * ortho.normalized;
    pelletDir = Quaternion.AngleAxis(deviation, axis) * pelletDir;
}
```

After `weapon.LastFireTime = ...` / ammo decrement, recoil kick:

```csharp
if (scatterModel)
    bb.CurrentScatterDeg = Mathf.Min(BotConstants.ScatterMaxDeg,
        bb.CurrentScatterDeg + BotConstants.ScatterRecoilKickDeg / Mathf.Max(0.5f, bb.AccuracyMult));
```

### 3.6 `Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`

Replace the `Eff. Accuracy` field with:

```csharp
if (bb.CurrentScatterDeg > 0f)
    Field("Scatter", $"{bb.CurrentScatterDeg:F1}°");
```

### 3.7 Tests (all passed before rollback)

- `BotBrainSystemTests.Tick_AimingAtVisibleTarget_ScatterConverges` — Scav, scatter 11°,
  30 brain ticks (dt 1/60) → asserts `< 9°` and `> 2.9°` (never below min; expected ≈ 7.3°).
- `BotPerceptionSystemTests.Tick_TargetUnseen_ScatterRelaxesBackUp` — HasTarget, player
  out of range, scatter 3° → one perception tick → asserts `> 3°` (expected ≈ 4°).
- `BotCombatSystemTests.Tick_FiringKicksScatterUp` — scatter 5°, fire once →
  asserts `> 5.5°`.
- `BotCombatSystemTests.Tick_UnconvergedScatter_ForcesNearMiss` — scatter 12°,
  `Weapon.Stats.SpreadAngle = 0`, `AccuracyMult = 1` → pellet deviation from
  `DesiredAimPoint` direction asserted in `[12×0.6 − ε, 12.01]`°.
- `BotCombatSystemTests.Tick_ConvergedScatter_StaysInsideCone` — scatter 3° (Scav min,
  converged) → deviation ≤ 3.01°.

Existing suites needed no changes (fallback path keeps direct-intent tests and
FireForward turrets on the legacy flat spread).

## 4. Research sources

- SAIN aim factors (pose × visibility × optic-by-distance × injury × velocity,
  recalc every ~0.1 s): [EnemyAim.cs](https://raw.githubusercontent.com/Solarint/SAIN/4.1.0/Classes/Bot/EnemyClasses/Shoot/EnemyAim.cs)
- SAIN per-shot recoil + lerp decay, skill clamp 0.5–2.0×:
  [Recoil.cs](https://raw.githubusercontent.com/Solarint/SAIN/4.1.0/Classes/Bot/WeaponFunction/Recoil.cs)
- Tarkov native bot aiming params (`BETTER_PRICISING`, `COEF_IF_MOVE`,
  `SCATTERING_DIST_MODIF`, `COEF_FROM_COVER`):
  [SPT bot difficulty/AI system](https://deepwiki.com/sp-tarkov/server-csharp/6.5-bot-ai-and-difficulty-system)
- Design context and the rest of the humanization work: `bot-humanization.md`
