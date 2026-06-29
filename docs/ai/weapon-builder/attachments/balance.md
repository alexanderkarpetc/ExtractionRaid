# Core Rarity Balance — Parabolic Scaling

> Per-tier stat balance for the two weapon cores (Payload / Delivery). The current authored
> numbers are the **Legendary** baseline; lower tiers scale **down** along a parabolic curve so
> rarity is strongly felt. Approved 2026-06-26. Numbers are tunable (2 knobs) — placeholders.
> Implemented in `Editor/WeaponBuilderStubAssets.cs`; design ref: [stats.md](./stats.md) · [slots.md](./slots.md).

## Model

- **Legendary = the existing balance.** Common → Epic are scaled down from it.
- **Parabolic**, not linear: slow growth at the bottom (Common/Uncommon/Rare clustered),
  steep at Epic → Legendary. The low tiers are "filler" loot; the chase is the top.
- **Compounds with slots** (P3): higher rarity already grants more attachment slots, so a
  Legendary core gives *better base stats + more tuning room* — two power axes on purpose.

### Curve

```
m(t)        = base + (1 - base) · (t/4)^p          // power (higher-is-better)
penalty(t)  = 2 - m(t)                              // lower-is-better (recoil/reload/spread/charge)
t = 0..4  (Common → Legendary)
base = 0.5   (RarityBaseFloor — Common floor; lower = more radical)
p    = 2.0   (RarityCurvePow  — steepness; higher = more top-heavy)
```

| Tier | Power `×L` | Penalty `×L` |
|---|---|---|
| Common    | 0.50 | 1.50 |
| Uncommon  | 0.53 | 1.47 |
| Rare      | 0.63 | 1.38 |
| Epic      | 0.78 | 1.22 |
| Legendary | **1.00** | **1.00** |

## What scales vs. what stays flat

Only "power/quality" stats scale; **archetype-identity** stats stay flat across all tiers, so a
Common Auto is still an Auto (same RoF, pellet count, aim cone).

| Core | Scales — power (`×m`) | Scales — penalty (`×(2−m)`) | Flat (identity) |
|---|---|---|---|
| **Payload** | Damage, Penetration, ArmorDamage | ChargeTime (Laser) | HeadshotMult, ProjectileSpeed, ProjectileLifetime, BleedChance |
| **Delivery** | MagazineSize, RecoilRecovery, BodyRotationSpeed, AimFollowSharpness | RecoilKick (V+H), SpreadAngle, ReloadTime, Equip/Unequip | **FireInterval (RoF)**, ProjectilesPerShot, ConeHalfAngle |

## Per-core tables (C · U · R · E · **L**)

### Payload — Ballistic
| Stat | C | U | R | E | **L** |
|---|---|---|---|---|---|
| Damage | 7.5 | 8.0 | 9.4 | 11.7 | **15** |
| Penetration | 7.5 | 8.0 | 9.4 | 11.7 | **15** |
| Armor Dmg | 2.5 | 2.7 | 3.1 | 3.9 | **5** |

### Payload — Laser
| Stat | C | U | R | E | **L** |
|---|---|---|---|---|---|
| Damage | 12.5 | 13.3 | 15.6 | 19.5 | **25** |
| Penetration | 12.5 | 13.3 | 15.6 | 19.5 | **25** |
| Armor Dmg | 4.0 | 4.3 | 5.0 | 6.3 | **8** |
| ChargeTime (s) ↓ | 1.50 | 1.47 | 1.38 | 1.22 | **1.0** |

### Delivery — Single-Action (Pistol)
| Stat | C | U | R | E | **L** |
|---|---|---|---|---|---|
| Recoil fwd ↓ | 2.3 | 2.2 | 2.1 | 1.8 | **1.5** |
| Recoil side ↓ | 1.5 | 1.5 | 1.4 | 1.2 | **1.0** |
| Reload (s) ↓ | 2.3 | 2.2 | 2.1 | 1.8 | **1.5** |
| Recovery spd ↑ | 2.0 | 2.1 | 2.5 | 3.1 | **4.0** |
| Magazine ↑ | 6 | 6 | 8 | 9 | **12** |

### Delivery — Auto (Rifle)
| Stat | C | U | R | E | **L** |
|---|---|---|---|---|---|
| Recoil fwd ↓ | 3.0 | 2.9 | 2.8 | 2.4 | **2.0** |
| Recoil side ↓ | 2.3 | 2.2 | 2.1 | 1.8 | **1.5** |
| Reload (s) ↓ | 3.0 | 2.9 | 2.8 | 2.4 | **2.0** |
| Recovery spd ↑ | 1.0 | 1.1 | 1.3 | 1.6 | **2.0** |
| Magazine ↑ | 15 | 16 | 19 | 23 | **30** |

### Delivery — Scatter (Shotgun)
| Stat | C | U | R | E | **L** |
|---|---|---|---|---|---|
| Spread ↓ | 45 | 44 | 41 | 37 | **30** |
| Recoil side ↓ | 9.0 | 8.8 | 8.3 | 7.3 | **6.0** |
| Reload (s) ↓ | 3.8 | 3.7 | 3.4 | 3.0 | **2.5** |
| Recovery spd ↑ | 1.5 | 1.6 | 1.9 | 2.3 | **3.0** |
| Magazine ↑ | 2 | 3 | 3 | 4 | **5** |

*(Flat across all tiers — not shown: FireInterval, ProjectilesPerShot, ConeHalfAngle,
ProjectileSpeed/Lifetime, HeadshotMult.)*

## Implementation

`Editor/WeaponBuilderStubAssets.cs`:
- Knobs: `RarityBaseFloor` (0.5) + `RarityCurvePow` (2.0).
- `PowerMul(tier)` / `PenaltyMul(tier)` — the curve.
- `ScalePayloadByRarity(legendary)` / `ScaleDeliveryByRarity(legendary)` — build the 5-tier
  array from the Legendary baseline (each Populate* method now passes its current values as the
  Legendary baseline).
- ChargeTime scaled in `PopulateLaser` (`_specificByTier`).

**Tuning workflow:** edit the 2 constants (or per-field direction in the Scale* helpers) →
run `Tools → Weapon Builder → Create Stub Assets` to re-bake all 5 tiers into the `.asset` files.

## Notes / open

- `StatsByTier` keeps its Common-fallback for *unauthored* tiers (used by tests via the factory);
  the generated assets now author all 5 tiers, so the fallback no longer triggers in-game.
- Mag sizes round to int (`RoundToInt`); tiny rounding (e.g. Scatter 5 → 2 at Common) is fine for
  placeholders.
- Knobs to revisit in playtest: `base` (radical-ness), `p` (steepness), and whether MagazineSize /
  ergonomics should scale at all (they're "felt" but blur archetype a little).
