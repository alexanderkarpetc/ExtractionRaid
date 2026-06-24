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
    /// AttachmentInstallSystem — the shared, stateless install/remove rules (used by both the
    /// attachment editor and inventory drag-drop). Covers the surface the presenter tests don't
    /// hit directly: slot derivation from the mod def, the CanInstall predicate, and Resolve.
    /// </summary>
    [TestFixture]
    public class AttachmentInstallSystemTests
    {
        const string Mag = "ExtendedMag"; // Magazine slot (real item id → AddToBackpack works)

        readonly List<Object> _cleanup = new();
        ICoreDefinitionRegistry _registry;
        InventoryState _inv;
        int _nextEid = 200;
        EId Alloc() => new EId(_nextEid++);

        [SetUp]
        public void SetUp()
        {
            var payload  = WeaponBuilderTestFactory.MakeBallistic(id: "p",
                commonStats: new CommonPayloadStats { Damage = 10f });
            var delivery = WeaponBuilderTestFactory.MakeDelivery(id: "d",
                commonStats: new DeliveryStats { FireInterval = 0.5f, MagazineSize = 20 });
            var mag = WeaponBuilderTestFactory.MakeAttachment(Mag, "Extended Magazine", AttachmentSlot.Magazine);
            var db  = WeaponBuilderTestFactory.MakeDatabase(
                payloads:    new[] { payload },
                deliveries:  new[] { delivery },
                attachments: new[] { mag });
            _registry = WeaponBuilderTestFactory.MakeRegistry(db);
            _cleanup.Add(payload); _cleanup.Add(delivery); _cleanup.Add(mag); _cleanup.Add(db);
            _inv = new InventoryState();
        }

        [TearDown]
        public void TearDown()
        {
            WeaponBuilderTestFactory.DestroyAll(_cleanup.ToArray());
            _cleanup.Clear();
        }

        static ItemState Weapon() => ItemState.CreateWeapon(new EId(1), "Weapon",
            new WeaponConfiguration(
                new PayloadCoreInstance("p", RarityTier.Common),
                new DeliveryCoreInstance("d", RarityTier.Common),
                exotic: null, ammoInMagazine: 20));

        [Test]
        public void Resolve_KnownReturnsDef_UnknownReturnsNull()
        {
            Assert.IsNotNull(AttachmentInstallSystem.Resolve(_registry, Mag));
            Assert.IsNull(AttachmentInstallSystem.Resolve(_registry, "ghost"));
            Assert.IsNull(AttachmentInstallSystem.Resolve(_registry, null));
        }

        [Test]
        public void CanInstall_BuiltWeaponTrue_NonWeaponFalse_NullModFalse()
        {
            var modDef = AttachmentInstallSystem.Resolve(_registry, Mag);
            Assert.IsTrue(AttachmentInstallSystem.CanInstall(Weapon(), modDef));
            Assert.IsFalse(AttachmentInstallSystem.CanInstall(ItemState.Create(new EId(2), "Medkit"), modDef));
            Assert.IsFalse(AttachmentInstallSystem.CanInstall(Weapon(), null));
        }

        [Test]
        public void CanInstallIntoFreeSlot_FreeTrue_OccupiedFalse()
        {
            var modDef = AttachmentInstallSystem.Resolve(_registry, Mag);
            var w = Weapon();
            Assert.IsTrue(AttachmentInstallSystem.CanInstallIntoFreeSlot(w, modDef)); // empty Magazine slot

            _inv.Backpack[0] = ItemState.Create(Alloc(), Mag, 1);
            Assert.IsTrue(AttachmentInstallSystem.Install(w, _registry, _inv, Alloc, Mag, out _));

            // Slot now occupied → not a free-slot target (highlight off), but a swap is still allowed.
            Assert.IsFalse(AttachmentInstallSystem.CanInstallIntoFreeSlot(w, modDef));
            Assert.IsTrue(AttachmentInstallSystem.CanInstall(w, modDef));
        }

        [Test]
        public void Install_DerivesSlotFromModDef_AndConsumes()
        {
            var w = Weapon();
            _inv.Backpack[0] = ItemState.Create(Alloc(), Mag, 1);

            bool ok = AttachmentInstallSystem.Install(w, _registry, _inv, Alloc, Mag, out var err);

            Assert.IsTrue(ok, err);
            Assert.IsTrue(AttachmentInstallSystem.InstalledIn(w, AttachmentSlot.Magazine).HasValue);
            Assert.IsNull(_inv.Backpack[0]); // one unit consumed
        }

        [Test]
        public void Install_NotOwned_FalseWithError()
        {
            var w = Weapon();
            bool ok = AttachmentInstallSystem.Install(w, _registry, _inv, Alloc, Mag, out var err);
            Assert.IsFalse(ok);
            Assert.IsNotNull(err);
            Assert.IsFalse(AttachmentInstallSystem.InstalledIn(w, AttachmentSlot.Magazine).HasValue);
        }
    }
}
