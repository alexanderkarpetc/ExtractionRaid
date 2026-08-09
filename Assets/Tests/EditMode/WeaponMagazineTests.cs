using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Magazine capacity vs the stored round count. Capacity is composed (rarity, later
    /// attachments); the count travels on the item's <see cref="WeaponConfiguration"/> as authored.
    /// Nothing used to reconcile them, so a Common bot pistol spawned holding 12 rounds in a
    /// 6-round magazine and the corpse dropped it that way — the compare panel duly showed
    /// "Magazine 6" next to "12 in mag".
    /// </summary>
    [TestFixture]
    public class WeaponMagazineTests
    {
        const int Capacity = 6;

        BallisticPayloadDefinition _payload;
        DeliveryCoreDefinition     _delivery;
        CoreDefinitionDatabase     _db;
        ICoreDefinitionRegistry    _registry;
        FakeRaidEvents             _events;

        [SetUp]
        public void SetUp()
        {
            _payload  = WeaponBuilderTestFactory.MakeBallistic("BallisticRound", ammoType: "Ammo_Rifle");
            _delivery = WeaponBuilderTestFactory.MakeDelivery("SingleAction",
                pattern: FiringPattern.Single,
                commonStats: new DeliveryStats
                {
                    FireInterval       = 0.4f,
                    ProjectilesPerShot = 1,
                    MagazineSize       = Capacity,
                    ReloadTime         = 1.5f,
                    ConeHalfAngle      = 35f,
                });
            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _payload },
                deliveries: new DeliveryCoreDefinition[] { _delivery });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
            _events   = new FakeRaidEvents();
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_payload, _delivery, _db);

        [Test]
        public void BuildWeaponForItem_OverfullConfig_ClampsToCapacity()
        {
            var weapon = Build(ammoInMagazine: Capacity * 2);

            Assert.AreEqual(Capacity, weapon.AmmoInMagazine,
                "A magazine can never hold more than the composed capacity.");
        }

        [Test]
        public void BuildWeaponForItem_FittingConfig_IsLeftAlone()
        {
            // The clamp must not "top up" a half-empty gun — spent rounds stay spent.
            var weapon = Build(ammoInMagazine: 2);

            Assert.AreEqual(2, weapon.AmmoInMagazine);
        }

        [Test]
        public void SpawnBot_StartsWithAFullMagazine()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, new[] { Vector3.zero },
                _events, _registry);

            var weapon = state.Bots[0].Weapon;
            Assert.IsNotNull(weapon);
            Assert.AreEqual(weapon.Stats.MagazineSize, weapon.AmmoInMagazine,
                "Bots start loaded to their own composed capacity, whatever rarity they rolled.");
        }

        WeaponEntityState Build(int ammoInMagazine)
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                new DeliveryCoreInstance("SingleAction", RarityTier.Common),
                exotic: null,
                ammoInMagazine: ammoInMagazine);

            var item = ItemState.CreateWeapon(new EId(1), "Weapon", config);
            var weapon = WeaponSyncSystem.BuildWeaponForItem(item, _registry, _events);
            Assert.IsNotNull(weapon, "Assembly must succeed for this fixture.");
            return weapon;
        }
    }
}
