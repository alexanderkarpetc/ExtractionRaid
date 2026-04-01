# Inventory, Items, and Related Systems

Technical reference for inventory structure, item definitions, equipment, crafting, loot, status effects, healing, stamina, and quests.

---

## 1. Inventory Structure

`InventoryState` holds all player (or lootable) items across four slot categories.

| Slot Category | Count | Field | Notes |
|---|---|---|---|
| Weapon Slots | 2 | `WeaponSlots[0..1]` | Only items with `ItemSlotType.Weapon` |
| Helmet Slot | 1 | `HelmetSlot` | Only `ItemSlotType.Helmet` |
| Body Armor Slot | 1 | `BodyArmorSlot` | Only `ItemSlotType.BodyArmor` |
| Backpack | 20 | `Backpack[0..19]` | Any item with `ItemSlotType.Backpack` |
| Quick Slot Bindings | 7 | `QuickSlotBindings[0..6]` | Indices into Backpack, -1 = unbound |

**Constants**: `WeaponSlotCount = 2`, `BackpackSize = 20`, `QuickSlotCount = 7`, `QuickSlotKeyOffset = 3`.

**Slot Addressing**: `InventorySlotRef(SlotType, Index)` — a value type used by all move/transfer operations. `SlotType` enum: `Weapon`, `Helmet`, `BodyArmor`, `Backpack`. Converts to `ItemSlotType` flags via `ToItemSlotType()` for AllowedSlots validation.

---

## 2. Item Definitions — Full Registry

`ItemDefinition.Registry` is a static dictionary built on first access. Each item has `AllowedSlots` flags controlling where it can be placed.

### Weapons

| Id | DisplayName | AllowedSlots | MaxStack | Notes |
|---|---|---|---|---|
| `Rifle` | Rifle | Weapon, Backpack | 1 | |
| `Shotgun` | Shotgun | Weapon, Backpack | 1 | |
| `Pistol` | Pistol | Weapon, Backpack | 1 | |

### Armor

| Id | DisplayName | AllowedSlots | ArmorPoints | MaxDurability | ArmorPrefabId |
|---|---|---|---|---|---|
| `Helmet_Basic` | Basic Helmet | Helmet, Backpack | 30 | 100 | `Helmet_Basic` |
| `Armor_Basic` | Basic Armor | BodyArmor, Backpack | 40 | 120 | `Armor_Basic` |

### Consumables

| Id | DisplayName | MaxStack | Notes |
|---|---|---|---|
| `Medkit` | Medkit | 200 | Continuous heal, stack = HP pool |
| `Advanced_Medkit` | Advanced Medkit | 1 | |
| `Bandage` | Bandage | 1 | Cures/downgrades bleed |
| `Grenade` | Grenade | 1 | |

### Standard Ammo

| Id | DisplayName | MaxStack | AmmoType | Penetration | ArmorDamage | BleedChance |
|---|---|---|---|---|---|---|
| `Ammo_Rifle` | Rifle Ammo | 60 | Ammo_Rifle | 10 | 5 | 0 |
| `Ammo_Shotgun` | Shotgun Ammo | 20 | Ammo_Shotgun | 8 | 4 | 0 |
| `Ammo_Pistol` | Pistol Ammo | 36 | Ammo_Pistol | 12 | 6 | 0 |

### AP Ammo (Armor-Piercing)

| Id | DisplayName | MaxStack | AmmoType | Penetration | ArmorDamage |
|---|---|---|---|---|---|
| `Ammo_Pistol_AP` | Pistol AP Ammo | 36 | Ammo_Pistol_AP | 30 | 7 |
| `Ammo_Rifle_AP` | Rifle AP Ammo | 60 | Ammo_Rifle_AP | 35 | 8 |

### HP Ammo (Hollow Point -- high bleed, no penetration)

