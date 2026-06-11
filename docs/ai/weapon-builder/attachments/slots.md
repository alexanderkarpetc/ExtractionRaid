# Attachments — Slot Taxonomy (Iteration 3)

> Які слоти, скільки їх, до чого прив'язані (Payload / Delivery / whole-weapon), і як це взаємодіє з rarity двох cores.
> **Статус:** 🔬 аналіз + ✅ **робочий layout обрано** (для тесту, див. нижче). Продовження [`analysis.md`](./analysis.md) (Q2/Q3) + [`stats.md`](./stats.md).
> **Дата:** 2026-06-07 (аналіз) · 2026-06-10 (layout).

---

## ✅ Working layout — обрано для тесту (2026-06-10)

```
PAYLOAD  → [Buttstock] [Optic] [Magazine]      к-ть розблокованих = f(Payload rarity)
DELIVERY → [Muzzle]    [Grip]                  к-ть розблокованих = f(Delivery rarity)
         + unique mods, що підходять лише під конкретний payload/delivery
```

**Зафіксовано:**
- **Вісь 1 = core-granted (1B)** — слоти приписані до cores, не whole-weapon.
- **Вісь 3 = rarity-scaled count** — к-ть слотів кожного core залежить від його rarity; комбінація рарностей двох cores = build-canvas. Крива Common→Legendary тюниться в тесті.
- **Unique (payload/delivery-specific) mods** — додаємо.

### ⚠️ Ключова clarification: слоти декаплені від stat-domain

Прив'язка слота до core **≠** хто володіє статом. `Magazine`-слот на **Payload**, але мод усе одно крутить `MagazineSize` (Delivery-composed стат). Слот = «місце повісити мод» + лічильник від rarity того core + тематична група в UI. Mod чіпає свої стати незалежно від того, чий слот його хостить.

Наслідок layout'у: **рарність Payload** контролює {Buttstock/Optic/Magazine}-кастомізацію («наскільки відточена платформа»), **рарність Delivery** — {Muzzle/Grip} («наскільки відточений firing-механізм»).

### Unique mods — без власного типу слота

Це звичайні слот-категорії з **обмеженою сумісністю** (поле `CompatibleArchetype?`; null = universal):
- *«Laser Focusing Optic»* — Optic-mod, лише **Laser** payload → ChargeTime/damage-scaling.
- *«Scatter Choke»* — Muzzle-mod, лише **Scatter** delivery → стискає spread-cone.
- *«Auto Heat-Sink»* — лише **Auto** delivery → −Heat ramp.

Дає **identity-chase** (шукаєш unique-mod під улюблений архетип — design.md §10) + лягає на compatibility-систему (Q23) природно.

### Нотатки на тест (не блокери)
1. **Buttstock (Payload) + Grip (Delivery)** обидва тюнять recoil/ergo — ок (як Tarkov stock+foregrip); Delivery-слот названо **Grip** (не «Grip/Stock»), щоб не дублювати stock.
2. **Баланс 3/2:** Payload (3) дає рарності Payload більший важіль на total slot count, ніж Delivery (2). Симетрія з §1 (Delivery робить більше) → опційний своп на 2/3. Тюнимо в тесті.

> Аналіз нижче (§1-§5) — rationale й розглянуті варіанти; layout вище його підсумовує.

---

## 1. Що ми маємо (grounded)

Наша зброя = **Payload Core + Delivery Core** (+ optional Exotic), **обидва cores мають rarity** (`PayloadCoreInstance.Rarity`, `DeliveryCoreInstance.Rarity`). На відміну від конкурентів, у яких слоти висять на монолітній зброї, наша зброя вже **композитна з двох іменованих ядер** — і design.md §6.4 прямо каже: _«слоти прив'язані не до всієї зброї, а до конкретних core-модулів»_.

### Ключове спостереження: розподіл тюнабельних стат сильно асиметричний

Хто володіє якими з наших 8 player-facing параметрів ([`stats.md`](./stats.md)):

| Параметр | Власник core | 
|---|---|
| Damage | **Payload** |
| Headshot Mult | **Payload** |
| Rate of Fire | **Delivery** |
| Magazine Size | **Delivery** |
| Recoil (V+H+recovery) | **Delivery** |
| Accuracy / Spread | **Delivery** |
| Ergonomics | **Delivery** (equip/turn) + whole-weapon (move/ADS) |
| Reload Time | **Delivery** |
| Noise | **Delivery** (механізм пострілу) |
| Sight Range / FOV | whole-weapon (aiming) |

**Висновок:** Payload — це «бойова частина» (warhead): володіє лише Damage + Headshot. Delivery — «механізм»: володіє майже всім тюнабельним. → природно, що **Delivery несе більше attachment-слотів** (muzzle/mag/grip), а **Payload — мало** (optic/payload-specific). Це не довільно — це випливає з того, що кожен core може фізично модифікувати.

---

## 2. Що роблять конкуренти — спектр моделей

Зі [`competitor-research.md`](./competitor-research.md), від найпростішого до найскладнішого:

