# Weapon Builder — Status

> **Living doc.** Трекає відкриті питання, прийняті рішення і блокери по ходу роботи над Weapon Builder. Оновлюється часто.

---

## Current phase

**Pre-implementation / Design consolidation.**

Дизайн зафіксовано у v0.7 ([design.md](../design.md)). Архітектурні питання відкриті ([architecture.md](../architecture.md)). Імплементація ще не стартувала.

---

## Open questions

### Design
- [ ] **Slot structure — конкретні правила.** Скільки слотів, якого типу, яка сумісність? `design.md` фіксує принцип, але не правила. (Tier 4)
- [ ] **Banned combinations matrix.** Які P×D комбінації явно заборонені дизайном? (напр. чи можливе Adhesive Foam + Rotary?) (Tier 4)
- [ ] **Exotic Mod × Core сумісність.** Кожен Exotic працює з кожним Payload/Delivery, чи є обмеження? (Tier 5)
- [ ] **Rarity — конкретні множники.** На скільки відсотків Uncommon кращий за Common? (Tier 4 — заповнення `StatsByTier`)
- [ ] ~~Fist Delivery — single behavior чи кілька?~~ — виключено зі scope Weapon Builder
- [ ] **Laser Charge — поведінка зарядки.** Hold-to-charge з release? Auto-release при повному заряді? Overcharge можливий? (Tier 2)
- [ ] **Payload secondary effects — обов'язкові чи опційні?** (Tier 2-3 — на кожен payload)

### Architecture — Tier 0 блокуючі питання
Виявлені комплексним ревʼю 2026-04-19. Треба закрити ДО старту коду Tier 0.

