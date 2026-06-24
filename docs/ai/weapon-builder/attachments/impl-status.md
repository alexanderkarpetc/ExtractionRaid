# Weapon Attachments — Implementation Status (resume doc)

> Code-level стан епіку для відновлення після context-compact. Дизайн — у сусідніх доках
> ([README](./README.md) · [analysis](./analysis.md) · [stats](./stats.md) · [slots](./slots.md) · [catalog](./catalog.md) · [ux](./ux.md) · [edit-access](./edit-access.md)).
> **Оновлено:** 2026-06-24.

## Поточний стан

**P1 ✅ + P2 ✅ + Loot-gating ✅ + Inventory drag/highlight ✅ (функціонально завершені). 599 EditMode green.** Sidegrade-loop живий end-to-end:
інвентар (будь-де) → right-click зброю → **Modify** → editor (двопанельний, Variant A) → фокус слота →
install/remove мод (**споживає/повертає мод з backpack**) → стати міняються з green/red give/take → equipped-зброя ресинкається live → в інвентарі pips + tooltip списком модів.
**Або прямо в інвентарі:** drag мода на зброю → ставиться; hover мода/зброї → кросс-хайлайт сумісних слотів.

**Verified:** compile + 599 EditMode + user-eyeball (P1/P2 — підтверджено; loot-gating + drag/highlight — фінальний in-game eyeball на користувачі).

## Що шипнуто — по фазах

