# Battle Design — Current Status

> Living document. Updated as decisions are made.
> Last updated: 2026-03-28

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

**Why additive**: multiplicative creates mandatory builds (Warframe, Division 2 evidence)
**Why ammo largest**: consumable = extraction economy risk/reward
**Why char tree smallest**: Tarkov removed Recoil Control skill due to veteran gap

### 2. Armor — 2 Slots
- **Helmet** — head hitbox protection
- **Body Armor (vest)** — torso hitbox protection
- Hit resolution: purely hitbox-based (bullet hits head → helmet stats, bullet hits body → vest stats)
- Each piece has: **Protection Level** (tier) + **Durability** (current/max)
- No plate carrier system, no face shields, no side plates — just 2 modules

### 3. Durability
- Format: `current / max` (e.g., 75/100)
- Repair: restores current, permanently reduces max
- Armor is intentionally temporary — limited repair cycles before replacement
- Armor Damage stat on ammo/weapons determines durability loss per hit
- **Step-function degradation**:
  - 100%-51% durability → **full Protection** (nominal tier)
  - 50%-26% durability → **Protection -1 tier** (T5 performs as T4)
  - 25%-1% durability → **Protection -2 tiers** (T5 performs as T3)
  - 0% durability → **no protection** (armor destroyed)
- UI must show current breakpoint zone (e.g., colored durability bar segments)

### 4. Penetration — Differential Table (Duckov-inspired)
```
EffectivePen = AmmoPen + WeaponPenMod + CharPenMod    (hard cap: 6)
ArmorProt    = ArmorTier (degraded by durability below 50%)

Differential = ArmorProt - EffectivePen
```

| Differential | Damage Multiplier | Meaning |
|-------------|-------------------|---------|
| ≤ 0 | **1.0x** | Full penetration — armor does nothing |
| +1 | **0.67x** | Partial — 33% absorbed |
| +2 | **0.50x** | Half damage gets through |
| +3 | **0.40x** | Strong protection — 60% absorbed |
| +4 | **0.33x** | Near-full block — only 33% gets through |

**Why this model**: transparent (player sees Pen and Armor tier → knows result), no hidden RNG, built-in blunt damage (even +4 still hurts), easy to show in UI.

### 5. Damage Formula
```
RawDMG   = WeaponBaseDMG + AmmoDmgMod + WeaponDmgMod + CharDmgMod
FinalDMG = RawDMG × PenMultiplier × HeadshotMultiplier
```

### 6. Ammo Archetypes (per caliber)

Each caliber (Rifle, Shotgun, Pistol, future...) has ammo variants:

| Type | Pen | DMG | Bleed | ArmorDmg | Role |
|------|-----|-----|-------|----------|------|
| Standard | +1 | +0 | 0% | 0% | Cheap, baseline |
| AP | +3 | -5 | 0% | +10% | Anti-armor, less flesh |
| HP (Hollow Point) | +0 | +10 | +30% | 0% | Flesh shredder, useless vs armor |
| Shredder | +1 | +0 | 0% | +50% | Destroys armor durability |
| Incendiary | +0 | +5 | 0% | 0% | Burn status (+40%) |

**Design intent**: every ammo type has a clear tactical niche. No "best ammo" — only "best ammo for this situation."

### 7. Combat Visual Feedback

| Shot Result | Crosshair Reaction | Floating Damage | Sound | Healthbar |
|-------------|-------------------|-----------------|-------|-----------|
| Full penetration | Normal hit marker | White number (flesh dmg) | Metal crack + flesh | Normal red drain |
| Partial pen (blunt) | Dimmed/small marker | Gray number (reduced) | Dull thud | Slower drain |
| Blocked (armor holds) | Spark ✕ icon | "0" or shield icon | Metal clang / ricochet | No drain, armor flash |
| Bleed applied | Hit marker + 💧 drop | Red number + bleed icon | Wet/slash sound | Tick marks appear |
| Armor broken | Crack icon burst | "ARMOR BROKEN" text | Glass shatter sound | Armor segment shatters |
| Headshot | Gold ✕✕ double marker | Large gold number | Distinct headshot ping | Large chunk drain |

**Principle**: player should ALWAYS understand what happened from feedback alone, without checking numbers.

### 8. Bleeding
- **Trigger**: per-shot roll, `BleedChance = AmmoBleed + WeaponBleed + CharBleed` (cap: 70%)
- **2 severity levels**: Level 1 (light) and Level 2 (heavy)
- Level 2 activates same way as Level 1 — new bleed roll while already bleeding upgrades to Level 2
- Level 2: different icon, more blood decals, more DPS (concrete values TBD)
- **Treatment**: bandage per level (1 bandage = remove 1 level, Level 2 → Level 1 → clear)
- HP ammo = primary bleed source (30%), weapon mods and char tree add smaller amounts

### 9. Headshot Multiplier
- Headshot multiplier is a **stat in the modifier system** (like Pen, DMG, Bleed)
- `HeadshotMulti = BaseMulti + AmmoMod + WeaponMod + CharTreeMod`
- Follows same additive rules, same 3-source budget, has hard cap
- Concrete values TBD

### 10. Weight / Mobility
- Heavier armor = **movement speed penalty** (%)
- Higher tier armor = heavier = slower
- Concrete values TBD

### 11. Armor Visibility on Enemies
- Helmet and body armor are **visually rendered on character sprites/models**
- Different tiers have distinct visual appearance
- Top-down proportions designed to make armor readable at game camera distance

---

## ❓ OPEN QUESTIONS

No open questions at this time. See DEFERRED section for topics pending design.

---

## 🔜 DEFERRED (will design later)

| Topic | Status | Notes |
|-------|--------|-------|
| Weapon Mod Tree / Weapon Builder | Separate system | Will have its own design doc |
| Character Skill Tree structure | Will exist, details TBD | Budget confirmed: 10-15% of stat caps |
| Concrete stat values | TBD | Base DMG, Protection tiers, cap numbers, bleed DPS, headshot multi values |
| Armor crafting/repair economy | TBD | Materials, costs, repair stations |

---

## 📋 DECISION LOG

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-03-28 | Additive-only modifiers, no multiplicative | Prevents mandatory builds (Warframe/Div2 evidence) |
| 2026-03-28 | 3 sources: Ammo > Weapon > CharTree | Extraction economy + anti-veteran-gap |
| 2026-03-28 | Hard caps per stat | Prevents god-builds, encourages diversification |
| 2026-03-28 | Duckov-style pen table (differential lookup) | Most transparent for player, no hidden RNG |
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
| 2026-03-28 | Durability: step-function (50%→-1, 25%→-2) | Clear breakpoints, readable in UI, 0%=destroyed |
