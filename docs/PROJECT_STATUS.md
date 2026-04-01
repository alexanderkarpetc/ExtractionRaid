# ExtractionRaid — Актуальний стан проекту

**Дата оновлення:** 2026-04-01
**Двигун:** Unity 6 (6000.3.10f1), URP, Input System
**Жанр:** Top-down extraction shooter (ref: Escape from Duckov)

---

## 1. Архітектура

5-шарова архітектура з однонаправленим потоком даних:

```
Input → Adapter → Context → System → State → Presenter → View
                                         ↘ EventBuffer ↗
```

| Шар | Відповідальність | Стан |
|-----|-------------------|------|
| **App** (Composition Root) | `App.Instance` singleton, bootstrap, запуск сесій | Готово |
| **Session** | `RaidSession` — гейм-луп, tick ordering; `Player` — профіль | Готово |
| **Systems** | 28+ stateless static систем, детермінований tick order | Готово |
| **Adapters** | 6 інтерфейсів (Input, Physics, NavMesh, Time, GrenadePosition, Events) | Готово |
| **View/Presenter** | 7 presenters + 12 IMGUI overlays, без геймплейної логіки | Готово |

**Tick Order (RaidSession.Tick):**

```
── Pre-movement ──────────────────────────
ADS state + blend (inline)
StaminaSystem               // sprint drain/regen
RollSystem                  // dodge roll FSM
MovementSystem              // player locomotion

── Weapon pipeline ───────────────────────
WeaponSyncSystem            // syncs weapon from inventory
WeaponEquipSystem           // PendingHotbarSlot intent
WeaponStateMachineSystem    // 6-phase FSM
AimingSystem                // dual-layer aim + recoil decay

── Consumables + status effects ──────────
QuickSlotSystem             // quick slot activation
GrenadeSystem               // throw + trajectory
MedkitSystem                // healing
StatusEffectSystem           // bleed L1/L2 ticks
BandageSystem               // bleed cure

── Combat ────────────────────────────────
ShootingSystem              // fire + ammo + recoil kick

── AI ────────────────────────────────────
PlayerFOVSystem             // visibility queries
BotPerceptionSystem         // vision/hearing/alert
BotBrainSystem              // behavior tree tick
BotMovementSystem           // NavMesh locomotion
BotCombatSystem             // fire/heal/grenades

── Resolution ────────────────────────────
ProjectileSystem            // movement + lifetime
GrenadeSystem.TickExplosions // detonation + area damage
DamageSystem                // armor → HP → bleed → events
ProcessCollisions / ProcessDamageAlerts / ProcessDeathEvents

── Interaction ───────────────────────────
NPC / Deploy / Craft / Loot / GroundItem pickup (inline)
```

**31 тип подій** у `RaidEventBuffer` (zero-alloc після прогріву).

**Кодова база:**

| Категорія | Файлів | Опис |
|-----------|--------|------|
| App | 4 | Bootstrap, App, GameLauncher, LaunchOptions |
| State | 25+ | Entities, inventory, health, armor, quests, signals |
| Systems | 28+ | Player, bot AI, combat, loot, crafting, quests |
| Systems/Bot | 12+ | BT framework (5 node types) + 6 action nodes + builder |
| Adapters | 12 | 6 інтерфейсів + Unity-імплементації |
| View/Presenter | 30+ | 7 presenters, 12 overlays, FoW, VFX helpers |
| Constants | 10 | Armor, Bots, Containers, Craft, Dodge, Grenade, Items, Med, Stamina, StatusEffects |
| Dev | 14 sections | DevCheats SO-based архітектура |
| Tests | 20 файлів | ~287 unit-тестів (EditMode) |
| **Всього** | **~171** | C# скриптів |

---

## 2. Зведена таблиця фіч

### COMBAT

