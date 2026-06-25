using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    /// <summary>
    /// AttachmentSlots — rarity-scaled slot unlock. Each core's rarity unlocks 1/1/2/2/3 of its
    /// slots (Common→Legendary), capped at the per-core category count (Payload 3, Delivery 2).
    /// </summary>
    [TestFixture]
    public class AttachmentSlotsTests
    {
        static ItemState Weapon(RarityTier payload, RarityTier delivery) =>
            ItemState.CreateWeapon(new EId(1), "Weapon",
                new WeaponConfiguration(
                    new PayloadCoreInstance("p", payload),
                    new DeliveryCoreInstance("d", delivery),
                    exotic: null, ammoInMagazine: 10));

        [Test]
        public void CountForRarity_FollowsCurve()
        {
            Assert.AreEqual(1, AttachmentSlots.CountForRarity(RarityTier.Common));
            Assert.AreEqual(1, AttachmentSlots.CountForRarity(RarityTier.Uncommon));
            Assert.AreEqual(2, AttachmentSlots.CountForRarity(RarityTier.Rare));
            Assert.AreEqual(2, AttachmentSlots.CountForRarity(RarityTier.Epic));
            Assert.AreEqual(3, AttachmentSlots.CountForRarity(RarityTier.Legendary));
        }

        [Test]
        public void UnlockedCounts_CappedAtCategoryCount()
        {
            Assert.AreEqual(3, AttachmentSlots.UnlockedPayloadCount(RarityTier.Legendary));  // Optic+Magazine+Buttstock
            Assert.AreEqual(2, AttachmentSlots.UnlockedDeliveryCount(RarityTier.Legendary)); // only Muzzle+Grip
        }

        [Test]
        public void Common_UnlocksOnlyFirstSlotPerCore()
        {
            var w = Weapon(RarityTier.Common, RarityTier.Common);
            Assert.IsTrue(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Optic));   // payload[0]
            Assert.IsTrue(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Muzzle));  // delivery[0]
            Assert.IsFalse(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Magazine));
            Assert.IsFalse(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Buttstock));
            Assert.IsFalse(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Grip));
            Assert.AreEqual(2, AttachmentSlots.TotalUnlocked(w));
        }

        [Test]
        public void RarePayload_UnlocksMagazine_NotButtstock()
        {
            var w = Weapon(RarityTier.Rare, RarityTier.Common);
            Assert.IsTrue(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Magazine));
            Assert.IsFalse(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Buttstock));
            Assert.AreEqual(3, AttachmentSlots.TotalUnlocked(w)); // 2 payload + 1 delivery
        }

        [Test]
        public void Legendary_UnlocksAllFive()
        {
            var w = Weapon(RarityTier.Legendary, RarityTier.Legendary);
            Assert.IsTrue(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Buttstock));
            Assert.IsTrue(AttachmentSlots.IsUnlocked(w, AttachmentSlot.Grip));
            Assert.AreEqual(5, AttachmentSlots.TotalUnlocked(w));
        }
    }
}
