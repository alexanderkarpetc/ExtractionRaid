using System;
using System.Collections.Generic;
using State;

namespace Systems
{
    /// <summary>
    /// Attachment slot taxonomy + rarity-scaled unlock rules (stateless).
    ///
    /// Slots are core-granted (slots.md §Working layout): the Payload core grants
    /// [Optic, Magazine, Buttstock] and the Delivery core grants [Muzzle, Grip], in that
    /// unlock order. How many of each core's slots are unlocked scales with that core's
    /// rarity — so the two cores' rarities together form the build canvas
    /// (Common/Common ≈ 2 slots → Legendary/Legendary = 5).
    /// </summary>
    public static class AttachmentSlots
    {
        // Unlock order per core — earlier entries unlock at lower rarity.
        public static readonly AttachmentSlot[] PayloadOrder =
            { AttachmentSlot.Optic, AttachmentSlot.Magazine, AttachmentSlot.Buttstock };
        public static readonly AttachmentSlot[] DeliveryOrder =
            { AttachmentSlot.Muzzle, AttachmentSlot.Grip };

        /// <summary>Slots a core of this rarity unlocks (before the per-core category cap).</summary>
        public static int CountForRarity(RarityTier rarity) => rarity switch
        {
            RarityTier.Common    => 1,
            RarityTier.Uncommon  => 1,
            RarityTier.Rare      => 2,
            RarityTier.Epic      => 2,
            RarityTier.Legendary => 3,
            _                    => 1,
        };

        public static int UnlockedPayloadCount(RarityTier payloadRarity) =>
            Math.Min(PayloadOrder.Length, CountForRarity(payloadRarity));

        public static int UnlockedDeliveryCount(RarityTier deliveryRarity) =>
            Math.Min(DeliveryOrder.Length, CountForRarity(deliveryRarity));

        /// <summary>Whether <paramref name="slot"/> is unlocked on this weapon given its core rarities.</summary>
        public static bool IsUnlocked(ItemState weapon, AttachmentSlot slot)
        {
            if (weapon == null || !weapon.HasWeaponConfiguration) return false;
            var cfg = weapon.WeaponConfiguration;

            int pIdx = Array.IndexOf(PayloadOrder, slot);
            if (pIdx >= 0) return pIdx < UnlockedPayloadCount(cfg.Payload.Rarity);

            int dIdx = Array.IndexOf(DeliveryOrder, slot);
            if (dIdx >= 0) return dIdx < UnlockedDeliveryCount(cfg.Delivery.Rarity);

            return false;
        }

        /// <summary>Total unlocked slots across both cores (payload + delivery).</summary>
        public static int TotalUnlocked(ItemState weapon)
        {
            if (weapon == null || !weapon.HasWeaponConfiguration) return 0;
            var cfg = weapon.WeaponConfiguration;
            return UnlockedPayloadCount(cfg.Payload.Rarity) + UnlockedDeliveryCount(cfg.Delivery.Rarity);
        }

        /// <summary>The first <paramref name="count"/> slots of an unlock order (helper for UI grouping).</summary>
        public static IEnumerable<AttachmentSlot> Take(AttachmentSlot[] order, int count)
        {
            for (int i = 0; i < order.Length && i < count; i++)
                yield return order[i];
        }
    }
}
