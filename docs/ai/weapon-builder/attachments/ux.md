# Attachments — UX Pass (Iteration 5)

> Три цілі: **(A)** вивести узгоджені параметри зброї в UI; **(B)** показати, що зброя = два cores; **(C)** придумати, де і як малювати слоти модів.
> **Статус:** 🔬 UX-дизайн (design-level; ground проти реального UXML на impl-етапі). Продовження [`catalog.md`](./catalog.md).
> **Дата:** 2026-06-10.
> **Constraints:** no UI artist → procedural/SDF/TMP/UI-Toolkit only; restrained-tactical (Destiny 2 / EFD, не Borderlands-juice); top-down.

---

## 1. Що вже є (ground)

Поточний **Weapon Builder** (Tier 1 + UX Pass 1 + Tier 6):
- **Side-by-side**: uGUI inventory canvas зліва + Builder modal справа (Workbench → E).
- **Builder**: palette (module cards) + **2 drop-слоти (Payload, Delivery)** + **live preview** (archetype label + flavor + charge hint + stat-групи Combat/Cadence/Pattern) + read-only backpack.
- **Drag&drop** infra: `ModuleCardElement` (drag) → `ModuleSlotElement` (drop, type-filter, wrong-type→red).
- **Tooltip system**: hover будь-який item/card → `WeaponTooltipBuilder` / `ModuleTooltipBuilder` overlay.
- **Archetype label** в інвентарі (`WeaponDisplayName.For`).

**Що змінюється:** preview-stat-групи треба узгодити з фінальним списком параметрів; додати **слоти модів**, **показ 2-core composition + rarity**, **live delta-preview** при наведенні мода.

---

## 2. Релевантні competitor UX-патерни (з research)

- **Division 2 — live delta-preview** (green↑/red↓ на affected стати при hover/install). **Must-have** — без нього sidegrade-tradeoff нечитабельний. Головний урок Division 1→2.
- **Duckov — drag-drop + підсвітка сумісних слотів** (bold frame). Легкий, action-friendly. У нас infra вже є.
- **Top-down (SYNTHETIK/Duckov) — "show consequence, not part"**: на top-down фізичний attachment невидимий, **модель зброї не міняється** (Duckov шипить без цього). Отже schematic-чіпи + числа = і є представлення. **Не потрібен gun-image з callout'ами** (Tarkov/CoD gunsmith) — це проти no-artist constraint.
- **SYNTHETIK — повний breakdown у меню, не в бойовому HUD**. Параметри+слоти живуть у Builder/inspect; бойовий HUD лишається мінімальним.

---

## 3. (A) Readout параметрів

Виводимо **узгоджений список** ([`stats.md`](./stats.md)) у preview, згруповано:

**Row layout (P1.2, implemented):** кожен рядок = верхня лінія **Label (зліва) + Value (справа)**, під нею **progress bar на всю ширину** (для bar-рядків). Bar-рядки несуть і значення, і бар. **Value-only рядки тонуть униз.**

| Подача | Параметри | Нотатки |
|---|---|---|
| **Bar (value + full-width bar, вище=краще)** | Damage, Rate of Fire, **Stability**, Accuracy, Ergonomics | feel-стати = 0..100 score; Damage/RoF = реальне значення, бар по reference-range |
| **Value-only (внизу)** | Headshot Mult, Magazine, Charge (laser) | дискретні/контекстні числа без бара |
| **Stealth (пізніше)** | Noise | bar-рядок (➕ нова механіка, P4) |

> **Recoil → "Stability".** Feel-стат показуємо як **Stability** (вище=краще), не "Recoil" — бо на барі тепер є значення, і "Recoil 70" читалося б двозначно, а "Stability 70 + повний бар" однозначно (як Destiny / реф-скрін). Internally — та сама recoil-goodness.

**Спеціальні правила (з `stats.md`):**
- **Reload Time** — **без абсолютного значення**; з'являється лише як дельта-рядок, коли мод його чіпає («Reload Time −20%»).
- **Sight Range / FOV** — **не показуємо взагалі** (ефект відчувається візуально через fog-of-war).
- **Bleed / Penetration / Armor** — **не на зброї** (ammo-канал), у weapon-readout відсутні.

**Live delta (Division-style):** наведення мода в palette або на слот → affected параметри підсвічуються **green↑ / red↓** з дельтою. Це робить give/take миттєво читабельним перед install. `WeaponStatComposer` рахує "preview stats with mod" vs "current" → diff.

---

## 4. (B) Показ 2-core composition

Зробити явним, що зброя = **Payload + Delivery**, кожен зі своєю rarity:
- **Два core-панелі** (поряд): кожна показує ім'я core ("Laser Charge" / "Auto") + **rarity gems** (◆◆◇ = 3/5) + rarity-tint рамки (rarity tint deferred з UX Pass 1 — тепер активуємо).
- Archetype label ("Laser Rifle") — над панелями як summary.
- **Слоти модів живуть ПІД своїм core** (§5) → це візуально доводить «слоти належать cores» (design.md §6.4) + «rarity core → к-ть слотів».

