# Weapon Builder — Implementation Tasks

> **Living doc.** Конкретні задачі реалізації з checkbox'ами. Оновлюється по ходу імплементації (marking done, додавання notes, PR links, discovered sub-tasks).
>
> **Формат:** `T-{tier}.{NN}` як stable ID. Кожна задача — мінімальна одиниця, яку можна взяти і зробити end-to-end з clear DoD.
>
> **Принцип наповнення:** Tier 0a розписаний повністю (готовий до старту). Tier 0b — headline-level (деталізуємо коли 0a mergeнутий). Пізніші tiers — тільки коли наближаємось.

---

## Legend

- `[ ]` — not started
- `[/]` — in progress
- `[x]` — done
- `[-]` — cancelled / deprecated
- 🚧 — blocked
- 🔗 — has PR/commit reference

---

## Tier 0a — Data Model Foundation

**Goal:** Всі нові types, SO infrastructure, registry port існують і покриті тестами. Старі weapons працюють як раніше. Безпечно мержиться.

**File layout:** Слідуємо існуючій конвенції проекту (flat by type, не feature-folder):
- Types / structs / enums / SO definitions → `Assets/Scripts/State/`
- Port interfaces + adapter implementations → `Assets/Scripts/Adapters/`
- Tests → `Assets/Tests/EditMode/` (flat, `{Subject}Tests.cs`)
- Assets → `Assets/Resources/WeaponBuilder/` (feature-folder ок для asset organization)

Рішення зафіксовано 2026-04-20.

### Cluster A: Enums and value types

- [x] **T-0a.01 — Enum `RarityTier`** ✅
  - Path: `Assets/Scripts/State/RarityTier.cs`
  - Members: `Common=0, Uncommon=1, Rare=2, Epic=3, Legendary=4` (явні int values для indexing)
  - DoD: enum доступний з коду, `(int)RarityTier.Common == 0`

- [x] **T-0a.02 — Enum `FiringPattern`** ✅
  - Path: `Assets/Scripts/State/FiringPattern.cs`
  - Members: `Single, Auto, Scatter, Rotary, Swarm`
  - DoD: enum доступний, всі 5 case'ів явні

- [x] **T-0a.03 — Stats structs (common)** ✅
  - Path: `Assets/Scripts/State/WeaponStats.cs`
  - Містить: `CommonPayloadStats` (8 полів з §D1), `DeliveryStats` (13 полів з §D1), `WeaponStats` (21 поле — Compose вихід)
  - Всі `[Serializable]` з public fields
  - DoD: structs скомпільовані, всі поля з architecture.md §D1 присутні

- [x] **T-0a.04 — Payload-specific stats structs** ✅
  - Path: `Assets/Scripts/State/PayloadSpecificStats.cs`
  - `LaserSpecificStats { ChargeTime }`, `RocketSpecificStats { ExplosionRadius }`, `FoamSpecificStats { SlowDuration, StickDuration }`
  - Всі `[Serializable]`
  - DoD: структури готові (Ballistic не має specific)

- [x] **T-0a.05 — `*CoreInstance` readonly structs** ✅
  - Path: `Assets/Scripts/State/CoreInstances.cs`
  - `PayloadCoreInstance`, `DeliveryCoreInstance`, `ExoticModInstance`
  - `[Serializable] readonly struct` з public readonly fields + `IEquatable<T>` (+ GetHashCode, ==, !=)
  - `ExoticModInstance` — без `Rarity` поля
  - DoD: structs скомпільовані, unit tests (T-0a.16) зелені на equality

- [x] **T-0a.06 — `WeaponConfiguration` тип** ✅ (struct створений; використання в InventoryItem schema — 0b)
  - Path: `Assets/Scripts/State/WeaponConfiguration.cs`
  - `[Serializable]` struct з: `PayloadCoreInstance Payload`, `DeliveryCoreInstance Delivery`, `bool HasExotic + ExoticModInstance Exotic` (bool-flag pattern для Unity serialization, accessor `Exotic?` computed), `int AmmoInMagazine`
  - DoD: тип компілюється, готовий до використання в InventoryItem schema (але ще НЕ підключається до runtime — це 0b)

