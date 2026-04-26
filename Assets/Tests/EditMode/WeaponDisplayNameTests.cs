using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponDisplayNameTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        DeliveryCoreDefinition     _pistolDelivery;
        ICoreDefinitionRegistry    _registry;

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle");
            _pistolDelivery = WeaponBuilderTestFactory.MakeDelivery(
                "SingleAction", formFactor: "Pistol", pattern: FiringPattern.Single);

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic },
                deliveries: new DeliveryCoreDefinition[] { _pistolDelivery });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _pistolDelivery, _db);

        [Test]
        public void For_NullItem_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, WeaponDisplayName.For(null, _registry));
        }

        [Test]
        public void For_NonWeaponItem_FallsThroughToDisplayName()
        {
            // "Medkit" is a real, registered ItemDefinition.
            var medkit = ItemState.Create(new EId(1), "Medkit");
            var name = WeaponDisplayName.For(medkit, _registry);
            Assert.AreEqual(medkit.DisplayName, name);
        }

        [Test]
        public void For_NonWeaponItem_UnknownDefinition_FallsThroughToId()
        {
            // No matching ItemDefinition → DisplayName falls back to DefinitionId.
            var unknown = ItemState.Create(new EId(2), "MysteryItem");
            Assert.AreEqual("MysteryItem", WeaponDisplayName.For(unknown, _registry));
        }

        [Test]
        public void For_BuiltWeapon_ResolvesArchetypeLabel()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);
            var weapon = ItemState.CreateWeapon(new EId(3), "Weapon", config);

            Assert.AreEqual("Ballistic Pistol", WeaponDisplayName.For(weapon, _registry));
        }

        [Test]
        public void For_WeaponWithMissingPayloadInRegistry_ReturnsBrokenLabel()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("GhostPayload", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction", RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);
            var weapon = ItemState.CreateWeapon(new EId(4), "Weapon", config);

            Assert.AreEqual(WeaponDisplayName.BrokenLabel,
                WeaponDisplayName.For(weapon, _registry));
        }

        [Test]
        public void For_WeaponWithMissingDeliveryInRegistry_ReturnsBrokenLabel()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("GhostDelivery",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);
            var weapon = ItemState.CreateWeapon(new EId(5), "Weapon", config);

            Assert.AreEqual(WeaponDisplayName.BrokenLabel,
                WeaponDisplayName.For(weapon, _registry));
        }

        [Test]
        public void For_WeaponWithNullRegistry_ReturnsBrokenLabel()
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                exotic: null,
                ammoInMagazine: 12);
            var weapon = ItemState.CreateWeapon(new EId(6), "Weapon", config);

            Assert.AreEqual(WeaponDisplayName.BrokenLabel,
                WeaponDisplayName.For(weapon, registry: null));
        }
    }
}