| Id | DisplayName | MaxStack | AmmoType | Pen | ArmorDmg | BleedChance |
|---|---|---|---|---|---|---|
| `Ammo_Rifle_HP` | Rifle HP Ammo | 60 | Ammo_Rifle_HP | 0 | 0 | 0.30 |
| `Ammo_Shotgun_HP` | Shotgun HP Ammo | 20 | Ammo_Shotgun_HP | 0 | 0 | 0.08 (per pellet, ~44%/shot) |
| `Ammo_Pistol_HP` | Pistol HP Ammo | 36 | Ammo_Pistol_HP | 0 | 0 | 0.25 |

### Crafting Materials (Common)

| Id | DisplayName | MaxStack |
|---|---|---|
| `Adhesive` | Adhesive | 20 |
| `Metal_Parts` | Metal Parts | 30 |
| `Mechanical_Parts` | Mechanical Parts | 20 |
| `Electronics` | Electronics | 15 |
| `Chemicals` | Chemicals | 20 |
| `Cloth` | Cloth | 30 |
| `Gunpowder` | Gunpowder | 30 |
| `Plastic` | Plastic | 20 |
| `Glass` | Glass | 15 |
| `Rubber` | Rubber | 15 |
| `Springs` | Springs | 15 |

### Crafting Materials (Rare)

| Id | DisplayName | MaxStack |
|---|---|---|
| `Military_Components` | Military Components | 5 |
| `Energy_Core` | Energy Core | 3 |

### Weapon Mods

All weapon mods: Backpack-only, MaxStack 1, no combat stats.

| Id | DisplayName |
|---|---|
| `Basic_Scope` | Basic Scope |
| `Advanced_Scope` | Advanced Scope |
| `Long_Barrel` | Long Barrel |
| `Short_Barrel` | Short Barrel |
| `Suppressor` | Suppressor |
| `Compensator` | Compensator |
| `Extended_Mag` | Extended Mag |
| `Fast_Reload_Mag` | Fast Reload Mag |
| `Recoil_Grip` | Recoil Grip |
| `Stabilized_Stock` | Stabilized Stock |
| `AP_Barrel` | Armor-Piercing Barrel |
| `Overclock_Receiver` | Overclock Receiver |

---

## 3. Item State

`ItemState` represents a single item instance:

| Field | Type | Notes |
|---|---|---|
| `Id` | EId | Unique entity ID |
| `DefinitionId` | string | Key into `ItemDefinition.Registry` |
| `StackCount` | int | Default 1; stackables can hold up to `MaxStackSize` |
| `CurrentDurability` | float | -1 = use definition defaults on first equip |
| `MaxDurability` | float | -1 = use definition defaults |

`HasCustomDurability` returns true when `CurrentDurability >= 0` (i.e., durability was set from combat).

---

## 4. Item Operations (InventorySystem)

**Pickup range**: `PickUpRange = 3f`

### TryPickUp (ground item -> backpack)

Two-phase stacking for stackable items:
1. **Phase 1**: Fill existing partial stacks of the same `DefinitionId` up to `MaxStackSize`.
2. **Phase 2**: Overflow remaining count into free backpack slots (creating new ItemState per slot).
3. If nothing was picked up (no space at all), returns false.
4. Non-stackable items go to the first free backpack slot.

Removes the ground item from `state.GroundItems` and fires `GroundItemDespawned`.

### TryDrop (inventory slot -> ground)

Removes item from any slot (weapon, equipment, backpack) and creates a `GroundItemState` at `dropPosition`. Preserves `StackCount`. Fires `GroundItemSpawned`.

### TryMove (within same inventory)

Swaps two slots. Validates:
- Source item exists.
- Target slot's `ItemSlotType` is in source item's `AllowedSlots`.
- If target slot has an item, the source slot type must be in the target item's `AllowedSlots` (bidirectional swap validation).

### FindNearestGroundItem

Linear scan of `state.GroundItems`, returns closest within `PickUpRange`.

### Helper Methods

| Method | Purpose |
|---|---|
| `FindFirstMedkitSlot(inv)` | First backpack index with DefinitionId == "Medkit" |
| `FindFirstBandageSlot(inv)` | First backpack index with DefinitionId == "Bandage" |
| `CountGrenades(inv)` | Count of grenade items in backpack |
| `ConsumeOneGrenade(inv)` | Removes first grenade, nulls the slot |