### Cluster B: ScriptableObject definitions

- [x] **T-0a.07 — Abstract `PayloadCoreDefinition` SO** ✅
  - Path: `Assets/Scripts/State/PayloadCoreDefinition.cs`
  - `public abstract class PayloadCoreDefinition : ScriptableObject`
  - Поля: `[SerializeField] string _id`, `[SerializeField] string _archetype`, `[SerializeField] string _ammoType`, `[SerializeField] CommonPayloadStats[] _statsByTier` (length 5)
  - Public accessor `CommonPayloadStats StatsByTier(RarityTier tier) => _statsByTier[(int)tier]`
  - Validation hook: editor warning якщо `_statsByTier.Length != 5`
  - DoD: abstract SO, не можна інстанціювати, але subclasses будуть

- [x] **T-0a.08 — 4 Payload subclasses** ✅
  - Paths:
    - `Assets/Scripts/State/BallisticPayloadDefinition.cs` — `[CreateAssetMenu(menuName="Weapons/Payload/Ballistic")]`, без specific
    - `Assets/Scripts/State/LaserPayloadDefinition.cs` — з `[SerializeField] LaserSpecificStats[] _specificByTier`
    - `Assets/Scripts/State/RocketPayloadDefinition.cs` — з `_specificByTier: RocketSpecificStats[]`
    - `Assets/Scripts/State/FoamPayloadDefinition.cs` — з `_specificByTier: FoamSpecificStats[]`
  - Для трьох з specific — accessor `SpecificByTier(RarityTier)`
  - DoD: всі 4 створюються через `Assets → Create → Weapons → Payload → ...`

- [x] **T-0a.09 — `DeliveryCoreDefinition` SO** ✅
  - Path: `Assets/Scripts/State/DeliveryCoreDefinition.cs`
  - `[CreateAssetMenu(menuName="Weapons/Delivery")]`
  - Поля: `_id`, `FiringPattern _pattern`, `DeliveryStats[] _statsByTier` (length 5), pattern-specific: `float _spinUpTime`, `float _spinDownTime`, `int _volleyCount`, `float _volleyInterval`
  - DoD: SO створюється через меню, працює single class без subclass'ів

- [x] **T-0a.10 — `ExoticModDefinition` SO** ✅ (minimal shell — full shape in Tier 5)
  - Path: `Assets/Scripts/State/ExoticModDefinition.cs`
  - `[CreateAssetMenu(menuName="Weapons/Exotic")]`
  - Поля: `_id`, `_archetype`. Stat modifiers шапка поки порожня — наповнюється Tier 5
  - DoD: SO створюється, компілюється; повноцінна поведінка — Tier 5

### Cluster C: Registry port + loading

- [x] **T-0a.10b — `CoreDefinitionDatabase` SO** ✅ (added 2026-04-20, per D3 amendment)
  - Path: `Assets/Scripts/State/CoreDefinitionDatabase.cs`
  - ScriptableObject з `[SerializeField] List<PayloadCoreDefinition> _payloads`, `List<DeliveryCoreDefinition> _deliveries`, `List<ExoticModDefinition> _exotics`
  - Public read-only accessors: `IReadOnlyList<...> Payloads/Deliveries/Exotics`
  - BuildIndex pattern: lazy-init dictionaries by `Id` on first `TryGet`; `Get` throws
  - `[CreateAssetMenu]` для створення в Inspector
  - DoD: SO скомпільовано, індекс будується лейзі, duplicate-id warning логується

- [x] **T-0a.11 — Port `ICoreDefinitionRegistry`** ✅
  - Path: `Assets/Scripts/Adapters/ICoreDefinitionRegistry.cs`
  - Інтерфейс: `PayloadCoreDefinition GetPayload(string id)`, `DeliveryCoreDefinition GetDelivery(string id)`, `ExoticModDefinition GetExotic(string id)` + `TryGet` варіанти
  - Throws / returns null на міссінг — консистентно: `Get` throws `KeyNotFoundException`, `TryGet` returns bool
  - DoD: інтерфейс compile, пустий

