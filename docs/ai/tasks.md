# ExtractionRaid — Task Tracker

> **Єдине джерело задач і статусів у репозиторії.** Інші документи описують лише поточну
> архітектуру, правила та прийняті рішення. Нову роботу додаємо сюди, а не в feature-доки.
>
> Оновлено: **2026-09-01**. Статуси звірено з поточним `master`, історією Git і C#-кодом.

## Як вести трекер

- `⬜ queued` — ще не почато.
- `🔄 active` — є часткова реалізація або робота вже йде.
- `⛔ blocked` — потрібне зовнішнє рішення, асети чи інша задача.
- `✅ done` — критерії приймання виконані; деталі реалізації живуть у відповідному system doc.
- Оновлювати статус у тому самому коміті, що змінює реалізацію.
- Не дублювати task-таблиці, backlog або `next steps` в інших Markdown-файлах.

## Поточний фокус

| ID | Задача | Статус | Обсяг | Критерій завершення |
|---|---|---:|---:|---|
| M1.3 | Extraction UX | 🔄 active | S–M | Виходи однозначно читаються у світі й на мапі; стани available/progress/interrupted/complete зрозумілі; на бойовій мапі авторено кілька виходів. HUD, minimap і підтримка кількох `ExtractionPointState` вже є. |
| T63 | Стартова складність | ⬜ queued | S–M | Новий гравець не отримує летальний фокус кількох rifle-ботів у перші секунди; результат підтверджено плейтестом без глобального спрощення AI. Узгодити з власником bot configs. |
| M2.1 | Завершити аудіошар | 🔄 active | L | Закрити відсутні weapon/UI/world sounds, ambience, music transitions і settings sliders; перевірити spatial mix. `GameAudioPresenter` і базова бібліотека вже працюють. |
| M2.4 | Weapon visual polish | 🔄 active | M | Muzzle/socket alignment, ручний prefab pass, видалення stale Mecanim; реальні меші замість технічних заглушок. Procedural reload/equip/unequip уже є. |
| M2.7 | Підключити ефекти progression | ⬜ queued | M | `ProgressionSystem.ApplyAllocatedEffects` більше не порожній; ефекти проходять через `RaidContext.*Config`; є EditMode-тести. |
| M3.1 | Друга бойова мапа | ⬜ queued | L | Окрема сцена проходить повний loop: deploy → combat/loot → extraction; авторені spawns, loot і таймер. Поточне розширення `Test_Map` не рахується другою мапою. |
| M3.6 | Контент бойових мап | 🔄 active | M | Узгоджені enemy roster, boss placement і loot tiers. Наповнення `Test_Map`/Rednek City триває окремим контентним потоком. |

## Release backlog