---

## 5. Equipment System

`EquipmentSystem` synchronizes `InventoryState` helmet/body armor slots to `RaidState.ArmorMap`.

### WriteBackDurability(state, entityId, inventory)

Copies `ArmorMap[entityId].Helmet/BodyArmor.CurrentDurability` back to the corresponding `ItemState`. Called **before** `SyncArmorFromInventory` to preserve combat damage on the item.

### SyncArmorFromInventory(state, entityId, inventory)

Reads `HelmetSlot` and `BodyArmorSlot` from inventory, creates `ArmorState` entries:
- If item has `HasCustomDurability` (from previous combat or loot), uses those values.
- Otherwise, creates fresh armor from `ItemDefinition.ArmorPoints` and `MaxDurability`.
- If both slots are empty, removes the entity from `ArmorMap`.

---

## 6. Quick Slots

`QuickSlotSystem` manages 7 quick-slot bindings (keys 3-9 via `QuickSlotKeyOffset = 3`).

Each binding is an index into `Backpack[]`. The system:

1. **ClearStaleBindings**: Every tick, if the bound backpack slot is null, resets binding to -1.
2. **Activation**: On `QuickSlotPressed`, if player is not rolling and hands are not busy, sets `player.ActiveQuickSlot` to the pressed slot index. `player.QuickSlotHeld` tracks hold state.
3. **Deactivation**: When the held key is released (input.QuickSlotHeld != activeSlot), clears active slot.

**API**:
- `GetActiveDefinitionId(player, inventory)` -- returns the `DefinitionId` of the currently held quick slot item (or null).
- `GetActiveBoundSlot(player, inventory)` -- returns the backpack index of the active quick slot item (or -1).

---

## 7. Loot System

`LootSystem` handles container creation, corpse loot, interactable detection, and cross-inventory transfer.

**Range**: `LootRange = 3f`

### Container Creation

`CreateContainer(state, config, position, events)`:
- Rolls `Random.Range(config.MinDrops, config.MaxDrops + 1)` items.
- Each drop: random entry from `config.PossibleDrops`, random count clamped to `MaxStackSize`.
- Items placed sequentially into a new `InventoryState.Backpack`.
- Creates `LootableContainerState` with `isContainer = true`.

### Corpse Loot

`CreateLootable(state, bot, config, events)`:
- Maps bot weapon prefab to item definition (e.g., `Weapon_Rifle` -> `Rifle`).
- Adds matching ammo (up to 30 or MaxStackSize).
- Adds remaining medkits and grenades from `bot.Blackboard`.
- **Armor loot**: preserves combat durability from `ArmorMap` -- broken armor is excluded.
- Creates `LootableContainerState` with `isContainer = false`.

### Interactable Detection

`FindNearestInteractable(state, playerPosition, facingDirection)` scans all interactable types and scores by distance + facing direction dot product:
- `score = distance * (1 - 0.5 * dot)` (closer + more centered = lower score)

**InteractableType** enum: `None`, `Lootable`, `GroundItem`, `Workbench`, `DeployPoint`, `Npc`.

### Transfer (cross-inventory)

`TryTransfer(fromInv, fromSlot, toInv, toSlot)`:
- Same swap logic as `TryMove` but between two different `InventoryState` instances.
- Validates `AllowedSlots` in both directions.

### Container Types

| TypeId | DisplayName | Drops | Possible Loot |
|---|---|---|---|
| `MedContainer` | Medical Supplies | 2-4 | Medkit(1), Bandage(1) |
| `AmmoBox` | Ammo Box | 2-4 | Ammo_Rifle(10-40), Ammo_Shotgun(4-14), Ammo_Pistol(8-24) |
| `RandomLootBox` | Loot Box | 2-4 | Medkit(1), Bandage(1), Grenade(1), Ammo_Rifle(10-30), Ammo_Shotgun(4-10), Ammo_Pistol(8-18) |

### State Types

