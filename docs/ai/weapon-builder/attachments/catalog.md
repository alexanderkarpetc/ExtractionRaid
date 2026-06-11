# Attachments — Starter Catalog (Iteration 4)

> Невеликий стартовий каталог модулів: конкретні attachment-и з give/take на наших полях ([`stats.md`](./stats.md)), по слот-категоріях ([`slots.md`](./slots.md)) + кілька unique.
> **Статус:** 🔬 draft каталог. **Числа — ілюстративні placeholder'и**, фінальні значення тюняться через DevCheats у плейтесті.
> **Дата:** 2026-06-10.

---

## 0. Принципи (нагадування)

- **Sidegrade, не upgrade:** кожен мод (крім дрібних generalist-ів) має **opposing-axis give/take** — виграш на одній осі = втрата на іншій ([`analysis.md`](./analysis.md)).
- **Generalist-виняток:** кілька дрібних near-no-downside модів ОК (Red Dot, Vertical Grip), якщо магнітуда мала — вартість = зайнятий слот (Destiny-патерн).
- **Магнітуди через DevCheats:** жодних hardcoded чисел; нижче — placeholder'и для відчуття пропорцій.
- **Маркер інфри:** ⚙️ = на наявних полях `WeaponStats`; ➕ = потребує нової механіки (Noise / Sight-FOV) або payload-specific стата.

---

## 1. Universal catalog (по слотах)

### 🔫 Muzzle (Delivery slot) — recoil / noise / damage axis

| Mod | Give (+) | Take (−) | Поля |
|---|---|---|---|
| **Suppressor / Глушник** | ➕ Noise −60%, ⚙️ Recoil −10% | ⚙️ Damage −10% | Noise, RecoilKick*, `Damage` |
| **Muzzle Brake / Дульний гальм** | ⚙️ Recoil −25% (V+H) | ➕ Noise +20% (гучніше) | RecoilKickForward/Side, Noise |
| **Power Compensator / Підсилювач** | ⚙️ Damage +12% | ⚙️ Recoil +15%, Spread +10% | `Damage`, RecoilKick*, `SpreadAngle` |

### 🖐 Grip (Delivery slot) — recoil / ergonomics axis

| Mod | Give (+) | Take (−) | Поля |
|---|---|---|---|
| **Vertical Grip / Передня рукоятка** | ⚙️ Recoil(V) −15% | — (generalist, мала магнітуда) | `RecoilKickForward` |
| **Angled Grip / Кутова рукоятка** | ⚙️ Ergonomics +15% (швидший ADS) | ⚙️ Recoil −5% лише (слабша стабілізація) | Ergonomics, RecoilKick* |

### 🪖 Buttstock (Payload slot) — stability ⇄ mobility axis

| Mod | Give (+) | Take (−) | Поля |
|---|---|---|---|
| **Heavy Stock / Важкий приклад** | ⚙️ Recoil −25%, Recovery +20% | ⚙️ Ergonomics −20% (повільніший ADS/move/turn) | RecoilKick*, `RecoilRecoverySpeed`, Ergonomics |
| **Skeleton Stock / Полегшений приклад** | ⚙️ Ergonomics +20% | ⚙️ Recoil +10% (менша стабільність) | Ergonomics, RecoilKick* |

### 🔭 Optic (Payload slot) — close-quick ⇄ long-range axis

| Mod | Give (+) | Take (−) | Поля |
|---|---|---|---|
| **Red Dot / Коліматор** | ⚙️ Spread −10%, Ergonomics +5% | — (generalist) | `SpreadAngle`, `ConeHalfAngle`, Ergonomics |
| **Sniper Scope / Снайперський приціл** | ➕ Sight Range ↑↑ (hidden), ⚙️ Spread −20% | ⚙️ Ergonomics −25%, ➕ FOV-cone ↓ (hidden) | Sight/FOV, `SpreadAngle`, Ergonomics |

### 🔋 Magazine (Payload slot) — capacity ⇄ speed axis

| Mod | Give (+) | Take (−) | Поля |
|---|---|---|---|
| **Extended Mag / Розширений магазин** | ⚙️ Magazine Size +50% | ⚙️ Reload +20% (показ дельтою), Ergonomics −10% | `MagazineSize`, `ReloadTime`, Ergonomics |
| **Quick Mag / Швидкий магазин** | ⚙️ Reload −25% (дельтою), Ergonomics +5% | ⚙️ Magazine Size −20% | `ReloadTime`, `MagazineSize`, Ergonomics |

---

## 2. Unique (archetype-restricted) mods

