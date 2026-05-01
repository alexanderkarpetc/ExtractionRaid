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
| 6 | Loot / Inventory integration (modules-as-items, loot drops, dev grant) | ✅ done (Waves A/B/D/E/F); G7 deferred sine die |
| 8 | 3D Modular Visualization | ✅ done (2026-04-30) — Waves A-E; Wave F deferred (UI prereq) |
| 8.x | Tier 8 follow-ups (muzzle alignment, reload/equip motion, Mecanim cleanup, socket tuning) | ⏳ NEXT (polish track) |
| 4a | **Bot weapon migration ONLY** (split from Tier 4) — closes Cluster B legacy debt | ⏳ planned (polish track) |
| 9 | VFX / SFX Language (scope-limited to current 2×3 archetypes) | ⏳ planned (polish track) |
| 10 | Weapon Feel Polish (iterative playtest tuning) | ⏳ planned (polish track) |
| 3 | Content expansion (Foam, Rocket, Rotary, Swarm) | ⏸ deferred sine die — engage коли polish converges |
| 4b | Rarity values + Slot Compatibility + banned combos (split from Tier 4) | ⏸ deferred sine die |
| 5 | Exotic Mods | ⏸ deferred sine die |
| 8 Wave F | Backpack composite icons | ⏸ deferred sine die — UI prereq |
| ~~7~~ | ~~Polish (Art/VFX, UX, balance)~~ — **deprecated, split into 8/9/10** | — |

**Tier numbers = stable IDs** (для посилань у коді/тестах/інших доках). **Execution order — нижче, ≠ tier number order.**

**Відкладено поза scope:** Fist Delivery (melee system — окремий проект), Typed Attachments.

---

## Execution sequence (revised 2026-05-01 — polish-first)

**Strategic pivot:** після того як Tier 6+8 закрили "raid → loot → build" loop + visible 2-module composition, фокус переходить на **polish існуючих 6 archetypes** замість content expansion. Tier 3/5 (нові payloads/deliveries/exotics) defer'аться до моменту, коли поточна гра feels great. Гасло: "make existing content feel amazing before adding more."

```
✅ FOUNDATION
   0a → 0b → 1 → 2 → UX Pass 1 → 6 → 8

🎯 POLISH TRACK (next, in order)

   1. Tier 8.x follow-ups
      • Muzzle alignment for symmetric meshes
      • Reload/Equip/Unequip procedural motion
      • Mecanim controller stale clip cleanup or replacement
      • Per-prefab PayloadMount/MuzzlePoint tuning

   2. Tier 4a — bot weapon migration (split from Tier 4)
      • Move BotSpawnSystem onto WeaponAssemblySystem.TryAssemble
      • Per-bot WeaponConfiguration on BotTypeConfig
      • Retire all Cluster B compat: WeaponItemFactory.DefaultConfigFor / SpawnItem,
        LootSystem.MapWeaponPrefab*, ItemDefinition ["Rifle"]/["Pistol"] entries,
        [Obsolete] WeaponPrefabId field, Ammo_Pistol family registry entries.
      • Closes ALL legacy debt у one tier.

   3. Tier 9 — VFX/SFX language (scope: current 2×3 archetypes)
      • Per-Payload VFX: Ballistic muzzle/tracer/impact, Laser charge glow/beam/burn
      • Per-Delivery feel: Auto cadence, Single emphatic, Scatter cone pellet pattern
      • SFX library: fire variants, charge sound, reload variations
      • Hit feedback polish: screen shake, hit pause, damage number animation

   4. Tier 10 — Weapon Feel iterative tuning
      • Recoil curves per archetype, charge timing, reload pace
      • Damage curves vs armor balance
      • Telemetry-driven playtest sprints (no archetype dominance / dead-on-arrival)

⏸ DEFERRED SINE DIE
   • Tier 3 — content expansion (Foam/Rocket/Rotary/Swarm)
   • Tier 5 — exotic mods
   • Tier 4b — rarity tier values + slot compat + banned combos
   • Tier 8 Wave F — backpack composite icons (UI prereq)
   • Tier 6 G7 — initial loadout polish (orphan, не блокує)
```

