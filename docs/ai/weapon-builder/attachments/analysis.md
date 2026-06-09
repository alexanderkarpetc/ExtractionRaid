# Weapon Attachments / Modules — Analysis (Iteration 1)

> **Epic:** додати **модулі** (attachments) на зброю, зібрану у Weapon Builder.
> **Статус:** 🔬 дослідження / аналіз задачі. Жодного коду. Жодних фіналізованих дизайн-рішень — лише leanings + open questions.
> **Дата:** 2026-06-07.

---

## 0. Що це за епік і як він лягає на існуючий концепт

**Глобальна ідея (user framing):** модулі — це **покращення, які НЕ змінюють головну логіку зброї**. За «логіку» (що випускає / як доставляє) відповідають **Payload** і **Delivery**. Модулі лише **тюнять характеристики й feel**: розширений магазин, глушник, коліматорний / снайперський приціл, приклад, рукоятка, дульний гальм тощо.

**Ключовий зв'язок:** це фактично **активація шару «Typed Attachments»**, який уже закладений у [`design.md`](../design.md) як частина _target shape_, але був відкладений (deferred for later). Ми не вигадуємо з нуля — є концептуальний каркас:

- **design.md §6.4 Typed Attachments:** _«Другорядні модулі, які не визначають базову ідентичність зброї, а тюнять її. Слоти прив'язані не до всієї зброї загалом, а до конкретних core-модулів.»_
- **design.md §6.5 Slot structure:** _«Ліміти збірки задаються кількістю слотів, типом слотів і сумісністю модулів — НЕ прихованим budget.»_
- **design.md §4:** Typed Attachments — у target shape, але поза поточним active scope. **§11:** «повний список Typed Attachments» — ще не пророблений.

> ⚠️ **Термінологічна колізія.** У кодбазі «module» вже означає Payload/Delivery core (`ModuleCache`, "Spawn Module", `WeaponModuleFlavor`). Цей новий шар у design.md названо **Typed Attachments**. Треба обрати термін і не плутати. Кандидати: **Attachments** (відрізняє від cores) vs **Modules** (user-facing слово користувача). → open question Q9.

---

## 1. Центральне питання: чи вкладаються stat-модулі в наш концепт?

### Напруга, яку треба чесно назвати

Весь Weapon Builder існує, щоб вбити **вертикальну ієрархію луту** (design.md §2):

> _«Лут одноманітний. Кожна пушка або строго краща, або строго гірша. Немає lateral variety — тільки вертикальна ієрархія.»_

Відповідь системи — **lateral variety**: Payload × Delivery = різні архетипи під різні ситуації, а не «сильніша/слабша пушка». Єдина санкціонована вертикаль — **Rarity** («та сама штука, але краща», power first).

**Ризик модулів:** модуль, що **просто покращує** стати без ціни (напр. «Extended Mag = +ємність, і все»), **повертає рівно ту вертикальну ієрархію, проти якої будувалась уся система**. «Пушка з модулем» стає _строго кращою_ за «пушку без». Це і є anti-pattern.

### Вердикт: ✅ вкладаються — але ЛИШЕ як sidegrades (tradeoffs), не pure upgrades

Це підтверджують **усі без винятку конкуренти**: робочі attachment-системи будуються на **компромісах**, а не на чистих апгрейдах. Приклади (деталі в §3):

| Модуль | Дає | Бере |
|---|---|---|
| Глушник (ZS / Duckov / Tarkov) | −шум, −recoil | −damage, −velocity / +heat / −ergo |
| Розширений магазин (Duckov / Destiny) | +ємність | −швидкість reload / −ADS speed |
| Приціл 2x (Duckov) | +72% дальність прицілу | +20% vertical recoil, +0.1s aim time |
| Снайп. приціл (ZERO Sievert) | +дальність огляду вперед | **звужує конус зору** (FOV ↓) |
| Приклад/рукоятка (Tarkov) | +стабільність | +вага → −mobility, −ergo → −ADS |

**Чому це не просто «ок», а ідеальний матч із філософією:** sidegrade-модуль додає **третю lateral-вісь варіативності**, а не вертикальну:

