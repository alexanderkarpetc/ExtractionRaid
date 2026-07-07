# Bot Humanization — Research & Roadmap

Goal: make raid bots read as *human players*, not turrets on legs. This doc has three parts:
§1 an audit of the current implementation's "robot tells", §2 research-backed techniques
(games, GDC talks, papers, extraction-shooter mods), §3 a prioritized roadmap mapped to
this codebase. Companion doc: `bot-ai.md` (architecture reference).

---

## 1. Audit — what breaks the human illusion today

Each item lists the tell, where it lives, and why a player reads it as "bot".

### Perception

| # | Tell | Where | Why it reads robotic |
|---|------|-------|----------------------|
| P1 | **Binary, instant detection.** Player at 34.9 m inside the cone is fully detected in one 0.2 s perception tick; at 35.1 m is invisible. No distance/angle scaling, no gradual awareness. | `BotPerceptionSystem.Tick` (`detected = vision OR heard OR alerted`) | Humans take longer to notice distant/peripheral targets; a hard range wall is instantly gamed by players ("stand at 36 m, free kills"). |
| P2 | **Gunshots are silent to bots.** Hearing = `dist <= HearingRange && player.Velocity > 0.1` — walking within 6 m. Firing a rifle 10 m behind a bot does nothing (unless the bullet hits). | `BotPerceptionSystem.Tick` hearing check; no sound-event concept anywhere | The single loudest thing in the game doesn't exist acoustically. Players notice immediately when a firefight next door doesn't attract anyone. |
| P3 | **Hearing = wallhack.** A "heard" detection sets `LastKnownTargetPos = player.Position` exactly, same as sight. Bot then faces/chases the precise spot through walls. | `BotPerceptionSystem.Tick` detected-branch | Humans localize sound roughly ("somewhere left, behind the wall"), not to the centimeter. |
| P4 | **No sneak/sprint distinction.** Velocity > 0.1 is one loudness. Player sprinting and slow-walking are acoustically identical. | same hearing check; `PlayerEntityState.IsSprinting` exists but is unused here | Removes the entire stealth-approach layer extraction players expect. |
| P5 | **Damage alert = exact position through walls.** `WasDamaged` → full detect at `player.Position`. | perception detected-branch + `RaidSession.ProcessDamageAlerts` | Getting shot should give a *direction*, not a GPS pin. |
| P6 | **Facing turns toward target before "reacting".** `BotMovementSystem` rotates toward `LastKnownTargetPos` at 540°/s the moment `HasTarget` flips, even while ShootNode is still burning `ReactionTime`. | `BotMovementSystem.Tick` facing block; `ShootNode` reaction gate | The tell is the *turn*, not the shot: the bot whips to face you instantly, then politely waits 0.5 s to fire. Reaction time must gate the whole response chain. |

### Combat

| # | Tell | Where | Why it reads robotic |
|---|------|-------|----------------------|
| C1 | **Infinite beam of fire.** Bots never consume ammo, never reload — they fire at exact `FireInterval` cadence forever. `WeaponPhase.Reloading` machinery exists but is bypassed. `HealNode.IsReloading` is dead code for bots. | `BotCombatSystem.ProcessFire` (no `AmmoInMagazine` decrement) | Humans fire in bursts, pause, reload, panic-reload. A metronomic infinite stream is the #1 audible tell. |
| C2 | **Flat accuracy.** `(1-Accuracy)*10°` cone is constant: first shot after acquiring = 20th shot; standing = strafing; close = far. No aim settle, no first-shot grace, no movement penalty. | `BotCombatSystem.ProcessFire` | Human accuracy ramps up as aim settles and collapses under movement/pressure. Also removes designer control of "first shots miss on purpose" (Halo/Bioshock trick that sells fairness). |
| C3 | **Perfect grenades.** Thrown exactly at `LastKnownTargetPos`, distance-clamped. | `BotCombatSystem.ProcessThrowGrenade` | Grenade landing on your head from a bot that can't see you feels like an aimbot. |
| C4 | **Instant, full heal.** `ProcessHeal` sets `CurrentHp = MaxHp` (ignores `config.HealAmount`), zero cast time, zero vulnerability, no retreat. | `BotCombatSystem.ProcessHeal` | The player mag-dumps a PMC to 10 %, it blips to 100 %. Feels like cheating; also no counterplay window like the player's own medkit cast time. |
| C5 | **No self-preservation.** No flee/retreat when outgunned, no cover concept, no repositioning after firing. ShootNode strafes on a fixed perpendicular metronome (flip every 1.2–2.6 s). | `BotTreeBuilder` (no flee branch), `ShootNode` strafe | Bots trade HP like they don't value their life — the core "not a player" signal in an extraction game where death = losing gear. |

