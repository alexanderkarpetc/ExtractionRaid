# Weapon Builder — resulting design note
_Чернетка v0.6 presentation patch_

## 1. Що це?

**Weapon Builder** — це системна фіча кастомізації зброї для нашого ретрофутуристичного топ-даун extraction shooter, у якій гравець збирає зброю з **двох головних ядер**:

- **Payload Core** — що саме зброя випускає / яку природу ураження має
- **Delivery Core** — як саме цей payload дістається цілі

Додаткові шари системи:
- **Exotic Mod** — один особливий модифікатор, який додає виразний twist
- **Typed Attachments** — додаткові типізовані слоти для тюнінгу характеристик і feel
- **Slot structure / module compatibility** — обмеження збірки задаються самою структурою слотів і сумісністю модулів
- **Rarity** — проста RPG-ієрархія сили на рівні core-модулів

**Payload Core + Delivery Core = конкретний архетип зброї**

Архетип — це **explicit label**, який система генерує з комбінації двох cores. Гравець бачить назву (напр. "Laser Rifle", "Foam Shotgun") і одразу розуміє, що це за зброя.

### Чим це не є
- Це **не класичний gunsmith**, де гравець бере готовий ствол і обвішує його дрібними статовими модами. Тут головна різниця між збірками народжується з **комбінації Payload Core і Delivery Core**, а не з дрібного тюнінгу.
- Це **не crafting system** — гравець не збирає зброю з десятків ресурсів за рецептом. Він комбінує готові модулі, знайдені як лут.
- Це **не skill tree для зброї** — прогресія йде через знаходження кращих модулів, а не через прокачку дерева.

### Бойова фантазія
Weapon Builder підтримує fantasy світу, де зброя — це суміш:
- industrial salvage-tech
- військових залишків
- небезпечних прототипів
- дивних, але потужних модульних зброєформ

---

## 2. Які проблеми вирішує?

### Проблеми без Weapon Builder
- **Зброя — це fixed items без ownership.** Гравець знаходить готову пушку і користується нею as is. Немає відчуття "це моя збірка".
- **Лут одноманітний.** Кожна пушка або строго краща, або строго гірша за іншу. Немає lateral variety — тільки вертикальна ієрархія.
- **Немає причини тримати кілька збірок.** Знайшов найкращу пушку — все інше сміття.
- **Рейди не відрізняються по loadout.** Немає підготовки під конкретний рейд, бо зброя не адаптується під ситуацію.

### Що Weapon Builder дає
- Гравець отримує зрозумілу, але глибоку систему: **що стріляє + як стріляє**
- Різні комбінації створюють **різні бойові архетипи**, а не просто варіації статів
- Система добре лягає на **лут, extraction loop і chase за модулями**
- Дає простір для **ретрофутуристичних і дивних комбінацій**
- Спрощує баланс, бо головна ідентичність зброї живе у двох основних ядрах

### Чому це потрібно грі
- Підвищує цінність луту, бо гравець шукає не тільки готові стволи, а й core-модулі
- Робить рейди більш різними, бо різні збірки вирішують різні бойові задачі
- Дає системний identity грі через зброю, яку можна не просто знайти, а зібрати

---

## 3. Hard rules

- Кожна збірка має рівно **один Payload Core**
- Кожна збірка має рівно **один Delivery Core**
- Кожна збірка може мати не більше **одного Exotic Mod**
- **Rarity** застосовується до **Payload Core** і **Delivery Core**
- Ліміти збірки визначаються слотами і сумісністю модулів, а не окремим hidden budget
- **Typed Attachments** не є частиною поточного active scope
- За замовчуванням система прагне до широкої сумісності між Payload і Delivery, але окремі комбінації можуть бути явно заборонені design-рішенням

---

## 4. Current scope vs target shape

### Target shape
Повна цільова форма Weapon Builder:
- Payload Core
- Delivery Core
- Exotic Mod
- Typed Attachments
- Slot structure / module compatibility
- Rarity
- loot / crafting / extraction integration

### Current approved scope
Поточний активний фокус:
- Payload Core
- Delivery Core
- Exotic Mod
- slot structure / module compatibility
- high-level Rarity

Поки не є активним фокусом:
- Typed Attachments у деталях
- точні правила slot compatibility
- точна integration частина rarity у лут і крафт