| Модель | Хто | Як | Authoring | Читабельність |
|---|---|---|---|---|
| **Generic untyped slots** | SYNTHETIK | N слотів, будь-який attachment у будь-який (макс 4) | мінімальний | проста, але втрачає «це muzzle» |
| **Fixed typed slots (whole-weapon)** | Division 2, Destiny (колонки) | фікс. набір типізованих слотів (Optic/Muzzle/Mag/Grip), 1 категорія на слот | низький | **найчистіша** |
| **Per-weapon variable typed** | ZERO Sievert, Duckov | зброя сама визначає, які слоти має; class-tagged compat; Duckov: unlock слотів = прогресія | середній | добра |
| **Recursive slot tree** | Tarkov, ABI | structural-частини відкривають child-слоти (handguard → rails → optics) | **дуже високий** | складна, потребує тулів |

**Орієнтири по кількості:** SYNTHETIK 4 (hard cap, хвалять за вибір+читабельність), Division 4, Destiny ~4 колонки, Duckov ~7, ZS ~8+aux, Tarkov десятки.

**Що це нам каже:** під наші constraints (top-down, no UI artist, restrained-tactical) — **мало слотів + типізовані** = найкраща читабельність. Recursive tree (Tarkov) — overkill і authoring-катастрофа. Generic (SYNTHETIK) — просто, але дозволяє stack 3× muzzle (дивно для extraction). Solid spot = **fixed/typed, але прив'язані до cores** (бо ми композитні).

---

## 3. Три осі рішення (де реально варіанти)

Слот-дизайн розкладається на **3 незалежні осі**. Можна мікс-енд-матч.

### Вісь 1 — Звідки беруться слоти?

- **1A. Whole-weapon (Division-style).** Кожна зібрана зброя має однаковий фікс. набір (напр. Optic/Muzzle/Magazine/Grip). _+ Найпростіше, найчитабельніше. − Ігнорує design.md §6.4, втрачає composition-flavor (Laser і Ballistic мають однакові слоти)._
- **1B. Core-granted (design.md §6.4).** Payload дає свої слоти, Delivery — свої; набір зброї = union. _+ Матч design + варіативність (різні білди мають різні слоти) + лягає на асиметрію §1. − Складніше: UI має адаптуватись під змінний набір._
- **1C. Hybrid.** Універсальний whole-weapon слот (Optic) + core-granted решта. _+ Баланс простоти й flavor._

→ **Лін: 1B або 1C.** Honors §6.4, дає варіативність, лягає на нашу композицію.

### Вісь 2 — Типізація слотів

- **2A. Typed (1 категорія на слот).** Muzzle-слот приймає лише muzzle. _+ Читабельно, нема stack-абузу (3× mag). − Variable-count типізованих слотів = комбінаторика «який слот дала rarity»._
- **2B. Generic-per-domain.** Слот має domain (Payload/Delivery); будь-який domain-attachment підходить; compat = `attachment.domain == slot.domain` (+ опц. archetype-tag). _+ Просто, нема комбінаторики, матч §6.4. − Можна stack однотипні (мітигація: «unique-equip per category»)._

→ **Лін: 2A (typed)** для читабельності й анти-stack — критичніше для extraction, ніж для roguelite. Але 2B простіший у реалізації змінної кількості.

### Вісь 3 — Що визначає кількість слотів?

- **3A. Fixed.** Стала к-ть незалежно від rarity. _Просто, але rarity двох cores не впливає на customization._
- **3B. Rarity-scaled.** Core дає `1 + f(rarity)` слотів. _+ Дає rarity **lateral**-вираження (більше тюнінгу), а не лише вертикальне (кращі числа). + Прямо відповідає на питання «2 cores різної рарності» — кожен дає свою кількість слотів._

→ **Лін: 3B** — див. §4, це найцікавіший лівер.

---

## 4. Rarity × Slots (ключове питання користувача)

Це найбагатша частина. У нас **два cores, потенційно різної рарності** — як це перетворити на attachment-canvas?

**Варіант 3B — rarity кожного core визначає к-ть його слотів:**

```
slot_count(core) = base + rarityBonus(core.Rarity)
напр.: Common 1 · Uncommon 1 · Rare 2 · Epic 2 · Legendary 3
weapon_total = payload_slots + delivery_slots
```

Тоді **комбінація рарностей двох cores = твій build-canvas:**
- Legendary **Delivery** + Common Payload → 3 mechanism-слоти + 1 payload-слот → важко-моддабельний *механізм* (recoil/mag/ergo білд).
- Legendary **Payload** + Common Delivery → інший flavor: моддабельна *бойова частина* (optic/lens).
- Це робить **вибір рарності латеральним**, а не лише «більші числа»: Legendary-Delivery vs Legendary-Payload — різні build-простори.

**Чому це не ламає філософію** (design.md «rarity = power first, variation second; не ламає архетип»):
- Більше слотів ≠ зміна архетипу (Payload×Delivery identity лишається).
- Слоти заповнюються **sidegrade**-модами ([`analysis.md`](./analysis.md)) — тобто це «більше ситуативного тюнінгу», не «більше raw power». Лишається lateral.
- ⚠️ Tension flag: high-rarity = більше customization-room = м'який вертикальний нахил. Прийнятно, бо приріст — у гнучкості, не в потужності. Тримати малі числа (cap ~3 на core).

