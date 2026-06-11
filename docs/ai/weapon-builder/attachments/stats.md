# Attachments — Stat Vocabulary (Iteration 2)

> На що саме впливатимуть Attachments? Який словник характеристик має зброя зараз, що тюнять конкуренти, і які стат-осі потрібні для транспарентних sidegrade-tradeoff-ів.
> **Статус:** 🔬 аналіз + ✅ **робочий список параметрів узгоджено** (див. нижче). Продовження [`analysis.md`](./analysis.md).
> **Дата:** 2026-06-07.

---

## ✅ Робочий список параметрів — узгоджено (2026-06-07)

Фінал першого проходу. Чотири рівні видимості:

### A. Показуємо гравцю + модифікується модами

| Параметр (player-facing) | Внутрішні поля | Нотатки |
|---|---|---|
| **Damage** | `Damage` | |
| **Headshot Mult** | `HeadshotDamageMultiplier` | наш «crit» |
| **Rate of Fire** | `FireInterval` (інверсія) | показуємо RoF (вище=краще), НЕ сирий interval у секундах |
| **Magazine Size** | `MagazineSize` | |
| **Recoil (V + H + recovery)** | `RecoilKickForward`, `RecoilKickSide`, `RecoilRecoverySpeed` | recovery під Recoil (не Ergo); **показуємо гравцю як "Stability"** (вище=краще, 0..100 score) — див. ux.md §3 |
| **Accuracy / Spread** | `SpreadAngle`, `ConeHalfAngle` (+ heat-spread coupling) | додано цієї ітерації; видно через SDF-crosshair |
| **Ergonomics** (агрегат) | ADS-speed + equip/unequip + move-speed-mult + `BodyRotationSpeed` | **один множник масштабує весь бандл**; ➕ 2 нові per-weapon поля |
| **Noise** | ➕ `NoiseRadius` + боти реагують на `WeaponFired` | **НОВА** perception-механіка (стелс vs DPS — глушник) |

### B. Модифікується, але базове значення приховане (показуємо лише дельту)

| Параметр | Поле | UI |
|---|---|---|
| **Reload Time** | `ReloadTime` | мод показує «−50% Reload Time»; **абсолютних секунд на UI немає** |

### C. Механіка в scope, але прихована від UI (без числа)

| Параметр | Інфра | UI |
|---|---|---|
| **Sight Range / FOV** | ➕ fog-of-war integration | ефект відчувається **візуально** (бачиш далі / вужче конус); числа нема |

### D. Поза attachment-scope → ammo-канал

- **Bleed Chance**, **Penetration**, **Armor Damage** — належать **ammo**, не weapon/attachment. Збігається з наявним станом (canonical source armor-damage/bleed = ammo).

> **Резюме:** 8 показаних (A) + 1 hidden-base (B) + 1 hidden-mechanic (C). Усе на наявних полях, **крім**: Ergonomics (➕ 2 per-weapon поля: move-mult, ADS-speed/transition — зараз глобальні), Noise (➕ нова perception-механіка + `NoiseRadius`), Sight Range/FOV (➕ fog-of-war integration).
>
> **Розблоковано:** Q12 → Ergonomics = агрегат (один множник). Q13 → Noise у scope + показаний. **Лишається:** Q14 (модель прицілу ZS vs Duckov — хоч hidden-from-UI, механіка потребує вибору), Q17 (формат дельти — Reload уже задає прецедент «%»).

---

## 1. Що зброя має ЗАРАЗ (grounded на коді)

Джерело істини — `State/WeaponStats.cs` (composed на equip) + runtime-поля `WeaponEntityState`. Усього **20 stat-полів** + heat + charge. Згруповано по player-facing бакетах:

| Бакет | Поля (фактичні) | Джерело |
|---|---|---|
| **Lethality** | `Damage`, `HeadshotDamageMultiplier`, `BasePenetration`, `BaseArmorDamage`, `BaseBleedChance` | Payload |
| **Projectile** | `ProjectileSpeed`, `ProjectileLifetime`, `ProjectilesPerShot` | Payload / Delivery |
| **Cadence** | `FireInterval` (=rate of fire), `MagazineSize`, `ReloadTime` | Delivery |
| **Accuracy / spread** | `SpreadAngle` (per-shot), `ConeHalfAngle` (aim cone) + `HeatLevel` (множить spread параболічно) | Delivery + runtime |
| **Recoil** | `RecoilKickForward` (≈vertical), `RecoilKickSide` (≈horizontal), `RecoilRecoverySpeed` | Delivery |
| **Handling / feel** | `AimFollowSharpness`, `BodyRotationSpeed`, `EquipTime`, `UnequipTime` | Delivery |
| **Charge** (Laser) | `ChargeTime` | Payload-specific |

