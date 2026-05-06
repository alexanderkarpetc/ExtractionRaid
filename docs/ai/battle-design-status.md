# Battle Design — Current Status

> Living document. Updated as decisions are made.
> Last updated: 2026-05-06

## Reference Documents
- `docs/ai/rpg-modifier-system.md` — base modifier architecture (3-source additive, caps, UI)
- `docs/ai/armor-research.md` — competitor analysis (15+ games), bleeding systems
- `docs/ai/weapons.md` — existing weapon FSM, ammo types, aiming

---

## ✅ DECIDED

### 1. RPG Modifier System (BASE)
```
FinalStat = Base + AmmoMod + WeaponMod + CharTreeMod
```
- All additive, hard caps per stat
- Budget: Ammo (50-60%) > Weapon Mods (25-35%) > Char Tree (10-15%)
- Weapon mods always have trade-offs (no pure upgrades)
- UI: color-coded breakdown per source (🟡Ammo / 🔵Weapon / 🟢Character / ⬜Base)
- **All shot-related stats** use full modifier pipeline: `WeaponBase + Ammo + WeaponMod + CharTree`
  - This applies to: Penetration, Damage, ArmorDmg, BleedChance, HeadshotMulti, BurnChance
  - WeaponBase = weapon identity stat (sniper base pen > pistol base pen)

**Why additive**: multiplicative creates mandatory builds (Warframe, Division 2 evidence)
**Why ammo largest**: consumable = extraction economy risk/reward
**Why char tree smallest**: Tarkov removed Recoil Control skill due to veteran gap

### 2. Armor — 2 Slots, 2 Hitboxes ONLY
- **Helmet** — head hitbox protection
- **Body Armor (vest)** — torso hitbox protection
- Hit resolution: purely hitbox-based (bullet hits head → helmet stats, bullet hits body → vest stats)
- **Only 2 hitboxes**: head and body. No arms, legs, or other zones. EVER.
- Each piece has: **Protection Points** + **Durability** (current/max)
- No plate carrier system, no face shields, no side plates — just 2 modules
- No armor materials — Protection Points value is the only differentiator
- **No tiers** — continuous points scale (0-100), no T1/T2/T3 labels
- **At 0 durability**: armor BREAKS and DISAPPEARS from character
  - Helmet: flies off physically with impulse from bullet direction (like PUBG)
  - Body armor: shatter/crack VFX + disappears

### 3. Durability
- Format: `current / max` (e.g., 75/100)
- Repair: restores current, permanently reduces max
- Armor is intentionally temporary — limited repair cycles before replacement
- **Durability damage per hit = FLAT POINTS** (not % of max)
  - Each ammo/weapon has an ArmorDmg stat = flat durability points removed per hit
  - This means: high-MaxDur armor takes MORE hits to degrade (3x max = 3x hits)
  - "Sturdy but weak" armor (low ArmorPts, high MaxDur) = viable build
  - "Fragile but strong" armor (high ArmorPts, low MaxDur) = glass cannon
- **ArmorDmg follows full modifier system** (like all shot-related stats):
  `TotalArmorDmg = WeaponBaseArmorDmg + AmmoArmorDmg + WeaponModArmorDmg + CharArmorDmg`
- **Then scales with absorption**: `FinalArmorDmg = TotalArmorDmg × (1 + absorptionRatio)`
  - absorptionRatio = 1.0 - DamageMultiplier (from pen curve)
  - Full pen (multi=1.0): ArmorDmg × 1.0 (base damage to durability)
  - Half absorbed (multi=0.5): ArmorDmg × 1.5 (armor works harder = wears faster)
  - Near-block (multi=0.27): ArmorDmg × 1.73 (armor tanks the hit = heavy wear)
  - Ricochet: ArmorDmg × 2.0 (max — armor fully deflected the bullet)
  - Design: armor that PROTECTS you degrades faster. Extraction tension.
- Each repair cycle reduces MaxDur → buffer before degradation shrinks over armor's lifetime
- **Parabolic degradation curve**:
  ```
  if durability% >= 70%:
      EffectiveArmor = BaseArmor                 // safe zone
  else:
      t = durability% / 70%                      // normalize 0..1
      EffectiveArmor = BaseArmor × t^p           // p=2, tunable via DevCheats
  ```
  - 70-100% → **safe zone**, full Armor Points
  - Below 70% → parabolic decay (gentle at first, steep near 0%)
  - 0% → armor BREAKS and disappears
