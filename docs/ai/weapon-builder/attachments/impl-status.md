# Weapon Attachments — Implementation Status (resume doc)

> Code-level стан епіку для відновлення після context-compact. Дизайн — у сусідніх доках
> ([README](./README.md) · [analysis](./analysis.md) · [stats](./stats.md) · [slots](./slots.md) · [catalog](./catalog.md) · [ux](./ux.md) · [edit-access](./edit-access.md)).
> **Оновлено:** 2026-06-24.

## Поточний стан

**P1 ✅ + P2 ✅ + Loot-gating ✅ + Inventory drag/highlight ✅ + P3 (unique mods + rarity-slots) ✅ (функціонально завершені). 614 EditMode green.** Sidegrade-loop живий end-to-end:
інвентар (будь-де) → right-click зброю → **Modify** → editor (двопанельний, Variant A) → фокус слота →
install/remove мод (**споживає/повертає мод з backpack**) → стати міняються з green/red give/take → equipped-зброя ресинкається live → в інвентарі pips + tooltip списком модів.
**Або прямо в інвентарі:** drag мода на зброю (або зброю на мод) → ставиться; hover мода/зброї → кросс-хайлайт вільних сумісних слотів. **Слотів — f(rarity); унікальні моди лише під свій архетип.**

**Verified:** compile + 614 EditMode + user-eyeball (P1/P2 — підтверджено; loot-gating + drag/highlight + P3 — фінальний in-game eyeball на користувачі; потребує `Create Stub Assets` для unique-.asset).

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

