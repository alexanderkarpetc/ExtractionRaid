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
| 0b | Migration (refactor state, assembly pipeline, legacy cleanup) | ✅ complete (2026-04-22) |
| 1 | Minimum Vertical Slice (Ballistic + Single-Action end-to-end) | ✅ complete (2026-04-23) |
| 2 | Core breadth (Laser Charge + Auto + Scatter) | ✅ complete (2026-04-23) |
| UX Pass 1 | UX polish (Builder D&D rewrite, tooltips, ammo, archetype labels, resolution scaling) | ✅ complete (2026-04-27) |
| 6 | Loot / Inventory integration (modules-as-items, loot drops, dev grant) | ⏳ **NEXT** |
| 8 | 3D Modular Visualization | ⏳ planned |
| 3 | Content expansion (Foam, Rocket, Rotary, Swarm) | ⏳ planned |
| 4 | Rarity + Slot Compatibility | ⏳ planned |
| 5 | Exotic Mods | ⏳ planned |
| 9 | VFX / SFX Language | ⏳ planned |
| 10 | Weapon Feel Polish | ⏳ planned |
| ~~7~~ | ~~Polish (Art/VFX, UX, balance)~~ — **deprecated, split into 8/9/10** | — |

**Tier numbers = stable IDs** (для посилань у коді/тестах/інших доках). **Execution order — нижче, ≠ tier number order.**

**Відкладено поза scope:** Fist Delivery (melee system — окремий проект), Typed Attachments.

---

## Execution sequence (поточний план виконання)

Послідовність вибрана щоб максимізувати player-facing value на кожному кроці. Tier 6 і Tier 8 reordered наперед (раніше були "пізніше" по tier number, але дають видиму трансформацію feature найшвидше).

```
✅ 0a → 0b → 1 → 2 → UX Pass 1
⏳ 6 (loot)  →  8 (3D viz)  →  3 (content)  →  4 (rarity)  →  5 (exotic)  →  9 (VFX)  →  10 (feel)
```

**Чому така послідовність:**

1. **Tier 6 first** — рiveть фічу. Зараз модулі infinite (debug), backpack у Builder read-only. Після Tier 6 модулі = real loot, drag-from-backpack у Builder активується, можна dev-grant'ом видавати конкретні модулі для playtesting. **Closes core design promise** "raid → loot → build". Залежність від Tier 4 (rarity для distribution) → відкидаємо вимогу: початково все Common, rarity layer'иться у Tier 4.

