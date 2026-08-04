using NUnit.Framework;
using State;

namespace Tests.EditMode
{
    /// <summary>
    /// Smoke tests for the static <see cref="ItemDefinition.Registry"/>: verifies that core
    /// items (armor pieces, ammo) load with the shape production code depends on.
    /// The AP/HP design-contract cases (AP pens harder, HP bleeds more) went away with those
    /// calibers in the ammo audit (2026-07-27) — bring them back with the ammo-selection
    /// feature that makes variants loadable again.
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

        [TestCase("Ammo_Rifle",      ExpectedResult = true)]  // brass: punches through armor
        [TestCase("Ammo_EnergyCell", ExpectedResult = false)] // laser: damage comes from the weapon
        public bool Ammo_HasPenetration(string id)
        {
            var def = ItemDefinition.Get(id);
            Assert.IsNotNull(def, $"{id} must be in registry");
            return def.Penetration > 0f;
        }

        // 2026-05-26: all ammo carries a baseline bleed chance (5%) so the bleed/HUD loop
        // always has a chance to fire.
        [TestCase("Ammo_Rifle")]
        [TestCase("Ammo_EnergyCell")]
        public void Ammo_HasBaselineBleedChance(string id)
        {
            var def = ItemDefinition.Get(id);
            Assert.IsNotNull(def, $"{id} must be in registry");
            Assert.Greater(def.BleedChance, 0f, $"{id} should carry the baseline bleed chance");
        }
    }
}
