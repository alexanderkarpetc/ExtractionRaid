using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using State;
using Systems;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponStatDisplayTests
    {
        // A "balanced pistol-ish" baseline mirroring the Common stub assets.
        static WeaponStats Baseline() => new WeaponStats
        {
            Damage                   = 15f,
            HeadshotDamageMultiplier = 2.0f,
            FireInterval             = 0.4f,
            MagazineSize             = 12,
            SpreadAngle              = 2f,
            ConeHalfAngle            = 35f,
            BodyRotationSpeed        = 300f,
            RecoilKickForward        = 1.5f,
            RecoilKickSide           = 1f,
            RecoilRecoverySpeed      = 4f,
            EquipTime                = 0.2f,
            UnequipTime              = 0.15f,
            ReloadTime               = 1.5f,
        };

        static WeaponStatDisplay.StatDisplayRow Find(IReadOnlyList<WeaponStatDisplay.StatDisplayRow> rows, string label)
            => rows.First(r => r.Label == label);

        // ── Param set & ordering ──────────────────────────────

        [Test]
        public void Build_ReturnsExpectedRows_InOrder()
        {
            var rows = WeaponStatDisplay.Build(Baseline());
            var labels = rows.Select(r => r.Label).ToArray();
            Assert.AreEqual(
                new[] { "Damage", "Rate of Fire", "Stability", "Accuracy", "Ergonomics", "Headshot", "Magazine" },
                labels);
        }

        [Test]
        public void OmittedParams_AreAbsent()
        {
            var labels = WeaponStatDisplay.Build(Baseline()).Select(r => r.Label).ToArray();
            CollectionAssert.DoesNotContain(labels, "Reload");
            CollectionAssert.DoesNotContain(labels, "Penetration");
            CollectionAssert.DoesNotContain(labels, "Bleed");
            CollectionAssert.DoesNotContain(labels, "Noise");
            CollectionAssert.DoesNotContain(labels, "Sight Range");
        }

        // ── Number rows ───────────────────────────────────────

        [Test]
        public void ValueOnlyRows_HaveValue_AndNoBar()
        {
            var rows = WeaponStatDisplay.Build(Baseline());
            foreach (var label in new[] { "Headshot", "Magazine" })
            {
                var row = Find(rows, label);
                Assert.IsFalse(row.HasBar, $"{label} should not have a bar");
                Assert.IsNotEmpty(row.Value, $"{label} should carry a value");
            }
        }

        [Test]
        public void ValueOnlyRows_SinkToBottom()
        {
            var labels = WeaponStatDisplay.Build(Baseline()).Select(r => r.Label).ToList();
            // Headshot + Magazine (value-only) must come after every bar row.
            int lastBarIdx = labels.IndexOf("Ergonomics");
            Assert.Greater(labels.IndexOf("Headshot"), lastBarIdx);
            Assert.Greater(labels.IndexOf("Magazine"), lastBarIdx);
        }

        [Test]
        public void Damage_FormatsValue()
            => Assert.AreEqual("15", Find(WeaponStatDisplay.Build(Baseline()), "Damage").Value);

        [Test]
        public void Headshot_FormatsWithMultiplierSign()
            => Assert.AreEqual("2×", Find(WeaponStatDisplay.Build(Baseline()), "Headshot").Value);

        [Test]
        public void Magazine_FormatsInteger()
            => Assert.AreEqual("12", Find(WeaponStatDisplay.Build(Baseline()), "Magazine").Value);

        [Test]
        public void RateOfFire_IsInverseOfFireInterval()
        {
            var s = Baseline(); s.FireInterval = 0.5f; // → 2.0/s
            Assert.AreEqual("2/s", Find(WeaponStatDisplay.Build(s), "Rate of Fire").Value);
        }

        [Test]
        public void RateOfFire_ZeroInterval_ShowsDash()
        {
            var s = Baseline(); s.FireInterval = 0f;
            Assert.AreEqual("—", Find(WeaponStatDisplay.Build(s), "Rate of Fire").Value);
        }

        // ── Bar rows ──────────────────────────────────────────

        [Test]
        public void BarRows_HaveBar_AndValue_Clamped01()
        {
            var rows = WeaponStatDisplay.Build(Baseline());
            foreach (var label in new[] { "Damage", "Rate of Fire", "Stability", "Accuracy", "Ergonomics" })
            {
                var row = Find(rows, label);
                Assert.IsTrue(row.HasBar, $"{label} should have a bar");
                Assert.IsNotEmpty(row.Value, $"{label} should also carry a value");
                Assert.GreaterOrEqual(row.BarRatio01, 0f);
                Assert.LessOrEqual(row.BarRatio01, 1f);
            }
        }

        [Test]
        public void Accuracy_ZeroSpread_IsMax()
        {
            var s = Baseline(); s.SpreadAngle = 0f;
            Assert.AreEqual(1f, Find(WeaponStatDisplay.Build(s), "Accuracy").BarRatio01, 1e-4f);
        }

        [Test]
        public void Accuracy_HugeSpread_IsZero()
        {
            var s = Baseline(); s.SpreadAngle = 999f;
            Assert.AreEqual(0f, Find(WeaponStatDisplay.Build(s), "Accuracy").BarRatio01, 1e-4f);
        }

        [Test]
        public void Accuracy_LowerSpread_IsBetter()
        {
            var tight = Baseline(); tight.SpreadAngle = 1f;
            var loose = Baseline(); loose.SpreadAngle = 8f;
            Assert.Greater(
                Find(WeaponStatDisplay.Build(tight), "Accuracy").BarRatio01,
                Find(WeaponStatDisplay.Build(loose), "Accuracy").BarRatio01);
        }

        [Test]
        public void Stability_LowerKick_IsBetter()
        {
            var soft = Baseline(); soft.RecoilKickForward = 0.5f; soft.RecoilKickSide = 0.3f;
            var hard = Baseline(); hard.RecoilKickForward = 3f;   hard.RecoilKickSide = 2f;
            Assert.Greater(
                Find(WeaponStatDisplay.Build(soft), "Stability").BarRatio01,
                Find(WeaponStatDisplay.Build(hard), "Stability").BarRatio01);
        }

        [Test]
        public void Stability_FasterRecovery_IsBetter()
        {
            var slow = Baseline(); slow.RecoilRecoverySpeed = 1f;
            var fast = Baseline(); fast.RecoilRecoverySpeed = 8f;
            Assert.Greater(
                Find(WeaponStatDisplay.Build(fast), "Stability").BarRatio01,
                Find(WeaponStatDisplay.Build(slow), "Stability").BarRatio01);
        }

        [Test]
        public void Ergonomics_FasterHandling_IsBetter()
        {
            var nimble = Baseline();
            nimble.EquipTime = 0.1f; nimble.UnequipTime = 0.1f; nimble.BodyRotationSpeed = 400f;
            var clunky = Baseline();
            clunky.EquipTime = 0.6f; clunky.UnequipTime = 0.5f; clunky.BodyRotationSpeed = 100f;
            Assert.Greater(
                Find(WeaponStatDisplay.Build(nimble), "Ergonomics").BarRatio01,
                Find(WeaponStatDisplay.Build(clunky), "Ergonomics").BarRatio01);
        }

        [Test]
        public void BarRatios_StayClamped_OnExtremeStats()
        {
            var extreme = new WeaponStats
            {
                Damage = 99999f,
                RecoilKickForward = 999f, RecoilKickSide = 999f, RecoilRecoverySpeed = -50f,
                SpreadAngle = -10f, // negative spread shouldn't push accuracy > 1
                EquipTime = -1f, UnequipTime = -1f, BodyRotationSpeed = 99999f,
                FireInterval = 0.001f, // huge rate of fire
            };
            foreach (var label in new[] { "Damage", "Rate of Fire", "Stability", "Accuracy", "Ergonomics" })
            {
                var r = Find(WeaponStatDisplay.Build(extreme), label).BarRatio01;
                Assert.GreaterOrEqual(r, 0f, $"{label} below 0");
                Assert.LessOrEqual(r, 1f, $"{label} above 1");
            }
        }
    }
}
