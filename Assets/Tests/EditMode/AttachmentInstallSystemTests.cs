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

        // Legendary/Legendary so the Magazine slot is unlocked — these tests cover install
        // mechanics; the rarity-scaled slot gate is covered in AttachmentSlotsTests.
        static ItemState Weapon() => ItemState.CreateWeapon(new EId(1), "Weapon",
            new WeaponConfiguration(
                new PayloadCoreInstance("p", RarityTier.Legendary),
                new DeliveryCoreInstance("d", RarityTier.Legendary),
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
            Assert.IsTrue(AttachmentInstallSystem.CanInstall(Weapon(), modDef, _registry));
            Assert.IsFalse(AttachmentInstallSystem.CanInstall(ItemState.Create(new EId(2), "Medkit"), modDef, _registry));
            Assert.IsFalse(AttachmentInstallSystem.CanInstall(Weapon(), null, _registry));
        }

        [Test]
        public void CanInstallIntoFreeSlot_FreeTrue_OccupiedFalse()
        {
            var modDef = AttachmentInstallSystem.Resolve(_registry, Mag);
            var w = Weapon();
            Assert.IsTrue(AttachmentInstallSystem.CanInstallIntoFreeSlot(w, modDef, _registry)); // empty Magazine slot

            _inv.Backpack[0] = ItemState.Create(Alloc(), Mag, 1);
            Assert.IsTrue(AttachmentInstallSystem.Install(w, _registry, _inv, Alloc, Mag, out _));

            // Slot now occupied → not a free-slot target (highlight off), but a swap is still allowed.
            Assert.IsFalse(AttachmentInstallSystem.CanInstallIntoFreeSlot(w, modDef, _registry));
            Assert.IsTrue(AttachmentInstallSystem.CanInstall(w, modDef, _registry));
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

        // ── CompatibleArchetype enforcement (P3-2) ────────────

        AttachmentDefinition MkArchetypeMod(string id, AttachmentSlot slot, string archetype)
        {
            var def = WeaponBuilderTestFactory.MakeAttachment(id, id, slot);
            WeaponBuilderTestFactory.SetPrivateField(def, "_compatibleArchetype", archetype);
            _cleanup.Add(def);
            return def;
        }

        [Test]
        public void ArchetypeMatches_UniversalAndTokenGating()
        {
            // Weapon = Laser payload + Scatter delivery.
            var laser   = WeaponBuilderTestFactory.MakeLaser(id: "lp",
                commonStats: new CommonPayloadStats { Damage = 10f });
            var scatter = WeaponBuilderTestFactory.MakeDelivery(id: "sd",
                formFactor: "Shotgun", pattern: FiringPattern.Scatter,
                commonStats: new DeliveryStats { FireInterval = 0.5f, MagazineSize = 6 });
            _cleanup.Add(laser); _cleanup.Add(scatter);

            var universal   = MkArchetypeMod("U",  AttachmentSlot.Optic,  "");
            var laserOnly   = MkArchetypeMod("LO", AttachmentSlot.Optic,  "Laser");
            var scatterOnly = MkArchetypeMod("SC", AttachmentSlot.Muzzle, "Scatter");
            var autoOnly    = MkArchetypeMod("AH", AttachmentSlot.Muzzle, "Auto");

            var db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:    new[] { laser },
                deliveries:  new[] { scatter },
                attachments: new[] { universal, laserOnly, scatterOnly, autoOnly });
            _cleanup.Add(db);
            var reg = WeaponBuilderTestFactory.MakeRegistry(db);

            var w = ItemState.CreateWeapon(new EId(5), "Weapon",
                new WeaponConfiguration(
                    new PayloadCoreInstance("lp", RarityTier.Legendary),
                    new DeliveryCoreInstance("sd", RarityTier.Legendary), null, 6));

            Assert.IsTrue(AttachmentInstallSystem.ArchetypeMatches(w, universal, reg));   // universal
            Assert.IsTrue(AttachmentInstallSystem.ArchetypeMatches(w, laserOnly, reg));   // payload archetype
            Assert.IsTrue(AttachmentInstallSystem.ArchetypeMatches(w, scatterOnly, reg)); // delivery pattern
            Assert.IsFalse(AttachmentInstallSystem.ArchetypeMatches(w, autoOnly, reg));   // not Auto

            Assert.IsTrue(AttachmentInstallSystem.CanInstall(w, laserOnly, reg));
            Assert.IsFalse(AttachmentInstallSystem.CanInstall(w, autoOnly, reg));
        }

        [Test]
        public void Install_ArchetypeIncompatible_RejectedAndNotConsumed()
        {
            // Ballistic weapon + a Laser-only Optic mod owned → install rejected, mod kept.
            var ball   = WeaponBuilderTestFactory.MakeBallistic(id: "bp",
                commonStats: new CommonPayloadStats { Damage = 10f });
            var single = WeaponBuilderTestFactory.MakeDelivery(id: "sd2",
                formFactor: "Rifle", pattern: FiringPattern.Single,
                commonStats: new DeliveryStats { FireInterval = 0.5f, MagazineSize = 20 });
            _cleanup.Add(ball); _cleanup.Add(single);

            var laserOptic = MkArchetypeMod("LaserOptic", AttachmentSlot.Optic, "Laser");

            var db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:    new[] { ball },
                deliveries:  new[] { single },
                attachments: new[] { laserOptic });
            _cleanup.Add(db);
            var reg = WeaponBuilderTestFactory.MakeRegistry(db);

            var inv = new InventoryState();
            inv.Backpack[0] = ItemState.Create(Alloc(), "LaserOptic", 1);

            var w = ItemState.CreateWeapon(new EId(6), "Weapon",
                new WeaponConfiguration(
                    new PayloadCoreInstance("bp", RarityTier.Legendary),
                    new DeliveryCoreInstance("sd2", RarityTier.Legendary), null, 20));

            bool ok = AttachmentInstallSystem.Install(w, reg, inv, Alloc, "LaserOptic", out var err);

            Assert.IsFalse(ok);
            Assert.IsNotNull(err);
            Assert.IsFalse(AttachmentInstallSystem.InstalledIn(w, AttachmentSlot.Optic).HasValue);
            Assert.AreEqual(1, inv.Backpack[0]?.StackCount); // not consumed
        }
    }
}
