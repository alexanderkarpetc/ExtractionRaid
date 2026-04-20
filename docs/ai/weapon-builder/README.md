# Weapon Builder

Системна фіча кастомізації зброї для extraction shooter. Поточна версія дизайну: **v0.7**.

---

## Короткий опис для команди

**Weapon Builder** — система кастомізації зброї, де гравець збирає зброю з двох ядер: **Payload Core** (що зброя випускає) і **Delivery Core** (як це дістається цілі). Комбінація двох cores визначає архетип зброї з explicit назвою (напр. "Laser Rifle", "Foam Shotgun"). Поверх архетипу можна додати один **Exotic Mod** — виразний twist поведінки снаряду або ресурсного ритму.

**Payload Cores (4):** Ballistic Round (стандартний снаряд), Micro-Rocket (вибуховий заряд), Laser Charge (charge-up лазер, реф Half-Life 1), Adhesive Foam (slow/sticking/movement denial). **Delivery Cores (6):** Single-Action (один важкий постріл), Auto (безперервна стрільба), Scatter (shotgun-like залп), Fist (контактний удар), Rotary (spin-up + високий темп), Swarm (volley мікро-снарядів). **Exotic Mods (5):** Ricochet, Split on Impact, Ammo Return on Kill, Boomerang Flight, Multi-Shot Pattern.

Система вирішує конкретні проблеми поточного стану: зброя — fixed items без ownership, лут одноманітний (пушка або строго краща, або строго гірша), немає причини тримати кілька збірок, немає підготовки loadout під конкретний рейд. Weapon Builder дає lateral variety — різні комбінації під різні ситуації замість вертикальної ієрархії сили. Збірка відбувається на базі. Модулі мають **Rarity** (вищий тір = кращі стати того ж модуля), а межі збірки задаються **структурою слотів і сумісністю модулів** — явними структурними правилами, а не прихованими числовими бюджетами.

**Обсяг реалізації для vertical slice:** 6 різних shooting behaviours (кожен Delivery Core — окрема механіка стрільби), 4 damage/effect pipelines (кожен Payload Core — свій тип ураження), 5 модифікаторів поведінки (Exotic Mods), система Rarity з 5 тірами (Common → Legendary), Slot structure / module compatibility rules, data-driven weapon assembly замість поточних hardcoded factory methods, UI збірки на базі. Не входить у vertical slice: Typed Attachments, crafting recipes, loot distribution, повна economy.

---

## Навігація

### Дизайн
- [design.md](./design.md) — поточний дизайн-док v0.7, source of truth по фічі

### Архітектура та імплементація
- [architecture.md](./architecture.md) — технічна архітектура, як фіча лягає на існуючу кодбазу *(living doc, заповнюється в міру обговорень)*

### План та статус
- [plan/roadmap.md](./plan/roadmap.md) — ultimate план реалізації по tiers (архітектурні питання + work items + exit criteria)
- [plan/tasks.md](./plan/tasks.md) — конкретні задачі реалізації з checkbox'ами (оновлюється по ходу коду)
- [plan/status.md](./plan/status.md) — living doc з рішеннями, відкритими питаннями, блокерами

---

## Принципи організації документації

- **Концептуальні доки** (design, architecture, per-module specs) живуть довго і описують систему
- **Фазові/планові доки** (status, roadmap) живуть час реалізації і фіксують прогрес
- **Нові доки створюються в міру потреби**, а не заздалегідь — порожні файли лише створюють шум
- Коли з'являться per-module spec'и (payload-cores.md, delivery-cores.md тощо) — вони ляжуть у `modules/` і `systems/` відповідно
