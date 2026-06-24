using System.Collections.Generic;
using Adapters;
using Quests;
using State;

namespace View.UI.Tooltip.Builders
{
    /// <summary>
    /// Builds a <see cref="TooltipModel"/> for any inventory item. Delegates to
    /// <see cref="WeaponTooltipBuilder"/> for built weapon items, and to
    /// <see cref="ModuleTooltipBuilder"/> for Weapon Builder module items
    /// (Payload / Delivery cores stored у backpack as standalone items — Tier 6 G1).
    /// Falls through to the generic item view (stack count, armor, ammo) otherwise.
    ///
    /// Pure C# — no Unity refs — fully unit-testable.
    /// </summary>
    public static class ItemTooltipBuilder
    {
        public static TooltipModel For(ItemState item,
            ICoreDefinitionRegistry registry = null,
            QuestDatabase questDatabase = null,
            LootableContainerState shopContext = null,
            bool itemIsInShop = false)
        {
            if (item == null) return new TooltipModel(string.Empty);
            if (item.HasWeaponConfiguration)
            {
                var weaponModel = WeaponTooltipBuilder.For(item, registry);
                return AppendPrice(weaponModel, item, shopContext, itemIsInShop);
            }

            // Tier 6 G1: module items у backpack share identity з palette cards у
            // Builder — tooltip має бути однаковий, не generic "module name only".
            if (registry != null)
            {
                if (registry.TryGetPayload(item.DefinitionId, out var payloadDef))
                    return ModuleTooltipBuilder.ForPayload(payloadDef);
                if (registry.TryGetDelivery(item.DefinitionId, out var deliveryDef))
                    return ModuleTooltipBuilder.ForDelivery(deliveryDef);

                // Attachment mods: show slot + stat deltas instead of a title-only generic view.
                if (registry.TryGetAttachment(item.DefinitionId, out var attachmentDef) && attachmentDef != null)
                {
                    var attModel = AttachmentTooltipBuilder.For(attachmentDef, item);
                    return AppendPrice(attModel, item, shopContext, itemIsInShop);
                }
            }

            var def   = item.Definition;
            var title = def?.DisplayName ?? item.DefinitionId;

            var sections = new List<TooltipSection>();

            if (item.IsResourceItem)
            {
                sections.Add(new TooltipSection(null, new[]
                {
                    new TooltipRow("Resource", $"{item.CurrentResource}/{item.MaxResource}"),
                }));
            }
            else if (item.StackCount > 1)
            {
                sections.Add(new TooltipSection(null, new[]
                {
                    new TooltipRow("Quantity", $"x{item.StackCount}"),
                }));
            }

            if (def != null && def.ArmorPoints > 0f)
            {
                float max = item.HasCustomDurability ? item.MaxDurability     : def.MaxDurability;
                float cur = item.HasCustomDurability ? item.CurrentDurability : max;
                sections.Add(new TooltipSection("Armor", new[]
                {
                    new TooltipRow("Armor Points", def.ArmorPoints.ToString("0")),
                    new TooltipRow("Durability",   $"{cur:0}/{max:0}"),
                }));
            }

            if (def != null && !string.IsNullOrEmpty(def.AmmoType))
            {
                var rows = new List<TooltipRow>();
                if (def.Penetration > 0f) rows.Add(new TooltipRow("Penetration",   def.Penetration.ToString("0.##")));
                if (def.ArmorDamage > 0f) rows.Add(new TooltipRow("Armor Damage",  def.ArmorDamage.ToString("0.##")));
                if (def.BleedChance > 0f) rows.Add(new TooltipRow("Bleed Chance",  $"{def.BleedChance * 100f:0}%"));
                if (rows.Count > 0)
                    sections.Add(new TooltipSection("Ammo", rows));
            }

            string subtitle = null;
            string description = null;
            if (def != null && def.Category == ItemCategory.Quest)
            {
                subtitle = "Quest Item";
                AppendQuestInfo(item.DefinitionId, questDatabase, sections, out description);
            }

            var model = new TooltipModel(title, subtitle, sections, description);
            return AppendPrice(model, item, shopContext, itemIsInShop);
        }

        static TooltipModel AppendPrice(TooltipModel model, ItemState item,
            LootableContainerState shop, bool itemIsInShop)
        {
            int price;
            string label;
            if (shop != null && shop.IsShop && itemIsInShop)
            {
                price = Systems.ShopSystem.GetBuyPrice(shop, item);
                label = "Buy";
            }
            else if (shop != null && shop.IsShop)
            {
                price = Systems.ShopSystem.GetSellPrice(shop, item);
                label = "Sell";
            }
            else
            {
                price = Systems.ShopSystem.GetGlobalSellPrice(item);
                label = "Value";
            }
            if (price <= 0) return model;
            var existing = model.Sections;
            var combined = new List<TooltipSection>(existing != null ? existing.Count + 1 : 1);
            if (existing != null) combined.AddRange(existing);
            combined.Add(new TooltipSection(null, new[]
            {
                new TooltipRow(label, price + "¢"),
            }));
            return new TooltipModel(model.Title, model.Subtitle, combined, model.Description);
        }

        static void AppendQuestInfo(string itemId, QuestDatabase database,
            List<TooltipSection> sections, out string description)
        {
            description = null;
            if (database == null) return;

            var rows = new List<TooltipRow>();
            string firstDescription = null;

            foreach (var entry in database.Entries)
            {
                var quest = entry.Quest;
                if (quest == null || !QuestReferencesItem(quest, itemId)) continue;

                rows.Add(new TooltipRow(
                    string.IsNullOrEmpty(quest.DisplayName) ? quest.Id : quest.DisplayName,
                    string.IsNullOrEmpty(quest.NpcId) ? "" : quest.NpcId));

                if (firstDescription == null && !string.IsNullOrEmpty(quest.Description))
                    firstDescription = quest.Description;
            }

            if (rows.Count > 0)
                sections.Add(new TooltipSection("Used For", rows));

            description = firstDescription;
        }

        static bool QuestReferencesItem(QuestDefinition quest, string itemId)
        {
            if (quest.Tasks != null)
            {
                foreach (var task in quest.Tasks)
                {
                    switch (task)
                    {
                        case FindAndTransferTask t when t.QuestItemId == itemId: return true;
                        case CraftTask t           when t.ItemId == itemId:      return true;
                        case FindItemTask t        when t.ItemId == itemId:      return true;
                    }
                }
            }

            if (quest.Rewards != null)
            {
                foreach (var reward in quest.Rewards)
                    if (reward.ItemId == itemId) return true;
            }

            return false;
        }
    }
}