**Окремі канали (не у `WeaponStats`):**
- **Ammo modifiers** — `Penetration / ArmorDamage / BleedChance` додаються у `ShootingSystem` на fire (per-ammo).
- **ADS** — `AdsMoveSpeedMultiplier / AdsRecoilMultiplier / AdsAimFollowMultiplier / AdsRecoilRecoveryMultiplier` — **глобальні** DevCheats-множники, НЕ per-weapon. `player.AdsBlend` (0..1).
- **Heat** — `WeaponHeatSystem` декеїть; інкремент лише Ballistic+Auto.
- **Exotic** — канал у `WeaponStatComposer.Compose` (зараз no-op).

> **Висновок по поточному словнику:** у нас уже є міцна база — lethality, cadence, recoil (V/H!), spread, handling-примітиви. Чого **немає** як named-стата: aggregate handling/ergonomics, **noise**, **weight**, **effective range / damage-falloff**, per-weapon **ADS/handling speed**, crit (у нас його роль грає headshot).

---

## 2. Словник характеристик конкурентів (comparative)

Зведено з [`competitor-research.md`](./competitor-research.md). ✓ = є явним стат-полем; дужки = виражено через інший механізм.

| Стат-вісь | Tarkov | ZERO Sievert | Duckov | SYNTHETIK | Destiny 2 | Division 2 |
|---|---|---|---|---|---|---|
| **Damage / Impact** | (ammo) | — | ✓ | ✓ | Impact | Weapon Dmg |
| **Rate of fire** | ✓ | — | — | firerate | RoF | RoF |
| **Recoil (V/H)** | V+H | Recoil (1) | V+H | recoil | Stability + RecoilDir | Stability |
| **Accuracy / spread** | MOA | Accuracy(=spread) | spread (ADS+hip) | deviation | (Range-coupled) | Accuracy |
| **Range** | sighting range | (view) | aim range | — | Range | Optimal Range |
| **Handling / Ergo / ADS-speed** | **Ergonomics** | **Ergonomics** | ADS time | (move-while-held) | **Handling** | **Handling** |
| **Magazine / capacity** | ✓ | ✓ | ✓ | — | Magazine | Extra Rounds |
| **Reload** | (ergo-driven) | — | ✓ | — | Reload | Reload |
| **Mobility / move** | weight→speed | ergo→stamina | (weight) | move-while-held | — | — |
| **Penetration** | (ammo) | — | — | ✓ | — | — |
| **Crit / Headshot** | — | — | crit dmg | crit + headshot | (precision) | Crit Ch/Dmg, Headshot |
| **Velocity** | muzzle velocity | — | bullet speed | velocity | Projectile Speed | — |
| **Noise / sound** | ✓ | ✓ (aggro) | sound range | — | — | — |
| **Weight** | ✓ | — | ✓ | — | — | — |
| **Zoom / FOV / view** | sighting | **FOV-cone↓** | aim-range (axis) | — | Zoom | — |
| **Heat / durability** | heat+durability | — | — | heat | — | — |

### Що з цього випливає

**Універсальне ядро** (є майже в усіх — це «безпечні» осі для attachment-ів): **Damage, Rate-of-fire, Recoil, Accuracy/Spread, Range, Handling, Magazine, Reload.**