### P1 — візуалізація наявного (✅)
- `Systems/WeaponStatDisplay.cs` — pure: WeaponStats → player-facing рядки (Damage/Headshot/RateOfFire/Magazine value-only + **Stability/Accuracy/Ergonomics/Damage/RoF** bar rows; bar fill = goodness, higher=better). `Recoil` показуємо як **"Stability"**.
- `View/UI/RarityVisuals.cs` — RarityTier → Color/Hex (gray/green/blue/purple/gold).
- Tooltip: бари + dual-rarity subtitle (rich-text колір) — `WeaponTooltipBuilder` + `TooltipModel`/`TooltipController`/`TooltipOverlay.{uxml,uss}` (бар-рядки, layout Label+Value/full-width bar/value-only внизу).
- Inventory: **dual-rarity corner frame** (`InventorySlotElement` `_rarityTl`/`_rarityBr` + `_inv-slot.uss`).
- Cheat + fresh-player loadout спавнять **random rarity** (Common-fallback для незаповнених tier'ів у `StatsByTier`/`SpecificByTier`).

### P2 — перші attachments (✅)
- **P2.1 data+compose:** `State/AttachmentSlot.cs` (Muzzle/Grip/Buttstock/Optic/Magazine), `State/WeaponStatAxis.cs` (Damage/RateOfFire/MagazineSize/ReloadTime/Recoil/Spread/Ergonomics), `State/AttachmentInstance.cs` ({Slot,DefinitionId}, IEquatable), `State/AttachmentDefinition.cs` (SO: Id/DisplayName/Slot/CompatibleArchetype/`StatDelta[]`; flat, no rarity). `WeaponConfiguration.Attachments[]` + `WeaponConfigVersion`. `CoreDefinitionDatabase._attachments` + `ICoreDefinitionRegistry`/`DatabaseCoreDefinitionRegistry` (Get/TryGet/All Attachment). **`WeaponStatComposer.ApplyAttachments(stats, config, registry)`** — axis→raw-field мапа (delta option A, raw-change семантика; %=ціле). Wired у `WeaponAssemblySystem.TryAssemble` + `WeaponTooltipBuilder`.
- **P2.2a presenter:** `View/UI/Attachments/AttachmentEditorPresenter.cs` — pure: Load/PayloadSlots+DeliverySlots/InstalledIn/CompatibleMods(slot-match)/Install/Remove(live, bump `WeaponConfigVersion`)/CurrentStats/PreviewWith. Compose через `WeaponAssemblySystem.TryAssemble`.
- **P2.2b UI:** `View/UI/Attachments/AttachmentEditorWindow.cs` + `Resources/UI/Attachments/AttachmentEditor.uss`. Двопанельна модалка (Variant A): header (archetype + 2 cores rarity-кольором), ліві слоти (focus-able, групи Payload/Delivery), правий mod-list (give/take теги) + stat-readout (`WeaponStatDisplay`) з **gold-барами** + **comparison-сегмент green/red** при hover. Reuse `WeaponBuilderPanelSettings` (sortOrder 200). `OnStateChanged` бампає `inventory.Version` (щоб InventoryWindow перемалював слот → pips/tooltip).
- **P2.2c entry:** `InventoryWindow.BuildPlayerOptions` → опція **"Modify"** для зброї → `AttachmentEditorWindow.Instance.Open(weapon)`. Tab/Esc закривають editor першим (`InventoryUI`). **config-version re-assembly:** `ItemState.WeaponConfigVersion` + `WeaponEntityState.ConfigVersion`; `WeaponSyncSystem.Tick` ребілдить при розбіжності (D6 live equipped-resync).
- **P2.3 base-моди:** `WeaponBuilderStubAssets` → 9 SO у `Resources/WeaponBuilder/Attachments/` + у DB (PowerComp, MuzzleBrake, Vertical/AngledGrip, Heavy/SkeletonStock, RedDot, Extended/QuickMag). Числа = catalog placeholders. Усі universal (compat-archetype порожній). `Tools → Weapon Builder → Create Stub Assets` (idempotent).
- **P2.4 mod-pips:** `InventorySlotElement` `_modPips` (top-right): solid крапки = встановлені моди **+ жовто-помаранчеві «!» = к-ть вільних слотів** (call-to-action "можна апгрейдити"). Total slots = canonical з `AttachmentEditorPresenter.PayloadSlots+DeliverySlots` (5). Новий токен `--color-mod-hint` rgb(255,170,50).
- **Tooltip polish (post-P2.4):** `TooltipModel.Footer` ("Right-click to modify") + `TooltipModel.FooterAccent` (footer підсвічується `--color-mod-hint` коли є вільний слот, клас `.tt-footer--accent`) + секція **"Attachments"** (список встановлених модів) у `WeaponTooltipBuilder`.

### Loot-gating (✅, 2026-06-24) — recoverable
Моди = **предмети у backpack** замість infinite registry-supply. Рішення: **recoverable** (install переміщує мод backpack→зброя; remove/swap повертає в backpack; блокується коли backpack повний).
- **L1 — content:** 9 `ItemDefinition` entries з id == AttachmentDefinition SO (PowerComp/MuzzleBrake/Vertical+AngledGrip/Heavy+SkeletonStock/RedDot/Extended+QuickMag), `Category=WeaponMod`, `MaxStackSize=20` (stackable).
- **L2 — presenter:** `AttachmentEditorPresenter` ctor тепер `(registry, InventoryState, Func<EId>)`. `CompatibleMods(slot)` = backpack-present (slot-match, deduped) ∪ installed (щоб можна було зняти). `CountInBackpack(modId)`. `Install` → `ConsumeFromBackpack` (decrement stack / null slot); swap повертає витіснений мод (transactional — rollback consume якщо немає місця). `Remove` → `InventorySystem.AddToBackpack` (повертає, блок коли повний). `LastError` для UI. Бампає `WeaponConfigVersion` (D6 live-resync).
- **L3 — UI:** `AttachmentEditorWindow` будує presenter з `App.Player.Inventory` + `App.AllocateEId`. Mod-rows показують `xN` owned-count; `.ae-mod-status` inline-feedback при block-on-full.
- **L4 — granting:** fresh-player loadout (`PlayerSpawnSystem`, slots 8-16, **1× кожного**) + dev-cheat **"Give All Mods"** (`DevCheatsWindow`, 3× кожного). **Real loot-table drops — відкладено (future).**

### Inventory drag-drop + cross-highlight (✅, 2026-06-24)
- **H1 — extraction:** install/remove gameplay-логіку винесено в `Systems/AttachmentInstallSystem` (stateless, за контрактом CLAUDE.md). API: `Install(weapon, reg, inv, alloc, modId, out err)` (slot derived from mod def), `Remove(weapon, inv, alloc, slot, out err)`, `InstalledIn`, `WithSlot` (public, для PreviewWith), `Resolve(reg, modId)→AttachmentDefinition`, `CanInstall(weapon, modDef)` (MVP: будь-яка built-зброя; archetype-gating → P3). `AttachmentEditorPresenter` тепер тонкий wrapper (делегує, додає StateChanged + LastError; зберіг wrong-slot guard).
- **H2 — drag-drop install (двонапрямлено):** `InventoryWindow.TryResolveAttachmentInstall(src, tgt)` — direction-agnostic резолвер: визначає (weapon, modId) незалежно від того що тягнули (mod→weapon АБО weapon→mod). І `TryDropOnSlot`, і `CanDropOnTarget` тягнуть через цей **один** резолвер + один `AttachmentInstallSystem.Install` → обидва напрямки ідентичні by construction. Install перед weapon-swap/TryMove; drag-ghost зелений над валідними цілями (swap дозволено).
- **H3 — cross-highlight:** hover мода → підсвічує weapon-слоти (`SetCompatible` → `.inv-slot--compat` yellow-orange); hover зброї → підсвічує моди в backpack; під час DRAG мода — всі сумісні weapon-слоти на час drag. **Підсвітка лише коли відповідний слот ВІЛЬНИЙ** (`AttachmentInstallSystem.CanInstallIntoFreeSlot` = CanInstall && !InstalledIn(slot)) — «можна додати», не swap. (Drag-drop install при цьому swap дозволяє.) Hooks: `OnSlotPointerEnter/Leave` (hover, skip during drag), drag-start у `OnSlotPointerMove` (mod-only), clear у `ClearAllSlotHover`. Iterate лише player-слоти (`EnumeratePlayerItemSlots` = weapon+backpack).

### Attachment item tooltip (✅, 2026-06-24)
Раніше attachment-предмет провалювався в generic-гілку `ItemTooltipBuilder` → лише title. Тепер:
- **`View/UI/Tooltip/Builders/AttachmentTooltipBuilder.cs`** (pure) — title + "{Slot} Attachment" subtitle + секція **"Effects"** з рядком на кожну `StatDelta` (значення = `<color>±N%</color>`, green=покращення / red=мінус). Quantity при stack>1.
- **`View/UI/AttachmentStatDisplay.cs`** (pure, спільний) — `AxisLabel`/`DeltaIsGood`/`FormatPercent`/`Hex(good)` (`GoodHex` #50C878 / `BadHex` #DC6464). Editor `AttachmentEditorWindow.DeltaIsGood` тепер делегує сюди (single-source good/bad-правила).
- `ItemTooltipBuilder` делегує на `AttachmentTooltipBuilder.For` коли `registry.TryGetAttachment` резолвиться (+ AppendPrice).

### Tests
`WeaponStatDisplayTests`, `AttachmentComposeTests`, `AttachmentEditorPresenterTests` (loot-gating: consume/return/swap/block-on-full/not-owned/already-installed/owned-list), `AttachmentInstallSystemTests` (Resolve/CanInstall/CanInstallIntoFreeSlot/derive-slot/not-owned), `AttachmentTooltipBuilderTests` (title/slot/effects/good-bad-color/recoil-neg/quantity), `RarityTierFallbackTests`, `TooltipBuildersTests` (extended + footer-accent + attachment-item delegation), `WeaponBuilderTestFactory` (+`MakeAttachment`, `MakeDatabase` attachments param). **607 green.**

## Паралельні зміни користувача (НЕ чіпати/будувати поверх)
- **Item icons** у inventory slots: `InventorySlotElement._icon` + `ItemIconRegistryAsset` + `UpdateIcon()` + `.inv-slot__icon` USS + `SetIconRegistry()`. (Користувач додав окремо.)
- **Esc-close** у `InventoryUI` (дзеркалить Tab — закриває editor/builder).

## Наступні кроки (на вибір — обрати)
1. **Playtest/balance** — числа модів = placeholders; протюнити на відчутті.
2. **P4 — нові механіки:** Noise→Suppressor (боти чують `WeaponFired` у `NoiseRadius`) + Sight/FOV→Sniper Scope (fog-of-war). Розблоковують 2 відкладені моди.
3. **P3 — unique-моди + rarity-scaled слоти:** `CompatibleArchetype` enforcement (Laser Focusing/Scatter Choke/Auto Heat-Sink) + к-ть слотів = f(core rarity) (зараз фіксований набір усіх 5).
4. **Real loot-table drops для модів** — зараз моди роздаються лише через cheat-loadout + dev-cheat; вписати у LootSystem/loot-таблиці контейнерів/ботів.

## Відкладене / спрощення MVP (треба памʼятати)
- Slot count = **фіксований** (усі 5 категорій), не rarity-scaled. Q20.
- Attachment supply = **loot-gated ✅** (backpack-consume, recoverable). АЛЕ моди ще не падають з лута — роздаються через cheat-loadout + dev-cheat "Give All Mods" (real drops → next step #4).
- `CompatibleArchetype` — поле є, **НЕ enforced** (усі моди universal у P2). Enforcement → P3 unique-моди.
- Suppressor/Sniper Scope **не зроблені** (потребують Noise/FOV механік → P4).
- "Right-click to modify" footer показується на ВСІХ weapon-тултіпах (навіть loot, де Modify ще нема в контекст-меню) — мінорна неточність.

## Як тестувати в грі
Свіжий гравець (видали сейв) спавниться з 6 зброями random-rarity + **9 типів модів у backpack (slots 8-16, 1× кожного)**. На наявному сейві: Dev Cheats → **"Give All Mods"** (3× кожного). Tab → інвентар → right-click зброю → **Modify** → editor. Список модів = лише ті, що в backpack (з `xN` owned-count). Встав мод → споживається з backpack, стати/бар, pips у слоті, tooltip "Attachments" + footer. Зніми/поміняй → мод повертається в backpack. Забий backpack ущент → remove показує "Backpack full". Equip + Modify → live-оновлення.
**Drag/highlight (без редактора):** наведи на мод у backpack → зброї з **вільним** відповідним слотом підсвічуються жовто-помаранчевим (і навпаки — наведи на зброю → моди, чий слот вільний); зайняті слоти НЕ підсвічуються. Перетягни мод на зброю **АБО зброю на мод** → ставиться (двонапрямлено, той самий результат; swap дозволено, лише підсвітка обмежена вільними слотами; drag-ghost зелений над валідними цілями).

## Verification команди (Unity bridge, порт 6401)
`refresh_unity(compile=request, scope=all)` → `read_console(types=[error], filter=CS)` → `run_tests(EditMode)` → `get_test_job`. Очікувано 607 green.
