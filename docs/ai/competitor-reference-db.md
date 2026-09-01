# Competitor & Reference Database

**Purpose:** one systematic place to pick a reference instead of re-googling competitors every time.
Indexed by **attribute** + **what each game is worth studying for** + **how much extractable knowledge
exists** (so we lean on well-documented games when we need real, citable detail).

**Our game:** Unity top-down **single-player PvE extraction shooter**. Canonical references:
**Escape from Duckov** (mechanics reference per `CLAUDE.md`) + **ZERO Sievert** (direct competitor — same niche).

**How to use:** find your task's attribute in the [Attribute index](#attribute-index), or scan the tier tables.
"Knowledge ★" = how much real info we can extract (wiki / GDC talks / our docs / big community). Prefer ★★★ when you need depth.

Related deep-dives we already have: [`armor-research.md`](armor-research.md) (15+ games on armor/pen/bleed),
[`gunplay/`](gunplay/) contains the current combat-feel reference.

---

## Tier 1 — Direct competitors (top-down PvE extraction)
The closest match to what we're building. **Study these first.**

| Game | Tags | Knowledge | Reference for |
|---|---|---|---|
| **ZERO Sievert** | top-down · extraction · survival · single-player PvE · looter | ★★★ | THE direct competitor. HUD (ammo/survival meters), scavenging loop, procedural maps, weapon mod depth, radiation/survival layer, hideout. |
| **Escape from Duckov** | top-down · extraction · single-player PvE · looter · comedy | ★★ | Our mechanics reference (CLAUDE.md). Transparent armor pen table, accessible extraction loop, inventory/quests/crafting, tone. |

## Tier 2 — Extraction-loop reference (mostly 3D, study the *loop* not the camera)
The genre's pillars — extraction tension, loot economy, risk/reward, raid structure.

| Game | Tags | Knowledge | Reference for |
|---|---|---|---|
| **Escape from Tarkov** | extraction · PvPvE · hardcore-sim · looter | ★★★ | Genre origin. Ammo/armor depth (per-ammo pen+dmg+armor-dmg), bleeding, inventory tetris, the "hidden formula" anti-pattern. |
| **Hunt: Showdown 1896** | extraction · PvPvE · tactical · horror | ★★★ | Best *tactical tension* design. Bounty objective = "win condition", sound design, dark readability, recent "hidden extraction" experiments. |
| **ARC Raiders** | extraction · PvPvE · co-op · 3rd-person | ★★ | "Most accessible extraction shooter at launch" — readable moment-to-moment, onboarding, approachable UX (Embark / The Finals team). |
| **Arena Breakout: Infinite** | extraction · PvPvE · mobile→PC · sim-lite | ★★ | Accessible Tarkov-like gateway. Streamlined armor/UX, onboarding funnel. |
| **Gray Zone Warfare** | extraction · PvPvE · mil-sim · squad | ★★ | Squad strategy/planning, mil-sim systems, methodical pacing. |
| **Marathon** | extraction · PvP-lean · hero-shooter | ★★ | Hero-ability layer on extraction, tuned gunfeel, top-tier production/UI polish (Bungie). |
| **Marauders** | extraction · PvPvE · dieselpunk | ★ | Boarding/space-piracy loop variant; niche but documented. |
| **The Forever Winter** | extraction · co-op PvE · survival-horror | ★ | Atmosphere + "you are prey" power fantasy inversion; survival meter (water) economy. |
| **Delta Force: Hawk Ops** | extraction · PvPvE · large-scale | ★★ | Modern F2P extraction onboarding + mode-blending (extraction + big battle). |
| **Deep Rock Galactic** | co-op PvE · extraction-of-loot · 1st-person | ★★★ | Co-op extraction *feel-good* loop, objective clarity, callouts, "leave no dwarf behind" extraction tension, class identity. |
| **Witchfire** | roguelite · extraction-lite · PvE | ★ | Roguelite + extraction hybrid, gunfeel, relic risk-on-death. |
| **Incursion Red River** | extraction · PvE · mil-sim | ★ | Solo/co-op PvE extraction, contractor framing. |
| **Vigor** | extraction · PvPvE · console-first | ★ | Lightweight extraction onboarding, shelter/economy meta. |

## Tier 3 — Top-down shooters (our perspective — camera, gunfeel, readability)
Study these for **how a top-down shooter reads and feels**, regardless of extraction.

