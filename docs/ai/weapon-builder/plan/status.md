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
- [x] ~~Декомпозувати Tier 0a у конкретні задачі~~ ✅ [tasks.md](./tasks.md) — 16 задач, 5 кластерів
- [ ] **Старт імплементації Tier 0a** (Cluster A → B → C → D+E)
- [ ] Закрити D6-D8 перед стартом Tier 1 коду (re-assembly triggers, invalid config, archetype labels)
- [ ] Деталізувати Tier 0b задачі після merge 0a
- [ ] Size estimation для Tier 0 — вирішити split 0a+0b чи ні
- [ ] Закрити D6-D8 перед стартом Tier 1 коду

---

## Related docs

- [README.md](../README.md)
- [design.md](../design.md)
- [architecture.md](../architecture.md)
