using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    [TestFixture]
    public class ArmorStateTests
    {
        // ── ArmorState.Create ─────────────────────────────────

        [Test]
        public void Create_SetsFieldsCorrectly()
        {
            var armor = ArmorState.Create(65f, 200f);
            Assert.AreEqual(65f, armor.ArmorPoints, 0.001f);
            Assert.AreEqual(200f, armor.MaxDurability, 0.001f);
        }

        [Test]
        public void Create_DurabilityStartsFull()
        {
            var armor = ArmorState.Create(50f, 150f);
            Assert.AreEqual(150f, armor.CurrentDurability, 0.001f);
            Assert.AreEqual(armor.MaxDurability, armor.CurrentDurability, 0.001f);
        }

        // ── IsBroken ──────────────────────────────────────────

        [Test]
        public void IsBroken_PositiveDurability_ReturnsFalse()
        {
            var armor = ArmorState.Create(30f, 100f);
            Assert.IsFalse(armor.IsBroken);
        }

        [Test]
        public void IsBroken_ZeroDurability_ReturnsTrue()
        {
            var armor = ArmorState.Create(30f, 100f);
            armor.CurrentDurability = 0f;
            Assert.IsTrue(armor.IsBroken);
        }

        [Test]
        public void IsBroken_NegativeDurability_ReturnsTrue()
        {
            var armor = ArmorState.Create(30f, 100f);
            armor.CurrentDurability = -5f;
            Assert.IsTrue(armor.IsBroken);
        }

        // ── DurabilityPercent ─────────────────────────────────

        [Test]
        public void DurabilityPercent_Full_ReturnsOne()
        {
            var armor = ArmorState.Create(50f, 100f);
            Assert.AreEqual(1f, armor.DurabilityPercent, 0.001f);
        }

        [Test]
        public void DurabilityPercent_Half_ReturnsHalf()
        {
            var armor = ArmorState.Create(50f, 100f);
            armor.CurrentDurability = 50f;
            Assert.AreEqual(0.5f, armor.DurabilityPercent, 0.001f);
        }

        [Test]
        public void DurabilityPercent_ZeroMax_ReturnsZero()
        {
            var armor = ArmorState.Create(50f, 0f);
            Assert.AreEqual(0f, armor.DurabilityPercent, 0.001f);
        }

        // ── ArmorSlotState ────────────────────────────────────

        [Test]
        public void ArmorSlotState_DefaultSlotsAreNull()
        {
            var slots = new ArmorSlotState();
            Assert.IsNull(slots.Helmet);
            Assert.IsNull(slots.BodyArmor);
        }

        // ── Weapon factories carry BasePenetration ────────────

        [Test]
        public void CreateRifle_HasBasePenetration()
        {
            var weapon = WeaponEntityState.CreateRifle(new EId(1));
            Assert.Greater(weapon.BasePenetration, 0f);
            Assert.Greater(weapon.BaseArmorDamage, 0f);
        }

        [Test]
        public void CreateShotgun_HasBasePenetration()
        {
            var weapon = WeaponEntityState.CreateShotgun(new EId(1));
            Assert.Greater(weapon.BasePenetration, 0f);
            Assert.Greater(weapon.BaseArmorDamage, 0f);
        }

        [Test]
        public void CreatePistol_HasBasePenetration()
        {
            var weapon = WeaponEntityState.CreatePistol(new EId(1));
            Assert.Greater(weapon.BasePenetration, 0f);
            Assert.Greater(weapon.BaseArmorDamage, 0f);
        }

        // ── Projectile carries Penetration ────────────────────

        [Test]
        public void ProjectileCreate_WithPenetration_CarriesValues()
        {
            var proj = ProjectileEntityState.Create(
                new EId(1), new EId(2), default, default,
                10f, 0f, 3f, 25f,
                penetration: 45f, armorDamage: 12f);

            Assert.AreEqual(45f, proj.Penetration, 0.001f);
            Assert.AreEqual(12f, proj.ArmorDamage, 0.001f);
        }

        [Test]
        public void ProjectileCreate_DefaultPenetration_Zero()
        {
            var proj = ProjectileEntityState.Create(
                new EId(1), new EId(2), default, default,
                10f, 0f, 3f, 25f);

            Assert.AreEqual(0f, proj.Penetration, 0.001f);
            Assert.AreEqual(0f, proj.ArmorDamage, 0.001f);
        }

        // ── ItemDefinition armor/ammo stats ───────────────────

        [Test]
        public void ItemDefinition_HelmetBasic_HasArmorStats()
        {
            var def = ItemDefinition.Get("Helmet_Basic");
            Assert.IsNotNull(def);
            Assert.Greater(def.ArmorPoints, 0f);
            Assert.Greater(def.MaxDurability, 0f);
        }

        [Test]
        public void ItemDefinition_ArmorBasic_HasArmorStats()
        {
            var def = ItemDefinition.Get("Armor_Basic");
            Assert.IsNotNull(def);
            Assert.Greater(def.ArmorPoints, 0f);
            Assert.Greater(def.MaxDurability, 0f);
        }

        [Test]
        public void ItemDefinition_AmmoRifle_HasPenetration()
        {
            var def = ItemDefinition.Get("Ammo_Rifle");
            Assert.IsNotNull(def);
            Assert.Greater(def.Penetration, 0f);
        }

        [Test]
        public void ItemDefinition_AmmoRifleAP_HasHigherPenetration()
        {
            var standard = ItemDefinition.Get("Ammo_Rifle");
            var ap = ItemDefinition.Get("Ammo_Rifle_AP");
            Assert.Greater(ap.Penetration, standard.Penetration,
                "AP ammo should have higher penetration than standard");
        }

        // ── RaidState.ArmorMap ────────────────────────────────

        [Test]
        public void RaidState_Create_HasArmorMap()
        {
            var state = RaidState.Create();
            Assert.IsNotNull(state.ArmorMap);
            Assert.AreEqual(0, state.ArmorMap.Count);
        }
    }
}
