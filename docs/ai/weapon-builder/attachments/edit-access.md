# Attachments — Edit-Access Pattern (P2.2 decision)

> Де і як гравець редагує attachments. Рішення: **edit-existing, будь-де**. Питання — home (де живе UI) + interaction (як ставиш мод). Аналіз конкурентів → варіанти → рекомендація.
> **Статус:** 🔬 decision doc для P2.2. **Дата:** 2026-06-12.

---

## 1. Вимога

Редагувати attachments **наявної** зброї, **будь-де** (не лише Workbench — як Duckov/ARC). Build-new (зібрати з cores) лишається на Workbench-Builder; це окремий потік.

---

## 2. Competitor access-patterns (research 2026-06-12)

| Гра | Entry | Modal/inline | Interaction | Anywhere | Conf. |
|---|---|---|---|---|---|
| **Duckov** | клік зброї в інвентарі | **inline** панель | **drag** мода з інвентаря в слот (compat = біла рамка) | base (не bench) | high |
| **ZERO Sievert** | Workbench «Mod Weapon» tab | modal | список зброї → слоти | **bench-gated** | high |
| **Tarkov** | double-click у stash | **full modal** gunsmith | клік слота → вікно сумісних; real-time stat update | stash, не in-raid | high |
| **Arena Breakout** | Gunsmith tab / **right-click→modify** | modal | клік слота → деталь; auto-buy missing | base | high |
| **Division 2** | інвентар → обрати зброю → **1 кнопка** (F/X/□) | **modal** weapon-detail | **focus-slot (thumbstick) → filtered list → green/red дельти** | **anywhere** | high |
| **Destiny 2** | item detail (Y/△) → socket-ряд | modal detail | focus-socket → tray опцій; stat-бари оновлюються на hover | **anywhere** | high |
| **ARC Raiders** | з інвентаря (клік мода → обрати зброю / drag) | inline-ish | compat-filtered; right-click видалити | **anywhere incl raid** | med |
| **SYNTHETIK** (top-down) | consumable kit → **draft 1-з-N** | popup | pick-1 (НЕ редактор) | mid-run | med |

**Джерела:** Duckov [Steam](https://steamcommunity.com/app/3167020/discussions/0/592900729837000649/)/[Kotaku](https://kotaku.com/escape-from-duckov-guide-tips-weapon-mods-storm-2000639589); ZS [Steam](https://steamcommunity.com/app/1782120/discussions/0/3321988498658693281/); Tarkov [sellersandfriends](https://www.sellersandfriends.com/blog/eft-modding-guide-learn-everything-about-weapon-mods); ABI [Charlie INTEL](https://www.charlieintel.com/games/how-to-modify-weapons-in-arena-breakout-infinite-323743/); Division 2 [Twinfinite](https://twinfinite.net/guides/division-2-weapon-mods-get-equip-remove-what-they-do-how/); Destiny 2 [alphr](https://www.alphr.com/destiny-2-equip-weapon-mods/); ARC [boosting-ground](https://boosting-ground.com/arc-raiders/guides/combat-weapons-loadouts/weapon-mods-attachments); SYNTHETIK [wiki](https://synthetikuniverse.wiki.gg/wiki/SYNTHETIK_1:Attachments).

### Розмежування патернів
- **Bench-gated modal** (ZS) — суперечить «anywhere». ✗
- **Inline inventory drag** (Duckov, ARC) — anywhere ✓, але **drag = миша-only** (погано для контролера/майбутнього).
- **Inventory-invoked modal, focus-slot→list** (Division 2, Destiny 2) — anywhere ✓, **controller-friendly** (без drag), авто-фільтрує сумісне, проста live-delta. **Найсильніший для нашого кейсу.**
- **Consumable draft** (SYNTHETIK) — мінімалістично, але НЕ reversible-edit. ✗ для нашої вимоги.

---

## 3. Варіанти для нас

**Option A — розширити build-new Builder (drag-drop).** Реюз palette/slots/ghost-drag Builder'а. − Drag = миша-only; Builder = build-new flavor (Workbench); змішує build+edit в одній модалці. − Research каже drag — слабший патерн.

**Option B — окремий inventory-invoked mod-editor (Division/Destiny-style).** Обрав зброю в інвентарі → modal weapon-detail (anywhere) → **slot-колонка → focus-slot → filtered list сумісних модів з backpack → live green/red дельта → confirm**; remove повертає мод. − Більше нового UI. + Правильний патерн (research-backed), controller-friendly, без drag-machinery, чисто розділяє build (Workbench) і edit (anywhere).

### Що реюзиться в Option B
- **Entry:** UI Toolkit `InventoryWindow` (обрати зброю → дія «Modify»).
- **Stat readout + live-delta:** `WeaponStatDisplay` (P1) — diff base vs with-mod.
- **Дані:** `AttachmentDefinition` + `WeaponStatComposer.ApplyAttachments` (P2.1).
- **Source модів:** backpack (як build-cost у Tier 6).
- Slot→list UI **простіший** за drag-drop (нема ghost/geometry-overlap).

---

## 4. Рекомендація: **Option B** (inventory-invoked, focus-slot→list, live-delta)

Research чітко сходиться: для «edit-existing + anywhere» Division/Destiny-патерн виграє (controller-friendly, auto-filter, проста delta, відв'язано від build-new). Drag (Duckov/ARC) — миша-only, гірше для майбутнього контролера. Top-down не потребує 3D-моделі зброї (Destiny rotatable не треба) — **slot-колонка + mod-list + stat-панель** достатньо, тривіально на UTK/SDF, нуль арту.

**Цільовий shape:**
```
Інвентар → обрати зброю → [Modify] (будь-де)
  ┌ weapon-detail modal ───────────────────────────┐
  │  Laser Rifle   ◆◆◇ · ◆◆◆◇                        │
  │  ┌ slots ──────┐   ┌ mods (для обраного слота) ┐ │
  │  │ Optic   ●   │   │ Red Dot      Δ...          │ │
  │  │ Magazine ○ ◄┼─  │ Extended Mag Δ Mag+ Reload-│ │  ← focus slot →
  │  │ Muzzle  ●   │   │ Quick Mag    Δ ...          │ │     filtered list
  │  └─────────────┘   └────────────────────────────┘ │
  │  ── stats (WeaponStatDisplay + green/red дельта) ─ │
  └───────────────────────────────────────────────────┘
```

---

## 5. Open sub-questions (P2.2)
- **Entry UX:** як саме «Modify» викликається з інвентаря (context-menu / кнопка на selected weapon / hotkey)? Залежить від поточного `InventoryWindow` (треба глянути select/context механіку).
- **Modal vs docked:** окрема модалка vs панель збоку від інвентаря. Лін: модалка (фокус на слотах).
- **Slot count:** фіксований набір усіх 5 категорій (MVP — вже вирішено).
- **Drag залишити опційно?** focus-slot→list = основний (controller); drag — пізніше nice-to-have.

---

## 6. Навігація
- [`ux.md`](./ux.md) — Builder layout + inventory icon + readout
- [`slots.md`](./slots.md) — слот-таксономія
- [`competitor-research.md`](./competitor-research.md) — install-stat-tradeoffs (іт.1)
- [`README.md`](./README.md) — epic roadmap