| Фіча | Стан | Деталі |
|-------|------|--------|
| Зброя (3 типи) | Готово | Rifle, Shotgun, Pistol з повним FSM (6 фаз) |
| Weapon FSM | Готово | Ready → Firing → Cooldown → Equipping → Unequipping → Reloading |
| Патрони / перезарядка | Готово | Магазин + резерв з бекпаку, auto-reload, dry fire |
| Типи патронів | Готово | Standard, AP, HP — по 3 калібри = 9 типів |
| Dual-layer aiming | Готово | Raw (instant) + Weapon (smoothed), aim split toggle |
| ADS | Готово | Зменшує gap/bloom, віддачу, швидкість руху, zoom |
| Віддача | Готово | Forward kick + side scatter, exponential recovery, ADS reduction |
| Convergence aiming | Готово | Parallax blend + aim-up для headshot detection |
| Headshot system | Готово | TargetedEntityId detection, multiplier per weapon (Rifle 2×, Pistol 2.5×, Shotgun 1.5×) |
| Armor system | Готово | Hyperbolic pen curve K/(K+diff), K=30 |
| Armor durability | Готово | Parabolic decay нижче 70%, break at 0 |
| Helmet ricochet | Готово | 40% при pen < armor, 0 HP dmg, 2× dur dmg |
| Armor break VFX | Готово | Helmet fly-off (фізика), ArmorBroken event |
| Impact VFX | Готово | 5 типів: Body/Head/Bullet/Armor/Ricochet, proportional scaling |
| Damage numbers | Готово | Floating, 4 trajectory modes, color-coded |
| Hit markers | Готово | Proportional: size/color scales by absorptionRatio; ricochet blue spark |
| Гранати | Готово | Фізична траєкторія, fuse 3.5с, 120 dmg, 5м radius, LOS check |
| Grenade trajectory UI | Готово | Overlay з передбаченням дуги |
| Damage pipeline | Готово | headshot → ricochet → armor → HP → bleed roll → events |

### PLAYER MECHANICS

| Фіча | Стан | Деталі |
|-------|------|--------|
| Рух | Готово | 5 м/с base, NavMesh clamping |
| Спринт | Готово | StaminaSystem: drain 20/с, regen 15/с після 1с delay, 1.6× speed |
| Стаміна | Готово | Max 100, cant sprint at 0, blocked during ADS/roll/hands busy |
| Ухилення (Roll) | Готово | 0.5с, 10.4 м/с, кулдаун 0.8с, повна невразливість |
| Здоров'я | Готово | 100 HP max, God Mode чит |
| Кровотеча | Готово | 2 рівні: L1 (3 HP/с), L2 (6 HP/с), per-shot bleed roll |
| Бандаж | Готово | Downgrade L2→L1→clear, 3с cast, interruptible |
| Медкіт | Готово | 2с delay + 15 HP/с continuous heal, stack consumption |
| Quick Slots | Готово | 7 слотів, прив'язка до бекпаку, auto-clear при витрачанні |

### INVENTORY & ITEMS

| Фіча | Стан | Деталі |
|-------|------|--------|
| Інвентар | Готово | 20 слотів бекпак + 2 зброї + шолом + броня |
| Weapon Hotbar | Готово | 2 слоти зброї, переключення 1-2 |
| Quick Slot Bindings | Готово | 7 bindings → backpack index, keys 3-9 |
| Item stacking | Готово | 2-phase merge (existing stacks → free slots) |
| Підбір / дроп | Готово | 3м range, auto-merge, GroundItemState |
| Equipment system | Готово | Sync inventory ↔ ArmorMap, WriteBackDurability |
| Item tooltip | Готово | Armor/ammo stat display |
| 38+ предметів | Готово | Зброя, броня, патрони (9), розхідники, крафт-матеріали (13), weapon mods |
| Crafting | Готово | 21 рецепт (4 категорії: Meds, Weapons, Ammo, WeaponMods) |

### AI & BOTS

| Фіча | Стан | Деталі |
|-------|------|--------|
| Behavior Tree | Готово | Selector/Sequence/Condition/Cooldown, cached per type |
| 6 Action Nodes | Готово | Patrol, Chase, Shoot, Dodge, Heal, ThrowGrenade |
| Perception | Готово | Vision (range+angle+LOS), hearing, damage alert, memory timer |
| Bot combat | Готово | Accuracy spread, fire rate, weapon per type |
| Bot healing | Готово | Emergency (30% HP, 1.5с delay) + safe (50% HP, 3с delay, not in combat) |
| Bot dodging | Готово | Perpendicular to player, cooldown-gated |
| Bot grenades | Готово | Ballistic throw, delay, inventory consumption |
| Corpse loot | Готово | Weapon + ammo + armor (з поточним durability) + medkits + grenades |
| 12 типів ботів | Готово | 3 combat (Scav/PMC/Boss) + 9 shooting range targets |

### LOOT & ECONOMY

