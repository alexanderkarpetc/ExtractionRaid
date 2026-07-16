# Weapon Attachments — Epic

Шар **модулів (attachments)** на зброю, зібрану у Weapon Builder. Покращення, що **тюнять характеристики й feel**, але **не міняють логіку** зброї (логіка = Payload + Delivery). Це активація шару **«Typed Attachments»** з [`../design.md`](../design.md) §6.4 (раніше deferred).

> **Статус (2026-07-16): ✅ ЕПІК ЗАВЕРШЕНО.** P1–P4 shipped: візуалізація наявного, перші моди, **loot-gating** (моди-як-предмети, recoverable), inventory **drag/highlight**, **rarity-scaled слоти**, **unique-моди**, **parabolic rarity-баланс**, **weapon-compare tooltip** (+ammo/mods рядки), **mod loot-drops**, **Sniper Scope** (P4). ~640 EditMode green.
>
> **Sniper Scope (P4)** — модель ZS/Duckov: вісь `SightRange` (метри) → `ScopeReveal = AdsBlend × dist-blend` → **екранний SDF-круг** у `FogOfWarComposite.shader` (глючений до `WeaponAimPoint`) + **ergo-driven damped-spring аім** у `AimingSystem` (куля/дот/круг лагають як одне; low ergo → овершут+bounce) + балістика (give: Velocity/Headshot/Spread; take: RoF/Recoil/Ergo). Тюнінг: `Raid → Dev Cheats → 🎯 Scope` (`DevCheatsScopeSection` + friendly `[CustomEditor]`). Механіка огляду — `docs/ai/fog-of-war.md`.
>
> **Suppressor (Noise) прибрано з плану** (2026-07-10 — забагато невідполірованого; unique-моди лишаються на proxy-осях). Тимчасовий resume-скаффолд `impl-status.md` розпущено — постійний запис тут. **Далі:** playtest/balance + polish наявних фіч.
>
> **Термінологія:** цей шар = **Attachments / mods**. «Cores / modules» = Payload/Delivery (щоб не плутати — слово «module» зайняте під cores). Q9 — soft, лишаємо «attachments».

---

## Quick resume для нової сесії

Читати у порядку:
1. **Цей файл** — статус, зафіксовані рішення, roadmap
2. [`analysis.md`](./analysis.md) — концепт-fit (чому sidegrades), competitor-підходи, top-down readability
3. [`stats.md`](./stats.md) — на що моди впливають (стат-словник + спецправила UI)
4. [`slots.md`](./slots.md) — слот-таксономія (layout, rarity×slots, unique mods)
5. [`catalog.md`](./catalog.md) — стартовий каталог модів (give/take)
6. [`ux.md`](./ux.md) — Builder layout + inventory-icon (RT-render)

---

## Зафіксовані дизайн-рішення

**Концепт ([`analysis.md`](./analysis.md)):**
- Моди = **sidegrades (opposing-axis tradeoffs)**, не pure upgrades. Додають **третю lateral-вісь** (Payload=*що*, Delivery=*як*, Attachment=*під яку ситуацію*).
- Анти-creep: **opposing-axis + slot scarcity + situational value**. **Без hidden budget** (Tarkov «hidden formula» = anti-pattern).
- Top-down: сигнал через crosshair/projectile/HUD/audio, **арту модів не робимо**.

**Параметри ([`stats.md`](./stats.md)) — 8 показаних:** Damage · Headshot Mult · Rate of Fire · Magazine Size · Recoil (V+H+recovery) · Accuracy/Spread · **Ergonomics** (агрегат, один множник) · **Noise**.
- **Reload Time** — базове приховане, показ лише дельтою («−20%»).
- **Sight Range / FOV** — механіка є, **прихована від UI** (відчувається візуально).
- **Bleed / Penetration / Armor** — **ammo-канал**, не на зброї.
- Нова інфра: Ergonomics-агрегат (+2 per-weapon поля: move-mult, ADS-speed), **Noise** (perception), **Sight/FOV** (fog-of-war).

**Слоти ([`slots.md`](./slots.md)):**
```
PAYLOAD  → [Buttstock] [Optic] [Magazine]   к-ть = f(Payload rarity)
DELIVERY → [Muzzle]    [Grip]               к-ть = f(Delivery rarity)
```
- **Core-granted**, **декаплені від stat-domain** (слот = «місце повісити мод» + лічильник від rarity core).
- **Rarity → к-ть слотів**; комбінація рарностей двох cores = build-canvas.
- **Unique mods** = обмежена сумісність (`CompatibleArchetype?`), без власного типу слота.

**Моди ([`catalog.md`](./catalog.md)):**
- **Flat, без rarity** (як Exotic; rarity лише на cores).
- Attachment instance = `{ SlotCategory, ModId }` у `WeaponConfiguration`.
- Стартовий каталог: 11 universal + 3 unique.

**UX ([`ux.md`](./ux.md)):**
- Builder: 2 core-панелі + слоти inline під cores + **live delta-preview** (green↑/red↓).
- Inventory icon = **RT-render зібраних cores** (live, кеш per-archetype) + **dual-rarity frame** + **mod-pips** + tooltip.
- Поверхні: Builder (edit) + inventory tooltip (read). Бойовий HUD не чіпаємо.

---

## Roadmap — 5 фаз

> ⚠️ **Історичний план (для контексту).** Фактично shipped: **P1–P4 ✅** (з поправкою — Sniper Scope = screen-space circle, НЕ конус↓; Suppressor/Noise **прибрано**). **P5 частково**: loot-drops модів ✅, RT-render іконки ✗ (backpack composite icons deferred sine die). Фінальний стан — у блоці «Статус» вгорі.

