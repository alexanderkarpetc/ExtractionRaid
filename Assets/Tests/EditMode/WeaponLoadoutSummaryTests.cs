using System.Collections.Generic;
using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// WeaponLoadoutSummary — the non-stat compare data: ammo type + the player's reserve of it,
    /// and the mods installed on the weapon (slot → display name).
    /// </summary>
    [TestFixture]
    public class WeaponLoadoutSummaryTests
    {
        readonly List<Object> _cleanup = new();
        ICoreDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var payload = WeaponBuilderTestFactory.MakeBallistic(id: "p", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats { Damage = 10f });
            var delivery = WeaponBuilderTestFactory.MakeDelivery(id: "d",
                commonStats: new DeliveryStats { FireInterval = 0.4f, MagazineSize = 20 });
            var mag = WeaponBuilderTestFactory.MakeAttachment("ExtendedMag", "Extended Magazine", AttachmentSlot.Magazine);
            var db = WeaponBuilderTestFactory.MakeDatabase(
                payloads: new[] { payload }, deliveries: new[] { delivery }, attachments: new[] { mag });
            _registry = WeaponBuilderTestFactory.MakeRegistry(db);
            _cleanup.Add(payload); _cleanup.Add(delivery); _cleanup.Add(mag); _cleanup.Add(db);
        }

        [TearDown]
        public void TearDown()
        {
            WeaponBuilderTestFactory.DestroyAll(_cleanup.ToArray());
            _cleanup.Clear();
        }

        static ItemState Weapon(params AttachmentInstance[] atts)
        {
            var w = ItemState.CreateWeapon(new EId(1), "Weapon",
                new WeaponConfiguration(
                    new PayloadCoreInstance("p", RarityTier.Common),
                    new DeliveryCoreInstance("d", RarityTier.Common),
                    exotic: null, ammoInMagazine: 20));
            if (atts != null && atts.Length > 0) w.WeaponConfiguration.Attachments = atts;
            return w;
        }

        [Test]
        public void Build_ReportsAmmoReserveAndMods()
        {
            var w = Weapon(new AttachmentInstance(AttachmentSlot.Magazine, "ExtendedMag"));
            var inv = new InventoryState();
            inv.Backpack[0] = ItemState.Create(new EId(50), "Ammo_Rifle", 40);

            var s = WeaponLoadoutSummary.Build(w, _registry, inv);

            Assert.AreEqual(40, s.AmmoReserve);
            StringAssert.Contains("Rifle", s.AmmoName); // resolved display name (e.g. "Rifle Ammo")
            Assert.AreEqual(1, s.Mods.Count);
            Assert.AreEqual("Magazine", s.Mods[0].Slot);
            Assert.AreEqual("Extended Magazine", s.Mods[0].Name);
        }

        [Test]
        public void Build_NoAmmoNoMods_ReserveZeroEmpty()
        {
            var s = WeaponLoadoutSummary.Build(Weapon(), _registry, new InventoryState());
            Assert.AreEqual(0, s.AmmoReserve);
            Assert.AreEqual(0, s.Mods.Count);
            StringAssert.Contains("Rifle", s.AmmoName); // ammo type still resolved even with 0 reserve
        }

        [Test]
        public void Build_NonWeapon_Empty()
        {
            var s = WeaponLoadoutSummary.Build(ItemState.Create(new EId(2), "Medkit"), _registry, new InventoryState());
            Assert.AreEqual(0, s.AmmoReserve);
            Assert.AreEqual(0, s.Mods.Count);
            Assert.AreEqual(string.Empty, s.AmmoName);
        }
    }
}