| Фіча | Стан | Деталі |
|-------|------|--------|
| Лут-контейнери | Готово | 3 типи: MedContainer, AmmoBox, RandomLootBox |
| Ground items | Готово | Spawn, pickup, despawn |
| Interactables | Готово | Lootables, ground items, workbenches, deploy points, NPCs |
| Quests | Готово | Accept/complete, 6 task types, NPC offering, rewards |
| Crafting materials | Готово | 13 типів (Adhesive, Metal_Parts, Electronics, etc.) |

### VISUAL SYSTEMS

| Фіча | Стан | Деталі |
|-------|------|--------|
| Fog of War | Готово | 5-stage pipeline, DX12 compatible, edge-finding |
| Crosshair | Готово | 8 станів, bloom, reload ring, ADS crosshair, proportional hit markers |
| Health bars | Готово | Dota 2-style segments, trail, flash, shake |
| Armor bar | Готово | Stripe above HP (helmet left / body right) |
| Defender HUD | Готово | Color-coded durability (green/yellow/red pulse), break overlay |
| Weapon animations | Готово | Mecanim, speed sync від state duration |
| Armor visuals | Готово | Meshes на Helmet01/Spine02 bones, helmet fly-off |
| Damage numbers | Готово | 4 trajectory modes, absorption-scaled |

### INFRASTRUCTURE

| Фіча | Стан | Деталі |
|-------|------|--------|
| DevCheats | Готово | 14 SO-based секцій, 50+ параметрів, Editor window |
| Debug tools | Готово | RaidState Debugger, BT Debugger, Quest Editor (4 files), Raid Tools Menu |
| Tests | Готово | 287 тестів у 20 файлах, 5 fake adapters |
| Scenes | Готово | 11 сцен (MainTest, ShootingRange, Hideout, Test_Map, demos) |
| Spawn points | Готово | 7 типів: Player, Bot, LooseLoot, Container, Workbench, Deploy, NPC |
| Launch modes | Готово | Menu, Raid, TestScenario, Hideout |

---

## 3. Деталі ключових систем

### 3.1 Зброя

| Параметр | Rifle | Shotgun | Pistol |
|----------|-------|---------|--------|
| Fire Interval | 0.2с | 0.6с | 0.4с |
| Урон | 10 | 8 (×7) | 15 |
| Снарядів/постріл | 1 | 7 | 1 |
| Розкид | 0° | 30° | 0° |
| Швидкість снаряда | 20 м/с | 30 м/с | 25 м/с |
| Магазин / Перезарядка | 30 / 2.0с | 5 / 2.5с | 12 / 1.5с |
| Base Penetration | 20 | 10 | 15 |
| Base ArmorDmg | 5 | 4 | 6 |
| Headshot Multi | 2.0× | 1.5× | 2.5× |
| Recoil (Fwd/Side/Rec) | 2/1.5/2 | 3/6/3 | 1.5/1/4 |

### 3.2 Типи патронів (9)

| Тип | Penetration | ArmorDmg | BleedChance | Нотатки |
|-----|-------------|----------|-------------|---------|
| Standard | +8-12 | +4-6 | 0 | Базові, балансовані |
| AP | +30-35 | +7-8 | 0 | Anti-armor, high pen |
| HP | 0 | 0 | 0.08-0.30 | Bleed build, useless vs armor |

Projectile stats = Weapon Base + Ammo stats (additive composition).

### 3.3 Armor Pipeline

```
HitSignal → Skip checks (self/dead/roll/god) → Headshot detect → HS multiplier
  → Ricochet check (helmet only, pen < armor, 40%)
      YES → 0 HP, 2× dur dmg, spark VFX, remove projectile
      NO  → Armor calc: mult = K/(K+diff), K=30
            → Apply dur dmg: armorDmg × (1 + absorptionRatio)
            → Apply HP dmg: rawDmg × mult
  → Bleed roll (independent of armor)
  → Events: HitConfirmed, DamageNumber, EntityDamaged/Died
```

**Durability:** safe zone 70-100% (full protection), parabolic decay (t^2) нижче 70%, break at 0%.

### 3.4 Боти

| Тип | HP | Зір | Точність | Зброя | Поведінка |
|-----|-----|------|----------|-------|-----------|
| Scav | 80 | 25м/110° | 0.5 | Rifle | Patrol, Chase, Shoot |
| PMC | 100 | 35м/120° | 0.75 | Rifle | All (heal, dodge, grenades) |
| Boss | 200 | 40м/140° | 0.65 | Shotgun | Chase, Shoot, Dodge |