2. **Tier 8 next** — 3D modular visualization. Currently всі weapons виглядають однаково (Weapon_Pistol/Rifle prefabs derive'аться з Delivery FormFactor only). Це підриває core promise "weapons are 2 modules". Після Tier 8 player візуально розрізняє Ballistic Pistol vs Laser Pistol vs Foam Pistol. Найбільший visual impact на playtest.

3. **3 → 4 → 5** — content + progression + identity (per original plan). Кожен tier розширює опції без зміни core loop.

4. **9 (VFX)** — після того як content існує (бо VFX мови треба знати під що проектувати).

5. **10 (Feel polish)** — фінальний tuning перед release. Iterative playtest loop.

**Parallel tracks possible:** Tier 8 (3D art), Tier 9 (VFX), part of Tier 10 (sound design) — все це може йти паралельно з programmer-driven Tier 3/4/5 якщо є artist/sound designer. Programmer track тримає sequential.

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

## Tier 6 — Loot / Inventory integration

> **Execution:** NEXT (per execution sequence above). Reordered наперед щоб fire-up the feature loop ASAP — модулі-як-items активують real inventory loop і дають реальну причину raid'ити.

### Goal
Модулі здобуваються як real inventory items. Builder перестає бути "infinite-supply debug screen" — стає продовженням inventory loop. **Builder UI не embed'ить власний backpack — використовує uGUI inventory canvas side-by-side** (per architecture decision 2026-04-28).

### Architectural decisions (resolved 2026-04-28)

1. **Module → ItemDefinition mapping:** hardcode 5 entries у `ItemDefinition.BuildRegistry` (BallisticRound, LaserCharge, SingleAction, Auto, Scatter). Auto-gen відкладений у Tier 4.
2. **Module stackability:** non-stackable (`MaxStackSize: 1`). Forward-compat з Tier 4 rarity.
3. **Build cost:** 1×payload + 1×delivery. Multi-quantity → Tier 10.
4. **Palette filter:** grayed-out for unavailable (не hidden) — player бачить possibility space.
5. **Bot module drops:** out of Tier 6 scope. Container drops + DevCheats покривають playtest. Bot drops → Tier 4 (з bot weapon migration).
6. **Side-by-side layout:** Builder + uGUI inventory canvas одночасно при interact з Workbench. Embedded backpack у Builder видаляється. Drag з inventory у Builder slot — через cross-stack `DragService` bridge.

### Work items (G1-G10)

- [ ] **G1.** Core modules як `ItemState` — 5 ItemDefinition entries у `ItemDefinition.BuildRegistry` (each: id, displayName, slot=Backpack, stackable=false)
- [ ] **G2.** Module spawning у loot tables — додати entries у `ContainerConstants.RandomLootBox` + new `ModuleCache` container type. **Bot drops out of scope** (Tier 4).
- [ ] **G3.** DevCheats "Spawn Module" — dropdown by type у `DevCheatsWindow.cs`, places item у player Backpack (для playtest без рейду)
- [ ] **G4.** Builder palette filter — `WeaponBuilderPresenter` exposes `IsPayloadAvailable(id)` / `IsDeliveryAvailable(id)` (read inventory), `ModuleCardElement` adds `wb-card-unavailable` class for grayed-out look
- [ ] ~~**G5★.** Cross-stack drag bridge.~~ **DEFERRED → Tier 4** (2026-04-28). Palette уже drag-source; drag-from-inventory дублював би функціонал. Unique value (instance disambiguation when 2× BallisticRound різних rarity) виникає тільки у Tier 4. Wave D (G6 build cost) + Wave E (G4 palette filter) разом дають complete inventory loop без cross-stack drag.
- [ ] **G6.** Build consumes modules — `WeaponBuilderPresenter.TryBuild` removes 1×payload + 1×delivery items from backpack on success. Fails з reason "Out of stock" якщо modules disappeared (race з inventory mutation).
- [ ] **G7.** Initial player loadout — starting modules у `Player`/`PlayerProfileState` setup. New player inventory contains 1× of each Common module so Builder is immediately functional.
- [ ] **G8.** ~~Inventory slot type для модулів~~ — **resolved**: модулі лежать у звичайному Backpack (decision 2026-04-28).
- [ ] **G9.** Open uGUI inventory canvas alongside Builder — Workbench interact triggers BOTH `WeaponBuilderWindow.Open()` AND inventory canvas show. ESC/Cancel у Builder closes both. Inventory layout shifted left у "Builder open" mode.
- [ ] **G10.** Layout coordination — Builder centered → Builder positioned right of viewport center; inventory canvas left. Anchor points + position math у `WeaponBuilderWindow.Open()` що sets layout mode + notifies inventory canvas.

### Removals (cleanup, виконати на старті Tier 6)

- [ ] Видалити `BackpackItemElement.cs` — embedded backpack item view більше не потрібен
- [ ] Видалити `wb-backpack-panel`, `wb-backpack-grid`, `wb-bp-item*` USS classes
- [ ] Видалити `RefreshBackpack()` + `_backpackGrid`/`_backpackItems` fields у `WeaponBuilderWindow.cs`
- [ ] Видалити `<ui:VisualElement name="backpackPanel">` block у `WeaponBuilderWindow.uxml`

### Execution waves (priority-ordered, revised 2026-04-28)

| Wave | Items | Why this order | Verifiable end state |
|---|---|---|---|
| **A. Side-by-side launch** ✅ DONE 2026-04-28 | G9 + G10 + cleanup (delete embedded backpack) | **Architectural pivot.** Без side-by-side layout усі інші waves адаптуються до помилкової assumption (embedded backpack). | ✅ Workbench → E opens Builder right + uGUI inventory left; loot panel hidden у Builder mode; Tab/ESC/× closes both; backdrop transparent + picking-mode=Ignore. |
| **B. Foundation** ✅ DONE 2026-04-28 | G1 + G3 | Modules as items + DevCheats grant — testbed для решти Tier 6 | ✅ 5 module ItemDefinitions у `BuildRegistry`; DevCheats "Spawn Module" + "Spawn All Modules" buttons. |
| ~~**C. Cross-stack drag bridge**~~ | ~~G5★~~ | **DEFERRED → Tier 4** (2026-04-28). Палітра вже drag-source — drag-from-inventory дублював би функціонал. Unique value cross-stack drag (instance disambiguation) виникає тільки коли rarity (Tier 4) робить individual items meaningful. Premature optimization для Tier 6. | — |
| **D. Build cost** ⭐ NEXT | G6 | Closes build cycle — Build тепер реально "коштує" модулі | TryBuild removes 1×payload + 1×delivery from backpack on success. |
| **E. UX completeness** | G4 | Visual feedback "що ти можеш зібрати" | Builder palette grayed-out для модулів яких нема у inventory. |
| **F. Economy** | G2 | In-game inventory loop | Open container in raid → module drops. |
| **G. Initial state** | G7 | Fresh save UX | Fresh save → modules у backpack без DevCheats. |

**Why Wave A first** (revised priority 2026-04-28): без side-by-side layout усе інше базується на assumption яку ми тільки-но відмовились (embedded backpack у Builder). Робимо architectural pivot first — навіть якщо G1/G3 (foundation) логічно "перші" по dependency graph, layout decision є **проривним** і має landing першим щоб інші waves будувались на ньому.

---

### Wave A — Side-by-side launch (detailed plan)

**Goal:** Workbench interact відкриває Builder + uGUI inventory canvas side-by-side. Embedded backpack у Builder видалений. Existing inventory functionality (drag/drop, equipment, hover tooltips, durability bars) працює з коробки.

**Current state (relevant):**
- `WeaponBuilderWindow` (UI Toolkit) — modal centered у panel, embedded `backpackPanel` всередині body ScrollView
- `InventoryUI.cs` — Tab key handler, sets `IsInventoryOpen` flag, drives `LootPopupView` через `PopupManager`
- `LootPopupView` (PopupBase, uGUI Canvas) — actual inventory visual: `_playerPanel` + optional `_lootContainerParent` (hidden when no loot)
- Existing pattern: setting `CraftTargetId` CLOSES inventory (InventoryUI:42-46) — opposite for Builder, треба інверсну coordination

**Sub-tasks:**

| Task | Files | Description |
|---|---|---|
| **A.1** Cleanup embedded backpack | `WeaponBuilderWindow.uxml/uss/cs`, delete `BackpackItemElement.cs` | Remove backpackPanel block, RefreshBackpack(), _backpackGrid/_backpackItems fields, all `wb-backpack-*` USS classes. |
| **A.2** WeaponBuilderWindow positioned right | `WeaponBuilderWindow.uss` `.wb-backdrop` align-items center → flex-end (right) with right padding | Builder shifts right, leaving ~700px на лівій частині viewport для inventory. |
| **A.3** Open inventory alongside Builder | `WeaponBuilderWindow.cs` Open(), new public API on `InventoryUI`, possibly new `BuilderTargetId` flag on PlayerEntityState | Workbench interact → Builder.Open() + InventoryUI.OpenForBuilder() (analog of LootTargetId/CraftTargetId pattern). |
| **A.4** Inventory canvas positioned left | `LootPopupView` RectTransform anchor — new "Builder mode" alignment, OR direct position adjustment | Inventory `_playerPanel` shifts to left side of viewport. Loot containers panel hidden у Builder mode. |
| **A.5** Coordinated close lifecycle | `WeaponBuilderWindow.Close()`, `InventoryUI.Update()` | ESC/Cancel/× у Builder → both close. Tab while Builder open → ignored OR also closes both (TBD). |

**Open design questions — confirmed 2026-04-28:**

1. ✅ **Position offsets** — measure first approach. Inspect inventory canvas width, then adjust Builder + inventory positioning to fit 1080p reference viewport.
2. ✅ **Inventory mode у Builder context** — only player panel (`_playerPanel`); hide `_lootContainerParent` (no loot context on Workbench).
3. ✅ **State flag** — new `BuilderTargetId` EId on `PlayerEntityState`, parallel to `LootTargetId`/`CraftTargetId`. InventoryUI watches → opens inventory canvas if `!= EId.None`.
4. ✅ **Tab as universal close** — Tab is the canonical "close everything" key. Pressing Tab while Builder open → closes BOTH Builder and inventory. ESC у Builder → same coordinated close.

**Effort:** ~4-6h. Plurality of work — coordination між UI Toolkit та uGUI lifecycle, layout positioning, edge cases у close logic.

**Tests:** Manual playtest checklist. Unit tests difficult у view layer; rely on existing tests still зелені.

---

### Exit criteria

- ✅ Модулі падають з contains'ів як real items + DevCheats grant works
- ✅ Гравець приносить модулі з рейду на базу
- ✅ Workbench interact → Builder + uGUI inventory side-by-side
- ✅ Drag з inventory у Builder slot працює (cross-stack bridge OK)
- ✅ Build consume'ить 1×payload + 1×delivery з backpack
- ✅ Builder palette показує grayed-out unavailable
- ✅ Initial player loadout містить starting modules
- ✅ Embedded backpack code видалений (no dead code)

### Dependencies

UX Pass 1 complete. ~~Tier 4 (rarity для distribution)~~ — зняли, все Common initially.

### Estimated effort

~12-15h Tier 6 (revised з 8-12h після adopting side-by-side approach). Cross-stack drag bridge — найвагоміша частина (~4-6h).

---

## Tier 7 — DEPRECATED

> **Розформовано (2026-04-27):** original Tier 7 був "polish bucket" що неявно об'єднував три зовсім різні категорії робіт (3D mesh variants / VFX-SFX / feel tuning). Розділено на **Tier 8** (3D viz), **Tier 9** (VFX/SFX), **Tier 10** (Feel polish) для чесного scope tracking.

---

## Tier 8 — 3D Modular Visualization

### Goal
Player візуально розрізняє composition. Замість одного `Weapon_Pistol` prefab'а для всіх "Pistol"-form builds — modular weapon view де payload mesh + delivery mesh runtime assembly. 4 payload meshes + 5 delivery meshes покривають усі 4×5=20 archetypes без геометричного scope.

### Architectural questions
- **Assembly model:**
  - **(A) Pre-built prefabs per archetype** — 20 prefabs at scale, scope grows ×N з exotic mods
  - **(B) Modular runtime composition** — each module mesh з attachment socket, runtime assembly
  - Recommend **(B)** — реально virtually безкоштовно scaling, відображає design intent (composition-based weapons)
- **Attachment socket strategy:** delivery mesh має named socket (e.g., "PayloadMount") — payload prefab прикріплюється з local transform
- **Animation rigging:** WeaponView animator має знати про modular parts (наприклад reload тримає charge module visible, fire trigger animates payload-specific particles emitter)

### Work items (high-level)
- [ ] V1. `WeaponView` rewrite — composition-aware, спам payload + delivery sub-prefabs at runtime
- [ ] V2. Modular mesh contract — кожний `PayloadCoreDefinition` / `DeliveryCoreDefinition` reference's prefab + attachment socket name
- [ ] V3. Resource loading — payload/delivery prefabs у `Resources/WeaponBuilder/Modules/`
- [ ] V4. Animator integration — bone/socket mapping per module type
- [ ] V5. Art delivery: 4 payload meshes (Ballistic, Laser, Rocket, Foam) + 5 delivery meshes (Single/Auto/Scatter — Pistol/Rifle/Shotgun forms; Rotary, Swarm — окремі forms)
- [ ] V6. Backpack item icons reflect actual archetype (compose from module thumbnails or use archetype-specific icon set)

### Exit criteria
- ✅ Build Ballistic+Pistol vs Laser+Pistol — visually distinct у hand
- ✅ Equip swap показує correct mesh
- ✅ Adding нового payload/delivery (Tier 3) = drop in 1 mesh, no system changes
- ✅ Inventory icons reflect archetype (player одразу бачить що у backpack)

### Dependencies
Tier 6 complete (бажано — модулі-як-items дає stronger reason to differentiate visually).

### Parallel tracks
Art (modular meshes + rigging) може йти паралельно з усіма іншими tiers якщо є artist. Programmer-side робота compact (~3-5 days).

---

## Tier 9 — VFX / SFX Language

### Goal
Кожен archetype має впізнавану візуальну і звукову мову. Ballistic стріляє кулями з muzzle flash; Laser — beam'ом з charged glow; Rocket — missile + AoE explosion; Foam — viscous splash; etc.

### Architectural questions
- **VFX hook system:** event-driven (через `RaidEventBuffer`) чи pulled by view? Recommend events — вже існує infrastructure (WeaponFired, ProjectileSpawned, etc.).
- **Particle ownership:** per-payload particle prefab vs runtime composition like meshes? Recommend per-payload prefab — particle composability вища.
- **SFX pipeline:** AudioSource pooling? Separate `SfxPresenter`? Recommend pooled через event-driven presenter (як інші presenter'и).

### Work items (high-level)
- [ ] X1. VFX events plumbing — `WeaponView` reads `WeaponFired`/`ChargingStarted`/`ChargeCompleted`/`ProjectileImpact` events, dispatches per-module particles
- [ ] X2. Per-Payload VFX:
  - Ballistic: muzzle flash + tracer + impact spark/decal
  - Laser: charged glow at muzzle (build-up) + beam projectile + impact burn + heat haze
  - Rocket: missile launch + smoke trail + AoE explosion (existing GrenadeSystem infra)
  - Foam: viscous projectile + splat decal + slow zone visualization
- [ ] X3. Per-Delivery VFX:
  - Rotary: barrel spinup animation + heat haze accumulation
  - Swarm: volley flash sequence (1 fire = 5 muzzle flashes у series)
  - Scatter: cone burst pattern (multi-pellet visualization)
- [ ] X4. Per-Exotic VFX:
  - Ricochet sparks
  - Split on Impact: visual fork at hit point
  - Boomerang trail
- [ ] X5. SFX library — fire variants per archetype, charge sound (Laser), spin sound (Rotary), reload variations
- [ ] X6. Hit feedback — screen shake, hit pause, damage number animation polish (extending existing `DamageNumberOverlay`)

### Exit criteria
- ✅ Player closing eyes може упізнати archetype по sound (Laser hum vs Ballistic crack vs Rocket whoosh)
- ✅ Кожна exotic mod має recognizable visual signature
- ✅ Hit feedback відчувається punchy без screen-saver-level over-effect

### Dependencies
Tier 3-5 complete (need content to design visual language for). Tier 8 не блокучий, але в parallel дає synergy (mesh + VFX часто розробляються разом).

### Parallel tracks
Most of цього tier — artist + sound designer work. Programmer-side: hooks plumbing (~3-5 days).

---

## Tier 10 — Weapon Feel Polish

### Goal
Кожна збірка стріляє так, щоб гравець хотів стріляти ще раз. Це **iterative playtest loop**, не "напиши код".

### Природа роботи
На відміну від попередніх tiers — це не feature delivery, а **тюнінг знаючи кожен number**. Industry стандарт для AAA shooter: 6+ months dedicated полишнгу. Для нашого scope — concentrated milestone у кінці перед public release.

### Work items (qualitative)
- [ ] Fire interval per archetype — feels snappy / punchy / heavy?
- [ ] Recoil patterns (per delivery): kick magnitude + direction + recovery curve
- [ ] Charge-up timing (Laser) — 1.0s OK чи треба 0.7-1.3?
- [ ] Reload pace per archetype (Pistol fast, Shotgun slow shell-by-shell?)
- [ ] Hit feedback timing — screen shake duration, hit pause length, damage number flight
- [ ] Sound design integration — every audio cue tuned to gameplay moment
- [ ] Animation polish — chamber/eject timing, idle pose, transition smoothness
- [ ] Damage curves vs armor (DPS targets per archetype role)
- [ ] Comprehensive balance pass — no archetype дominantні / dead-on-arrival

### Exit criteria
- ✅ Playtest sessions кажуть "feels good" не "функціонально"
- ✅ No archetype дominance (telemetry / balance heatmap)
- ✅ All audio + animation cues feel synced до moment-of-impact

### Dependencies
Tier 8 + Tier 9 complete (need real visuals/audio to tune feel against). Tier 5 (exotic) at minimum — щоб полирувати full archetype set, не лише core.

### Process
- Iterative playtest weeks (1-week sprints зі specific focus: "this week — Laser feel")
- Telemetry-driven (TTK heatmaps, fire-rate logs, kill streak distributions)
- Може running у parallel з content/feature work earlier — collect data continuously, dedicate sprints later.

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