- **Payload** = _що_ зброя випускає (identity + логіка)
- **Delivery** = _як_ доставляє (identity + логіка)
- **Attachments** = _під яку ситуацію затюнено_ (stat/feel, lateral) ← **новий шар**
- **Exotic** = один виразний behavioral twist (Ricochet/Split — логіка снаряда)
- **Rarity** = вертикальна вісь на cores

Тобто модулі-sidegrades **поглиблюють ту саму ідею** («різні збірки під різні рейди»), а не суперечать їй. Stealth-білд (глушник, −DPS) vs sustained-білд (extended mag, −reload) vs long-range білд (приціл, −close-quarters) — це **причини тримати кілька збірок улюбленого архетипу**, що прямо закриває проблему design.md §2 («немає причини тримати кілька збірок»).

### Важливі нюанси з research

1. **Не КОЖЕН модуль мусить бути двостороннім.** Tarkov: окремий muzzle brake — майже чистий апгрейд; tradeoff живе на рівні **всієї збірки** (slot occupancy + вага + ammo interplay). Destiny: кілька дрібних no-downside generalist-модулів ОК, **бо cap+interpolation тримає їхню магнітуду тривіальною**. → Правило: _більшість_ модулів = явні tradeoffs; кілька дрібних generalist-ів припустимі, якщо їхній ефект малий.

2. **Анти-creep інструмент, який МИ ВЖЕ обрали, збігається з найнадійнішим у конкурентів.** Наш design свідомо: **slot structure + compatibility, БЕЗ hidden budget**. Найробастніші транспарентні інструменти конкурентів — саме **opposing-axis tradeoffs + slot scarcity + situational value**. А Tarkov-івський «hidden formula» прямо названо anti-pattern (і в нашому [`competitor-reference-db.md`](../../competitor-reference-db.md), і самою спільнотою Tarkov). → **Спираємось на транспарентні tradeoffs + дефіцит слотів, уникаємо прихованих бюджетів.** Філософськи чисто.

3. **Top-down знімає вимогу до арту модулів** (критично для нас — «no UI artist / no mod art»). На top-down масштабі гравець фізично не бачить глушник чи приціл на стволі. Duckov взагалі НЕ міняє модель зброї — стати застосовуються, ствол не змінюється. Сигнал про модифікований стан іде через **HUD/числа + projectile VFX + crosshair + audio**, а не силует. У нас уже є рівно ці поверхні (SDF-crosshair виражає spread/recoil; projectile trails; ammo HUD). → деталі §4.

---

## 2. Як це лягає на нашу архітектуру (попередньо)

Хороша новина — гачки вже існують:

- **`WeaponStats`** містить рівно ті поля, які модулі тюнили б: `MagazineSize`, `ReloadTime`, `SpreadAngle`, `ConeHalfAngle`, `RecoilKickForward/Side`, `RecoilRecoverySpeed`, `AimFollowSharpness`, `EquipTime/UnequipTime`, `ProjectileSpeed`, `BasePenetration`, `BaseBleedChance`…
- **`WeaponStatComposer.Compose(...)`** уже приймає Payload+Delivery (+Exotic no-op). Модулі = **новий compose-канал** поверх (як rarity/ammo/exotic — окремі канали, що складаються).
- **Heat вже є** (`WeaponHeatSystem`) → tradeoff «глушник: −шум / +heat» (Tarkov) лягає природно.
- **Modules-as-items інфра вже є** (Tier 6: payload/delivery — items, дропають, консьюмляться при Build). Attachments-як-items — те саме розширення.
- **Builder UI вже має preview-pane + drag-drop palette/slots** → live stat-preview модуля (must-have UX, §5) і слоти для attachment-ів кладуться у наявний паттерн.
- **Slots, прив'язані до cores** (design.md §6.4): muzzle/magazine-слот логічно належить **Delivery**, focusing-lens — **Payload** (Laser), приціл — можливо whole-weapon. → open question Q2.

