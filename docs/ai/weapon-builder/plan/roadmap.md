# Weapon Builder — Implementation Roadmap

> **Ultimate план реалізації фічі.** Структурований по tiers. Для кожного tier — архітектурні питання, рішення, work items, exit criteria, залежності.
>
> **Принцип:** Tier 0-2 опрацьовуємо детально (це vertical slice + найближче розширення). Tier 3-7 — high-level bullets, деталізуємо в міру наближення. Не намагаємось відповісти на всі архітектурні питання одразу.
>
> **Джерела:** декомпозиція з обговорення 2026-04-18, архітектурні питання з [../architecture.md](../architecture.md).

---

## Огляд tiers

| Tier | Назва | Статус |
|------|-------|--------|
| 0a | Data model foundation (types, SO infra, registry) | ✅ complete (2026-04-20) |
| 0b | Migration (refactor state, assembly pipeline, legacy cleanup) | 📋 ready for planning |
| 1 | Minimum Vertical Slice (Ballistic + Single-Action end-to-end) | ⏳ not started |
| 2 | Core breadth (Laser Charge + Auto + Scatter) | ⏳ not started |
| 3 | Content expansion (Foam, Rocket, Rotary, Swarm) | ⏳ not started |
| 4 | Rarity + Slot Compatibility | ⏳ not started |
| 5 | Exotic Mods | ⏳ not started |
| 6 | Loot / Economy integration | ⏳ not started |
| 7 | Polish (Art/VFX, UX, balance) | ⏳ not started |

**Відкладено поза scope:** Fist Delivery (melee system — окремий проект), Typed Attachments.

---

## Architectural questions (Tier 0 — all resolved)

Пройдені ДО старту коду. Детальні рішення — [architecture.md](../architecture.md).

- **Q1. Composed weapon representation** ✅ (2026-04-18) — composition + cached stats; `WeaponConfiguration` у `InventoryItem`
- **Q2. Delivery Core abstraction** ✅ (2026-04-19) — `FiringPattern` enum + internal dispatch у `ShootingSystem`
- **Q6. Rarity data model** ✅ (2026-04-19) — per-instance rarity; `StatsByTier` per module
- **Q7. Factory migration** ✅ (2026-04-19) — фазовано через compat layer; Shotgun видаляється
- **D1. WeaponStats field mapping** ✅ (2026-04-20) — 8 Payload + 13 Delivery, нуль overlap
- **D2. Heterogeneous payloads** ✅ (2026-04-20) — abstract base + typed Payload subclass'и
- **D3. ScriptableObject для Definitions** ✅ (2026-04-20) — SO + `Resources.LoadAll` via `ICoreDefinitionRegistry`
- **D4. Value semantics для Instances** ✅ (2026-04-20) — `[Serializable] readonly struct`

**Decision R1 (2026-04-20):** Tier 0 розділений на **0a (data model)** + **0b (migration)** для зменшення ризику великого diff і розблокування паралельної роботи.

---

## Tier 0a — Data Model Foundation

### Goal
Всі нові типи, SO infrastructure, registry port існують і покриті тестами. Старий weapon pipeline (factories, monolithic state) **ще живе як раніше** — нічого не мігровано. Безпечно мержиться, не ламає гру.

