# Armor Systems Research — Competitor Analysis

## Overview
Research of armor/protection systems across 15+ shooters for ExtractionRaid armor design.
Focus: helmet + vest, penetration system, durability, blunt damage, visual clarity for player.

---

## Classification of Armor Models

### Model 1: Flat Reduction (Simple)
**Games: PUBG, Vigor, DMZ**
- Armor = flat % damage reduction or bonus HP
- No penetration, no ammo interaction
- PUBG: 3 tiers (30%/40%/55% reduction), durability breaks → armor gone
- DMZ: plates = +50 HP each (max 3), no pen system
- Vigor: single plate = 10% reduction, head always exposed

| Pro | Con |
|-----|-----|
| Instant readability | Ammo types meaningless |
| No learning curve | No depth/meta |
| Quick loot decisions | No crafting loop value |

### Model 2: Per-Weapon Armor Penetration (CS2)
- Each weapon has fixed AP% (Glock 47%, AK 77.5%, AWP 97.5%)
- Binary armor: have it or don't ($650 vest, +$350 helmet)
- Helmet = threshold mechanic (M4 headshot: kill without helmet, survive with)

| Pro | Con |
|-----|-----|
| Creates weapon meta | No armor tiers |
| Economic decisions (buy rounds) | Not suited for extraction loop |
| Helmet as clear threshold | No ammo variety |

### Model 3: Ammo Pen vs Armor Class (Tarkov-family)
**Games: Escape from Tarkov, Arena Breakout, Gray Zone Warfare**

#### Escape from Tarkov
- 6 armor classes (expanded to 1-10 with plate update)
- Each ammo: Penetration value (0-79), Armor Damage %, Base Damage
- Pen chance formula: `PenChance ≈ PenValue - (ArmorClass × 15) + 15 + (Durability% × 5)` (capped 0-95%)
- Durability degrades → effective class drops → easier to pen
- **Blunt damage**: `BluntDMG = BaseDMG × BluntThroughput(0.2-0.4) × f(pen, class, durability)`
- 7+ armor materials: Aramid (best repair 95%), Ceramic (worst 40%), UHMWPE, Steel, Titan
- Repair reduces max durability permanently
- Helmet: ricochet chance (Low/Med/High), face shields, ear covers
- Body zones: Head(35HP), Thorax(85HP), Stomach(70HP), Arms(60HP), Legs(65HP)
- Plate system (0.14+): separate front/back/side plates in carriers

| Pro | Con |
|-----|-----|
| Deepest meta (ammo choice = strategy) | Very complex, hidden formulas |
| Long armor lifecycle (repair economy) | New player confusion ("why is he alive?") |
| Material trade-offs | Requires wiki/external resources |
| Headshot zones + ricochet | Hard to communicate in-game |

#### Arena Breakout
- 6 classes, similar to Tarkov
- **4 penetration outcomes** (unique):
  1. Ricochet (steel only) → 0 damage, heavy durability loss
  2. Block → minimal blunt damage
  3. Half penetration → reduced damage
  4. Full penetration → full damage
- Pierce Level (tier 1-6) + Penetration Stat (numeric within tier)
- Lower durability → higher pen chance (gradual, not binary)
- Materials: Aramid, UHMWPE, Titanium, Steel, Ceramic, Aluminum, Composite

#### Gray Zone Warfare
- NIJ standard: IIA < IIA+ < II < IIIA < IIIA+ < III < III+ < III++
- Plate coverage: Front/Back/Sides explicitly
- Helmets capped at IIIA
- Ammo types: Soft Point / FMJ / AP
- Durability: 2-10% per hit, full protection until 0%

### Model 4: Protection vs Penetration Differential (Duckov)
**Game: Escape from Duckov**
- Armor: T0-T5 (Protection values 0-5)
- Ammo: Penetration values per type within caliber
- Simple lookup table:

| Protection - Pen | Damage Multiplier |
|-----------------|-------------------|
| Pen ≥ Prot (+0) | 1.0x (full damage) |
| Prot > Pen by 1 | 0.67x |
| Prot > Pen by 2 | 0.50x |
| Prot > Pen by 3 | 0.40x |
| Prot > Pen by 4 | 0.33x |

- Durability: current/max, repair reduces max permanently
- Below 50% durability → armor effectiveness degrades
- Separate helmet slot, same math

| Pro | Con |
|-----|-----|
| Transparent formula (player can learn table) | Less granular than Tarkov |
| Ammo choice matters | Fewer material trade-offs |
| Durability lifecycle for economy | Simpler = less long-term depth? |
| Easy to show in UI | |

### Model 5: Binary Penetration Gates (Delta Force)
**Game: Delta Force: Hawk Ops**
- 6 armor classes, 7 ammo pen levels (color-coded to match)
- Three outcomes:
  - Pen > Armor → **full damage**
  - Pen = Armor → **~25% damage** (blunt)
  - Pen < Armor → **0 damage** (only durability loss)
- Armor damage multiplier varies by matchup (e.g., Pen4 vs Armor6 = 60% durability damage)

| Pro | Con |
|-----|-----|
| Very clear outcomes (0/25%/100%) | Wrong ammo = completely useless |
| Color-coding aids readability | Harsh cliff — feels unfair |
| Forces ammo decision | Less nuance in mid-range matchups |

### Model 6: Continuous Curve (The Cycle: Frontier)
- Rarity-based: Common(10) → Legendary(33)
- Continuous penetration curve (no hard breakpoints)
- 5pt diff = ~13% mod, 10pt = ~23%, 20pt = ~37%
- Damage **amplification** when pen > armor (bonus damage!)
- Durability binary: full protection until 0
- Separate helmet + shield slots

| Pro | Con |
|-----|-----|
| No cliff effects | Max gap = only 37% (gear feels less impactful) |
| Low-gear players always do some damage | Contributed to game's balance issues |
| Simple rarity progression | Shut down partly due to gear gap frustration |

### Model 7: Flat Subtraction (Marauders)
- `FinalDMG = (BulletDMG - ArmorLevel) × BodyPartMultiplier`
- Armor levels ~5-10, no pen stat on ammo
- Head multiplier 3.5x AFTER armor subtraction
- Durability binary (works until 0)
- Some armor doubles as storage rig

| Pro | Con |
|-----|-----|
| Easiest to understand | No ammo depth |
| Headshot math is dramatic | Low-damage weapons become useless vs armor |
| Armor/storage trade-off | No material/repair economy |

### Model 8: RPG Stat-Based (Dark and Darker, STALKER 2)

#### Dark and Darker
- Armor Rating → non-linear DR% (cap 80%)
- Armor Penetration stat: `effective_DR = DR × (1 - Pen)`
- True Damage bypasses armor entirely
- Helmets have Headshot Reduction Mod (e.g., Great Helm = 23%)
- No durability on armor

#### STALKER 2
- Two stats: **Bulletproof** (penetration/bleeding) + **Impact** (HP absorption)
- Anomaly resistances (radiation, chemical, thermal, psi, gravity)
- Bleeding possible even with max bulletproof
- Artifact slots for further customization
- Weight/mobility trade-off (exoskeleton vs jacket)

---

## Master Comparison Table