| Game | Tags | Knowledge | Reference for |
|---|---|---|---|
| **Hotline Miami 1/2** | top-down · twitch · ultra-violent | ★★★ | Lethality + readability at top-down scale, hit impact, persistent gore, music-driven flow. (Already a VFX ref in our docs.) |
| **SYNTHETIK (+ Legion Rising)** | top-down · tactical-roguelite · gunplay-sim | ★★ | DEEPEST top-down gunplay: manual reload/eject, jam/overheat, ammo types, recoil — closest mechanical cousin to our weapon depth. |
| **Brigador** | top-down/iso · vehicular · destructible | ★★ | Isometric readability, destructible environments, weighty movement. |
| **Door Kickers 1/2** | top-down · tactical · RTS-lite | ★★ | Top-down tactical planning, room clearing, AI behavior readability. |
| **Running with Rifles** | top-down · tactical · open-world RPG | ★★ | Large-scale top-down combat + RPG progression in one camera. |
| **Alien Swarm: Reactive Drop** | top-down · co-op · tactical | ★★ | Free + open codebase (Source) — co-op top-down shooter reference, class kits. |
| **Nuclear Throne** | top-down · roguelite · twin-stick | ★★ | Frantic top-down game feel, screenshake/juice, tight enemy waves. |
| **Enter the Gungeon** | top-down · twin-stick · roguelite | ★★★ | "Near-perfected twin-stick formula", readable pixel art that isn't noisy, dodge-roll feel, weapon variety. |
| **Helldivers 1** | top-down · co-op · twin-stick | ★★ | Top-down co-op + stratagem UX (became Helldivers 2 in 3D). Friendly-fire tension. |
| **Ruiner** | top-down · cyberpunk · action | ★ | Stylish top-down action, dash combat, UI aesthetic. |
| **The Ascent** | top-down/iso · cyberpunk · ARPG-shooter | ★★ | Dense iso world readability, cover/verticality at top-down, loot-shooter HUD. |
| **Brotato / Vampire Survivors / Halls of Torment** | top-down · survivor-like · auto/twin-stick | ★★ | Readability under swarm, escalating density, minimalist HUD legibility. |
| **Voidigo** | top-down · roguelite · twin-stick | ★ | Modern juicy twin-stick feel, boss design. |
| **Hyper Light Drifter** | top-down · action-adventure | ★★ | Top-down melee/ranged feel, art-driven readability, minimal UI. |
| **Crimsonland** | top-down · arena-survival | ★ | Classic persistent-corpse arena; readability under extreme density. |

## Tier 4 — Game-feel / UX / juice masters (cross-genre, study the craft)
Not our genre, but **best-in-class for the thing in the tag** — pull when polishing feel/UX.

| Game | Tags | Knowledge | Reference for |
|---|---|---|---|
| **Hades** | action-roguelite · UX · juice | ★★★ | Gold standard for feedback/juice, readable chaos, onboarding, damage numbers, hit-stop. |
| **Helldivers 2** | co-op · 3rd-person · UX | ★★★ | Armor-feedback readability, stratagem UX, "best armor feedback reference" (our armor-research). |
| **Dead Cells** | action-roguelite · game-feel | ★★ | Hit-stop, weapon identity, responsive controls. |
| **Risk of Rain 2** | co-op · scaling · UX | ★★ | Item-stacking readability, escalating power fantasy, HUD under clutter. |

## Tier 5 — Survival / looter / systemic adjacents
Reference for survival meters, systemic worlds, loot economy — not combat camera.

| Game | Tags | Knowledge | Reference for |
|---|---|---|---|
| **STALKER 2 / STALCRAFT: X** | survival · open-world · shooter | ★★ | Radiation/anomaly survival layer, RPG armor stats, zone atmosphere. |
| **The Division 1/2** | looter-shooter · 3rd-person · RPG | ★★★ | Armor/HP/gear-score loot UX, world HUD, cover shooter readability. |
| **Pacific Drive** | survival · run-based · single-player | ★★ | Run-and-extract structure without combat focus; resource/condition UX. |
| **Chernobylite** | survival · base-building · PvE | ★ | Survival + base/hideout loop, mission planning. |

---

## Attribute index (reverse lookup)

- **top-down perspective** → ZERO Sievert, Duckov, Hotline Miami, SYNTHETIK, Brigador, Door Kickers, RWR, Alien Swarm, Nuclear Throne, Enter the Gungeon, Helldivers 1, Ruiner, The Ascent, Brotato/VS/Halls, Voidigo, Hyper Light Drifter, Crimsonland
- **extraction loop** → ZERO Sievert, Duckov, Tarkov, Hunt, ARC Raiders, Arena Breakout, Gray Zone, Marathon, Marauders, Forever Winter, Delta Force, DRG, Witchfire, Incursion, Vigor
- **single-player / PvE** → ZERO Sievert, Duckov, DRG (co-op), Forever Winter, Witchfire, Incursion, Pacific Drive, Chernobylite
- **deep gunplay/weapon sim** → Tarkov, SYNTHETIK, ZERO Sievert, Gray Zone
- **armor / penetration / bleed** → see [`armor-research.md`](armor-research.md): Tarkov, Arena Breakout, GZW, Duckov, DaD, Hunt, Helldivers 2, The Division
- **HUD / UI readability** → ZERO Sievert, The Division, Helldivers 2, Hades, The Ascent, Marathon
- **game feel / juice / hit-stop** → Hades, Hotline Miami, Nuclear Throne, Dead Cells, Enter the Gungeon
- **survival meters (hunger/rad/thirst)** → ZERO Sievert, STALKER/STALCRAFT, Forever Winter, Chernobylite
- **co-op** → DRG, Helldivers 1/2, Alien Swarm, ARC Raiders, Risk of Rain 2
- **readability under swarm/density** → Brotato, Vampire Survivors, Halls of Torment, Crimsonland, Risk of Rain 2

## Knowledge availability — quick picks (★★★ = lean here for real detail)
Tarkov · Hunt · ZERO Sievert · DRG · Hades · Helldivers 2 · The Division · Hotline Miami · Enter the Gungeon

---

## Maintenance
- Add a game → put it in the right tier table + every matching line of the [Attribute index](#attribute-index).
- When a topic gets a deep-dive doc (like `armor-research.md`), link it from the relevant attribute line instead of duplicating.
- Knowledge ★ is "how much we can extract", not how good the game is — bump it as wikis/GDC talks/our own notes accumulate.

_Sources (initial sweep, 2026-06): Glitchwave/Slant twin-stick charts, Switchblade/Eneba/DualShockers extraction rankings, Wikipedia "Category: Extraction shooters", Steam tags (Top-Down/Extraction), GamerRant/TheGamer top-down lists, PC Gamer (Hunt), Kotaku (ZERO Sievert)._
