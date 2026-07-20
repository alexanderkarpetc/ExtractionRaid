# Release Scope — Feature Map & Gap Analysis

> Snapshot: 2026-07-17. Мета цього доку — **одним поглядом** побачити всю гру великими мазками,
> що вже зроблено vs що лишилось, і **скільки роботи до релізу**.
>
> Джерела: аудит доків (`handoff.md`, `battle-design-status.md`, `weapon-builder/plan/*`,
> `gunplay/README.md`, `inventory-and-items.md`) + повна інвентаризація коду (`Assets/Scripts/`,
> `Assets/Scenes/`, build settings) на 2026-07-17. Спірні для оцінки твердження звірені з кодом.

## Легенда

**Статус:** ✅ shipped (реальна логіка, здебільшого з тестами) · 🔄 partial (працює, але тонкий контент / є діри) · 🟥 missing / stub (немає або лише косметика).

**Обсяг роботи (грубо):** S ≤ ~1 сесія · M кілька сесій · L ~1–2 тижні · XL багатотижнево / потребує ассетів чи артиста.

---

## Частина 1 — Карта фіч (великими мазками)

### A. Core combat / feel — ✅ найсильніша частина гри
| Фіча | Статус | Нотатка |
|---|---|---|
| Стрільба, балістика, projectile collision | ✅ | point-blank probe, own-owner filter, lock-on convergence |
| Hit feedback (crosshair X, damage numbers v2, rim flash, decals) | ✅ | розлого відполіровано |
| Кров / ragdoll / casing / mag drop / camera shake / hitstop / flinch | ✅ | AAA-рівень juice для graybox |
| HUD damage (directional vignette + low-HP glow) | ✅ | єдиний SDF-шейдер |
| Aim cursor v2 + recoil visuals | ✅ | uGUI+SDF стек |
| Armor / penetration / durability / bleed L1-L2 / headshot / ricochet | ✅ | гіперболічна pen-крива, вага→мобільність |

### B. Weapons (Weapon Builder) — ✅ фундамент + полиш, 🔄 залишковий полиш
| Фіча | Статус | Нотатка |
|---|---|---|
| 6 архетипів (Ballistic/Laser × Pistol/Rifle/Shotgun) | ✅ | composition + cached stats |
| Modular 3D-візуалізація (payload+delivery) | ✅ | Tier 8 |
| Attachments + моди epic + Sniper Scope (P1–P4) | ✅ | loot-gated моди, rarity-слоти, compare-tooltip |
| Процедурний reload/equip/unequip motion | ✅ | Tier 8.x (player) |
| Muzzle alignment / socket tuning / Mecanim cleanup | 🔄 | Tier 8.x залишок (S–M) |
| Bot weapon migration (Tier 4a) | 🔄 | боти ще на legacy compat-шарі (M) |
| Per-archetype VFX/SFX (Tier 9) | 🟥 | SFX = частина аудіо-діри |
| Feel/balance полиш (Tier 10) | 🟥 | ітеративний playtest-луп (L, ongoing) |
| Content expansion — Foam/Rocket/Rotary/Swarm, exotics (Tier 3/5) | 🟥 | deferred sine die (поза релізом) |

### C. Enemies / AI — ✅
| Фіча | Статус | Нотатка |
|---|---|---|
| Behavior tree (10 нод: patrol/chase/shoot/melee/cover/dodge/heal/search/grenade/fire-forward) | ✅ | Selector/Sequence/Condition/Cooldown |
| Perception (FOV+occlusion), navmesh movement, combat, spawn | ✅ | з тестами |
| Ростер: Scav / PMC / Boss / Zombie + range-targets | ✅ | per-type SO configs |
| Horde wave spawner | ✅ | тест-сцена |

### D. Player / survival — ✅
Movement, sprint+stamina+exhaustion, dodge roll, grenades, medkit (resource-pool heal), bandage (bleed cure), fog-of-war / vision cone, interaction prompt — усе реальне, з тестами. Status effects — 🔄 лише Bleeding (контент тонкий).

### E. Inventory / items / loot — ✅
Slot-based інвентар (2 weapon + helmet + body + 20 backpack + 7 quick-slots), ~101 item definitions, стакінг, equipment, loot-контейнери + corpse-loot + ground items, drag/drop крос-джерельний (shop/stash), quick-slot bind. Повний UI Toolkit. З тестами.