- [x] ~~**D1.** Склад `WeaponStats` блоку~~ ✅ 8 Payload + 13 Delivery, без overlap
- [x] ~~**D2.** Stats structure для різнорідних Payloads~~ ✅ abstract base + typed subclass'и
- [x] ~~**D3.** ScriptableObject vs plain data для `*CoreDefinition`~~ ✅ SO (abstract base + subclass'и для Payload)
- [x] ~~**D4.** Value semantics: readonly struct vs class для `*CoreInstance`~~ ✅ `[Serializable] readonly struct`
- [x] ~~D5. ExoticMod без rarity — явно зафіксовано~~ ✅

### Architecture — Tier 1 блокуючі
- [ ] **D6.** Re-assembly triggers (коли запускається composition)
- [ ] **D7.** Invalid configuration handling (fallback strategy)
- [ ] **D8.** Archetype label system (lookup / template / hybrid)

### Architecture — Tier 3+ (високорівневі, не закриті)
Великі питання з [architecture.md](../architecture.md):
- [ ] **Q3.** Payload Core abstraction (IPayloadBehavior чи data-only)
- [ ] **Q4.** Slot structure / module compatibility (Tier 4)
- [ ] **Q5.** Exotic Mod hooks (event-driven vs strategies) (Tier 5)

### Architecture — housekeeping
- [ ] **D9.** RaidContext / ports integration для `*CoreDefinition` registry
- [ ] **D10.** Raid State Debugger update (CLAUDE.md §5.7)
- [ ] **D11.** DevCheats integration (rarity multipliers, spin-up times)
- [ ] **D12.** Docs sync `.cursor/rules/weapon-builder*.mdc` — після завершення планування

### Production
- [ ] **UI збірки на базі — mockups / wireframes.** Поки немає.
- [ ] **VFX / SFX scope per module.** Кожен Payload/Delivery/Exotic потребує свого feel — хто і коли це робить?
- [ ] **Tier 0 size estimation.** Чи ділити на 0a (data model) + 0b (migration)? Див. R1 у [architecture.md](../architecture.md#open-risks).

---

## Decisions log

Фіксуємо прийняті рішення з контекстом — щоб через місяць не переобговорювати те саме.

### 2026-04-17 — v0.7 approved, Hidden Budget removed
**Було:** Hidden Budget як невидимий ліміт проти "все найкраще одразу".
**Стало:** Slot structure / module compatibility — явні структурні обмеження через слоти і правила сумісності.
**Причина:** Явні правила чесніші для гравця і простіші для імплементації, ніж балансування невидимої budget-математики.

### 2026-04-17 — Doc structure: folder per feature
**Рішення:** Weapon Builder живе у `docs/ai/weapon-builder/` з окремими файлами під дизайн, архітектуру, план.
**Причина:** Фіча занадто велика для одного файлу. Розділення концептуальних (живуть довго) і планових (живуть час реалізації) доків.

### 2026-04-20 — D6 / D7 / D8 resolved (Tier 1 blockers closed)

**D6 — Re-assembly triggers:** Варіант B.
- On equip (auto) + on explicit "Apply" button (manual, Tier 4 UI)
- `WeaponAssemblySystem.Assemble` — окрема system, викликається з обох місць
- Runtime state persistence: `AmmoInMagazine` у `WeaponConfiguration` (persistent), решта runtime полів — скидаються при re-assembly
- Tier 0b/1 реалізує on-equip path only; Apply button — Tier 4

**D7 — Invalid configuration handling:** Варіант C (ghost weapon), strict — без auto-repair.
- `WeaponAssemblySystem.TryAssemble(WeaponConfiguration, out WeaponEntityState) → bool`
- Будь-який missing definition (Payload/Delivery/Exotic) → `false`, log + `WeaponAssemblyFailed` event
- **Без auto-repair Exotic** — strict C: broken exotic ламає всю збірку, гравець явно виправляє в Builder
- Invalid item лишається в inventory як ghost (не видаляється), equip fails clearly, player unarmed
- Tier 0b/1: немає broken-UI; Tier 4 — ⚠️ icon + tooltip + Salvage/Repair

**D8 — Archetype labels:** Варіант A (pure template, no baseline strip).
- `PayloadCoreDefinition.DisplayName` + `DeliveryCoreDefinition.FormFactor` — нові поля SO
- Template: `"{payload.DisplayName} {delivery.FormFactor}"`
- Examples: "Ballistic Pistol", "Ballistic Rifle", "Laser Pistol", "Foam Shotgun", "Rocket Launcher"
- Legacy Rifle/Pistol після міграції → "Ballistic Rifle"/"Ballistic Pistol"
- Exotic NOT у label (окремий UI element)
- Override-table — deferred to Tier 5

**Наслідки для Tier 0b:**
- +2 fields на SOs (DisplayName, FormFactor) — editor stub script треба оновити
- +1 system class `WeaponAssemblySystem` з `TryAssemble`
- +1 helper `WeaponArchetypeLabel.Compose`
- +1 event type `WeaponAssemblyFailed` у RaidEventBuffer

**Див.:** [architecture.md §D6, D7, D8](../architecture.md)

### 2026-04-23 — Tier 1 complete ✅
**Vertical slice landed.** Гравець може:
- Підійти до Workbench у Hideout → press E → Builder opens
- Select Payload + Delivery у 2 dropdowns → live preview stats + archetype label
- Click Build → new ItemState (з WeaponConfiguration) у backpack
- Equip → стріляє як pistol/rifle згідно з Delivery FormFactor
- Alt route: DevCheats "Toggle Weapon Builder" button — відкриває Builder з будь-де

**Зроблено:**
- Cluster A — Presenter + state, 14 unit tests
- Cluster B — UI Toolkit modal (UXML + USS + runtime Window), 2x upsized layout
- Cluster C — Workbench scene interactable (InteractPressed input, proximity prompt)
- Cluster D — DevCheats toggle button
- Cluster E — AppBootstrap integration + end-to-end tests (5)

**Архітектурно:**
- Presenter — pure C#, testable без Unity
- UI Toolkit runtime pattern (UIDocument + PanelSettings bootstrap) — slope для future UI
- Generic "Weapon" ItemDefinition — identity у WeaponConfiguration, prefab derived з Delivery FormFactor
- `PlayerEntityState.IsWeaponBuilderOpen` auto-gates gameplay input через existing `IsInMenu`

**Test coverage after Tier 1:** ~75 total зелених (Tier 0a 24 + Tier 0b 29 + Tier 1 22).

**Unlocked для Tier 2:**
- Додати Laser Charge payload + charge-up state machine
- Додати Auto Delivery handler у ShootingSystem
- Додати Scatter Delivery handler
- 6 working архетипів (2 payloads × 3 deliveries)

### 2026-04-22 — D9-D14 resolved (Tier 1 design decisions)

**D9 — UI location:** окремий modal screen, callable з будь-якого контексту (hideout + raid). Primary trigger — physical workbench у hideout scene. Secondary — DevCheats shortcut для debug/raid.

**D10 — Module supply:** infinite, all-unlocked у Tier 1. Loot integration — Tier 6.

**D11 — UI layout:** single screen з 3 dropdowns (Payload/Delivery/Exotic), live preview (stats + archetype label), Build/Cancel buttons.

**D12 — Build result:** новий ItemState у перший free backpack slot, `AmmoInMagazine = MagazineSize`. Existing items не зачіпаються. `DefinitionId = "Weapon"` (generic — identity у WeaponConfiguration).

**D13 — Entry point:** physical Workbench scene object у hideout + Interact key. `WorkbenchView` MonoBehaviour + prompt UI + DevCheats global hotkey для dev testing.

**D14 — Tier 1 E2E scope:** Ballistic + Single-Action, 10-step demo approved. Deferred to 2+: Laser/Auto/Scatter/Exotic/Rarity UI/loot integration/repair UI.

**Див.:** [architecture.md §D9-D14](../architecture.md)

### 2026-04-22 — Tier 0b complete ✅
Всі 18 задач 6 кластерів закриті. Legacy factories + compat layer + Shotgun повністю видалені. WeaponEntityState — pure data з composition + cached Stats. ItemState/GroundItemState тепер carry WeaponConfiguration. 53 tests (24 Tier 0a + 22 unit + 7 integration). Tier 1 розблокований.

### 2026-04-22 — Bot weapons deferred to Tier 4
**Decision:** Bot weapons (BotSpawnSystem + BotConstants) залишаються **повністю hardcoded** для Tier 0b і 1. Вони не проходять через assembly pipeline, їхні Stats populate напряму з BotConstants raw fields.

**Перенесено в Tier 4** (разом з rarity):
- Видалити всі hardcoded stat fields з `BotConstants.BotTypeConfig`
- Додати `WeaponConfiguration WeaponConfiguration` до `BotTypeConfig`
- `BotSpawnSystem` має отримати registry з context, викликати `WeaponAssemblySystem.TryAssemble`
- Bot variety приходитиме з **rarity-per-bot** (Scav=Common, Boss=Epic/Legendary) + різні delivery/payload combinations
- Balance може "попливти" — це ок, зафіксується в Tier 4 balance pass

**Чому не зараз:**
- Без rarity всі боти мали б однакові Stats (Common) → втрата variety
- Без Rotary/Swarm heavy bots не мають адекватного delivery
- Scope creep у Cluster C (вже breaking change)
- Weapon Builder навмисно player-facing, bot path — окремий

### 2026-04-20 — Tier 0a complete ✅
**Виконано:**
- Всі нові types у `Assets/Scripts/State/`: enums (RarityTier, FiringPattern), stats structs (CommonPayloadStats, DeliveryStats, WeaponStats, 3 payload-specific), readonly struct instances (PayloadCoreInstance, DeliveryCoreInstance, ExoticModInstance), WeaponConfiguration
- SO definitions: abstract `PayloadCoreDefinition` + 4 subclass'і (Ballistic/Laser/Rocket/Foam), concrete `DeliveryCoreDefinition`, `ExoticModDefinition`, central `CoreDefinitionDatabase`
- Port `ICoreDefinitionRegistry` + реалізація `DatabaseCoreDefinitionRegistry` у `Assets/Scripts/Adapters/`
- Інтеграція: `RaidContext.CoreDefinitions`, RaidSession ctor параметр, App завантажує Database через `Resources.Load<>`
- Editor utility `WeaponBuilderStubAssets` (menu: `Tools → Weapon Builder → Create Stub Assets`) — idempotent authoring з чисел pre-migration factories
- Stub assets створені: BallisticRound/SingleAction/Auto + CoreDefinitionDatabase (Common tier заповнений)
- 24 unit tests зелені (CoreInstance equality + Registry lookup)

**Нульовий runtime impact:** існуючі weapons (Rifle/Pistol/Shotgun) працюють як раніше, нова система поряд і не використовується — Tier 0b її підключить.

**Next:** декомпозиція Tier 0b у конкретні task'и.

### 2026-04-20 — D3 amendment: Database SO over Resources.LoadAll
**Рішення:** Міняємо D3 loading mechanism з `Resources.LoadAll<T>` на central `CoreDefinitionDatabase` SO (за патерном `QuestDatabase`).

**Чому:**
- Explicit — явний список assets у Database Inspector, не автомагічний scan
- No Resources build bloat для всіх assets (тільки Database у Resources)
- Консистентно з existing project pattern (`QuestDefinition` + `QuestDatabase`)
- Simpler hot-reload: Database rebuild індексу замість сканування filesystem

**Наслідки:**
- `ICoreDefinitionRegistry` wrap'ить Database і будує BuildIndex dictionaries
- Cluster D (stub assets) додає `CoreDefinitionDatabase.asset` як central aggregator
- Registry реалізація у Cluster C стає простішою (один SO → indices, замість Resources scan)

**Див.:** [architecture.md §D3](../architecture.md)

### 2026-04-20 — R1 decision: Tier 0 split into 0a + 0b
**Контекст:** Tier 0 work items зросли до ~14 після закриття D1-D4. Розмір ризикує великим diff, довгим review, конфліктами merge.

**Рішення:** Розділити на два sub-tiers:
- **Tier 0a — Data Model Foundation.** Всі нові types (enums, structs, SOs, registry port), stub assets. Старі weapons (Rifle/Shotgun/Pistol) працюють БЕЗ змін. Безпечно мержиться.
- **Tier 0b — Migration.** `WeaponEntityState` refactor, `WeaponAssemblySystem`, compat layer для Rifle/Pistol, Shotgun повне видалення, ShootingSystem rewrite з dispatch, read-site refactor, Debugger update.

**Наслідки:**
- 0a розблоковує **паралельну роботу** — дизайнер наповнює SO assets поки програміст працює над 0b
- 0b стартує тільки після merge 0a і passing тестів
- Gate між 0a і 0b — zero progress на 0b, доки 0a не зелений

**Див.:** [roadmap.md — Tier 0a / 0b](./roadmap.md)

### 2026-04-20 — D3 + D4 resolved: SO authoring + struct instances
**D3 — `*CoreDefinition` як ScriptableObject:**
- Abstract base + typed subclass'и (для Payload), plain SO (для Delivery/Exotic поки що)
- Assets у `Assets/Resources/WeaponBuilder/{Payloads,Deliveries,Exotics}/`
- `StatsByTier` серіалізується як `CommonPayloadStats[]` з індексом = `(int)RarityTier`
- Loading через новий port `ICoreDefinitionRegistry` — `Resources.LoadAll<T>(path)` на startup
- Consistency з DevCheats і ItemDefinition патернами

**D4 — `*CoreInstance` як readonly struct:**
- `[Serializable] readonly struct` з public readonly fields + `IEquatable<T>`
- Value semantics: zero GC, immutable, structural equality
- `ExoticModInstance?` — nullable value type у composition
- Instance тримає тільки `DefinitionId`, lookup definition через registry (CLAUDE.md rule 6 ✅)
- Extension method `Definition<T>()` опційно для handler зручності

**Наслідки для Tier 0 коду:**
- Створюємо `ICoreDefinitionRegistry` port + його реалізацію на Resources.LoadAll
- 3 abstract SO bases + 4 Payload subclass'и + per-asset authoring workflow
- Test infrastructure: `ScriptableObject.CreateInstance<T>()` + builder helpers
- Inspector edit stats per tier → масив індексований rarity enum

**Див.:** [architecture.md §Tier 0 remaining details — D3, D4](../architecture.md)

### 2026-04-20 — D1 + D2 resolved: WeaponStats composition
**D1 — Розподіл полів по джерелах:**
- 8 stats з Payload: Damage, Speed, Lifetime, HeadshotMult, Penetration, ArmorDamage, BleedChance, AmmoType
- 13 stats з Delivery: FireInterval, ProjectilesPerShot, SpreadAngle, ConeHalfAngle, BodyRotationSpeed, AimFollowSharpness, 3×Recoil, Equip/Unequip Time, MagazineSize, ReloadTime
- Нуль overlap — кожне поле з одного джерела
- AmmoType — identifier на `PayloadCoreDefinition`, не в `WeaponStats`
- Ammo modifiers складаються окремо в `ShootingSystem` на fire (як зараз) — третій канал

**D2 — Stats structure для різнорідних Payloads:**
- `PayloadCoreDefinition` стає abstract base + 4 subclass'и (`Ballistic/Laser/Rocket/Foam`)
- Common stats (8 полів) у base `StatsByTier: Map<RarityTier, CommonPayloadStats>`
- Payload-specific (ChargeTime, ExplosionRadius, Slow/Stick) — у subclass `SpecificByTier`
- Handlers касят definition до свого типу (type-safe, explicit, Unity SO-friendly)
- Delivery поки без subclass'ів — всі 5 мають однаковий shape

**Наслідки:**
- `WeaponStats` — один flat struct з 21 common-полем (8 Payload + 13 Delivery)
- `ShootingSystem` handlers для Laser/Rocket/Foam робитимуть cast на свій typed definition
- Exotic модифікатори для specific-полів застосовуються в handler'і (не в `Compose`)
- `Compose` pipeline простий і детермінований

**Див.:** [architecture.md §Tier 0 remaining details — D1, D2](../architecture.md)

### 2026-04-19 — Architecture review (Tier 0 readiness check)
**Контекст:** Після закриття Q1/Q2/Q6/Q7 зробили комплексне ревʼю стану архітектури. Мета — впевнитись, що Tier 0 готовий до імплементації.

**Висновок:** 4 великі питання закриті, але є **12 subsidiary-деталей**, які треба опрацювати:
- 4 must-do перед Tier 0 кодом (D1-D4): склад Stats, Stats structure для різних payloads, SO vs plain data, struct vs class
- 3 should-do перед Tier 1 (D6-D8): re-assembly triggers, invalid config handling, archetype labels
- 5 tracked/housekeeping (D9-D12): RaidContext integration, Debugger update, DevCheats, docs sync

**Consistency checks:**
- Всі hard rules design.md мапляться на архітектуру ✅
- ExoticMod без rarity — зафіксовано явно в §1 і в hard rules mapping ✅
- CLAUDE.md compliance — stateless static systems ✅, IDs only in state ✅, малі diff'и ⚠️ (Tier 0 великий — R1), docs sync ❌ (D12 очікує)

**Ризики:**
- R1: Tier 0 scope підріс з 5 до 12 work items — розглянути split на 0a+0b
- R2: Laser Charge state machine — рішення Tier 2
- R3: Multi-projectile × custom payload — перевірка Tier 2

**Див.:** [architecture.md §Tier 0 remaining details](../architecture.md)

### 2026-04-19 — Q7 resolved: Factory migration + scope cut
**Рішення:**
- Фазована міграція: Rifle/Pistol переводяться на новий pipeline через тимчасовий compat layer (Tier 0-2)
- **Shotgun видаляється з гри** — зменшує legacy surface, не треба Scatter міграції в Tier 2
- До кінця Tier 2: factories (`CreateRifle` / `CreatePistol`) повністю видалені, compat layer прибраний
- `CreateShotgun` видаляється одразу в Tier 0
- Scatter Delivery лишається в scope Weapon Builder як нова поведінка (Tier 2), але не міграція існуючого
- **AmmoType прив'язується до Payload Core** (не до weapon / delivery)
  - Ballistic → Ammo_Rifle (Rifle і Pistol ділять його після міграції)
  - Laser → energy cell, Rocket → rocket ammo, Foam → foam canister

**Наслідки:**
- Compat layer ~5 рядків у `WeaponSyncSystem`, явно позначений як temporary
- Існуючі системи (shooting range, armor tests) завжди мають працюючу зброю
- Ammo_Shotgun видаляється разом з Shotgun
- Один code path, нуль dual maintenance

**Див.:** [architecture.md §7](../architecture.md)

### 2026-04-19 — Q6 resolved: Rarity data model
**Рішення:**
- **Q6.1** Rarity per module instance (не per weapon) — відповідає hard rule з design.md
- **Q6.2** Enum `RarityTier { Common, Uncommon, Rare, Epic, Legendary }`
- **Q6.3** Per-module stat tables: `StatsByTier: Map<RarityTier, Stats>` всередині кожного `*CoreDefinition`

**Наслідки:**
- `PayloadCoreInstance` / `DeliveryCoreInstance` у `WeaponConfiguration` = `{ DefinitionId, Rarity }`
- `*CoreDefinition` одразу має `StatsByTier` (у Tier 0 заповнений тільки Common, решта — в Tier 4)
- Composition pipeline Tier 0 вже знає як обирати stats по rarity
- Ammo modifiers лишаються окремим каналом (складаються в ShootingSystem на fire)
- Tier 4 = заповнення таблиць + UI + balance, не переписування

**Див.:** [architecture.md §6](../architecture.md)

### 2026-04-19 — Q2 resolved: Delivery Core abstraction
**Рішення:** FiringPattern enum у `DeliveryCoreDefinition` + внутрішній dispatch у `ShootingSystem` + state machine extension.

- Handlers — static методи `ShootingSystem` (не окремі класи / strategies)
- Параметричні deliveries (Single/Auto/Scatter) шарять helper-код
- Rotary/Swarm мають власні handlers + нові фази state machine (`SpinningUp`, `SpinningDown`, `VolleyActive`)
- Runtime state (SpinLevel, VolleyShotsRemaining) — у `WeaponEntityState`, handlers stateless
- Fist Delivery виключена з scope Weapon Builder (окрема melee система)

**Scope:**
- Enum + dispatch закладаємо в **Tier 0** навіть для 1 pattern (Single)
- Нові фази state machine — в **Tier 3** коли з'являться Rotary/Swarm

**Також зафіксовано як guiding principle:** немає пріоритету зберігати існуючу архітектуру. Якщо переписування дає очевидні переваги — ріжемо без зволікань.

**Див.:** [architecture.md §2](../architecture.md)

### 2026-04-18 — Q1 resolved: composed weapon representation
**Рішення:**
- **Q1.1** Composition + cached computed stats (explicit modules + `Stats` block + runtime fields окремо)
- **Q1.2** Cached on assembly; runtime state окремо; ammo modifiers окремо (як зараз у `ShootingSystem`)
- **Q1.3** `WeaponConfiguration` живе в `InventoryItem` (persistent), `WeaponEntityState` створюється `WeaponSyncSystem` при equip — розширюємо існуючий patterns

**Наслідки:**
- `WeaponEntityState` треба refactor: розділити identity / Stats / runtime
- Всі read sites `weapon.FieldName` → `weapon.Stats.FieldName` (механічна правка)
- `InventoryItem` schema розширюється на `WeaponConfiguration`
- `WeaponSyncSystem` ускладнюється: замість `definitionId → factory` тепер `WeaponConfiguration → assembly pipeline`
- Гравець зможе тримати кілька зібраних збірок в інвентарі — це безпосередньо вирішує design problem "немає причини тримати кілька збірок"

**Див.:** [architecture.md §1](../architecture.md)

### 2026-04-18 — Tier-based roadmap approved
**Рішення:** Реалізація структурована по 8 tiers (0-7). Fist Delivery і Typed Attachments — поза scope роадмапи. Tier 0-2 плануються детально, Tier 3-7 — high-level outlines, деталізуються в міру наближення.
**Причина:** Фіча масивна, без tier gating ризик розповзання scope. Архітектурні питання прив'язані до tiers, де вони вперше стають блокерами, а не вирішуються всі одразу.
**Див.:** [roadmap.md](./roadmap.md)

---

## Blockers

*Нічого не блокує на даний момент. Коли з'явиться — додаємо сюди з контекстом і owner'ом.*

---

## Next actions

- [x] ~~Пройти Tier 0 архітектурні питання Q1, Q2, Q6, Q7~~ ✅
- [x] ~~Комплексне ревʼю стану архітектури~~ ✅
- [x] ~~Закрити D1+D2~~ ✅
- [x] ~~Закрити D3, D4~~ ✅
- [x] ~~R1 decision — Tier 0 split~~ ✅ 0a (data model) + 0b (migration)
- [x] ~~Декомпозувати Tier 0a у конкретні задачі~~ ✅
- [x] ~~Старт імплементації Tier 0a~~ ✅ complete (2026-04-20)
- [x] ~~Merge Tier 0a~~ ✅ committed `03e07b9` (2026-04-20)
- [x] ~~Закрити D6-D8~~ ✅ (2026-04-20)
- [x] ~~Детальна декомпозиція Tier 0b у конкретні T-0b.NN task'и~~ ✅ 18 задач, 6 кластерів ([tasks.md](./tasks.md))
- [x] ~~Старт імплементації Tier 0b~~ ✅ complete (2026-04-22)
- [ ] **Merge Tier 0b у master** (наступний крок)
- [ ] **Start Tier 1** — Vertical Slice: Ballistic + Single-Action end-to-end + UI збірки на базі

---

## Related docs

- [README.md](../README.md)
- [design.md](../design.md)
- [architecture.md](../architecture.md)