**LootableContainerState**: `Id`, `Position`, `TypeId`, `Inventory` (full InventoryState), `IsContainer` (true = world container, false = corpse).

**GroundItemState**: `Id`, `DefinitionId`, `Position`, `StackCount`. Lightweight -- no full inventory, just a single item on the ground.

---

## 8. Crafting

### Recipe Structure

`CraftRecipe`: `RecipeId`, `DisplayName`, `Description`, `Category`, `ResultItemId`, `ResultCount`, `Ingredients[]`.

`CraftIngredient`: `DefinitionId` + `Count`.

`CraftCategory` enum: `Meds`, `Weapons`, `Ammo`, `WeaponMods`.

### Crafting Flow (CraftingSystem)

1. `CanCraft(inv, recipe)` -- checks all ingredients exist in backpack with sufficient counts, plus at least one free slot.
2. `TryCraft(state, recipeId)` -- looks up recipe in `CraftConstants`, validates, consumes ingredients, creates result item.
3. **ConsumeIngredient**: iterates backpack, drains stacks (removes slot if fully consumed, decrements otherwise).

### All Recipes

#### Meds

| RecipeId | Result | Count | Ingredients |
|---|---|---|---|
| `Bandage` | Bandage | 1 | Cloth x2, Adhesive x1 |
| `FieldMedkit` | Medkit | 1 | Cloth x3, Chemicals x2, Adhesive x2, Plastic x1 |
| `AdvancedMedkit` | Advanced_Medkit | 1 | Cloth x4, Chemicals x4, Adhesive x3, Electronics x1 |

#### Weapons

| RecipeId | Result | Count | Ingredients |
|---|---|---|---|
| `ImprovisedRifle` | Rifle | 1 | Metal_Parts x7, Mechanical_Parts x3, Adhesive x2 |
| `PumpShotgun` | Shotgun | 1 | Metal_Parts x5, Mechanical_Parts x4, Adhesive x2, Springs x2 |

#### Ammo

| RecipeId | Result | Count | Ingredients |
|---|---|---|---|
| `PistolAmmo` | Ammo_Pistol | 8 | Gunpowder x1, Metal_Parts x1 |
| `PistolAPAmmo` | Ammo_Pistol_AP | 8 | Gunpowder x1, Metal_Parts x1, Military_Components x1 |
| `RifleAmmo` | Ammo_Rifle | 5 | Gunpowder x2, Metal_Parts x2 |
| `RifleAPAmmo` | Ammo_Rifle_AP | 5 | Gunpowder x2, Metal_Parts x2, Military_Components x1 |

#### Weapon Mods

| RecipeId | Result | Ingredients |
|---|---|---|
| `BasicScope` | Basic_Scope | Glass x2, Metal_Parts x1 |
| `AdvancedScope` | Advanced_Scope | Glass x2, Electronics x2, Metal_Parts x1 |
| `LongBarrel` | Long_Barrel | Metal_Parts x2, Mechanical_Parts x1 |
| `ShortBarrel` | Short_Barrel | Metal_Parts x2, Adhesive x1 |
| `Suppressor` | Suppressor | Metal_Parts x2, Cloth x1, Adhesive x1 |
| `Compensator` | Compensator | Metal_Parts x2, Mechanical_Parts x1 |
| `ExtendedMag` | Extended_Mag | Metal_Parts x2, Springs x2 |
| `FastReloadMag` | Fast_Reload_Mag | Metal_Parts x2, Mechanical_Parts x1 |
| `RecoilGrip` | Recoil_Grip | Rubber x2, Metal_Parts x1 |
| `StabilizedStock` | Stabilized_Stock | Metal_Parts x2, Adhesive x1 |
| `APBarrel` | AP_Barrel | Metal_Parts x3, Military_Components x1 |
| `OverclockReceiver` | Overclock_Receiver | Electronics x2, Mechanical_Parts x2 |

---

## 9. Status Effects

### Types

Currently only `StatusEffectType.Bleeding`.

### StatusEffectInstance