**Shooting Range (9 рядів):**
- Row 1-3: Static immortal (10k HP)
- Row 4-5: Patrol (horizontal/vertical)
- Row 6: Fast patrol (6 м/с)
- Row 7: Dodge targets
- Row 8: Weak killable (50 HP, helmet)
- Row 9: Armored (light/heavy/glass cannon/tank)

### 3.5 Предмети

**Зброя (3):** Rifle, Shotgun, Pistol
**Броня (2):** Helmet_Basic (30 AP, 100 dur), Armor_Basic (40 AP, 120 dur)
**Патрони (9):** Standard/AP/HP × Rifle/Shotgun/Pistol
**Розхідники (4):** Medkit (×200), Advanced_Medkit (×1), Bandage (×1), Grenade (×1)
**Крафт-матеріали (13):** Adhesive, Metal_Parts, Mechanical_Parts, Electronics, Chemicals, Cloth, Gunpowder, Plastic, Glass, Rubber, Springs, Military_Components, Energy_Core
**Weapon Mods:** присутні в item registry, crafting recipes

### 3.6 Crafting

21 рецепт у 4 категоріях: Meds, Weapons, Ammo, WeaponMods. Ingredient check + free slot validation.

---

## 4. Що НЕ реалізовано

### Критичні для core loop:

| Фіча | Пріоритет | Нотатки |
|-------|-----------|---------|
| **Extraction zones** | Критичний | Немає зон/таймерів — без цього немає extraction loop |
| **Raid end conditions** | Критичний | Немає win/lose/extract screen |
| **Menu / lobby** | Критичний | LaunchMode.Menu існує, UI не реалізовано |
| **Stash / persistence** | Критичний | SaveManager є, повного save/load немає |
| **Loadout selection** | Високий | Hideout scene є, loadout UI немає |

### Важливі для polish:

| Фіча | Пріоритет | Нотатки |
|-------|-----------|---------|
| **Audio** | Високий | Повна тиша, 31 event type готові для hookup |
| **Explosion VFX** | Середній | Гранати працюють, візуального вибуху немає |
| **More status effects** | Низький | Архітектура є, лише Bleeding L1/L2 |
| **Multiplayer** | Низький | Single-player архітектура |

### Deferred (дизайн визначений, реалізація відкладена):

| Фіча | Статус дизайну |
|-------|---------------|
| Weapon Mod Tree | RPG modifier system задокументований, budget 25-35% |
| Character Skill Tree | Budget 10-15% визначений |
| Concrete stat values | Armor tiers, bleed DPS, HS multi — TBD |
| Armor crafting/repair | Economy design відкладений |

---

## 5. Документація

### docs/ai/ (14 файлів)

| Документ | Тип | Зміст |
|----------|-----|-------|
| **CLAUDE.md** | Контракт | Правила, workflow, DevCheats SO архітектура, task routing |
| **architecture.md** | Tech | Шари, повний tick order, 25+ state класів, 31 event type, 14 DevCheats секцій, debug tools, spawn points |
| **entity-lifecycle.md** | Tech | Spawn/despawn контракти, binding, callbacks → inbox |
| **weapons.md** | Tech | FSM, ammo types (9), dual-layer aiming, ADS, convergence, stats |
| **crosshair.md** | Tech | 8 cursor states, ADS crosshair, proportional hit markers, ricochet markers, 17+ DevCheats |
| **fog-of-war.md** | Tech | 5-stage pipeline, DX12 compatibility, edge-finding, FAQ |
| **armor-system.md** | Tech | Pen formula, durability curve, damage pipeline, ricochet, equipment sync, ammo composition, VFX |
| **bot-ai.md** | Tech | BT framework, 6 nodes, perception, 12 bot types, blackboard (24 fields) |
| **inventory-and-items.md** | Tech | 38+ items, slots, stacking, equipment, crafting (21 recipes), loot, status effects, stamina, quests |
| **armor-research.md** | Design | Аналіз 15+ ігор, bleeding systems, top-down feedback patterns |
| **rpg-modifier-system.md** | Design | 3-source additive modifiers, hard caps, UI breakdown |
| **battle-design-status.md** | Design | Living doc: 14 decided + deferred + 33 dated decisions |
| **testing-and-workflow.md** | Process | Test strategy, DevCheats isolation rule, feature flow |
| **fx-artist-guide.md** | Art | 5 impact VFX prefabs, proportional scaling, art direction |

### .cursor/rules/ (10 файлів) — синхронізація