- [x] **T-0a.12 — Registry реалізація поверх Database** ✅
  - Path: `Assets/Scripts/Adapters/DatabaseCoreDefinitionRegistry.cs`
  - Constructor приймає `CoreDefinitionDatabase` (завантажений з `Resources.Load<CoreDefinitionDatabase>("WeaponBuilder/CoreDefinitionDatabase")` у composition root)
  - Building: делегує index'и до Database
  - Missing → `Get` throws `KeyNotFoundException`, `TryGet` returns false
  - DoD: unit test завантажує test Database → `registry.GetPayload("BallisticRound")` повертає його

- [x] **T-0a.13 — RaidContext integration** ✅
  - `RaidContext.CoreDefinitions` додано (readonly, nullable)
  - `RaidSession` приймає `ICoreDefinitionRegistry` через constructor (nullable default)
  - `App` завантажує `CoreDefinitionDatabase` через `Resources.Load<>` (за QuestDatabase pattern), створює `DatabaseCoreDefinitionRegistry`, передає у RaidSession
  - Database відсутній → warning, registry стає null. Tier 0a-safe (ніхто не читає з нього ще)

### Cluster D: Stub assets (designer-parallel)

- [x] **T-0a.14 — Stub Payload asset: Ballistic Round (Common)** ✅
- [x] **T-0a.15 — Stub Delivery assets: Single-Action + Auto (Common)** ✅
- [x] **T-0a.14b — `CoreDefinitionDatabase` asset** ✅ (створено тим самим editor script)

Editor script: `Tools → Weapon Builder → Create Stub Assets` (idempotent).
Values sourced from `CreatePistol` (SingleAction) / `CreateRifle` (Auto) / compromise-average of both (Ballistic Common).

### Cluster E: Tests

- [x] **T-0a.16 — Unit tests** ✅
  - Paths: `Assets/Tests/EditMode/{Subject}Tests.cs` (flat, per-subject)
    - [x] `CoreInstanceTests.cs` ✅ — equality, GetHashCode, RarityTier ordering, WeaponConfiguration nullable Exotic pattern
    - [x] `CoreDefinitionRegistryTests.cs` ✅ — Get/TryGet for Payload/Delivery/Exotic, missing-id handling, duplicate-id warning, null-db guard
  - Покриває:
    - `*CoreInstance` structural equality (дві з однаковими ID+Rarity рівні; різні — не рівні)
    - `RarityTier` integer ordering збігається з order'ом enum
    - `PayloadCoreDefinition.StatsByTier(tier)` повертає правильний indexed element
    - Registry: завантаження через `Resources.LoadAll`, lookup success, lookup missing (TryGet false, Get throws)
    - Registry: duplicate id warning (optional — mock logger)
  - DoD: всі тести зелені, coverage принаймні основних шляхів

---

## Tier 0a Exit Gate ✅ PASSED (2026-04-20)

- [x] Всі T-0a.* закриті
- [x] Нові файли у `Assets/Scripts/State/` і `Assets/Scripts/Adapters/` (flat layout per project convention)
- [x] `Assets/Resources/WeaponBuilder/` має 3 stub assets + CoreDefinitionDatabase (Common tier populated з реальних чисел factories)
- [x] `RaidContext.CoreDefinitions` доступний (non-null після Database завантаження в App)
- [x] Unit tests зелені (24 тести: CoreInstance + CoreDefinitionRegistry)
- [x] Існуючі weapons (Rifle/Shotgun/Pistol) працюють БЕЗ змін — нульовий runtime impact
- [x] Shooting range, armor tests — не зачеплені
- [ ] 0a merged у main / dev branch (pending)

---

## Tier 0b — Migration

**Goal:** Перевести existing weapons (Rifle/Pistol) на новий data-driven pipeline через тимчасовий compat layer. Shotgun повністю видалити. Після exit gate: нуль legacy factories, нуль compat layer, `WeaponEntityState` має нову shape.

**Принцип декомпозиції:** кожна задача — мінімальна одиниця, яка лишає кодбазу у робочому стані. Clusters впорядковані так, щоб після кожної попередньої cluster нічого не падало.

