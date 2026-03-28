# RPG Modifier System — Base Architecture

## Overview
ExtractionRaid uses a **multi-source additive modifier system** where stats are composed from 3 independent sources: Character Tree, Weapon Mods, and Ammunition. All sources stack **additively** with **hard caps** per stat.

This document defines the BASE rules that apply to ALL game stats — damage, penetration, bleeding, and any future parameters.

---

## Core Formula

```
FinalStat = BaseStat + AmmoMod + WeaponMod + CharTreeMod
```

**Always additive. Never multiplicative between sources.**

### Why Additive Only?

| Alternative | Problem | Example from competitors |
|-------------|---------|--------------------------|
| Multiplicative between sources | Exponential power creep, mandatory builds | Warframe: fully modded weapon = 10-50x base damage |
| Hybrid (additive inside, multi between) | "Mandatory category" always emerges | Division 2: Glass Cannon (Amplified) is near-mandatory |
| Best-of-category | Frustrating — bonuses don't combine | Destiny 2: multiple buffs of same type = wasted |

**Additive means:**
- Player can add numbers in their head: `3 + 1 + 0.5 = 4.5 Pen`
- Each bonus has a clear, predictable value regardless of other bonuses
- No hidden "this is secretly 3x better because it's in a separate multiplicative bucket"
- Balancing is linear — doubling a mod's value exactly doubles its impact

---

## Three Modifier Sources

### Source 1: Ammunition (PRIMARY — largest bonuses)

**Contribution**: 50-60% of total modifier budget

| Why largest? | Details |
|--------------|---------|
| **Consumable** | Ammo is spent every raid — economic risk/reward |
| **Tactical choice** | Player picks ammo per-raid based on expected threats |
| **Extraction economy** | Expensive AP ammo = high risk, high reward. Cheap ammo = safe but weak |
| **Per-encounter decision** | Different ammo in different mags for different situations |

**Examples:**
```
Standard Round:  Pen +1,  DMG +0,   Bleed +0%
AP Round:        Pen +3,  DMG -5,   Bleed +0%    (high pen, slight dmg penalty)
HP Round:        Pen +0,  DMG +10,  Bleed +30%   (no pen, high flesh dmg + bleed)
Incendiary:      Pen +0,  DMG +5,   Bleed +0%,   Burn +40%
Shredder:        Pen +1,  DMG +0,   Bleed +0%,   ArmorDmg +50% (destroys durability)
```

### Source 2: Weapon Modifications (MEDIUM — persistent investment)

**Contribution**: 25-35% of total modifier budget

| Why medium? | Details |
|-------------|---------|
| **Persistent** | Mods stay on weapon across raids — long-term investment |
| **Crafting/looting loop** | Finding/crafting a rare barrel = meaningful progression |
| **Build identity** | Weapon loadout defines playstyle |
| **Trade-off based** | Better pen barrel = worse accuracy (each mod has pros and cons) |

**Examples:**
```
Long Barrel:       Pen +1.0,  Accuracy +15%,  Handling -10%
Rifled Barrel:     Pen +0.5,  Range +20%,     Weight +5%
Hollow-Point Tip:  Pen -0.5,  DMG +8,         Bleed +10%
Match Trigger:     DMG +0,    FireRate +10%,   Recoil +5%
Suppressor:        DMG -3,    Sound -80%,      Pen -0.5
```

**Key design rule**: Weapon mods should ALWAYS have trade-offs. Pure upgrades don't exist.

### Source 3: Character Skill Tree (SMALLEST — progression ceiling)

**Contribution**: 10-15% of total modifier budget

| Why smallest? | Details |
|---------------|---------|
| **Anti-veteran-gap** | New player vs 100-hour player gap must be small |
| **Tarkov lesson** | EFT removed Recoil Control skill because it created invisible power gap |
| **Gear > Character** | In extraction shooters, GEAR you bring should matter more than account level |
| **Meaningful but not dominant** | Feels good to unlock, doesn't make you unkillable |

**Examples:**
```
Marksman I:    Pen +0.25
Marksman II:   Pen +0.25  (total +0.5)
Marksman III:  Pen +0.25  (total +0.75, MAX)

Butcher I:     Bleed +3%
Butcher II:    Bleed +3%  (total +6%)
Butcher III:   Bleed +4%  (total +10%, MAX)

Steady Hand I:  Recoil -3%
Steady Hand II: Recoil -3% (total -6%)
```

**Design rule**: Max character tree bonus for any stat ≤ 15% of the hard cap.

---

## Hard Caps

Every stat has an absolute maximum. No combination of sources can exceed it.

```
Penetration:     cap 6    (ammo max ~4 + weapon max ~1.5 + char max ~0.75)
Bleed Chance:    cap 70%  (ammo max ~40% + weapon max ~20% + char max ~10%)
Bonus Damage:    cap 30   (ammo max ~20 + weapon max ~8 + char max ~5)
Armor Damage:    cap 80%  (ammo max ~50% + weapon max ~20% + char max ~10%)
Headshot Multi:  cap TBD  (ammo + weapon + char — same 3-source budget)
Burn Chance:     cap 60%  (same distribution)
```

### Why Hard Caps?

1. **Prevents god-builds**: Even with best-in-slot everything, you can't exceed the cap
2. **Encourages diversity**: If you're capped on Pen, invest remaining budget in Bleed or DMG
3. **Predictable balance**: Designer knows the max possible value for every stat
4. **Diminishing returns without hidden math**: Instead of soft caps with invisible curves, hard cap is honest — "you're at max, invest elsewhere"

### Cap Budget Distribution