**Чому така послідовність (revised):**

1. **Tier 8.x follow-ups first** — закриває visible polish gap від symmetric pivot (muzzle position approximate, animator clips stale, sockets placed on око). Найшвидший visible win — make 6 archetypes feel coherent після Wave B-E foundation.

2. **Tier 4a — bot migration** — closes ALL legacy compat debt (Cluster A retired player-facing references; Cluster B awaits bot migration). Bot loot stops dropping `Rifle`/`Pistol` items — гра стає coherent. Reusable foundation for future bot content (rarity per bot type у Tier 4b).

3. **Tier 9 — VFX/SFX** — без content expansion (Tier 3) ми scope'имо це до існуючих 2×3. Original argument "need content to design visual language for" — applies only якщо ми робимо content тіерthem параллельно. Як standalone polish — current archetypes провідно distinguish'аються через VFX language (Ballistic vs Laser).

4. **Tier 10 — Feel polish** — iterative playtest sprints. Бере real visuals/audio від Tier 9, реальну кохерентну гру від Tier 4a, додає balance + tuning. Це **не один sprint** а ongoing loop.

**Re-engage content tracks (Tier 3/4b/5/Wave F) коли:**
- Polish pass converged — playtest sessions кажуть "feels great", not "функціонально"
- Telemetry shows balanced 2×3 matrix (no archetype dominance)
- UI track оновлений (для Wave F)
- Decision на content scope reset — based on what we learn from polishing 2×3

**Parallel tracks possible:** Tier 9 (artist + sound designer) і Tier 10 (designer balance) можуть йти паралельно з програмер track Tier 8.x → 4a. Якщо artist/sound designer відсутній — programmer-only path is 8.x → 4a → (block on art for 9 → 10).

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

> **Status (2026-05-01): ⏸ DEFERRED SINE DIE.** Polish-first pivot — engage коли current 2×3 archetypes feel great (Tier 8.x → 4a → 9 → 10 converged). Reasoning: adding more content before existing feels polished risks scope spread + dilutes feedback-loop signal.

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

## Tier 4 — split into 4a (bot migration) + 4b (rarity + slots)

