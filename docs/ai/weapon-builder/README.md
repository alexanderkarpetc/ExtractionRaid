# Weapon Builder

Системна фіча кастомізації зброї для extraction shooter. Поточна версія дизайну: **v0.7**.

> **Status (2026-04-24): ⏸ Paused after Tier 2.** Foundation + 6 working archetypes у грі. Playable, тестований. Продовження (Tier 3+) — коли повернемось.

---

## Quick resume для нової сесії

Якщо ти повертаєшся до фічі після паузи, читай у цьому порядку:

1. **Цей файл** — стан, що працює, що далі
2. [plan/status.md](./plan/status.md) — decisions log, відкриті питання, pause summary
3. [plan/roadmap.md](./plan/roadmap.md) — tier structure і exit criteria
4. [plan/tasks.md](./plan/tasks.md) — конкретні checkbox'и (T-*.NN)
5. [architecture.md](./architecture.md) — якщо працюєш з кодом: усі ключові рішення (Q1-7, D1-14) з rationale
6. [design.md](./design.md) — якщо треба перевірити design intent

**Короткий entry point:** `Tools → Weapon Builder → Create Stub Assets` у Unity Editor відновлює всі SO assets, якщо їх немає локально.

---

## Current state (2026-04-24)

### Tier progress

| Tier | Scope | Status |
|------|-------|--------|
| **0a** Data model foundation | Types, SOs, registry, DB | ✅ complete (2026-04-20) |
| **0b** Migration | State refactor, assembly pipeline, Shotgun + factories видалено | ✅ complete (2026-04-22) |
| **1** Vertical slice | Workbench, Builder UI (UI Toolkit), DevCheats, Ballistic+Pistol E2E | ✅ complete (2026-04-23) |
| **2** Core breadth | +Laser (charge-up), +Scatter, 6 archetypes | ✅ complete (2026-04-23) |
| **3** Content expansion | +Foam, +Rocket, +Rotary, +Swarm | ⏳ planned |
| **4** Rarity + Slots | Per-tier stat values, banned combos, bot weapon migration | ⏳ planned |
| **5** Exotic Mods | 5 Exotic mods via hook system | ⏳ planned |
| **6** Loot integration | Module items у loot pipeline | ⏳ planned |
| **7** Polish | VFX, SFX, balance, UI polish | ⏳ planned |

**Test coverage:** ~90 зелених тестів (24 Tier 0a + 29 Tier 0b + 22 Tier 1 + 15 Tier 2).

### Що працює у грі прямо зараз

**Player flow:**
1. Гравець у Hideout підходить до Workbench object → натискає `E`
2. Відкривається Weapon Builder modal (UI Toolkit): 2 dropdowns (Payload, Delivery) + live preview (stats + archetype label) + Build/Cancel
3. Select Payload × Delivery → preview оновлюється в реальному часі через presenter
4. Build → новий `ItemState` з `WeaponConfiguration` лендає у backpack
5. Close → control повертається. Equip у hotbar → weapon готов
6. Shoot — стріляє згідно з assembled stats

**Alt entry:** DevCheats → "Toggle Weapon Builder" button — відкриває Builder з будь-де (включно з рейдом).

**6 working archetypes:**

| Payload × Delivery | Archetype label | Fire behaviour |
|---|---|---|
| Ballistic × Single | "Ballistic Pistol" | Instant single shot |
| Ballistic × Auto | "Ballistic Rifle" | Instant auto fire |
| Ballistic × Scatter | "Ballistic Shotgun" | Instant 7-pellet burst |
| Laser × Single | "Laser Pistol" | 1s charge → single shot |
| Laser × Auto | "Laser Rifle" | 1s charge per shot → auto cycle |
| Laser × Scatter | "Laser Shotgun" | 1s charge → 7-beam burst |

**Charge-up feedback:** energy-blue dot ring навколо crosshair під час Charging phase, center dot pulses з intensity.

### Data-driven guarantee

Нуль hardcoded weapon numbers у game code. Усі stats приходять з SO assets у `Assets/Resources/WeaponBuilder/`:

```
CoreDefinitionDatabase.asset  ← central aggregator
Payloads/
  BallisticRound.asset
  LaserCharge.asset          (+ LaserSpecificStats { ChargeTime })
Deliveries/
  SingleAction.asset (FormFactor=Pistol, Pattern=Single)
  Auto.asset         (FormFactor=Rifle,  Pattern=Auto)
  Scatter.asset      (FormFactor=Shotgun, Pattern=Scatter)
```

Додавання нового Payload/Delivery = новий `.asset` файл у відповідну папку + entry у `CoreDefinitionDatabase`. Weapon Builder UI автоматично показує його.

---

## Короткий опис для команди

**Weapon Builder** — система кастомізації зброї, де гравець збирає зброю з двох ядер: **Payload Core** (що зброя випускає) і **Delivery Core** (як це дістається цілі). Комбінація двох cores визначає архетип зброї з explicit назвою (напр. "Laser Rifle", "Foam Shotgun"). Поверх архетипу можна додати один **Exotic Mod** — виразний twist поведінки снаряду або ресурсного ритму.

**Payload Cores (4):** Ballistic Round (стандартний снаряд), Micro-Rocket (вибуховий заряд), Laser Charge (charge-up лазер, реф Half-Life 1), Adhesive Foam (slow/sticking/movement denial). **Delivery Cores (6):** Single-Action (один важкий постріл), Auto (безперервна стрільба), Scatter (shotgun-like залп), Fist (контактний удар — виключений з WB, окрема melee система), Rotary (spin-up + високий темп), Swarm (volley мікро-снарядів). **Exotic Mods (5):** Ricochet, Split on Impact, Ammo Return on Kill, Boomerang Flight, Multi-Shot Pattern.