---

## 5. (C) Де і як малювати слоти модів — пропозиція

**Inline під кожним core** (не окремою панеллю) — найкоherentніше: візуалізує core-ownership + rarity→count одночасно.

```
┌─ WEAPON BUILDER ─────────────────────────────────────────────┐
│  Laser Rifle                          ⚡ charge · 1.0s        │
│                                                               │
│  ┌─ PAYLOAD ────────────┐   ┌─ DELIVERY ──────────────────┐  │
│  │ Laser Charge  ◆◆◇     │   │ Auto            ◆◆◆◆◇        │  │
│  │ ─ mod slots ─         │   │ ─ mod slots ─               │  │
│  │ [Optic ●][Mag  ○]     │   │ [Muzzle ●][Grip ○][· lock]  │  │
│  │ [Stock · lock]        │   │                             │  │
│  └──────────────────────┘   └─────────────────────────────┘  │
│                                                               │
│  ── Stats ──────────────────────────────────────────────     │
│  Damage      24            Recoil    ▮▮▯▯▯  (−10% ↓ green)    │
│  RoF        8.5/s          Accuracy  ▮▮▮▮▯                    │
│  Magazine    30            Ergonomics▮▮▮▯▯  (−8%  ↑ red)      │
│  Noise      ▮▮▮▮▮ → ▮▮▯▯▯ (−60% ↓ green)                      │
│  ▸ Reload Time −20%   (з'являється лише як дельта)            │
│                                                               │
│  [ palette: mod cards filtered by selected slot ]    [Apply]  │
└───────────────────────────────────────────────────────────────┘
```

**Стани слота:**
- **Empty** — категорія-лейбл + faint icon, drop-target (○).
- **Filled** — mini mod-card (●); клік = зняти / replace.
- **Locked** — greyed + 🔒 + hint «rarity ↑» (слот існує, але rarity core ще не розблокувала).
- **Incompatible on drag** — червона рамка (як поточний wrong-type reject) + причина (domain / `CompatibleArchetype`).

**Поведінка:**
- Drag mod з palette → drop на слот відповідної категорії; сумісні слоти підсвічуються (Duckov-style).
- Click-fallback (як зараз): клік мода → клік слота.
- Hover mod/слот → live delta на Stats (§3) + tooltip композиції.
- Unique-моди: показуються в palette лише коли поточний build їх приймає (`CompatibleArchetype` matches), інакше grayed (реюз Tier 6 `IsModuleAvailable` патерн).

**Реюз infra:** `ModuleSlotElement` (зменшений варіант для mod-слотів) + `ModuleCardElement` (mini) + наявний drag/drop overlap-тест. Attachment instance `{ SlotCategory, ModId }` ([`catalog.md`](./catalog.md) Q28) живе у `WeaponConfiguration`.

---

## 6. Дві поверхні

| Поверхня | Роль | Що показує |
|---|---|---|
| **Builder (Workbench)** | edit | повний інтерактив: 2 cores + слоти + install/remove + live delta + palette |
| **Inventory tooltip** | read-only | hover зброї → archetype + 2 cores + rarity + список встановлених модів + фінальні стати (реюз `WeaponTooltipBuilder`) |

Бойовий HUD — **не чіпаємо** (мінімальний; параметри/моди не виводяться в бою — SYNTHETIK-принцип).

---

## 7. Inventory slot — weapon у малому слоті

**Ground:** backpack-комірка зараз **102×102px** (не 30 — місця достатньо), text-label-рендер (`WeaponDisplayName.For`), procedural USS. Badge/bar-патерн усталений (durability-bar, quick-key, quest-dot — absolutely-positioned children у `InventorySlotElement` + `_inv-slot.uss`). Іконок ще немає.

**Проблема:** weapon = 2 cores (можливо різні rarity) + моди. Як це донести в малій комірці, особливо у щільнішій сітці.

### Ключовий reframe: іконка зброї = РЕНДЕР зібраних cores, не унікальний спрайт

Зброя в грі вже = **два зліплені core-префаби з власним візуалом** (Tier 8). Отже іконку не малюємо — **рендеримо ту саму комбінацію**. Icon = буквально рендер зібраної зброї → композиція видно безпосередньо, нуль 2D-арту. (Це сильніше за абстрактні glyph-и: префаби вже існують.)

