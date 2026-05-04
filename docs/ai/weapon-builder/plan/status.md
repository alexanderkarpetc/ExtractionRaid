# Weapon Builder — Status

> ⏸ **PAUSED 2026-05-01, partial unpause 2026-05-04 → 2026-05-05.** Foundation + Tier 6 + Tier 8 done; Cluster A (player-facing legacy) retired 2026-05-01; **Cluster B (bot weapon migration / Tier 4a) retired 2026-05-04**; **Tier 8.x* (asset architecture rebuild) shipped 2026-05-05** — full inversion of payload/delivery prefab roles, all 5 module prefabs regenerated, weapon-on-death drop physics added, MuzzlePoint dynamic resolution. Remaining backlog: **Tier 9 (VFX/SFX, awaiting FX artist)** → Tier 10 (feel polish, mostly overlap з gunplay). 6 archetypes feel coherent + composable.

> **Status (2026-05-01):** Foundation done (Tiers 0-2) + UX Pass 1 done + **Tier 6 done** (Waves A/B/D/E/F; G7 deferred) + **Tier 8 done** (Waves A-E; F deferred — UI prereq) + **Cluster A retired** (legacy `Rifle`/`Pistol`/`Ammo_Pistol*` player-facing references replaced з Builder content).
>
> **Strategic pivot 2026-05-01:** content expansion (Tier 3, 5) deferred sine die. Polish-first execution: make current 6 archetypes feel great. 📋 Next track: **Tier 8.x follow-ups → Tier 4a (bot weapon migration) → Tier 9 (VFX/SFX, scoped 2×3) → Tier 10 (feel)**. See [Pause summary](#pause-summary--session-resumption-guide) для повного rationale.

---

## Pause summary — session resumption guide

### TL;DR стан

Foundation (Tiers 0a, 0b, 1, 2) + UX Pass 1 завершені. Gameplay: гравець підходить до Workbench у Hideout (prompt "Weapon Workbench · Press E"), відкриває Builder screen (UI Toolkit modal з drag&drop palette + slots + read-only backpack context). Перетягує Payload + Delivery cards у typed slots, бачить live preview (archetype, charge hint якщо Laser, stat groups). Build → новий weapon у backpack + auto-grant `2× MagazineSize` matching ammo. Hover на будь-який inventory item / module card → tooltip з композицією і stats. Inventory показує archetype labels ("Laser Pistol") замість generic "Weapon". 6 working archetypes (Ballistic × {Pistol, Rifle, Shotgun} + Laser × {Pistol, Rifle, Shotgun}). ~120 зелених тестів. Data-driven — усі stats з SO assets.

**Last work:** UX Pass 1 (4 passes, 4 commits): charge hint + disabled tooltip + workbench prompt; archetype labels у inventory + auto-grant ammo; universal tooltip system; Builder D&D rewrite.

### Де все живе

**Code:**
- Types: `Assets/Scripts/State/` (enums, structs, SO definitions, WeaponConfiguration)
- Systems: `Assets/Scripts/Systems/` (WeaponAssemblySystem, WeaponStatComposer, WeaponItemFactory, WeaponChargeResolver, WeaponSyncSystem, WeaponDisplayName, ShootingSystem, WeaponStateMachineSystem)
- Adapters: `Assets/Scripts/Adapters/` (ICoreDefinitionRegistry, DatabaseCoreDefinitionRegistry)
- Builder UI: `Assets/Scripts/View/UI/WeaponBuilder/` (presenter + window + Elements/{ModuleCardElement, ModuleSlotElement, BackpackItemElement})
- Tooltip system: `Assets/Scripts/View/UI/Tooltip/` (TooltipController, TooltipModel, Builders/{Item,Weapon,Module}TooltipBuilder)
- Scene interactable: `Assets/Scripts/View/WorkbenchView.cs`
- Editor: `Assets/Scripts/Editor/WeaponBuilderStubAssets.cs`, `Assets/Scripts/Editor/TooltipAssetsBootstrap.cs` (menu `Tools → Weapon Builder → Create Stub Assets`)

**Assets:**
- SOs: `Assets/Resources/WeaponBuilder/Payloads/{BallisticRound,LaserCharge}.asset` + `Deliveries/{SingleAction,Auto,Scatter}.asset` + `CoreDefinitionDatabase.asset` + `WeaponBuilderPanelSettings.asset`
- UXML/USS: `Assets/Resources/UI/WeaponBuilder/` (D&D layout) + `Assets/Resources/UI/Tooltip/` (tooltip overlay)

**Tests:** `Assets/Tests/EditMode/` (12 test files related — grep "Weapon|Core|Ammo|Armor|Tooltip")

### Як перевірити що все працює

1. Unity → **Test Runner → EditMode → Run All** → очікується ~120 зелених
2. Play mode → hideout scene → знайди Workbench → press E → modal відкривається з drag&drop palette
3. Або: **Window → Dev Cheats → "Toggle Weapon Builder"** — відкриває з будь-де
4. Drag Laser card → Payload slot, Auto card → Delivery slot → Build → equip → shoot → повинен бути charge-up затримка + laser projectile
5. Open inventory (Tab) → built weapon показується як "Laser Rifle", не "Weapon"
6. Hover на будь-який item у inventory → tooltip з композицією/stats

### Якщо assets відсутні (fresh clone)

`Tools → Weapon Builder → Create Stub Assets` (idempotent — створює 5 .asset файлів у Resources/WeaponBuilder/).

### Next work: planned execution sequence (revised 2026-04-27)

**Виконуємо у такому порядку:**

| # | Tier ID | Назва | Чому саме тут |
|---|---------|-------|---|
| 1 | **6** | Loot / Inventory integration | Активує core loop "raid → loot → build". Drag-from-backpack у Builder оживає. NEXT. |
| 2 | **8** | 3D Modular Visualization (NEW) | Player візуально розрізняє composition. Найбільший visual impact на playtest. |
| 3 | **3** | Content expansion (Foam, Rocket, Rotary, Swarm) | Повна 4×5 archetype matrix. |
| 4 | **4** | Rarity + Slot Compatibility | Progression hook + bot weapon migration. |
| 5 | **5** | Exotic Mods | Hook system + 5 exotic mods. Identity / "wow" factor. |
| 6 | **9** | VFX / SFX Language (NEW) | Per-archetype visual і audio мова. |
| 7 | **10** | Weapon Feel Polish (NEW) | Iterative playtest loop — final tuning. |

> **Note:** Tier numbers = stable IDs (для refs). Execution order ≠ tier number order — see [roadmap.md execution sequence](./roadmap.md#execution-sequence-поточний-план-виконання) for rationale.
>
> **Old Tier 7 (Polish bucket)** — deprecated, split into Tier 8/9/10 на 2026-04-27 щоб чесно tracking'увати окремі категорії робіт (3D meshes / VFX-SFX / feel tuning).

Детальна декомпозиція кожного — у [roadmap.md](./roadmap.md). Наступний tier декомпозується у конкретні T-*.NN (як робили для Tier 0b/1/2) коли беремось.

### Known gaps (to track)

- Bot weapons hardcoded у `BotConstants` — migration deferred до Tier 4 (per 2026-04-22 decision)
- `.cursor/rules/weapon-builder*.mdc` counterpart (CLAUDE.md §7 вимога) — не зроблено
- `docs/ai/weapons.md` застаріла (pre-migration Rifle/Shotgun/Pistol) — не оновлена
- Weapon mesh для FormFactor="Shotgun" (видалений разом з Shotgun item у 0b) — fallback на Weapon_Rifle prefab
- ~~Inventory UI показує "Weapon" DefinitionId замість archetype label~~ ✅ done у UX Pass 1 (`WeaponDisplayName.For` helper)
- Drag-from-backpack у Builder — навмисно read-only до Tier 6 (модулів-як-items ще немає)

### Key architectural decisions (швидкий reference)

Усі з rationale в [architecture.md](../architecture.md):

- **Q1** Composition + cached computed stats (не monolithic)
- **Q2** FiringPattern enum + internal dispatch у ShootingSystem (не Strategy per delivery)
- **Q6** Per-module-instance rarity, `StatsByTier` tables per module
- **Q7** Phased migration через compat layer (already removed у 0b); AmmoType на Payload
- **D1** 7 Payload stats + 13 Delivery stats, no overlap
- **D2** Abstract `PayloadCoreDefinition` + typed subclasses (heterogeneous payload stats)
- **D3** SO + central `CoreDefinitionDatabase` aggregator (не `Resources.LoadAll`)
- **D4** `readonly struct` з `[Serializable]` + `IEquatable<T>` для `*CoreInstance`
- **D6** On-equip + explicit Apply button trigger for re-assembly
- **D7** Ghost-weapon pattern (strict, no auto-repair) for invalid configs
- **D8** Pure template archetype label `"{payload.DisplayName} {delivery.FormFactor}"`
- **D9** Modal callable from anywhere (Workbench physical + DevCheats button)
- **D10** Infinite module supply у Tier 1 (loot integration → Tier 6)
- **D11** Single-screen UI Toolkit dropdowns + live preview
- **D12** New ItemState у backpack, magazine full, generic `"Weapon"` DefinitionId
- **D13** Physical Workbench scene object + InteractPressed key (E)
- **D14** Variant B charge-up: Laser charges перед кожним пострілом regardless of Delivery

---

> **Living doc.** Трекає відкриті питання, прийняті рішення і блокери по ходу роботи над Weapon Builder. Оновлюється часто.

---

## Current phase

**Pre-implementation / Design consolidation.**

Дизайн зафіксовано у v0.7 ([design.md](../design.md)). Архітектурні питання відкриті ([architecture.md](../architecture.md)). Імплементація ще не стартувала.

---

## Open questions

### Design
- [ ] **Slot structure — конкретні правила.** Скільки слотів, якого типу, яка сумісність? `design.md` фіксує принцип, але не правила. (Tier 4)
- [ ] **Banned combinations matrix.** Які P×D комбінації явно заборонені дизайном? (напр. чи можливе Adhesive Foam + Rotary?) (Tier 4)
- [ ] **Exotic Mod × Core сумісність.** Кожен Exotic працює з кожним Payload/Delivery, чи є обмеження? (Tier 5)
- [ ] **Rarity — конкретні множники.** На скільки відсотків Uncommon кращий за Common? (Tier 4 — заповнення `StatsByTier`)
- [ ] ~~Fist Delivery — single behavior чи кілька?~~ — виключено зі scope Weapon Builder
- [ ] **Laser Charge — поведінка зарядки.** Hold-to-charge з release? Auto-release при повному заряді? Overcharge можливий? (Tier 2)
- [ ] **Payload secondary effects — обов'язкові чи опційні?** (Tier 2-3 — на кожен payload)

### Architecture — Tier 0 блокуючі питання
Виявлені комплексним ревʼю 2026-04-19. Треба закрити ДО старту коду Tier 0.

- [x] ~~**D1.** Склад `WeaponStats` блоку~~ ✅ 8 Payload + 13 Delivery, без overlap
- [x] ~~**D2.** Stats structure для різнорідних Payloads~~ ✅ abstract base + typed subclass'и
- [x] ~~**D3.** ScriptableObject vs plain data для `*CoreDefinition`~~ ✅ SO (abstract base + subclass'и для Payload)
- [x] ~~**D4.** Value semantics: readonly struct vs class для `*CoreInstance`~~ ✅ `[Serializable] readonly struct`
- [x] ~~D5. ExoticMod без rarity — явно зафіксовано~~ ✅

### Architecture — Tier 1 блокуючі
- [ ] **D6.** Re-assembly triggers (коли запускається composition)
- [ ] **D7.** Invalid configuration handling (fallback strategy)
- [ ] **D8.** Archetype label system (lookup / template / hybrid)

### Architecture — Tier 3+ (високорівневі, не закриті)
Великі питання з [architecture.md](../architecture.md):
- [ ] **Q3.** Payload Core abstraction (IPayloadBehavior чи data-only)
- [ ] **Q4.** Slot structure / module compatibility (Tier 4)
- [ ] **Q5.** Exotic Mod hooks (event-driven vs strategies) (Tier 5)

### Architecture — housekeeping
- [ ] **D9.** RaidContext / ports integration для `*CoreDefinition` registry
- [ ] **D10.** Raid State Debugger update (CLAUDE.md §5.7)
- [ ] **D11.** DevCheats integration (rarity multipliers, spin-up times)
- [ ] **D12.** Docs sync `.cursor/rules/weapon-builder*.mdc` — після завершення планування

### Production
- [ ] **UI збірки на базі — mockups / wireframes.** Поки немає.
- [ ] **VFX / SFX scope per module.** Кожен Payload/Delivery/Exotic потребує свого feel — хто і коли це робить?
- [ ] **Tier 0 size estimation.** Чи ділити на 0a (data model) + 0b (migration)? Див. R1 у [architecture.md](../architecture.md#open-risks).

---

## Decisions log

Фіксуємо прийняті рішення з контекстом — щоб через місяць не переобговорювати те саме.

### 2026-05-01 — Paused, pivot to Gunplay epic

**Контекст:** після того як ми засіли planning Tier 8.x follow-ups + Tier 4a (bot migration) + Tier 9 (VFX/SFX) у polish track, став очевидним більший pivot. Goal "make existing 6 archetypes feel great" — це не Weapon Builder polish, це **gunplay polish overall**. Recoil, hit feedback, blood, ragdoll, decals, camera shake — все це cross-cutting concerns живуть на рівні гри, не feature-specific.

**Decision:** **Pause Weapon Builder roadmap.** Створено окремий epic [Better Feel Gunplay](../../gunplay/README.md) під який ми складаємо comprehensive list of polish items (categorized by impact tier). Weapon Builder специфічні follow-ups (Tier 8.x muzzle alignment, Tier 4a bot migration) waiting у roadmap — re-engage'аться у природний момент гри (e.g., bot migration може стати necessary якщо gunplay potlight reveals coherence issues з legacy bot loot).

**User quote:** "взагалі всі пункти офігенні! Давай тоді зробимо паузу в роботі з weapon builder і почнемо роботу над іншим epic - better feel gunplay."

**What's preserved:**
- Roadmap up to date — usable resumption point
- All Cluster A retirements landed (legacy player-facing weapon refs gone)
- Polish track sequence (Tier 8.x → 4a → 9 → 10) documented для коли re-engage
- Tier 3/4b/5 + Wave F still deferred sine die

**Re-engage criteria:** Gunplay epic phase A-B done + visible "feel great" landing на existing 2×3 archetypes. Then revisit чи окремі Weapon Builder polish items уже covered by gunplay work, чи треба окремо.

### 2026-05-01 — Strategic pivot: polish-first, content tracks deferred

**Контекст:** Tier 6 + Tier 8 closed core "raid → loot → build" loop + visible 2-module composition. Question came up: continue з Tier 3 (content expansion — Foam/Rocket/Rotary/Swarm) or shift focus до polish on existing 2×3 archetypes? User decided **polish-first.**

**User quote:** "tier 3 взагалі хочеться десь сильно потім робити, коли буде вже круто відчуватись те що є."

**New execution sequence:**

```
🎯 POLISH TRACK (next, in order):
  1. Tier 8.x follow-ups (formalized) — visual coherence after Wave B/C symmetric pivot
  2. Tier 4a (split) — bot weapon migration ONLY → closes Cluster B legacy debt
  3. Tier 9 — VFX/SFX language, scoped to current 2×3 archetypes
  4. Tier 10 — Weapon Feel iterative tuning loop

⏸ DEFERRED SINE DIE:
  • Tier 3 — content expansion
  • Tier 5 — exotic mods
  • Tier 4b — rarity values + slot compat (split з оригінального Tier 4)
  • Tier 8 Wave F — backpack composite icons (UI prereq)
```

**Tier 4 split rationale.** Original Tier 4 mixed 3 теми: rarity values + slot compat + bot weapon migration. Bot migration — pure legacy cleanup (independent of content design). Splitting allows shipping cleanup на polish track without committing to content/balance design (rarity values).

**Tier 9 scope-limited.** Original "need content to design VFX language for" applied if Tier 3 ran у parallel. Standalone VFX polish для existing 2 payloads × 3 deliveries ≠ blocked.

**Re-engage content tracks (3, 5, 4b, Wave F) коли:**
- Polish loop converged — playtest sessions кажуть "feels great", not "функціонально"
- Telemetry shows balanced 2×3 matrix
- UI track оновлений (для Wave F)
- Decision на content scope reset

**Roadmap updates landed:** [overview tier table](./roadmap.md#огляд-tiers) reordered, [execution sequence](./roadmap.md#execution-sequence-revised-2026-05-01--polish-first) rewritten, two new tier blocks ([Tier 8.x](./roadmap.md#tier-8x--tier-8-follow-ups-visual-coherence-pass) + [Tier 4a](./roadmap.md#tier-4a--bot-weapon-migration-split-from-tier-4)) authored, Tier 4 split into 4a/4b, Tier 3/5 explicit defer notices added.

### 2026-05-01 — Cluster A: legacy player-facing weapon refs retired

**Goal:** remove all references що spawn'ять legacy `Rifle`/`Pistol` items або `Ammo_Pistol`-family ammo from player-facing flows (starting loadout, dev cheats, ground items, loot tables, crafting). Compat layer лишається для bot drops + tests until Tier 4a (bot migration).

**Changes (1 commit):**
- `Systems/PlayerSpawnSystem.GiveStartingLoadout` — `WeaponItemFactory.SpawnItem("Rifle")` → `ItemState.CreateWeapon("Weapon", BallisticRound+Auto config)`. Starting weapon тепер native Builder weapon.
- `Session/RaidSession.SpawnTestGroundItems` — `("Rifle", pos, 1)` → Builder weapon ground item via `GroundItemState.CreateWeapon`. Confirms ground/inventory round-trip with `WeaponConfiguration`.
- `Editor/DevCheatsWindow.HideoutGiftItems` — replaced `("Rifle", 1) + ("Pistol", 1) + ("Ammo_Pistol", 36) + ("Ammo_Pistol_AP", 18)` з 5 modules (BallisticRound/LaserCharge/SingleAction/Auto/Scatter — 1× each) + retired Pistol-caliber ammo.
- `Constants/ItemGroups.WeaponsDrops` — now reuses `ContainerConstants.WeaponModuleDrops` (5 modules); `MixedDrops` interleaves modules + drops Ammo_Pistol; `AmmoDrops` = Rifle-only.
- `Constants/CraftConstants` — `ImprovisedRifle` recipe replaced з 5 module recipes (`BallisticRoundModule`, `LaserChargeModule`, `SingleActionModule`, `AutoModule`, `ScatterModule`) under `CraftCategory.Weapons`. `PistolAmmo` + `PistolAPAmmo` recipes retired.
- `Constants/ContainerConstants.AmmoBox` + `RandomLootBox` — `Ammo_Pistol` drop entries removed.

**What survives до Tier 4a:**
- `WeaponItemFactory.DefaultConfigFor` + `IsKnownWeaponDefinition` + `SpawnItem` — used by bot loot path
- `LootSystem.MapWeaponPrefabToDefinition/Ammo` — bot loot fallback
- `ItemDefinition.["Rifle"]` + `["Pistol"]` registry entries — bot loot creates these
- `Ammo_Pistol`/`_AP`/`_HP` registry entries — bot loot fallback + EditMode tests
- `[Obsolete] ItemDefinition.WeaponPrefabId` field — bot weapon prefab resolution

All marked у коді як Cluster B targets (Tier 4a) — clear ownership.

**Tests:** 434/434 зелені (no test changes needed — integration tests still validate compat layer).

**Player-facing outcome:**
- Starting weapon = native Builder weapon ("Ballistic Rifle" archetype label, configurable у Builder)
- Hideout stash gift bag = 5 modules (player can build any current archetype з нуля)
- Loot containers cleaned (no orphan Ammo_Pistol drops)
- Crafting offers module recipes — extends Tier 6 module-as-items loop

### 2026-05-01 — Tier 6 Wave F (G2) shipped + G7 deferred sine die

**Wave F (G2 loot economy) done — code-side.** Modules can now drop в raid containers, completing the "raid → loot → build" core loop without DevCheats.

**Implementation:**
- `Constants/ContainerConstants.cs`:
  - 5 module entries (`BallisticRound`, `LaserCharge`, `SingleAction`, `Auto`, `Scatter`) appended to `RandomLootBox.PossibleDrops` — uniform random pick → ~50% chance per drop slot is module
  - New `ModuleCache` ContainerType (`enum ContainerType.ModuleCache`, displayName "Module Cache", MinDrops=1 MaxDrops=2, module-only pool)
  - Shared `WeaponModuleDrops[]` static reused by ModuleCache pool — single source of truth для module loot composition
  - `ContainerType` enum extended; Registry entry added
- `Tests/EditMode/LootSystemTests.cs`: +3 tests — `ModuleCache_RegistryLookup_Succeeds`, `RandomLootBox_IncludesAllWeaponModules`, `CreateContainer_ModuleCache_DropsOnlyWeaponModules` (10-run sweep with deterministic seed)
- Test count: **434 passed** (was 431).

**Out of scope (per design):**
- **Scene placement** of `ModuleCache` spawn points у raid maps — manual user task via Unity Editor; `LootContainerSpawnPoint` dropdown auto-shows new enum value.
- **Bot drops** — Tier 4 (з bot weapon migration).
- **Per-module weighting** — Tier 4 (з rarity layer).

**G7 (initial loadout) deferred sine die.** Reason: DevCheats "Spawn All Modules" + Wave F loot economy cover testing/playtest needs. "Fresh save UX" доцільно полірувати разом з general onboarding pass (Tier 10 feel polish або earlier dedicated UX iteration).

**Tier 6 status:** ✅ done (Waves A/B/D/E/F shipped). Wave C deferred → Tier 4. G7 deferred sine die.

### 2026-04-30 — Tier 6 audit: D+E silently landed

**Контекст:** post-Tier-8 review of Tier 6 status revealed roadmap claims to be stale — code review показало що Wave D (G6 build cost) AND Wave E (G4 palette filter) уже implemented, just not reflected in docs.

**Findings (verified by `grep` + read of `WeaponBuilderPresenter.cs` + `ModuleCardElement.cs`):**

- ✅ **G6 (Build cost)** — `WeaponBuilderPresenter.TryBuild` (line 189+) consumes 1×payload + 1×delivery from backpack on success; `CanBuild` (line 114) gates on `HasModuleInBackpack`; `DisabledReason` (line 166) explains missing module to player. Comment explicitly tagged "Tier 6 G6".
- ✅ **G4 (Palette filter)** — `WeaponBuilderPresenter.IsModuleAvailable(id)` (line 277) + `ModuleCardElement.SetAvailable(bool)` (line 58) + `WeaponBuilderWindow.cs:566/568` calls SetAvailable on each card; USS classes `wb-card-unavailable` + `:hover` variant у `WeaponBuilderWindow.uss` lines 161-167.
- ❌ **G2 (Loot economy)** — confirmed not done: no module IDs у `ContainerConstants` / loot system.
- ❌ **G7 (Initial loadout)** — confirmed not done: `PlayerSpawnSystem.GiveStartingLoadout` (lines 71-92) only spawns weapon + ammo + grenade + medkit + bandage + helmet + armor; no modules.

**Tier 6 actual state:** ✅ Waves A/B/D/E done. ❌ Waves F (G2 loot) + G (G7 initial loadout) open.

**Decision:** No new code work — just sync roadmap/status/README docs to reflect reality. Updated:
- `roadmap.md` Tier 6 work item checkboxes (G1/G3/G4/G6/G8/G9/G10 → ✅)
- `roadmap.md` Tier 6 wave table (D/E "DONE — audited 2026-04-30", F = NEXT)
- `roadmap.md` overview tier table ("⏳ NEXT" → "🚧 partial — Waves A/B/D/E done; F/G open")
- `status.md` header (Tier 6 progress reflected)

**Why this drift happened:** Tier 6 D+E were delivered earlier (likely during UX Pass 1 closeout або immediately after Wave B foundation, 2026-04-28+) but doc updates were missed. Audit triggered by user question "здається ми там вже багато чого зробили — якщо так, онови доки".

**Lesson logged:** every Wave completion has to land roadmap checkbox + wave table status + overview table simultaneously, or status doc drifts.

### 2026-04-30 — Tier 8 Waves A-E complete + symmetric pivot

**Контекст:** після Wave A (pipeline refactor) + Wave B (initial asymmetric proof — delivery owns full weapon body, payload = small attachment), user pushed to **symmetric composition**: "не важливо які меші — головне реалізувати ідею збірки view зброї з двох головних модулей". PolygonApocalypse pack виявився містить modular parts (Mod_Body, Mod_Barrel, Mod_Stock, Mod_Handle, Mod_Loader, Mod_Attach) — switched approach mid-tier.

**Final composition model:**
- **Delivery prefab** (`Weapon_Pistol/Rifle/Shotgun.prefab`) carries: `DeliveryBody` (Mod_Body mesh) + `WeaponView` + `Animator` + `MuzzlePoint` + `RightHandGrip` + `PayloadMount` socket.
- **Payload prefab** (`Module_Payload_BallisticBarrel/LaserEmitter.prefab`) carries: wrapper GO + barrel/emitter mesh.
- Composition = delivery instantiated under WeaponPivot, payload spawned as child of PayloadMount socket on equip.

**Wave outcomes:**
- **Wave A** ✅ — `DeliveryCoreDefinition.WeaponPrefab` (GameObject ref); `WeaponSyncSystem` reads from SO; string-switch resolver видалений; `ItemDefinition.WeaponPrefabId` marked `[Obsolete]` (full removal у Tier 4 з bot migration).
- **Wave B** ✅ — `PayloadCoreDefinition.AttachmentPrefab`; `WeaponEntityState.PayloadPrefab`; `WeaponView._payloadMount` socket + `AttachPayload(GameObject)`; `CharacterBody.SwapWeaponModel(prefab, id, payloadPrefab)` overload extended.
- **Wave B-symmetric pivot** ✅ — replaced SM_Wep_AssaultRifle_01 → Mod_Body_05 + cube barrel → Mod_Barrel_01.
- **Wave C** ✅ — Pistol = Body_10 + Barrel_01 (Ballistic) / Barrel_15 (Laser); Shotgun = Body_15 + same; LaserEmitter prefab created; `LaserCharge.AttachmentPrefab` wired; Shotgun fallback gap closed.
- **Wave D** ✅ — Mecanim clips became stale after pivot (animation paths `Hand/Magazine` no longer exist); replaced з procedural recoil kick на `WeaponView` (positional impulse along -Z, ease-out-quad recovery scaled to fire interval). Payload's optional Animator runs independently per Unity defaults.
- **Wave E** ✅ — `Tools → Weapon Builder → Create Module Prefabs` editor utility — idempotent, auto-creates primitive prefabs for any SO without wired visual prefab + auto-wires references. Forward-compat for Tier 3 content.

**Test coverage:** 431 EditMode tests зелені (+2 Wave B propagation tests).

**Follow-ups (logged, not blocking):**
1. **Muzzle alignment for symmetric meshes.** Currently MuzzlePoint on delivery (V-Q3) — but barrel живе на payload, тому each payload has different barrel length → MuzzlePoint approximation. Окрема ітерація: чи move MuzzlePoint у payload prefab (`WeaponView` resolves dynamically post-`AttachPayload`).
2. **Reload/Equip/Unequip procedural motion** — Wave D landed only Fire kick. Inert clips fire silently. Tier 9 polish.
3. **Mecanim stale clip cleanup** — clips animate non-existent paths. Housekeeping → Tier 9.
4. **Per-prefab PayloadMount/MuzzlePoint tuning** — placeholder positions (e.g., `(0, 0.03, 0.40)`) set on око; manual Inspector adjustment per-archetype.

**Tier 8 status:** ✅ done (functionally). Waves A-E shipped end-to-end visible 2-module composition + drop-in path for new content. **Wave F (backpack composite icons) deferred sine die** — blocked on UI prereq (current uGUI `InventorySlotView`/`LootPopupView` не підтримує composite icons; чекаємо UI Toolkit / new inventory rendering track). Re-engage Wave F коли inventory rendering layer оновлений — until then Tier 8 closed.

**Doc updates:** README "Workflow: додавання нового модуля" section додає крок 4 — run utility — щоб contributors не забували.

### 2026-04-29 — Tier 8 architectural decisions + wave plan

**Контекст:** старт Tier 8 (3D Modular Visualization). До цього усі weapons виглядали однаково (один prefab per FormFactor) — підриває core promise "weapons are 2 modules". Перед стартом коду — комплексне ревʼю поточного pipeline + резолюція 7 архітектурних питань.

**Decomposition поточного pipeline'у:** `WeaponConfiguration → WeaponSyncSystem.ResolveWeaponPrefab` (string-switch по `DeliveryDef.FormFactor`) → `WeaponEntityState.PrefabId` → `CharacterBody.SwapWeaponModel(prefabId)` → `Resources.Load("Prefabs/Weapons/" + prefabId)` → instantiate під `_weaponPivot`. Лише 3 prefabs (Pistol/Rifle/Shotgun); Shotgun fallback на Rifle. Payload **взагалі не впливає на візуал**.

**Architectural decisions (V-Q1 to V-Q7):**

1. **V-Q1.** Modular runtime composition (B), не pre-built per-archetype (A). 4×5 + Exotic (×5) = scope explosion.
2. **V-Q2.** Animator на Delivery (Fire/Reload/Equip — per-mechanism). Payload може мати own optional animator для passive visual.
3. **V-Q3.** MuzzlePoint на Delivery (barrel exit position). Payload emitter prefab spawn'иться там runtime (Tier 9).
4. **V-Q4.** Direct `GameObject` reference на SO (не string path). Typesafe Inspector authoring.
5. **V-Q5.** `ItemDefinition.WeaponPrefabId` deprecated у Tier 8, видаляється у Tier 4 (разом з bot weapon migration).
6. **V-Q6.** Explicit `Transform` reference (PayloadMount) на Delivery prefab. Find-by-name fragile.
7. **V-Q7.** Backpack icons (старий V6 у roadmap) defer'ються у Wave F (last). Visual pipeline — пріоритет; icons не блокують differentiation у hand.

**Wave plan (A-F):**
- **A. Pipeline refactor (no art) ⭐ NEXT** — заміна string-id resolver на SO-driven prefab refs.
- **B. Payload attachment proof** — 1 archetype з реальною композицією (primitive shapes).
- **C. Cover 2×3 archetypes** — Ballistic/Laser × Pistol/Rifle/Scatter; Shotgun fallback видалений (closes Tier 0b memory gap).
- **D. Animator integration** — verify Fire/Reload/Equip незалежні від payload.
- **E. Forward-compat assets** — editor utility для drop-in нових modules (підгот для Tier 3).
- **F. Backpack icons** — composite Payload+Delivery sprites у inventory. **Deferred** після A-E.

**Effort estimate:** ~15-20h programmer-side. Detailed у [roadmap.md Tier 8](./roadmap.md#tier-8--3d-modular-visualization).

**Tier 6 status note:** Tier 6 Waves A+B complete. Решта (Wave D — build cost, E — palette filter, F — economy, G — initial state) лишаються відкритими — Tier 8 стартує як паралельний track. Architectural pivot Tier 8 (composition-based visual) пришвидшує core promise demonstration і не блокує Tier 6 economy waves які можна підняти потім.

### 2026-04-28 — Tier 6 Wave C deferred: cross-stack drag → Tier 4

**Decision:** Wave C (cross-stack drag bridge G5★) defer'нута з Tier 6 у Tier 4.

**Rationale:**
- Builder palette уже drag-source (Pass 4 D&D rewrite). Drag-from-inventory дублював би existing функціонал — player click'ом палітри селект'ить модуль, інвентар показує availability.
- **Unique value cross-stack drag — instance disambiguation** — виникає тільки коли rarity (Tier 4) робить 2× BallisticRound (Common vs Rare) different items. Без rarity всі instances однакові → palette достатньо disambiguate'ить.
- Wave D (G6 build cost) + Wave E (G4 palette filter) разом дають complete inventory loop без cross-stack drag: палітра показує grayed-out unavailable, Build consumes modules з backpack.
- Saves ~4-6h, redirected у G6/G4/G2/G7 які closing the loop more meaningfully.

**Tier 4 will pick this up alongside rarity work** — DragService implementation тоді matches the actual UX need.

### 2026-04-28 — Tier 6 Wave B complete: foundation ✅

G1 + G3 done.

- **G1**: 5 module ItemDefinitions у `ItemDefinition.BuildRegistry` (BallisticRound, LaserCharge, SingleAction, Auto, Scatter — all `MaxStackSize: 1`, `AllowedSlots: Backpack`). Ids match `PayloadCoreDefinition.Id` / `DeliveryCoreDefinition.Id` для майбутнього Wave D consume lookup.
- **G3**: DevCheats "Spawn Module" dropdown + button у Raid section + "Spawn All Modules" convenience button. `SpawnModuleIntoBackpack(id)` finds free slot, allocates EId, places `ItemState.Create`.
- **Bonus**: `Raid → Remove Save On Start` toggle (default ON) — Editor menu checkbox + EditorPrefs persistence + App.Initialize hook (`#if UNITY_EDITOR`). Fresh player every Play Mode entry by default; toggle off to test save persistence.
- **Inventory cleanup**: starting loadout simplified — лише single Ballistic Rifle (slot 0). Pistol + spare-Pistol + Ammo_Pistol прибрані з `GiveStartingLoadout`. Weapon variety тепер через Builder + loot.

**Effort actual:** ~1.5h (within estimate).

### 2026-04-28 — Tier 6 Wave A complete: side-by-side launch ✅

Workbench interact (E key) тепер відкриває **Builder modal на правому боці** + **uGUI inventory canvas на лівому**. Player бачить Familiar inventory UI (durability bars, weight, equipment slots, hotbar) поряд з Builder. Embedded backpack у Builder UI повністю видалений.

**Implementation summary:**
- `PlayerEntityState.BuilderTargetId` (EId) — replaces old `IsWeaponBuilderOpen` bool, parallel to `LootTargetId`/`CraftTargetId`. Sentinel value у `WeaponBuilderWindow` since Tier 6 doesn't track specific workbench yet.
- `WeaponBuilderWindow.uss` — backdrop `align-items: flex-end + padding-right: 32px` (right-anchor); transparent bg (no dim); `picking-mode="Ignore"` on UXML so pointer events pass through до uGUI underneath.
- `LootPopupView.OpenForBuilder()` — Builder mode variant: only `_playerPanel`, `_lootContainerParent` forced hidden.
- `InventoryUI.Update` — watches `BuilderTargetId`, `_openedByBuilder` flag для lifecycle tracking. Tab while Builder open → calls `WeaponBuilderWindow.Instance.Close()` (universal "close everything" key).

**Files modified:** PlayerEntityState, WeaponBuilderWindow (cs/uxml/uss), LootPopupView, InventoryUI. **Files deleted:** `BackpackItemElement.cs`.

**Runtime polish (caught у Editor playtest):**
- `picking-mode="Ignore"` on backdrop — без цього UI Toolkit panel consume'ив усі pointer events, inventory був не-interactive.
- Backdrop dim → transparent — side-by-side layout не потребує dim, він тільки obscure'ив inventory візуально.

**Effort actual:** ~5h (within 4-6h estimate).

### 2026-04-28 — Tier 6 architecture: side-by-side inventory + cross-stack drag bridge

**Контекст:** під час планування Tier 6 (loot/inventory) виникло концептуальне питання: чи правильно тримати backpack як embedded panel у Builder UI (поточний стан після UX Pass 1), чи відкривати uGUI inventory canvas alongside Builder як side-by-side layout (industry pattern: Diablo, Tarkov, Minecraft, PoE).

**Decision: side-by-side (approach B).** Поточний embedded backpack у Builder видаляється; uGUI inventory canvas відкривається при interact з Workbench, Builder shift'иться вправо.

**Чому:**
- Reuse existing inventory functionality (durability bars, weight, stack counts, equipment slots) — без duplicate rendering у `BackpackItemElement`.
- Less cluttered Builder — фокус на core job (palette + slots + preview).
- Familiar UX pattern для гравця.
- `BackpackItemElement` уже почав drift'ити від `InventorySlotView` (різні tooltip approaches) — divergence стане більше з часом.

**Cost:**
- Cross-stack drag bridge (uGUI → UI Toolkit) — нова engineering інвестиція (~4-6h). Записано як **highest priority всередині Tier 6** — це load-bearing piece що reuse'ається у будь-яких future inventory ↔ UI Toolkit interactions.
- Layout coordination (Builder shift on Workbench open + close coord).
- Visual style mismatch (uGUI old style vs UI Toolkit new dark cards) — тимчасово, до Tier 9 polish pass.

**Tier 6 scope revised:** G1-G10 (було G1-G8). Embedded backpack видаляється явно. Detailed waves у [roadmap.md](./roadmap.md#tier-6--loot--inventory-integration).

### 2026-04-28 — Tier 6 architectural decisions (5 confirmed)

Підтверджено перед стартом Tier 6 коду:

1. **Module → ItemDefinition mapping:** hardcode 5 entries у `ItemDefinition.BuildRegistry` (BallisticRound, LaserCharge, SingleAction, Auto, Scatter). Auto-gen відкладений у Tier 4 (rarity змусить refactor anyway).
2. **Module stackability:** non-stackable (`MaxStackSize: 1`). Forward-compat з Tier 4 rarity (різні tier = різні items).
3. **Build cost:** 1×payload + 1×delivery. Multi-quantity рішення відкладене у Tier 10 (feel/balance).
4. **Palette filter behavior:** grayed-out for unavailable modules (player бачить full possibility space + усвідомлює що шукати), не hidden.
5. **Bot module drops:** out of Tier 6 scope. Bot loot config refactor зв'язаний з bot weapon migration (Tier 4). Container drops + DevCheats покривають Tier 6 playtest потреби.

### 2026-04-27 — Roadmap restructure: Tier 7 split, execution reorder

**Контекст:** після UX Pass 1 closeout стало видно що original Tier 7 ("Polish") був неявним bucket'ом для 3 зовсім різних категорій робіт — 3D meshes, VFX/SFX, feel tuning. Окремо — Tier 6 (loot integration) дає найбільший player-facing impact (активує core loop), тому має йти раніше за content tiers.

**Зміни:**
1. **Tier 7 deprecated** — розділений на 3 окремі tier'и:
   - **Tier 8** — 3D Modular Visualization (modular weapon meshes per payload + delivery)
   - **Tier 9** — VFX / SFX Language (per-archetype visual і audio мова)
   - **Tier 10** — Weapon Feel Polish (iterative playtest tuning)
2. **Execution reorder:** Tier 6 (loot) виконується **NEXT** замість після Tier 5. Залежність "Tier 6 needs Tier 4 rarity" знята — initial drops все Common, rarity layer'иться у Tier 4 пізніше.
3. **Tier 8 виконується після Tier 6**, а не у кінці (як було Tier 7) — щоб visual differentiation з'явилась швидше.
4. **Tier numbers стабільні як ID** для refs у коді/тестах. Execution order — окремо.

**Причини:**
- 3D mesh work не "polish" — це structural feature gap (підриває "weapons are 2 modules" promise)
- VFX і Feel — два різних типи робіт (visual language vs gameplay tuning), кожен значний scope
- Tier 6 first → активує real loot loop і drag-from-backpack у Builder, що найбільший transformation feature

**Documentation updated:**
- `roadmap.md` — нові tier secії 8/9/10, deprecated Tier 7 note, expanded Tier 6 з work items + revised dependencies, "Execution sequence" секція на початку
- `README.md` — tier progress table reordered, "Що ще треба зробити" rewritten в execution order
- `status.md` — "Next work" заміна на planned execution sequence з rationale

### 2026-04-27 — UX Pass 1 closeout: A.02 + A.03 + A.04 + C.05 done

Дозакрив 4 items з оригінального doc'у які раніше були помічені як "deferred":
- **A.02** Module descriptions — `Systems/WeaponModuleFlavor.cs` (5 hardcoded entries) + `TooltipModel` extended з optional `Description` field + `ModuleTooltipBuilder` surfaces it. Description рендериться у tooltip між subtitle і stats sections (UXML/USS додано `tt-description` style).
- **A.03** Stats grouping у Builder preview — refactored `RefreshPreview` на 3 групи: **Combat** (Damage / Headshot / Penetration) / **Cadence** (Charge if Laser / Fire Interval / Magazine / Reload) / **Pattern** (Projectile Speed / Projectiles per Shot). New USS class `wb-stat-group-heading` (18px bold blue), no emoji icons (skipped як cosmetic noise).
- **A.04** Archetype flavor sub-line — `Systems/WeaponArchetypeFlavor.cs` (6 hardcoded entries для Tier 1-2 archetypes). `WeaponBuilderPresenter.PreviewArchetypeFlavor` getter. New label у preview UXML між archetype і chargeHint (`wb-archetype-flavor`).
- **C.05** Modal fade-in/out — USS `transition-property: opacity; transition-duration: 0.15s` на `.wb-window` + `.wb-window-fading` class. Open: mount with class → next-frame remove. Close: add class → schedule.Execute(160ms) sets display=None. Generation-counter (`_fadeGen`) protects against rapid Open/Close races.

**B.05** (ghost weapon visual badge) видалений з плану — `[Broken Weapon]` text marker достатньо; visual badge edge-case полишимо до Tier 6 коли module loot drops зможуть створити reality для ghosted state.

**+10 нових тестів:** WeaponArchetypeFlavorTests (8), WeaponBuilderPresenter Archetype flavor (4), TooltipBuilders Module description (3). Загалом ~130 зелених.

**Files NEW (3):** WeaponArchetypeFlavor.cs, WeaponModuleFlavor.cs, WeaponArchetypeFlavorTests.cs.
**Files MODIFIED:** Presenter, Window (UXML/USS/cs), TooltipModel, TooltipController, ModuleTooltipBuilder, TooltipOverlay (UXML/USS), 2 існуючих test files.

### 2026-04-27 — UX Pass 1 complete ✅

Фокусний UX polish над foundation. 4 sub-passes, 4 commits. Не torkнув content (Tier 3+) і rarity/loot.

**Sub-passes:**
1. **Quick wins** — charge hint у preview ("⚡ Requires charge — 1.0s before each shot"), disabled Build button tooltip з reason, Workbench prompt rename ("Weapon Workbench · Press E"). Presenter exposed `PreviewRequiresCharge`, `PreviewChargeTime`, `DisabledReason`.
2. **Inventory + ammo** — `Systems/WeaponDisplayName.For(item, registry)` helper у 2 callsites (`InventorySlotView`, `EquipmentSlotView`); built weapons показуються як "Ballistic Pistol" / "Laser Rifle" замість "Weapon". Auto-grant `2× MagazineSize` matching ammo після TryBuild — fix для Laser-trap (mag full, reserve empty).
3. **Universal tooltip system** — `TooltipController` (view-singleton як `WeaponBuilderWindow.Instance`) + `TooltipModel` data + 3 builders (`Item`, `Weapon`, `Module`). Cross-stack: uGUI inventory hover → UI Toolkit overlay panel (sortOrder=1000). Y-flip helper `ShowFromPanel` для UI Toolkit callers. Naming: `TooltipModel` (не `TooltipPayload` — конфлікт з weapon-builder term "payload").
4. **Builder D&D rewrite** — UXML/USS rewrite, presenter unchanged. Layout: palette (cards) + slots + preview + read-only backpack context. Drag mechanic: PointerDown→capture→ghost→geometry overlap test проти `worldBound` of slots→drop. Type filtering, click fallback, click suppression after drag. Backpack source: `App.Instance.Player.Inventory.Backpack` (read-only до Tier 6).

**Архітектурні рішення:**
- View-layer singletons (`*.Instance`) дозволені — повторюємо `WeaponBuilderWindow.Instance` precedent. CLAUDE.md §3.12 ("never add new singletons") трактується як "не додавай global gameplay state" — view service locators tolerated.
- `App.Instance.Tooltip` accessor НЕ додавали — лишаємось у letter §3.2.
- Stack vs uGUI tradeoff для D&D: вибрали залишитись у UI Toolkit для Builder. Аргумент — UXML/USS markup редагується агентом напряму (Edit/Write), uGUI Canvas потребує Inspector wiring що сильно сповільнює iteration. Cross-stack drag (uGUI ↔ UI Toolkit) відкладено до Tier 6 коли воно реально потрібне.
- Tooltip naming: `TooltipModel`, не `TooltipPayload` — щоб не плутати з weapon "Payload Core" terminology.

**Нові артефакти (reusable):**
- `Systems/WeaponDisplayName.For(item, registry)` — будь-який inventory rendering
- `View/UI/Tooltip/TooltipController` + `TooltipModel` + 3 builders
- `View/UI/WeaponBuilder/Elements/{ModuleCardElement, ModuleSlotElement, BackpackItemElement}` — UI Toolkit primitives
- `docs/ai/ui-styling.md` (+ `.cursor/rules` mirror) — Tier A/B sizing + sort orders + color palette

**Tests:** +28 (WeaponDisplayName 7, Tooltip builders 14, Presenter extensions 10, end-to-end fix 1 retro).

**Out-of-scope (свідомо deferred):** edit-existing-weapon mode, build feedback toast, fade-in/out animations, rarity tint, decision support callouts, drag-from-backpack у Builder.

**Див.:** [`ux-improvements.md`](./ux-improvements.md), [`docs/ai/ui-styling.md`](../../ui-styling.md).

### 2026-04-17 — v0.7 approved, Hidden Budget removed
**Було:** Hidden Budget як невидимий ліміт проти "все найкраще одразу".
**Стало:** Slot structure / module compatibility — явні структурні обмеження через слоти і правила сумісності.
**Причина:** Явні правила чесніші для гравця і простіші для імплементації, ніж балансування невидимої budget-математики.

### 2026-04-17 — Doc structure: folder per feature
**Рішення:** Weapon Builder живе у `docs/ai/weapon-builder/` з окремими файлами під дизайн, архітектуру, план.
**Причина:** Фіча занадто велика для одного файлу. Розділення концептуальних (живуть довго) і планових (живуть час реалізації) доків.

### 2026-04-20 — D6 / D7 / D8 resolved (Tier 1 blockers closed)

**D6 — Re-assembly triggers:** Варіант B.
- On equip (auto) + on explicit "Apply" button (manual, Tier 4 UI)
- `WeaponAssemblySystem.Assemble` — окрема system, викликається з обох місць
- Runtime state persistence: `AmmoInMagazine` у `WeaponConfiguration` (persistent), решта runtime полів — скидаються при re-assembly
- Tier 0b/1 реалізує on-equip path only; Apply button — Tier 4

**D7 — Invalid configuration handling:** Варіант C (ghost weapon), strict — без auto-repair.
- `WeaponAssemblySystem.TryAssemble(WeaponConfiguration, out WeaponEntityState) → bool`
- Будь-який missing definition (Payload/Delivery/Exotic) → `false`, log + `WeaponAssemblyFailed` event
- **Без auto-repair Exotic** — strict C: broken exotic ламає всю збірку, гравець явно виправляє в Builder
- Invalid item лишається в inventory як ghost (не видаляється), equip fails clearly, player unarmed
- Tier 0b/1: немає broken-UI; Tier 4 — ⚠️ icon + tooltip + Salvage/Repair

**D8 — Archetype labels:** Варіант A (pure template, no baseline strip).
- `PayloadCoreDefinition.DisplayName` + `DeliveryCoreDefinition.FormFactor` — нові поля SO
- Template: `"{payload.DisplayName} {delivery.FormFactor}"`
- Examples: "Ballistic Pistol", "Ballistic Rifle", "Laser Pistol", "Foam Shotgun", "Rocket Launcher"
- Legacy Rifle/Pistol після міграції → "Ballistic Rifle"/"Ballistic Pistol"
- Exotic NOT у label (окремий UI element)
- Override-table — deferred to Tier 5

**Наслідки для Tier 0b:**
- +2 fields на SOs (DisplayName, FormFactor) — editor stub script треба оновити
- +1 system class `WeaponAssemblySystem` з `TryAssemble`
- +1 helper `WeaponArchetypeLabel.Compose`
- +1 event type `WeaponAssemblyFailed` у RaidEventBuffer

**Див.:** [architecture.md §D6, D7, D8](../architecture.md)

### 2026-04-23 — Tier 1 complete ✅
**Vertical slice landed.** Гравець може:
- Підійти до Workbench у Hideout → press E → Builder opens
- Select Payload + Delivery у 2 dropdowns → live preview stats + archetype label
- Click Build → new ItemState (з WeaponConfiguration) у backpack
- Equip → стріляє як pistol/rifle згідно з Delivery FormFactor
- Alt route: DevCheats "Toggle Weapon Builder" button — відкриває Builder з будь-де

**Зроблено:**
- Cluster A — Presenter + state, 14 unit tests
- Cluster B — UI Toolkit modal (UXML + USS + runtime Window), 2x upsized layout
- Cluster C — Workbench scene interactable (InteractPressed input, proximity prompt)
- Cluster D — DevCheats toggle button
- Cluster E — AppBootstrap integration + end-to-end tests (5)

**Архітектурно:**
- Presenter — pure C#, testable без Unity
- UI Toolkit runtime pattern (UIDocument + PanelSettings bootstrap) — slope для future UI
- Generic "Weapon" ItemDefinition — identity у WeaponConfiguration, prefab derived з Delivery FormFactor
- `PlayerEntityState.IsWeaponBuilderOpen` auto-gates gameplay input через existing `IsInMenu`

**Test coverage after Tier 1:** ~75 total зелених (Tier 0a 24 + Tier 0b 29 + Tier 1 22).

**Unlocked для Tier 2:**
- Додати Laser Charge payload + charge-up state machine
- Додати Auto Delivery handler у ShootingSystem
- Додати Scatter Delivery handler
- 6 working архетипів (2 payloads × 3 deliveries)

### 2026-04-22 — D9-D14 resolved (Tier 1 design decisions)

**D9 — UI location:** окремий modal screen, callable з будь-якого контексту (hideout + raid). Primary trigger — physical workbench у hideout scene. Secondary — DevCheats shortcut для debug/raid.

**D10 — Module supply:** infinite, all-unlocked у Tier 1. Loot integration — Tier 6.

**D11 — UI layout:** single screen з 3 dropdowns (Payload/Delivery/Exotic), live preview (stats + archetype label), Build/Cancel buttons.

**D12 — Build result:** новий ItemState у перший free backpack slot, `AmmoInMagazine = MagazineSize`. Existing items не зачіпаються. `DefinitionId = "Weapon"` (generic — identity у WeaponConfiguration).

**D13 — Entry point:** physical Workbench scene object у hideout + Interact key. `WorkbenchView` MonoBehaviour + prompt UI + DevCheats global hotkey для dev testing.

**D14 — Tier 1 E2E scope:** Ballistic + Single-Action, 10-step demo approved. Deferred to 2+: Laser/Auto/Scatter/Exotic/Rarity UI/loot integration/repair UI.

**Див.:** [architecture.md §D9-D14](../architecture.md)

### 2026-04-22 — Tier 0b complete ✅
Всі 18 задач 6 кластерів закриті. Legacy factories + compat layer + Shotgun повністю видалені. WeaponEntityState — pure data з composition + cached Stats. ItemState/GroundItemState тепер carry WeaponConfiguration. 53 tests (24 Tier 0a + 22 unit + 7 integration). Tier 1 розблокований.

### 2026-04-22 — Bot weapons deferred to Tier 4
**Decision:** Bot weapons (BotSpawnSystem + BotConstants) залишаються **повністю hardcoded** для Tier 0b і 1. Вони не проходять через assembly pipeline, їхні Stats populate напряму з BotConstants raw fields.

**Перенесено в Tier 4** (разом з rarity):
- Видалити всі hardcoded stat fields з `BotConstants.BotTypeConfig`
- Додати `WeaponConfiguration WeaponConfiguration` до `BotTypeConfig`
- `BotSpawnSystem` має отримати registry з context, викликати `WeaponAssemblySystem.TryAssemble`
- Bot variety приходитиме з **rarity-per-bot** (Scav=Common, Boss=Epic/Legendary) + різні delivery/payload combinations
- Balance може "попливти" — це ок, зафіксується в Tier 4 balance pass

**Чому не зараз:**
- Без rarity всі боти мали б однакові Stats (Common) → втрата variety
- Без Rotary/Swarm heavy bots не мають адекватного delivery
- Scope creep у Cluster C (вже breaking change)
- Weapon Builder навмисно player-facing, bot path — окремий

### 2026-04-20 — Tier 0a complete ✅
**Виконано:**
- Всі нові types у `Assets/Scripts/State/`: enums (RarityTier, FiringPattern), stats structs (CommonPayloadStats, DeliveryStats, WeaponStats, 3 payload-specific), readonly struct instances (PayloadCoreInstance, DeliveryCoreInstance, ExoticModInstance), WeaponConfiguration
- SO definitions: abstract `PayloadCoreDefinition` + 4 subclass'і (Ballistic/Laser/Rocket/Foam), concrete `DeliveryCoreDefinition`, `ExoticModDefinition`, central `CoreDefinitionDatabase`
- Port `ICoreDefinitionRegistry` + реалізація `DatabaseCoreDefinitionRegistry` у `Assets/Scripts/Adapters/`
- Інтеграція: `RaidContext.CoreDefinitions`, RaidSession ctor параметр, App завантажує Database через `Resources.Load<>`
- Editor utility `WeaponBuilderStubAssets` (menu: `Tools → Weapon Builder → Create Stub Assets`) — idempotent authoring з чисел pre-migration factories
- Stub assets створені: BallisticRound/SingleAction/Auto + CoreDefinitionDatabase (Common tier заповнений)
- 24 unit tests зелені (CoreInstance equality + Registry lookup)

**Нульовий runtime impact:** існуючі weapons (Rifle/Pistol/Shotgun) працюють як раніше, нова система поряд і не використовується — Tier 0b її підключить.

**Next:** декомпозиція Tier 0b у конкретні task'и.

### 2026-04-20 — D3 amendment: Database SO over Resources.LoadAll
**Рішення:** Міняємо D3 loading mechanism з `Resources.LoadAll<T>` на central `CoreDefinitionDatabase` SO (за патерном `QuestDatabase`).

**Чому:**
- Explicit — явний список assets у Database Inspector, не автомагічний scan
- No Resources build bloat для всіх assets (тільки Database у Resources)
- Консистентно з existing project pattern (`QuestDefinition` + `QuestDatabase`)
- Simpler hot-reload: Database rebuild індексу замість сканування filesystem

**Наслідки:**
- `ICoreDefinitionRegistry` wrap'ить Database і будує BuildIndex dictionaries
- Cluster D (stub assets) додає `CoreDefinitionDatabase.asset` як central aggregator
- Registry реалізація у Cluster C стає простішою (один SO → indices, замість Resources scan)

**Див.:** [architecture.md §D3](../architecture.md)

### 2026-04-20 — R1 decision: Tier 0 split into 0a + 0b
**Контекст:** Tier 0 work items зросли до ~14 після закриття D1-D4. Розмір ризикує великим diff, довгим review, конфліктами merge.

**Рішення:** Розділити на два sub-tiers:
- **Tier 0a — Data Model Foundation.** Всі нові types (enums, structs, SOs, registry port), stub assets. Старі weapons (Rifle/Shotgun/Pistol) працюють БЕЗ змін. Безпечно мержиться.
- **Tier 0b — Migration.** `WeaponEntityState` refactor, `WeaponAssemblySystem`, compat layer для Rifle/Pistol, Shotgun повне видалення, ShootingSystem rewrite з dispatch, read-site refactor, Debugger update.

**Наслідки:**
- 0a розблоковує **паралельну роботу** — дизайнер наповнює SO assets поки програміст працює над 0b
- 0b стартує тільки після merge 0a і passing тестів
- Gate між 0a і 0b — zero progress на 0b, доки 0a не зелений

**Див.:** [roadmap.md — Tier 0a / 0b](./roadmap.md)

### 2026-04-20 — D3 + D4 resolved: SO authoring + struct instances
**D3 — `*CoreDefinition` як ScriptableObject:**
- Abstract base + typed subclass'и (для Payload), plain SO (для Delivery/Exotic поки що)
- Assets у `Assets/Resources/WeaponBuilder/{Payloads,Deliveries,Exotics}/`
- `StatsByTier` серіалізується як `CommonPayloadStats[]` з індексом = `(int)RarityTier`
- Loading через новий port `ICoreDefinitionRegistry` — `Resources.LoadAll<T>(path)` на startup
- Consistency з DevCheats і ItemDefinition патернами

**D4 — `*CoreInstance` як readonly struct:**
- `[Serializable] readonly struct` з public readonly fields + `IEquatable<T>`
- Value semantics: zero GC, immutable, structural equality
- `ExoticModInstance?` — nullable value type у composition
- Instance тримає тільки `DefinitionId`, lookup definition через registry (CLAUDE.md rule 6 ✅)
- Extension method `Definition<T>()` опційно для handler зручності

**Наслідки для Tier 0 коду:**
- Створюємо `ICoreDefinitionRegistry` port + його реалізацію на Resources.LoadAll
- 3 abstract SO bases + 4 Payload subclass'и + per-asset authoring workflow
- Test infrastructure: `ScriptableObject.CreateInstance<T>()` + builder helpers
- Inspector edit stats per tier → масив індексований rarity enum

**Див.:** [architecture.md §Tier 0 remaining details — D3, D4](../architecture.md)

### 2026-04-20 — D1 + D2 resolved: WeaponStats composition
**D1 — Розподіл полів по джерелах:**
- 8 stats з Payload: Damage, Speed, Lifetime, HeadshotMult, Penetration, ArmorDamage, BleedChance, AmmoType
- 13 stats з Delivery: FireInterval, ProjectilesPerShot, SpreadAngle, ConeHalfAngle, BodyRotationSpeed, AimFollowSharpness, 3×Recoil, Equip/Unequip Time, MagazineSize, ReloadTime
- Нуль overlap — кожне поле з одного джерела
- AmmoType — identifier на `PayloadCoreDefinition`, не в `WeaponStats`
- Ammo modifiers складаються окремо в `ShootingSystem` на fire (як зараз) — третій канал

**D2 — Stats structure для різнорідних Payloads:**
- `PayloadCoreDefinition` стає abstract base + 4 subclass'и (`Ballistic/Laser/Rocket/Foam`)
- Common stats (8 полів) у base `StatsByTier: Map<RarityTier, CommonPayloadStats>`
- Payload-specific (ChargeTime, ExplosionRadius, Slow/Stick) — у subclass `SpecificByTier`
- Handlers касят definition до свого типу (type-safe, explicit, Unity SO-friendly)
- Delivery поки без subclass'ів — всі 5 мають однаковий shape

**Наслідки:**
- `WeaponStats` — один flat struct з 21 common-полем (8 Payload + 13 Delivery)
- `ShootingSystem` handlers для Laser/Rocket/Foam робитимуть cast на свій typed definition
- Exotic модифікатори для specific-полів застосовуються в handler'і (не в `Compose`)
- `Compose` pipeline простий і детермінований

**Див.:** [architecture.md §Tier 0 remaining details — D1, D2](../architecture.md)

### 2026-04-19 — Architecture review (Tier 0 readiness check)
**Контекст:** Після закриття Q1/Q2/Q6/Q7 зробили комплексне ревʼю стану архітектури. Мета — впевнитись, що Tier 0 готовий до імплементації.

**Висновок:** 4 великі питання закриті, але є **12 subsidiary-деталей**, які треба опрацювати:
- 4 must-do перед Tier 0 кодом (D1-D4): склад Stats, Stats structure для різних payloads, SO vs plain data, struct vs class
- 3 should-do перед Tier 1 (D6-D8): re-assembly triggers, invalid config handling, archetype labels
- 5 tracked/housekeeping (D9-D12): RaidContext integration, Debugger update, DevCheats, docs sync

**Consistency checks:**
- Всі hard rules design.md мапляться на архітектуру ✅
- ExoticMod без rarity — зафіксовано явно в §1 і в hard rules mapping ✅
- CLAUDE.md compliance — stateless static systems ✅, IDs only in state ✅, малі diff'и ⚠️ (Tier 0 великий — R1), docs sync ❌ (D12 очікує)

**Ризики:**
- R1: Tier 0 scope підріс з 5 до 12 work items — розглянути split на 0a+0b
- R2: Laser Charge state machine — рішення Tier 2
- R3: Multi-projectile × custom payload — перевірка Tier 2

**Див.:** [architecture.md §Tier 0 remaining details](../architecture.md)

### 2026-04-19 — Q7 resolved: Factory migration + scope cut
**Рішення:**
- Фазована міграція: Rifle/Pistol переводяться на новий pipeline через тимчасовий compat layer (Tier 0-2)
- **Shotgun видаляється з гри** — зменшує legacy surface, не треба Scatter міграції в Tier 2
- До кінця Tier 2: factories (`CreateRifle` / `CreatePistol`) повністю видалені, compat layer прибраний
- `CreateShotgun` видаляється одразу в Tier 0
- Scatter Delivery лишається в scope Weapon Builder як нова поведінка (Tier 2), але не міграція існуючого
- **AmmoType прив'язується до Payload Core** (не до weapon / delivery)
  - Ballistic → Ammo_Rifle (Rifle і Pistol ділять його після міграції)
  - Laser → energy cell, Rocket → rocket ammo, Foam → foam canister

**Наслідки:**
- Compat layer ~5 рядків у `WeaponSyncSystem`, явно позначений як temporary
- Існуючі системи (shooting range, armor tests) завжди мають працюючу зброю
- Ammo_Shotgun видаляється разом з Shotgun
- Один code path, нуль dual maintenance

**Див.:** [architecture.md §7](../architecture.md)

### 2026-04-19 — Q6 resolved: Rarity data model
**Рішення:**
- **Q6.1** Rarity per module instance (не per weapon) — відповідає hard rule з design.md
- **Q6.2** Enum `RarityTier { Common, Uncommon, Rare, Epic, Legendary }`
- **Q6.3** Per-module stat tables: `StatsByTier: Map<RarityTier, Stats>` всередині кожного `*CoreDefinition`

**Наслідки:**
- `PayloadCoreInstance` / `DeliveryCoreInstance` у `WeaponConfiguration` = `{ DefinitionId, Rarity }`
- `*CoreDefinition` одразу має `StatsByTier` (у Tier 0 заповнений тільки Common, решта — в Tier 4)
- Composition pipeline Tier 0 вже знає як обирати stats по rarity
- Ammo modifiers лишаються окремим каналом (складаються в ShootingSystem на fire)
- Tier 4 = заповнення таблиць + UI + balance, не переписування

**Див.:** [architecture.md §6](../architecture.md)

### 2026-04-19 — Q2 resolved: Delivery Core abstraction
**Рішення:** FiringPattern enum у `DeliveryCoreDefinition` + внутрішній dispatch у `ShootingSystem` + state machine extension.

- Handlers — static методи `ShootingSystem` (не окремі класи / strategies)
- Параметричні deliveries (Single/Auto/Scatter) шарять helper-код
- Rotary/Swarm мають власні handlers + нові фази state machine (`SpinningUp`, `SpinningDown`, `VolleyActive`)
- Runtime state (SpinLevel, VolleyShotsRemaining) — у `WeaponEntityState`, handlers stateless
- Fist Delivery виключена з scope Weapon Builder (окрема melee система)

**Scope:**
- Enum + dispatch закладаємо в **Tier 0** навіть для 1 pattern (Single)
- Нові фази state machine — в **Tier 3** коли з'являться Rotary/Swarm

**Також зафіксовано як guiding principle:** немає пріоритету зберігати існуючу архітектуру. Якщо переписування дає очевидні переваги — ріжемо без зволікань.

**Див.:** [architecture.md §2](../architecture.md)

### 2026-04-18 — Q1 resolved: composed weapon representation
**Рішення:**
- **Q1.1** Composition + cached computed stats (explicit modules + `Stats` block + runtime fields окремо)
- **Q1.2** Cached on assembly; runtime state окремо; ammo modifiers окремо (як зараз у `ShootingSystem`)
- **Q1.3** `WeaponConfiguration` живе в `InventoryItem` (persistent), `WeaponEntityState` створюється `WeaponSyncSystem` при equip — розширюємо існуючий patterns

**Наслідки:**
- `WeaponEntityState` треба refactor: розділити identity / Stats / runtime
- Всі read sites `weapon.FieldName` → `weapon.Stats.FieldName` (механічна правка)
- `InventoryItem` schema розширюється на `WeaponConfiguration`
- `WeaponSyncSystem` ускладнюється: замість `definitionId → factory` тепер `WeaponConfiguration → assembly pipeline`
- Гравець зможе тримати кілька зібраних збірок в інвентарі — це безпосередньо вирішує design problem "немає причини тримати кілька збірок"

**Див.:** [architecture.md §1](../architecture.md)

### 2026-04-18 — Tier-based roadmap approved
**Рішення:** Реалізація структурована по 8 tiers (0-7). Fist Delivery і Typed Attachments — поза scope роадмапи. Tier 0-2 плануються детально, Tier 3-7 — high-level outlines, деталізуються в міру наближення.
**Причина:** Фіча масивна, без tier gating ризик розповзання scope. Архітектурні питання прив'язані до tiers, де вони вперше стають блокерами, а не вирішуються всі одразу.
**Див.:** [roadmap.md](./roadmap.md)

---

## Blockers

*Нічого не блокує на даний момент. Коли з'явиться — додаємо сюди з контекстом і owner'ом.*

---

## Next actions (after pause)

Foundation (Tiers 0a/0b/1/2) + UX Pass 1 завершені (2026-04-27). Усі immediate action items закриті:

- [x] ~~Пройти Tier 0 архітектурні питання Q1-Q7~~ ✅
- [x] ~~Закрити D1-D14 subsidiary details~~ ✅
- [x] ~~Tier 0a (foundation)~~ ✅ committed `03e07b9` (2026-04-20)
- [x] ~~Tier 0b (migration)~~ ✅ complete (2026-04-22)
- [x] ~~Tier 1 (vertical slice)~~ ✅ complete (2026-04-23)
- [x] ~~Tier 2 (core breadth)~~ ✅ complete (2026-04-23)
- [x] ~~UX Pass 1 (clarity + inventory + tooltip + D&D)~~ ✅ complete (2026-04-27)

**Closed since pause:**
- ✅ Tier 4a — bot weapon migration (2026-05-04). All bots (Scav/PMC/Boss/Targets/KillFeel*) спавняться з `WeaponConfiguration` (Payload + Delivery), pass через WeaponSyncSystem.BuildWeaponForItem. Bot projectiles тепер з повним stat set (HeadshotMultiplier, Penetration, ArmorDamage, BleedChance). Bot loot drops actual weapon з current ammo state. `ItemDefinition.WeaponPrefabId` field видалено; legacy `CharacterBody.SwapWeaponModel(string)` overload видалено; `LootSystem.MapWeaponPrefabToDefinition/Ammo` lookups знесено. 434/434 tests passing. See decision log entry below.

**Remaining backlog (when ready to resume):**
- Tier 8.x — visual coherence pass (muzzle alignment, pivot tuning across 6 archetypes)
- Tier 9 — VFX/SFX language (cross-cuts з gunplay FX brief — можна координувати з art track)
- Tier 10 — Weapon Feel Polish (cross-cuts з gunplay polish epic — більшість вже зроблено)
- Tier 3 / 5 — content expansion + Exotic mods, defer sine die

Якщо беремо tier — steps:

Якщо беремо tier — steps:
1. Прочитати декомпозицію потрібного tier у [roadmap.md](./roadmap.md)
2. Розписати у `tasks.md` конкретні T-N.NN задачі (як робили для 0b/1/2)
3. Код по кластерах з чекбоксами

---

## Decisions log (post-pause)

### 2026-05-05 — Tier 8.x* shipped: full asset architecture rebuild

**Context:** Original Tier 8.x (visual coherence pass — muzzle alignment, animator cleanup, socket tuning) escalated into а full architectural pivot після playtest analysis. Original assumption was "fix MuzzlePoint position bugs"; реальна проблема виявилась що **payload prefab role був перевернутий** — payload had barrel mesh, delivery had body. Counterintuitive: "delivery delivers bullets" means barrel should be ON delivery, not payload. User-driven design call to invert.

**New asset architecture:**
- **Payload prefab** = weapon BASE (handle, receiver, magazine — held у hand). Owns Animator, WeaponView component, RightHandGrip (IK target inside KickGroup), DeliverySocket (where barrel mounts), KickGroup (recoil mesh container).
- **Delivery prefab** = BARREL insert (короткий для Pistol, longer для Rifle/Shotgun). Owns mesh + MuzzlePoint child. No MonoBehaviours.
- 5 prefabs total: 2 payloads (BallisticRound, LaserCharge) × 3 deliveries (SingleAction, Auto, Scatter).

**Code renames (no legacy aliases):**
- `PayloadCoreDefinition._attachmentPrefab` → `_basePrefab` (semantic role inverted)
- `DeliveryCoreDefinition._weaponPrefab` → `_barrelPrefab`
- `WeaponEntityState.WeaponPrefab` → `BasePrefab`; `PayloadPrefab` → `BarrelPrefab`
- `WeaponView._deliveryBody` → `_recoilKickTarget`
- `WeaponView.AttachPayload(...)` → `AttachDelivery(GameObject barrelPrefab)` — instantiates barrel as child of `_deliverySocket`, resolves `_resolvedMuzzlePoint` через `FindDeepChild("MuzzlePoint")`
- `CharacterBody.SwapWeaponModel(GameObject prefab, ...)` → `SwapWeaponModel(GameObject basePrefab, GameObject barrelPrefab, string idForTracking)` — instantiates payload root, calls `weaponView.AttachDelivery(barrelPrefab)`
- `CharacterBody.SwapWeaponModel(string)` overload deleted (no Resources.Load fallback)

**Prefab generation:**
- `WeaponBuilderModulePrefabsUtility.cs` rewritten для new architecture. Creates payload з {Animator, WeaponView, KickGroup → {PayloadBaseMesh, DeliverySocket, RightHandGrip}, root-level just structure}. Delivery з {DeliveryBarrelMesh, MuzzlePoint at projectile spawn height}. Persistent .mat assets под `Resources/Prefabs/Modules/Materials/` (5 placeholder materials, gunmetal/cool-blue tint).
- Animator wires `Resources/Animation/Weapon_Base.controller` automatically (clips animate root euler angles → work без per-prefab override controllers).
- MuzzlePoint Y synced з `DevCheats.Config.Parallax.ProjectileSpawnHeight` so visual flash + bullet origin align.

**Bug fixes after manual playtest:**
- Stale prefabs (utility ran з cached old code) — wipe + regenerate with recompile checkpoint
- Magenta materials (orphan `new Material(...)` references) — saved as persistent .mat assets
- Animator controller missing → Equip/Unequip silent — wired Weapon_Base.controller у utility
- WeaponView._animator field unwired → PlayClip no-op — wired через SerializedObject у utility
- RightHandGrip outside KickGroup → hand stayed static during weapon recoil → moved INTO KickGroup so IK target kicks з weapon
- WeaponPivot sibling of skeleton → weapon hung у воздусі коли character ragdoll'иться → **Option A weapon drop**: new `ViewCheatsWeaponDropSection` + `RagdollPresenter.TryDropWeapon` reparents weapon to `[WeaponDropPool]`, adds Rigidbody + collider + impulse, despawns с ragdoll lifetime

**Legacy deletions (no compat shim):**
- `Resources/Prefabs/Weapons/` directory — Weapon_Pistol/Rifle/Shotgun.prefab gone
- `Resources/Prefabs/Modules/Module_Payload_BallisticBarrel.prefab` (old role)
- `Resources/Prefabs/Modules/Module_Payload_LaserEmitter.prefab` (old role)
- `Resources/Animation/_Pistol/_Rifle/_Shotgun/` directories with override controllers

**Test infrastructure:**
- New `WeaponPrefabStructureTests.cs` (22 cases) — validates prefab hierarchy, WeaponView serialized refs wired, Animator controller present, MuzzlePoint Y aligned з ProjectileSpawnHeight (±0.1m tolerance), SO `BasePrefab`/`BarrelPrefab` references valid + point до prefabs з required children
- `WeaponBuilderTestFactory.MakeStubWeaponPrefab` → split into `MakeStubBasePrefab` + `MakeStubBarrelPrefab`
- 455/455 tests passing

**Original Tier 8.x sub-items resolution:**
- 8x.1 (muzzle to payload) — replaced by full architecture refactor; muzzle тепер lives на delivery prefab з dynamic resolution post-attach
- 8x.2 (procedural reload/equip) — not needed; Weapon_Base.controller clips animate root euler angles via empty paths, work з new structure
- 8x.3 (strip animator overrides) — done as part of legacy cleanup
- 8x.4 (PayloadMount Inspector tuning) — N/A; PayloadMount socket replaced з DeliverySocket on payload (architectural shift)

**Decoupling gameplay/visual muzzle decision:**
- User concern: barrel-length variation per weapon → inconsistent wall-flush behavior
- Analysis: real bug exists, but marginal у current 6 archetypes (~25cm max barrel diff). Top-down camera doesn't show barrel obstruction → predictability vs realism trade not critical для arcade-ish extraction tone.
- Decision: **NOT decouple зараз**. Keep coupled (visual muzzle = gameplay muzzle). Revisit якщо weapon library expands sharply, OR camera shifts perspective, OR playtests reveal frequent "WTF moments" з збройного wall-flush.

**Effort:** ~6h migration + ~2h bug fix iteration. Net delta: +1 architectural test file (22 cases), +1 ViewCheats section (WeaponDrop), 5 regenerated prefabs, 5 placeholder materials, ~-200 LOC (legacy paths + override controllers + obsolete fields).

### 2026-05-04 — Tier 4a shipped: bot weapon migration to Builder pipeline

**Context:** Bots used legacy `WeaponPrefabId = "Weapon_Rifle"` + 6 hardcoded combat stat fields у `BotTypeConfig` (FireInterval, ProjectileSpeed/Damage/Lifetime, ProjectilesPerShot, SpreadAngle). `BotSpawnSystem` constructed `WeaponEntityState` manually — bypassed Builder composition. Bot projectiles missing Penetration / ArmorDamage / BleedChance / HeadshotMultiplier — pre-migration bot shots завжди absorbed by player armor regardless of damage tier.

**Migration scope:**
- `BotConstants.BotTypeConfig` — replaced `WeaponPrefabId` + 6 stat fields з single `WeaponConfiguration WeaponConfig`. 3 weapon presets: `RifleWeapon` (Auto), `PistolWeapon` (SingleAction), `ShotgunWeapon` (Scatter). 20 bot configs migrated.
- `BotSpawnSystem.SpawnBot` — bot weapon goes through `WeaponSyncSystem.BuildWeaponForItem` (transient ItemState wraps WeaponConfiguration). Falls back на `App.Instance.CoreDefinitions` if registry not passed (matches PlayerSpawnSystem pattern).
- `BotCombatSystem.ProcessFire` — passes full Builder-derived stats до projectile create.
- `BotView.Initialize` — accepts `WeaponEntityState` (not string id); visual goes through `Delivery._weaponPrefab` + payload mount, як player.
- `BotPresenter.SpawnView` — looks up bot from session.RaidState to get composed weapon.
- `LootSystem.CreateLootable(bot)` — drops bot's actual `WeaponConfiguration` з current ammo state. Ammo derived from `PayloadDefinition.AmmoType`.
- `WeaponSyncSystem.BuildWeaponForItem` — removed `#pragma warning disable CS0618` legacy fallback на `ItemDefinition.WeaponPrefabId`.
- `ItemDefinition.WeaponPrefabId` — field видалено повністю. `Rifle`/`Pistol` entries залишаються як preset labels для loot drops.
- `CharacterBody.SwapWeaponModel(string)` — legacy `Resources.Load<GameObject>("Prefabs/Weapons/" + id)` overload видалено. Single Builder path: `SwapWeaponModel(GameObject prefab, string idForTracking, GameObject payload)`.
- `RaidSession` — 24 `BotSpawnSystem.SpawnBot` callsites threaded `_coreDefinitions`.

**Tests:** 434/434 passing. `EditModeTestsUtils.BuildDefaultCoreRegistry` додано Scatter delivery + realistic FireInterval/MagazineSize у test deliveries (Auto 0.2s/30, SingleAction 0.4s/12, Scatter 0.6s/5). `LootSystemTests.CreateBot` builds weapon via Builder. `BotSpawnSystemTests.SpawnBot_CreatesWeaponFromConfig` updated assertions (now checks `DeliveryDefinition.Id == "Scatter"` для Boss).

**Beneficial gameplay impacts:**
- Bot shots мають правильний armor pen interaction (BasePenetration from Payload).
- Bot headshot multiplier працює.
- Bots with bleed-causing payloads → bleed effect applied.
- Boss bot використовує Scatter delivery → 7 pellets, 30° spread (composition-driven, не hardcoded).
- Loot drop preserves bot's actual weapon config (Pistol drops Pistol + Pistol ammo, не hardcoded "Rifle" mapping).

---

## Related docs

- [README.md](../README.md)
- [design.md](../design.md)
- [architecture.md](../architecture.md)