### Production framing
У перший вертикальний зріз входить лише:
- **4 Payload Core**
- **6 Delivery Core**
- **5 Exotic Mod**
- без деталізованих Typed Attachments
- без повної economy частини Hidden Budget
- без повної loot/crafting інтеграції rarity

---

## 5. Як це виглядає для гравця

### Very high-level player flow
Збірка зброї відбувається **на базі** (не в рейді).

1. Гравець обирає **Payload Core**
2. Гравець обирає **Delivery Core**
3. Гра показує **архетип зброї**, який із цього виходить
4. Опційно додається **Exotic Mod**
5. Гравець бачить підсумок: що це за зброя, наскільки вона сильна, які в неї особливості та обмеження

### Як система не перевантажує гравця
Перший досвід гравця будується на простих core-комбінаціях.  
Складність наростає поступово: спочатку `Payload + Delivery`, потім rarity, потім exotic, і лише пізніше — глибші шари на кшталт typed attachments.

---

## 6. Системні шари

### 6.1. Payload Core
Визначає:
- природу ураження
- damage fantasy
- secondary effect
- тип боєприпасу або тех-заряду

### 6.2. Delivery Core
Визначає:
- форму стрільби
- темп використання
- геометрію ураження
- спосіб доставки payload до цілі

### 6.3. Exotic Mod
Один спеціальний модифікатор, який додає виразний twist.

Може змінювати:
- trajectory
- projectile behavior
- firing pattern
- resource rhythm

Exotic layer навмисно не обмежений одним типом модифікації; його задача — давати найсильніший окремий twist поверх базового архетипу.

### 6.4. Typed Attachments
Другорядні модулі, які не визначають базову ідентичність зброї, а тюнять її.

Базовий принцип:
- слоти прив’язані не до всієї зброї загалом, а до конкретних core-модулів

### 6.5. Slot structure / module compatibility
У поточному дизайні **окремого hidden budget немає**.

Ліміти збірки задаються:
- кількістю слотів
- типом слотів
- сумісністю модулів між собою

Принцип:
- якщо слот є і модуль сумісний, його можна встановити
- якщо слота немає, модуль встановити не можна
- якщо модулі несумісні, така комбінація забороняється rules-рівнем, а не бюджетом

Тобто система обмежує гравця не прихованими числами, а **явною структурою збірки**.

### 6.6. Rarity
Rarity живе на рівні core-модулів і читається як:
> **”це та сама штука, але краща”**

Принцип:
- **power first, variation second**
- модуль не перетворюється на інший модуль
- rarity не повинна ламати архетип
- вища rarity = **кращі стати** (damage, fire rate, penetration тощо — залежно від типу модуля)

RPG rarity tiers:
- **Common**
- **Uncommon**
- **Rare**
- **Epic**
- **Legendary**

---

## 7. Approved core set

### Payload Core
- **Ballistic Round** — стандартний твердий снаряд; grounded baseline
- **Micro-Rocket** — малий ракетний або вибуховий заряд; explosive chaos
- **Laser Charge** — лазер з зарядкою (charge-up before firing); clean sci-fi identity, прямий реф — лазер з Half-Life 1
- **Adhesive Foam** — payload не для burst damage, а для примусового контролю дистанції й руху ворога через slow / sticking / movement denial

### Delivery Core
- **Single-Action Delivery** — один важкий постріл з high commitment
- **Auto Delivery** — безперервна або майже безперервна автоматична стрільба
- **Rotary Delivery** — розкрутка перед вогнем і дуже високий темп
- **Scatter Delivery** — близький залп / конус / shotgun-like pattern
- **Swarm Delivery** — spectacle delivery для volley of micro-projectiles, а не homing system
- **Fist Delivery** — контактний delivery core для ударних, вприскувальних або імпульсних зброєформ

### Exotic Mod
- **Ricochet** — снаряд відбивається від поверхонь і може вразити додаткові цілі або дістати ціль за укриттям
- **Split on Impact** — снаряд розпадається на кілька осколків при влучанні, даючи AoE або додаткове ураження навколо точки контакту
- **Ammo Return on Kill** — вбивство повертає частину витрачених боєприпасів у магазин, нагороджуючи агресивну гру
- **Boomerang Flight** — снаряд летить по дузі і повертається назад, даючи шанс вразити ціль двічі або дістати з незвичного кута
- **Multi-Shot Pattern** — один постріл випускає кілька снарядів у фіксованому патерні (напр. горизонтальна лінія, трикутник), на відміну від random spread у Scatter Delivery