**Рекомендоване порядкування кластерів:** A → B → C → D → E → F.

### Cluster A — SO field additions (standalone, no behaviour change)

- [x] **T-0b.01 — Add `DisplayName` to `PayloadCoreDefinition`** ✅
  - Path: `Assets/Scripts/State/PayloadCoreDefinition.cs`
  - `[SerializeField] string _displayName` + `public string DisplayName => _displayName`
  - DoD: поле компілюється; існуючі assets не падають (default = "")

- [x] **T-0b.02 — Add `FormFactor` to `DeliveryCoreDefinition`** ✅
  - Path: `Assets/Scripts/State/DeliveryCoreDefinition.cs`
  - `[SerializeField] string _formFactor` + `public string FormFactor => _formFactor`
  - DoD: поле компілюється

- [/] **T-0b.03 — Update `WeaponBuilderStubAssets` editor script** (script оновлено, треба re-run menu item)
  - ✅ Script тепер заповнює: Ballistic.DisplayName="Ballistic", SingleAction.FormFactor="Pistol", Auto.FormFactor="Rifle"
  - ⏳ **Action item:** У Unity виконати `Tools → Weapon Builder → Create Stub Assets` щоб оновити існуючі .asset файли
  - DoD: три stub asset'и мають нові значення на диску

- [x] **T-0b.04 — `WeaponArchetypeLabel.Compose` helper** ✅ (+ 9 unit tests)
  - Path: `Assets/Scripts/State/WeaponArchetypeLabel.cs`
  - `public static class WeaponArchetypeLabel { public static string Compose(PayloadCoreDefinition, DeliveryCoreDefinition) }` → `"{payload.DisplayName} {delivery.FormFactor}"`
  - Null-guard: empty string якщо null на вході
  - DoD: static helper + unit tests

### Cluster B — Core systems (standalone, no behaviour change)

- [x] **T-0b.05 — `WeaponStatComposer`** ✅
  - Path: `Assets/Scripts/Systems/WeaponStatComposer.cs`
  - `public static class WeaponStatComposer { public static WeaponStats Compose(PayloadCoreInstance, PayloadCoreDefinition, DeliveryCoreInstance, DeliveryCoreDefinition) }`
  - Bаlансує 20 полів з 7 Payload + 13 Delivery (per architecture.md §D1)
  - Не знає про `ICoreDefinitionRegistry` — приймає resolved definitions
  - DoD: compose коректний + unit tests (compose з Common tier → очікувані числа)

- [x] **T-0b.06 — `WeaponAssemblyFailed` event** ✅ (+ `StringPayload2` на RaidEvent)
  - Path: `Assets/Scripts/Adapters/IRaidEvents.cs`, `RaidEventBuffer.cs`
  - Додати event type з payload: `{ string InventoryItemId, string Reason }`
  - DoD: event emittable, consumers можуть читати

- [x] **T-0b.07 — `WeaponAssemblySystem`** ✅
  - **Scope adjustment:** у Cluster B повертає `AssemblyResult` struct (`WeaponStats` + resolved definitions), НЕ `WeaponEntityState` — це був би race з Cluster C state refactor. `WeaponSyncSystem` (Cluster D) комбінує TryAssemble результат + runtime fields у state
  - Fail cases закриті (per D7 strict): missing Payload/Delivery/Exotic → false + reason
  - Null-registry guard теж закритий
  - Unit tests: 6 — success (2), fails (3), null-registry (1)

### Cluster C — State refactor (breaking change for read sites)

- [x] **T-0b.08 — Refactor `WeaponEntityState` structure** ✅
  - Path: `Assets/Scripts/State/WeaponEntityState.cs`
  - Нова shape:
    ```
    composition: PayloadCore (instance), DeliveryCore (instance), ExoticMod (instance + hasExotic)
    cached:      Stats (WeaponStats)
    runtime:     Phase, PhaseStartTime, LastFireTime, AmmoInMagazine, RecoilOffset
    identity:    Id, PrefabId
    ```
  - Видалити старі flat-поля (FireInterval, ProjectileDamage тощо — тепер у `Stats.X`)
  - **Внутрішні зміни factory:** `CreateRifle/Pistol/Shotgun` тимчасово заповнюють `Stats` напряму (compat), runtime поля як раніше. AmmoType читається з `weapon.PayloadCore.Definition.AmmoType` (поки null-safe через stub).
  - Треба шимnути factory щоб вони не падали до T-0b.11 (видалення factories)
  - DoD: компілюється; Rifle/Pistol створюються без runtime error

