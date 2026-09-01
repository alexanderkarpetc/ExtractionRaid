# Bot AI System

## 1. Overview

The bot AI uses a **behavior tree (BT)** architecture with an intent-based execution model.
Each frame the pipeline runs in order:

1. **BotPerceptionSystem** -- updates target detection (vision, hearing, damage alerts)
2. **BotBrainSystem** -- ticks the behavior tree, which writes *intents* to `BotEntityState`
3. **BotMovementSystem** -- reads `DesiredVelocity`, applies NavMesh clamping
4. **BotCombatSystem** -- reads `WantsToFire`, `WantsToHeal`, `WantsToThrowGrenade` and spawns projectiles/grenades/heals

Behavior trees are composed per bot type in `BotTreeBuilder`, cached after first build.
Bot type configs live in `BotConstants` as `readonly BotTypeConfig` structs with default values overridden per type.

All systems are static, stateless, and iterate `RaidState.Bots`.

---

## 2. Behavior Tree Framework

### BTStatus

```
Success   -- node completed its goal
Failure   -- node cannot run / precondition unmet
Running   -- node is still executing (multi-frame)
```

### IBTNode

```csharp
interface IBTNode {
    string Name { get; }
    BTStatus Tick(BotEntityState bot, RaidState state, in RaidContext ctx, in BotTypeConfig config);
}
```

Every `Tick` return goes through the `Traced()` extension method, which records the status in `BotBlackboard.Trace` for debug visualization.

### BTSelector (priority fallback)

Ticks children left-to-right. Returns the status of the **first child that does not return Failure**. If all children fail, returns Failure.

### BTSequence (all-must-succeed)

Ticks children left-to-right. Returns the status of the **first child that does not return Success**. If all children succeed, returns Success.

### BTCondition (gate)

Wraps a `Func<BotEntityState, RaidState, BotTypeConfig, bool>` predicate. Returns Success if true, Failure if false. Used as the first child in a Sequence to guard combat/dodge branches.

### BTCooldown (rate limiter)

Wraps a child node. While the cooldown timer > 0, returns Failure and decrements the timer. When ready, ticks the child; on Success, resets the timer to `duration`. Timer state is stored in the blackboard via getter/setter lambdas (e.g. `bb.DodgeCooldownTimer`).

---

## 3. Tree Builder

`BotTreeBuilder.GetOrBuild(config)` returns a cached `IBTNode` tree per `TypeId`.

Tree structure is driven by `BotBehaviorFlags` on the config:

```
Root (Selector)
 +-- [if Heal]                       HealNode
 +-- [if Dodge]                      Sequence "Dodge"
 |                                     Condition "Damaged?" -> WasDamaged || IsRolling
 |                                     DodgeNode (owns config.DodgeCooldown via NextDodgeTime timestamp)
 +-- [if Shoot|Chase|MeleeAttack]    Sequence "Combat"
 |                                     Condition "HasTarget?"
 |                                     Selector "Tactics"
 |                                       [if ThrowGrenade] Cooldown (GrenadeCooldown)
 |                                                           ThrowGrenadeNode
 |                                       Selector "Engage"
 |                                         [if MeleeAttack] Cooldown (MeleeAttackCooldown)
 |                                                            MeleeAttackNode
 |                                         [if TakeCover]   TakeCoverNode
 |                                         [if Shoot]       ShootNode
 |                                         [if Chase]       ChaseNode
 +-- [if Patrol]                     PatrolNode
```

**BotBehaviorFlags** (bitmask):

| Flag          | Bit |
|---------------|-----|
| Patrol        | 0   |
| Chase         | 1   |
| Shoot         | 2   |
| Heal          | 4   |
| Dodge         | 5   |
| ThrowGrenade  | 6   |
| MeleeAttack   | 7   |
| FireForward   | 8   |
| TakeCover     | 9   |

Priority order: Heal > Dodge > Combat (Grenade > Melee > TakeCover > Shoot > Chase > Search) > Patrol.