```
┌──────────────────────────────────────────────────────────────┐
│              HARD CAP (100% budget)                          │
│                                                              │
│  ┌─────────────────────────────┐                             │
│  │   Ammo (50-60%)             │  ← consumable, biggest      │
│  ├─────────────────────────────┤                             │
│  │   Weapon Mods (25-35%)      │  ← persistent, medium       │
│  ├─────────────────────────────┤                             │
│  │   Character Tree (10-15%)   │  ← progression, smallest    │
│  └─────────────────────────────┘                             │
│                                                              │
│  Sum of all sources CAN exceed cap → clamped to cap          │
│  This means: if you max ammo+weapon, char tree adds nothing  │
│  → encourages spreading investment across different stats     │
└──────────────────────────────────────────────────────────────┘
```

---

## UI: Player-Facing Stat Breakdown

### Weapon Inspect Screen

Every stat shows its composition with source-colored breakdown:

```
┌─────────────────────────────────────────┐
│ AK-74 + AP Rounds                       │
│                                         │
│ Penetration ████████░░ 4.5 / 6          │
│   ■ Ammo (AP-M):        3.0             │
│   ■ Barrel (Long):     +1.0             │
│   ■ Skill (Marksman II):+0.5            │
│                                         │
│ Damage ██████░░░░ 48                     │
│   ■ Ammo (AP-M):       -5               │
│   ■ Barrel (Long):     +0               │
│   ■ Skill (Precision I):+3              │
│   ■ Base weapon:        50              │
│                                         │
│ Bleed Chance ██░░░░░░░░ 13%             │
│   ■ Ammo (AP-M):        0%              │
│   ■ Barrel (Long):     +0%              │
│   ■ Skill (Butcher II): +6%             │
│   ■ Weapon perk:        +7%             │
│                                         │
│ Armor Damage ████░░░░░░ 35%             │
│   ■ Ammo (AP-M):        25%             │
│   ■ Mod (Shredder tip): +5%             │
│   ■ Skill:              +5%             │
└─────────────────────────────────────────┘
```

**Color coding per source:**
- 🟡 Ammo — yellow/gold (consumable, risk)
- 🔵 Weapon mods — blue (persistent)
- 🟢 Character tree — green (progression)
- ⬜ Base weapon — white/gray

### Combat Feedback (real-time)

Different shot results produce different feedback through existing systems:

| Result | Crosshair | Floating DMG | Sound | Healthbar |
|--------|-----------|-------------|-------|-----------|
| **Full penetration** | Normal hit marker | White number (flesh dmg) | Metal crack + flesh hit | Normal red drain |
| **Partial pen (blunt)** | Dimmed/small marker | Gray number (reduced) | Dull thud | Slower drain, different color? |
| **Blocked** | ✕ with spark icon | "0" or shield icon | Metal clang/ricochet | No drain, armor flash |
| **Bleed applied** | Hit marker + 💧 | Red number + bleed icon | Wet/slash sound | Tick marks on healthbar |
| **Armor break** | Crack icon | "ARMOR BROKEN" | Glass/crack sound | Armor segment shatters |
| **Headshot** | Gold double-marker | Large gold number | Distinct headshot ping | Large drain |

---

## Interaction with Armor System

The modifier system feeds directly into the armor penetration formula (see armor-research.md):

```
Effective Penetration = AmmoPen + WeaponPenMod + CharPenMod
Armor Protection = ArmorClass (modified by durability)

Differential = ArmorProtection - EffectivePenetration

Damage Multiplier = lookup(Differential)  // Duckov-style table
  0 or less:  1.0x  (full damage)
  +1:         0.67x
  +2:         0.50x
  +3:         0.40x
  +4:         0.33x

Final Damage = (WeaponBaseDMG + AmmoDmgMod + WeaponDmgMod + CharDmgMod) × DamageMultiplier
Bleed Roll = random(0-100) < (AmmoBleed + WeaponBleed + CharBleed)  // capped at 70%
```

### Example Combat Scenario

**Attacker**: AK-74 + Long Barrel + AP-M Rounds + Marksman II
```
Penetration: 3.0 (ammo) + 1.0 (barrel) + 0.5 (skill) = 4.5
Damage: 50 (base) - 5 (AP penalty) + 0 (barrel) + 3 (skill) = 48
Bleed: 0% (AP ammo) + 0% (barrel) + 6% (skill) = 6%
```

**Defender**: Level 5 Body Armor (Protection 5, full durability)
```
Differential = 5 - 4.5 = 0.5 → rounds to +1 → 0.67x multiplier
Final Damage = 48 × 0.67 = 32.2 HP
Bleed Roll: 6% chance → likely no bleed
```

**Same attacker vs Level 3 Armor** (Protection 3):
```
Differential = 3 - 4.5 = -1.5 → 0 or less → 1.0x multiplier
Final Damage = 48 × 1.0 = 48 HP (full damage!)
```

**Takeaway the player understands**: "My 4.5 Pen easily beats Level 3 armor, but struggles against Level 5. I need better ammo or a different barrel."

---

## Design Principles Summary

1. **Additive only** — player can do the math in their head
2. **3 sources with clear hierarchy** — Ammo (biggest) > Weapon (medium) > Character (smallest)
3. **Hard caps prevent god-builds** — encourages stat diversification
4. **Every mod has trade-offs** — no pure upgrades on weapons
5. **Ammo is consumable = extraction economy** — best stats require economic risk
6. **Character tree is small** — gear matters more than account level (anti-veteran-gap)
7. **Everything visible in UI** — color-coded breakdown, no hidden formulas
8. **Combat feedback per result** — crosshair, floating damage, sound, healthbar all react differently