Звичайні слот-категорії з `CompatibleArchetype?` ≠ null. Зав'язані на наші signature-механіки.

| Mod | Слот · Restricted | Give (+) | Take (−) | Поля |
|---|---|---|---|---|
| **Laser Focusing Optic** | Optic · **Laser** payload | ⚙️ ChargeTime −30% (швидший заряд) | ⚙️ max-charge Damage −15% | `ChargeTime` (payload-specific), charge-curve |
| **Scatter Choke** | Muzzle · **Scatter** delivery | ⚙️ Spread −30% (тісніший cone, +дальність) | ⚙️ close-range coverage ↓, Recoil +10% | `SpreadAngle`, `ConeHalfAngle`, RecoilKick* |
| **Auto Heat-Sink** | Muzzle · **Auto** delivery | ⚙️ Heat ramp −30% (повільніший перегрів) | ⚙️ Damage −8% | `HeatLevel` coupling, `Damage` |

> Unique-моди = **identity-chase** під улюблений архетип (design.md §10). Кожен тюнить саме те, що робить архетип особливим: Laser → charge, Scatter → pellet-cone, Auto+Ballistic → heat.

---

## 3. Coverage check

Які стат-осі каталог реально вправляє:

| Стат-вісь | Покрито модами |
|---|---|
| Damage | Power Comp (+), Suppressor (−), Auto Heat-Sink (−), Laser Focusing (−max) |
| Headshot Mult | ⚠️ **не покрито** — поки жоден мод не чіпає (кандидат на майбутній precision-мод) |
| Rate of Fire | ⚠️ **не покрито** свідомо (RoF = archetype identity, ближче до Delivery-core; ризик дисбалансу) |
| Magazine / Reload | Extended Mag, Quick Mag |
| Recoil (V+H+recovery) | Suppressor, Brake, Comp, Vertical Grip, Angled Grip, Heavy/Skeleton Stock, Scatter Choke |
| Accuracy / Spread | Comp (+), Red Dot, Sniper Scope, Scatter Choke |
| Ergonomics | Angled Grip, Heavy/Skeleton Stock, Red Dot, Sniper Scope, Extended/Quick Mag |
| Noise | Suppressor, Brake — ➕ потребує Noise-механіки |
| Sight Range / FOV | Sniper Scope — ➕ потребує fog-of-war integration |
| ChargeTime (Laser) | Laser Focusing Optic |
| Heat (Auto) | Auto Heat-Sink |

**Спостереження:**
- **Recoil — найгустіша вісь** (7 модів). Очікувано: recoil/ergo — центр attachment-геймплею у всіх конкурентів.
- **2 моди залежать від нових механік** (Suppressor/Brake → Noise; Sniper Scope → FOV). Якщо механіки не в scope першого playtest — ці моди тимчасово на проксі (Suppressor cost = Damage, give = поки лише Recoil; Scope = поки лише Spread/Ergo без sight-range).
- **Headshot/RoF не чіпаємо** — свідомо (RoF = archetype identity; Headshot — кандидат на пізніше).
- **Кожна слот-категорія має ≥2 моди** = виражений opposing-axis вибір у кожному слоті.

---

## 4. Open questions

- **Q26. Розмір каталогу для першого playtest.** 11 universal + 3 unique — достатньо? Чи зрізати до 1/слот для MVP?
- **Q27. Generalist-моди (Red Dot, Vertical Grip) — лишаємо near-no-downside?** Чи дати дрібний cost для консистентності «усе sidegrade»?
- ~~**Q28. Rarity на самих модах?**~~ ✅ **RESOLVED 2026-06-10** — **ні, моди flat** (є мод і все). Rarity живе лише на cores (як `ExoticModInstance` — без rarity, консистентно з design.md hard rule). Core-rarity впливає на attachment-шар **тільки через к-ть слотів**, не через силу модів. Attachment instance = `{ SlotCategory, ModId }`, без rarity-поля.
- **Q29. Suppressor cost.** Damage (поточний вибір) vs ProjectileSpeed vs RoF? Тюнинг балансу.
- **Q30. Proxy-поведінка** Suppressor/Scope до того, як Noise/FOV-механіки готові — лишити частковими чи тримати весь мод за механікою?

---

## 5. Навігація
- [`stats.md`](./stats.md) — стат-словник (що моди чіпають)
- [`slots.md`](./slots.md) — слот-таксономія (де моди живуть)
- [`analysis.md`](./analysis.md) — sidegrade-принцип, competitor give/take
- [`competitor-research.md`](./competitor-research.md) — реальні give/take конкурентів (Duckov exact %)
