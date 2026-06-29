using System.Collections.Generic;
using NUnit.Framework;
using State;
using Systems;
using View.UI.Inventory;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure weapon-comparison logic: baseline selection (active + flip, skip-self, none) and the
    /// stat-diff row pairing (deltas + bars). View rendering is covered by eyeball.
    /// </summary>
    [TestFixture]
    public class WeaponCompareTests
    {
        static ItemState Wpn(int id) => ItemState.CreateWeapon(new EId(id), "Weapon",
            new WeaponConfiguration(
                new PayloadCoreInstance("p", RarityTier.Common),
                new DeliveryCoreInstance("d", RarityTier.Common),
                exotic: null, ammoInMagazine: 10));

        // ── WeaponCompareTarget ───────────────────────────────

        [Test]
        public void Candidates_SelectedFirst()
        {
            var w0 = Wpn(1);
            var w1 = Wpn(2);
            var c = WeaponCompareTarget.Candidates(new[] { w0, w1 }, selectedSlot: 1, hovered: null);
            CollectionAssert.AreEqual(new[] { w1, w0 }, c); // selected slot leads
        }

        [Test]
        public void Candidates_SkipsEmptyAndNonWeapon()
        {
            var w0 = Wpn(1);
            var medkit = ItemState.Create(new EId(9), "Medkit");
            var c = WeaponCompareTarget.Candidates(new[] { w0, null, medkit }, selectedSlot: 0, hovered: null);
            CollectionAssert.AreEqual(new[] { w0 }, c);
        }

        [Test]
        public void Candidates_ExcludesHoveredItself()
        {
            var w0 = Wpn(1);
            var w1 = Wpn(2);
            // Hovering the equipped w0 → compare against the other equipped weapon, not itself.
            var c = WeaponCompareTarget.Candidates(new[] { w0, w1 }, selectedSlot: 0, hovered: w0);
            CollectionAssert.AreEqual(new[] { w1 }, c);
        }

        [Test]
        public void Candidates_NoneEquipped_Empty()
        {
            var c = WeaponCompareTarget.Candidates(new ItemState[] { null, null }, selectedSlot: 0, hovered: null);
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void Pick_WrapsByFlipStep()
        {
            var w0 = Wpn(1);
            var w1 = Wpn(2);
            var c = new List<ItemState> { w1, w0 };
            Assert.AreSame(w1, WeaponCompareTarget.Pick(c, 0));
            Assert.AreSame(w0, WeaponCompareTarget.Pick(c, 1));
            Assert.AreSame(w1, WeaponCompareTarget.Pick(c, 2));  // wrap
            Assert.AreSame(w0, WeaponCompareTarget.Pick(c, -1)); // negative wrap
            Assert.IsNull(WeaponCompareTarget.Pick(new List<ItemState>(), 0));
        }

        // ── WeaponStatComparison ──────────────────────────────

        static WeaponStatDisplay.StatDisplayRow Bar(string label, string value, float bar)
            => new(label, value, bar);
        static WeaponStatDisplay.StatDisplayRow Val(string label, string value)
            => new(label, value, -1f);

        [Test]
        public void Build_ComputesDeltasAndBars()
        {
            var hovered = new List<WeaponStatDisplay.StatDisplayRow>
            {
                Bar("Damage", "20", 0.8f),
                Bar("Ergonomics", "50", 0.50f),
                Val("Magazine", "30"),
            };
            var baseline = new List<WeaponStatDisplay.StatDisplayRow>
            {
                Bar("Damage", "12", 0.5f),
                Bar("Ergonomics", "65", 0.65f),
                Val("Magazine", "25"),
            };

            var rows = WeaponStatComparison.Build(hovered, baseline);

            Assert.AreEqual(8f, rows[0].Delta, 1e-3); // Damage +8 → improved
            Assert.IsTrue(rows[0].Improved);
            Assert.IsTrue(rows[0].HasBar);
            Assert.AreEqual(0.8f, rows[0].HoveredBar, 1e-3);
            Assert.AreEqual(0.5f, rows[0].BaselineBar, 1e-3);

            Assert.AreEqual(-15f, rows[1].Delta, 1e-3); // Ergonomics −15 → worsened
            Assert.IsTrue(rows[1].Worsened);

            Assert.AreEqual(5f, rows[2].Delta, 1e-3); // Magazine +5, value-only
            Assert.IsFalse(rows[2].HasBar);
        }

        [Test]
        public void Build_NullBaseline_ZeroDeltas()
        {
            var hovered = new List<WeaponStatDisplay.StatDisplayRow> { Bar("Damage", "20", 0.8f) };
            var rows = WeaponStatComparison.Build(hovered, null);
            Assert.AreEqual(0f, rows[0].Delta, 1e-3);
            Assert.IsFalse(rows[0].Improved);
            Assert.AreEqual(-1f, rows[0].BaselineBar, 1e-3); // no baseline bar
        }

        [Test]
        public void Build_UnparseableValue_NoDelta()
        {
            var hovered  = new List<WeaponStatDisplay.StatDisplayRow> { Bar("Rate of Fire", "—", 0f) };
            var baseline = new List<WeaponStatDisplay.StatDisplayRow> { Bar("Rate of Fire", "6/s", 0.5f) };
            var rows = WeaponStatComparison.Build(hovered, baseline);
            Assert.AreEqual(0f, rows[0].Delta, 1e-3); // "—" unparseable → no delta
        }
    }
}