**Humanization pass (2026-07-07)** — the Combat sequence is gated by `Alert?`
(`HasTarget && IsAlert`) instead of `HasTarget?`: the reaction window (accumulated in
`BotBrainSystem`, personality-scaled) must elapse before the bot chases, fires, or even
turns toward the target. A `SearchNode` after `ChaseNode` handles arriving at the last
known position without regaining sight (scan sweep → give up → patrol). See
The live tuning values are in `BotConstants`; speculative follow-up ideas belong in `tasks.md`.

---

## 4. Action Nodes

### PatrolNode

- **Purpose**: Walk between waypoints in a loop.
- **Conditions**: `PatrolWaypoints` array must be non-empty.
- **Behavior**: Moves toward current waypoint at `PatrolSpeed`. On arrival (< 1 m), advances index and waits `PatrolWaitTime` (2 s). Always returns Running.
- **Intents**: Sets `DesiredVelocity`.

### ChaseNode

- **Purpose**: Move toward the last known target position along NavMesh path corners
  (straight-line fallback without navmesh).
- **Conditions**: `HasTarget` must be true; returns Failure otherwise.
- **Behavior**: Path-follows at `ChaseSpeed`; repaths every 0.75 s or when the LKP
  drifts > 2 m from the cached path target. On arrival (< 1 m): Success if the target
  is visible, **Failure if not** — falls through to SearchNode.
- **Intents**: Sets `DesiredVelocity`.

### SearchNode (2026-07-07)

- **Purpose**: Investigate the last known position instead of freezing there.
- **Conditions**: `HasTarget && !CanSeeTarget` and within `SearchArriveDistance` of LKP.
- **Behavior**: Sweeps facing ±80° around the arrival heading for `SearchDuration`
  (4.5 s), then calls `bb.ClearTarget()` — the bot gives up and resumes patrol.
  BotMovementSystem skips its face-the-target override while `SearchEndTime >= 0`.

### ShootNode

- **Purpose**: Fire weapon at visible target with human trigger discipline.
- **Conditions**: `HasTarget && CanSeeTarget && DistanceToTarget <= EngageRange` (plus
  the global engagement-radius gate). Reaction is handled tree-wide by the `Alert?`
  condition, not here.
- **Behavior**: Strafes (aggression-scaled), sways aim (Perlin), and fires in
  **bursts** of 2–5 shots (aggression-scaled) with 0.35–0.9 s pauses. Computes
  `bb.EffectiveAccuracy` = config accuracy × personality × aim-settle ramp
  (×0.45→×1 over 0.9 s of continuous sight) × moving penalty × recently-hit penalty.
- **Intents**: Sets `DesiredAimPoint`, `WantsToFire` (only during a burst window).

### TakeCoverNode (2026-07-12)

- **Purpose**: SAIN-inspired fight-from-cover — pick a spot the enemy can't see, run
  there, then cycle hide → peek → shoot → duck back. Sits before `ShootNode` in the
  Engage selector.
- **Conditions**: `HasTarget && TimeSinceTargetSeen <= CoverEngageMemoryTime (4 s)
  && DistanceToTarget <= EngageRange`, plus Physics/NavMesh ports present. Fails
  otherwise → falls through to plain Shoot/Chase. Stale contact means the bot
  investigates (Chase/Search) instead of turtling.
- **Cover search**: ring-samples candidates around the bot (12 directions ×
  radii 2.5/5/8/11 m), navmesh-snaps each, keeps points where the ray from the
  **enemy eye** (`PlayerEyeHeight`) to the point at torso height (1.1 m) is blocked.
  Head-height (1.7 m) blocked too = full cover (score bonus). A point must also have
  a workable **peek spot** — a ±1.6 m lateral step with a CLEAR torso-height line to
  the enemy — otherwise it's rejected (cover you can't shoot from is a turtle spot).
  Score = run distance + penalty for being closer than 10 m to the enemy − full-cover
  bonus; candidates beyond `EngageRange × 0.9` from the enemy or closer than 4 m are
  rejected, as are spots **behind the enemy** (dot gate — reaching them means running
  through the target's line of fire; straight from SAIN's CoverAnalyzer) and spots
  near a recently blacklisted point. Failed search → 1.5 s cooldown before retrying.