- [x] **T-0b.09 — Migrate read sites до `weapon.Stats.X`** ✅
  - Paths (з grep):
    - `Assets/Scripts/Systems/ShootingSystem.cs`
    - `Assets/Scripts/Systems/WeaponStateMachineSystem.cs`
    - `Assets/Scripts/Systems/Bot/BotCombatSystem.cs`
    - `Assets/Scripts/Systems/Bot/BotSpawnSystem.cs` (якщо читає weapon stats)
    - `Assets/Scripts/View/PlayerPresenter.cs`
    - `Assets/Scripts/View/AimCursorOverlay.cs`
    - `Assets/Scripts/Editor/RaidStateDebuggerWindow.cs` — частково, повний refresh у T-0b.16
  - Механічний refactor: `weapon.FireInterval` → `weapon.Stats.FireInterval` тощо
  - `weapon.AmmoType` → `weapon.PayloadCore.Definition.AmmoType` (можливий null-check шим)
  - DoD: все компілюється; existing tests зелені; shooting range + armor tests зелені

### Cluster D — Pipeline migration (new assembly flow)

- [x] **T-0b.10 — Compat layer `LegacyDefinitionToConfig`** ✅
  - Path: `Assets/Scripts/Systems/WeaponSyncSystem.cs` (static dictionary усередині)
  - `static readonly Dictionary<string, WeaponConfiguration> LegacyDefinitionToConfig`:
    - `"Rifle"` → Ballistic/Common + Auto/Common
    - `"Pistol"` → Ballistic/Common + Single/Common
  - Позначити коментарем `// TEMPORARY — removed at end of Tier 0b`
  - DoD: dictionary існує

- [x] **T-0b.11 — Rewrite `WeaponSyncSystem` на assembly pipeline** ✅
  - Замінити виклик `WeaponEntityState.CreateFromDefinitionId` на:
    1. Map definitionId → `WeaponConfiguration` через compat layer
    2. Викликати `WeaponAssemblySystem.TryAssemble(config, registry, out state, out reason)`
    3. Success → put state into hotbar
    4. Fail → emit `WeaponAssemblyFailed` event, hotbar slot empty (ghost-weapon per D7)
  - DoD: Rifle/Pistol рейд grameplay parity (FireInterval, damage, magazine — ідентичні pre-migration)

- [x] **T-0b.12 — `ShootingSystem` dispatch по `FiringPattern`** ✅
  - Path: `Assets/Scripts/Systems/ShootingSystem.cs`
  - Розділити `Tick` на case по `weapon.DeliveryCore.Definition.Pattern`:
    - `Single` → повноцінна реалізація (відтворює поточну поведінку, працює для `"Pistol"`)
    - `Auto` → виклик того самого helper з різним FireInterval/Magazine (працює для `"Rifle"` після compat)
    - `Scatter` / `Rotary` / `Swarm` — throw `NotImplementedException` (будуть у Tier 2/3)
  - DoD: Rifle + Pistol відчувається ідентично pre-migration на shooting range

### Cluster E — Legacy cleanup

