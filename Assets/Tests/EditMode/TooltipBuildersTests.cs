using Adapters;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace Tests.EditMode
{
    [TestFixture]
    public class TooltipBuildersTests
    {
        CoreDefinitionDatabase     _db;
        BallisticPayloadDefinition _ballistic;
        LaserPayloadDefinition     _laser;
        DeliveryCoreDefinition     _singleAction;
        DeliveryCoreDefinition     _auto;
        ICoreDefinitionRegistry    _registry;

        const float LaserChargeTime = 1.25f;

        [SetUp]
        public void SetUp()
        {
            _ballistic = WeaponBuilderTestFactory.MakeBallistic(
                "BallisticRound", displayName: "Ballistic", ammoType: "Ammo_Rifle",
                commonStats: new CommonPayloadStats
                {
                    Damage = 15f, ProjectileSpeed = 25f, BasePenetration = 12f,
                    HeadshotDamageMultiplier = 2f,
                });
            _laser = WeaponBuilderTestFactory.MakeLaser(
                "LaserCharge", displayName: "Laser", ammoType: "Ammo_EnergyCell",
                commonStats: new CommonPayloadStats
                {
                    Damage = 25f, ProjectileSpeed = 60f, BasePenetration = 25f,
                    HeadshotDamageMultiplier = 1.5f,
                },
                chargeTime: LaserChargeTime);
            _singleAction = WeaponBuilderTestFactory.MakeDelivery(
                "SingleAction", formFactor: "Pistol", pattern: FiringPattern.Single,
                commonStats: new DeliveryStats
                {
                    FireInterval = 0.4f, MagazineSize = 12, ProjectilesPerShot = 1,
                    ReloadTime = 1.6f,
                });
            _auto = WeaponBuilderTestFactory.MakeDelivery(
                "Auto", formFactor: "Rifle", pattern: FiringPattern.Auto,
                commonStats: new DeliveryStats
                {
                    FireInterval = 0.2f, MagazineSize = 30, ProjectilesPerShot = 1,
                    ReloadTime = 2.5f,
                });

            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _ballistic, _laser },
                deliveries: new DeliveryCoreDefinition[] { _singleAction, _auto });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_ballistic, _laser, _singleAction, _auto, _db);

        // ── ItemTooltipBuilder ────────────────────────────────

        [Test]
        public void ItemBuilder_NullItem_ReturnsEmptyModel()
        {
            var model = ItemTooltipBuilder.For(null, _registry);
            Assert.IsTrue(model.IsEmpty);
        }

        [Test]
        public void ItemBuilder_PlainStackable_TitleAndQuantitySection()
        {
            var ammo = ItemState.Create(new EId(1), "Ammo_Rifle", stackCount: 30);
            var model = ItemTooltipBuilder.For(ammo, _registry);

            Assert.AreEqual("Rifle Ammo", model.Title);
            Assert.IsTrue(HasRow(model, "Quantity", "x30"));
        }

        [Test]
        public void ItemBuilder_ArmorItem_IncludesArmorSection()
        {
            var helmet = ItemState.Create(new EId(2), "Helmet_Basic");
            var model = ItemTooltipBuilder.For(helmet, _registry);

            Assert.AreEqual("Basic Helmet", model.Title);
            Assert.IsTrue(HasSection(model, "Armor"));
            Assert.IsTrue(HasRow(model, "Armor Points", "30"));
        }

        [Test]
        public void ItemBuilder_DelegatesToWeaponBuilderForBuiltWeapon()
        {
            var weapon = MakeWeaponItem("BallisticRound", "SingleAction", ammo: 12);
            var model = ItemTooltipBuilder.For(weapon, _registry);

            Assert.AreEqual("Ballistic Pistol", model.Title);
            Assert.IsTrue(HasSection(model, "Combat"));
            Assert.IsTrue(HasSection(model, "Cadence"));
        }

        // ── WeaponTooltipBuilder ──────────────────────────────

        [Test]
        public void WeaponBuilder_NonWeaponItem_ReturnsEmptyModel()
        {
            var medkit = ItemState.Create(new EId(10), "Medkit");
            var model = WeaponTooltipBuilder.For(medkit, _registry);
            Assert.IsTrue(model.IsEmpty);
        }

        [Test]
        public void WeaponBuilder_BallisticPistol_HasArchetypeAndCombatStats()
        {
            var weapon = MakeWeaponItem("BallisticRound", "SingleAction", ammo: 8);
            var model = WeaponTooltipBuilder.For(weapon, _registry);

            Assert.AreEqual("Ballistic Pistol", model.Title);
            Assert.AreEqual("Ballistic · Pistol", model.Subtitle);
            Assert.IsTrue(HasRow(model, "Damage", "15"));
            Assert.IsTrue(HasRow(model, "Magazine", "8/12"));
        }

        [Test]
        public void WeaponBuilder_LaserPistol_IncludesChargeRow()
        {
            var weapon = MakeWeaponItem("LaserCharge", "SingleAction", ammo: 12);
            var model = WeaponTooltipBuilder.For(weapon, _registry);

            Assert.AreEqual("Laser Pistol", model.Title);
            Assert.IsTrue(HasRow(model, "Charge", $"{LaserChargeTime:0.##} s"),
                "Charge row should appear for Laser weapons");
        }

        [Test]
        public void WeaponBuilder_BallisticPistol_NoChargeRow()
        {
            var weapon = MakeWeaponItem("BallisticRound", "SingleAction", ammo: 12);
            var model = WeaponTooltipBuilder.For(weapon, _registry);

            Assert.IsFalse(HasRowLabel(model, "Charge"),
                "Ballistic weapons must not show a Charge row");
        }

        [Test]
        public void WeaponBuilder_NullRegistry_FallsBackToTitleOnly()
        {
            var weapon = MakeWeaponItem("BallisticRound", "SingleAction", ammo: 12);
            var model = WeaponTooltipBuilder.For(weapon, registry: null);

            Assert.AreEqual(WeaponDisplayName.BrokenLabel, model.Title);
            Assert.AreEqual(0, model.Sections.Count);
        }

        // ── ModuleTooltipBuilder ──────────────────────────────

        [Test]
        public void ModuleBuilder_Payload_NullDefinition_Empty()
        {
            Assert.IsTrue(ModuleTooltipBuilder.ForPayload(null).IsEmpty);
        }

        [Test]
        public void ModuleBuilder_BallisticPayload_TitleSubtitleAndStats()
        {
            var model = ModuleTooltipBuilder.ForPayload(_ballistic);
            Assert.AreEqual("Ballistic", model.Title);
            Assert.AreEqual("Payload · Ammo_Rifle", model.Subtitle);
            Assert.IsTrue(HasRow(model, "Damage", "15"));
            Assert.IsFalse(HasRowLabel(model, "Charge"));
        }

        [Test]
        public void ModuleBuilder_LaserPayload_IncludesChargeRow()
        {
            var model = ModuleTooltipBuilder.ForPayload(_laser);
            Assert.AreEqual("Laser", model.Title);
            Assert.IsTrue(HasRow(model, "Charge", $"{LaserChargeTime:0.##} s"));
        }

        [Test]
        public void ModuleBuilder_Delivery_TitleFromFormFactor_PatternInSubtitle()
        {
            var model = ModuleTooltipBuilder.ForDelivery(_singleAction);
            Assert.AreEqual("Pistol", model.Title);
            Assert.AreEqual("Delivery · Single", model.Subtitle);
            Assert.IsTrue(HasRow(model, "Magazine", "12"));
            Assert.IsTrue(HasRow(model, "Reload", "1.6 s"));
        }

        // ── Helpers ───────────────────────────────────────────

        ItemState MakeWeaponItem(string payloadId, string deliveryId, int ammo)
        {
            var config = new WeaponConfiguration(
                new PayloadCoreInstance(payloadId,   RarityTier.Common),
                new DeliveryCoreInstance(deliveryId, RarityTier.Common),
                exotic: null,
                ammoInMagazine: ammo);
            return ItemState.CreateWeapon(new EId(100), "Weapon", config);
        }

        static bool HasSection(TooltipModel model, string heading)
        {
            for (int i = 0; i < model.Sections.Count; i++)
                if (model.Sections[i].Heading == heading) return true;
            return false;
        }

        static bool HasRow(TooltipModel model, string label, string value)
        {
            foreach (var section in model.Sections)
                foreach (var row in section.Rows)
                    if (row.Label == label && row.Value == value) return true;
            return false;
        }

        static bool HasRowLabel(TooltipModel model, string label)
        {
            foreach (var section in model.Sections)
                foreach (var row in section.Rows)
                    if (row.Label == label) return true;
            return false;
        }
    }

}