| ID | Задача | Статус | Обсяг | Критерій завершення |
|---|---|---:|---:|---|
| M2.2 | Exotic hook + 3–4 exotics | ⬜ queued | M | Data-driven hook інтегрований у stat/fire/impact pipeline; 3–4 різні exotics доступні через loot/build flow і протестовані. |
| M2.3 | Rocket payload + Rotary delivery | ⬜ queued | M | Нові cores мають behavior, stats, VFX, mesh, loot source і тести; матриця досягає 3×4. |
| M2.5 | Per-archetype VFX/SFX | 🔄 active | M | Ballistic/Laser/Rocket мають послідовну власну мову пострілу, польоту й impact. Ballistic audio та частина VFX уже є. |
| M2.6 | Weapon Builder UX polish | 🔄 active | S–M | Builder читається як headline-фіча: зрозуміла композиція, preview, compare, validation і завершений visual pass. Основний UI та compare flow уже працюють. |
| M3.2 | Баланс spawn/loot/extract | ⬜ queued | M | Обидві бойові мапи мають перевірену щільність ботів, loot tiers, ризик і розташування виходів. |
| M3.3 | Економіка | 🔄 active | M | Кілька торговців, buy-back, узгоджені ціни та material sinks. `ShopSystem` і один shop уже працюють. |
| M3.4 | Наскрізний quest arc | 🔄 active | M | Квести ведуть гравця від онбордингу через основні системи до довшої цілі. Quest framework і стартовий контент уже є. |
| M3.5 | Повне покриття іконками | 🔄 active | M | Усі player-facing items мають читабельні іконки та mapping у чинному registry/atlas. Pipeline уже працює, покриття неповне. |
| M4.1 | UI і Settings pass | 🔄 active | S–M | `DeployUI` перенесено з IMGUI, settings завершені включно з audio sliders, building UX не показує placeholder-потоків. |
| M4.2 | Meta-loop tests | ⬜ queued | M | Покриті extraction, shop, craft, building, quest, save/load та повний raid outcome flow. |
| M4.3 | Save versioning і міграції | ⬜ queued | S | Save має schema version, контрольовані міграції та тести сумісності старих даних. |
| M4.4 | Balance pass | 🔄 active | L | Плейтестом зведені weapon/armor/loot/economy/difficulty/progression values; немає очевидної домінантної стратегії або soft-lock. |
| M4.5 | Release validation | ⬜ queued | M | Standalone build проходить smoke/perf pass на цільовій платформі; quality settings і EN-only presentation готові. |
| A1 | Прибрати Unity refs зі State | ⬜ queued | M | `WeaponEntityState` більше не зберігає `GameObject`/ScriptableObject refs; view/assembly резолвлять їх поза State; тести й debugger оновлені. Поточний код порушує канонічний state contract. |
| A2 | Прибрати global access із Systems | ⬜ queued | L | Systems отримують inventory/registries/configs через args/`RaidContext`; прямі `App.Instance` і `DevCheats` reads відсутні; focused tests не залежать від global state. |
| A3 | Винести highlight eligibility з View | ⬜ queued | S | Proximity/eligibility outline визначає gameplay state/system; `InteractableOutlineTarget` лише відображає resolved target і не читає `App.Instance`. |
| A4 | Reset outline registry без Domain Reload | ⬜ queued | S | `InteractableOutlineRegistry` очищає static dictionaries/buffers через `SubsystemRegistration`; повторний Play не успадковує stale renderers. |

## Завершено

| ID | Фіча | Статус | Підтвердження |
|---|---|---:|---|
| M1.1 | Gear loss + baseline floor | ✅ done | KIA очищає raid inventory, stash зберігається; порожньому інвентарю видається анти-soft-lock комплект. |
| M1.2 | Raid timer + timeout fail-state | ✅ done | Timeout іде через звичайний KIA pipeline; HUD і focused EditMode coverage додані. |
| M1.4 | Bot weapon migration | ✅ done | Боти використовують `WeaponConfiguration`; legacy player/bot weapon IDs і orphan ammo прибрані. |
| M1.5 | Audio scaffold | ✅ done | `GameAudioPresenter`, pooled spatial voices та перша хвиля weapon/impact/reload/UI sounds працюють. Подальше наповнення — M2.1. |
| M2.8 | Material cost progression | ✅ done | Ноди оплачуються матеріалами зі stash/backpack через `ProgressionCostSystem`; skill points відсутні. |
| R1 | Inventory quick transfer | ✅ done | Double-click transfer працює. |
| R2 | Bot weapon rarity | ✅ done | Per-spawn rarity roll і тести працюють. |
| R3 | Tooltip/value cleanup | ✅ done | Compare та value для priced items узгоджені. |
| R4 | Magazine capacity guard | ✅ done | Overfill заблоковано focused тестами. |

## Поза v1.0

Ці записи не входять у release commitment; вони зберігаються тут, щоб не розповзались по
feature-доках.

| ID | Задача | Статус | Умова повернення |
|---|---|---:|---|
| X1 | Secure container / insurance | ⬜ parked | Після стабілізації gear-loss economy. |
| X2 | Foam/Swarm cores і повна 4×5 матриця | ⬜ parked | Після завершення 3×4 release scope та balance pass. |
| X3 | Procedural maps | ⬜ parked | Не раніше завершення двох authored maps. |
| X4 | Додаткові status effects | ⬜ parked | Коли з'явиться конкретна gameplay-потреба; Bleeding L1/L2 лишається baseline. |
| X5 | Глибший bot simulation: cover, suppression, POI roaming, looting, extraction, factions | ⬜ parked | Після T63 і стабілізації базової складності. |
| X6 | Composite weapon icons | ⬜ parked | Після повного item-icon pass і підтвердженої потреби в двошаровому rendering. |

## Паралельна відповідальність

- `Systems/Meta` і progression/loot частково веде Олександр — узгоджувати перетини до правок.
- Контент `Test_Map`/Rednek City ведеться окремим map-content потоком.
- Рослинність і шейдери мають окремого власника; не змішувати з gameplay-задачами.