- Example (65 pts helmet): 70%→65, 60%→48, 50%→33, 30%→12, 10%→1, 0%→💥
- UI: durability bar with 3 colored zones:
  - 🟢 Green (70-100%) — safe zone, full protection
  - 🟡 Yellow (40-70%) — degradation started
  - 🔴 Red (0-40%) — critical, armor nearly useless

### 4. Penetration — Continuous Hyperbolic Curve
```
EffectivePen  = WeaponBasePen + AmmoPen + WeaponMod + CharTreeMod    (hard cap: 100)
EffectiveArmor = ArmorPoints (degraded by durability curve)

diff = EffectiveArmor - EffectivePen
```

```
if diff ≤ 0:  DamageMultiplier = 1.0            (full damage)
if diff > 0:  DamageMultiplier = K / (K + diff)  (armor absorbs)
```

With K=30 (tunable via DevCheats):

| Armor advantage | Multiplier | Absorbed | Feel |
|----------------|-----------|----------|------|
| 0 | 1.00x | 0% | Armor useless against this ammo |
| 5 | 0.86x | 14% | Slight protection |
| 10 | 0.75x | 25% | Noticeable |
| 20 | 0.60x | 40% | Serious protection |
| 30 | 0.50x | 50% | Half absorbed |
| 50 | 0.375x | 63% | Very strong |
| 80 | 0.273x | 73% | Near-full block |