| Feature | EFT | Arena Breakout | GZW | Duckov | Delta Force | The Cycle | Marauders | PUBG | CS2 | DMZ | STALKER2 | D&D | Hunt | Vigor |
|---------|-----|---------------|-----|--------|-------------|-----------|-----------|------|-----|-----|----------|-----|------|-------|
| Armor Tiers | 1-10 | 1-6 | NIJ | T0-T5 | 1-6 | Rarity | Lvl 5-10 | 1-3 | Binary | None | L/M/H | Rarity | None | Single |
| Pen System | Formula | 4 outcomes | NIJ vs ammo | Table lookup | Binary gate | Curve | None | None | Per-weapon% | None | Stat-based | Pen stat | None | None |
| Durability | Degrades eff. | Degrades pen% | Binary@0 | Degrades@50% | Degrades | Binary@0 | Binary@0 | Breaks | Minimal | None | Unknown | None | N/A | Hidden |
| Blunt DMG | 20-40% thru | Yes (non-steel) | Implied | Via multiplier | 25% at match | Via curve | Via subtraction | No | No | No | Impact stat | True DMG | N/A | No |
| Helmet/Vest | Yes+face | Yes+face+ricochet | Yes (IIIA cap) | Yes | Yes | Yes | Yes | Yes | Yes | No | Yes | Yes | No | No |
| Materials | 7+ types | 7 types | NIJ based | None noted | Limited | None | None | None | None | None | Suits | None | N/A | None |
| Repair | Yes (-max) | Yes (-max) | Yes | Yes (-max) | Yes | No (regen) | Yes | No | Rebuy | No | Yes | No | N/A | No |
| Ammo Variety | Very high | High | SP/FMJ/AP | Per caliber | 7 pen levels | Low | None | None | None | None | Caliber | None | None | None |
| Readability | Low | Low-Med | Low | Medium-High | Med (colors) | High (rarity) | High | Very High | High | Very High | Medium | Medium | N/A | Very High |
| Extraction Fit | Best | Best | Good | Very Good | Good | OK (dead) | Good | Poor | Poor | OK | Partial | OK | Poor | OK |

---

## Key Takeaways for ExtractionRaid

### What works for our requirements:
1. **Duckov's differential table** — most transparent pen system, easy to show in UI
2. **Tarkov's ammo diversity** — pen value + damage + armor damage as separate stats per ammo type
3. **Arena Breakout's 4 outcomes** — clear feedback categories (ricochet/block/partial/full)
4. **Delta Force's color coding** — visual language for ammo-vs-armor matchup
5. **CS2's helmet threshold** — helmet as binary "survive headshot or not" creates drama

### Problems to avoid:
1. **Hidden formulas** (Tarkov) — player doesn't understand why they died
2. **Gear cliff** (Delta Force) — wrong ammo = literally 0 damage feels unfair
3. **Tiny gap** (The Cycle) — max 37% difference makes gear feel unimportant
4. **No ammo depth** (PUBG/Marauders) — removes strategic layer

---

## Bleeding / Wound Systems Research

### Escape from Tarkov (most complex)
- **Trigger**: Per-ammo `lightBleedChance` / `heavyBleedChance` rolled on flesh hit
- **Tiers**: Light (1HP/6s all limbs) / Heavy (1.5HP/4s all limbs)
- **Treatment**: Bandage (light), Tourniquet/Hemostatic (heavy), Medkits (both)
- **Other wounds**: Fracture (limp/aim sway), Pain (blur/dark), Tremor (screen shake), Contusion (deaf), Destroyed limb (damage overflow)
- **Feedback**: Icons per limb, blood trails, screen blur/shake, limping sounds

### Escape from Duckov
- **Trigger**: Melee/sharp weapons (NOT bullets by default)
- **Tiers**: Single type, stacks x3 (1/2/3 HP/s)
- **Treatment**: Bandage, Medkit, Herat (grants bleed immunity)
- **Other wounds**: Pain (-90% speed, -5 stamina), Fracture (2HP/s while moving, -10 maxHP per stack)
- **Feedback**: HUD indicators

### Arena Breakout
- **Trigger**: Flesh damage from bullets
- **Tiers**: Light / Severe (bleed alone cannot kill — needs secondary damage)
- **Treatment**: Bandage (light), Coagulant (severe). Fix bleeds BEFORE broken limbs
- **Other wounds**: Broken limb (arm=aim sway, leg=slow, abdomen=hunger drain), Pain (breathing sounds scale with injury count)
- **Feedback**: Red vein overlay on screen, audible moaning