> Принцип: **visualize-first** — встановити візуальну мову на наявних даних, потім нарощувати механіку (де-ризикує проблему читабельності tradeoff-ів, що завалила Division 1).
>
> **Existing-field vs нова механіка:** більшість модів (включно з unique) працюють на **наявних** полях `WeaponStats`. Лише **2 моди** потребують нових систем: Suppressor (Noise), Sniper Scope (Sight/FOV) → ізольовані у P4.

| Фаза | Скоуп | Нова механіка | Exit-критерій |
|---|---|---|---|
| ✅ **P1 — Візуалізація наявного** (shipped 2026-06-10) | `WeaponStatDisplay` (pure model) → tooltip стат-бари (Damage/RoF/Stability/Accuracy/Ergonomics + value; Headshot/Magazine/Charge value-only) + 2-core dual-rarity subtitle + inventory **dual-rarity corner frame** (`RarityVisuals`). Cheat + fresh-player loadout spawn random rarity (Common-fallback for unauthored tiers). | — | ✅ Гравець бачить «зброя = 2 cores + ці стати» барами + кольорами. 564 EditMode green. |
| **P2 — Перші attachments** | **Foundation:** `AttachmentDefinition` SO + instance у `WeaponConfiguration` + mod-канал у `WeaponStatComposer` + re-assembly на install + Raid State Debugger. **Слоти** (мінімальний фікс. набір) у Builder inline під cores. **Base-моди (лише existing-field):** Extended/Quick Mag, Heavy/Skeleton Stock, Vertical/Angled Grip, Power Comp. **Live-delta** preview. Inventory: **mod-pips**. Grant через DevCheats. | — | Перший playable sidegrade-loop: встав мод → стати міняються з видимим give/take. |
| **P3 — Rarity-слоти + unique** | Slot count = **f(core rarity)** + unlock-порядок + locked-slot UI. **Unique-моди** (`CompatibleArchetype`): Laser Focusing, Scatter Choke, Auto Heat-Sink (теж existing-field). Дозбір universal-каталогу. | — | Повний lateral build-canvas + identity-chase, усе на наявних полях. |
| **P4 — Нові механіки** | **Noise→aggro:** боти реагують на `WeaponFired` у `NoiseRadius` → стат Noise → **Suppressor**. **Sight/FOV→fog-of-war:** per-weapon view-range/cone → **Sniper Scope** (модель ZS vs Duckov — Q14). Кожна — свій milestone. | ✅ 2 системи | Suppressor (стелс vs DPS) + Sniper Scope працюють як справжні sidegrade-и. |
| **P5 — Повна бібліотека + economy** | Повний каталог. Attachments у **loot-pool** (як Tier 6 для cores). **RT-render** іконки (камера-rig). Balance/feel + UX-поліш (footprint, generalist-cost messaging). | — | Епік закрито: контент + economy + polish. |

**Розвилка:** P4 можна підняти раніше, якщо стелс-вісь (Suppressor) важлива для першого переконливого playtest (ядро ZS/Duckov feel) — але вона важча (чіпає bot-AI). Інакше existing-field моди (P2-P3) дають повноцінний sidegrade-loop і без неї.

**Кожна фаза** лендить EditMode-тести (compose-математика, slot-rules, compat) — проектна норма.

---

## Архітектурні гачки (що вже готове)

- **`WeaponStats`** має поля для всіх 8 параметрів (крім Ergonomics-агрегату — compute-шар P1, і Noise/Sight — P4).
- **`WeaponStatComposer.Compose`** уже приймає Exotic-канал (no-op) → mod-канал додається симетрично (P2).
- **`WeaponConfiguration`** несе Payload/Delivery instances → attachment-instances додаються поряд (P2).
- **Modules-as-items + drag-drop palette/slots** (Tier 6 + UX Pass 1) → реюз для слотів і loot (P2/P5).
- **`WeaponTooltipBuilder`** існує → прокачка в P1.
- **Heat** (`WeaponHeatSystem`) + **ChargeTime** (Laser) → unique-моди Auto Heat-Sink / Laser Focusing (P3).
- **Fog-of-war** існує → Sight/FOV-мод (P4).

Усе data-driven через SO (як cores), нуль нових singleton-ів, тюнінг через DevCheats.

---

## Залишкові open questions (дрібні, не блокери)

Зведено з 5 доків — вирішуються по ходу фаз:
- **Delta-формат** (Q34/Q17): % всюди, чи абсолют, чи бар-shift зі стрілкою.
- **Модель прицілу** (Q14): ZS (конус↓/range↑) vs Duckov (axis-aware ADS-range) — рішення в P4.
- **Generalist-cost** (Q27/Q36): мод без downside — дати дрібний cost чи лишити near-pure (cap тримає магнітуду)?
- **Slot-баланс** (Q25): Payload 3 / Delivery 2 vs своп на 2/3 — на тест.
- **Dual-rarity подача** (Q37): 2 corner brackets vs split-border vs трикутники.
- **Footprint** (Q39): weapon = 1 cell vs larger у dense grid.
- **Persistence** (Q24): структура attachment-масиву у `WeaponConfiguration` — рішення в P2 foundation.

---

## Навігація
- [`analysis.md`](./analysis.md) · [`stats.md`](./stats.md) · [`slots.md`](./slots.md) · [`catalog.md`](./catalog.md) · [`ux.md`](./ux.md)
- [`competitor-research.md`](./competitor-research.md) — повні per-game findings із джерелами
- [`../README.md`](../README.md) — Weapon Builder (parent, paused) · [`../design.md`](../design.md) §6.4 Typed Attachments · [`../architecture.md`](../architecture.md) §D1
- [`../../competitor-reference-db.md`](../../competitor-reference-db.md) — індекс рефів за атрибутом