> **Split 2026-05-01:** original Tier 4 об'єднував 3 різні теми (rarity values + slot compat + bot weapon migration). Bot migration — це **legacy cleanup** (closes Cluster B compat layer, makes loot coherent), не зв'язана з content design. Rarity + slots — content progression layer.
>
> **Tier 4a** (bot migration only) — у polish track, scoped above після Tier 8.x. Detailed section: [Tier 4a — Bot Weapon Migration](#tier-4a--bot-weapon-migration-split-from-tier-4).
>
> **Tier 4b** (rarity values + slot compat) — deferred sine die. Scope below preserved для майбутнього engagement.

### Tier 4b Goal (deferred)
Механіка rarity (5 тірів з кращими статами) + явні правила сумісності модулів замість "все з усім".

### Tier 4b work items (high-level, deferred)
- [ ] A3. Rarity data model (реалізація — structure вже затверджена в Tier 0)
- [ ] E3. Rarity Scaling System (застосування множників до stats)
- [ ] A4. Slot structure data model
- [ ] E2. Slot Compatibility Rules engine
- [ ] F5. UI feedback на заборонену комбінацію
- [ ] **G5★ Cross-stack drag bridge** (deferred з Tier 6 Wave C) — uGUI ↔ UI Toolkit drag для distinguishing module instances by rarity

### Tier 4b architectural questions (still deferred)
- Q4 повністю: де живе правило сумісності (в модулі / в слоті / окремий rules engine)?
- Rarity множники: глобальна таблиця vs per-module? Конкретні числа?
- Banned combinations matrix — конкретний список?

### Tier 4b exit criteria
- ✅ Rarity візуально відрізняється (модулі мають tier) і впливає на stats
- ✅ Неможливо зібрати заборонену комбінацію
- ✅ Slot structure відображена в UI

### Tier 4b dependencies (when re-engaged)
Polish loop converged (Tier 8.x → 4a → 9 → 10 done). Optionally Tier 3 для testing rarity на ширшому content matrix.

---

## Tier 5 — Exotic Mods

> **Status (2026-05-01): ⏸ DEFERRED SINE DIE.** Same rationale as Tier 3 — polish current 2×3 first.

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

- [x] **G1.** Core modules як `ItemState` — 5 ItemDefinition entries у `ItemDefinition.BuildRegistry` (each: id, displayName, slot=Backpack, stackable=false) ✅
- [x] **G2.** Module spawning у loot tables — added 5 modules to `ContainerConstants.RandomLootBox.PossibleDrops` + new `ModuleCache` ContainerType (1-2 drops, module-only pool). Bot drops out of scope (Tier 4). Scene placement of `ModuleCache` instances — manual user task. ✅ (2026-05-01)
- [x] **G3.** DevCheats "Spawn Module" — dropdown by type у `DevCheatsWindow.cs`, places item у player Backpack (для playtest без рейду) ✅
- [x] **G4.** Builder palette filter — `WeaponBuilderPresenter.IsModuleAvailable(id)` (read inventory), `ModuleCardElement.SetAvailable` adds `wb-card-unavailable` class for grayed-out look ✅
- [ ] ~~**G5★.** Cross-stack drag bridge.~~ **DEFERRED → Tier 4** (2026-04-28). Palette уже drag-source; drag-from-inventory дублював би функціонал. Unique value (instance disambiguation when 2× BallisticRound різних rarity) виникає тільки у Tier 4. Wave D (G6 build cost) + Wave E (G4 palette filter) разом дають complete inventory loop без cross-stack drag.
- [x] **G6.** Build consumes modules — `WeaponBuilderPresenter.TryBuild` removes 1×payload + 1×delivery items from backpack on success ✅
- [ ] **G7.** ~~Initial player loadout~~ — **DEFERRED "на потім" (2026-05-01)**. Reason: current onboarding не критичний — DevCheats "Spawn All Modules" + Wave F loot економіки покриває testing/playtest. Real "fresh save UX" доцільно полірувати разом з general onboarding pass (Tier 10 feel polish або earlier dedicated UX iteration).
- [x] **G8.** ~~Inventory slot type для модулів~~ — **resolved**: модулі лежать у звичайному Backpack (decision 2026-04-28).
- [x] **G9.** Open uGUI inventory canvas alongside Builder ✅
- [x] **G10.** Layout coordination ✅

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
| **D. Build cost** ✅ DONE (audited 2026-04-30) | G6 | Closes build cycle — Build реально "коштує" модулі | ✅ TryBuild removes 1×payload + 1×delivery from backpack on success; CanBuild gates on backpack presence; DisabledReason explains "No payload module / No delivery module у backpack". |
| **E. UX completeness** ✅ DONE (audited 2026-04-30) | G4 | Visual feedback "що ти можеш зібрати" | ✅ `IsModuleAvailable(id)` + `ModuleCardElement.SetAvailable(bool)` wired у `WeaponBuilderWindow`; `wb-card-unavailable` USS class з hover variant. |
| **F. Economy** ✅ DONE 2026-05-01 (code; scene placement manual) | G2 | In-game inventory loop | ✅ RandomLootBox + new ModuleCache ContainerType seeded; LootSystem.CreateContainer for ModuleCache produces only module items; tested. User places ModuleCache spawn points у raid scenes manually. |
| ~~**G. Initial state**~~ | ~~G7~~ | **DEFERRED 2026-05-01** — DevCheats + loot economy cover playtest needs. Re-engage as part of broader onboarding/UX pass. | — |

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

> **Execution:** ✅ DONE (2026-04-30). Waves A-E shipped end-to-end symmetric two-module visualization (delivery body + payload barrel from PolygonApocalypse modular parts). Wave F (backpack composite icons) — **deferred sine die**, blocked on UI prereq (current uGUI `InventorySlotView`/`LootPopupView` не підтримує composite icons). Re-engage Wave F коли inventory rendering layer оновлений (новий UI Toolkit track або redesign). Не блокує Tier 8 closure для visible-differentiation goal.

### Goal
Player візуально розрізняє composition. Замість одного `Weapon_Pistol` prefab'а для всіх "Pistol"-form builds — modular weapon view де payload mesh + delivery mesh runtime assembly. 4 payload meshes + 5 delivery meshes покривають усі 4×5=20 archetypes без геометричного scope.

### Current state of 3D pipeline (для context)

```
WeaponConfiguration → WeaponSyncSystem.ResolveWeaponPrefab(itemDef, deliveryDef)
  ├─ ItemDefinition.WeaponPrefabId  (legacy override)
  └─ DeliveryDef.FormFactor switch:
       "Pistol"/"Rifle" → "Weapon_Pistol"/"Weapon_Rifle"
       _                → "Weapon_Rifle" (Shotgun fallback ← Tier 0b gap)
  → WeaponEntityState.PrefabId
  → CharacterBody.SwapWeaponModel(prefabId)
  → Resources.Load("Prefabs/Weapons/" + prefabId)
  → instantiate під _weaponPivot
  → WeaponView component → MuzzlePoint, Animator
```

3 prefabs (Pistol/Rifle/Shotgun), Payload **взагалі не впливає на візуал**, ні Payload ні Delivery SO не мають mesh-related полів.

### Architectural decisions (resolved 2026-04-29)

- **V-Q1.** Pre-built per-archetype prefabs (A) vs modular runtime composition (B)? → **B**. 4×5=20 prefabs × Exotic Mods (×5) = scope explosion. Modular reflects composition design intent.
- **V-Q2.** Animator ownership? → **Delivery owns Animator + Fire/Reload/Equip triggers** (per-mechanism: pistol slide, rifle bolt, rotary spinup). Payload — purely visual; може мати own optional animator для passive flair (laser glow), не interferes з gameplay triggers.
- **V-Q3.** MuzzlePoint ownership? → **Delivery** (barrel exit position). Payload може приплюсувати own emitter prefab spawned at MuzzlePoint runtime (Tier 9 VFX scope).
- **V-Q4.** Mesh asset reference на SO? → **Direct `GameObject` field** (typesafe, Inspector-visible). Project не використовує Addressables.
- **V-Q5.** `ItemDefinition.WeaponPrefabId` legacy override? → **Mark deprecated у Tier 8, видаляється у Tier 4** (разом з bot weapon migration що теж сидить на legacy path).
- **V-Q6.** Payload attachment socket? → **Explicit `Transform` reference на Delivery prefab** (set у Inspector). Find-by-name (як `RightHandGrip`) — fragile.
- **V-Q7.** Backpack item icons? → **Окремий Wave F**, виконується після visual pipeline. Composite — `Sprite` поля на обох SO + `InventorySlotView` рендерить 2 sub-images.

### Execution waves

| Wave | Items | Why this order | Verifiable end state |
|---|---|---|---|
| **A. Pipeline refactor (no art)** ⭐ NEXT | `WeaponPrefab` GameObject field на `DeliveryCoreDefinition`; `CharacterBody.SwapWeaponModel(string)` → `SwapWeaponModel(WeaponEntityState)`; resolver reads `delivery.WeaponPrefab` замість `prefabId` string switch; existing `Weapon_Pistol/Rifle/Shotgun` приписуються до SingleAction/Auto/Scatter SOs. | Architectural pivot first. Без цього усе art-делання базується на string-id resolver який ми викидаємо. | Equip Ballistic Pistol → виглядає як зараз (cube). Tests зелені. String-switch resolver видалений. |
| **B. Payload attachment proof (1 archetype)** | `PayloadCoreDefinition` отримує `GameObject AttachmentPrefab`. `Module_Delivery_Rifle` додає `Transform PayloadMount` socket. `Module_Payload_BallisticBarrel` (primitive shape). Composer instantiate'ить delivery + child-instantiate'ить payload prefab у socket. | Smallest reproducible end-to-end з реальною композицією. Primitive shapes — без art-залежності. | Build Ballistic+Rifle → ствол primitive прикріплений до сокета. Equip/shoot/reload працює. |
| **C. Cover existing 2×3 archetypes** | 2 payload prefabs (Ballistic, Laser — Tier 1-2 implemented set), 3 delivery prefabs (Pistol, Rifle, Scatter) з sockets. **Shotgun fallback видалений** — Scatter має власний mesh. | 6 archetypes — поточний content scope. Player візуально розрізняє composition. Closes Tier 0b memory gap (Shotgun fallback). | Усі 6 archetypes візуально distinct. Shotgun fallback memory gap closed. |
| **D. Animator integration** | Verify Fire/Reload/Equip triggers працюють незалежно від payload prefab. Payload може мати own animator (visual flair, e.g., laser pulse) без interference з gameplay triggers. | Animation pipeline не повинен зламатися при composition. | Shoot Laser+Pistol → fire trigger animates pistol slide; payload glow окремо. |
| **E. Forward-compat assets** | Editor utility `Tools → Weapon Builder → Create Module Prefabs` — idempotent — створює primitive prefabs для всіх payloads/deliveries у `Resources/Prefabs/Modules/`. Artist drop-in path: replace primitive з real mesh, не торкаючи коду. | Tier 3 content (Foam/Rocket/Rotary/Swarm) має drop-in path. | Add new payload SO → run utility → primitive prefab створюється + wired. |
| **F. Backpack icons (V6)** | `Sprite Icon` поля на Payload + Delivery SO. `InventorySlotView` рендерить composite icon (2 sub-images) для weapon items. **Deferred до завершення A-E** (decision 2026-04-29) — visual pipeline пріоритетний. | Inventory readability — без цього будь-яка зброя у backpack виглядає однаково. Може йти у parallel якщо UI engineer вільний після Wave C. | Backpack item shows composite (payload + delivery sub-icons). |

### Exit criteria
- ✅ Build Ballistic+Pistol vs Laser+Pistol — visually distinct у hand
- ✅ Equip swap показує correct mesh
- ✅ Adding нового payload/delivery (Tier 3) = drop in 1 mesh, no system changes
- ✅ Shotgun fallback видалений (closes Tier 0b memory gap)
- ✅ Inventory icons reflect archetype (Wave F deliverable)
- ✅ `ItemDefinition.WeaponPrefabId` deprecated (повне видалення у Tier 4 з bot migration)

### Dependencies
Tier 6 Wave B complete (modules-as-items). Wave A може стартувати незалежно від решти Tier 6 waves.

### Parallel tracks
Art (real meshes + rigging) може йти паралельно з Wave A-D якщо є artist. Wave F (UI icons) може стартувати паралельно з B-E якщо є UI engineer.

### Estimated effort
~15-20h programmer-side. Waves A+D — програмер (~6-8h). Waves B+C — програмер + primitive art (~4-6h). Wave E — engineer (~2h). Wave F — UI engineer (~3-4h).

---

## Tier 8.x — Tier 8 Follow-Ups (visual coherence pass)

> **Execution:** ⭐ NEXT (polish track). Formalized 2026-05-01 from Tier 8 closeout follow-ups list. Closes visible polish gap від Wave B/C symmetric pivot.

### Goal
Make 6 archetypes (Pistol/Rifle/Shotgun × Ballistic/Laser) feel coherent. Tier 8 landed pipeline + composition; Tier 8.x закриває visible gaps що залишилися (muzzle position approximate, anim paths stale, sockets placed на око).

### Work items

- [ ] **8x.1 Muzzle alignment for symmetric meshes.** Зараз `MuzzlePoint` — на delivery prefab (V-Q3), approximate position relative to barrel tip. З symmetric model barrel живе на payload → each barrel has different length → MuzzlePoint visual mismatch. Two paths:
  - **(a)** Move MuzzlePoint у payload prefab; `WeaponView.MuzzlePoint` resolves dynamically post-`AttachPayload` (lookup child by name або setter from AttachPayload).
  - **(b)** Keep on delivery; per-prefab manual alignment (existing). Acceptable якщо barrel pool small.
  - **Recommend (a)** — proper architectural fix; lays groundwork for Tier 9 (VFX spawn at correct muzzle).

- [ ] **8x.2 Reload/Equip/Unequip procedural motion.** Wave D landed Fire kick only. Other animation triggers fire silently на stale Mecanim clips. Procedural patterns:
  - Reload — body lowers (-Y) over `ReloadTime * 0.4`, holds, raises back. Optional magazine swap visible if `Magazine` socket exposed.
  - Equip — body rises into position from below (+Y kick) over `EquipTime`.
  - Unequip — body lowers off-screen.
  - Reuse `_deliveryBody` reference + similar ease-out pattern.

- [ ] **8x.3 Mecanim controller stale clip cleanup.** 3 weapon prefabs carry `Weapon_Pistol_Override` / `Weapon_Rifle_Override` / `Weapon_Shotgun_Override` controllers з 5 stale clips animating non-existent paths. Either:
  - Strip controllers (set Animator.controller = null) — clean.
  - Or recreate clips animating new `DeliveryBody` sub-children — Mecanim parity.
  - **Recommend strip** — procedural recoil уже covers visible feedback; cleaning removes dead asset weight.

- [ ] **8x.4 Per-prefab PayloadMount/MuzzlePoint tuning.** Pistol/Shotgun PayloadMount positioned `(0, 0.03, 0.18)`/`(0, 0.03, 0.40)` на око. Manual Inspector pass — verify barrel attaches properly when equipped. (Rifle уже tuned by user manually.)

### Exit criteria
- ✅ Bullets spawn at visible barrel tip across усіх 6 archetypes
- ✅ Reload triggers visible motion (body lowers/raises)
- ✅ Equip/Unequip — visible body intro/outro
- ✅ No stale animator clips floating у prefabs
- ✅ All 6 archetypes have tuned socket positions

### Dependencies
Tier 8 complete (already done).

### Estimated effort
~4-6h programmer-side. 8x.1 — biggest piece (~1.5h). 8x.2 — ~1.5h. 8x.3 — 30min. 8x.4 — 1h Inspector work.

---

## Tier 4a — Bot Weapon Migration (split from Tier 4)

> **Execution:** Polish track #2. Split з оригінального Tier 4 на 2026-05-01 щоб не блокувати legacy cleanup на content/balance design (Tier 4b — rarity values, slot compat — defers sine die).

### Goal
Bot weapons go through `WeaponAssemblySystem.TryAssemble` like player weapons. Closes ALL Cluster B legacy debt — після Tier 4a компат-шару немає, registry clean, loot drops coherent.

### Work items

- [ ] **B1.** Add `WeaponConfiguration` field to `BotConstants.BotTypeConfig`. Per bot type — explicit Payload+Delivery composition:
  - Scav, Target* — `Ballistic + Auto` (Common rarity)
  - PMC, Boss — TBD (could vary з future rarity work)
- [ ] **B2.** `BotSpawnSystem` reads `BotTypeConfig.WeaponConfiguration` → calls `WeaponAssemblySystem.TryAssemble` → builds `WeaponEntityState` via assembly pipeline (parity з player flow). Removes hardcoded stat field population from `BotConstants` body.
- [ ] **B3.** Update `LootSystem.CreateLootable` для bots:
  - Replace `WeaponItemFactory.SpawnItem(MapWeaponPrefabToDefinition(...))` → `ItemState.CreateWeapon("Weapon", botConfig.WeaponConfiguration)`
  - Drop `MapWeaponPrefabToDefinition` + `MapWeaponPrefabToAmmo` static methods entirely
  - Bot loot creates Builder weapon — same as player drops/creates

### Removals after B1-B3 (cleanup)

- [ ] Delete `WeaponItemFactory.DefaultConfigFor` + `IsKnownWeaponDefinition` + `SpawnItem`
- [ ] Delete `WeaponItemFactory` entirely if no other callers
- [ ] Delete `ItemDefinition.["Rifle"]` and `["Pistol"]` registry entries
- [ ] Delete `[Obsolete] ItemDefinition.WeaponPrefabId` field (+ obsolete pragma на other side)
- [ ] Delete `Ammo_Pistol` / `Ammo_Pistol_AP` / `Ammo_Pistol_HP` registry entries (no payload uses Pistol-caliber ammo. If Tier 3 splits Ballistic into per-caliber payloads later, re-add then.)
- [ ] Update `WeaponSyncSystemIntegrationTests.cs` — 3 sites що тестують legacy "Rifle"/"Pistol" compat — переписати на pure Builder pipeline (replace `WeaponItemFactory.SpawnItem("Rifle")` → `ItemState.CreateWeapon("Weapon", config)`)
- [ ] Update `EditModeTestsUtils.cs:114, 169` + `AmmoSystemTests` — drop Ammo_Pistol references якщо registry entry deleted
- [ ] Update `WeaponSyncSystem.BuildWeaponForItem` — remove `[Obsolete]` fallback path (legacy WeaponPrefabId resolution)

### Exit criteria
- ✅ Bot weapons go through assembly pipeline (composition + cached Stats)
- ✅ Bot loot drops Builder weapons (`"Weapon"` ItemState з WeaponConfiguration), не legacy "Rifle"/"Pistol"
- ✅ Zero references to `Rifle`/`Pistol` ItemDefinition entries у codebase
- ✅ `WeaponItemFactory` deleted (or trivial остаток, single use)
- ✅ Cluster B Legacy debt fully closed
- ✅ All 434+ tests зелені

### Dependencies
Tier 8 complete. Cluster A retired (already done). Tier 4a — only legacy cleanup; no rarity/balance design needed.

### Out of scope (now Tier 4b)
- Per-bot rarity tier (Scav=Common, Boss=Epic) — needs `StatsByTier` filling-in, balance pass
- Slot compatibility rules (banned combos)
- Cross-stack drag bridge (G5★ from Tier 6 Wave C)

### Estimated effort
~6-8h programmer. B1+B2 — ~3h. B3 + cleanup — ~3h. Tests — ~2h.

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

### Dependencies (revised 2026-05-01)
Tier 8 done (✅). Tier 8.x follow-ups + Tier 4a recommended before Tier 9 — закривають visible coherence + bot loot coherence. **Tier 3/5 NOT prerequisite anymore** — Tier 9 scope обмежено до current 2×3 archetypes (Ballistic/Laser × Pistol/Rifle/Shotgun). Original "need content to design visual language for" applied if doing both у parallel; standalone polish для existing content not blocked.

**Scope-limited work items для current 2×3:**
- X1 (events plumbing) — full
- X2 (per-Payload VFX) — Ballistic + Laser only (Foam/Rocket — defer)
- X3 (per-Delivery VFX) — Single/Auto/Scatter only (Rotary/Swarm — defer)
- X4 (per-Exotic VFX) — DEFER entirely (Tier 5 deferred)
- X5 (SFX library) — current 2×3 + general reload/charge
- X6 (hit feedback polish) — full

### Parallel tracks
Most of цього tier — artist + sound designer work. Programmer-side: hooks plumbing (~3-5 days).

---

## Tier 10 — Weapon Feel Polish

### Goal
Кожна збірка стріляє так, щоб гравець хотів стріляти ще раз. Це **iterative playtest loop**, не "напиши код".

### Природа роботи
На відміну від попередніх tiers — це не feature delivery, а **тюнінг знаючи кожен number**. Industry стандарт для AAA shooter: 6+ months dedicated полишнгу. Для нашого scope — concentrated milestone у кінці перед public release.

### Scope (revised 2026-05-01)
Iterative tuning over current **2×3 archetypes** (6 weapons). Re-scope коли content tracks (Tier 3/5) re-engage'аться. Goal: гра feels great на існуючому content, **не на гіпотетичному 4×5×exotics matrix.**

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