**Жанрово-специфічне:**
- **Extraction-realism (Tarkov/ZS/Duckov):** додають **Noise** + **Weight** + **Handling/Ergonomics** як перші-класні осі. Саме вони роблять глушник/приклад справжніми tradeoff-ами (не «просто краще»).
- **Looter (Destiny/Division):** **Crit/Headshot** + **opposing-axis sliders** (Range⇄Stability). У нас роль crit грає `HeadshotDamageMultiplier` — окремий crit не потрібен.
- **Top-down (ZS/Duckov):** **Range = view/aim-range**, не damage-falloff і не camera-zoom. ZS: приціл звужує конус зору (зав'язано на fog-of-war). Duckov: axis-aware ADS-range.

**Найважливіше спостереження:** найкращі tradeoff-системи (Tarkov-трикутник, Destiny-слайдери) тримаються на осях, **яких у нас зараз немає named**: Handling/ADS-speed, Noise, Weight. Тобто щоб attachment-tradeoffs були цікавими, доведеться **додати 1–3 нові стат-осі**, а не лише крутити наявні.

---

## 3. На що Attachments впливатимуть — мапа (attachment → стати)

Кандидатні attachment-родини (з §3 analysis.md) → які наші стати тюнять і на якій осі компроміс. ⚙️ = вже маємо поле; ➕ = потрібен новий стат/механік.

| Attachment | Дає (give) | Бере (take) | Поля |
|---|---|---|---|
| **Глушник (Suppressor)** | ➕ −noise, ⚙️ −recoil | ⚙️ −Damage / −ProjectileSpeed, ⚙️ +Heat | `Damage`, `ProjectileSpeed`, `RecoilKick*`, `HeatLevel`, ➕Noise |
| **Дульний гальм (Brake)** | ⚙️ −recoil V/H | ⚙️ ↓Handling / ➕+weight | `RecoilKickForward/Side`, (Handling) |
| **Damage-muzzle** | ⚙️ +Damage | ⚙️ +recoil / +spread | `Damage`, `RecoilKick*`, `SpreadAngle` |
| **Розширений магазин** | ⚙️ +MagazineSize | ⚙️ +ReloadTime / ➕↓Handling-ADS | `MagazineSize`, `ReloadTime` |
| **Quick mag / швидке перезаряджання** | ⚙️ −ReloadTime | ⚙️ −MagazineSize | `ReloadTime`, `MagazineSize` |
| **Приклад (Stock)** | ⚙️ +stability (−recoil, +recovery) | ➕ ↓Handling-ADS / +weight | `RecoilKick*`, `RecoilRecoverySpeed`, (Handling) |
| **Рукоятка (Grip)** | ⚙️ −recoil (H-focus можливий) | ⚙️ дрібний trade або none (generalist) | `RecoilKickSide`, `AimFollowSharpness` |
| **Коліматор (Red-dot)** | ⚙️ −spread / +handling (дрібний) | майже none (generalist, мала магнітуда) | `SpreadAngle`, `ConeHalfAngle` |
| **Снайперський приціл (Scope)** | ➕ +view/aim-range | ➕ ↓peripheral-cone (FOV) / ⚙️ ↓Handling / ⚙️ +recoil | ➕ViewRange (fog-of-war), `ConeHalfAngle`, `EquipTime` |
| **Лазер / тактичне (опц.)** | ⚙️ −hip-spread | ⚙️ +ADS-spread (Duckov) | `SpreadAngle` (hip vs ADS — split?) |
| **Payload-specific (напр. Laser focusing lens)** | ⚙️ −ChargeTime | ⚙️ −Damage-scaling | `ChargeTime`, charge curve |

**Спостереження:** ~70% мапиться на **наявні** поля. Бракує трьох механік для «класичних» tradeoff-ів: **Noise**, **Handling/ADS-speed**, **ViewRange/FOV-tradeoff** (приціл).

---

## 4. Gaps — стат-осі, яких бракує (рішення на майбутнє)

Ранжовано за value/cost для attachment-tradeoff-ів:

1. **Handling / ADS-speed як per-weapon стат** — _high value, medium cost._
   Зараз ADS — глобальні множники. Без per-weapon handling приклад/мага не можуть тюнити «швидкість прицілювання» — а це головна tradeoff-вісь у Tarkov/Destiny/Division (Ergonomics/Handling). Варіанти: (а) новий стат `Handling` (aggregate), (б) per-weapon `AdsTransitionTime` + reuse `EquipTime`. → **гарячий кандидат №1.**

2. **Noise / sound signature → bot aggro** — _high value, higher cost (нова механіка)._
   Зараз боти чують **лише рух** (`BotPerceptionSystem.HearingRange` × `player.Velocity`), НЕ постріли. `WeaponFired` подія існує (VFX/casings), але не задіяна для aggro. Щоб глушник був справжнім sidegrade (стелс vs DPS — ядро ZS/Duckov), треба: боти реагують на `WeaponFired` у радіусі → стат `NoiseRadius` → suppressor його зменшує. Без цього глушник = беззмістовний. → **гарячий кандидат №2** (але це окрема механіка perception, не лише стат).

3. **View / aim range (для прицілу на top-down)** — _medium value, зав'язано на fog-of-war._
   Range у top-down = «як далеко видно вперед» (ZS звужує конус; Duckov розширює ADS-range axis-aware). У нас Є fog-of-war + ADS — приціл логічно тюнить саме їх. Потрібен стат на кшталт `AimViewRange` / `VisionConeAngle`-mod. Робить «снайперський приціл» справжнім tradeoff (бачиш далі вперед / вужче по боках). → **кандидат №3**, інтеграція з fog-of-war.

4. **Weight / encumbrance** — _medium value, нова інфра._
   Items не мають weight (лише броня має weight→speed factor — є precedent). Вага — улюблена «третя вісь» Tarkov/Duckov (recoil-білд важкий → −mobility). Корисно, але це нова inventory-механіка. → **опційно**, можна відкласти; tradeoff-и працюють і без неї (opposing-axis + slot scarcity достатньо).

5. **Effective range / damage falloff** — _low value зараз._
   Damage не падає з дистанцією (`ProjectileLifetime × Speed` = hard max, без falloff). Optimal-range стат (Division) дав би ще одну вісь, але це міняє core combat math. → **не чіпати у цьому епіку.**

6. **ADS vs hip spread split** — _low-medium._
   Зараз `SpreadAngle` єдиний; ADS впливає глобально. Duckov розділяє ADS-spread vs hip-spread (лазер: −hip, +ADS). Дало б тонші tradeoff-и для tactical-attachment-ів. → **nice-to-have.**

---

## 5. Пропонований attachment-tunable стат-surface (leaning)

Мінімальний набір осей, що (а) маємо або дешево додаємо, (б) дають транспарентні opposing-axis tradeoffs, (в) читаються на top-down:

**Tier 0 (наявні поля — нуль нової інфри):**
- `Damage` ⇄ `RecoilKick*` / `SpreadAngle` (damage-muzzle)
- `MagazineSize` ⇄ `ReloadTime` (mag-и)
- `RecoilKickForward` / `RecoilKickSide` / `RecoilRecoverySpeed` (приклад/гальм/рукоятка)
- `SpreadAngle` / `ConeHalfAngle` (коліматор, accuracy)
- `ProjectileSpeed` (suppressor velocity-trade)
- `ChargeTime` (Laser focusing — payload-specific)
- `HeatLevel`-coupling (sustained-fire trade)

**Tier 1 (1 новий per-weapon стат):**
- ➕ **Handling / ADS-speed** — розблоковує найважливішу вісь (приклад/мага: stability/capacity ⇄ handling).

**Tier 2 (нові механіки, окремі рішення):**
- ➕ **Noise → aggro** (suppressor stealth-tradeoff) — найбільший «extraction-feel» payoff, але окрема perception-механіка.
- ➕ **View-range / FOV-cone** (scope) — інтеграція з fog-of-war.
- ➕ (опц.) **Weight** — третя вісь mobility.

**Канонічні opposing-axis пари для нашої гри:**

| Tradeoff-пара | Attachment-носій | Інфра |
|---|---|---|
| Magazine ⇄ Reload | extended / quick mag | ⚙️ є |
| Damage ⇄ Recoil/Spread | damage-muzzle | ⚙️ є |
| Recoil/Stability ⇄ Handling | приклад / гальм | ➕ Handling |
| Noise ⇄ Damage/Velocity | глушник | ➕ Noise |
| View-range ⇄ Peripheral-cone/Handling | приціл | ➕ FOV |
| Fire-rate ⇄ Recoil/Heat | (delivery-залежно) | ⚙️ є |

---

## 6. Open questions → наступна ітерація

- ~~**Q12. Handling-стат: aggregate чи композит?**~~ ✅ **RESOLVED 2026-06-07** — **aggregate `Ergonomics`** (один множник масштабує ADS-speed + equip/unequip + move-mult + turn-rate).
- ~~**Q13. Noise-механіка в scope?**~~ ✅ **RESOLVED 2026-06-07** — **так, у scope + показана** гравцю. Потребує нової perception-механіки (боти реагують на `WeaponFired` у `NoiseRadius`).
- **Q14. Приціл = яка модель?** ZS (конус↓/range↑ через fog-of-war) vs Duckov (axis-aware ADS-range). Хоч Sight Range/FOV **прихований від UI**, механіка потребує вибору. ⏳ open.
- ~~**Q15. Weight — у scope?**~~ ✅ **RESOLVED 2026-06-07** — **ні**, поза scope. Tradeoff-и тримаємо на opposing-axis + slot scarcity.
- **Q16. Скільки осей тюнить ОДИН attachment?** 1 give + 1 take (Destiny-clean) чи 2-3 (Duckov-rich)? Впливає на читабельність preview. ⏳ open.
- **Q17. Транспарентність магнітуди.** Reload уже задає прецедент «показуємо дельту %, не абсолют». Абсолютні дельти (Division green↑/red↓) vs % всюди? Транспарентний cap на стат? ⏳ partial.

---

## 7. Навігація
- [`analysis.md`](./analysis.md) — концептуальний fit + competitor synthesis (іт. 1)
- [`competitor-research.md`](./competitor-research.md) — повні per-game findings
- [`../architecture.md`](../architecture.md) §D1 — склад `WeaponStats` (7+13), composition pipeline
- [`../../weapons.md`](../../weapons.md) — runtime FSM, ammo, ADS, heat, convergence