### P3 — unique mods + rarity-scaled slots (✅, 2026-06-25)
- **P3-1 — rarity-scaled слоти:** `Systems/AttachmentSlots.cs` — `PayloadOrder [Optic,Magazine,Buttstock]` + `DeliveryOrder [Muzzle,Grip]`; `CountForRarity` (Common/Uncommon 1, Rare/Epic 2, Legendary 3, cap=category count); `IsUnlocked(weapon, slot)` / `TotalUnlocked(weapon)`. Common/Common ≈ 2 слоти (Optic+Muzzle) → Legendary/Legendary = 5. `CanInstall`/`Install` reject locked slots; editor показує лише розблоковані; pips «!» + tooltip footer-total = `TotalUnlocked` (per-weapon). Видалено старі fixed `AttachmentEditorPresenter.PayloadSlots/DeliverySlots`.
- **P3-2 — archetype enforcement:** `AttachmentInstallSystem.ArchetypeMatches(weapon, modDef, registry)` — empty token = universal; інакше case-insensitive match vs payload `Archetype` / delivery `FormFactor` / `Pattern`. `CanInstall`/`CanInstallIntoFreeSlot` тепер беруть `registry` + чекають archetype; `Install` guard'иться через CanInstall; presenter `CompatibleMods` фільтрує за archetype. Усі callers (InventoryWindow drop+highlight, tests) оновлені.
- **P3-3 — unique mods:** 3 SO через `WeaponBuilderStubAssets.MakeUniqueAttachment` (Laser Focusing Optic: Optic/**Laser**; Scatter Choke: Muzzle/**Scatter**; Auto Heat-Sink: Muzzle/**Auto**) — ефекти на наявних осях (charge/heat = proxy, true-версії → P4). + matching `ItemDefinition` (LaserFocusing/ScatterChoke/AutoHeatSink, stackable 20) + grant у fresh loadout (12 модів, slots 8-19) + "Give All Mods". **Треба `Tools → Weapon Builder → Create Stub Assets` щоб згенерувати 3 нові .asset + оновити DB.**

### Tests
`WeaponStatDisplayTests`, `AttachmentComposeTests`, `AttachmentEditorPresenterTests` (loot-gating; weapon=Legendary so all slots unlocked), `AttachmentInstallSystemTests` (Resolve/CanInstall/free-slot/derive-slot/not-owned + **archetype match + install-reject**), `AttachmentSlotsTests` (rarity curve / cap / Common→Legendary unlock), `AttachmentTooltipBuilderTests`, `RarityTierFallbackTests`, `TooltipBuildersTests` (+ footer-accent + attachment-item delegation), `WeaponBuilderTestFactory`. **614 green.**

## Паралельні зміни користувача (НЕ чіпати/будувати поверх)
- **Item icons** у inventory slots: `InventorySlotElement._icon` + `ItemIconRegistryAsset` + `UpdateIcon()` + `.inv-slot__icon` USS + `SetIconRegistry()`. (Користувач додав окремо.)
- **Esc-close** у `InventoryUI` (дзеркалить Tab — закриває editor/builder).

## Наступні кроки (на вибір — обрати)
1. **Playtest/balance** — числа модів = placeholders; протюнити на відчутті (тепер ще й rarity-крива слотів + unique-моди).
2. **P4 — нові механіки:** Noise→Suppressor (боти чують `WeaponFired` у `NoiseRadius`) + Sight/FOV→Sniper Scope (fog-of-war). Розблоковують відкладені моди + дають unique-модам справжні charge/heat-ефекти.
3. **Розширити compare-diff** на рядки attachments/ammo-type (зараз лише bar-стати) — `WeaponComparePanel`.

## Відкладене / спрощення MVP (треба памʼятати)
- Slot count = **rarity-scaled ✅** (`AttachmentSlots`, Common/Uncommon 1 · Rare/Epic 2 · Legendary 3, cap=category count). Крива тюниться в плейтесті.
- `CompatibleArchetype` — **enforced ✅** (P3-2). 3 unique-моди (Laser/Scatter/Auto). Інші моди universal.
- Unique-моди використовують **наявні стат-осі як proxy** (Laser Focusing → Damage/Ergo замість ChargeTime; Auto Heat-Sink → Recoil/Damage замість Heat). Справжні charge/heat-ефекти → P4.
- Attachment supply = **loot-gated ✅** (backpack-consume, recoverable) + **drop from loot ✅** (2026-06-26): `ContainerConstants.AttachmentModDrops` in RandomLootBox (×0.5) + ModuleCache (cores+mods) + 25% bot drop (`LootSystem.CreateLootable`). Fresh-player starting mods removed; "Give All Mods" cheat stays. Drop-rates = placeholders (`BotModDropChance`, mod weights).
- Suppressor/Sniper Scope **не зроблені** (потребують Noise/FOV механік → P4).
- "Right-click to modify" footer показується на ВСІХ weapon-тултіпах (навіть loot, де Modify ще нема в контекст-меню) — мінорна неточність.

## Як тестувати в грі
> ⚠️ Після P3: запусти `Tools → Weapon Builder → Create Stub Assets` (генерує 3 нові unique-.asset + оновлює DB), інакше LaserFocusing/ScatterChoke/AutoHeatSink не існуватимуть у грі.

Свіжий гравець (видали сейв) спавниться з 6 зброями random-rarity + **12 типів модів у backpack (9 universal + 3 unique, slots 8-19, 1× кожного)**. На наявному сейві: Dev Cheats → **"Give All Mods"** (3× кожного). Tab → інвентар → right-click зброю → **Modify** → editor. Editor показує **лише розблоковані слоти** (к-ть = f(rarity) кожного core — Common-зброя ≈ 2 слоти, Legendary = 5). Список модів = лише ті, що в backpack + **archetype-сумісні** (Laser Focusing видно лише на Laser-зброї, Scatter Choke — на Scatter, Auto Heat-Sink — на Auto). Встав мод → споживається з backpack, стати/бар, pips у слоті, tooltip "Attachments" + footer. Зніми/поміняй → мод повертається в backpack. Забий backpack ущент → remove показує "Backpack full". Equip + Modify → live-оновлення.
**Drag/highlight (без редактора):** наведи на мод у backpack → зброї з **вільним** відповідним слотом підсвічуються жовто-помаранчевим (і навпаки — наведи на зброю → моди, чий слот вільний); зайняті слоти НЕ підсвічуються. Перетягни мод на зброю **АБО зброю на мод** → ставиться (двонапрямлено, той самий результат; swap дозволено, лише підсвітка обмежена вільними слотами; drag-ghost зелений над валідними цілями).

## Verification команди (Unity bridge, порт 6401)
`refresh_unity(compile=request, scope=all)` → `read_console(types=[error], filter=CS)` → `run_tests(EditMode)` → `get_test_job`. Очікувано 614 green. (+ після P3: `Tools → Weapon Builder → Create Stub Assets` для unique-.asset.)