### Movement / Navigation

| # | Tell | Where | Why it reads robotic |
|---|------|-------|----------------------|
| M1 | **Chase walks a straight line into walls.** `ChaseNode` steers directly at `LastKnownTargetPos`; only Patrol uses NavMesh path corners. NavMesh clamp keeps the bot on-mesh but it grinds along geometry. | `ChaseNode.Tick` vs `PatrolNode.EnsurePath` | Bot slides along a wall face like a Roomba. Patrol already solved this — chase didn't inherit it. |
| M2 | **Reaching last-known-position = statue.** ChaseNode returns Success within 1 m of LKP and zeroes velocity; the bot stands frozen staring at a wall until `TargetMemoryDuration` expires, then teleport-switches to patrol. | `ChaseNode.Tick` `dist < 1f` branch | Humans search: check corners, sweep the room, give up gradually. |
| M3 | **No group awareness.** Every bot is a solo brain vs. the player. No spacing, no staggered pushes, no "buddy died → be careful", bots may clump/stack on the same LKP point. | whole system (single-target, no bot-to-bot signal) | Three bots converging on one door shoulder-to-shoulder reads as a zombie wave, not a squad. |

### Identity / Life

| # | Tell | Where | Why it reads robotic |
|---|------|-------|----------------------|
| I1 | **Clones.** Every Scav has identical reaction 0.8 s / accuracy 0.5 / speeds. Per-encounter jitter exists (`ReactionJitter`, sway seed) but per-*bot* identity doesn't. | `BotConstants` type configs; `BotSpawnSystem` | Players fight 10 identical twins per raid. One dice-roll per spawn (reaction/accuracy/aggression multipliers) is cheap and fully deterministic-testable. |
| I2 | **Bots don't live in the raid.** `RaidState` has `Lootables`, `GroundItems`, `ExtractionPoints` — bots never loot, never migrate POI→POI, never extract. They pace fixed waypoint loops forever. | `PatrolNode` only; no loot/extract nodes | In extraction games (Arena Breakout, Tarkov SAIN+Questing mods) *bots doing player things* is what sells "that was a real player". |

### Already-good foundations (keep, build on)

- Intent-based BT + stateless systems → new nodes slot in cleanly, all testable in EditMode.
- Patrol humanization already shipped: per-leg speed scale, arrival easing, Perlin wander,
  head-scan at waypoints, stuck watchdog (`PatrolNode`, `BotConstants` patrol block).
- Reaction jitter per acquisition, Perlin aim sway, strafe-while-shooting (`ShootNode`).
- Facing turn-rate limit (540°/s) instead of snap (`BotMovementSystem`).
- Stagger fire-lockout gives hit-reaction counterplay (`BotCombatSystem`).
- Weapon state machine already supports magazines/reload phases (`WeaponEntityState`).

---

## 2. Research — how humans (and convincing bots) behave

### 2.1 The Last of Us — human enemy AI (GameAIPro2 ch. 34, Travis McIntosh)

The definitive reference for "enemies real enough you feel bad killing them":

