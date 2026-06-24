using System.Collections.Generic;
using NUnit.Framework;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;
using View.UI;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace Tests.EditMode
{
    /// <summary>
    /// AttachmentTooltipBuilder — attachment item tooltip shows slot + stat deltas with
    /// green (improvement) / red (downside) coloring, instead of a title-only generic view.
    /// </summary>
    [TestFixture]
    public class AttachmentTooltipBuilderTests
    {
        readonly List<Object> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            WeaponBuilderTestFactory.DestroyAll(_cleanup.ToArray());
            _cleanup.Clear();
        }

        AttachmentDefinition Mk(string id, string name, AttachmentSlot slot,
                                params (WeaponStatAxis axis, float pct)[] deltas)
        {
            var def = WeaponBuilderTestFactory.MakeAttachment(id, name, slot, deltas);
            _cleanup.Add(def);
            return def;
        }

        static bool HasSection(TooltipModel m, string heading)
        {
            foreach (var s in m.Sections) if (s.Heading == heading) return true;
            return false;
        }

        static string RowValue(TooltipModel m, string label)
        {
            foreach (var s in m.Sections)
                foreach (var r in s.Rows)
                    if (r.Label == label) return r.Value;
            return null;
        }

        [Test]
        public void Null_ReturnsEmpty() => Assert.IsTrue(AttachmentTooltipBuilder.For(null).IsEmpty);

        [Test]
        public void Title_And_SlotSubtitle_And_EffectsSection()
        {
            var def = Mk("ExtendedMag", "Extended Magazine", AttachmentSlot.Magazine,
                (WeaponStatAxis.MagazineSize, 50f), (WeaponStatAxis.ReloadTime, 15f));
            var m = AttachmentTooltipBuilder.For(def);

            Assert.AreEqual("Extended Magazine", m.Title);
            StringAssert.Contains("Magazine", m.Subtitle);  // "Magazine Attachment"
            Assert.IsTrue(HasSection(m, "Effects"));
        }

        [Test]
        public void Effects_RowsAreColoredByGoodOrBad()
        {
            // MagazineSize +50 → good (green); ReloadTime +15 → worse/slower (red).
            var def = Mk("ExtendedMag", "Extended Magazine", AttachmentSlot.Magazine,
                (WeaponStatAxis.MagazineSize, 50f), (WeaponStatAxis.ReloadTime, 15f));
            var m = AttachmentTooltipBuilder.For(def);

            var mag = RowValue(m, "Magazine");
            Assert.IsNotNull(mag);
            StringAssert.Contains("+50%", mag);
            StringAssert.Contains(AttachmentStatDisplay.GoodHex, mag);

            var reload = RowValue(m, "Reload");
            Assert.IsNotNull(reload);
            StringAssert.Contains("+15%", reload);
            StringAssert.Contains(AttachmentStatDisplay.BadHex, reload);
        }

        [Test]
        public void Recoil_NegativeIsGood()
        {
            var def = Mk("MuzzleBrake", "Muzzle Brake", AttachmentSlot.Muzzle,
                (WeaponStatAxis.Recoil, -20f));
            var m = AttachmentTooltipBuilder.For(def);

            var recoil = RowValue(m, "Recoil");
            Assert.IsNotNull(recoil);
            StringAssert.Contains("-20%", recoil);
            StringAssert.Contains(AttachmentStatDisplay.GoodHex, recoil); // less recoil = improvement
        }

        [Test]
        public void NoModifiers_NoEffectsSection()
        {
            var def = Mk("Bare", "Bare Mod", AttachmentSlot.Optic);
            var m = AttachmentTooltipBuilder.For(def);
            Assert.AreEqual("Bare Mod", m.Title);
            Assert.IsFalse(HasSection(m, "Effects"));
        }

        [Test]
        public void StackedItem_ShowsQuantity()
        {
            var def = Mk("ExtendedMag", "Extended Magazine", AttachmentSlot.Magazine,
                (WeaponStatAxis.MagazineSize, 50f));
            var item = ItemState.Create(new EId(7), "ExtendedMag", stackCount: 3);
            var m = AttachmentTooltipBuilder.For(def, item);
            Assert.AreEqual("x3", RowValue(m, "Quantity"));
        }
    }
}