**Підхід (обрано 2026-06-10): live render у RenderTexture, лінивий, кеш per-archetype.**
- Рендеримо кожну `(payload, delivery)` комбінацію **один раз** у кешований RT при першій потребі → переюз в усіх слотах. ~6 one-time дрібних рендерів, не per-frame.
- **Переваги над пре-бейком:** нуль ручного кроку; авто-покриває новий контент (Tier 3); **нема binary git-churn** від PNG; завжди збігається з реальними префабами.
- **Пре-бейк (editor-утиліта)** — валідний fallback (прецедент: `Create Module Prefabs`). Обидва ділять той самий camera-rig (камера + framing + інстанс) — різниця лише *коли* (editor/runtime) і *де* (PNG-asset / кеш-RT).
- UI Toolkit показує RT через `Background.FromRenderTexture(rt)`.
- **Ключ рендеру = лише `(payload, delivery)`** (6 стабільних варіантів): моди не мають мешів (no mod art), rarity не міняє меш → і те, й те йде **оверлеями поверх** RT, не в рендері.
- Framing: side-profile, щоб видно обидва cores (payload-barrel + delivery-body).

### At-slot cues (шарування комірки)

1. **RT-фон** — рендер 3D зброї (side-profile). ⇒ архетип/композиція візуально.
2. **Dual-rarity frame** (overlay) — два кутові L-bracket'и: top-left = rarity color Payload, bottom-right = rarity color Delivery. ⇒ «2 cores + їх rarity». Signature-cue. ✅
3. **Mod pips** (overlay) — ряд крапок у куті (`●●○` = 2 моди / 3 слоти). ⇒ «наскільки зброя зібрана» (Diablo-sockets). ✅
4. **Tooltip (hover)** — повний breakdown: 2 cores + rarity + встановлені моди + фінал-стати (розширити `WeaponTooltipBuilder`).

### Інкрементальний шлях
**Dual-rarity frame + mod-pips → можна вже зараз** (нуль арту, лише USS-children над поточним text-label). **RT-render → окремий камера-rig крок** (інстанс префабів → preview-камера → RT-кеш). До нього text-label лишається як проксі.

### Density / footprint
- **1-cell (рекомендую):** frame + pips + tooltip. Graceful degrade: ~60px читабельно; 30px → лишається лише rarity-frame, решта в tooltip.
- **Larger footprint (Tarkov 1×2/2×2):** якщо підемо в щільний Tetris-grid. Відкладаємо до рішення про формат сітки.

**Competitor-якорі:** Tarkov = рендер реальної зброї з модами (арт+footprint — не ми). Diablo/PoE/Destiny = rarity-color border + tooltip (наш базовий патерн). Sockets/pips = Diablo gems.

---

## 8. Open questions

- **Q31. Core-панелі — горизонтально (2 колонки) чи вертикально (2 рядки)?** Залежить від ширини Builder-модалки в side-by-side режимі.
- **Q32. Rarity-вираження:** gems (◆◆◇) vs кольорова рамка vs обидва? (rarity-tint палітра — потрібна для cards теж.)
- **Q33. Слот-категорія: іконка, текст-лейбл чи обидва?** На top-down + no-artist — лейбл надійніший за іконку.
- **Q34. Delta-формат:** % (Reload вже задає прецедент) чи абсолют, чи бар-shift зі стрілкою? Консистентність по всіх параметрах.
- **Q35. Locked-слот — показувати завжди (greyed) чи ховати?** Greyed → гравець бачить, що дасть вища rarity (Duckov-патерн «бачити possibility space»).
- **Q36. Generalist-cost** (Q27) впливає на UX: мод без downside показує лише green-дельти — чи це читається як «pure upgrade» (проти меседжу sidegrade)?
- **Q37. Dual-rarity подача:** 2 corner brackets vs split-border vs 2 corner triangles?
- **Q38. Mod pips:** показувати open-слоти (○) теж, чи лише filled? (open → видно potential, але +шум)
- **Q39. Footprint:** weapon = 1 cell завжди, чи larger у dense grid?
- ~~**Q40. Glyph vs label / icon approach.**~~ ✅ **RESOLVED 2026-06-10** — **RT-render зібраних cores** (live, кеш per-archetype); rarity+mods = overlays. Пре-бейк = fallback. Text-label → проксі до rig'у, далі в tooltip.
- **Q41. RT-render rig деталі:** framing/кут (side-profile?), lighting, layer-ізоляція, RT-розмір/пул, інвалідація кешу. ⏳ impl-етап.

---

## 9. Навігація
- [`stats.md`](./stats.md) — параметри (що виводимо)
- [`slots.md`](./slots.md) — слот-таксономія (core-ownership, rarity→count)
- [`catalog.md`](./catalog.md) — моди (give/take для delta-preview)
- [`analysis.md`](./analysis.md) §5 — UX-патерни конкурентів (Division live-preview, Duckov drag)
- [`../../ui-styling.md`](../../ui-styling.md) — Tier A/B sizing, sort orders, color palette (Builder UI)
