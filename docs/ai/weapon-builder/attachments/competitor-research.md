# Weapon Attachments — Competitor Research (full findings)

> Companion to [`analysis.md`](./analysis.md). Raw per-game findings + sources from the iteration-1 research sweep (2026-06-07). Confidence flagged per claim. Treat numbers as "current-ish" — balance is patch-volatile.

---

## Tier 1 — Direct competitors (top-down PvE extraction)

### ZERO Sievert ★ (THE direct competitor)

**Slots (high confidence):** scope, muzzle, barrel, handguard (rail — gates foregrips/lasers), stock, grip, magazine, + aux slots 1–4 (lasers/torches/foregrips). **Per-weapon** — pistols lack stocks etc. Compatibility per-attachment: some optics fit all weapons (Alton, Milosun Red Dot, Vudu), others only a few (TSU, EC 74, CU).

**Three core stats (high):** counter-intuitive directions —
- **Ergonomics (E)** ↑ = better (ADS speed, lower stamina drain).
- **Recoil (R)** ↓ = better (full-auto grouping).
- **Accuracy (A)** = a **spread** value → lower = tighter = better (bar fills the confusing way).
Design intent explicit: an optic may reduce Recoil+Ergo but **increase Accuracy (add spread)** — a sidegrade.

**Examples:** optics short-range (EOTech, Aimpoint, Milosun/NTS Red Dot) vs high-mag (PSO-1, Vudu 1-6x, Lion Scope — rare). Muzzle: per-caliber suppressors (−bullet-noise/aggro + better hip-fire; Stealth-spec preserves dmg bonus) vs brakes (raw recoil). Foregrip example "R6, E8" (−6 recoil, +8 ergo). Stocks trade ergo↔recoil. Lasers reduce hip-fire spread (place in slot 4 to avoid clashing with scope); torches reduce ergo (don't stack).

**Anti-creep (high — stated philosophy):** stat tradeoffs (primary) + loot rarity/scarcity (Lion Scope, TR-15 grip hard to find) + weapon-specific compatibility. Caveat: within a compat class "some attachments are clearly better" — not a perfect sidegrade web; local best-in-slot gated by rarity.

**Install UX (high):** **Hideout workbench ONLY → "Mod Weapon" tab.** Weapon must be in inventory/stash, **NOT equipped**. **Cannot mod in-raid** — between-raid base activity. Strong contrast with Duckov.

**Loot:** attachments looted in raids + trader buys; rarity informal ("very rare to find"), no explicit color ladder.

**Top-down standout (high — most important finding):** magnified scopes **severely shrink field of vision** when aiming — _"zooming only works well if you do NOT have a scope, because they severely limit your field of vision."_ Ties into ZS's fog-of-war vision cone (~90°, 110° with "Doe-eyed" perk). **Optic = view-shape tradeoff (range↑ vs cone-width↓), not cosmetic zoom.**

Sources: [Fandom Attachments](https://zero-sievert.fandom.com/wiki/Attachments), [Performance modifiers](https://zero-sievert.fandom.com/wiki/Performance_modifiers), [SteamAH best-attachments](https://steamah.com/zero-sievert-best-attachments-for-each-weapon/), [TechRaptor guide](https://techraptor.net/gaming/guides/zero-sievert-weapons-and-attachments), [Steam "how to equip"](https://steamcommunity.com/app/1782120/discussions/0/3321988498658693281/), [gamerblurb scope/FOV](https://gamerblurb.com/articles/how-to-attach-a-scope-in-zero-sievert).

---

### Escape from Duckov ★ (our mechanics reference)

**Slots (high):** scope/sight, muzzle, grip, stock, tactical (lasers/lights), magazine + **barrel (LOCKED by default → unlock attachment slots at workbench = progression gate)**. Class-tagged compatibility (e.g. "BR" mods only fit battle rifles). Slots differ per weapon.

**Exact stat-sheets (high — game exposes numbers):**
- **Red Dot (1x):** +0.16 aim range, **+0.1 crit-dmg mult**, −ADS time, custom crosshair. 0.1 kg, ₽500. ≈ low-end near-pure upgrade (cost = opportunity vs bigger scope).
- **2x Scope:** **+72% aim distance** BUT **+20% vertical, +8% horizontal recoil, +0.1s aim time**. 0.3 kg, ₽1100, **Quality tier 3**. Textbook sidegrade.
- **4x/8x:** more visible range; higher zoom raises hip-fire spread; historically aim-time penalties (softened in patches). Range gain modest (~10% even at 8x).
- **Muzzle:** brake −23%/−23% V/H recoil; suppressor **−75% sound** BUT **−10% range, −10% bullet speed**; damage muzzle **+10% dmg** BUT **+10%/+10% recoil**.
- **Grip:** balanced −23%/−23%; horizontal-focused **−43% horizontal**; hip-fire grips.
- **Stock:** recoil stock −23%/−23% (stacks with grip for stability builds).
- **Tactical:** basic laser −13% hip-fire spread; rapid-response laser −20% ADS time/+15% range/−13% ADS spread BUT **+20% hip-fire spread**; flashlights = utility.
- **Magazine:** extended **+100% capacity** BUT **+10% ADS time**; quick mag = faster reload/+move/+spread-recovery.

**Anti-creep (high):** explicit numeric tradeoffs + **weight** on every attachment + **Quality/rarity tiers** (price scales) + **slot-unlock gate** + class-compat tags. Honest caveat: recent patches _reduced_ some penalties → trending slightly more upgrade-y.

**Install UX (high — opposite of ZS):** **drag-drop in inventory, ANYWHERE incl. in-raid**; compatible slots highlight (bold white frame); no level requirement. Workbench used for slot-**unlock**/craft/repair/deconstruct, NOT the install action. **No visual mod on weapon model** (we can match this).

**Loot:** looted + bought (bunker shop + rotating merchants) + quest-unlocked; deconstruct recycles into parts.

**Top-down (high — cleanest model):** scopes **do NOT zoom camera** — they **extend visible aim-range when ADS (RMB)**, **axis-dependent**: 4x → ~38m on Y (up/down) but ~25m on X; 1x/none → ~13–14m X. Popular first-person Workshop mod exists → top-down + long-range optics is a real UX tension.

Sources: [BoostRoom modding guide](https://boostroom.com/blog/weapon-modding-guide-attachments-that-actually-matter-in-escape-from-duckov), [escapefromduckov.net mods](https://escapefromduckov.net/guide/the-best-mods-for-escape-from-duckov), [Steam customization](https://steamcommunity.com/app/3167020/discussions/0/592900729837000649/), [2x Scope item](https://www.escapefromduckov.io/archive/items/574), [Red Dot item](https://www.duckescapefromduckov.com/en/items/attachments/scope_all_reddot_1), [first-person mod writeup](https://allthings.how/escape-from-duckov-first-person-mod-features-limits-install/).

---

## Tier 2 — Depth gold standard (extraction, 3D)

### Escape from Tarkov (deepest modding in genre)

**Recursive slot TREE (high — URL-confirmed via TarkovArmory build strings):** mods form a tree; **structural** mods carry child slots, **leaf** mods don't. Real path: `mod_handguard → mod_mount_000 → mod_foregrip`, `mod_reciever → mod_mount → mod_scope → mod_scope` (red-dot piggyback on scope). An optic can sit 3 levels deep (sidemount → rail → scope). Three top-level categories: Functional, Gear (sights/tactical), Vital parts (gun can't fire without).
- **Structural slots:** receiver/dust-cover, gas block, handguard/RIS, rail sections (RIS/KeyMod/M-LOK/dovetail), scopes (open piggyback/magnifier), stock adapters, muzzle adapters.
- **Leaf slots:** muzzle device, magazine, pistol grip, charging handle, foregrip, tactical, optic glass.
- **Container behaviour:** a rail keeps parent's inventory footprint even with 4 attachments → space-saving; detaching parent detaches subtree.

**Stat axes (high):** Ergonomics (ADS speed + ADS volume + aim stamina drain), Recoil V/H (separate), Accuracy MOA (lower=tighter; displayed as _radius_ so real spread ~2×), Weight (stamina/ADS/arm-sway/sprint), Sighting range (zero adjust, ≠ effective range), Muzzle velocity, Durability burn/heat.

**Give/take examples:** DTK-1 brake −26% V/−10% H; "Muzzle Brake 762" −40% V; Zenit RSh-1 foregrip −25%/−15%. **Suppressors = canonical tradeoff:** mask noise + recoil but **ergo penalty (never better than −17, common −20)** + weight + **severe heat → jam after 2–3 mags + max-durability loss**. AP/match ammo: +pen BUT +recoil (M61 +50–75%).
→ brakes/grips/stocks ≈ near-pure recoil upgrades (cost = ergo/weight/slot); **suppressors + heavy/long barrels = genuine tradeoffs**. **Tradeoff lives at whole-build level.**

**The ergo↔recoil↔weight triangle (high — core anti-"strictly-better"):** recoil-reduction parts add weight + cut ergo; weight punishes via stamina/sprint/arm-sway; ergo drives ADS speed + ADS noise + aim stamina. Community thresholds: >70 ergo ≈ sub-300ms ADS (SMG), 50–70 ≈ ~350ms (AR), <50 ≈ 400ms+ (heavy). Derived "Evo Ergo" metric (ergo+weight) predicts true ADS — raw ergo alone misleads.

**Compatibility (high):** slot-type + caliber + **mounting standards modeled** (Picatinny/KeyMod/M-LOK/AK-dovetail). Optic on AK needs sidemount/dust-cover rail. **Mount method affects stats** (suppressor _through_ a brake stacks the brake's recoil; direct-thread doesn't).

**Install UX:** out-of-raid stash/loadout (per-slot dropdowns, live stat readout); in-raid field-mod possible (mags, carried parts); **presets** (patch 13.5: save build, one-click, auto-buy missing). Third-party tools near-mandatory: Totov Builder, TarkovBOT (3D), TarkovArmory.

**"Hidden formula" (the anti-pattern, high):** historically opaque — recoil-affecting attrs hidden → spreadsheet meta. **Dec 2023 recoil rework (0.14) exposed most hidden stats** (well-received). **Still hidden:** dispersion (random scatter), MOA-as-radius mislead, additive-vs-multiplicative compounding undocumented. Verdict: real historical flaw, much improved, not fully transparent.

**Loot/tiers:** traders (loyalty LL1–4 + quest gates = main ladder), found-in-raid, flea market (PMC ~15+).

Sources: [Weapon mods wiki](https://escapefromtarkov.fandom.com/wiki/Weapon_mods), [Performance modifiers](https://escapefromtarkov.fandom.com/wiki/Performance_modifiers), [NamuWiki attachments](https://en.namu.wiki/w/Escape%20from%20Tarkov/%EB%AC%B4%EA%B8%B0/%EB%B6%80%EC%B0%A9%EB%AC%BC), [TarkovArmory](https://tarkovarmory.com/weapons), [terrabattle2 recoil guide](https://terrabattle2.com/ultimate-guide-to-recoil-control-and-weapon-handling-in-eft/), [StealthCore gun stats](https://stealth-core.com/blog/eft/gun-stats-in-tarkov/), [Totov](https://www.totovbuilder.com/), [TarkovBOT](https://tarkovbot.eu/weapon-modding).

---

### Arena Breakout: Infinite (streamlined Tarkov)

**Scale:** ~900 mods / 20+ slots per weapon / ~72–75 guns. Slots: muzzle, optics (red-dot/holo/ACOG/6×/20×/hybrid/thermal/NV), magazines (std/extended/drum), stocks, grips/foregrips, handguards, barrels, gas blocks, tactical (IR/laser/bipod), rails/mounts. **Mount-first prerequisite chain** ("ultra-realism logic"; high-mag scopes gated to long-range platforms). Flatter than Tarkov — low confidence it replicates the deepest rail-container recursion.

**Tradeoff matrix (high direction, low magnitude — English numbers thin):**

| Attachment | Vert recoil | Horiz recoil | ADS speed | Ergo | Weight |
|---|---|---|---|---|---|
| Suppressor | ↓ | ↓ | worse | better | heavier |
| Compensator | ↓↓ | ↓ | worse | worse | heavier |
| Short barrel | worse | ↓ | much faster | better | lighter |
| Angled grip | ↓ | ↓ | faster | better | — |

Modeled: V/H recoil, ergo, ADS speed, accuracy, weight, sound, heat/barrel-degradation; visual recoil vs actual recoil distinct. Subsonic + suppressor: −60% detection range.

**Install UX (biggest differentiator):** Gunsmith → Weapon Assembly OR right-click inventory → Modify. **Auto-equips owned parts; auto-buys cheapest market option for gaps** → "fill the build, game sources the parts." Sort by effectiveness/rarity. **Live readout + 3D preview.** One-tap loadouts.

**Transparency:** exposes V/H recoil + ergo live + 3D preview; described as more transparent than Tarkov. Magnitudes unpublished.

**Loot:** looted in-raid (extract to keep) + player marketplace (price by rarity) + NPC vendors. Rarity/quality tier = soft progression gate.

Sources: [Charlie INTEL modding](https://www.charlieintel.com/games/how-to-modify-weapons-in-arena-breakout-infinite-323743/), [arenabreakout-infinite.com customization](https://arenabreakout-infinite.com/weapon-customization/), [gamingonphone gunsmith](https://gamingonphone.com/guides/arena-breakout-the-complete-gunsmith-guide-gun-customization-attachments/), [Differ review](https://differ.blog/p/arena-breakout-infinite-review-2025-is-this-free-tarkov-alternative-f3e2cf).

---

## Tier 3 — Top-down shooters (readability focus)

### SYNTHETIK (+ Legion Rising) ★ (closest top-down mechanical cousin)

**Four modification layers:**
1. **Weapon Upgrade Kits → Attachments (per-weapon):** kit offers choice of ~3 random-rolled attachments + flat +5% dmg. **Max 4 attachment slots.** Rare attachments render **gold**, low chance of bonus 4th option.
2. **Stat upgrades (per-weapon):** once 4 slots full, kits convert to raw boosts; ~12 upgrades to max, then +1% additive dmg.
3. **Weapon Upgrade Shrines (world-placed mid-run).**
4. **Item Modules / passives (global, affect all weapons).**

**Two-sided attachments (the key lesson):** Muzzle Compensator +15% dmg BUT +3° deviation +10% recoil; Caliber Reduction big accuracy/recoil BUT −10% dmg; Eternium Amalgam +25% dmg/+20% firerate/−10% recoil BUT **crawl movement while held**; Hyper Accelerator full-pen + velocity BUT −5% dmg; Tac Grip −20% recoil **while stationary**; Custom Fitting class-specific (pistols half recoil / ARs +50% headshot).

**Global modules:** 50 Kills Upgrade (usage-based perm boost), Inverted Recoil (accuracy ↑ longer you hold fire), Twin Link II (×2 projectiles 1s BUT +5 recoil all weapons), Powershot (every 6th shot bonus −5 heat).

**Anti-one-true-build:** two-sided attachments self-balance; class/weapon-family bias which mods are useful; +5%-per-kit = opportunity cost of _which_ weapon to invest.

**Top-down readability (critical):** modification kept OFF the battlefield; combat HUD shows only **state**: heat meter (colored bar; >100% = self-damage), **mag count only near end-of-mag**, active-reload bar with colored window, jam cleared via reload UI. Full breakdown on separate stats panel (press X) — read the **numeric** value, bars imprecise. **All depth via HUD bars/numbers + audio, NOT weapon model.**

**Progression:** hybrid — run-scoped (attachments/kits/shrines) + persistent (research unlocks, class levels).

Sources: [Attachments](https://synthetikuniverse.wiki.gg/wiki/SYNTHETIK_1:Attachments), [The Basics](https://synthetikuniverse.wiki.gg/wiki/SYNTHETIK_1:The_Basics), [Items](https://synthetikuniverse.wiki.gg/wiki/SYNTHETIK_1:Items), [Newbie guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2325695775), [modules thread](https://steamcommunity.com/app/528230/discussions/2/1726450077636834466/).

### Enter the Gungeon (synergies, not slots)

Guns modified by **synergies** (gun+gun or gun+passive combos), affecting only the synergy gun. Examples: Arctic Warfare (+62.5% dmg, freeze, recolor orange/white), Breakfast Club (reload halved, +60% firerate, +50% dmg), Hammer and Nail (3× dmg). Some downsides: Akey Breaky (infinite ammo BUT can't open locks), Cormorant (no reload BUT +3 curse). Mostly upside, RNG-gated by hidden "Synergy Factor."
**Top-down readability:** distinct projectile sprite/color/sound per gun; **recolor-on-transform** makes modified state legible; floating blue arrow = only persistent on-screen indicator; detail in paused Ammonomicon. Run-scoped.

Sources: [Synergies wiki](https://enterthegungeon.wiki.gg/wiki/Synergies).

### Nuclear Throne (mutations) + Risk of Rain 2 (proc-stacking, cross-ref)

**NT mutations:** pick 1 of 4 on level-up; run-scoped. **Conditional value** is the balancer: Eagle Eyes (tightens spread — great single-shot, **ruins spread weapons**); Laser Brain (build-defining only with lasers). Readability: weapon = projectile sprite + muzzle flash + sound + corner icon; mutations = static portrait icon strip, never battlefield clutter. (NT wiki 403 → numbers approximate.)

**RoR2 (cross-ref for global on-hit modules):** items = global passives, **stack linearly, gated by proc coefficient** (Tri-Tip Dagger 10%/stack bleed × proc-coeff; ATG Missile scales off triggering hit's dmg) → same item reads differently per weapon **without per-weapon authoring**. Readability via projectile VFX (missiles/daggers), single corner icon + stack count. Borrow the proc-coeff _gating_, not unbounded stacks.

Sources: [NT Mutations](https://nuclear-throne.fandom.com/wiki/Mutations), [RoR2 Tri-Tip](https://riskofrain2.wiki.gg/wiki/Tri-Tip_Dagger), [stacking formulas](https://deltiasgaming.com/risk-of-rain-2-ror2-formulas-guide-how-item-stacking-actually-works/).

---

## Tier 4/5 — Looter-shooters (stat-tradeoff design)

### Destiny 2 ★ (canonical opposing-axis model)

**Model:** fixed **perk columns** (Barrel/Sight → Magazine → Trait1 → Trait2); each column = 1 option; contents random-rolled per drop. Columns "adjust stat distribution outside base stats."

**Barrel give/take:** Full Bore (+Range / −Stability −Handling), Extended Barrel (+Range +RecoilDir / −Handling), Smallbore (+Range +Stability), Corkscrew (+all small / —), Chambered Comp (+Stability +RecoilDir / −Handling); shotgun Smoothbore (+Range / +spread), Full Choke (tighter ADS spread / −precision dmg).
**Magazine give/take:** Steady Rounds (+Stability / −Range), Extended Mag (+Mag / −Reload), Alloy Casing (+Reload / −Stability), Accurized (+Range), Tactical (+all slight), High-Explosive (+BlastRadius +ProjSpeed / −Mag), Implosion (+Stability +ProjSpeed / −BlastRadius), Phase Mag (+Dmg / −RoF −Mag).
**Defining axis pairs:** Range⇄Stability, Magazine⇄Reload, BlastRadius⇄Stability/ProjSpeed, Damage/Impact⇄RateOfFire.

**Anti-creep MECHANISM (3 independent layers, high):**
1. **Opposing-axis tradeoffs** (above).
2. **Hidden stat budget + cap (100) + interpolation curve** — `DestinyStatGroupDefinition` caps each stat at 100; "true hidden" value interpolated to displayed → a raw +10 may show +9, and near-cap returns diminish hard.
3. **Archetype/intrinsic frame envelope** — RPM frame limits achievable stats; "prevents any single weapon being objectively superior"; inverse fire-rate↔impact baked in.

**Near-pure upgrades tolerated** (Corkscrew, Tactical Mag) — allowed precisely because cap+interpolation keeps magnitude trivial.

**UX:** weapon-inspect column picks; community tools (BSMT, light.gg) preview stat deltas; in-game stat-bar shift on hover.

Sources: [Barrels](https://d2.destinygamewiki.com/wiki/Barrels), [Magazines](https://d2.destinygamewiki.com/wiki/Magazines), [Stats](https://d2.destinygamewiki.com/wiki/Stats), [High Ground Gaming stats explained](https://www.highgroundgaming.com/destiny-2-weapon-stats-explained/), [Shattered Vault perks](https://shatteredvault.com/kb/weapon-optimization/weapon-perks/).

### The Division 2 ★ (slot UX leader + cautionary tale)

**Model:** 4 slots (Optic, Muzzle, Magazine, Underbarrel/Grip). **Set-stats, NOT random rolls** (D2 deliberately removed D1's random rolls). Crafted from blueprints, reusable, freely swappable across weapons. ~72 mods (12 optic / 34 mag / 6 underbarrel / 20 muzzle).

**Give/take:** Digital Scope +45% headshot / −20% stability; Laser Pointer +crit / −stability; Extended 7.62 Mag +50 ammo / −10% reload; Osprey Suppressor +20% crit / −10% RoF; **pure-positive ones exist** (Short Grip +crit-dmg, Flexible Tubular Spring +20% reload, T2 Red Dot +accuracy+crit — no downside). Axis pairs: Headshot/Crit⇄Stability, Ammo⇄Reload, Crit⇄RoF.

**Anti-creep (instructive — WEAKER than Destiny):** some tradeoffs but several pure-positive; **primary containment = acquisition friction (blueprint grind) + balance lives in gear-score/brand-sets/recalibration, NOT the mod slots.**

> **Cautionary tale (high):** Division **1** built mods as random-roll give/take → "clogged inventory" + decision fatigue. Division **2** swapped to **fixed, mostly-positive, freely-swappable** mods to cut fatigue. A studio **abandoning** per-mod tradeoffs because UX cost > depth benefit.

**UX (D2's #1 strength):** apply anytime from inventory; **live green↑/red↓ stat-delta preview**; set-stats make preview deterministic (no roll anxiety).

Sources: [GamesRadar mods](https://www.gamesradar.com/division-2-mods-skill-weapon-gear-unlocking-power/), [Division2Tracker](https://division2tracker.com/the-division-2-weapon-mods/), [GameRant best mods](https://gamerant.com/the-division-2-best-weapon-mods/), [BuffNerfRepeat crafting](https://buffnerfrepeat.com/guides/how-to-unlock-and-craft-weapon-mods-in-the-division-2).

### Borderlands 3 (modular generation, no install — contrast case)

Guns assembled from manufacturer-specific RNG parts (barrel/grip/stock/mag/sight/element/accessory; parts don't mix manufacturers). **No install step** — "no mechanic to alter weapon parts." Anti-creep via RNG dispersion + no recombination + **manufacturer identity = functional sidegrade** (Tediore reload-as-grenade, Maliwan dual-element, Atlas homing) + element situational vs enemy type + level-treadmill obsolescence. Anointments = build-specific, rerollable. (Per-part numeric deltas low confidence — datamined only.)

Sources: [BLCM parts wiki](https://github.com/BLCM/BLCMods/wiki/Borderlands-3-Item-and-Weapon-Parts), [Borderlands Wiki weapons](https://borderlands.fandom.com/wiki/Borderlands_3_Weapons), [MentalMars arsenal](https://mentalmars.com/guides/borderlands-3-weapon-arsenal-explained/).

---

## Distilled anti-power-creep patterns (ranked)

1. **Opposing-axis tradeoff** (Destiny) — bind each gain to a paired loss on a stat the player cares about. Most robust, transparent.
2. **Hidden stat budget + cap + diminishing interpolation** (Destiny) — powerful but **conflicts with our "no hidden budget" rule** + "hidden formula" anti-pattern. Avoid or make transparent.
3. **Archetype/frame envelope** — class intrinsic pre-spends budget; mods tune within a fixed silhouette.
4. **Opportunity cost via limited exclusive slots** (Destiny columns, Division 4, SYNTHETIK 4) — even pure-positive mods compete.
5. **Situational value > universal value** (NT, BL3 elements, range optics) — cheapest to author, no stat math.
6. **Diminishing returns even without explicit penalties** — curve gains so one-statting decays.
7. **Acquisition friction / RNG dispersion** (Division, BL3) — manages curve, not per-mod depth.
8. **Move balance off the mod entirely** (Division 2 → gear-score) — low decision-fatigue, power lives elsewhere.

**Key cross-game takeaway:** the desired "tradeoff-slider" feel = **#1 + #4 + #5 together**, paired with a **strong live stat-bar preview** (Division 2's best feature) so give/take is legible at install time. A few small no-downside generalist mods are fine **if** a transparent cap keeps magnitude trivial.
