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

- [/] **T-0a.06 — `WeaponConfiguration` тип** (struct створений; використання в InventoryItem schema — пізніше в 0b)
  - Path: `Assets/Scripts/State/WeaponConfiguration.cs`
  - `[Serializable]` struct з: `PayloadCoreInstance Payload`, `DeliveryCoreInstance Delivery`, `bool HasExotic + ExoticModInstance Exotic` (bool-flag pattern для Unity serialization, accessor `Exotic?` computed), `int AmmoInMagazine`
  - DoD: тип компілюється, готовий до використання в InventoryItem schema (але ще НЕ підключається до runtime — це 0b)

### Cluster B: ScriptableObject definitions

- [ ] **T-0a.07 — Abstract `PayloadCoreDefinition` SO**
  - Path: `Assets/Scripts/State/PayloadCoreDefinition.cs`
  - `public abstract class PayloadCoreDefinition : ScriptableObject`
  - Поля: `[SerializeField] string _id`, `[SerializeField] string _archetype`, `[SerializeField] string _ammoType`, `[SerializeField] CommonPayloadStats[] _statsByTier` (length 5)
  - Public accessor `CommonPayloadStats StatsByTier(RarityTier tier) => _statsByTier[(int)tier]`
  - Validation hook: editor warning якщо `_statsByTier.Length != 5`
  - DoD: abstract SO, не можна інстанціювати, але subclasses будуть

- [ ] **T-0a.08 — 4 Payload subclasses**
  - Paths:
    - `Assets/Scripts/State/BallisticPayloadDefinition.cs` — `[CreateAssetMenu(menuName="Weapons/Payload/Ballistic")]`, без specific
    - `Assets/Scripts/State/LaserPayloadDefinition.cs` — з `[SerializeField] LaserSpecificStats[] _specificByTier`
    - `Assets/Scripts/State/RocketPayloadDefinition.cs` — з `_specificByTier: RocketSpecificStats[]`
    - `Assets/Scripts/State/FoamPayloadDefinition.cs` — з `_specificByTier: FoamSpecificStats[]`
  - Для трьох з specific — accessor `SpecificByTier(RarityTier)`
  - DoD: всі 4 створюються через `Assets → Create → Weapons → Payload → ...`

- [ ] **T-0a.09 — `DeliveryCoreDefinition` SO**
  - Path: `Assets/Scripts/State/DeliveryCoreDefinition.cs`
  - `[CreateAssetMenu(menuName="Weapons/Delivery")]`
  - Поля: `_id`, `FiringPattern _pattern`, `DeliveryStats[] _statsByTier` (length 5), pattern-specific: `float _spinUpTime`, `float _spinDownTime`, `int _volleyCount`, `float _volleyInterval`
  - DoD: SO створюється через меню, працює single class без subclass'ів

- [ ] **T-0a.10 — `ExoticModDefinition` SO**
  - Path: `Assets/Scripts/State/ExoticModDefinition.cs`
  - `[CreateAssetMenu(menuName="Weapons/Exotic")]`
  - Поля: `_id`, `_archetype`. Stat modifiers шапка поки порожня — наповнюється Tier 5
  - DoD: SO створюється, компілюється; повноцінна поведінка — Tier 5

### Cluster C: Registry port + loading

- [ ] **T-0a.11 — Port `ICoreDefinitionRegistry`**
  - Path: `Assets/Scripts/Adapters/ICoreDefinitionRegistry.cs`
  - Інтерфейс: `PayloadCoreDefinition GetPayload(string id)`, `DeliveryCoreDefinition GetDelivery(string id)`, `ExoticModDefinition GetExotic(string id)` + `TryGet` варіанти
  - Throws / returns null на міссінг — вирішити консистентно (пропоную: `Get` throws, `TryGet` returns bool)
  - DoD: інтерфейс compile, пустий

