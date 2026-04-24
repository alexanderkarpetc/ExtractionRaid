using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    /// <summary>
    /// Smoke tests for the static <see cref="ItemDefinition.Registry"/>: verifies that
    /// core items (armor pieces, standard/HP ammo) load with the shape production code
    /// depends on, plus one design-contract check (AP ammo pens better than standard).
    /// </summary>
    [TestFixture]
    public class ItemDefinitionRegistryTests
    {
        [TestCase("Helmet_Basic")]
        [TestCase("Armor_Basic")]
        public void CoreArmor_HasExpectedShape(string id)
        {
            var def = ItemDefinition.Get(id);
            Assert.IsNotNull(def, $"{id} must be in registry");
            Assert.Greater(def.ArmorPoints,  0f, $"{id} should have positive ArmorPoints");
            Assert.Greater(def.MaxDurability, 0f, $"{id} should have positive MaxDurability");
            Assert.IsNotNull(def.ArmorPrefabId, $"{id} should carry a visual prefab id");
        }

        [TestCase("Ammo_Rifle",    ExpectedResult = true)]  // standard: has pen, no bleed
        [TestCase("Ammo_Rifle_HP", ExpectedResult = false)] // HP:       zero pen, has bleed
        public bool Ammo_HasPenetration(string id)
        {
            var def = ItemDefinition.Get(id);
            Assert.IsNotNull(def, $"{id} must be in registry");
            return def.Penetration > 0f;
        }

        [TestCase("Ammo_Rifle",    ExpectedResult = false)] // standard: no bleed
        [TestCase("Ammo_Rifle_HP", ExpectedResult = true)]  // HP:       has bleed
        public bool Ammo_HasBleedChance(string id)
        {
            var def = ItemDefinition.Get(id);
            Assert.IsNotNull(def, $"{id} must be in registry");
            return def.BleedChance > 0f;
        }

        [Test]
        public void AmmoRifleAP_HasHigherPenetrationThanStandard()
        {
            // Design contract: AP ammo pen > standard ammo pen.
            var standard = ItemDefinition.Get("Ammo_Rifle");
            var ap       = ItemDefinition.Get("Ammo_Rifle_AP");
            Assert.Greater(ap.Penetration, standard.Penetration,
                "AP ammo should have higher penetration than standard");
        }
    }
}
