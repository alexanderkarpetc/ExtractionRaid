# Weapon Attachments — Implementation Status (resume doc)

> Code-level стан епіку для відновлення після context-compact. Дизайн — у сусідніх доках
> ([README](./README.md) · [analysis](./analysis.md) · [stats](./stats.md) · [slots](./slots.md) · [catalog](./catalog.md) · [ux](./ux.md) · [edit-access](./edit-access.md)).
> **Оновлено:** 2026-06-16.

## Поточний стан

**P1 ✅ + P2 ✅ (функціонально завершені). 588 EditMode green.** Sidegrade-loop живий end-to-end:
інвентар (будь-де) → right-click зброю → **Modify** → editor (двопанельний, Variant A) → фокус слота →
install/remove мод → стати міняються з green/red give/take → equipped-зброя ресинкається live → в інвентарі pips + tooltip списком модів.

**Verified:** compile + 588 EditMode + user-eyeball (editor працює, pips/tooltip — щойно полагоджено, фінальний eyeball на користувачі).

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

### Tests
`WeaponStatDisplayTests`, `AttachmentComposeTests`, `AttachmentEditorPresenterTests`, `RarityTierFallbackTests`, `TooltipBuildersTests` (extended), `WeaponBuilderTestFactory` (+`MakeAttachment`, `MakeDatabase` attachments param). **588 green.**

## Паралельні зміни користувача (НЕ чіпати/будувати поверх)
- **Item icons** у inventory slots: `InventorySlotElement._icon` + `ItemIconRegistryAsset` + `UpdateIcon()` + `.inv-slot__icon` USS + `SetIconRegistry()`. (Користувач додав окремо.)
- **Esc-close** у `InventoryUI` (дзеркалить Tab — закриває editor/builder).

## Наступні кроки (на вибір — обрати після compact)
1. **Playtest/balance** — числа модів = placeholders; протюнити на відчутті.
2. **P4 — нові механіки:** Noise→Suppressor (боти чують `WeaponFired` у `NoiseRadius`) + Sight/FOV→Sniper Scope (fog-of-war). Розблоковують 2 відкладені моди.
3. **P3 — unique-моди + rarity-scaled слоти:** `CompatibleArchetype` enforcement (Laser Focusing/Scatter Choke/Auto Heat-Sink) + к-ть слотів = f(core rarity) (зараз фіксований набір усіх 5).
4. **Loot-gating attachments** — зараз infinite supply (registry); зробити моди-як-items у backpack з consume (як Tier 6 для cores).

## Відкладене / спрощення MVP (треба памʼятати)
- Slot count = **фіксований** (усі 5 категорій), не rarity-scaled. Q20.
- Attachment supply = **infinite** (з registry), без backpack-consume. (loot-gating = future).
- `CompatibleArchetype` — поле є, **НЕ enforced** (усі моди universal у P2). Enforcement → P3 unique-моди.
- Suppressor/Sniper Scope **не зроблені** (потребують Noise/FOV механік → P4).
- "Right-click to modify" footer показується на ВСІХ weapon-тултіпах (навіть loot, де Modify ще нема в контекст-меню) — мінорна неточність.

## Як тестувати в грі
Свіжий гравець (видали сейв) спавниться з 6 зброями random-rarity (без модів). Tab → інвентар → right-click зброю → **Modify** → editor. Встав мод → стати/бар, pips у слоті, tooltip "Attachments" + footer. Equip + Modify → live-оновлення.

## Verification команди (Unity bridge, порт 6401)
`refresh_unity(compile=request, scope=all)` → `read_console(types=[error], filter=CS)` → `run_tests(EditMode)` → `get_test_job`. Очікувано 588 green.