| Статус | Файли |
|--------|-------|
| Синхронізовано | entity-lifecycle, testing-workflow |
| Потребує оновлення | architecture-details, weapons, crosshair (оновлені .md, .mdc відстають) |
| Сильно застарілі | battle-design-status (step-function vs parabolic), armor-research (42%), rpg-modifier-system (51%) |
| Немає .mdc | armor-system, bot-ai, inventory-and-items, fx-artist-guide |

---

## 6. Тести

**287 тестів у 20 файлах** (EditMode):

| Тест-клас | К-ть | Покриття |
|-----------|------|----------|
| ArmorSystemTests | 34 | Durability curve, pen math, ricochet, calculate |
| ShootingSystemTests | 27 | Fire, weapon state, ammo composition |
| AimingSystemTests | 25 | Facing, cone, smooth rotation |
| WeaponStateMachineTests | 25 | Phase transitions, timing |
| ArmorStateTests | 24 | State validation, item defs, weapon pen |
| DamageSystemTests | 19 | Full armor pipeline, headshot, ricochet, bleed |
| AmmoSystemTests | 18 | Count, consume, reload |
| BotHealTests | 15 | Emergency/safe heal, conditions |
| PlayerFOVSystemTests | 15 | Visibility, sectors, range |
| LootSystemTests | 10 | Generation, transfers, armor preservation |
| BotSpawnSystemTests | 9 | Instantiation, faction armor |
| ProjectileSystemTests | 9 | Movement, lifetime, despawn |
| StatusEffectSystemTests | 9 | Bleed levels, stacking |
| PlayerSpawnSystemTests | 8 | Player init, equipment |
| MovementSystemTests | 8 | Input → position |
| WeaponEquipSystemTests | 8 | Slot selection |
| EquipmentSystemTests | 7 | Armor sync, durability |
| BotBrainSystemTests | 6 | Patrol/chase/fire decisions |
| BotCombatSystemTests | 6 | Firing, projectile spawn |
| BotPerceptionSystemTests | 5 | Vision/hearing |

**Інфраструктура:** 5 fake adapters (Input, NavMesh, Physics, Events, Time), deterministic RNG injection.

---

## 7. Контент та сцени

### Сцени (11)

| Сцена | Призначення |
|-------|-------------|
| MainTestScene | Основна ігрова сцена |
| ShootingScene | Стрілецький полігон (9 рядів, 35+ мішеней) |
| HideoutScene | Hideout з NPC, workbench, deploy points |
| TestScene, Test_Map | Тестові сцени |
| Demo_City_Standard/URP, Demo_Building_Interior, Demo_Bunker | Polygon Apocalypse demos |

### Assets

| Категорія | Кількість |
|-----------|-----------|
| Core prefabs | ~20 (player, bots, weapons, armor, VFX) |
| Environment prefabs | ~1800 (Polygon Apocalypse) |
| Custom shaders | 6 (5 FoW + HealthBarFill) |
| Animation clips | 21 (5 per weapon + base) |
| VFX impact prefabs | 5 (Body/Head/Bullet/Armor/Ricochet) |
| VFX weapon prefabs | 3 (Muzzle, Trail variants) |

---

## 8. Рекомендації

### Наступні кроки для гри:

**Core extraction loop (блокер для vertical slice):**
1. Extraction zones + timer + success screen
2. Raid failure screen (death)
3. Lobby / loadout selection UI
4. Stash + persistence між рейдами

**Polish:**
5. Audio system (events готові, потрібен SFX hookup)
6. Explosion VFX для гранат

### Документація:

**Sync .cursor/rules/ (коли повернемось до цього):**
- Оновити 3 .mdc файли які відстали від оновлених .md
- Створити 4 нові .mdc для нових доків
- Оновити 3 сильно застарілі .mdc (battle-design, armor-research, rpg-modifier)

---

## 9. Сильні сторони

- **Чиста архітектура** — строгий state/logic/view split, тестується без сцен
- **Глибока combat система** — armor pen curve, ricochet, proportional feedback, 9 типів патронів, headshot system
- **Повноцінний AI** — behavior tree + perception + tactical healing/dodge/grenades, 12 bot types
- **Розвинений інвентар** — 38+ items, 21 crafting recipe, equipment sync, quick slots, quests
- **Mature DevTools** — 14 секцій DevCheats (SO-based), state/BT debuggers, quest editor
- **Production-quality FoW** — 5-stage pipeline з edge-finding, DX12 support
- **Потужне тестове покриття** — 287 тестів, fake adapters, deterministic RNG
- **Comprehensive документація** — 14 docs/ai файлів покривають всі реалізовані системи