- **Vision cone scales with distance** — wide angle close, narrow far ("angle inversely
  proportional to distance"). Fixes both "didn't see the player next to him" and
  "spotted a speck at 40 m instantly".
- **Awareness accumulator, not a boolean.** Seeing the player starts a timer that
  increments while seen and decrements while unseen; perception triggers at ~**1–2 s**
  for an unaware NPC, much lower once in combat, much higher pre-first-contact.
- **No position cheating.** Perceived player → entity object (position + timestamp),
  broadcast to allies. Unperceived player's entry simply goes stale.
- **Combat cycle**: advance on last-known-position → if unseen for **10 s+**, one NPC
  approaches to check → group transitions to search. Focus tests said 2 min was too
  slow; a pacing cheat (player moved >5 m from believed pos → force the approach) cut
  it to ~30 s.
- **Search map**: occupancy grid; the player's possible location "bleeds" into
  neighboring cells over time, cells visible to any NPC are cleared. Searchers pick
  cells intelligently instead of wandering.
- **Cover posts + post selectors**: candidate cover points scored by multiplying
  normalized criteria (distance curve, path validity, not-behind-player, path must not
  run toward the player). Rejecting cover whose path passes the player killed the
  "NPC rushes at me to hide" complaint.
- **Lethality via coordination**: exactly one *OpportunisticShooter* role guarantees
  someone is always shooting; everyone else takes cover/flanks (Combat Coordinator
  roles: Flanker, Approacher, Investigator, StayUpAndAimer). Flank routes cost-shaped
  around the "combat vector" so flankers swing wide like players expect.
- Dialog exists to **communicate decisions** ("He's flanking!") — intelligence the
  player can't perceive doesn't exist.

### 2.2 SAIN (Tarkov's "make bots human" mod) — what the community proved works

- **Personalities** from gear value + random chance (rat/coward/rusher) drive the
  same config in different directions — the single biggest "that's a player" effect.
- **Hearing** affected by the bot's own state (health, movement), walls, weather;
  suppressed vs unsuppressed shots differ; bots investigate noises and even rush a
  player they *hear healing*.
- **No aimbot snap**: scaled reaction times, simulated recoil per weapon build and
  skill; optics matter (scoped = better far / worse near).
- **Suppression**: bullets snapping nearby debuff stats — fire changes behavior even
  when it doesn't hit.
- **Movement**: dynamic cover from colliders + navmesh queries, corner leaning,
  stutter-sprint, squad flank/suppress with voice callouts.

### 2.3 Extraction-genre "fake player" bots (Arena Breakout Infinite)

AI PMCs get names/dogtags, roam POIs, loot (selectively — gold items), and mostly
*do player things* until killed. The tell players cite most: **shoot one in the back
with a suppressed weapon from range and it instantly snap-turns to your exact
position** — i.e., perception cheating destroys the illusion faster than anything
aiming-related. Bots that loot, roam and extract convince; bots that react
superhumanly expose themselves in one interaction.

### 2.4 Reaction-time numbers

Human simple reaction ~**0.25 s**; game-AI guidance: ~0.25 s base delay when already
aware, ~0.4 s when a friend-or-foe decision is involved, plus variance. Critically,
the delay must gate the *whole* response (turn, move, fire) — a bot that snaps its
facing instantly and then waits politely reads as a robot with a shot timer.

### 2.5 Believability science (2K BotPrize)

The two bots that passed the FPS Turing test in 2012 (UT^2, MirrorBot) beat humans'
humanness ratings (~52%). What judges used to spot bots: relentless optimality.
Humans hold grudges (chasing one enemy suboptimally), take dumb risks, commit to
mistakes before correcting, and are unpredictable in *which* suboptimal thing they do.
Humanizers that worked: imperfect evolved aim, replaying recorded human traces when
stuck, mirroring opponent behavior. Lesson for us: variance and visible imperfection
beat clever play.

---

## 3. Roadmap

### Wave 1 — SHIPPED in this pass (2026-07-07)

All deterministic, EditMode-tested, no new dependencies. Tuning in
`BotConstants` (perception/combat humanization blocks); per-bot rolls on `BotBlackboard`.

| Fix | Tell | Implementation |
|-----|------|----------------|
| Graduated vision | P1 | `VisionAwareness01` accumulator: instant inside 35 % of VisionRange, 0.15→1.1 s detect time to the edge, ×1.6 peripheral band, ×0.25 when already in combat, decay 0.5/s. 360° close-presence sense at 2.5 m. (`BotPerceptionSystem`) |
| Gunshots audible | P2 | Player weapon fired within the last 0.25 s → noise event, heard to 40 m. |
| Noise tiers | P4 | Slow movement ×0.45 hearing range, sprint ×2.2 (reads `PlayerEntityState.IsSprinting`). |
| Fuzzy sound localization | P3, P5 | Heard-only contact stores LKP + error: 20 % of distance (movement), 10 % (gunshot), 2.5 m flat (damage alert). Exact position only from eyes-on. |
| Reaction gates everything | P6 | `IsAlert` computed in `BotBrainSystem`; BT Combat branch + movement target-facing both gated. Personality-scaled threshold + per-acquisition jitter. |
| Burst fire + ammo + reload | C1 | 2–5 shot bursts (aggression-scaled), 0.35–0.9 s pauses; magazine consumed, auto reload on empty + tactical reload when low & target unseen (infinite reserves). (`ShootNode` + `BotCombatSystem`) |
| Aim settle + penalties | C2 | Accuracy ramps ×0.45→×1 over 0.9 s of continuous sight, resets after 1.2 s unseen; ×0.75 while moving fast; ×0.85 within 1.5 s of taking damage. |
| Grenade scatter | C3 | Throw error 1.5 m + 0.4 m per unseen-second, cap 4 m. |
| Heal = cast + partial | C4 | 2 s cast, medkit committed up front, bot retreats and cannot fight during it; heals by `HealAmount`, not to full. |
| Chase pathfinding | M1 | NavMesh corner-following (same pattern as patrol), repath on 0.75 s cadence or 2 m LKP drift. |
| Search then give up | M2 | `SearchNode`: at LKP without sight → ±80° scan sweep for 4.5 s → forget target → patrol. |
| Per-spawn personality | I1 | Reaction ×0.85–1.3, accuracy ×0.9–1.08, aggression ×0.7–1.3 (burst length/pause, strafe energy). |

### Wave 2 — combat depth (next)

1. **Cover system**: annotate cover points (or sample navmesh edges); TLOU-style
   multiplicative scoring (LoS-to-threat blocked, distance band, path not through
   player's line). New `TakeCoverNode` between Shoot and Chase; peek-fire-duck cycle
   using the burst pause as the duck window.
2. **Flee/panic**: HP low + no medkits, or squad-mates died → retreat away from the
   combat vector to a far post; Boss exempt. Personality (aggression) sets the threshold.
3. **Suppression**: projectiles passing near a bot raise a suppression level →
   accuracy/strafe debuff + more conservative branch choices. `ProjectileSystem`
   proximity events feed `bb.WasSuppressed`.
4. **One-shooter coordination token** (TLOU OpportunisticShooter, cheap version):
   a per-raid slot map so 3 Scavs don't all mag-dump simultaneously; others reposition
   or hold. Also anti-clump spacing: bots repel from each other's chase targets.
5. **Sound telegraphing**: `BotReloadStarted` / `BotHealStarted` domain events → SFX
   + VO barks so the player can exploit the windows (intelligence must be perceivable).
6. **Bot-vs-bot sound reaction**: bots hear *other bots'* gunfights and rotate/investigate.

### Wave 3 — bots that live in the raid (fake-player layer)

1. **POI roaming**: replace fixed waypoint loops with a POI graph; bots pick a
   destination, travel, dwell (loot animation at `Lootables`), move on.
2. **Looting**: interact with `LootableContainerState` / corpses / `GroundItems`
   (state exists already); crouch-loot pose, interruptible, drops on death.
3. **Extraction behavior**: late-raid, surviving PMC-types path to
   `ExtractionPointState` and leave — encountering a bot "heading to extract with
   loot" is the strongest player-illusion moment in the genre.
4. **Factions**: Scav-vs-PMC hostility (targeting beyond `state.PlayerEntity`);
   distant bot-vs-bot firefights as ambient world events.
5. **Gear-derived personality** (SAIN model): better-geared spawns → bolder configs;
   loadout variance per spawn.

### Testing / tuning notes

- All new params are consts in `BotConstants`; move to a DevCheats section
  (`BotAIConfig` in `RaidContext`) when live-tuning is needed — follow
  `BotEngagementConfig` precedent.
- EditMode coverage added: graduated vision (instant/far/peripheral), noise tiers,
  gunshot hearing + localization bound, reaction gating, burst/ammo/reload, heal cast,
  search give-up.
- Manual feel-check scene: `ShootingScene_RangedRange` (7 bots, cover layout) — watch
  for burst cadence, reload pauses, search sweeps at lost contact, no more wall-grinding chase.
