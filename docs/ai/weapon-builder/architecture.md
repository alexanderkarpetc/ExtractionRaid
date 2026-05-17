# Weapon Builder — Architecture

> **Living doc.** Заповнюється в міру того, як ми обговорюємо вбудовування фічі в існуючу кодбазу. Поки тут лише контекст і відкриті питання.

---

## Guiding principles

- **Немає пріоритету зберігати існуючу архітектуру.** Якщо переписування дає очевидні переваги — ріжемо без зволікань. Existing code — це orienting reference, не constraint.
- **State-first.** Runtime state живе у entity states, systems — stateless функції.
- **Explicit over implicit.** Enum'и з явними case'ами замість неявних behavior flags.

---

## Мета документа

Описати, як Weapon Builder вбудовується в поточну архітектуру проекту:
- як змінюється data model (`WeaponEntityState` та супутні)
- як трансформується shooting pipeline (`ShootingSystem`, `ProjectileSystem`)
- як cores і mods стають data-driven замість hardcoded factory methods
- які нові системи з'являються (slot validation, module composition, rarity scaling)
- як це лягає на 5-шарову архітектуру (App → Session → Systems → Adapters → View/Presenter)

---

## Поточний стан системи зброї (baseline)

*Зафіксовано на момент старту роботи над Weapon Builder. Джерело: exploration кодбази + `docs/ai/weapons.md`.*

### Створення зброї
- Hardcoded factory: `WeaponEntityState.CreateRifle / CreateShotgun / CreatePistol`
- Dispatcher `CreateFromDefinitionId(EId, definitionId)` за string ID
- `WeaponSyncSystem` викликає factory при додаванні зброї в inventory

### Data model
`WeaponEntityState` — monolithic стан з ~20 параметрів:
- Shooting: `FireInterval`, `ProjectileSpeed`, `ProjectileLifetime`, `ProjectileDamage`, `HeadshotDamageMultiplier`, `BasePenetration`, `BaseArmorDamage`, `BaseBleedChance`, `ProjectilesPerShot`, `SpreadAngle`
- Aiming: `ConeHalfAngle`, `BodyRotationSpeed`, `AimFollowSharpness`
- Recoil: `RecoilKickForward`, `RecoilKickSide`, `RecoilRecoverySpeed`
- Equip/Unequip: `EquipTime`, `UnequipTime`
- Ammo: `AmmoType`, `MagazineSize`, `AmmoInMagazine`, `ReloadTime`
- Runtime: `LastFireTime`, `Phase`, `PhaseStartTime`, `RecoilOffset`

### Shooting pipeline
`ShootingSystem`:
1. Перевірка стану (ready, ammo, equipped)
2. Обчислення напрямку (parallax + convergence blend)
3. Композиція combat stats: `weapon + ammo definition`
4. Спавн N projectiles (`ProjectilesPerShot`) із spread
5. Перехід weapon state machine → Firing
6. Recoil apply
7. Ammo decrement

Стан машина: Ready → Firing → Cooldown → Ready (+ Equipping, Unequipping, Reloading)

### Ammo
`ItemDefinition` задає per-ammo-type модифікатори: `Penetration`, `ArmorDamage`, `BleedChance`. Додаються до weapon base stats в `ShootingSystem`.

---

## Ключові архітектурні питання (TBD)

*Список питань, які треба закрити ДО початку імплементації. Відповіді фіксуємо тут у міру обговорень.*

### 1. Як представити composed weapon? ✅ RESOLVED (2026-04-18)

**Q1.1 — Monolithic vs composition:** Composition + cached computed stats.

```
WeaponEntityState {
  // композиція (persistent config refs)
  PayloadCore: PayloadCoreInstance,   // { DefinitionId, Rarity }
  DeliveryCore: DeliveryCoreInstance, // { DefinitionId, Rarity }
  ExoticMod: ExoticModInstance?,       // { DefinitionId } — БЕЗ rarity (hard rule з design.md)

  // computed stats (cache, regenerated on assembly/equip)
  Stats: WeaponStats { FireInterval, ProjectileDamage, BasePenetration, ... },

  // runtime state (mutable during play)
  Phase, PhaseStartTime, AmmoInMagazine, RecoilOffset, LastFireTime, ...
}
```

**Чому:** явна модель, first-class provenance (завжди зрозуміло з чого зібрано), чистий поділ identity / computed / runtime. Добре лягає на майбутні tiers (Tier 4 rarity scaling, Tier 5 exotic hooks).

**Ціна:** refactor усіх read sites (`weapon.FireInterval` → `weapon.Stats.FireInterval`) — механічний, одноразовий.

**Q1.2 — Cached on assembly vs computed on read:** Cached on assembly.

- Stats обчислюються один раз при збірці / equip, лежать у `Stats`.
- Runtime поля (Phase, AmmoInMagazine, RecoilOffset) — окремі mutable-поля.
- **Ammo modifiers — окремо**, складаються в `ShootingSystem` на fire (як зараз). Зміна ammo ≠ перебудова weapon.
- Invalidation: на equip (якщо конфіг змінився на базі).

**Чому:** stats не змінюються в бою (немає dynamic rarity, немає hot-swap модулів під час рейду). Reads дуже часті (кожен fire tick). Cache — очевидно оптимальніший.

**Q1.3 — Де живе assembled форма:** Розширюємо існуючий `WeaponSyncSystem` pattern.

