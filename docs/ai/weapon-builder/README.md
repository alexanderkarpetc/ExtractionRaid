# Weapon Builder

Системна фіча кастомізації зброї для extraction shooter. Поточна версія дизайну: **v0.7**.

> **Status (2026-05-01):** Foundation done (Tiers 0-2) + UX Pass 1 done + **Tier 6 done** (A/B/D/E/F shipped; G7 deferred) + **Tier 8 done (Waves A-E)**. 📋 Next: Tier 3 (content expansion — Foam/Rocket/Rotary/Swarm). See [plan/roadmap.md](./plan/roadmap.md#execution-sequence-поточний-план-виконання) for full execution sequence.

---

## Quick resume для нової сесії

Якщо ти повертаєшся до фічі, читай у цьому порядку:

1. **Цей файл** — стан, що працює, що зроблено (UX Pass 1 outcome зафіксовано нижче)
2. [plan/status.md](./plan/status.md) — decisions log, pause summary, наступні tier'и
3. [plan/ux-improvements.md](./plan/ux-improvements.md) — completed UX pass record (acceptance gate done)
4. [architecture.md](./architecture.md) — якщо потрібно глибше у код: rationale Q1-7 / D1-14
5. [design.md](./design.md) — design intent
6. [plan/roadmap.md](./plan/roadmap.md) — повний tier roadmap (Tier 3-7)
7. [plan/tasks.md](./plan/tasks.md) — Tier 0-2 task records (історичний reference)

**Короткий entry point:** `Tools → Weapon Builder → Create Stub Assets` у Unity Editor відновлює всі SO assets, якщо їх немає локально.

---

## Current state (2026-04-27)

### Tier progress

| Tier | Scope | Status |
|------|-------|--------|
| **0a** Data model foundation | Types, SOs, registry, DB | ✅ complete (2026-04-20) |
| **0b** Migration | State refactor, assembly pipeline, Shotgun + factories видалено | ✅ complete (2026-04-22) |
| **1** Vertical slice | Workbench, Builder UI (UI Toolkit), DevCheats, Ballistic+Pistol E2E | ✅ complete (2026-04-23) |
| **2** Core breadth | +Laser (charge-up), +Scatter, 6 archetypes | ✅ complete (2026-04-23) |
| **UX Pass 1** | Builder D&D rewrite, universal tooltip system, inventory archetype labels, ammo auto-grant, resolution scaling | ✅ complete (2026-04-27) |
| **6** Loot / Inventory integration | Waves A/B/D/E/F done (modules drop у containers); G7 deferred sine die | ✅ done |
| **8** 3D Modular Visualization | Modular weapon meshes (runtime composition, attachment sockets); Waves A-E done з symmetric pivot. Wave F (icons) deferred — blocked on UI prereq | ✅ done (Wave F → future track) |
| **3** Content expansion | +Foam, +Rocket, +Rotary, +Swarm | ⏳ planned |
| **4** Rarity + Slots | Per-tier stat values, banned combos, bot weapon migration | ⏳ planned |
| **5** Exotic Mods | 5 Exotic mods via hook system | ⏳ planned |
| **9** VFX / SFX Language | Per-payload/delivery/exotic visual + audio language, hit feedback polish | ⏳ planned |
| **10** Weapon Feel Polish | Iterative tuning loop — fire interval, recoil, charge timing, animation polish, balance | ⏳ planned |
| ~~**7**~~ | ~~Polish (Art/VFX, UX, balance)~~ — **deprecated, split into 8/9/10** | — |

**Tier numbers = stable IDs.** Execution order ≠ tier number order — see [plan/roadmap.md](./plan/roadmap.md#execution-sequence-поточний-план-виконання) for rationale. Currently: 6 → 8 → 3 → 4 → 5 → 9 → 10.

**Test coverage:** ~120 зелених тестів (90 з foundation + ~30 з UX Pass 1: WeaponDisplayName, Tooltip builders, presenter extensions).

### Що працює у грі прямо зараз

**Player flow:**
1. Гравець у Hideout підходить до Workbench object → натискає `E` (prompt: "Weapon Workbench · Press E")
2. Відкривається Weapon Builder modal (UI Toolkit): palette of module **cards** + 2 drop **slots** (Payload, Delivery) + live preview (archetype, charge hint якщо Laser, stat groups) + read-only **backpack panel** для context
3. **Drag&drop:** перетягнути card з palette → drop на typed slot. Wrong type → red border, silent reject. Filled slot → replace
4. **Click fallback:** клік на card теж selects (toggle off повторним кліком на selected)
5. Hover на card / slot / backpack item → **tooltip** з композицією і stats
6. Build → новий `ItemState` (з WeaponConfiguration) + auto-grant `2× MagazineSize` matching ammo лендають у backpack
7. Close → control повертається. Equip у hotbar → weapon готов. Inventory показує **archetype label** ("Laser Pistol") замість generic "Weapon"
8. Shoot — стріляє згідно з assembled stats. Disabled Build button показує tooltip з причиною

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

## Workflow: додавання нового модуля (Tier 3+ content)

**Крок 0** — design stats / behavior — поза scope цього документа.

**Крок 1.** Створи SO asset:
- Payload: `Assets/Resources/WeaponBuilder/Payloads/<Name>.asset` (через Create Asset menu, тип `BallisticPayloadDefinition` / `LaserPayloadDefinition` / `FoamPayloadDefinition` / `RocketPayloadDefinition`)
- Delivery: `Assets/Resources/WeaponBuilder/Deliveries/<Name>.asset` (тип `DeliveryCoreDefinition`)

**Крок 2.** Заповни stats у Inspector (Common tier мінімум — інші rarity у Tier 4).

**Крок 3.** Додай SO у `CoreDefinitionDatabase.asset` array (`Payloads` або `Deliveries`).

**Крок 4 ⭐** — `Tools → Weapon Builder → Create Module Prefabs`:
- Auto-generates primitive 3D placeholder prefab за canonical path (`Resources/Prefabs/Modules/Module_Payload_<Id>.prefab` або `Resources/Prefabs/Weapons/Weapon_<Id>.prefab`)
- Wires SO's `_attachmentPrefab` / `_weaponPrefab` reference
- For new deliveries — створює full hand prefab з `WeaponView` component, `Animator`, `DeliveryBody`, `MuzzlePoint`, `RightHandGrip`, `PayloadMount` already wired
- **Idempotent** — re-run never duplicates (skips already-wired SOs)

**Крок 5 (optional, artist drop-in).** Replace primitive content of prefab з real mesh (відкрити prefab у editor, edit children) — code/SO setup не торкається. Position adjustments на `PayloadMount` / `MuzzlePoint` per-prefab у Inspector.

> **Якщо Крок 4 пропустиш** — SO буде у Builder UI, але equip → ghost-weapon path / no visible weapon mesh. Утіліта закриває цей gap для будь-яких нових модулів.

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

## Що ще треба зробити (in execution order)

### Tier 8: 3D Modular Visualization (active 2026-04-30)
- ✅ **Wave A** — pipeline refactor: SO-driven prefab refs (DeliveryCore.WeaponPrefab, PayloadCore.AttachmentPrefab)
- ✅ **Wave B (symmetric pivot)** — replaced asymmetric "delivery=full body, payload=tiny attachment" з symmetric "delivery=Mod_Body, payload=Mod_Barrel" using PolygonApocalypse modular parts
- ✅ **Wave C** — cover existing 6 archetypes (Pistol/Rifle/Shotgun × Ballistic/Laser); Shotgun fallback gap closed
- ✅ **Wave D** — procedural recoil kick на Fire (replaces stale Mecanim clips); payload's optional Animator works independently
- ✅ **Wave E** — `Tools → Weapon Builder → Create Module Prefabs` editor utility — auto-creates primitive prefabs for new SOs (Tier 3 drop-in path)
- ⏸ **Wave F deferred sine die** — backpack composite icons. Blocked on UI prereq: current uGUI inventory rendering не підтримує 2-image composite. Re-engage коли inventory layer оновлений. **Не блокує Tier 8 closure.**

**Open follow-ups (з Tier 8):**
- Muzzle alignment for symmetric meshes — окрема ітерація після Wave C
- Reload/Equip/Unequip procedural motion — Tier 9
- Mecanim controller stale clip cleanup — Tier 9 housekeeping
- PayloadMount/MuzzlePoint per-prefab tuning — manual у Inspector

### Tier 6: Loot / Inventory integration ✅ done (2026-05-01)
- ✅ Wave A — side-by-side launch (Builder + uGUI inventory)
- ✅ Wave B — modules-as-items + DevCheats grant ("Spawn Module" / "Spawn All Modules")
- ✅ Wave D — build cost (TryBuild consumes 1×payload + 1×delivery from backpack; CanBuild gates; DisabledReason explains missing module)
- ✅ Wave E — palette filter (`IsModuleAvailable` + `wb-card-unavailable` USS class for grayed-out look)
- ✅ Wave F — loot economy (RandomLootBox + new ModuleCache ContainerType; modules drop у raid containers)
- ⏸ Wave G (G7 initial loadout) — **deferred sine die** (DevCheats + loot cover playtest needs)

### Content (Tier 3)
- Payload: **Foam** (status effects: slow + stick), **Rocket** (AoE explosion, ExplosionRadius)
- Delivery: **Rotary** (SpinningUp phase + state machine extension), **Swarm** (VolleyActive phase, serial burst)
- Full 4×5 матриця = 20 archetypes

### Systemic (Tier 4)
- Rarity: fill `StatsByTier` для Uncommon → Legendary, UI dropdown для rarity selection, balance pass
- Slot compatibility: banned combos matrix, UI feedback
- Bot weapons: мігрувати BotSpawnSystem на assembly pipeline
- Rarity visual tint на cards + inventory items

### Feature (Tier 5)
- Exotic Mods × 5, hook system (OnFire / OnHit / OnKill / OnProjectileUpdate)

### Tier 9: VFX / SFX Language
- Per-Payload VFX (Ballistic muzzle+tracer+spark, Laser beam+glow+burn, Rocket missile+AoE, Foam splat+slow zone)
- Per-Delivery VFX (Rotary spinup, Swarm volley flash sequence, Scatter cone)
- Per-Exotic VFX (Ricochet sparks, Split fork, Boomerang trail)
- SFX library (charge sound, fire variants, reload variations)
- Hit feedback polish (screen shake, hit pause, damage numbers)

### Tier 10: Weapon Feel Polish
- Iterative playtest tuning: fire interval, recoil, charge timing, reload pace
- Animation polish, sound design integration
- Damage curves vs armor balance pass
- Comprehensive playtest sweep (no archetype dominance / dead-on-arrival)

### Відкладено з минулого
- `.cursor/rules/weapon-builder*.mdc` counterpart (per CLAUDE.md §7) — не зроблено
- Update `docs/ai/weapons.md` — зараз застаріла, описує pre-migration Rifle/Shotgun/Pistol
- Weapon view prefabs (Weapon_Shotgun було видалено — нового mesh для Scatter formfactor немає, fallback на Weapon_Rifle) — буде закрите Tier 8

---

## UX Pass 1 — outcome (2026-04-27)

Фокусний UX polish над foundation. Перетворив dropdown-based Builder з generic-name inventory у drag-and-drop card UX з hover tooltips і archetype labels у всьому інвентарі.

**Зроблено (4 passes, 4 commits):**

| Pass | Tasks | Highlights |
|------|-------|------------|
| 1 | Charge hint, disabled Build tooltip, Workbench prompt | Confusion fixes, presenter exposes `PreviewRequiresCharge` / `PreviewChargeTime` / `DisabledReason` |
| 2 | `WeaponDisplayName` helper + 2 inventory callsites, auto-grant ammo on Build | Inventory shows "Ballistic Pistol" / "Laser Rifle" замість "Weapon"; Laser-trap fix (2× mag of correct ammo type) |
| 3 | Universal tooltip system (`TooltipModel` + 3 builders + UI Toolkit overlay) | Cross-stack: uGUI inventory hover → UI Toolkit overlay panel. Hooked у InventorySlotView/EquipmentSlotView |
| 4 | Builder D&D rewrite (palette + slots + ghost + backpack context) | UXML/USS rewrite, presenter unchanged. Drag&drop intra-stack у UI Toolkit. Read-only backpack visible inside Builder |

**Нові архітектурні артефакти (reusable):**
- `Systems/WeaponDisplayName.For(item, registry)` — будь-яке inventory rendering показує archetype
- `View/UI/Tooltip/TooltipController.Instance` — view-singleton, `Show` (uGUI bottom-left coords) / `ShowFromPanel` (UI Toolkit top-left coords)
- 3 tooltip builders: `Item`, `Weapon`, `Module` (для Payload/Delivery cards)
- `WeaponBuilder/Elements/{ModuleCardElement, ModuleSlotElement, BackpackItemElement}` — UI Toolkit primitives
- `docs/ai/ui-styling.md` (+ `.cursor/rules` mirror) — Tier A/B sizing + sort orders + color palette

**Нові файли:**
- `Assets/Scripts/Systems/WeaponDisplayName.cs`
- `Assets/Scripts/View/UI/Tooltip/{TooltipModel, TooltipController}.cs` + `Builders/{Item,Weapon,Module}TooltipBuilder.cs`
- `Assets/Scripts/View/UI/WeaponBuilder/Elements/{ModuleCardElement, ModuleSlotElement, BackpackItemElement}.cs`
- `Assets/Resources/UI/Tooltip/TooltipOverlay.{uxml,uss}` + `TooltipPanelSettings.asset`
- `Assets/Scripts/Editor/TooltipAssetsBootstrap.cs`
- `docs/ai/ui-styling.md`

**Тести +28:** WeaponDisplayName (7), Tooltip builders (14), Presenter extensions (10), end-to-end fix (1 retro). Загалом ~120 зелених.

**Out-of-scope (свідомо deferred):**
- Edit existing weapon mode — не реалізується
- Module cards як real loot items у backpack — це Tier 6 work
- Build feedback toast / fade-in animations — Tier 7 polish
- Rarity tint на cards / inventory — Tier 4
- Decision support callouts ("you already have this build") — out of scope per design

**Backpack source у Builder = `App.Instance.Player.Inventory.Backpack` (read-only).** Drag-from-backpack додасться коли Tier 6 переведе модулі у форму items.

---

## Навігація

### Дизайн
- [design.md](./design.md) — поточний дизайн-док v0.7, source of truth по фічі

### Архітектура та імплементація
- [architecture.md](./architecture.md) — технічна архітектура, усі resolved rationale

### План та статус
- [plan/status.md](./plan/status.md) — decisions log, pause summary, next-tier guidance
- [plan/roadmap.md](./plan/roadmap.md) — tier structure + exit criteria
- [plan/tasks.md](./plan/tasks.md) — Tier 0-2 task records (historical)
- [plan/ux-improvements.md](./plan/ux-improvements.md) — UX Pass 1 record (completed 2026-04-27)

### UI styling
- [docs/ai/ui-styling.md](../ui-styling.md) — Tier A/B sizing, color palette, sort orders (created у UX Pass 1)

---

## Принципи організації документації

- **Концептуальні доки** (design, architecture, per-module specs) живуть довго і описують систему
- **Фазові/планові доки** (status, roadmap, tasks) живуть час реалізації і фіксують прогрес
- **Нові доки створюються в міру потреби**, а не заздалегідь — порожні файли лише створюють шум
- Коли з'являться per-module spec'и (payload-cores.md, delivery-cores.md тощо) — вони ляжуть у `modules/` і `systems/` відповідно