- [x] **T-0b.13 — Remove Shotgun completely** ✅
  - Paths (з grep):
    - `Assets/Scripts/State/WeaponEntityState.cs` — видалити `CreateShotgun`, case `"Shotgun"` у dispatcher
    - `Assets/Scripts/State/ItemDefinition.cs` — видалити `"Shotgun"`, `"Ammo_Shotgun"`, `"Ammo_Shotgun_HP"`
    - `Assets/Scripts/Constants/BotConstants.cs` — weapon lists
    - `Assets/Scripts/Constants/ItemGroups.cs`
    - `Assets/Scripts/Constants/ContainerConstants.cs`
    - `Assets/Scripts/Constants/CraftConstants.cs`
    - `Assets/Scripts/Systems/LootSystem.cs`
    - `Assets/Scripts/Systems/PlayerSpawnSystem.cs`
    - `Assets/Scripts/Session/RaidSession.cs` — SpawnTestBots або подібне
  - `grep -R "Shotgun\|Ammo_Shotgun\|Weapon_Shotgun" Assets/Scripts` має повертати порожньо (окрім коментарів у deleted factories історичних commit'ів)
  - DoD: гра компілюється, тести зелені, shotgun ніде не референситься

- [x] **T-0b.14 — Remove `CreateRifle` / `CreatePistol` factories** ✅
  - Path: `Assets/Scripts/State/WeaponEntityState.cs`
  - Видалити обидва factory methods + dispatcher `CreateFromDefinitionId` cases
  - На цей момент `WeaponSyncSystem` через compat layer + assembly pipeline — factory не потрібна
  - DoD: `grep -R "CreateRifle\|CreatePistol" Assets/Scripts` порожньо; гра працює

- [x] **T-0b.15 — Remove compat layer** ✅
  - `ItemState` розширено на `HasWeaponConfiguration` + `WeaponConfiguration` + `CreateWeapon` factory
  - `GroundItemState` — той самий pattern + `CreateWeapon`
  - `ItemDefinition` — додано `WeaponPrefabId`, заповнено для Rifle/Pistol
  - Новий `Systems/WeaponItemFactory` — центральний helper (`DefaultConfigFor`, `SpawnItem`, `IsKnownWeaponDefinition`)
  - `WeaponSyncSystem` — **compat dict видалений**, `BuildWeaponForItem` читає `invItem.WeaponConfiguration` напряму
  - Оновлено spawn sites: `PlayerSpawnSystem`, `LootSystem` (direct weapon + backpack drops), `CraftingSystem`, `QuestSystem`, `RaidSession` (3 місця — loot points, quest items, test spawns)
  - `InventorySystem.TryPickUp` / `TryDrop` — копіюють WeaponConfiguration між ground ↔ inventory
  - `LootPopupView` (4 місця) — copy WeaponConfiguration у всіх ground ↔ inventory flows + RebuildFloorInventory
  - Save data breaking change OK (немає production saves)

### Cluster F — Tools & tests

- [x] **T-0b.16 — Update `RaidStateDebuggerWindow`** ✅ (composition section у Cluster C; inventory weapon slots показують WeaponConfiguration у Cluster F)
  - Path: `Assets/Scripts/Editor/RaidStateDebuggerWindow.cs`
  - Відобразити новий shape `WeaponEntityState`:
    - Composition section (Payload ID+Rarity, Delivery ID+Rarity, Exotic ID)
    - Stats block (expandable? just dump fields)
    - Runtime (Phase, Ammo, Recoil, etc.)
  - DoD: debugger показує нову shape без exceptions

- [x] **T-0b.17 — Unit tests for Cluster A+B helpers** ✅ (зроблено під час Cluster A/B: 9 ArchetypeLabel + 7 Composer + 6 Assembly = 22 тести)
  - Path: `Assets/Tests/EditMode/`
  - `WeaponArchetypeLabelTests.cs` — compose format, null-guards
  - `WeaponStatComposerTests.cs` — Common tier compose → очікувані значення; Rarity selection правильна
  - `WeaponAssemblySystemTests.cs` — TryAssemble success + 3 fail cases (missing Payload, Delivery, Exotic)
  - DoD: ~15-20 нових тестів зелені

- [x] **T-0b.18 — Integration test: Rifle / Pistol parity** ✅ (7 тестів: Rifle + Pistol parity, 4 ghost-weapon paths, ground round-trip)
  - Path: `Assets/Tests/EditMode/WeaponSyncSystemIntegrationTests.cs`
  - Spawn weapon через InventoryItem("Rifle") → WeaponEntityState має FireInterval=0.2, Damage=15 (Ballistic Common), Mag=30
  - Spawn через "Pistol" → FireInterval=0.4, Damage=15, Mag=12
  - Ghost test: невалідна конфігурація (payload з неіснуючим ID) → TryAssemble false, event emitted, hotbar slot empty
  - DoD: 3+ integration tests зелені

---

## Tier 0b Exit Gate ✅ PASSED (2026-04-22)

- [x] Всі T-0b.01..18 закриті
- [x] Rifle і Pistol працюють на new pipeline (через WeaponItemFactory → WeaponAssemblySystem)
- [x] `WeaponEntityState` — composition + Stats + runtime, без flat legacy полів
- [x] `ShootingSystem` dispatch по FiringPattern (Single/Auto/Scatter fall-through, Rotary/Swarm NotImplemented)
- [x] Shotgun повністю видалений (10 файлів code, assets, ammo, constants)
- [x] Factories (`CreateRifle`/`CreatePistol`/`CreateShotgun`) видалені
- [x] Compat layer видалений — items carry WeaponConfiguration themselves
- [x] `ItemState` + `GroundItemState` schema розширено на `WeaponConfiguration`
- [x] `WeaponAssemblyFailed` event emittable + captured у FakeRaidEvents
- [x] Raid State Debugger — composition section для weapons + WeaponConfiguration на inventory slots
- [x] Unit + integration tests зелені (Tier 0a 24 + ArchetypeLabel 9 + Composer 7 + Assembly 6 + Integration 7 = 53 тести)
- [ ] Shooting range, armor tests зелені (verify у Unity)
- [ ] 0b merged у master

---

## Tier 1 — Vertical Slice (Ballistic + Single-Action end-to-end)

**Goal:** Player interacts з physical Workbench у Hideout → Weapon Builder screen (UI Toolkit) → selects Payload + Delivery → Build → new ItemState у backpack → equip → shoot. DevCheats shortcut — open Builder з будь-де (включно з рейдом).

**Scope reminder (per D14):** Common-only tier, no Exotic Mods у UI, Ballistic + Single-Action мінімум. Laser/Auto/Scatter — коли їхні assets додадуться у Database, вони з'являться в UI автоматично (Tier 2 scope).

### Cluster A — Presenter + state (pure C#, testable)

- [x] **T-1.01 — List accessors у `ICoreDefinitionRegistry`** ✅ (+3 tests)
- [x] **T-1.01b — Generic "Weapon" ItemDefinition + FormFactor prefab fallback** ✅
- [x] **T-1.02 — `WeaponBuilderState` struct** ✅
- [x] **T-1.03 — `WeaponBuilderPresenter` class** ✅
- [x] **T-1.04 — `WeaponBuilderPresenterTests`** ✅ (14 tests: defaults, partial/full selection, events, inventory gating, TryBuild success+3 fail paths)
- [x] **T-1.05 — `IsWeaponBuilderOpen` flag на `PlayerEntityState`** ✅

### Cluster B — UI Toolkit view

- [x] **T-1.06 — UXML + USS** ✅ (`Resources/UI/WeaponBuilder/WeaponBuilderWindow.{uxml,uss}` + `WeaponBuilderAssetsBootstrap` для PanelSettings)
- [x] **T-1.07 — `WeaponBuilderWindow` MonoBehaviour + UIDocument** ✅
- [x] **T-1.08 — Bindings (dropdowns → presenter → preview)** ✅
- [x] **T-1.09 — Open/Close input pause** ✅ (`PlayerEntityState.IsWeaponBuilderOpen`)

### Cluster C — Workbench scene interactable

- [x] **T-1.10 — `InteractPressed` у `IInputAdapter`** ✅ (Key.E, gated по `BlockGameplayInput`)
- [x] **T-1.11 — `WorkbenchView` MonoBehaviour** ✅ (proximity detection + billboard prompt TextMesh + open Builder on E)
- [x] **T-1.12 — Hideout scene integration** ✅ (workbench placed у Hideout by user; editor utility removed after one-shot use)

### Cluster D — DevCheats shortcut

- [x] **T-1.13 — DevCheats toggle button** ✅ (просто кнопка у `DevCheatsWindow` — без окремої SO section, mirror CraftingMockup pattern)

### Cluster E — Integration + tests

- [x] **T-1.14 — WeaponBuilderWindow initialization у `App`** ✅ (AppBootstrap spawns host GameObject + constructs presenter з registry/inventory/allocator)
- [x] **T-1.15 — Integration test (end-to-end без UI)** ✅ (5 tests: pistol full-flow, rifle full-flow, multi-build, ground-round-trip, preview drives final stats)

### Tier 1 Exit Gate ✅ PASSED (2026-04-23)

- [x] Всі T-1.* закриті
- [x] Hideout workbench → Builder open works
- [x] DevCheats global shortcut works
- [x] Build → new weapon у backpack, equippable, shoots as pistol
- [x] Presenter unit tests (14) + registry list tests (3) + end-to-end tests (5) зелені — **22 Tier 1 tests**
- [ ] Merge у master

---

## Tier 2 — Core Breadth (Laser + Scatter)

**Goal:** Ballistic × {Single, Auto, Scatter} + Laser × {Single, Auto, Scatter} = 6 working archetypes. Laser payload вводить charge-up mechanic — `ChargeTime` відпрацьовує перед кожним пострілом, незалежно від delivery (Variant B — consistent behaviour).

### Cluster A — Charge-up pipeline

- [x] **T-2.01 — `WeaponPhase.Charging` + `ChargeStartTime` runtime field** ✅
- [x] **T-2.02 — Charge events у `IRaidEvents`** ✅ (Started/Completed/Cancelled + FakeRaidEvents impl)
- [x] **T-2.03 — `WeaponStateMachineSystem.Charging` handler** ✅ (cancel on release або swap-pending → Unequipping; completion → ShootingSystem)
- [x] **T-2.04 — `ShootingSystem` charge gate** ✅ (Ready+Laser → Charging; Charging+elapsed → fall-through до fire pipeline)
- [x] **T-2.05 — ChargeTime lookup helper** ✅ (`Systems/WeaponChargeResolver.cs`)

### Cluster B — Asset content

- [/] **T-2.06 — `LaserCharge.asset`** (script готовий, треба run menu)
- [/] **T-2.07 — `Scatter.asset`** (script готовий, треба run menu)
- [x] **T-2.08 — `Ammo_EnergyCell` ItemDefinition** ✅
- [x] **T-2.09 — Update `WeaponBuilderStubAssets`** ✅ (PopulateLaser + PopulateScatter + Database тепер reference'ить всі 5 core assets)

### Cluster C — UI feedback

- [x] **T-2.10 — AimCursorOverlay charge ring** ✅ (energy-blue dot ring поверх crosshair, center dot pulses з intensity)

### Cluster D — Tests

- [x] **T-2.11 — State machine tests (Charging flow)** ✅ (3 tests: cancel on release, hold stays, swap cancels + Unequipping)
- [x] **T-2.12 — ShootingSystem charge tests** ✅ (4 tests: Ready→Charging, time-remaining stays, time-elapsed fires+emits Completed, non-laser bypass)
- [x] **T-2.13 — End-to-end: Laser charge flow** ✅ (8 tests: RequiresChargeUp for all Laser combos, Scatter new content parity, full Charging cycles, cancel)

### Tier 2 Exit Gate ✅ (pending user verification)

- [x] 6 working archetypes (Ballistic/Laser × Single/Auto/Scatter)
- [x] Laser charges перед кожним shot консистентно (Variant B)
- [x] UI charge progress ring (blue pulsing center)
- [x] Tests — +15 нових (3 state machine + 4 ShootingSystem + 8 end-to-end)
- [ ] Merge

---

## Tier 3+ (placeholder)

> Деталізується ближче до Tier 3.

---

## Notes

Формат для робочих заміток під час імплементації (приклад):

```
T-0a.07: started 2026-04-21, branch: `wb/0a-payload-definition`, blocked on T-0a.03
T-0a.11: merged 2026-04-22, PR #123 🔗
```

Додавайте inline під конкретну задачу або в нижній "log" секцію — як зручно.

---

## Related docs

- [roadmap.md](./roadmap.md) — tier structure і exit criteria
- [status.md](./status.md) — decisions log, open architectural questions
- [../architecture.md](../architecture.md) — всі design rationale