```
InventoryItem (weapon)
  WeaponConfiguration {
    PayloadCoreId + Rarity,
    DeliveryCoreId + Rarity,
    ExoticModId?,
    AmmoInMagazine,  // persistent — магазин зберігається
  }
       │
       │ (on equip — WeaponSyncSystem)
       ▼
WeaponEntityState (runtime)
  composition refs + computed Stats + runtime fields
```

- **Persistence:** `WeaponConfiguration` живе в `InventoryItem` (гравець носить зібрану зброю в рюкзаку).
- **Runtime:** `WeaponEntityState` створюється `WeaponSyncSystem` при equip з `WeaponConfiguration`.
- **Наслідок:** гравець може мати в інвентарі кілька зібраних збірок — це прямо вирішує проблему "немає причини тримати кілька збірок" з design.md.

**Чому:** лягає на існуючу архітектуру (WeaponSyncSystem вже робить щось подібне для Rifle/Shotgun/Pistol). `WeaponSyncSystem` ускладнюється — замість `definitionId → factory` тепер `WeaponConfiguration → assembly pipeline`.

### 2. Як абстрагувати Delivery Core? ✅ RESOLVED (2026-04-19)

**Note:** Fist Delivery поза scope Weapon Builder (окрема melee система). Лишається **5 deliveries**.

**Підхід:** State machine extension + внутрішній dispatch по `FiringPattern` enum у `ShootingSystem`.

```csharp
DeliveryCoreDefinition {
  // параметри (data)
  FireInterval, ProjectilesPerShot, SpreadAngle, ...

  // pattern — виражає "який тип delivery"
  Pattern: FiringPattern,  // Single | Auto | Scatter | Rotary | Swarm

  // pattern-specific params (0 / null для неактуальних)
  SpinUpTime, SpinDownTime,    // Rotary
  VolleyCount, VolleyInterval, // Swarm
}
```

**ShootingSystem** — stateless system з dispatch по Pattern:

```csharp
void Tick(...) {
  switch (weapon.DeliveryCore.Pattern) {
    case FiringPattern.Single:  HandleSingleAction(...); break;
    case FiringPattern.Auto:    HandleAuto(...); break;
    case FiringPattern.Scatter: HandleScatter(...); break;
    case FiringPattern.Rotary:  HandleRotary(...); break;
    case FiringPattern.Swarm:   HandleSwarm(...); break;
  }
}
```

Handlers — static методи того ж system'у (не окремі класи). Параметричні (Single/Auto/Scatter) можуть ділити helper-функції.

**State machine phases:**

Поточні фази: `Ready, Firing, Cooldown, Equipping, Unequipping, Reloading`.