### F. Meta-loop — 🔄 системи є, контент/ризик тонкі
| Фіча | Статус | Нотатка |
|---|---|---|
| Extraction (per-zone hold-таймер, progress, RequestExtraction) | ✅ | `ExtractionSystem` + HUD |
| Raid start/end flow, outcome (Extracted/KIA), end-screen | ✅ | `App.StartRaid/EndRaid`, `EndOfRaidPresenter` |
| **Втрата спорядження при смерті** | ✅ | **M1.1 (2026-07-18): KIA → `Player.Inventory.ClearAll()` в `App.EndRaid` (повний wipe, stash цілий); стартовий кіт видається раз (`HasReceivedStartingKit`), після смерті — re-gear зі stash. Deferred: secure-container/insurance, save-on-death, стартовий пістоль.** |
| Загальний raid-таймер / ліміт часу | 🟥 | лише per-zone hold |
| Hideout hub (як не-ворожий «рейд») | ✅ | `HideoutScene` |
| Stash (deposit/withdraw + swap) | ✅ | персист |
| Crafting (~24 рецепти: meds/weapons/ammo/mods) | ✅ | |
| Base building / upgrade levels | 🔄 | `BuildingSystem` є; взаємодія частково stub (placeholder-діалоги) |
| Quests (9 типів задач, prereq-граф, rewards, NPC hand-over) | ✅ | система розлога; **7 квестів контенту** |
| Economy / currency (Credits) | 🔄 | `ShopSystem` реальний; **1 shop контенту**, без buy-back/loyalty |
| Progression / level / XP / skills | 🟥 | `Level` є, але **ніщо не інкрементує**; XP/skill/unlock немає |

### G. Shell / game flow — ✅ (з шорсткостями)
Main menu (Continue/New/Quit) ✅ · AppBootstrap+DI (~20 презентерів) ✅ · MainMenu→Hideout→Deploy→Raid→End→Hideout ✅ · Pause menu (Esc, Resume/Settings/Exit) ✅ · **Deploy UI = legacy IMGUI** 🔄 (функціональний, не продакшн). Settings-глибина не перевірена.

### H. Save / persistence — ✅ базово
JSON single-slot (`save.json`): name, credits, повний інвентар (+ builder-конфіги), stash, quest progress, building levels. Load на старті, save на кожен EndRaid. Не персистить world/position/XP. Без версіонування/міграцій.

### I. Content — 🔄 тонко
| Тип | Кількість | Нотатка |
|---|---|---|
| Playable мапи (в білді) | **2** | `test_level`, `main_map` — handcrafted, spawn-points у сцені; без процедурки |
| Hub / menu сцени | 2 | Hideout, MainMenu |
| Dev/test сцени | 6 | shooting ranges, sandbox |
| Shops | 1 | `ShopAL` |
| Quests | 7 | + questgraph |
| Status effects | 1 | Bleeding |

### J. Presentation / tech — ✅ / 🟥
VFX/juice-шар розлогий ✅ (ragdoll, decals, ejectors, shake, hitstop, damage numbers, minimap, world bars, NPC dialogue UI). **Аудіо — 🟥 повністю відсутнє: 0 SFX-хуків, 0 аудіо-ассетів, 0 `AudioSource`.** Архітектура (5-шарова, тестована) ✅. ~660 EditMode тестів на combat/weapons/AI/inventory — але **мета-луп (extraction/shop/craft/building/quest/save) без тестів**.

---

## Частина 2 — Аналіз: скільки до релізу

### Що вже фактично «в наявності»
Гра має **надзвичайно сильне ядро “секунда-до-секунди”** (бій, зброя, броня, AI, інвентар, feel) — це зазвичай найдорожча і найризикованіша частина shooter'а, і вона в основному готова та протестована. Плюс **робочий кістяк мета-лупу** (extraction, hideout, stash, craft, quests, shop, save, меню-flow). Тобто гра вже **запускається end-to-end**: меню → база → deploy → рейд → бій/лут → extraction/смерть → end-screen → база з персистом.

### Головні діри, що відділяють «прототип, який грається» від «релізу»

**Блокери ідентичності жанру / “must-fix”:**
1. 🟥 **Втрата спорядження при смерті (M).** Зараз KIA нічого не забирає — це вбиває саму суть extraction-shooter (risk/reward). Треба: drop інвентарю на труп/у світ або wipe, рішення про secure-container/insured-slots. **Найважливіша механіка з усіх дір.**
2. 🟥 **Аудіо (XL).** Немає жодного звуку. Потрібно: audio-менеджер+pooling (event-driven презентер, як інші), SFX (постріли/reload/impact/ricochet/UI/кроки/ambient), музика, і власне ассети. Найбільший **обсяг** роботи + залежність від ассетів.

**Контент до «повноцінного» релізу:**
3. 🔄 **Мапи (L).** 2 handcrafted мапи — тонко. Треба ще + балансування loot/spawn/extract-точок.
4. 🔄 **Economy (M).** Розширити до кількох торгівців, ціни/buy-back/баланс.
5. 🔄 **Quests (M).** Побудувати релізний questline-арк поверх наявних 7.
6. 🔄 **Raid timer / fail-states (S–M).** Жанрово-стандартний тиск часу (опційно, але бажано).

**Полиш/якість:**
7. 🔄 **Weapon залишок (M):** Tier 8.x (muzzle/socket/Mecanim), Tier 4a (bot migration), Tier 9 VFX, Tier 10 feel/balance.
8. 🔄 **Shell-полиш (S–M):** замінити IMGUI DeployUI на нормальний UI, глибина Settings, building-взаємодія (зняти placeholder-діалоги).
9. 🔄 **Мета-луп тести (M):** extraction/shop/craft/building/quest/save зараз без покриття.
10. 🟥 **Onboarding/tutorial (M):** немає (для публічного релізу зазвичай треба).