### Gray Zone Warfare (most realistic)
- **Trigger**: Penetrating damage, severity scales with damage amount
- **Tiers**: Light (self-heals 30s) / Medium / Severe (rapid death)
- **Treatment**: Two-step: Tourniquet (STOP) → Bandage (TREAT) → Blood bag (REPLENISH)
- **Other wounds**: Bruises (armor stops round), Bone damage, Organ damage, Blood volume system
- **Feedback**: HUD per limb, dizziness at low blood

### STALKER 2 (best visual feedback)
- **Trigger**: Penetrating/slashing damage
- **Tiers**: 4 color-coded — Green (mild) / Yellow (moderate) / Orange (serious) / Red (death in ~10s)
- **Treatment**: 1 bandage stops any level
- **Feedback**: Color-coded icon above HP, screen blood splatter, heartbeat acceleration

### Delta Force
- **Trigger**: Combat damage
- **Tiers**: Bleeding (HP drain) + Fracture (debuff + HP drain on movement)
- **Treatment**: Medkit (30HP/s + fix wounds), Surgical kit (fractures)
- **Unique**: **Pain screaming reveals your position to enemies!**
- **Feedback**: Blur, character screams

### Hunt: Showdown (best tactical design)
- **Trigger**: Rending ammo only (Dum Dum, Flechette) — ammo-type specific
- **Tiers**: 3 — Light / Medium / Intense (~18 DPS). **Escalation**: consecutive hits upgrade severity
- **Treatment**: Hold key 2/4/6 seconds (no consumable needed, but leaves you vulnerable)
- **Counter-traits**: Bloodless (cap at light), Blazeborne (instant stop)
- **Feedback**: 1/2/3 blood drop icons, wound wrapping animation

### PUBG
- No bleeding system. Uses DBNO (knock) in squad modes instead.

### Dark and Darker
- **Trigger**: Rogue class skill only (not universal)
- 20 damage over 5 seconds (4 DPS), single tier

---

## Bleeding Design Patterns

### Trigger mechanisms:
1. **Per-ammo chance** (Tarkov) — each ammo has bleed %, most granular
2. **Penetration-based** (GZW, ABI) — bullet penetrates armor = bleed, severity ∝ damage
3. **Ammo-type specific** (Hunt) — only special ammo causes bleed, creates ammo economy
4. **Melee/sharp only** (Duckov) — bullets don't bleed, simplest

### Tactical role of bleeding:
1. **Resource drain** (Tarkov, ABI, GZW) — spend bandages/meds from inventory → economic pressure
2. **Time pressure** (Hunt) — must stand still 2-6s to heal → vulnerability window
3. **Positional reveal** (Delta Force) — pain screams reveal location → stealth penalty
4. **Severity escalation** (Hunt) — consecutive bleed hits upgrade tier → rewards sustained fire

### Notable mechanics for ExtractionRaid:
- **Hunt's escalation** — consecutive bleeding hits upgrade severity (light→intense)
- **Delta Force's scream** — wound = audio/visual reveal on map (top-down = indicator?)
- **GZW's two-step** — tourniquet STOPS, bandage TREATS (two decisions, two items)
- **STALKER 2's color-coding** — 4 colors instantly readable
- **Duckov's stacking** — simple 1/2/3 HP/s, easy to understand
- **ABI's "can't kill"** — bleed alone won't deliver final blow (prevents feel-bad deaths)

---

## Continuous vs Discrete Armor/Penetration Research

### Games using continuous armor values (0-100+):
- **Warframe**: 15-1500+ armor, `DR = Armor / (Armor + 300)`, cap 90%
- **League of Legends**: 0-400+ armor, `DmgMulti = 100 / (100 + Armor)`
- **Dark and Darker**: piecewise linear AR → DR% (2%/pt low, 1%/pt mid, 0.5%/pt high), cap 80%
- **The Cycle: Frontier**: 10-33 armor points, scaled differential with diminishing returns
- **Remnant 2, Diablo 4, WoW, ARK**: all continuous with hyperbolic curves

