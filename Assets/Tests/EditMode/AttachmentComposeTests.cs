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
    /// WeaponStatComposer.ApplyAttachments — player-facing axis deltas (option A) mapped
    /// onto raw WeaponStats fields. Raw-change semantics: +Recoil/+Spread/+ReloadTime are
    /// worse, +Damage/+Magazine/+Ergonomics are better; RoF is inverse of FireInterval.
    /// </summary>
    [TestFixture]
    public class AttachmentComposeTests
    {
        readonly List<Object> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            WeaponBuilderTestFactory.DestroyAll(_cleanup.ToArray());
            _cleanup.Clear();
        }

        static WeaponStats Baseline() => new WeaponStats
        {
            Damage              = 10f,
            FireInterval        = 0.5f,
            MagazineSize        = 20,
            ReloadTime          = 2f,
            RecoilKickForward   = 2f,
            RecoilKickSide      = 1f,
            SpreadAngle         = 4f,
            EquipTime           = 0.4f,
            UnequipTime         = 0.3f,
            BodyRotationSpeed   = 200f,
        };

        AttachmentDefinition MakeAtt(string id, AttachmentSlot slot,
                                     params (WeaponStatAxis axis, float pct)[] deltas)
        {
            var def = ScriptableObject.CreateInstance<AttachmentDefinition>();
            WeaponBuilderTestFactory.SetPrivateField(def, "_id", id);
            WeaponBuilderTestFactory.SetPrivateField(def, "_slot", slot);
            var mods = new StatDelta[deltas.Length];
            for (int i = 0; i < deltas.Length; i++)
                mods[i] = new StatDelta { Axis = deltas[i].axis, Percent = deltas[i].pct };
            WeaponBuilderTestFactory.SetPrivateField(def, "_modifiers", mods);
            _cleanup.Add(def);
            return def;
        }

        ICoreDefinitionRegistry RegistryWith(params AttachmentDefinition[] defs)
        {
            var db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            db.SetEntries(null, null, null, new List<AttachmentDefinition>(defs));
            _cleanup.Add(db);
            return new DatabaseCoreDefinitionRegistry(db);
        }

        static WeaponConfiguration Config(params AttachmentInstance[] installed)
        {
            var cfg = new WeaponConfiguration(
                new PayloadCoreInstance("p", RarityTier.Common),
                new DeliveryCoreInstance("d", RarityTier.Common),
                exotic: null, ammoInMagazine: 30);
            cfg.Attachments = installed;
            return cfg;
        }

        // ── No-op cases ───────────────────────────────────────

        [Test]
        public void NoAttachments_ReturnsUnchanged()
        {
            var reg = RegistryWith();
            var cfg = Config(); // Attachments stays null
            var s = WeaponStatComposer.ApplyAttachments(Baseline(), cfg, reg);
            Assert.AreEqual(20, s.MagazineSize);
            Assert.AreEqual(10f, s.Damage);
        }

        [Test]
        public void MissingDefinition_IsSkipped()
        {
            var reg = RegistryWith(); // empty registry
            var cfg = Config(new AttachmentInstance(AttachmentSlot.Muzzle, "ghost"));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(), cfg, reg);
            Assert.AreEqual(2f, s.RecoilKickForward);
        }

        [Test]
        public void EmptyDefinitionId_IsSkipped()
        {
            var reg = RegistryWith();
            var cfg = Config(new AttachmentInstance(AttachmentSlot.Muzzle, ""));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(), cfg, reg);
            Assert.AreEqual(10f, s.Damage);
        }

        // ── Per-axis deltas ───────────────────────────────────

        [Test]
        public void Magazine_Plus50_RoundsUp()
        {
            var def = MakeAtt("mag", AttachmentSlot.Magazine, (WeaponStatAxis.MagazineSize, 50f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Magazine, "mag")), RegistryWith(def));
            Assert.AreEqual(30, s.MagazineSize);
        }

        [Test]
        public void ReloadTime_Plus20_IncreasesTime()
        {
            var def = MakeAtt("rl", AttachmentSlot.Magazine, (WeaponStatAxis.ReloadTime, 20f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Magazine, "rl")), RegistryWith(def));
            Assert.AreEqual(2.4f, s.ReloadTime, 1e-4f);
        }

        [Test]
        public void Damage_Plus12_Scales()
        {
            var def = MakeAtt("dmg", AttachmentSlot.Muzzle, (WeaponStatAxis.Damage, 12f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Muzzle, "dmg")), RegistryWith(def));
            Assert.AreEqual(11.2f, s.Damage, 1e-4f);
        }

        [Test]
        public void SightRange_AddsRawMeters_NotPercent()
        {
            // Sniper scope: SightRange is additive meters (base is 0), not a percent multiplier.
            var def = MakeAtt("scope", AttachmentSlot.Optic, (WeaponStatAxis.SightRange, 15f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Optic, "scope")), RegistryWith(def));
            Assert.AreEqual(15f, s.SightRangeBonus, 1e-4f);
        }

        [Test]
        public void ProjectileSpeed_Plus75_Scales()
        {
            var baseStats = Baseline();
            baseStats.ProjectileSpeed = 20f;
            var def = MakeAtt("vel", AttachmentSlot.Optic, (WeaponStatAxis.ProjectileSpeed, 75f));
            var s = WeaponStatComposer.ApplyAttachments(baseStats,
                Config(new AttachmentInstance(AttachmentSlot.Optic, "vel")), RegistryWith(def));
            Assert.AreEqual(35f, s.ProjectileSpeed, 1e-3f); // 20 × 1.75
        }

        [Test]
        public void Headshot_Plus50_Scales()
        {
            var baseStats = Baseline();
            baseStats.HeadshotDamageMultiplier = 2f;
            var def = MakeAtt("hs", AttachmentSlot.Optic, (WeaponStatAxis.Headshot, 50f));
            var s = WeaponStatComposer.ApplyAttachments(baseStats,
                Config(new AttachmentInstance(AttachmentSlot.Optic, "hs")), RegistryWith(def));
            Assert.AreEqual(3f, s.HeadshotDamageMultiplier, 1e-3f); // 2 × 1.5
        }

        [Test]
        public void Spread_Plus10_Widens()
        {
            var def = MakeAtt("sp", AttachmentSlot.Muzzle, (WeaponStatAxis.Spread, 10f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Muzzle, "sp")), RegistryWith(def));
            Assert.AreEqual(4.4f, s.SpreadAngle, 1e-4f);
        }

        [Test]
        public void Recoil_Plus15_ScalesBothKicks()
        {
            var def = MakeAtt("rec", AttachmentSlot.Grip, (WeaponStatAxis.Recoil, 15f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Grip, "rec")), RegistryWith(def));
            Assert.AreEqual(2.3f, s.RecoilKickForward, 1e-4f);
            Assert.AreEqual(1.15f, s.RecoilKickSide, 1e-4f);
        }

        [Test]
        public void RateOfFire_Plus20_LowersFireInterval()
        {
            var def = MakeAtt("rof", AttachmentSlot.Muzzle, (WeaponStatAxis.RateOfFire, 20f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Muzzle, "rof")), RegistryWith(def));
            Assert.AreEqual(0.5f / 1.2f, s.FireInterval, 1e-4f);
        }

        [Test]
        public void Ergonomics_Positive_ImprovesHandling()
        {
            var def = MakeAtt("ergo", AttachmentSlot.Buttstock, (WeaponStatAxis.Ergonomics, 20f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Buttstock, "ergo")), RegistryWith(def));
            Assert.AreEqual(0.4f / 1.2f, s.EquipTime, 1e-4f);     // faster draw
            Assert.AreEqual(0.3f / 1.2f, s.UnequipTime, 1e-4f);   // faster holster
            Assert.AreEqual(240f, s.BodyRotationSpeed, 1e-4f);    // faster turn
        }

        [Test]
        public void Ergonomics_Negative_WorsensHandling()
        {
            var def = MakeAtt("heavy", AttachmentSlot.Buttstock, (WeaponStatAxis.Ergonomics, -10f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Buttstock, "heavy")), RegistryWith(def));
            Assert.AreEqual(0.4f / 0.9f, s.EquipTime, 1e-4f);     // slower draw
            Assert.AreEqual(180f, s.BodyRotationSpeed, 1e-4f);    // slower turn
        }

        // ── Multi-delta + stacking ────────────────────────────

        [Test]
        public void MultiDeltaMod_AppliesAllAxes()
        {
            // Extended-mag style: +Magazine, +ReloadTime, -Ergonomics.
            var def = MakeAtt("extMag", AttachmentSlot.Magazine,
                (WeaponStatAxis.MagazineSize, 50f),
                (WeaponStatAxis.ReloadTime, 20f),
                (WeaponStatAxis.Ergonomics, -10f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Magazine, "extMag")), RegistryWith(def));
            Assert.AreEqual(30, s.MagazineSize);
            Assert.AreEqual(2.4f, s.ReloadTime, 1e-4f);
            Assert.AreEqual(0.4f / 0.9f, s.EquipTime, 1e-4f);
        }

        [Test]
        public void TwoMods_SameAxis_Stack()
        {
            var a = MakeAtt("d1", AttachmentSlot.Muzzle, (WeaponStatAxis.Damage, 10f));
            var b = MakeAtt("d2", AttachmentSlot.Grip,   (WeaponStatAxis.Damage, 10f));
            var s = WeaponStatComposer.ApplyAttachments(Baseline(),
                Config(new AttachmentInstance(AttachmentSlot.Muzzle, "d1"),
                       new AttachmentInstance(AttachmentSlot.Grip,   "d2")),
                RegistryWith(a, b));
            Assert.AreEqual(10f * 1.1f * 1.1f, s.Damage, 1e-4f); // 12.1
        }
    }
}