- [ ] **T-0a.12 — Registry реалізація на Resources**
  - Path: `Assets/Scripts/Adapters/ResourcesCoreDefinitionRegistry.cs`
  - Constructor: `Resources.LoadAll<PayloadCoreDefinition>("WeaponBuilder/Payloads")` etc., build `Dictionary<string, T>` по `_id`
  - Duplicate id → log warning + last-one-wins
  - Missing → `Get` throws `KeyNotFoundException`, `TryGet` returns false
  - DoD: unit test (T-0a.14) завантажує stub asset → `registry.GetPayload("BallisticRound")` повертає його

- [ ] **T-0a.13 — RaidContext integration**
  - Path: `Assets/Scripts/Session/RaidContext.cs` (existing; додаємо readonly property)
  - Додати `ICoreDefinitionRegistry CoreDefinitions` у RaidContext
  - Composition root (`App` чи equivalent) — створює `ResourcesCoreDefinitionRegistry` на startup і передає в RaidContext
  - Жодна нова система ще не читає з нього в 0a — буде використано в 0b
  - DoD: RaidContext має property, compile green, ніхто не падає на null

### Cluster D: Stub assets (designer-parallel)

- [ ] **T-0a.14 — Stub Payload asset: Ballistic Round (Common)**
  - Path: `Assets/Resources/WeaponBuilder/Payloads/BallisticRound.asset`
  - Тип: `BallisticPayloadDefinition`
  - `_id = "BallisticRound"`, `_archetype = "Ballistic"`, `_ammoType = "Ammo_Rifle"`
  - `_statsByTier[(int)Common]` заповнений **реальними числами** так, щоб збірка Ballistic+Single відтворювала поточні Pistol stats (Damage=15, Speed=25, Lifetime=3, HeadshotMult=2.0, Pen/ArmorDmg/Bleed з існуючих)
  - Решта tiers — placeholder (0 або дуплікат Common, Tier 4 заповнить)
  - DoD: asset існує, registry його знаходить, числа відповідають поточним Pistol

- [ ] **T-0a.15 — Stub Delivery assets: Single-Action + Auto (Common)**
  - Paths: `Assets/Resources/WeaponBuilder/Deliveries/SingleAction.asset`, `Auto.asset`
  - Single: `_pattern = Single`, Common stats відтворюють Pistol delivery-частину (FireInterval=0.4, ProjPerShot=1, SpreadAngle, mag=12, reload, recoil...)
  - Auto: `_pattern = Auto`, Common відтворює Rifle (FireInterval=0.2, ProjPerShot=1, mag=30, ...)
  - DoD: обидва assets існують, registry знаходить

### Cluster E: Tests

- [/] **T-0a.16 — Unit tests**
  - Paths: `Assets/Tests/EditMode/{Subject}Tests.cs` (flat, per-subject)
    - [x] `CoreInstanceTests.cs` ✅ — equality, GetHashCode, RarityTier ordering, WeaponConfiguration nullable Exotic pattern
    - [ ] `CoreDefinitionRegistryTests.cs` (later, after T-0a.11/12)
  - Покриває:
    - `*CoreInstance` structural equality (дві з однаковими ID+Rarity рівні; різні — не рівні)
    - `RarityTier` integer ordering збігається з order'ом enum
    - `PayloadCoreDefinition.StatsByTier(tier)` повертає правильний indexed element
    - Registry: завантаження через `Resources.LoadAll`, lookup success, lookup missing (TryGet false, Get throws)
    - Registry: duplicate id warning (optional — mock logger)
  - DoD: всі тести зелені, coverage принаймні основних шляхів

---

## Tier 0a Exit Gate

Перед стартом 0b перевіряємо:

- [ ] Всі T-0a.* закриті
- [ ] `Assets/Scripts/WeaponBuilder/` структура створена
- [ ] `Assets/Resources/WeaponBuilder/` має 3 stub assets з реальними Common stats
- [ ] `RaidContext.CoreDefinitions` доступний
- [ ] Unit tests зелені
- [ ] Існуючі weapons (Rifle/Shotgun/Pistol) працюють БЕЗ змін
- [ ] Shooting range, armor tests зелені
- [ ] 0a merged у main / dev branch

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