### Games using discrete tiers + continuous penetration (hybrid):
- **Tarkov**: discrete armor class 1-10, continuous pen 0-79
- **Arena Breakout**: discrete 1-6, continuous pen stat within tier
- **Delta Force**: discrete 1-6 armor, discrete 1-7 pen

### Three formula types for continuous systems:

**Hyperbolic** `DR = Armor / (Armor + K)`:
- Naturally diminishing returns, elegant, each +1 armor = same EHP gain
- Used by: Warframe (K=300), LoL (K=100), Remnant (K=200)

**Piecewise linear** (Dark and Darker):
- Hand-tuned slopes per range (2%→1%→0.5% per point)
- Full designer control but "very difficult to predict" — player feedback

**Scaled differential** (The Cycle):
- `diff = Pen - Armor`, apply scale factor → multiplier
- 5pt diff = 13%, 10pt = 23%, 20pt = 37%
- Players found it confusing, armor felt too weak

### Readability problem with continuous:
- Dark and Darker: players say "very difficult to predict" DR from armor number
- The Cycle: confusing even with range 10-33
- **Diablo 4 solution**: show DR% directly, not just armor number
- **Tarkov advantage**: "Class 4 armor" is instantly understood

---

## Top-Down Shooter Combat Feedback Research

### Helldivers 2 (best armor feedback reference)
- **3-tier hitmarker**: Red X = full pen (100%), White X = partial (65%), Ricochet icon = blocked (0%)
- **Blue spark on deflection** — instant "your weapon can't penetrate" signal
- **Projectile physically ricochets** — observable physics event, not just UI
- No floating damage numbers — hitmarker color carries entire communication load

### Synthetik (best gun feel in top-down)
- Floating damage: **size = magnitude**, **color = type** (yellow headshot, orange crit, white normal)
- Hitstop (micro slow-motion) on critical hits
- Metallic ping/ding on every hit (enemies are robots)
- Dynamic crosshair bloom reflecting accuracy/recoil
- Screen shake proportional to weapon weight

### Foxhole (binary armor audio)
- **Deflect**: "pssht" + spark at impact
- **Penetrate**: different sound + shrapnel effect
- Two completely distinct audio-visual pairs for binary outcome
- Vehicle armor shows visual wear/damage on model

### Hotline Miami / Crimsonland (persistent environment)
- Blood accumulates as PERMANENT decals on floor
- From top-down camera this is uniquely powerful — battlefield tells the story
- Corpses remain entire level

### Nuclear Throne (screen shake philosophy)
- Directional screen shake opposite to firing direction (camera recoil)
- Larger weapons = more shake
- Corpse physics: bodies fling across room, bounce off walls
- No damage numbers — all feedback is kinetic/spatial

### Enter the Gungeon (minimal approach)
- White sprite flash on hit (~50-150ms) — bare minimum feedback
- NO floating damage by default (deliberate choice)
- Scouter item enables damage numbers (info as gameplay reward)

### ZERO Sievert (closest genre: top-down extraction)
- Functional/realistic feedback over arcade juice
- Emphasis on tactical decision-making feedback
- Recent updates focused on sharper recoil/NPC behavior feel

### Key patterns for ExtractionRaid:
1. **Material-based particles**: sparks for armor, blood for flesh (spatial, instant)
2. **Binary audio pairs**: distinct sounds for penetrate vs deflect (Foxhole)
3. **Size-coded damage numbers**: magnitude through size, type through color (Synthetik)
4. **Persistent blood decals**: different patterns for armor vs flesh hits
5. **Ricochet physics**: bullet visibly bouncing off (Helldivers 2)
6. **Hitmarker color = effectiveness**: 3 states minimum (Helldivers 2)