**Чим модуль ВІДРІЗНЯЄТЬСЯ від Exotic** (важливо не змішати):
- **Exotic** = один behavioral twist, змінює _логіку снаряда/firing_ (Ricochet, Split, Boomerang). «Wow».
- **Attachment/Module** = stat/feel-тюнінг, **НЕ чіпає логіку** (user framing). «Та сама пушка, затюнена під ситуацію».
- Сірі зони (firing-mode swap, тип боєприпасу) → треба явно вирішити, де межа. SYNTHETIK «Hair Trigger» (burst→semi) — це зміна Delivery-логіки, тож у нас радше Exotic/out-of-scope. → Q6.

---

## 3. Аналіз конкурентів (synthesis)

Повна матриця з джерелами — у [`competitor-research.md`](./competitor-research.md). Тут — стисла суть.

### Зведена таблиця

| Гра | Слоти | Install UX | Анти-creep | Top-down урок |
|---|---|---|---|---|
| **ZERO Sievert** ★ (прямий конкурент) | scope/muzzle/barrel/handguard/stock/grip/mag + 4 aux; per-weapon | **Лише workbench, між рейдами, зброя unequipped** | tradeoffs (ergo↓/recoil↓/spread↑) + рідкість + сумісність | **Приціл звужує конус зору** (range↑, FOV↓) — зав'язано на fog-of-war |
| **Escape from Duckov** ★ (mechanics ref) | scope/muzzle/grip/stock/tactical/mag + **barrel (locked → unlock на workbench)** | **Drag-drop у будь-де, навіть у рейді**; підсвітка сумісних слотів | явні % tradeoffs + **вага** + **Quality tier** + slot-unlock | приціл = **axis-aware дальність огляду** (не zoom); ~38m Y vs 25m X; **модель не змінюється** |
| **Escape from Tarkov** | рекурсивне **дерево слотів** (structural vs leaf); каліброві/mount правила | stash/loadout; presets; in-raid field-mod можливий | трикутник **ergo↔recoil↔weight**; tradeoff на рівні всієї збірки | «hidden formula» = **anti-pattern** (виправлено 2023, але dispersion ще прихований) |
| **Arena Breakout: Infinite** | ~20+ слотів, mount-first prereq | **auto-buy відсутніх частин**; live readout + 3D preview | tradeoff-матриця (suppressor: recoil↓/ergo↑/ADS↓) | транспарентні стати + 3D-preview = сучасний accessible-стандарт |
| **SYNTHETIK** ★ (top-down cousin) | **макс 4 attachment-слоти** + kit-апгрейди + global modules | mid-run, на equipped зброї | **двосторонні** attachments (dmg↔accuracy/mobility); 12 апгрейдів → cap | **читабельність через bars/числа/audio, НЕ модель**; mag-число лише near-empty |
| **Enter the Gungeon** | синергії (комбо), не слоти | автоматично при наявності предметів | здебільшого upside, але RNG-gated | **recolor-on-transform** + distinct projectile = legible mod-state |
| **Nuclear Throne** | mutations (level-up перки) | вибір 1 з 4 при левелі | **conditional value** (Eagle Eyes гарний на single-shot, поганий на spread) | mutations = статична icon-strip, ніколи не clutter |
| **Division 2** | 4 слоти (optic/muzzle/mag/underbarrel); **set-stats, не roll** | будь-коли; **найкращий live stat-preview** (green↑/red↓) | деякі pure-positive; баланс живе у gear-score, не модулях | **Cautionary tale:** D1 мав roll-tradeoff-модулі → decision fatigue → D2 відкотив до fixed/mostly-positive |
| **Destiny 2** ★ (tradeoff-модель) | **колонки** (Barrel/Mag/Trait), 1 опція на колонку | weapon-inspect; community-preview-тули | **opposing-axis слайдери** (Range⇄Stability, Mag⇄Reload) + **hidden cap-budget (100) + diminishing interpolation** + archetype-envelope | — |
| **Borderlands 3** | RNG-генеровані частини, **немає install-кроку** | — (loot-grind і є UX) | RNG-дисперсія + manufacturer-identity = sidegrade; level-treadmill | — |

### Анти-power-creep інструменти (рейтинг за надійністю)