| Field | Type | Notes |
|---|---|---|
| `Type` | StatusEffectType | |
| `Level` | int | 1 = light (L1), 2 = heavy (L2) |
| `AppliedTime` | float | `state.ElapsedTime` at creation |
| `LastTickTime` | float | Updated each tick |

### Bleed Mechanics

- **Apply**: If no bleed exists, creates L1. If L1 exists, upgrades to L2. L2 is max.
- **Tick**: Every `BleedTickInterval` (1s), deals `BleedL1DamagePerTick` (3) or `BleedL2DamagePerTick` (6).
- **Downgrade**: L2 -> L1, or L1 -> removed.
- **Remove**: Deletes the effect entirely.
- **DevCheats.ForceBleedPlayer**: Debug toggle to apply bleed to player.

### Bleed Constants

| Constant | Value |
|---|---|
| `BleedL1DamagePerTick` | 3 HP |
| `BleedL2DamagePerTick` | 6 HP |
| `BleedTickInterval` | 1 second |
| `BandageUseTime` | 3 seconds |

---

## 10. Healing

### Bandage (BandageSystem)

**Purpose**: Cure or downgrade bleeding.

**Flow**:
1. Player holds bandage via quick slot (`QuickSlotHeld` + `DefinitionId == "Bandage"`).
2. Cannot start if: rolling, hands busy, no bleed active.
3. Sets `player.IsUsingBandage = true`, records `BandageUseStartTime` and `ActiveBandageSlot`.
4. After `BandageUseTime` (3s) elapses: calls `DowngradeBleed` (L2 -> L1, or L1 -> removed).
5. Consumes the bandage (nulls the backpack slot).
6. **Interruption**: releasing the key, dying, rolling, or bleed being removed externally stops the bandage.

### Medkit (MedkitSystem)

**Purpose**: Continuous HP restoration.

**Flow**:
1. Player holds medkit via quick slot (`QuickSlotHeld` + `DefinitionId == "Medkit"`).
2. Cannot start if: rolling, hands busy, HP is full.
3. Sets `player.IsUsingMedkit = true`, records start time.
4. **Delay phase**: `UseDelay` (2s) before healing begins (`MedkitHealingActive = false`).
5. **Healing phase**: Heals `HealPerSecond` (15) HP/s. Each integer point of healing drains 1 from `StackCount`.
6. Uses fractional accumulator (`MedkitHealFraction`) for sub-integer rates.
7. Stops when: key released, player dies, medkit stack depleted, HP reaches max.

### Med Constants

| Constant | Value |
|---|---|
| `MedConstants.UseDelay` | 2 seconds |
| `MedConstants.HealPerSecond` | 15 HP/s |
| `MedConstants.TotalHealAmount` | 200 HP (informational; actual limit is stack count) |

---

## 11. Stamina

`StaminaSystem` manages sprint resource.

**Flow**:
1. Sprint requires: `SprintPressed` + moving + `Stamina > 0` + not rolling + not hands busy + not ADS.
2. While sprinting: drains `SprintDrainRate` (20/s), records `LastSprintStopTime`.
3. After stopping: waits `RegenDelay` (1s), then regens `RegenRate` (15/s) up to `MaxStamina`.

### Stamina Constants

| Constant | Value |
|---|---|
| `MaxStamina` | 100 |
| `SprintDrainRate` | 20/s |
| `RegenRate` | 15/s |
| `RegenDelay` | 1 second |
| `SprintSpeedMultiplier` | 1.6x |

---

## 12. Quests

### Data Model

**QuestDefinition** (ScriptableObject):
- `Id`, `DisplayName`, `Description`
- `RequiredLevel`, `NpcId` (which NPC offers it)
- `Tasks` (list of `QuestTask` subclasses)
- `Rewards` (list of `QuestReward { ItemId, Count }`)

**QuestDatabase** (ScriptableObject): list of `QuestDatabaseEntry` (Quest + `RequiredQuestIds[]` for prerequisite chains). Indexed by quest ID.

**QuestProgressState**: runtime tracking, `Dictionary<string, QuestProgress>`.