Система вирішує конкретні проблеми поточного стану: зброя — fixed items без ownership, лут одноманітний (пушка або строго краща, або строго гірша), немає причини тримати кілька збірок, немає підготовки loadout під конкретний рейд. Weapon Builder дає lateral variety — різні комбінації під різні ситуації замість вертикальної ієрархії сили. Збірка відбувається на базі. Модулі мають **Rarity** (вищий тір = кращі стати того ж модуля), а межі збірки задаються **структурою слотів і сумісністю модулів** — явними структурними правилами, а не прихованими числовими бюджетами.

---

## Архітектурні здобутки (що вже є у кодбазі)

**Composition-based state (§1, D1, D2):**
- `WeaponEntityState` — composition refs (`PayloadCore`, `DeliveryCore`, `ExoticMod?`) + cached `WeaponStats` + runtime fields
- `PayloadCoreDefinition` abstract base + 4 typed subclasses (Ballistic/Laser/Rocket/Foam), payload-specific stats через polymorphism
- `DeliveryCoreDefinition` concrete SO з `FiringPattern` enum

**Pipeline:**
```
Builder UI (WeaponBuilderPresenter — plain C#, testable)
  ↓ TryBuild
ItemState (HasWeaponConfiguration=true, у InventoryItem)
  ↓ ground ↔ inventory — WeaponConfiguration preserved
WeaponSyncSystem.BuildWeaponForItem
  ↓ WeaponAssemblySystem.TryAssemble
WeaponEntityState (runtime, composition + Stats + PrefabId)
  ↓ ShootingSystem dispatch по FiringPattern
Projectiles spawned
```

**Key systems (`Assets/Scripts/Systems/`):**
- `WeaponStatComposer` — pure: (Payload + Delivery + Rarity) → WeaponStats
- `WeaponAssemblySystem` — registry lookup + ghost-weapon handling (D7)
- `WeaponChargeResolver` — Laser detection + ChargeTime lookup
- `WeaponItemFactory` — central weapon item spawning (replaces old compat layer)
- `ShootingSystem` — pattern dispatch (Single/Auto/Scatter shared param handler; Rotary/Swarm throw)
- `WeaponStateMachineSystem` — Phase transitions (adds Charging handling)

**UI (`Assets/Scripts/View/UI/WeaponBuilder/`):**
- `WeaponBuilderWindow` MonoBehaviour + UIDocument (runtime UI Toolkit)
- `WeaponBuilderPresenter` plain C# (unit-tested, 14 tests)
- UXML/USS у `Resources/UI/WeaponBuilder/`
- `WeaponBuilderAssetsBootstrap` editor script auto-creates PanelSettings

**Scene objects:**
- `WorkbenchView` — proximity interactable, TextMesh prompt, opens Builder on `E`

**DevCheats integration:** toggle button у DevCheats window.

---

## Що ще треба зробити

### Content (Tier 3)
- Payload: **Foam** (status effects: slow + stick), **Rocket** (AoE explosion, ExplosionRadius)
- Delivery: **Rotary** (SpinningUp phase + state machine extension), **Swarm** (VolleyActive phase, serial burst)
- Full 4×5 матриця = 20 archetypes

### Systemic (Tier 4)
- Rarity: fill `StatsByTier` для Uncommon → Legendary, UI dropdown для rarity selection, balance pass
- Slot compatibility: banned combos matrix, UI feedback
- Bot weapons: мігрувати BotSpawnSystem на assembly pipeline

### Feature (Tier 5)
- Exotic Mods × 5, hook system (OnFire / OnHit / OnKill / OnProjectileUpdate)

### Integration (Tier 6)
- Module items як ground items / loot drops
- Remove "infinite" placeholder у Builder UI — filter по inventory modules

### Polish (Tier 7)
- VFX per payload (Ballistic bullet, Laser beam, Rocket explosion, Foam splat)
- SFX: charge sound, fire variations per delivery
- Weapon meshes per FormFactor
- Inventory UI: archetype label вмеsто "Weapon" DefinitionId
- Rarity visual tint на dropdown items + inventory items

### Відкладено з минулого
- `.cursor/rules/weapon-builder*.mdc` counterpart (per CLAUDE.md §7) — не зроблено
- Update `docs/ai/weapons.md` — зараз застаріла, описує pre-migration Rifle/Shotgun/Pistol
- Weapon view prefabs (Weapon_Shotgun було видалено — нового mesh для Scatter formfactor немає, fallback на Weapon_Rifle)

---

## Навігація

### Дизайн
- [design.md](./design.md) — поточний дизайн-док v0.7, source of truth по фічі

### Архітектура та імплементація
- [architecture.md](./architecture.md) — технічна архітектура, усі resolved rationale

### План та статус
- [plan/roadmap.md](./plan/roadmap.md) — tier structure + exit criteria
- [plan/tasks.md](./plan/tasks.md) — конкретні задачі з checkbox'ами (оновлюється по ходу коду)
- [plan/status.md](./plan/status.md) — decisions log, відкриті питання, pause summary

---

## Принципи організації документації

- **Концептуальні доки** (design, architecture, per-module specs) живуть довго і описують систему
- **Фазові/планові доки** (status, roadmap, tasks) живуть час реалізації і фіксують прогрес
- **Нові доки створюються в міру потреби**, а не заздалегідь — порожні файли лише створюють шум
- Коли з'являться per-module spec'и (payload-cores.md, delivery-cores.md тощо) — вони ляжуть у `modules/` і `systems/` відповідно