1. **Opposing-axis tradeoff** (Destiny) — кожен gain прив'язаний до парного loss на стат, що теж важливий: Range⇄Stability, Mag⇄Reload, Damage⇄RateOfFire. **Найробастніший, транспарентний.** ← наш головний кандидат.
2. **Slot scarcity / opportunity cost** (усі) — фіксована мала к-ть слотів; навіть pure-positive модуль конкурує за слот. ← у нас уже філософія (design.md §6.5).
3. **Situational value** (NT, BL3 елементи, range-залежні приціли) — «найкращий» залежить від дистанції/ворога/стилю. **Найдешевший в авторингу** (без stat-математики).
4. ~~Hidden stat-budget + cap + diminishing returns (Destiny)~~ — потужний, АЛЕ **суперечить нашому правилу «no hidden budget»** + anti-pattern «hidden formula». **Уникаємо** (або робимо транспарентний cap).
5. **Вага / encumbrance** (Tarkov, Duckov) — ціна на іншій осі (stamina/mobility). Залежить, чи маємо ми encumbrance. → Q7.
6. **Acquisition friction / rarity-gating** (Division, BL3) — керує кривою потужності, але **не створює цікавих per-mod рішень**. Доповнення, не основа.

> **Головна засторога (Division 1→2):** per-mod tradeoffs дають глибину, але коштують UX-ясності й створюють decision fatigue + inventory clutter. Division це навіть **відкотила**. Висновок: tradeoffs ТРЕБА парувати з **сильним live stat-preview** (§5), інакше гравець не зчитує give/take.

---

## 4. Top-down читабельність (критично — наш найбільший constraint)

**Головний урок (SYNTHETIK / Gungeon / NT / Duckov):** на top-down масштабі **фізичний attachment на стволі невидимий**. Тому модулі комунікуються через _наслідки_, не _деталь_:

- **HUD / числа** — повний stat-breakdown у Builder/inventory, **не** у бойовому HUD.
- **Crosshair** — у нас SDF-crosshair уже виражає spread/recoil. Модуль, що розширює spread / міняє recoil, **видимий через crosshair** автоматично. Це наша головна перевага.
- **Projectile VFX / tracer** — Gungeon «recolor-on-transform»: колір/форма траси може кодувати модифікований стан.
- **Audio** — suppressed = тихіше (і геймплейно: менший aggro-радіус ботів).
- **Бойовий HUD — мінімальний**: SYNTHETIK показує mag-число лише near-empty; повний breakdown — в окремій панелі. Збігається з нашою restrained-tactical естетикою.