### Work items
- [ ] Enum `RarityTier { Common, Uncommon, Rare, Epic, Legendary }`
- [ ] Enum `FiringPattern { Single, Auto, Scatter, Rotary, Swarm }`
- [ ] `CommonPayloadStats`, `DeliveryStats` — serializable structs (common fields)
- [ ] Payload-specific stats: `LaserSpecificStats`, `RocketSpecificStats`, `FoamSpecificStats`
- [ ] `readonly struct PayloadCoreInstance / DeliveryCoreInstance / ExoticModInstance` з `[Serializable]` і `IEquatable<T>`
- [ ] `WeaponConfiguration` тип (поки не використовується runtime; додається в `InventoryItem` schema)
- [ ] Abstract SO `PayloadCoreDefinition` + 4 subclass'и (Ballistic, Laser, Rocket, Foam) з `[CreateAssetMenu]`
- [ ] SO `DeliveryCoreDefinition` (plain, без subclass'ів)
- [ ] SO `ExoticModDefinition` (plain)
- [ ] `StatsByTier` serialization: `CommonPayloadStats[]` індексований `(int)RarityTier`
- [ ] Port `ICoreDefinitionRegistry` + реалізація через `Resources.LoadAll` на startup
- [ ] Ports integration: `RaidContext` отримує registry
- [ ] Stub assets у `Assets/Resources/WeaponBuilder/`: Ballistic Round, Single-Action, Auto (Common tier заповнений реальними числами, що дублюють поточні Pistol/Rifle stats)
- [ ] Unit tests: registry lookup (ID→definition), struct equality, SO load coverage, `StatsByTier` indexing

### Exit criteria
- ✅ Всі нові types скомпільовані, реєстр працює, stub assets завантажуються через registry
- ✅ Unit tests зелені; coverage на structural equality і registry
- ✅ Старі weapons (Rifle/Shotgun/Pistol) працюють БЕЗ змін — ніхто нові types ще не використовує
- ✅ Shooting range, armor tests не зачеплені
- ✅ Merge без конфліктів з поточним кодом

### Dependencies
Немає.

### Unknowns / research needed
Жодних блокуючих (всі D1-D4 resolved).

### Parallel work unlocked
Після 0a дизайнер може наповнювати SO assets (stats, VFX refs), програміст — працювати над 0b.

---

## Tier 0b — Migration

### Goal
Переводимо existing weapons на новий pipeline через compat layer. Factories зникають (Shotgun повністю, `CreateRifle/CreatePistol` тимчасово через compat). `WeaponEntityState` refactored. ShootingSystem працює через dispatch.

### Work items
- [ ] A1. Refactor `WeaponEntityState`: composition refs (`PayloadCore / DeliveryCore / ExoticMod?`) + `WeaponStats Stats` cache + runtime fields окремо
- [ ] A6. `WeaponStatComposer` — `(PayloadCoreInstance, DeliveryCoreInstance, ExoticModInstance?) → WeaponStats` (20 common полів з 7+13)
- [ ] E1. `WeaponAssemblySystem.TryAssemble(WeaponConfiguration, out WeaponEntityState) → bool` — валідація existent definitionIds + composition. Fail: missing Payload/Delivery/Exotic (strict, no auto-repair per D7)
- [ ] E1.1. `WeaponAssemblyFailed` event у `RaidEventBuffer` (per D7)
- [ ] E1.2. Ghost-weapon handling у `WeaponSyncSystem`: TryAssemble fail → log + event, item лишається в inventory, hotbar slot empty
- [ ] E2. SO fields: `PayloadCoreDefinition.DisplayName`, `DeliveryCoreDefinition.FormFactor` (per D8)
- [ ] E3. `WeaponArchetypeLabel.Compose(Payload, Delivery) → string` helper
- [ ] E4. Update `WeaponBuilderStubAssets` editor script: додати DisplayName ("Ballistic") + FormFactor ("Pistol" для Single, "Rifle" для Auto)
- [ ] Rewrite `WeaponSyncSystem` на assembly pipeline — `WeaponConfiguration → WeaponEntityState` замість factory dispatch
- [ ] Compat layer `LegacyDefinitionToConfig` (static dictionary у `WeaponSyncSystem`): `"Rifle" → Ballistic/Common + Auto/Common`, `"Pistol" → Ballistic/Common + Single/Common`
- [ ] Видалити Shotgun **повністю**: `CreateShotgun`, `Ammo_Shotgun`, Shotgun SO assets, loot tables, inventory spawners — де б не було згадки
- [ ] Видалити `CreateRifle` / `CreatePistol` — вони вже через compat layer
- [ ] `ShootingSystem` rewrite: dispatch по `weapon.DeliveryCore.Pattern` (1 case `Single` повноцінно, `Auto` — shared helper з Single для Tier 0b)
- [ ] Read sites: `weapon.FireInterval` → `weapon.Stats.FireInterval` тощо (механічний refactor)
- [ ] D10. `RaidStateDebuggerWindow` — відобразити нові поля (composition refs + Stats block + runtime)
- [ ] Integration tests: Rifle і Pistol працюють ідентично pre-migration (FireInterval, damage, mag size через новий pipeline = значення що були hardcoded)
- [ ] Integration test: ghost-weapon path — invalid WeaponConfiguration → TryAssemble returns false, event emitted, item лишається

### Exit criteria
- ✅ `WeaponEntityState` — composition + `Stats` cache + runtime, не monolithic
- ✅ `WeaponAssemblySystem` приймає `WeaponConfiguration` → видає working `WeaponEntityState`
- ✅ Unit tests: composition correctness, rarity selection, 21 поле мапиться правильно
- ✅ Shotgun видалений повністю (grep на `Shotgun` / `Ammo_Shotgun` порожній поза worktree коментарями)
- ✅ Rifle і Pistol працюють через compat layer; gameplay parity з pre-migration (integration tests)
- ✅ `ShootingSystem` dispatch по FiringPattern (поки `Single` повний + `Auto` shared)
- ✅ Shooting range, armor tests зелені
- ✅ Raid State Debugger показує нові поля

### Dependencies
Tier 0a merged.

### Unknowns / research needed
Жодних блокуючих для коду, але перед Tier 1 треба закрити **D6** (re-assembly triggers), **D7** (invalid config handling), **D8** (archetype labels).

---

## Tier 1 — Minimum Vertical Slice

### Goal
Одна збірка (**Ballistic Round + Single-Action**) працює end-to-end: гравець на базі вибирає cores → отримує working weapon → може з неї стріляти.

### Architectural questions to resolve
- **Q3. Як абстрагувати Payload Core?**
  - `IPayloadBehavior` з реалізаціями vs data-only?
  - Для простих payload (Ballistic) вистачає data — але що з Laser (charge-up) у Tier 2?
  - A: _TBD (хоча б baseline для Tier 1)_

- **Archetype labeling (E4):**
  - Hardcoded mapping table vs runtime composition з правил?
  - A: _TBD_

### Decisions
_TBD._

### Work items
- [ ] B1. Ballistic Round — повноцінна data + Common StatsByTier заповнений реально
- [ ] C1. Single-Action Delivery — повноцінна поведінка (Tier 0 stub замінюється)
- [ ] Pistol мігрує з compat layer у явну конфігурацію Ballistic+Single (compat для Pistol видаляється)
- [ ] E4. Archetype label system (Ballistic+Single → "Pistol" / "Rifle"-like — обговорити)
- [ ] F1. Мінімальний UI збірки (debug form прийнятний)
- [ ] F2. Archetype preview з computed stats

### Exit criteria
- ✅ Гравець може на базі зібрати Ballistic + Single-Action збірку через UI
- ✅ Збірка з'являється в hotbar і стріляє
- ✅ Stats збірки видно в preview
- ✅ Pistol повністю мігрований (compat layer лишився тільки для Rifle)
- ✅ End-to-end flow документований у `architecture.md`

### Dependencies
Tier 0b complete.

### Unknowns / research needed
Tier 1 блокуючі:
- [x] ~~**D6.** Re-assembly triggers~~ ✅ On equip + explicit Apply (2026-04-20)
- [x] ~~**D7.** Invalid configuration handling~~ ✅ Ghost weapon pattern, strict (no auto-repair) (2026-04-20)
- [x] ~~**D8.** Archetype label system~~ ✅ Pure template `{DisplayName} {FormFactor}` (2026-04-20)

Відкрите:
- Де саме на базі живе Weapon Builder screen? Окремий UI, частина існуючого inventory UI, чи новий екран?

---

## Tier 2 — Core Breadth

### Goal
Довести, що data-driven архітектура масштабується. Додаємо складний payload (Laser Charge) і два параметричні delivery (Auto, Scatter). Після tier — 2 payload × 3 delivery = 6 working архетипів.

### Architectural questions to resolve
- **Q3 extended.** Як Laser Charge вписується в Payload abstraction? Charge-up це:
  - Окремий state у weapon state machine?
  - Поле в `WeaponEntityState` (`ChargeLevel`, `ChargeStartTime`)?
  - Payload-specific behavior hook у pipeline?
  - A: _TBD_

- **Laser Charge specifics:** hold-to-charge з release? Auto-release при повному? Overcharge?
  - A: _TBD (design question з status.md)_

### Decisions
_TBD._

### Work items
- [ ] B2. Laser Charge — charge-up логіка + stats + VFX hooks + `AmmoType = EnergyCell`
- [ ] C2. Auto Delivery — повноцінна поведінка
- [ ] C3. Scatter Delivery — повноцінна поведінка (як нова, не міграція Shotgun)
- [ ] Rifle мігрує з compat layer у явну Ballistic+Auto конфігурацію
- [ ] **Видалення compat layer у `WeaponSyncSystem`** — весь legacy зникає
- [ ] **Видалення `CreateRifle` / `CreatePistol`** — factories прибираються повністю
- [ ] Розширення archetype label system на 6 нових комбінацій

### Exit criteria
- ✅ 6 working архетипів (Ballistic/Laser × Single/Auto/Scatter)
- ✅ Laser Charge має відчутну charge-up механіку
- ✅ Перевірено, що додавання нового payload/delivery не потребує правок у центральних системах (тільки нових data/behavior)
- ✅ Factories + compat layer повністю видалені. Нуль legacy кодпафу.

### Dependencies
Tier 1 complete.

### Unknowns / research needed
- Як візуалізувати charge-up для гравця (bar? glow? sound)?

---

## Tier 3 — Content Expansion

### Goal
Повний набір 4 Payload × 5 Delivery (без Fist) = 20 архетипів.

### Work items (high-level)
- [ ] B3. Adhesive Foam — slow/sticking effect
- [ ] B4. Micro-Rocket — explosive AoE
- [ ] C4. Rotary — SpinUp state в state machine
- [ ] C5. Swarm — volley логіка (серія пострілів за один fire)

### Architectural questions (deferred)
- Як state machine розширюється на SpinUp (Rotary)?
- Як Swarm волей лягає на поточний fire flow (один fire event = кілька shots з інтервалом)?
- Adhesive Foam status effect — нова система чи розширення існуючої?

### Exit criteria
- ✅ 20 працюючих архетипів
- ✅ Кожен Delivery має свій відчутний feel

### Dependencies
Tier 2 complete.

---

## Tier 4 — Rarity + Slot Compatibility

### Goal
Механіка rarity (5 тірів з кращими статами) + явні правила сумісності модулів замість "все з усім".

### Work items (high-level)
- [ ] A3. Rarity data model (реалізація — structure вже затверджена в Tier 0)
- [ ] E3. Rarity Scaling System (застосування множників до stats)
- [ ] A4. Slot structure data model
- [ ] E2. Slot Compatibility Rules engine
- [ ] F5. UI feedback на заборонену комбінацію
- [ ] **B1. Bot weapon migration** — видалити hardcoded stat fields з `BotConstants`, додати `WeaponConfiguration` до `BotTypeConfig`, `BotSpawnSystem` через assembly pipeline. Per-bot rarity/delivery combinations (Scav=Common, Boss=Epic, heavy=Rotary). Balance може "попливти" — це ок, буде зафіксоване у цій же tier balance pass. Див. [status.md 2026-04-22](./status.md)

### Architectural questions (deferred)
- Q4 повністю: де живе правило сумісності (в модулі / в слоті / окремий rules engine)?
- Rarity множники: глобальна таблиця vs per-module? Конкретні числа?
- Banned combinations matrix — конкретний список?

### Exit criteria
- ✅ Rarity візуально відрізняється (модулі мають tier) і впливає на stats
- ✅ Неможливо зібрати заборонену комбінацію
- ✅ Slot structure відображена в UI

### Dependencies
Tier 3 complete (бажано, щоб було на чому тестувати rarity).

---

## Tier 5 — Exotic Mods

### Goal
5 Exotic Mods через event-driven hook system.

### Work items (high-level)
- [ ] D6. Hook system для Exotic Mods (event pipeline: on-fire, on-hit, on-kill, on-projectile-update)
- [ ] D1. Multi-Shot Pattern (fire handler)
- [ ] D2. Ricochet (projectile trajectory)
- [ ] D3. Split on Impact (hit handler)
- [ ] D4. Ammo Return on Kill (kill handler)
- [ ] D5. Boomerang Flight (projectile trajectory — найскладніший)

### Architectural questions (deferred)
- Q5 повністю: events vs strategies?
- Стекування — поточний scope 1 Exotic, але архітектура має дозволяти розширення?
- Exotic × Core compatibility — які комбінації мають сенс?

### Exit criteria
- ✅ 5 Exotic Mods працюють на будь-якій P×D комбінації (або explicit banned list)
- ✅ Hook system розширюваний для майбутніх Exotic Mods

### Dependencies
Tier 4 complete.

---

## Tier 6 — Loot / Economy Integration

### Goal
Модулі здобуваються через loot і extraction loop.

### Work items (high-level)
- [ ] G1. Core modules як loot items
- [ ] G2. Rarity distribution (які rarity звідки випадають)
- [ ] G3. Module storage в інвентарі
- [ ] G4. Integration з extraction loop

### Exit criteria
- ✅ Модулі падають з ворогів/контейнерів
- ✅ Гравець приносить модулі з рейду на базу
- ✅ Rarity distribution збалансована (не все Legendary відразу)

### Dependencies
Tier 4 complete (rarity потрібна для distribution).

---

## Tier 7 — Polish

### Goal
Фіча виглядає, звучить і грається як production quality.

### Work items (high-level)
- [ ] H1. Per-Payload VFX (projectile visuals, muzzle flash, impact)
- [ ] H2. Per-Delivery animations і SFX
- [ ] H3. Per-Exotic VFX (ricochet sparks, boomerang trail)
- [ ] H4. Weapon mesh variations (якщо потрібно)
- [ ] F3. Slot visualization в UI
- [ ] F4. Module inventory UI polishing
- [ ] I1-I4. Comprehensive testing + balance pass

### Dependencies
Tier 5-6 complete. Частина art-роботи (H1, H2) може йти паралельно з Tier 2-3, якщо є художники.

---

## Загальні принципи

1. **Не переходимо до наступного tier, поки exit criteria попереднього не виконані.** Tier — це gate, не просто мітка часу.
2. **Архітектурні питання закриваються в tier, де вони вперше стають блокерами.** Не раніше.
3. **Tier 0-2 — високий пріоритет детального планування.** Tier 3-7 — loose outlines, доуточнюємо по мірі наближення.
4. **Fist Delivery, Typed Attachments — поза scope роадмапи.** Повернемось як окремий проект.

---

## Related docs

- [../README.md](../README.md) — огляд фічі
- [../design.md](../design.md) — дизайн-спека v0.7
- [../architecture.md](../architecture.md) — архітектурні питання і відповіді
- [status.md](./status.md) — open questions, decisions log, blockers