**QuestStatus** enum: `NotStarted`, `Active`, `Completed`, `Failed`.

### Task Types

| Type | Class | Key Fields |
|---|---|---|
| FindAndTransfer | `FindAndTransferTask` | `QuestItemId` |
| KillEnemy | `KillEnemyTask` | `EnemyTypeId`, `HeadshotsOnly` |
| FindPlace | `FindPlaceTask` | `PlaceId` |
| ProvideSupply | `ProvideSupplyTask` | `ItemId` |
| Extract | `ExtractTask` | `LevelId` |
| Craft | `CraftTask` | `ItemId` |

All tasks share: `Description`, `RequiredCount`, `InOneRaid` (must complete in single raid).

### Quest Flow (QuestSystem)

1. **GetAvailableQuests**: Filters by NPC, status == NotStarted, requirements met (level + prerequisite quests completed).
2. **TryAccept**: Sets status to Active, creates TaskProgress entries.
3. **IncrementTask**: Increments `CurrentCount` on a specific task index.
4. **TryFulfillTasks**: Debug/shortcut -- maxes out all task progress (quest stays Active until NPC claim).
5. **TryCompleteAndGrantRewards**: Validates space for rewards, grants items (stacking-aware), sets Completed.
6. **CanFitRewards**: Checks existing partial stacks + free slots against reward requirements.

---

## 13. Key Files

| File | Purpose |
|---|---|
| `Assets/Scripts/State/InventoryState.cs` | Slot structure, backpack/weapon/equipment arrays |
| `Assets/Scripts/State/InventorySlotRef.cs` | Slot addressing (SlotType + Index) |
| `Assets/Scripts/State/ItemState.cs` | Item instance (id, definition, stack, durability) |
| `Assets/Scripts/State/ItemDefinition.cs` | Full item registry with all stats |
| `Assets/Scripts/State/GroundItemState.cs` | Dropped item on ground |
| `Assets/Scripts/State/LootableContainerState.cs` | Container/corpse loot state |
| `Assets/Scripts/State/StatusEffectState.cs` | StatusEffectType enum + StatusEffectInstance |
| `Assets/Scripts/State/QuestProgressState.cs` | Quest progress tracking |
| `Assets/Scripts/Systems/InventorySystem.cs` | Pickup, drop, move, find helpers |
| `Assets/Scripts/Systems/EquipmentSystem.cs` | Armor equip/unequip, durability sync |
| `Assets/Scripts/Systems/QuickSlotSystem.cs` | Quick slot binding and activation |
| `Assets/Scripts/Systems/LootSystem.cs` | Container creation, corpse loot, interactables |
| `Assets/Scripts/Systems/CraftingSystem.cs` | Recipe validation and crafting |
| `Assets/Scripts/Systems/StatusEffectSystem.cs` | Bleed apply/tick/downgrade/remove |
| `Assets/Scripts/Systems/BandageSystem.cs` | Bandage use (bleed cure) |
| `Assets/Scripts/Systems/MedkitSystem.cs` | Medkit use (continuous heal) |
| `Assets/Scripts/Systems/StaminaSystem.cs` | Sprint drain and regen |
| `Assets/Scripts/Systems/QuestSystem.cs` | Quest accept/complete/rewards |
| `Assets/Scripts/Constants/CraftConstants.cs` | All craft recipes |
| `Assets/Scripts/Constants/ContainerConstants.cs` | Container type configs |
| `Assets/Scripts/Constants/StatusEffectConstants.cs` | Bleed/bandage constants |
| `Assets/Scripts/Constants/MedConstants.cs` | Medkit heal constants |
| `Assets/Scripts/Constants/StaminaConstants.cs` | Sprint/stamina constants |
| `Assets/Scripts/Quests/QuestDefinition.cs` | Quest ScriptableObject schema |
| `Assets/Scripts/Quests/QuestDatabase.cs` | Quest DB with prerequisite support |
| `Assets/Scripts/Quests/QuestTask.cs` | Quest task type hierarchy |