**Альтернатива 4-bis — rarity масштабує не к-ть, а *якість* слотів** (Common-слот приймає лише Common-моди; Legendary-слот — будь-які). Складніше, менш читабельно. Не раджу для старту.

---

## 5. Рекомендація (конкретний стартовий proposal)

**Mix: 1B/1C (core-granted) + 2A (typed) + 3B (rarity-scaled), мало слотів.**

```
            ┌──────────────── ASSEMBLED WEAPON ────────────────┐
 PAYLOAD ───┤  [Optic]            [Lens*]                       │  *payload-specific
 (rarity)   │   ▲ accuracy/sight   ▲ Laser: charge/damage       │   (Ballistic: barrel-treatment?)
            │                                                    │
 DELIVERY ──┤  [Muzzle]      [Magazine]       [Grip/Stock]      │
 (rarity)   │   ▲ recoil/noise ▲ mag/reload/ergo  ▲ recoil/ergo │
            └────────────────────────────────────────────────────┘
   slot count per core unlocked by rarity: Common 1 → Rare 2 → Legendary 3
```

- **Payload-слоти (typed):** `Optic` (accuracy/sight-range/ergo, whole-weapon-ish але приписуємо Payload бо «прицілюєш payload») + `Lens`/payload-specific (Laser: charge/damage; Ballistic: TBD — можливо просто 1 слот Optic на Common Payload).
- **Delivery-слоти (typed):** `Muzzle` (recoil/noise/damage-trade) → `Magazine` (mag/reload/ergo) → `Grip/Stock` (recoil/ergo). Порядок unlock'у за rarity.
- **Стартовий канвас Common/Common ≈ 2 слоти** (1 Delivery: Muzzle + 1 Payload: Optic) — не перевантажує. **Cap Legendary/Legendary ≈ 5-6.** Дрібно, читабельно, масштабується.

**Чому саме так:**
- Лягає на **асиметрію §1** (Delivery несе механізм-моди, Payload — мало).
- Honors **design.md §6.4** (слоти на cores).
- **Rarity = lateral build-canvas** (відповідь на питання користувача).
- Малий total → читабельний на top-down + у наявному drag-drop slots-UI.
- Typed → нема stack-абузу, кожен слот має ясну ідентичність у preview.

**Реюз інфри:** наявний drag-drop palette/slots UI (Builder) + modules-as-items (Tier 6). Слоти стають частиною weapon preview/inspect; attachment instance живе у `WeaponConfiguration` поряд із Payload/Delivery instances.

---

## 6. Open questions → наступна ітерація

- ~~**Q18. Вісь 1: core-granted vs whole-weapon?**~~ ✅ **RESOLVED 2026-06-10** — core-granted (1B). Payload: Buttstock/Optic/Magazine; Delivery: Muzzle/Grip.
- **Q19. Вісь 2: typed (2A) чи generic-per-domain (2B)?** Layout показує typed-категорії; чи слот строго одна категорія, чи domain-generic — ⏳ open (нахил: typed).
- ~~**Q20. Rarity-curve слотів.**~~ ✅ accepted-for-testing — rarity-scaled count, крива Common→Legendary тюниться в плейтесті. **Sub-open:** порядок unlock'у слотів per core (Payload: Optic→Magazine→Buttstock? Delivery: Muzzle→Grip?).
- ~~**Q21. Ballistic Payload-слот / payload-specific.**~~ ✅ **RESOLVED 2026-06-10** — payload-specific = **unique mods** (обмежена сумісність на наявних слот-категоріях), не окремий тип слота. Payload дає Buttstock/Optic/Magazine універсально.
- **Q22. Чи всі deliveries/payloads мають усі свої слоти?** (Single-action — менший mag; чи слот-набір однаковий, лише к-ть unlocked різниться за rarity?) ⏳ open.
- ~~**Q23. Attachment compatibility поза domain.**~~ ✅ напрям обрано — поле `CompatibleArchetype?` (null = universal; "Laser"/"Scatter"/… = unique). Деталі реалізації — Tier 0 даних.
- **Q24. Persistence у `WeaponConfiguration`.** Як зберігаємо встановлені attachment-instances (масив per slot? `{ SlotId, ModId, Rarity? }`) — структурне рішення для Tier 0 даних.
- **Q25. Slot-баланс Payload 3 / Delivery 2** — лишити чи свопнути на 2/3 (симетрія з §1 «Delivery робить більше»). ⏳ на тест.

---

## 7. Навігація
- [`analysis.md`](./analysis.md) — концептуальний fit, Q2/Q3 origin
- [`stats.md`](./stats.md) — стат-словник (що тюнять слоти)
- [`competitor-research.md`](./competitor-research.md) — повні slot-таксономії конкурентів
- [`../design.md`](../design.md) §6.4 Typed Attachments, §6.5 Slot structure, §6.6 Rarity
- [`../architecture.md`](../architecture.md) §6 (rarity data model), §D1 (core stat-ownership)