**Наслідки:**
- **Арт модулів НЕ потрібен** (Duckov шипить без зміни моделі) — величезне полегшення під «no UI artist».
- **Приціл у top-down ≠ zoom камери.** Дві робочі моделі: ZS — _view-shape tradeoff_ (дальність вперед↑ / конус↓, зав'язано на fog-of-war, який у нас Є); Duckov — _axis-aware дальність огляду_ при ADS. Це робить «снайперський приціл» справжнім sidegrade на top-down. → Q8.

---

## 5. UX-патерни (що красти)

- **Live stat-preview** (Division 2 / Destiny / ABI) — **must-have**. Наш Builder уже має preview-pane; розширити на дельти від модуля (green↑/red↓). Без цього tradeoffs нечитабельні.
- **Drag-drop + підсвітка сумісних слотів** (Duckov) — легший, action-friendly; у нас уже є drag-drop palette/slots.
- **Auto-fill / auto-buy відсутніх частин** (ABI) — friction-killer для preset-білдів. Можливо пізніше.
- **Install location:** workbench-only (ZS, friction+risk) vs anywhere-incl-raid (Duckov, SYNTHETIK). У нас Workbench/Builder уже канон → модулі ставимо там. Чи дозволяти field-swap — Q5.
- **Set-stats, не random rolls** (Division 2 свідомо прибрала roll-и) — для нас простіше й транспарентніше; rarity може масштабувати магнітуду детерміновано.

---

## 6. Tentative leanings для нашої гри (НЕ рішення — напрямок)

> Це не зафіксовані рішення, а робочі гіпотези на основі analysis. Фіналізуємо в наступних ітераціях.

1. **Модулі = sidegrade-шар** з **транспарентними tradeoffs**, без hidden budget (матч філософії + уникає Tarkov anti-pattern).
2. **Анти-creep:** opposing-axis tradeoffs (#1) + slot scarcity (#2) + situational value (#3). Вагу/rarity — як вторинні важелі, якщо доречно.
3. **Top-down:** сигнал через crosshair/projectile/HUD/audio; **арту модулів не робимо**.
4. **UX:** live stat-preview у Builder (розширити наявний); install на Workbench; set-stats (не rolls).
5. **Архітектура:** модулі = новий compose-канал у `WeaponStatComposer`; слоти прив'язані до cores (design.md §6.4); attachments-як-items поверх Tier 6 інфри.
6. **Чіткий поділ із Exotic:** модулі НЕ міняють логіку. Behavioral-речі лишаються Exotic/out-of-scope.

---

## 7. Open questions → наступна ітерація

- **Q1. Список модулів (MVP).** Який стартовий набір? (напр. muzzle: глушник / гальмо / damage-brake; mag: extended / quick; optic: red-dot / scope; stock; grip.) Кожен — give/take.
- **Q2. Прив'язка слотів.** Слоти — на Payload, Delivery, чи whole-weapon? (muzzle/mag → Delivery? lens → Payload(Laser)? optic → whole?)
- **Q3. К-ть слотів.** Фіксована мала (SYNTHETIK 4, Division 4)? Per-archetype різна? Unlock-progression (Duckov barrel)?
- **Q4. Rarity на модулях?** Cores мають rarity, Exotic — ні. Якщо так — rarity масштабує _benefit_, але чи зменшує _cost_? (зменшення cost → pure upgrade, небезпечно).
- **Q5. Install location / field-swap.** Лише Workbench, чи дозволити swap у рейді (Duckov)?
- **Q6. Межа Module ↔ Exotic.** Firing-mode swap, тип боєприпасу, charge-tuning — модуль чи exotic? Де лінія «не міняє логіку»?
- **Q7. Вага / encumbrance.** Чи є в нас система ваги, щоб задіяти її як tradeoff-вісь? (інакше відпадає інструмент #5).
- **Q8. Приціл на top-down.** ZS-модель (конус↓/range↑, через fog-of-war) vs Duckov (axis-aware ADS-range)? Що сумісне з нашим fog-of-war + ADS?
- **Q9. Термінологія.** «Attachments» (відрізняє від cores) vs «Modules» (слово user-а). Узгодити, бо «module» зайнятий.
- **Q10. Сумісність.** Як вирішуємо, що з чим стикується? (per-archetype tags, як Duckov «BR»; чи універсально).
- **Q11. Compose-математика.** Additive % vs multiplicative; порядок із rarity/ammo/exotic каналами; чи є транспарентний cap.

---

## 8. Джерела

Повний перелік URL — у [`competitor-research.md`](./competitor-research.md). Ключові: EFT/ABI Fandom + TarkovArmory/Totov; ZERO Sievert Fandom + SteamAH + gamerblurb (FOV); Escape from Duckov BoostRoom + escapefromduckov.io (exact stat-sheets); SYNTHETIK synthetikuniverse.wiki.gg; Destiny d2.destinygamewiki.com (Barrels/Magazines/Stats) + HighGroundGaming (cap/interpolation); Division2Tracker + GameRant; Gungeon/NuclearThrone/RoR2 wiki.gg.

---

## 9. Навігація

- [`stats.md`](./stats.md) — **стат-словник (іт. 2):** на що Attachments впливають, наші стати vs конкуренти, gaps
- [`slots.md`](./slots.md) — **слот-таксономія (іт. 3):** які слоти, скільки, прив'язка до cores, rarity × slots
- [`competitor-research.md`](./competitor-research.md) — повні per-game findings із джерелами
- [`../design.md`](../design.md) §6.4 Typed Attachments, §6.5 Slot structure — концептуальний каркас
- [`../architecture.md`](../architecture.md) — D1 (WeaponStats склад), D2 (payload subclasses), composition pipeline
- [`../../competitor-reference-db.md`](../../competitor-reference-db.md) — індекс рефів за атрибутом