### Approved high-level systems
- **Rarity** — concept approved
- **Slot structure / module compatibility** — concept approved

### Deferred for later
- Typed Attachments details
- expanded payload library
- expanded delivery library
- detailed loot / crafting / rarity economy

---

## 8. Поточні висновки по матриці Payload × Delivery

### Найсильніші payload-и
- **Ballistic Round** — найкращий baseline payload
- **Laser Charge** — найсильніший clean sci-fi payload
- **Micro-Rocket** — найяскравіший хаотичний payload
- **Adhesive Foam** — найсамобутніший utility/control payload

### Найсильніші delivery для ранньої розробки
- **Single-Action Delivery**
- **Auto Delivery**
- **Scatter Delivery**
- **Fist Delivery**

### Delivery, які дають сильний character layer, але дорожчі по scope
- **Rotary Delivery**
- **Swarm Delivery**

### Найсильніші комбінації для перших прототипів
Критерій відбору: комбінація **gameplay value** (наскільки цікаво грати), **простоти реалізації** (наскільки дешево зробити) і **design confidence** (наскільки ми впевнені, що це працює).

- Ballistic + Single-Action
- Ballistic + Auto
- Ballistic + Scatter
- Laser + Single-Action
- Laser + Auto
- Laser + Fist
- Adhesive Foam + Auto
- Adhesive Foam + Scatter
- Adhesive Foam + Fist
- Micro-Rocket + Single-Action
- Micro-Rocket + Fist

---

## 9. Пріоритет розробки

### Payload Core
1. Ballistic Round
2. Laser Charge
3. Adhesive Foam
4. Micro-Rocket

### Delivery Core
1. Single-Action Delivery
2. Auto Delivery
3. Scatter Delivery
4. Fist Delivery
5. Rotary Delivery
6. Swarm Delivery

### Exotic Mod
1. Multi-Shot Pattern
2. Ricochet
3. Split on Impact
4. Ammo Return on Kill
5. Boomerang Flight

### Порядок реалізації
1. Ballistic + Single-Action
2. Ballistic + Auto
3. Ballistic + Scatter
4. Laser + Single-Action
5. Laser + Auto
6. Adhesive Foam + Auto
7. Adhesive Foam + Scatter
8. Fist Delivery на Ballistic / Laser / Adhesive Foam
9. Micro-Rocket + Single-Action
10. Micro-Rocket + Fist
11. Rotary Delivery
12. Swarm Delivery
13. Exotic Mods

---

## 10. Чому chase не закінчується після першої сильної збірки

Навіть якщо гравець уже знайшов сильний і улюблений архетип, мотивація шукати нові модулі лишається через:
- **кращу rarity-версію** того ж Payload або Delivery
- **інший delivery** під інший стиль рейду
- **інший payload** під інший тип ворогів або ситуацій
- **рідкісний Exotic Mod**
- майбутню **синергію з Typed Attachments**, коли цей шар буде введений

Тобто chase будується не лише на принципі “знайти ще сильнішу пушку”, а на принципі:
> **знайти кращу або більш підходящу версію улюбленого архетипу**

---

## 11. Що поки не проробляємо

- формули статів
- повний список Typed Attachments
- UI-флоу збірки в деталях
- crafting recipes
- exact loot distribution
- повну allowed / banned matrix для всіх комбінацій
- точні бонуси кожного rarity tier
- точну інтеграцію rarity у loot / crafting / extraction economy

---

## 12. Стислий опис фічі

**Weapon Builder** у нашому шутері — це система, де гравець комбінує **Payload Core** і **Delivery Core**, щоб отримати конкретний архетип зброї. Далі збірка може бути посилена через **Rarity**, модифікована одним виразним **Exotic Mod**, а в майбутньому — дотюнена через **Typed Attachments**. Модулі добуваються через лут і extraction loop, а межі збірки задаються **структурою слотів і сумісністю модулів**.