Нові фази (додаємо в Tier 3 коли з'явиться Rotary/Swarm):
- `SpinningUp` — Rotary: розкрутка
- `SpinningDown` — Rotary: зупинка
- `VolleyActive` — Swarm: триває серія пострілів

**State-first принцип:** весь runtime state (поточний SpinLevel, VolleyShotsRemaining тощо) живе у `WeaponEntityState` runtime-полях, НЕ в behavior instance. Handlers — stateless функції.

**Чому це рішення:**
- Параметричні deliveries (Single/Auto/Scatter) природно шарять helper-код — не треба 3 окремих файлів
- Rotary/Swarm отримують явні handlers — їхня логіка реально відрізняється
- Дод новий pattern = новий enum value + новий handler + (опційно) нові фази state machine — це explicit і легко rev'ювати
- State machine розширюється явно — debug тривіальний

**Scope-related нотатка:**

Для Tier 1 (тільки Single) — enum і dispatch закладаємо **одразу**, навіть з одним case'ом. Для Tier 3 (Rotary/Swarm) це стане природним розширенням, а не переписуванням.

Фази `SpinningUp` / `VolleyActive` — додаємо коли вперше знадобляться (Tier 3), не зараз.

### 3. Як абстрагувати Payload Core?
- [ ] Payload впливає на projectile behavior, damage type, secondary effects
- [ ] Чи потрібен `IPayloadBehavior` для custom projectile logic (напр. charge-up для Laser, AoE для Rocket, sticky для Foam)?
- [ ] Чи це data-only — просто набір стат, а поведінка емерджентна?

---

### D6. Re-assembly triggers ✅ RESOLVED (2026-04-20)

**Коли `WeaponConfiguration → WeaponEntityState` відбувається:**

1. **On equip** (auto) — `WeaponSyncSystem` викликає `WeaponAssemblySystem.TryAssemble` коли зброя з'являється у hotbar
2. **On explicit "Apply"** (manual) — коли гравець редагує *equipped* weapon через Weapon Builder UI, натискає "Apply" → re-assembly на поточному `WeaponEntityState`

**Що НЕ викликає re-assembly:**
- Зміна ammo type — це runtime-field, не частина Stats
- Reactive hooks на будь-який edit WeaponConfiguration — немає

**Runtime state persistence:** `AmmoInMagazine` живе у `WeaponConfiguration` (persistent) і продовжує з того самого значення при re-equip. `Phase`/`PhaseStartTime`/`RecoilOffset`/`LastFireTime` — скидаються при кожній assembly (вони справді runtime).

**Scope:**
- Tier 0b / 1: реалізувати лише on-equip path. "Apply" — Tier 4 UI.
- `WeaponAssemblySystem` виносимо як окрему systemу — вона викликається з двох місць пізніше (WeaponSyncSystem і Apply button handler).

---

### D7. Invalid configuration handling ✅ RESOLVED (2026-04-20)

**Pattern: "Ghost weapon".** Invalid config не кидає і не silent-unarmed'ить — item лишається в inventory **явно помічений як broken**, equip fails з clear signal.

**Валідація на `WeaponAssemblySystem.TryAssemble(WeaponConfiguration, out WeaponEntityState)`:**
- Payload `DefinitionId` відсутній або not found у registry → `false`
- Delivery `DefinitionId` відсутній або not found → `false`
- Exotic `HasExotic=true` але `DefinitionId` відсутній/not found → `false`
  - **Немає auto-repair.** Невалідний exotic ламає всю збірку (strict C, без mild D). Гравець мусить явно виправити через builder UI
- Все ok → `true`, out = повноцінний `WeaponEntityState`

**Що robить `WeaponSyncSystem` на `TryAssemble == false`:**
- Inventory item **лишається** (не видаляється)
- У hotbar-слоті — empty (або null placeholder)
- Log error з деталями чому fail
- Event `WeaponAssemblyFailed` (`RaidEventBuffer`) для UI feedback
- Гравець залишається unarmed (не крешиться), але explicit — weapon в інвентарі, але нефункціональний

**UI scope:**
- Tier 0b/1: немає спеціального UI. Log в консолі. Item видно в inventory, equip тихо не працює (буде подальша warning у logs). Достатньо для dev/testing — production не мусить стикатися.
- Tier 4: broken icon ⚠️ на item, tooltip з reason ("Payload 'BallisticRound' is missing from the registry"), кнопка "Salvage" або "Repair in Builder".

**Джерела invalid configs (реалістично):**
- Save data з pre-migration era (0b compat layer мапить — за визначенням валідні)
- Видалений SO asset (dev testing)
- Data corruption (rare)

Production гравці не можуть створити invalid config через UI — Builder enforces validity при збірці.

---

### D9. Weapon Builder UI location ✅ RESOLVED (2026-04-22)

**Rule:** Окремий modal screen, який можна відкрити **з будь-якого контексту** (raid, hideout, menu). Trigger'и context-specific, але сам screen — один.

**Primary trigger (player-facing, production):** фізичний workbench object у hideout level. Гравець підходить до workbench → натискає Interact key → відкривається Builder screen. Consistent з extraction shooter pattern (Escape from Tarkov / Duckov workbench).

**Secondary trigger (dev):** DevCheats кнопка для запуску Builder з будь-якого стану — включно з рейдом. Debug-only, не production feature.

**Screen as modal:** коли open — pause input/time (як і inventory modal), ESC/Cancel закриває. Не окрема Unity scene — overlay.

**Наслідки для Tier 1:**
- Новий `WorkbenchInteractable` MonoBehaviour + view component на scene object у hideout
- Нова DevCheats section "Weapon Builder" з toggle-відкриття
- Builder UI screen — overlay, що працює поверх будь-якого стейту

### D10. Module supply для Tier 1 ✅ RESOLVED (2026-04-22)

**Рішення:** Всі Payload / Delivery / Exotic — infinite та all-unlocked у Builder screen для Tier 1. Нема loot/economy integration.

**Чому:** Tier 1 validates UX і pipeline, не balance/loot. Economy — Tier 6.

**Наслідки:**
- Builder screen запитує у registry список усіх available Payloads/Deliveries
- В UI всі показуються як selectable options
- Нуль inventory lookup для модулів
- Після Tier 6 (loot integration): same UI, але options фільтруються по модулям у player inventory

### D11. Builder UI layout ✅ RESOLVED (2026-04-22)

**Рішення:** Single screen, 3 dropdowns (Payload / Delivery / Exotic optional), live preview zone нижче.

**Layout sketch:**
```
┌── Weapon Builder ──────────────────────────────┐
│                                                │
│  Payload:  [ Ballistic Round ▼ ]  [Common ▼]  │
│  Delivery: [ Single-Action  ▼ ]  [Common ▼]  │
│  Exotic:   [ (none)         ▼ ]              │
│                                                │
│  ── Preview ───────────────────────────────    │
│  Archetype: Ballistic Pistol                   │
│  Damage:       15    ProjectileSpeed: 25       │
│  FireInterval: 0.4   Magazine: 12              │
│  HeadshotMult: 2.0x  Penetration: 15           │
│  ...                                           │
│                                                │
│  [ Cancel ]                       [ Build ]    │
└────────────────────────────────────────────────┘
```

**Behaviour:**
- Dropdowns reactive: зміна будь-якого triggers recompose + preview update (in-memory)
- Preview — stats + archetype label
- Build button creates new ItemState у backpack
- Cancel — close without changes

### D12. Build result ✅ RESOLVED (2026-04-22)

**Рішення:** Build створює **новий** `ItemState` з `WeaponConfiguration` — прилітає у перший free backpack slot. Existing inventory items не зачіпаються.

**Initial state:**
- `AmmoInMagazine = MagazineSize` (повний магазин)
- `DefinitionId = "Weapon"` (generic — build defines identity, see note)
- `WeaponPrefabId` — mapped з Delivery FormFactor (Pistol/Rifle — у Tier 1 з hardcoded map; Tier 3+ — може приходити з Delivery asset)

**Якщо backpack повний:** Build button disabled + tooltip "Backpack full".

**DefinitionId нота:** після міграції "Rifle"/"Pistol" — це legacy item types. Нові builds можуть використати **"Weapon"** як generic DefinitionId (або конкретний derived з archetype — "Ballistic Pistol"). Пропоную generic "Weapon" для Tier 1 — identity живе у `WeaponConfiguration`, `DisplayName` беремо з `WeaponArchetypeLabel.Compose`.

### D13. Entry point — physical workbench ✅ RESOLVED (2026-04-22)

**Рішення:** Hideout level має scene object "Workbench" (prefab + collider + interactable behaviour). Гравець підходить, натискає Interact → Builder screen open.

**Implementation components:**
- `WorkbenchView` MonoBehaviour — scene object, рендер та interact prompt
- Existing interact input (use якщо є — перевірити `IInputAdapter`)
- `WorkbenchState` (entity?) — поки не потрібен, workbench — просто interactable object без внутрішнього стану

**DevCheats shortcut** — global hotkey / UI button для testability з рейду.

**Scope Tier 1:**
- 1 workbench prefab у Hideout scene
- Interact range (e.g., 2m) + prompt UI ("Press E to craft")
- Opens Builder screen

### D14. Tier 1 Minimum E2E demo ✅ RESOLVED (2026-04-22)

**Scope:** Ballistic Round (Common) + Single-Action (Common) end-to-end.

**Demo steps:**
1. Player у Hideout level підходить до Workbench
2. Interact → Builder screen opens
3. Default state: empty composition, preview порожній
4. Player selects Payload = Ballistic Round (Common)
5. Player selects Delivery = Single-Action (Common)
6. Preview shows: "Ballistic Pistol" label, full stats table (Damage=15, FireInterval=0.4, Mag=12, ...)
7. Player clicks Build → new `ItemState` with `WeaponConfiguration` lands у backpack
8. Close Builder, extract to raid
9. Equip new weapon in hotbar slot → WeaponSyncSystem builds WeaponEntityState
10. Shoot — стріляє як pistol

**Out of Tier 1 scope (deferred):**
- Laser Charge (Tier 2 — charge-up logic)
- Auto Delivery (Tier 2 — rewrite ShootingSystem Auto handler)
- Scatter Delivery (Tier 2)
- Exotic Mods (Tier 5)
- Rarity UI (Tier 4 — Common-only)
- Banned combos / slot structure (Tier 4)
- Module drops / loot integration (Tier 6)
- Repair / salvage UI for broken weapons (Tier 4)

### D8. Archetype label system ✅ RESOLVED (2026-04-20)

**Pattern: pure template `{Payload.DisplayName} {Delivery.FormFactor}`.** Без override-table у Tier 1, без baseline stripping. Consistent і explicit — extension'и приходять Tier 5 polish'ом.

**Нові fields на SOs (додаємо в Tier 0b):**
- `PayloadCoreDefinition.DisplayName : string` — "Ballistic", "Laser", "Rocket", "Foam"
- `DeliveryCoreDefinition.FormFactor : string` — "Pistol", "Rifle", "Shotgun", "Machinegun", "Launcher"

**Composer helper:**
```csharp
public static class WeaponArchetypeLabel
{
    public static string Compose(PayloadCoreDefinition payload, DeliveryCoreDefinition delivery)
        => $"{payload.DisplayName} {delivery.FormFactor}";
}
```

**Null/empty tolerance (contract).** Callers in production always pass resolved SO refs
from the registry — so the happy path is both-present. However the composer is also
used during *partial* UI state (presenter shows "Ballistic" once only a payload is
picked, or "Pistol" once only a delivery is picked), so the helper falls back to a
single segment when one side is null/empty, and returns `string.Empty` when both are
missing. Tests in `WeaponArchetypeLabelTests` lock this behaviour.

**Tier 1 matrix (Pattern → FormFactor):**
| Delivery Pattern | FormFactor |
|------------------|------------|
| Single | Pistol |
| Auto | Rifle |
| Scatter | Shotgun |
| Rotary | Machinegun |
| Swarm | Launcher |

**Examples after compose:**
- Ballistic + Single → "Ballistic Pistol"
- Ballistic + Auto → "Ballistic Rifle"
- Laser + Single → "Laser Pistol"
- Foam + Scatter → "Foam Shotgun"
- Rocket + Swarm → "Rocket Launcher"

**Migration impact:** legacy Rifle/Pistol після 0b міграції відображаються як "Ballistic Rifle"/"Ballistic Pistol" — трохи verbose, але consistency переважає. Override-table (Tier 5) дозволить spicy names для specific combos, якщо знадобиться.

**Exotic NOT included in label:** design rule — archetype = Payload + Delivery only. Exotic — modifier, може відображатись окремо в UI (e.g. "Ballistic Rifle · Ricochet"), але не частина базової назви.

### 4. Як імплементувати slot structure / module compatibility?
- [ ] Дата модель слотів: per-weapon чи per-core?
- [ ] Де живе правило сумісності: у модулі, у слоті, окремий rules engine?
- [ ] Валідація: в runtime (weapon builder UI) чи на рівні save data?

### 5. Як Exotic Mod модифікує поведінку?
Exotic Mods — принципово різні модифікації:
- Ricochet, Boomerang — змінюють projectile trajectory
- Split on Impact — hit handler
- Ammo Return on Kill — kill event handler
- Multi-Shot Pattern — fire handler (спавн кількох projectiles)

- [ ] Event-driven модифікатори (hooks у pipeline) чи стратегії?
- [ ] Стекування: поточний scope — 1 Exotic, але архітектура має дозволяти розширення?

### 6. Rarity data model ✅ RESOLVED (2026-04-19)

**Note:** Реалізація scaling (заповнення реальними числами + UI) — Tier 4. **Data model** фіксуємо в Tier 0, бо визначає структуру `PayloadCoreInstance` / `DeliveryCoreInstance` і `*CoreDefinition`.

**Q6.1 — Per module instance (не per weapon).** Кожен модуль у інвентарі має свій tier. Збірка = комбінація tiers. Відповідає hard rule з design.md: "Rarity застосовується до Payload Core і Delivery Core".

**Q6.2 — Enum `RarityTier`:**

```csharp
enum RarityTier { Common, Uncommon, Rare, Epic, Legendary }
```

Enum для label, читається в коді і логах. Розширюваний (новий tier — один рядок).

**Q6.3 — Per-module stat tables.** Кожен `*CoreDefinition` містить повну таблицю stats per tier:

```csharp
PayloadCoreDefinition {
  Id,
  Archetype,                 // "Ballistic", "Laser", "Foam", "Rocket"
  StatsByTier: Map<RarityTier, PayloadStats>
  //   Common    → { Damage: 10, Penetration: 5, BleedChance: 0.1 }
  //   Uncommon  → { Damage: 12, Penetration: 6, ... }
  //   ...
}

DeliveryCoreDefinition {
  Id, Pattern,
  StatsByTier: Map<RarityTier, DeliveryStats>
  //   Common → { FireInterval: 0.2, ProjectilesPerShot: 1, SpreadAngle: 3 }
  //   ...
}

PayloadCoreInstance {   // живе у InventoryItem.WeaponConfiguration
  DefinitionId,
  Rarity: RarityTier,
}
```

**Composition на equip:**
```csharp
Stats = Compose(
  payloadDef.StatsByTier[payloadInstance.Rarity],
  deliveryDef.StatsByTier[deliveryInstance.Rarity],
  exoticDef?.StatsModifier
)
```

**Чому per-module, а не глобальний multiplier:**
- Rarity = "та сама штука, але краща" — і що саме "краще" природно різне для різних модулів (Legendary Laser → faster charge, Legendary Ballistic → more damage)
- Дизайнер має повний контроль per-module, без обхідних шляхів для нестандартного масштабування
- Data volume — не проблема (10 модулів × 5 tiers × ~5 stats = 250 значень — малий ScriptableObject)

**Взаємодія з ammo modifiers:** Rarity живе в `Stats` (cached на equip). Ammo modifiers складаються в `ShootingSystem` на fire (як зараз). Конфлікту немає — це два окремих канали.

**Що це дає для Tier 0:**
- `*CoreInstance` = `{ DefinitionId, Rarity }`
- `*CoreDefinition` одразу має `StatsByTier` (навіть якщо в Tier 0 заповнено тільки Common)
- Composition pipeline знає як читати rarity → stats
- Tier 4 = заповнення таблиць числами + UI, не зміна архітектури

### 7. Міграція з hardcoded factories ✅ RESOLVED (2026-04-19)

**Підхід:** Фазована міграція. Існуючі зброї переводяться як pre-built weapon configurations на новій системі. Жодного паралельного code path — один pipeline, тимчасовий compat layer.

**Scope спрощення:** Shotgun видаляється з гри (один менше legacy + не треба міграції Scatter в Tier 2 для покриття існуючих зброї). Scatter Delivery лишається в scope Weapon Builder, але як нова поведінка, не міграція.

**Фази міграції:**

| Фаза | Що відбувається |
|------|-----------------|
| Tier 0 | Composition + assembly pipeline готові. `WeaponSyncSystem` працює з `WeaponConfiguration`. Rifle/Pistol лишаються через compat layer. Shotgun видалено з гри. |
| Tier 1 | Ballistic + Single-Action реалізується повноцінно. Pistol мігрує (видаляється `CreatePistol`, `"Pistol"` у compat layer стає WeaponConfiguration без хардкоду). |
| Tier 2 | Auto + Scatter реалізуються. Rifle мігрує на Ballistic+Auto. `CreateRifle` / `CreatePistol` повністю видалені. |
| Tier 2 end | Compat layer видаляється. Inventory items одразу мають `WeaponConfiguration`. |

**Compat layer (тимчасовий, Tier 0-2):**

```csharp
// temporary — WeaponSyncSystem
static readonly Dictionary<string, WeaponConfiguration> LegacyDefinitionToConfig = new() {
  ["Rifle"]  = new() { Payload = Ballistic/Common, Delivery = Auto/Common },
  ["Pistol"] = new() { Payload = Ballistic/Common, Delivery = Single/Common },
};
```

~5 рядків, явно позначено як temporary, видаляється в кінці Tier 2.

**Чому фазовано, а не все одразу:**
- Shooting range, armor tests, інші системи завжди мають працюючу зброю
- Існуючі зброї слугують continuous integration test для нової системи (Rifle стріляє як раніше → pipeline правильний)
- Один code path — нуль dual maintenance
- Guiding principle виконується: ми **ріжемо** (factories зникають до кінця Tier 2), просто фазовано щоб не блокувати паралельну роботу

**AmmoType binding — resolved:** `AmmoType` прив'язується до **Payload Core**, не до weapon archetype.

- Ballistic Round → Ammo_Rifle (один ammo type на payload)
- Laser Charge → energy cell
- Micro-Rocket → rocket ammo
- Adhesive Foam → foam canister

**Наслідок:** після міграції Rifle і Pistol ділять Ammo_Rifle (обидва — Ballistic). З дизайн-погляду це логічно: обидва це ballistic зброя, ammo має бути спільна.

---

## Приблизні контури нових систем

*Попередній список, буде уточнений.*

- `WeaponAssemblySystem` — валідація і композиція збірки з модулів
- `WeaponStatComposer` — обчислення фінальних стат (base + rarity + ammo)
- `DeliveryBehavior*` — per-delivery поведінки (Single-Action, Auto, Rotary, Swarm, Scatter)
- `PayloadBehavior*` — per-payload поведінки (якщо потрібні)
- `ExoticModHooks` — event-driven модифікатори
- `SlotCompatibilityRules` — data/rules про те, що з чим поєднується

---

## Hard rules ↔ architecture mapping

Перевірка, що архітектура не суперечить design.md §3 hard rules.

| Hard rule (design.md) | Архітектурна реалізація | Статус |
|-----------------------|-------------------------|--------|
| Рівно 1 Payload Core | `WeaponEntityState.PayloadCore` — non-nullable single | ✅ |
| Рівно 1 Delivery Core | `WeaponEntityState.DeliveryCore` — non-nullable single | ✅ |
| ≤1 Exotic Mod | `WeaponEntityState.ExoticMod?` — nullable single | ✅ |
| Rarity застосовується до Payload і Delivery | `*CoreInstance.Rarity` на Payload і Delivery; `ExoticModInstance` **без Rarity** | ✅ |
| Ліміти = slot structure + compatibility, не hidden budget | Data model Tier 4 (Q4) — відкладено, але архітектура composition не закладає budget | ⏳ consistent |
| Typed Attachments поза active scope | Не в поточній data model | ✅ |

---

## Tier 0 remaining details

Чотири великі архітектурні питання закриті. Але при комплексному ревʼю 2026-04-19 ми виявили **subsidiary details**, які треба закрити до старту коду Tier 0 — інакше вони заблокують імплементацію.

### Must-do before Tier 0 code

#### D1. Склад `WeaponStats` блоку ✅ RESOLVED (2026-04-20)

Кожне поле поточного `WeaponEntityState` віднесено до одного джерела — Payload або Delivery, без overlap.

**Payload contributes (8 stats):**
- `ProjectileDamage` — природа ураження
- `ProjectileSpeed` — ballistic vs laser vs rocket мають різну фізику польоту
- `ProjectileLifetime` — властивість projectile type
- `HeadshotDamageMultiplier` — Rocket = 0× (AoE не headshot'ить), Ballistic = 2× — природа payload
- `BasePenetration`, `BaseArmorDamage`, `BaseBleedChance` — characteristics снаряду
- `AmmoType` — identifier (не число), живе на `PayloadCoreDefinition`, **не в WeaponStats**

**Delivery contributes (13 stats):**
- `FireInterval`, `ProjectilesPerShot`, `SpreadAngle` — темп і геометрія pattern
- `ConeHalfAngle`, `BodyRotationSpeed`, `AimFollowSharpness` — weapon feel
- `RecoilKickForward`, `RecoilKickSide`, `RecoilRecoverySpeed` — recoil як функція pattern
- `EquipTime`, `UnequipTime` — важчий delivery = довший draw
- `MagazineSize`, `ReloadTime` — Auto→велика mag, Single→мала

**Runtime (не у WeaponStats):**
- `LastFireTime`, `Phase`, `PhaseStartTime`, `RecoilOffset`, `AmmoInMagazine`

**Композиція (рanitity вже враховується в `StatsByTier[rarity]`):**

```csharp
WeaponStats Compose(PayloadCoreInstance p, DeliveryCoreInstance d) {
  var ps = p.Definition.StatsByTier[p.Rarity];
  var ds = d.Definition.StatsByTier[d.Rarity];
  return new WeaponStats {
    // Payload (8)
    ProjectileDamage = ps.Damage,
    ProjectileSpeed = ps.Speed,
    ProjectileLifetime = ps.Lifetime,
    HeadshotDamageMultiplier = ps.HeadshotMult,
    BasePenetration = ps.Penetration,
    BaseArmorDamage = ps.ArmorDmg,
    BaseBleedChance = ps.Bleed,
    // Delivery (13)
    FireInterval = ds.FireInterval,
    ProjectilesPerShot = ds.ProjectilesPerShot,
    SpreadAngle = ds.SpreadAngle,
    ConeHalfAngle = ds.ConeHalfAngle,
    BodyRotationSpeed = ds.BodyRotationSpeed,
    AimFollowSharpness = ds.AimFollowSharpness,
    RecoilKickForward = ds.RecoilKickForward,
    RecoilKickSide = ds.RecoilKickSide,
    RecoilRecoverySpeed = ds.RecoilRecoverySpeed,
    EquipTime = ds.EquipTime,
    UnequipTime = ds.UnequipTime,
    MagazineSize = ds.MagazineSize,
    ReloadTime = ds.ReloadTime,
  };
}
```

**Три окремі канали stats при стрільбі:**

1. **Weapon base** (cached у `WeaponStats` на equip) — перевіряє базові значення
2. **Ammo modifier** (+Penetration/+ArmorDamage/+BleedChance) — складається в `ShootingSystem` на fire (як зараз, не чіпаємо)
3. **Exotic modifier** — застосовується в pipeline Tier 5, не в базовій композиції

**Sanity checks:**
- Ballistic+Auto vs Ballistic+Single — однакові damage/pen/bleed (Payload), різні FireInterval/mag/recoil (Delivery) ✅
- Laser+Auto vs Ballistic+Auto — однакові FireInterval/mag/recoil, різні damage/speed/bleed ✅
- Rocket+Single — HeadshotMult=0 (Payload rule) ✅

#### D2. Stats structure для різнорідних Payloads ✅ RESOLVED (2026-04-20)

**Рішення:** Common stats у base + payload-specific у typed subclasses.

`PayloadCoreDefinition` стає abstract base з 4 subclass'ами:

```csharp
abstract class PayloadCoreDefinition {
  Id,
  Archetype,  // "Ballistic" | "Laser" | "Rocket" | "Foam"
  AmmoType,
  StatsByTier: Map<RarityTier, CommonPayloadStats>  // 8 common fields
}

class BallisticPayloadDefinition : PayloadCoreDefinition {
  // no specific stats
}

class LaserPayloadDefinition : PayloadCoreDefinition {
  SpecificByTier: Map<RarityTier, LaserSpecificStats>
  // LaserSpecificStats { ChargeTime }
}

class RocketPayloadDefinition : PayloadCoreDefinition {
  SpecificByTier: Map<RarityTier, RocketSpecificStats>
  // RocketSpecificStats { ExplosionRadius }
}

class FoamPayloadDefinition : PayloadCoreDefinition {
  SpecificByTier: Map<RarityTier, FoamSpecificStats>
  // FoamSpecificStats { SlowDuration, StickDuration }
}
```

**Handlers касят definition до свого типу:**

```csharp
static void HandleLaserCharge(WeaponEntityState w, ...) {
  var def = (LaserPayloadDefinition)w.PayloadCore.Definition;
  var specific = def.SpecificByTier[w.PayloadCore.Rarity];
  float chargeTime = specific.ChargeTime;
  // ... charge-up logic
}
```

**Чому subclass + cast (не flat nullable / union):**
- Type-safe — Ballistic handler не може прочитати `ExplosionRadius`
- Explicit over implicit (guiding principle) — cast явно каже "я знаю що це Laser"
- Extensibility — новий payload = новий subclass, `WeaponStats` не чіпаємо
- Немає boxing — немає interface у value struct
- SO-friendly — Unity ScriptableObject підтримує наслідування нативно (важливо для D3)

**Payload-specific + Exotic interaction:** Exotic модифікатор для специфічних полів (напр. "Overcharge" halves ChargeTime) застосовується **в handler'і** — handler знає і specific stat, і exotic context:

```csharp
float GetEffectiveChargeTime(WeaponEntityState w) {
  var def = (LaserPayloadDefinition)w.PayloadCore.Definition;
  var base = def.SpecificByTier[w.PayloadCore.Rarity].ChargeTime;
  return ApplyExoticChargeModifier(base, w.ExoticMod);
}
```

**Delivery без subclass'ів:** всі 5 deliveries мають однаковий shape stats (SpinUpTime/VolleyCount зараз "delivery-common" під той самий `FiringPattern` dispatch). Якщо в майбутньому знадобиться — можна симетрично застосувати той самий патерн до Delivery.

#### D3. ScriptableObject для `*CoreDefinition` ✅ RESOLVED (2026-04-20, amended 2026-04-20)

**Рішення:** `PayloadCoreDefinition`, `DeliveryCoreDefinition`, `ExoticModDefinition` — **ScriptableObject** (abstract base + subclass'и для Payload).

**Layout amendment:** Проект не використовує feature-folders — `ItemDefinition`, `QuestDefinition`, entity states лежать плоско у `Assets/Scripts/State/`. Наш `*CoreDefinition` слідує цій конвенції.

```
Assets/Scripts/State/
  PayloadCoreDefinition.cs       (abstract SO base)
  BallisticPayloadDefinition.cs
  LaserPayloadDefinition.cs
  RocketPayloadDefinition.cs
  FoamPayloadDefinition.cs
  DeliveryCoreDefinition.cs
  ExoticModDefinition.cs

Assets/Resources/WeaponBuilder/        (feature-folder OK для asset organization)
  Payloads/
    BallisticRound.asset
    LaserCharge.asset
    MicroRocket.asset
    AdhesiveFoam.asset
  Deliveries/
    SingleAction.asset
    Auto.asset
    Scatter.asset
    Rotary.asset
    Swarm.asset
  Exotics/
    Ricochet.asset, SplitOnImpact.asset, ...
  CoreDefinitionDatabase.asset          (central aggregator; see Loading below)
```

**Authoring:** дизайнер редагує `.asset` у Unity Inspector. Кожен subclass має `[CreateAssetMenu(menuName="Weapon Builder/Payload/Laser")]`.

**StatsByTier serialization:** Unity не серіалізує `Dictionary<>` з коробки. Зберігаємо як масив, індексований enum:

```csharp
[SerializeField] CommonPayloadStats[] _statsByTier; // length = 5 (per RarityTier)
public CommonPayloadStats StatsByTier(RarityTier t) => _statsByTier[(int)t];
```

`OnValidate` у SO тримає масив довжиною рівно 5 (resize if needed).

**Loading (amended 2026-04-20):** Використовуємо **central `CoreDefinitionDatabase` SO** (як `QuestDatabase` pattern), а не `Resources.LoadAll`:

- `CoreDefinitionDatabase` — одинокий SO asset з полями `List<PayloadCoreDefinition> _payloads`, `List<DeliveryCoreDefinition> _deliveries`, `List<ExoticModDefinition> _exotics`
- Дизайнер додає references на payload/delivery/exotic assets у Database Inspector явно
- `ICoreDefinitionRegistry` wrap'ить Database і будує lookup dictionaries (BuildIndex)
- Systems отримують registry через `RaidContext`

**Чому Database, а не `Resources.LoadAll`:**
- Explicit — phantom SO assets у Resources не потрапляють у registry випадково
- No Resources build bloat для всіх assets (тільки Database у Resources, решта — downstream refs)
- Консистентно з `QuestDatabase` — existing project pattern
- Simpler hot-reload — Database refresh'ить індекс, не скан Resources

**Чому SO:**
- Authoring workflow: 4 Payloads × 5 tiers × 7 common + specific stats — багато числових даних, Inspector природний
- VFX/SFX asset refs (prefabs, AudioClips) перетягуються прямо в поля
- Консистентно з `QuestDefinition` pattern
- Hot-reload у Inspector — швидкий iteration time

**Testing:** `ScriptableObject.CreateInstance<T>()` + helper-builder типу `TestPayloadDefinitions.MakeBallistic(damage: 10, ...)`.

#### D4. Value semantics для `*CoreInstance` ✅ RESOLVED (2026-04-20)

**Рішення:** `readonly struct` з `[Serializable]` і public readonly fields.

```csharp
[Serializable]
public readonly struct PayloadCoreInstance : IEquatable<PayloadCoreInstance> {
  public readonly string DefinitionId;
  public readonly RarityTier Rarity;

  public PayloadCoreInstance(string definitionId, RarityTier rarity) {
    DefinitionId = definitionId;
    Rarity = rarity;
  }

  public bool Equals(PayloadCoreInstance other) =>
    DefinitionId == other.DefinitionId && Rarity == other.Rarity;
}

[Serializable]
public readonly struct DeliveryCoreInstance : IEquatable<DeliveryCoreInstance> { ... }

[Serializable]
public readonly struct ExoticModInstance : IEquatable<ExoticModInstance> {
  public readonly string DefinitionId;
  // no rarity (hard rule)
}
```

**У composition:** `ExoticModInstance?` — nullable value type (C# native, Unity-friendly).

**Чому readonly struct:**
- Value semantics: instance = value, копіюється при assign, не shares mutation
- Zero GC pressure — `WeaponConfiguration` часто копіюватиметься між inventory layers
- Unity serialization природна (public fields + `[Serializable]`)
- Immutable by design: змінити rarity = створити новий instance
- Equality structural
- `readonly record struct` приваблива, але має Unity serialization gotcha'і — чистий readonly struct безпечніше

**Instance тримає тільки `DefinitionId`, НЕ ref на Definition:**
- CLAUDE.md rule 6: "State stores values and IDs only" ✅
- Serialization: save file зберігає string IDs
- Hot-reload: definitions можуть бути перезавантажені без інвалідації instances

**Resolution:**
```csharp
var def = registry.GetPayloadDefinition(instance.DefinitionId);
// handler cast:
var laserDef = (LaserPayloadDefinition)def;
```

Опційно — extension method для зручності (не блокуючий для Tier 0):
```csharp
public static T Definition<T>(this PayloadCoreInstance instance, ICoreDefinitionRegistry reg)
  where T : PayloadCoreDefinition
  => (T)reg.GetPayloadDefinition(instance.DefinitionId);
```

#### D5. ExoticMod без rarity — явно зафіксовано

✅ Зафіксовано у composition shape (див. §1) і у hard rules mapping вище.

### Should-do before Tier 1 code

#### D6. Re-assembly triggers

Коли саме запускається composition pipeline?
- On equip (зрозуміло).
- Гравець редагує збірку на базі поки equipped — re-equip автоматично? Disable editing? Explicit "Apply"?
- A: _TBD (питання Tier 1 UX)_

#### D7. Invalid configuration handling

Що робить Assembly System якщо `WeaponConfiguration` invalid (не існує definitionId, несумісні cores, пошкоджений save)?
- Exception → crash
- Fallback на порожню збірку / null
- Logged warning + best-effort partial assembly
- A: _TBD (потрібен мінімальний graceful-failure шлях у Tier 0)_

#### D8. Archetype label system

Як генерується назва архетипу з `(PayloadCore, DeliveryCore)`?
- Hardcoded lookup table `(payloadArch, deliveryArch) → label`
- Шаблон "{PayloadName} {DeliveryName}"
- Per-combination override + шаблон fallback
- A: _TBD_

### Tracked but not blocking

#### D9. RaidContext / ports integration
`*CoreDefinition` — config, має потрапляти в systems через RaidContext (CLAUDE.md §4). Новий port `ICoreDefinitionRegistry` чи розширення існуючого.

#### D10. Raid State Debugger update
CLAUDE.md §5.7 — після refactor'у `WeaponEntityState` треба оновити `RaidStateDebuggerWindow`.

#### D11. DevCheats extension
Rarity multipliers, spin-up times, volley intervals — мають бути DevCheats параметрами (Tier 2+).

---

## Open risks

### R1. Tier 0 scope expansion
Work items Tier 0 зросли з 5 до 12 (додались Shotgun removal, read-site refactor, compat layer, Debugger update). Якщо estimation показує >> 2 тижнів — варто розділити Tier 0 на **0a (data model + types)** і **0b (migration + debugger)**.

### R2. Laser Charge state machine (Tier 2)
Charge-up потребує або нової phase `Charging`, або substate всередині `Firing`, або окремого runtime поля `ChargeLevel`. Рішення — Tier 2, але попередити.

### R3. Multi-projectile × custom Payload (Tier 2)
Ballistic+Scatter = 7 звичайних куль. Laser+Scatter = 7 лазерних променів. Треба впевнитись, що pipeline допускає N projectiles × custom payload behavior.

---

## Related docs

- [design.md](./design.md) — дизайн-спека фічі
- [../weapons.md](../weapons.md) — поточна система зброї
- [../architecture.md](../architecture.md) — загальна архітектура проекту