- **Phase machine** (`bb.CoverPhase`): `MoveTo` (path-follow at ChaseSpeed, fires
  bursts on the move at ×0.5 accuracy while target visible; stuck ≥ 2 s → abandon
  point) → `Hold` (hidden, velocity zero, 1.2–2.8 s ÷ aggression; extended while
  reloading; **shot while hidden → the spot is compromised: blacklist it for 3 s
  (2 m radius, SAIN's "spotted point") and re-search**) → `Peek`
  (sidestep to peek spot at 0.8× ChaseSpeed) → `Exposed` (returns **Failure** so
  ShootNode owns the trigger; ducks back on: timer 1.5–3 s, taking a hit, reload
  start, or losing sight of the target) → `Return` (sidestep back) → `Hold` …
- **Revalidation**: every 0.5 s, or immediately when the LKP drifts > 3 m from the
  snapshot the cover was picked against — re-checks the blocked ray, min enemy
  distance, and recomputes the peek side. Broken cover → immediate re-search.
- **Intents**: Sets `DesiredVelocity`; during MoveTo also `DesiredAimPoint`,
  `WantsToFire`, `EffectiveAccuracy` (shared burst gates with ShootNode).
- **Enabled on**: PMC, RangedTarget.

### MeleeAttackNode (2026-05-10)

- **Purpose**: Contact damage for melee-only enemies (zombies). Sits BEFORE `ShootNode`/`ChaseNode` in the Engage selector — when target is within melee range, attack instead of chasing past or shooting at point-blank.
- **Conditions**: `HasTarget && DistanceToTarget <= MeleeAttackRadius`. Fails otherwise → falls through to Chase.
- **Behavior**: Sets `WantsToMeleeAttack = true`, plants feet (`DesiredVelocity = zero`), aims at target. Returns Success → wrapping `BTCooldown` resets `MeleeAttackCooldownTimer`.
- **Intents**: Sets `DesiredAimPoint`, `DesiredVelocity = zero`, `WantsToMeleeAttack = true`.
- **Dispatch**: `BotCombatSystem.ProcessMeleeAttack` reads the intent → `DamageSystem.ApplyMeleeDamage(state, targetId, MeleeAttackDamage, attackerId, hitPoint, hitDir, ctx)`. Direct HP damage (no projectile pipeline, no armor reduction for V0.1) + emits `EntityDamaged` / `EntityDied` / `EntityHit` for view feedback.
- **Config (per-type):** `MeleeAttackRadius`, `MeleeAttackDamage`, `MeleeAttackCooldown` on `BotTypeConfig`.

### DodgeNode

- **Purpose**: Perform a dodge roll perpendicular to the player direction.
- **Conditions**: Guarded by BTCondition (`WasDamaged || IsRolling`); cooldown is owned
  by the node itself — `bb.NextDodgeTime = ElapsedTime + config.DodgeCooldown`, armed
  when a roll actually starts. (Fixed 2026-07-08: the old BTCooldown wrapper armed only
  on Success, but the node returns Running — the per-type cooldown never applied and
  bots dodged as often as the global 0.8 s `DodgeConstants.Cooldown` allowed.)
- **Behavior**: If already rolling, returns Running. Otherwise picks a random lateral direction (left or right relative to player) and calls `RollSystem.StartBotRoll`. Returns Running on success, Failure if roll could not start.
- **Intents**: Triggers roll state on `BotEntityState`.

### HealNode

- **Purpose**: Use a medkit to restore HP.
- **Conditions**: Must be alive, have medkits remaining, and heal cooldown expired.
- **Two modes**:
  - **Emergency heal**: HP ratio < `EmergencyHealThreshold` (0.3) AND time since last damage > `EmergencyHealDelay` (1.5 s). Cooldown = `EmergencyHealCooldown` (8 s).
  - **Safe heal**: HP ratio < `HealThreshold` AND time since damage > `HealSafeDelay` (3 s) AND cannot see target AND distance > `HealSafeEnemyDistance` (10 m) AND not reloading. Cooldown = `HealCooldown` (15 s).
- **Intents**: Sets `WantsToHeal = true`. CombatSystem restores HP to max and decrements `MedkitsRemaining`.

### ThrowGrenadeNode

- **Purpose**: Throw a grenade at the target's last known position when they are behind cover.
- **Conditions**: `HasTarget && !CanSeeTarget && GrenadesRemaining > 0`. Distance must be between `GrenadeMinThrowDist` and `GrenadeConstants.MaxThrowRange`. Guarded by BTCooldown.
- **Behavior**: On first valid tick, starts a random delay (1-2 s). Returns Failure while waiting (so other branches can still run). When delay expires, sets throw intent.
- **Intents**: Sets `WantsToThrowGrenade = true`, `GrenadeThrowTarget`.
- **Note**: `CanSeeTarget` becoming true resets the delay timer (via PerceptionSystem setting `GrenadeThrowDelayTimer = -1`).

---

## 5. Perception System

Runs on a **fixed interval** of `PerceptionTickInterval` (0.2 s) per bot, not every frame.

### Detection Sources (humanization pass 2026-07-07)

| Source        | Condition |
|---------------|-----------|
| Vision        | In cone (range + angle, or within 2.5 m 360° close-sense) AND linecast clear AND **`VisionAwareness01` reached 1**. Awareness is instant inside 35 % of `VisionRange`, takes 0.15–1.1 s toward the edge, ×1.6 in the peripheral band, ×0.25 if already tracking a target; decays 0.5/s when sight breaks. |
| Hearing       | Distance <= noise radius: `HearingRange` walking, ×0.45 slow movement, ×2.2 sprinting, and **gunshots** (player fired within last 0.25 s) audible to `GunshotHearingRange` (40 m). |
| Damage alert  | `WasDamaged` flag (set externally when bot takes damage) |

Detection = seen OR heard OR damage alert.

### Target Memory

When detected, blackboard is updated: `TargetEId`, `HasTarget`, `CanSeeTarget`, `DistanceToTarget`, `TimeSinceTargetSeen = 0`.

`LastKnownTargetPos` is **exact only when seen**. Heard/damage contacts store a fuzzed
position (error: 20 % of distance for movement noise, 10 % for gunshots, 2.5 m flat for
damage) — no more through-wall GPS pins. Re-acquiring sight after ≥1.2 s unseen resets
the aim-settle ramp (`AimSettle01 = 0`).

When not detected, `TimeSinceTargetSeen` increments each perception tick. After `TargetMemoryDuration` seconds the target is fully cleared (all tracking fields reset, `ReactionTimer` reset).

### Vision Blocking

`VisionBlockingMask` defaults to layer 0 (Default). Linecast uses `ctx.Physics.Linecast`.

---

## 6. Movement System

Runs every frame after the brain tick.

- Reads `DesiredVelocity` written by BT nodes.
- Clamps speed to `ChaseSpeed` maximum.
- **Roll override**: If `IsRolling`, uses `RollDirection * DodgeConstants.Speed` instead.
- Applies NavMesh clamping via `ctx.NavMesh.SamplePosition(candidatePos, 1f)`.
- Facing: turns toward `LastKnownTargetPos` at `FacingTurnRateDeg` (540°/s) **only when
  `IsAlert`** and no search is active (SearchNode drives facing itself); otherwise faces
  the movement direction. Pre-alert bots don't snap-turn to targets they haven't noticed.

---

## 7. Combat System

Runs every frame, processes three intent flags:

### Fire

- Checks `WantsToFire`, weapon fire interval, **reload phase and magazine**.
- Bots consume `AmmoInMagazine` (infinite reserves): empty mag → `WeaponPhase.Reloading`
  for `Stats.ReloadTime`, then refill. `TickReload` also starts a **tactical reload**
  when the mag is < 30 % and the target is out of sight. FireForward test turrets opt
  out of ammo tracking.
- Accuracy: uses `bb.EffectiveAccuracy` published by ShootNode (settle/movement/pressure
  adjusted); falls back to raw `config.Accuracy` when unset.
- Burst bookkeeping: each fired shot decrements `bb.BurstShotsLeft`; when the burst is
  spent, rolls `bb.NextBurstTime` pause (shorter for aggressive personalities).
- Spawns `ProjectilesPerShot` projectiles with:
  - **Weapon spread**: `SpreadAngle * 0.5` random yaw per pellet.
  - **Accuracy spread**: `(1 - accuracy) * 10` degrees random rotation on both axes.
- Projectile spawn position: bot position + 0.5 m forward + 1.2 m up.

### Heal

- `ProcessHeal` starts a **2 s cast** (`bb.HealCastEndTime`) and commits the medkit.
- While casting, HealNode holds the tree in Running: the bot retreats from the threat
  at 60 % speed and cannot fire — the same punish window a healing player gives.
- `TickHealCast` applies **`config.HealAmount`** (not full HP; 50 % of max if the
  config has no amount) when the cast completes.

### Grenade

- Clamps throw distance to `[GrenadeConstants.MinThrowRange, MaxThrowRange]`.
- Throw target is scattered around the LKP: 1.5 m base + 0.4 m per unseen-second,
  capped at 4 m (set in ThrowGrenadeNode).
- Uses `GrenadeSystem.ComputeThrowVelocity` for ballistic arc.
- Spawns `GrenadeEntityState` with standard fuse time, damage, and explosion radius.
- Decrements `GrenadesRemaining`.

---

## 8. Spawn System

`BotSpawnSystem.SpawnBot(state, typeId, position, patrolWaypoints, events)`:

1. Looks up `BotTypeConfig` from `BotConstants`.
2. Creates `BotEntityState` with id, position, patrol waypoints.
3. Creates `WeaponEntityState` from config (fire interval, damage, speed, spread, pellets).
4. Sets `MedkitsRemaining` and `GrenadesRemaining` from config.
4a. Rolls per-spawn **personality**: `ReactionTimeMult` (0.85–1.3), `AccuracyMult`
    (0.9–1.08), `Aggression` (0.7–1.3) — same-type bots are individuals, not clones.
5. Adds to `state.Bots` and `state.HealthMap`.
6. If config has `HelmetDefinitionId` or `BodyArmorDefinitionId`, looks up `ItemDefinition` and creates `ArmorSlotState` in `state.ArmorMap`.
7. Fires `BotSpawned` event.

---

## 9. Bot Types

### Combat Bots

| Type | Prefab       | Weapon          | HP  | Vision (range/angle) | Hearing | Memory | Reaction | Accuracy | Engage | Chase | Behaviors                                      | Armor            | Medkits | Grenades |
|------|-------------|-----------------|-----|----------------------|---------|--------|----------|----------|--------|-------|-------------------------------------------------|------------------|---------|----------|
| Scav | BotView     | Weapon_Rifle    | 80  | 25 / 110             | 6       | 5 s    | 0.8 s    | 0.50     | 20     | 4     | Patrol, Chase, Shoot                            | Helmet_Basic     | 0       | 0        |
| PMC  | BotBossView | Weapon_Rifle    | 100 | 35 / 120             | 6       | 8 s    | 0.4 s    | 0.75     | 28     | 5     | Patrol, Chase, Shoot, Heal, Dodge, ThrowGrenade | Helmet + Body    | 2       | 2        |
| Boss | BotPmcView  | Weapon_Shotgun  | 200 | 40 / 140             | 6       | 12 s   | 0.3 s    | 0.65     | 15     | 5.5   | Chase, Shoot, Dodge                             | Armor_Basic body | 0       | 0        |

**PMC** details: Dodge cooldown 5 s, grenade cooldown 20 s, grenade min throw 5 m, heal threshold 0.5, heal cooldown 15 s.

**Boss** details: Shotgun (7 pellets, 25 deg spread, 7 dmg each), dodge cooldown 3 s, no patrol.

### Shooting Range Targets

| Type             | HP     | Patrol | Dodge (CD) | Armor                    |
|------------------|--------|--------|------------|--------------------------|
| Target           | 10000  | --     | --         | --                       |
| TargetWeak       | 50     | --     | --         | Helmet_Basic             |
| TargetPatrol     | 10000  | 3 m/s  | --         | --                       |
| TargetFast       | 10000  | 6 m/s  | --         | --                       |
| TargetDodge      | 10000  | --     | 2 s        | --                       |
| TargetLightArmor | 10000  | --     | --         | Helmet_Basic             |
| TargetHeavyArmor | 10000  | --     | --         | Helmet_Basic + Armor_Basic |
| TargetGlassCannon| 50     | --     | --         | Helmet_Basic + Armor_Basic |
| TargetTank       | 200    | --     | --         | Armor_Basic (body only)  |

All targets have 0 vision/hearing/accuracy and 999 s reaction time -- they never fight back.

---

## 10. Blackboard

`BotBlackboard` is the per-bot working memory. Reset on spawn.

| Field                   | Type      | Purpose                                                    |
|-------------------------|-----------|------------------------------------------------------------|
| `TargetEId`             | EId       | Entity id of current target (player)                       |
| `LastKnownTargetPos`    | Vector3   | Last position where target was detected                    |
| `HasTarget`             | bool      | Whether bot is tracking any target                         |
| `CanSeeTarget`          | bool      | Whether bot currently has line-of-sight to target          |
| `DistanceToTarget`      | float     | Distance to target (updated each perception tick)          |
| `TimeSinceTargetSeen`   | float     | Seconds since target was last detected; triggers memory clear |
| `PatrolWaypoints`       | Vector3[] | Waypoint loop for patrol behavior                          |
| `PatrolWaypointIndex`   | int       | Current waypoint index                                     |
| `PatrolWaitTimer`       | float     | Countdown at each waypoint (2 s)                           |
| `ReactionTimer`         | float     | Accumulates toward `ReactionTime` before shooting          |
| `DodgeCooldownTimer`    | float     | Countdown for dodge availability                           |
| `HealCooldownTimer`     | float     | Countdown for heal availability                            |
| `PerceptionTimer`       | float     | Countdown to next perception tick (0.2 s interval)         |
| `IsDodging`             | bool      | Legacy dodge state flag                                    |
| `DodgeDirection`        | Vector3   | Legacy dodge direction                                     |
| `DodgeTimer`            | float     | Legacy dodge timer                                         |
| `MedkitsRemaining`      | int       | Medkits available for heal actions                         |
| `GrenadesRemaining`     | int       | Grenades available for throw actions                       |
| `GrenadeCooldownTimer`  | float     | Countdown for grenade availability (BTCooldown)            |
| `GrenadeThrowDelayTimer`| float     | -1 = idle; positive = counting down before throw           |
| `WasDamaged`            | bool      | Set externally on damage, cleared each perception tick     |
| `LastDamageTime`        | float     | Elapsed time of last damage (for heal delay logic)         |
| `RunningNodeId`         | int       | Reserved for BT re-entry (not currently used)              |
| `DebugStatus`           | string    | Human-readable label for current behavior (debug overlay)  |
| `Trace`                 | BTTrace   | Records each node's BTStatus per tick for debug viz        |
| `VisionAwareness01`     | float     | Graduated detection accumulator (1 = seen)                 |
| `IsAlert`               | bool      | Reaction window elapsed — gates combat + target-facing     |
| `LastCanSeeTime`        | float     | Last ElapsedTime with eyes-on; drives aim-settle reset     |
| `ReactionTimeMult` / `AccuracyMult` / `Aggression` | float | Per-spawn personality rolls |
| `BurstShotsLeft` / `NextBurstTime` | int / float | Trigger-discipline burst state           |
| `AimSettle01` / `EffectiveAccuracy` | float | Aim-settle ramp and published final accuracy    |
| `HealCastEndTime`       | float     | -1 idle; heal completes at this ElapsedTime                |
| `ChasePath*`            | —         | NavMesh corner buffer for chase path-following             |
| `SearchEndTime` / `SearchScanBaseDir` | float / Vector3 | Search-at-LKP scan state             |
| `CoverPhase`            | CoverPhase | Fight-from-cover state machine (None/MoveTo/Hold/Peek/Exposed/Return) |
| `CoverPoint` / `CoverPeekPos` | Vector3 | Hidden spot + lateral shooting spot                    |
| `CoverEnemyAnchor`      | Vector3   | LKP snapshot the cover was validated against               |
| `CoverPhaseStartTime` / `CoverPhaseEndTime` | float | Phase entry time + hold/expose deadline |
| `CoverNextSearchTime` / `CoverRevalidateTimer` | float | Failed-search gate + periodic re-check |
| `CoverBlacklistPos` / `CoverBlacklistUntil` | Vector3 / float | Compromised-spot blacklist (shot while hidden) |
| `CoverPath*` / `CoverStuckTimer` / `CoverLastPosition` | — | NavMesh corner buffer + stuck watchdog for the run to cover |

---

## 11. Key Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Systems/Bot/BotBrainSystem.cs` | Main tick: iterates bots, clears intents, ticks BT |
| `Assets/Scripts/Systems/Bot/BotPerceptionSystem.cs` | Vision + hearing + damage alert detection |
| `Assets/Scripts/Systems/Bot/BotMovementSystem.cs` | Velocity application + NavMesh clamping |
| `Assets/Scripts/Systems/Bot/BotCombatSystem.cs` | Fire / heal / grenade execution |
| `Assets/Scripts/Systems/Bot/BotSpawnSystem.cs` | Bot creation with weapon, HP, armor, blackboard |
| `Assets/Scripts/Systems/Bot/BotTreeBuilder.cs` | BT composition per type with caching |
| `Assets/Scripts/Systems/Bot/BT/IBTNode.cs` | Node interface + BTStatus enum |
| `Assets/Scripts/Systems/Bot/BT/BTSelector.cs` | Priority fallback composite |
| `Assets/Scripts/Systems/Bot/BT/BTSequence.cs` | All-must-succeed composite |
| `Assets/Scripts/Systems/Bot/BT/BTCondition.cs` | Predicate gate node |
| `Assets/Scripts/Systems/Bot/BT/BTCooldown.cs` | Rate-limiting decorator |
| `Assets/Scripts/Systems/Bot/BT/BTStatus.cs` | Success / Failure / Running enum |
| `Assets/Scripts/Systems/Bot/BT/BTTraceExtensions.cs` | Debug trace recording extension |
| `Assets/Scripts/Systems/Bot/Nodes/PatrolNode.cs` | Waypoint patrol loop |
| `Assets/Scripts/Systems/Bot/Nodes/ChaseNode.cs` | NavMesh path-follow to last known target position |
| `Assets/Scripts/Systems/Bot/Nodes/SearchNode.cs` | Scan sweep at lost-contact position, then give up |
| `Assets/Scripts/Systems/Bot/Nodes/ShootNode.cs` | Aim and fire at visible target |
| `Assets/Scripts/Systems/Bot/Nodes/TakeCoverNode.cs` | Fight-from-cover: pick → run → hide → peek → duck cycle |
| `Assets/Scripts/Systems/Bot/Nodes/DodgeNode.cs` | Lateral dodge roll |
| `Assets/Scripts/Systems/Bot/Nodes/HealNode.cs` | Emergency / safe heal logic |
| `Assets/Scripts/Systems/Bot/Nodes/ThrowGrenadeNode.cs` | Delayed grenade throw at cover targets |
| `Assets/Scripts/Systems/Bot/Nodes/MeleeAttackNode.cs` | Contact attack — raises `WantsToMeleeAttack` when target in range |
| `Assets/Scripts/Systems/HordeSpawnSystem.cs` | Wave spawner for `horde_range` test scene (zombie crowd) |
| `Assets/Scripts/Constants/BotConstants.cs` | All bot type configs + global tuning |
| `Assets/Scripts/State/BotEntityState.cs` | Bot entity: position, weapon, intents, roll state |
| `Assets/Scripts/State/BotBlackboard.cs` | Per-bot AI working memory |
| `Assets/Scripts/State/BTTrace.cs` | Trace recording for debug visualization |

---

## 12. Horde Spawn System (test scene, 2026-05-10)

Runtime wave spawner active only when `LevelState.LevelId == "horde_range"`. Lets us test crowd-shooting feel without authoring static `BotSpawnPoint`s in the scene.

**Flow:**
1. `RaidSession.Tick` calls `HordeSpawnSystem.Tick(state, ctx, events, coreDefinitions)` when level is `horde_range`.
2. System reads `DevCheats.Config.Horde` (see below) every tick.
3. Grace period (`GracePeriod` seconds, default 5) — no spawns at raid start.
4. After grace: spawn batches of `SpawnBatchSize` zombies on a ring of radius `SpawnRingRadius ± SpawnRingJitter` around the player. Arc-limited via `SpawnArc` degrees (360 = all sides).
5. Cap at `MaxAlive` live zombies of `ZombieTypeId`.
6. HP override: each spawned zombie's `HealthState` is rewritten with `ZombieMaxHp` from the config, replacing the value baked into `BotTypeConfig` — keeps zombie HP runtime-tunable.

**Zombie type config** (`BotConstants.Zombie`):
- Behaviors: `Chase | MeleeAttack` only — no `Shoot`. Carries `PistolWeapon` as a visual placeholder (swap mesh later for a pipe).
- `visionRange = 999`, `visionAngle = 360`, `hearingRange = 999` — always sees player.
- `chaseSpeed = 2.8`, `meleeAttackRadius = 1.6`, `meleeAttackDamage = 12`, `meleeAttackCooldown = 1.0`.

**DevCheats section** `DevCheatsHordeSection` (lives under DevCheats — controls gameplay spawn cadence + zombie HP, not a visual concern):
- `Enabled`, `ZombieTypeId`, `ZombieMaxHp`
- `GracePeriod`, `SpawnInterval`, `SpawnBatchSize`, `MaxAlive`
- `SpawnRingRadius`, `SpawnRingJitter`, `SpawnArc`

**Scene:** `Assets/Scenes/ShootingScenes/ShootingScene_Horde.unity` — clone of KillFeel with `AppBootstrap._defaultLevelId = "horde_range"`. NavMesh baked on the Plane so chase paths resolve.

---

## 13. Ranged-Combat Test Scene (2026-05-12)

`ShootingScene_RangedRange.unity` (level id `"ranged_range"`) is a static-spawn scene for testing ranged-combat against shooting bots with cover usage. Unlike Horde (waves) — this is a deterministic 7-bot layout.

**Bot type:** `BotConstants.RangedTarget` — streamlined PMC clone. Vision 70m / engage 50m / Chase+Shoot only (no Heal, Dodge, Grenade — pure ranged behaviour). Helmet Basic for ricochet feedback.

**Layout** (player spawn at `(0, 0, 0)` facing +Z; spawned in `RaidSession.SpawnRangedRangeTargets()`):
- Zone A — close (z~12-13): 2 RangedTargets in open. Instant aggro on raid start (within vision).
- Zone B — mid (z~30): 2 RangedTargets behind side walls. Central pillar at z=22 splits lanes.
- Zone C — mid-far (z~50): asymmetric — left RangedTarget in L-corner, right behind straight wall.
- Zone D — long range (z~75): 1 RangedTarget behind a 10m central wall, must approach via flanks.

Scene authored statically (13 cover cubes spawned via `execute_code` at scene-build time). NavMesh baked on a 200×200 Plane covering z=-60..140.

Code-spawn deliberately offsets bot positions from cube colliders so spawners never sit inside walls. Adjust positions in `SpawnRangedRangeTargets()` if scene cover layout changes.