**Why hyperbolic curve**:
- Smooth, no cliff effects (unlike Duckov's step table)
- Natural diminishing returns (first 20 pts = 40%, next 20 = +23%)
- Every +1 point matters (good for 3-source modifiers)
- K parameter tunable at runtime
- Player sees their Pen and enemy Armor → can estimate result
- Built-in blunt damage (multiplier never reaches 0)
- **No over-penetration bonus**: Pen 100 vs Armor 10 = same 1.0x as Pen 11 vs Armor 10
  - This is intentional: AP ammo vs unarmored target = 1.0x but with -5 DMG penalty
  - Standard ammo is BETTER vs unarmored (same 1.0x, higher base DMG)
  - Forces carrying mixed ammo types — AP is not universally best

### 5. Damage Formula
```
EffectivePen = WeaponBasePen + AmmoPen + WeaponPenMod + CharPenMod
RawDMG       = WeaponBaseDMG + AmmoDmgMod + WeaponDmgMod + CharDmgMod
FinalDMG     = RawDMG × PenMultiplier × HeadshotMultiplier
```

**Penetration sources (4 now, not 3):**
- WeaponBasePen — inherent to weapon (sniper > rifle > pistol)
- AmmoPen — ammo type (AP > Standard > HP)
- WeaponPenMod — from weapon attachments (barrel, etc.)
- CharPenMod — from character skill tree

**Headshot order of operations**: HeadshotMultiplier applies AFTER PenMultiplier.
This means elite helmets CAN make headshots weaker than bodyshots — this is a FEATURE.
It changes meta: elite helmet = "aim for the body instead" — tactical depth.

### 6. Ammo Archetypes (per caliber, 0-100 scale)

Each caliber (Rifle, Shotgun, Pistol, future...) has ammo variants:

| Type | Pen | DMG | Bleed | ArmorDmg (flat pts) | Role | Status |
|------|-----|-----|-------|---------------------|------|--------|
| Standard | +10 (Rifle) / +12 (Pistol) | +0 | 0% | +5 (Rifle) / +6 (Pistol) | Cheap, baseline | ✅ impl |
| AP | +35 (Rifle) / +30 (Pistol) | -5 | 0% | +8 (Rifle) / +7 (Pistol) | Anti-armor, less flesh | ✅ impl 2026-05-05 |
| HP (Hollow Point) | +0 | +10 | +30% (Rifle) / +25% (Pistol) | +0 | Flesh shredder, useless vs armor | ✅ impl 2026-05-05 |
| Shredder | +10 | +0 | 0% | +25 | Destroys armor durability | ⏸ deferred |
| Incendiary | +0 | +5 | 0% | +0 | Burn status (+40%) | ⏸ deferred (no Burn system) |

**Design intent**: every ammo type has a clear tactical niche. No "best ammo" — only "best ammo for this situation."
*Note: concrete values playtest-tunable. Composition pipeline у `ShootingSystem`: `WeaponBase + Ammo (+ WeaponMod + CharTree placeholders)`.*

**2026-05-05 impl note**: payload `BaseArmorDamage = 0` (canonical source = ammo). This means ArmorDmg differential lives entirely у ammo choice, не у weapon archetype. AP rifle effective ArmorDmg = 0 + 8 = 8.

### 7. Combat Visual Feedback — Continuous Proportional System

Feedback is NOT discrete states — it's a **continuous mix proportional to damage result**.

```
absorptionRatio = 1.0 - DamageMultiplier    // 0.0 = full pen, 1.0 = full block
fleshRatio      = DamageMultiplier           // 0.0 = no flesh dmg, 1.0 = full flesh dmg
```

**Particles** — proportional mix:
- `fleshRatio` controls **blood** amount (splatter size, decal count, intensity)
- `absorptionRatio` controls **sparks** amount (spark count, brightness, size)
- At multiplier 1.0 (full pen): 100% blood, 0% sparks
- At multiplier 0.5: 50% blood, 50% sparks (mixed impact)
- At multiplier 0.27 (near-block): 27% blood, 73% sparks (mostly metal)

**Floating damage** — size = magnitude:
- Number size scales with FinalDMG (bigger hit = bigger number)
- Color blends: white (full pen) → gray (heavy absorption)
- Small hits produce small subtle numbers, big hits produce large prominent numbers

**Sound** — proportional blend:
- `fleshRatio` controls flesh hit sound volume
- `absorptionRatio` controls metal/impact sound volume
- Full pen = loud flesh hit, quiet metal
- Heavy armor = loud clang, quiet flesh thud

**Persistent blood decals**:
- Decal size/count proportional to `fleshRatio`
- Full pen = large blood splatter on ground (stays permanently)
- Heavy armor absorption = small or no blood, spark scorch marks instead

**Special feedback states (discrete, not proportional):**

| State | Trigger | Crosshair | Floating | Sound | Particles |
|-------|---------|-----------|----------|-------|-----------|
| **Ricochet** | Helmet only, 40% when Pen < Armor | Spark ✕ + direction | No number | Ricochet ping | Bright spark + bullet deflects physically |
| **Bleed applied** | Bleed roll success | Marker + 💧 | Red + bleed icon | Wet/slash | Extra blood burst |
| **Armor broken** | Durability → 0 | Crack burst | "ARMOR BROKEN" | Glass shatter | Helmet flies off / vest shatters |
| **Headshot** | Head hitbox hit | Gold ✕✕ | Large gold number | Headshot ping | Blood burst (large) |
| **Kill** | HP → 0 | Red ✕ | — | Kill confirm | — |

**"Blocked" state**: does NOT exist as separate feedback. No shot is ever fully blocked
(multiplier never reaches 0). Instead, very high armor advantage naturally produces
mostly sparks + tiny damage number + loud metal sound = player understands "my weapon
is ineffective." Only helmet ricochet = true 0-damage block.

**MUST HAVE feedback principles:**
1. **Proportional particles**: blood/sparks ratio = damage/absorption ratio (continuous, not binary)
2. **Ricochet physics**: bullet visibly deflects off helmet (MUST HAVE)
3. **Persistent blood decals**: stay permanently, size proportional to flesh damage dealt
4. **Damage number size = magnitude**: bigger hit = bigger number (Synthetik pattern)
5. **Sound blend**: flesh vs metal sound volumes proportional to pen result
6. **No "blocked" state for body armor**: every shot does some damage + proportional feedback

**Principle**: player should ALWAYS understand what happened from feedback alone.
The continuous feedback system means every shot tells a story through its unique
mix of blood, sparks, sound, and number size.

### 8. Helmet Ricochet
- **Condition**: fires ONLY when bullet PenPoints < helmet ArmorPoints (diff > 0)
- **Chance**: 40% (fixed, parameterizable via DevCheats)
- **On ricochet**: 0 HP damage to player, helmet takes **full durability damage** (same ArmorDmg as normal hit)
  - Shredder ammo ricochet = still applies its +25 flat ArmorDmg bonus to helmet durability
  - This means ricochet is NOT free — helmet still degrades, eventually breaks
- **Visual**: bright spark VFX + bullet physically deflects off helmet + distinct ricochet ping
- **Attacker feedback**: spark ✕ icon on crosshair with deflection direction
- **Design intent**: makes helmets feel special vs body armor, rewards investing in headgear

### 9. Bleeding
- **Trigger**: per-shot roll, `BleedChance = AmmoBleed + WeaponBleed + CharBleed` (cap: 70%)
- **Bleed ignores armor**: roll happens regardless of pen result. Blunt hit through armor can still cause bleed
  - Design intent: HP ammo vs heavy armor = low direct damage but bleed still works = "death by bleeding"
  - Gives HP ammo a niche even vs armored targets (attrition warfare)
- **Shotgun**: each pellet = separate bleed roll. 7 pellets × low bleed% = shotgun is bleed machine
  - Balance via per-caliber ammo stats: Shotgun_HP might have 8% bleed per pellet (not 30%)
  - `1 - (0.92)^7 = 44%` chance per shot — strong but not guaranteed
- **2 severity levels**: Level 1 (light) and Level 2 (heavy)
- Level 2 activates same way as Level 1 — new bleed roll while already bleeding upgrades to Level 2
- Level 2: different icon, more blood decals, more DPS (concrete values TBD)
- **Treatment**: bandage per level (1 bandage = remove 1 level, Level 2 → Level 1 → clear)
- HP ammo = primary bleed source, weapon mods and char tree add smaller amounts

### 10. Headshot Multiplier
- Headshot multiplier is a **stat in the modifier system** (like Pen, DMG, Bleed)
- `HeadshotMulti = WeaponBaseHSMulti + AmmoMod + WeaponMod + CharTreeMod`
- **WeaponBaseHSMulti is per-weapon**: sniper has higher base HS multi than pistol
- Follows same additive rules, same 3-source budget, has hard cap
- Concrete values TBD

### 11. Weight / Mobility
- Heavier armor = **movement speed penalty** (%)
- Weight = `(ArmorPoints + MaxDurability)` summed across both equipped slots
- Speed multiplier = `max(WeightSpeedFloor, 1 - totalWeight × WeightSpeedFactor)`
- Constants ([`ArmorConstants.cs`](../../Assets/Scripts/Constants/ArmorConstants.cs)):
  - `WeightSpeedFactor = 0.0005f` (0.05% per weight unit)
  - `WeightSpeedFloor = 0.5f` (max 50% slowdown — god-gear edge case clamp)
- Tuning at 2026-05-05:
  - Basic kit (Helmet 30/100 + Armor 40/120 = 290 weight) → 14.5% slowdown
  - Mid-tier kit (~400 weight) → 20% slowdown
  - Elite kit (~550 weight) → 27.5% slowdown
- Per-piece weight override TBD (deferred — no special lightweight items yet)
- Multiplied у `MovementSystem.Tick` after sprint + ADS scales

### 12. Defender Feedback (own armor status)
- **HUD armor scheme**: visual diagram of helmet + vest on character silhouette (WoW-style)
- **Color-coded by durability zone**:
  - 🟢 Green (70-100%) — safe zone
  - 🟡 Yellow (40-70%) — degradation started, caution
  - 🔴 Red (0-40%) — critical, armor nearly useless
- Shows ArmorPoints value next to each piece (current effective, not base)
- **On-hit pulse**: armor piece flashes briefly when hit (visual confirmation)
- **Zone transition alert**: distinct sound + flash when armor drops from green→yellow or yellow→red
  - Helps player decide: keep fighting or retreat?

### 13. Looted Armor
- Killed enemy drops armor with **current durability preserved**
  - Enemy had 80pt helmet at 30% dur → lootable as 80pt helmet at 30% dur
- Player can equip looted armor immediately or stash in inventory
- Repair after raid restores current dur (but reduces max dur as usual)
- **Design intent**: looting armor from enemies = core extraction loop. High-value armor on a kill = reward

### 14. Armor Visibility on Enemies
- Helmet and body armor are **visually rendered on character models**
- **3-4 visual classes** of mesh/appearance (light / medium / heavy / elite look)
  - Each visual class covers a range of Armor Points (e.g., light=10-30, heavy=60-80)
  - Player sees "heavy helmet" → estimates ~60-80 pts
- **On-hit feedback confirms**: first shot reveals effectiveness through blood/sparks ratio
- Combination: visual estimate BEFORE combat + feedback confirmation DURING combat

---

## ❓ OPEN QUESTIONS

Surfaced 2026-05-05 audit (impl drift after a month of work).

### Implementation drift / gaps from current design

- [x] ~~Penetration cap enforcement~~ — ✅ shipped 2026-05-05 (`ArmorConstants.PenetrationCap`).
- [x] ~~ArmorPoints / ArmorDamage cap enforcement~~ — ✅ shipped 2026-05-05.
- [x] ~~Weight / mobility coupling~~ — ✅ shipped 2026-05-05 (linear, see §11).

**No open architectural items for V0.1.** Char skill tree та WeaponMod system пере'хали у DEFERRED — не V0.1 scope (decision 2026-05-05).

### Concrete value tuning (not architectural — playtest-driven)

- [ ] **HeadshotMulti per archetype.** Currently 2.0x flat for Pistol/Rifle/Shotgun (Ballistic & Laser). Design: sniper > rifle > pistol — defer differentiation until Sniper archetype lands.
- [ ] **Bleed L2 DPS values.** L1/L2 architecture exists, concrete tick rate / DPS scaling TBD.
- [ ] **Burn DPS / duration.** Incendiary ammo + Burn status — deferred, not implemented.

### Deferred ammo archetypes (architecture present, content gap)

- [ ] **Shredder ammo** (+25 ArmorDmg, anti-armor durability spec). Defer until Standard/AP/HP feel-tested.
- [ ] **Incendiary ammo** + Burn status system. Same defer trigger.

### Open economy/content questions

See DEFERRED section.

---

## 🔜 DEFERRED (will design later)

| Topic | Status | Notes |
|-------|--------|-------|
| **Character Skill Tree** | 🚫 **Out of V0.1 scope** (2026-05-05) | May not ship at all для V0.1. Stat budget reserved (10-15%) but не реалізується. Composition pipeline ready коли захочеться додати |
| **WeaponMod system** | 🚫 **Deferred indefinitely** (2026-05-05) | Mod items already exist in inventory/craft DB (Basic_Scope, Long_Barrel, Suppressor, тощо) як lootable objects but they don't modify weapon stats. Wiring stat impact = "колись, не зараз" |
| Concrete stat values | TBD playtest-tuned | Base DMG, Protection tiers, cap numbers, bleed DPS, headshot multi values |
| Armor crafting/repair economy | TBD | Materials, costs, repair stations |
| ~~Armor damage formula~~ | ✅ Resolved | ArmorDmg = flat pts × (1 + absorptionRatio) |
| Economy feel | TBD | Early=swap often, mid=rare armor, late=mid is base, high is precious |
| Per-piece weight override | TBD | Special lightweight elite armor — needed when content tier expands |
| Bleed L2 DPS values | TBD playtest | L1/L2 architecture present, concrete numbers not tuned |
| Burn status + Incendiary ammo | Deferred | Architecture supports (DamageModifier wired). Defer until Standard/AP/HP feel-tested |
| Shredder ammo | Deferred | Same as above |
| HSMulti per archetype | Deferred | All ranged weapons 2.0x flat. Wait for Sniper archetype |
| Bot ammo scaling | Deferred | All bots use Standard. Revisit when raid difficulty curve becomes a design pass |

---

## 📋 DECISION LOG

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-03-28 | Additive-only modifiers, no multiplicative | Prevents mandatory builds (Warframe/Div2 evidence) |
| 2026-03-28 | 3 sources: Ammo > Weapon > CharTree | Extraction economy + anti-veteran-gap |
| 2026-03-28 | Hard caps per stat | Prevents god-builds, encourages diversification |
| 2026-03-28 | ~~Duckov-style pen table~~ → replaced by hyperbolic curve | Superseded 2026-03-29 |
| 2026-03-28 | 2 armor slots only (helmet + vest) | Simplicity, no plate management |
| 2026-03-28 | Durability with permanent max loss on repair | Creates gear lifecycle for extraction economy |
| 2026-03-28 | Full visual feedback per shot result | MUST HAVE: player always understands what happened |
| 2026-03-28 | Bleeding: 2 levels, re-roll upgrades severity | Tarkov-inspired (light/heavy), simple escalation |
| 2026-03-28 | Bleeding treatment: bandage per level | 1 bandage = -1 level. Simple, consumable-based |
| 2026-03-28 | Headshot multiplier in modifier system | Same 3-source additive rules as other stats |
| 2026-03-28 | Weight → movement speed penalty | Higher tier = heavier = slower. Values TBD |
| 2026-03-28 | Armor visually rendered on characters | Readable at top-down camera distance |
| 2026-03-28 | Weapon builder = separate system | Deferred, own design doc later |
| 2026-03-28 | Char skill tree will exist, details later | Budget: 10-15% of stat caps confirmed |
| 2026-03-28 | ~~Durability: step-function~~ → replaced by parabolic curve | Superseded 2026-03-29 |
| 2026-03-29 | No armor materials — only tier/quality matters | Simplicity, no hidden stats |
| 2026-03-29 | Only 2 hitboxes EVER (head + body) | No limbs, no complexity creep |
| 2026-03-29 | 0 durability = armor breaks + disappears | Helmet flies off (PUBG-style), vest shatters |
| 2026-03-29 | Helmet ricochet: 40% when bullet Pen < helmet Prot | Makes helmets special, no HP dmg, durability dmg only |
| 2026-03-29 | Armor damage formula → deferred to next iteration | Focus on core pen/damage first |
| 2026-03-29 | Continuous points (0-100), NO tiers | Granular modifiers work naturally, smooth curve, no cliff effects |
| 2026-03-29 | Durability: parabolic curve (safe zone 70%+, t^p decay below) | Smooth, no cliff, aggressive near 0%, p tunable via DevCheats |
| 2026-03-29 | Hyperbolic pen curve: K/(K+diff), K=30 tunable | Smooth diminishing returns, every +1 point matters, DevCheats tunable |
| 2026-03-29 | Material-based particles: sparks for armor, blood for flesh | MUST HAVE — Helldivers 2 / Foxhole pattern |
| 2026-03-29 | Ricochet: bullet physically deflects off helmet | MUST HAVE — not just UI icon, actual physics visual |
| 2026-03-29 | Persistent blood decals on ground | Stay permanently, different patterns per hit type |
| 2026-03-29 | Floating damage size = magnitude | Bigger hit = bigger number (Synthetik pattern) |
| 2026-03-29 | No "blocked" state for body — only ricochet (helmet) | Continuous curve never reaches 0, feedback is proportional |
| 2026-03-29 | Continuous proportional feedback (blood/sparks ratio) | fleshRatio + absorptionRatio blend particles, sound, numbers |
| 2026-03-29 | Pen has 4 sources: WeaponBasePen + Ammo + Mod + Char | Sniper inherently pens more than pistol |
| 2026-03-29 | Headshot after armor = FEATURE that elite helmet changes meta | "Aim for body" vs elite helmet is tactical depth |
| 2026-03-29 | Enemy armor: visual mesh classes + on-hit feedback | 3-4 visual appearances + blood/sparks ratio confirms |
| 2026-03-29 | Bleed ignores armor (roll independent of pen result) | HP ammo niche vs armor = attrition via bleeding |
| 2026-03-29 | Shotgun: per-pellet bleed roll | Balanced via lower per-pellet bleed% on shotgun ammo |
| 2026-03-29 | Ricochet = full durability damage (same ArmorDmg) | Ricochet not free — helmet still degrades |
| 2026-03-29 | No over-penetration bonus (diff ≤ 0 = always 1.0x) | AP ammo worse vs unarmored — forces mixed ammo loadouts |
| 2026-03-29 | HeadshotMulti base is per-weapon (not global) | Sniper HS multi > Rifle > Pistol |
| 2026-03-29 | Durability damage = FLAT points per hit (not %) | High MaxDur = more hits to degrade. Enables "sturdy but weak" builds |
| 2026-03-29 | Bleed L2 re-trigger = ignored (already at max) | Simple, no stacking beyond 2 |
| 2026-03-29 | Bandage has cast time (value TBD) | Architectural: yes. Tuning: later |
| 2026-03-29 | ArmorDmg in ammo table = flat points (not %) | Consistent with flat durability damage system |
| 2026-03-29 | ArmorDmg scales with absorptionRatio: ×(1+absRatio) | Armor that protects = degrades faster. Max 2x on ricochet |
| 2026-03-29 | Weight = f(ArmorPts + MaxDur), overridable per item | Both protection and sturdiness contribute to weight |
| 2026-03-29 | Defender HUD: armor silhouette with color zones (WoW-style) | Green/yellow/red + pulse on hit + zone transition alert sound |
| 2026-03-29 | ALL shot stats use full modifier pipeline (WeaponBase+Ammo+Mod+Char) | Pen, DMG, ArmorDmg, Bleed, HSMulti, Burn — all consistent |
| 2026-03-29 | Looted armor keeps current durability | Core extraction loop: kill → loot armor → repair |
| 2026-05-05 | Ammo carries DamageModifier (AP -5, HP +10, Standard 0) | Closes design table → impl gap. AP becomes proper trade-off (better pen, less flesh DMG). HP becomes flesh shredder. Floor at 0 prevents negative damage from compounded penalties. |
| 2026-05-05 | Payload BaseArmorDamage = 0 (was 5/8) | Removes WeaponBase + Ammo double-count. Ammo is canonical ArmorDmg source — consistent з ammo-carries-modifier pattern. Standard rifle effective ArmorDmg drops 10 → 5 (closer to design intent). |
| 2026-05-05 | Bot ammo scaling deferred (all bots use Standard) | No PMC AP / Boss specials yet. Adds simplicity для playtest baseline; revisit when raid difficulty curve becomes a design pass. |
| 2026-05-05 | HSMulti per-archetype differentiation deferred | All ranged weapons currently 2.0x flat. Wait for Sniper archetype (Tier 3 deferred) before differentiating. |
| 2026-05-05 | Shredder + Incendiary ammo deferred | Architecture supports them (DamageModifier already wired). Defer until Standard/AP/HP feel-tested first. |
| 2026-05-05 | Pen/Armor/ArmorDmg caps enforced via `ArmorConstants` | Was documented invariant only. Hardcoded constants (no DevCheats config layer) — future-proofs additive stack for WeaponMod/CharTree. Zero behavior change today (current values under caps). |
| 2026-05-05 | Weight → speed: linear, hardcoded constants | `weight = ArmorPts + MaxDur` per slot summed; multiplier = `max(0.5, 1 - weight × 0.0005)`. Constants in `ArmorConstants` (no config — won't change often). Per-piece override deferred. Applies in MovementSystem after sprint/ADS scales. |
| 2026-05-05 | **Char skill tree out of V0.1 scope** | May not even ship у V0.1 release. Composition pipeline (`+ CharTreeMod` placeholder) залишається architectural, but no impl planned. Re-engage post-V0.1 if scope opens. |
| 2026-05-05 | **WeaponMod system deferred indefinitely** | Mod items already exist as lootable inventory objects (Scope/Barrel/Suppressor/Mag/Grip/Stock у Items+Craft DB), but stat composition wiring deferred to "колись". V0.1 ships з `WeaponBase + Ammo` only. |
| 2026-05-06 | Laser charge mechanic (HL Tau cannon) | Hold-to-charge, fire-on-release. Damage scales `lerp(0.3, 1.0, chargeRatio)`. Quick tap = weak, full hold = full damage. Replaces auto-fire-on-full-charge behavior. Differentiates laser vs ballistic feel значно. |
| 2026-05-06 | Laser rifle burst (laser + Auto delivery) | After release, fires `lerp(1, 6, chargeRatio)` shots auto-paced (interval 0.07s). All burst shots use cached BurstChargeRatio for damage + VFX. Quick tap = 1 shot, full hold = 6-shot burst. Rifle тепер distinct від laser pistol/shotgun behavior. |
| 2026-05-06 | Laser hitscan migration explicitly DECLINED | Recon analysis: top-down + cursor aim makes hitscan vs fast-projectile near-indistinguishable on 95% engagement distances. ZERO Sievert (reference) uses projectiles for lasers. Effort 4-6h not worth marginal gain. Re-engage если camera/aim model changes. |
