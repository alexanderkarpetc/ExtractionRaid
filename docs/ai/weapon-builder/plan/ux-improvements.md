# Weapon Builder — UX Improvements Pass 1

> **Status:** 📋 Planned, awaiting implementation. Created 2026-04-24.
> **Goal:** Polish UX of existing Weapon Builder feature (Tier 0-2 done) — clarity & inventory integration.
> **Scope position:** Subset of future Tier 7 polish, лендує перед content expansion (Tier 3+).

---

## How to use this doc (for resuming agent)

Читати у такому порядку:
1. **Цей файл (повністю)** — все що треба для роботи
2. [`README.md`](../README.md) — quick state recap (що працює зараз)
3. [`plan/status.md`](./status.md) — pause summary + 14 key decisions reference
4. **Якщо потрібно глибше:** `architecture.md` (rationale), `design.md` (intent)

**Не потрібно:** перечитувати всю історію tier-by-tier декомпозиції.

---

## Context recap

Weapon Builder feature is paused after Tier 2 (2026-04-24). Foundation is solid:

- 6 working archetypes (Ballistic / Laser × Pistol / Rifle / Shotgun)
- Player flow: Workbench у Hideout → press E → modal → 2 dropdowns + preview → Build → item у backpack → equip → shoot
- ~90 зелених тестів, data-driven (всі stats з SO assets)
- Architecture: Presenter (pure C#) + UI Toolkit Window + WorkbenchView + DevCheats fallback

**Що цей UX pass НЕ робить:**
- ❌ Не додає нові payload / delivery / exotic modules (Tier 3+)
- ❌ Не реалізує rarity (Tier 4)
- ❌ Не торкає loot integration (Tier 6)

**Що цей UX pass робить:**
- ✅ Робить існуючі 6 archetypes **зрозумілими** при першому використанні
- ✅ Інтегрує weapons у inventory UI чесно (archetype label, не "Weapon")
- ✅ Закриває кілька workflow rough edges (ammo logistics, backpack full, ghost weapon viz)

---

## What's IN scope (kept gaps from UX audit)

### Information clarity

**G1. Dropdown показує raw identity ("Ballistic" / "Pistol")** without context — гравець не знає що Laser робить інакше до спроби.

**G2. Stats panel — raw numbers** (Damage=15, FireInterval=0.4s) без units / icons / контексту. Player мусить math-ити.

**G3. Charge-up invisible у preview.** Гравець збирає Laser+Pistol, equip'ить, тисне Attack → нічого не стріляє → confusion. Має бути hint "⚡ Requires charge — 1.0s before each shot" у preview.

**G4. Archetype label dim.** "Ballistic Pistol" — назва без feel description ("Reliable, single-shot, high damage per round").

### Inventory integration

**G5. Built item у backpack показується як generic "Weapon".** Player не розрізнить три власні збірки, тільки by EId або по черзі equip'я.

**G6. Hotbar slots — теж generic "Weapon".** Якщо у player'а Ballistic Rifle + Laser Pistol на двох слотах — UI каже "Weapon" і "Weapon".

**G7. Tooltip на weapon item у inventory — мінімальний/відсутній.** Hover не показує composition / key stats.

### Polish

**G14. Default UI Toolkit dropdown styling — функціональний, але не polished.** Hover state, selected state, padding, alignment dropdown items.

### Workflow

**G15. Ammo logistics gap.** Build Laser → mag full (12 EnergyCell) → але `Ammo_EnergyCell` у inventory = 0 → після першого reload зброя useless. Або grant'имо reserve, або попереджаємо.

**G16. Backpack full → "Backpack is full." помилка** без affordance. Має бути або highlight inventory або open drop selector.

**G17. Workbench prompt "Press E to craft" — generic.** "Weapon Workbench" або "Press E to open Weapon Builder" специфічніше.

**G18. ESC snap-close — без animation.** Modal зникає миттєво, немає sense of "closing".

### Edge cases

**G19. Ghost weapon UI silent.** Якщо item має невалідну `WeaponConfiguration`, inventory просто не показує weapon у hotbar — без візуальної ознаки broken state. Player не знає чому слот пустий після reload save.

**G20. Build button disabled state без tooltip explaining why.** Сіра кнопка — гравець не бачить причини (немає payload selected? backpack full? обидва?).

---

## What's OUT of scope (explicitly deferred — points 8-13 from audit)

User вирішив (2026-04-24) НЕ робити у цьому UX pass:

- **Decision support / inventory awareness у Builder** — Builder не показує "що ти вже маєш" inline (що зменшує clutter detection). Залишилось як future feature.
- **"Edit existing weapon" mode** — кожний Build створює новий item. Replacing in place не реалізується.
- **"Replace current weapon" toggle** — не реалізується.
- **Build feedback toast** — після Build modal закривається silently. Не додаємо confirmation toast.
- **Preview diff highlighting** — switching dropdown не підсвічує що саме змінилось (color flash на змінені stats).
- **SFX** — нуль звукових feedback (charge sound, fire variants, dropdown click) — не додаємо у цей pass.

Ці пункти будуть переглянуті разом з Tier 7 polish (повний production pass) або Tier 4 (коли rarity додає необхідність "edit existing" workflow).

---

## Implementation plan — 3 cluster, ~10 task

Cluster ordering: **A → B → C** (B залежить від decision-points в A).

### Cluster A — Builder preview clarity

Goal: гравець, який вперше відкриває Builder, розуміє що кожна опція робить + чому Build disabled.

| Task | Path / File | Scope |
|------|-------------|-------|
| **UX-A.01** Charge-up hint у preview | `WeaponBuilderWindow.cs` + `WeaponBuilderPresenter.cs` (expose `RequiresChargeUp` + `ChargeTime`) + UXML (нова `wb-hint` row) | When preview is composed and `presenter.PreviewRequiresCharge` true → render "⚡ Requires charge — {ChargeTime:F1}s before each shot" у preview pane |
| **UX-A.02** Module description sub-labels | UXML (вузька label під кожним dropdown) + USS (`.wb-module-desc`) + asset description fields | Add new optional `[SerializeField] string _description` on `PayloadCoreDefinition` and `DeliveryCoreDefinition`. Editor stub script populates: Ballistic="Solid bullet, grounded baseline", Laser="Charged energy beam — high damage, slower fire", Single="One heavy shot, high commitment", Auto="Sustained automatic fire", Scatter="Close-range cone burst (multi-pellet)". Window shows description below currently-selected dropdown option |
| **UX-A.03** Stats panel formatting pass | `WeaponBuilderWindow.cs` `RefreshPreview()` + USS | Group stats у sections: "Combat" (Damage, HeadshotMult, Penetration), "Cadence" (FireInterval, Magazine, ReloadTime), "Pattern" (ProjectilesPerShot, SpreadAngle). Add inline icons (text emoji ОК для MVP: 🎯 ⏱ 📦 etc.). Keep value formatting consistent (decimals, units like `s`/`%`) |
| **UX-A.04** Archetype sub-description | New helper `Systems/WeaponArchetypeFlavor.cs` (composition → flavor string) + Window renders below archetype label | Compose 1-line flavor: "Ballistic Pistol" → "Reliable single-shot sidearm". 6 entries hardcoded для existing archetypes (extension path: data-driven later). Якщо combo unmapped — show empty |
| **UX-A.05** Build button disabled tooltip | `WeaponBuilderWindow.cs` — на disabled Build, set `tooltip` attribute | Show reason when hovered: "Select a payload", "Select a delivery", "Backpack is full". Reuse `presenter.TryBuild` pattern: presenter exposes `string DisabledReason { get; }` |

**Acceptance for Cluster A:**
- Open Builder → bottom of preview shows hint when Laser selected
- Each dropdown's selection has 1-line description below
- Stats grid grouped у 3 logical sections
- "Ballistic Pistol" має sub-line "Reliable single-shot sidearm"
- Hover на disabled Build → tooltip with reason

**Tests:** extend `WeaponBuilderPresenterTests` for new exposed properties (RequiresCharge, ChargeTime, DisabledReason, FlavorText for combinations).

---

### Cluster B — Inventory integration

Goal: built weapons показуються як archetype labels у inventory + hotbar + tooltips. Player одразу бачить різницю між власними збірками.

**Architectural decision needed (block before B starts):**

`ItemState.DisplayName` зараз — `Definition?.DisplayName ?? DefinitionId`. Для weapon item (`DefinitionId="Weapon"`) це поверне "Weapon" generic.

Опції щоб показати "Ballistic Pistol":
- **(B-arch-1)** ItemState має `WeaponConfiguration` — можемо compose label inline без registry (тільки IDs не resolve до DisplayName/FormFactor — лише raw IDs). Result: "BallisticRound SingleAction". Acceptable but ugly.
- **(B-arch-2)** Введення view-side helper `Systems/WeaponDisplayName.For(ItemState, ICoreDefinitionRegistry) → string`. Caller (inventory UI) має реєстр через `App.Instance.CoreDefinitions`. Cleaner, але coupling. Recommend.
- **(B-arch-3)** Перенесення resolution на ItemState за допомогою static `ItemDefinition`-like registry для weapons. Розкладати — занадто рано (rarity buckets, Exotic prefix etc. — Tier 4+).

**Recommend B-arch-2 для цього pass.** Helper resolves через registry, inventory-side code викликає його.

| Task | Path / File | Scope |
|------|-------------|-------|
| **UX-B.01** `WeaponDisplayName` helper | New `Systems/WeaponDisplayName.cs` | Static `For(ItemState item, ICoreDefinitionRegistry registry) → string`. If `!item.HasWeaponConfiguration` → fallback `item.Definition?.DisplayName ?? item.DefinitionId`. Else compose via `WeaponArchetypeLabel.Compose` using registry to resolve. Null-safe. Tests for: weapon item produces "Ballistic Pistol", non-weapon falls through, broken config (registry can't resolve) returns "[Broken Weapon]" |
| **UX-B.02** Inventory UI uses helper | `Assets/Scripts/View/UI/Inventory/InventoryUI.cs` (and any other place що рендерить ItemState назву — grep `item.DisplayName`) | Replace direct `item.DisplayName` для weapon-slot rendering з `WeaponDisplayName.For(item, App.Instance.CoreDefinitions)`. **Don't replace** для backpack non-weapon items (zero behaviour change). Find every location showing weapon items. Note: UX-B uses `App.Instance` access у view layer — OK по CLAUDE.md (View layer can access App) |
| **UX-B.03** Hotbar slot label | Knwown слоти у HUD (search "Hotbar" / `WeaponSlots[`) — `View/AimCursorOverlay`? `InventoryUI.cs`? | Same treatment for hotbar UI rendering. Якщо hotbar показує тільки prefab visualy + не label — потрібно додати inline text label поверх slot icon |
| **UX-B.04** Weapon item tooltip | Find existing tooltip infrastructure (grep "Tooltip" — there might be `Assets/Scripts/View/UI/Tooltips` etc.) | На hover weapon item у inventory → показати: archetype label, payload+delivery names, key 4-5 stats (Damage, FireInterval, Magazine, Penetration, ChargeTime if present). Якщо tooltip system не існує — створити мінімальний (UI Toolkit panel або Canvas Text) — scope-cap |
| **UX-B.05** Ghost weapon visual badge | Inventory UI — when `WeaponDisplayName.For` повертає "[Broken Weapon]" → red tint or warning icon overlay | Self-explanatory broken-state — гравець відразу розуміє що item потребує attention |

**Acceptance for Cluster B:**
- Backpack: 3 different builds показуються як "Ballistic Pistol" / "Ballistic Rifle" / "Laser Pistol", не "Weapon × 3"
- Hotbar slot tooltip / label показує archetype
- Hover на weapon item → tooltip з композицією + key stats
- Item з broken config → red badge у inventory

**Tests:** unit tests на `WeaponDisplayName.For` (5-7 cases). Integration test: build → check `WeaponDisplayName.For` повертає expected label.

---

### Cluster C — Workflow rough edges

Goal: знизити friction на cross-feature boundaries (ammo, backpack, prompt clarity).

| Task | Path / File | Scope |
|------|-------------|-------|
| **UX-C.01** Workbench prompt rename | `WorkbenchView.cs` `_promptText` default | "Press E to craft" → "Weapon Workbench  ·  Press E". Specific і має both type + action. Single-field change |
| **UX-C.02** Ammo reserve auto-grant on Build | `WeaponBuilderPresenter.cs` TryBuild → after item lands, also add 1-2 mag-worth ammo to inventory of corresponding `AmmoType` | When building Laser+Pistol — додати `Ammo_EnergyCell × 24` у backpack (if there's a free slot OR if there's existing stack to merge into). Якщо немає де покласти — silent skip (don't fail Build). Reset assumption — це Tier 1/2 без loot integration, тому magic ammo OK. **Open question:** скільки давати? Reasonable: 2x MagazineSize. Document decision. |
| **UX-C.03** Backpack full UX | `WeaponBuilderWindow.cs` Build button disabled tooltip + maybe inline message | Покращити тільки message + disabled tooltip — не додаємо drop selector (would be scope creep). Already partially covered by UX-A.05 |
| **UX-C.04** Dropdown styling polish | `WeaponBuilderWindow.uss` | Hover state, selected state, dropdown popup styling, padding consistency. Visual nicety pass. Match modal's overall dark theme |
| **UX-C.05** Modal fade-out animation | `WeaponBuilderWindow.cs` Close() | Replace immediate `display = None` with brief fade (0.15s opacity). UI Toolkit transitions or schedule.Execute. Low-priority — можна skip якщо bandwidth tight |

**Acceptance for Cluster C:**
- Workbench prompt каже "Weapon Workbench"
- Build Laser → backpack автоматично отримує EnergyCell ammo (один раз)
- Disabled Build tooltip каже "Backpack full"
- Dropdown styling polished
- ESC fades modal out (optional)

**Tests:** unit test for ammo auto-grant у presenter. Integration test: build creates item + ammo entry.

---

## Architectural notes для resuming agent

### Layer boundaries (CLAUDE.md compliance check)

- **Presenter (pure C#):** ОК додавати `RequiresCharge`, `ChargeTime`, `DisabledReason`, `FlavorText` properties.
- **WeaponDisplayName helper:** lives у `Systems/` namespace (static class). Acceptable що приймає registry — це data lookup, не runtime logic.
- **View / Window:** OK using `App.Instance.CoreDefinitions` directly per CLAUDE.md (View layer has app access). Inventory UI same.
- **Auto-grant ammo:** дискусійне — це **gameplay action** (мутує inventory). Якщо presenter це робить — він є gameplay-adjacent. Обґрунтовано: presenter уже мутує inventory (Build → новий item). Auto-grant ammo — той самий level of mutation. Acceptable.

### Don't introduce

- New singletons (CLAUDE.md rule 12)
- Direct Unity refs у State (rule 6/8)
- Hidden static state у systems (rule 5)

### File reference for next agent

| Аспект | Files |
|------|------|
| Presenter | `Assets/Scripts/View/UI/WeaponBuilder/WeaponBuilderPresenter.cs` |
| Window | `Assets/Scripts/View/UI/WeaponBuilder/WeaponBuilderWindow.cs` |
| UXML / USS | `Assets/Resources/UI/WeaponBuilder/WeaponBuilderWindow.{uxml,uss}` |
| SOs | `Assets/Scripts/State/PayloadCoreDefinition.cs` (+subclasses), `DeliveryCoreDefinition.cs` |
| Stub assets | `Assets/Resources/WeaponBuilder/Payloads/*.asset` etc. + editor script `WeaponBuilderStubAssets.cs` |
| Workbench | `Assets/Scripts/View/WorkbenchView.cs` |
| Inventory UI | search для `InventoryUI.cs` and grep `WeaponSlots`, `Backpack` references |
| Tests | `Assets/Tests/EditMode/` — see `WeaponBuilderTestFactory` for SO test factory |

---

## Implementation order

Recommended sequence:

1. **Cluster A first** — pure additive, no architectural blockers. Ships even if B/C delayed.
2. **B-arch decision** — confirm B-arch-2 (`WeaponDisplayName` helper) before starting B coding.
3. **Cluster B** — biggest impact на player perception. Cluster A is preparation; B is delivery.
4. **Cluster C** — finishing touches.

**Estimated effort:**
- Cluster A: ~4-6 hours (presenter changes + UXML/USS + flavor table)
- Cluster B: ~4-8 hours (helper + audit existing inventory UI + tooltip — depends on existing infra)
- Cluster C: ~2-4 hours

**Total:** ~10-18 hours. Не розбивати на tier — це один UX pass.

---

## Test strategy

**Per cluster:**
- A: extend `WeaponBuilderPresenterTests` — new properties (4-5 cases each); test `WeaponArchetypeFlavor` (6 happy combos + null/empty fallback)
- B: new `WeaponDisplayNameTests` (5-7 cases); integration test через `WeaponBuilderEndToEndTests` extension
- C: presenter test for ammo auto-grant; manual play-test for dropdown polish + fade

**Existing test count baseline:** ~90 зелених. Очікується після UX pass: ~110-115.

---

## Open design questions for next agent

Перед coding — підтвердити з користувачем:

1. **UX-C.02 ammo amount** — скільки давати? My recommendation: 2× MagazineSize. Confirm.
2. **UX-A.04 flavor table** — hardcode у helper, чи додати на SO як `[SerializeField] string _flavorText`? Recommend SO field — data-driven, але scope-bigger. MVP: hardcode 6 entries.
3. **UX-B.04 tooltip** — створювати власну infrastructure чи reuse existing (грепнути)? Якщо немає — yet-another-modal scope concern. Possible to scope-cap до simple text overlay у inventory UI.
4. **UX-C.05 fade-out** — yes/no? Optional polish.

---

## Acceptance gate (whole pass)

- [ ] Усі UX-A.* tasks done
- [ ] Усі UX-B.* tasks done
- [ ] Усі UX-C.* tasks done (or explicitly skipped/deferred з обґрунтуванням)
- [ ] Existing tests все ще зелені
- [ ] +20-25 нових unit/integration tests
- [ ] Manual play-test:
  - Open Builder, бачу module descriptions
  - Build Laser+Pistol — preview show charge hint, build disabled tooltip when no selection
  - Inventory shows "Ballistic Pistol" / "Laser Rifle" instead of "Weapon × N"
  - Hover на weapon item → tooltip з compositioin
  - Workbench prompt "Weapon Workbench"
- [ ] Update `README.md` + `status.md` з outcome
- [ ] Commit + merge

---

## Related docs

- [`README.md`](../README.md) — feature overview, current state
- [`design.md`](../design.md) — design intent
- [`architecture.md`](../architecture.md) — Q1-Q7 + D1-D14 architectural decisions
- [`plan/roadmap.md`](./roadmap.md) — tier structure
- [`plan/status.md`](./status.md) — pause summary, decisions log
- [`plan/tasks.md`](./tasks.md) — Tier 0-2 task records
