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

## Tier 0b — Migration (headline-level)

> Детально опрацьовується ПІСЛЯ merge 0a. Наразі — only headlines.

- [ ] **T-0b.01 — Refactor `WeaponEntityState`** на composition + Stats + runtime sections
- [ ] **T-0b.02 — `WeaponStatComposer`** — (Payload, Delivery, Exotic?) → WeaponStats
- [ ] **T-0b.03 — `WeaponAssemblySystem`** — WeaponConfiguration → WeaponEntityState
- [ ] **T-0b.04 — Rewrite `WeaponSyncSystem`** на assembly pipeline
- [ ] **T-0b.05 — Compat layer** `LegacyDefinitionToConfig` для Rifle/Pistol
- [ ] **T-0b.06 — Видалити Shotgun** повністю (code, assets, ammo, loot, spawners)
- [ ] **T-0b.07 — Видалити `CreateRifle` / `CreatePistol`** factories
- [ ] **T-0b.08 — Rewrite `ShootingSystem`** на dispatch по `FiringPattern`
- [ ] **T-0b.09 — Read sites refactor** — `weapon.X` → `weapon.Stats.X`
- [ ] **T-0b.10 — `RaidStateDebuggerWindow`** update під нові поля
- [ ] **T-0b.11 — Integration tests** — Rifle/Pistol parity pre/post migration

---

## Tier 1+ (placeholder)

> Деталізується коли Tier 0 закритий і ми підійдемо ближче до Tier 1.

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