**Опційно / поза MVP:**
11. 🟥 **Progression/XP/skills (M–L)** — вирішити, чи взагалі в scope релізу.
12. 🟥 **Більше status effects, save-версіонування, процедурні мапи** — deferred.

### 🔒 Зафіксовані рішення (2026-07-17..18)
- **Ціль релізу = повний v1.0** (не EA/demo). Отже майже весь контент+полиш заходить у scope.
- **Progression (XP / level / skills) — ПОЗА scope релізу.** Мета-прогрес лишається через
  quests + credits + building levels + stash. `Level`-поле стоїть, але XP-луп не робимо для v1.0.
- **Weapon Builder = headline-фіча, вкладаємось (2026-07-18).** Player-facing core-assembly loop
  лишається й розвивається → обсяг: **3×4 архетипи** (+Rocket payload, +Rotary delivery) + **3–4 exotics**
  + balance + Builder UX. Повна 4×5 = стретч. Attachments + модульне 3D + композиція — база.
- **Мапи: 2 ігрові + бункер** (зараз 1 ігрова `main_map` + бункер → треба +1 мапу).
- **Туторіал: окремого немає — перші квести = онбординг.** Квести розмазуємо на весь геймплей.
- **Іконки предметів = must-have** — генеровані (немає UI-артиста).
- **Локалізація = EN-only.**
- Процедурна генерація мап — deferred (поза v1.0).

> 📋 **Execution-план з віхами M1–M4 → [`v1.0-roadmap.md`](./v1.0-roadmap.md).**

### Груба оцінка обсягу (під v1.0)

| Workstream | Обсяг | У v1.0? |
|---|---|---|
| Втрата спорядження при смерті (risk-loop) | M | ✅ must — core ідентичність жанру |
| Аудіо (система + SFX + музика + ассети) | XL | ✅ must — найбільший одиничний обсяг |
| Raid timer / fail-states | S–M | ✅ так |
| Weapon залишок: Tier 8.x (muzzle/socket/Mecanim) + 4a (bot migration) | M | ✅ так |
| Weapon Tier 9 (per-archetype VFX/SFX) | M | ✅ так (SFX-частина = разом з аудіо; VFX ± артист) |
| Weapon Tier 10 (feel/balance) | L, ongoing | ✅ так — фінальний playtest-луп |
| Shell-полиш (DeployUI→нормальний UI, settings, building UX без stub) | S–M | ✅ так |
| **Weapon Builder — вкладення:** Tier 3/5 (нові cores + exotics, обсяг TBD) + balance + Builder UX | L | ✅ так — headline (рішення 2026-07-18) |
| Мапи: +1 ігрова (до 2) + баланс loot/spawn/extract | L | ✅ так |
| Economy: кілька торгівців, ціни/buy-back/loyalty, баланс | M | ✅ так |
| Quests: наскрізний questline-арк (перші = онбординг) поверх наявних 7 | M | ✅ так |
| Іконки предметів — згенерувати + змапити (система вже вшита) | M | ✅ must-have; реєстр+atlas є, ~82 з ~85 без іконки (генеровані — немає UI-артиста) |
| Мета-луп тести (extraction/shop/craft/building/quest/save) | M | ✅ так |
| Save-версіонування/міграції | S | 🔶 бажано |
| Локалізація | — | ❌ EN-only (рішення 2026-07-18) — не робимо |
| Progression / XP / skills | M–L | ❌ **поза scope** (рішення 2026-07-17) |
| Окремий tutorial, процедурні мапи, ще status effects | — | ❌ deferred |

**Висновок великими мазками:** ~**70–80% важкого технічного ядра готово** (бій/зброя/броня/AI/інвентар/feel + кістяк мета-лупу). Для **v1.0** лишаються три пласти:
1. **Механіки-must:** risk-loop (M) + аудіо (XL) + raid timer (S–M).
2. **Полиш:** weapon-залишок (8.x/4a/9/10), shell, мета-луп тести.
3. **Контент (найбільший невідомий за трудомісткістю для v1.0):** мапи (L), economy (M), quests (M), tutorial (M).

Найдорожче за обсягом — **аудіо (XL)** і **контент-виробництво (мапи + economy + quests)**. Найважливіше за впливом при малому обсязі — **risk-loop (M)**.

### Найкращий наступний крок (рекомендація)
Найбільший приріст «це вже extraction-гра» за найменший обсяг: **1) risk-loop (M)** → **2) каркас аудіо-шару** (event-driven `SfxPresenter` + постріли/impact/reload/UI як перша хвиля SFX). Обидва без архітектурного ризику й одразу роблять луп «чесним» і «живим». Контент-важкі пласти (мапи/economy/quests) — паралельний, довший трек; аудіо-повнота і Tier 9/10 частково залежать від артиста.

---

## Related docs
- `handoff.md` — поточний стан + останні лендинги
- `battle-design-status.md` — armor/pen/bleed/feedback дизайн
- `weapon-builder/plan/roadmap.md` — тіри зброї (8.x / 4a / 9 / 10 залишок)
- `gunplay/README.md` — combat-feel shipped state
- `inventory-and-items.md` — інвентар/лут/крафт/quests технічний довідник
