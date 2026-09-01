using NUnit.Framework;
using Progression;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ProgressionSystemTests
    {
        ProgressionTreeConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ProgressionTreeDefaults.BuildRuntime();

        [TearDown]
        public void TearDown()
        {
            if (_cfg != null) Object.DestroyImmediate(_cfg);
        }

        [Test]
        public void ApplyAllocatedEffects_AggregatesPredatorCombatNodes()
        {
            var state = new PlayerProgressionState();
            state.AllocatedNodeIds.AddRange(new[]
            {
                "predator.0.0", // damage +6%
                "predator.0.1", // penetration +8%
                "predator.0.2", // armor damage +15%
                "predator.0.3", // headshot +45%
                "predator.0.4", // damage +6%
                "predator.1.0", // recoil -15%
                "predator.1.1", // recovery +25%
                "predator.1.2", // reload -15%
                "predator.1.4", // equip -20%
                "predator.1.5", // heat -35%
                "predator.2.0", // max HP +8
                "predator.2.2", // bleed +25%
                "predator.2.4", // max HP +10
            });

            var result = ProgressionSystem.ApplyAllocatedEffects(_cfg, state);

            Assert.AreEqual(1.12f, result.WeaponDamageMultiplier, 0.001f);
            Assert.AreEqual(1.08f, result.PenetrationMultiplier, 0.001f);
            Assert.AreEqual(1.15f, result.ArmorDamageMultiplier, 0.001f);
            Assert.AreEqual(1.45f, result.HeadshotDamageMultiplier, 0.001f);
            Assert.AreEqual(0.85f, result.RecoilMultiplier, 0.001f);
            Assert.AreEqual(1.25f, result.RecoilRecoveryMultiplier, 0.001f);
            Assert.AreEqual(0.85f, result.ReloadTimeMultiplier, 0.001f);
            Assert.AreEqual(0.80f, result.EquipTimeMultiplier, 0.001f);
            Assert.AreEqual(0.65f, result.HeatBuildupMultiplier, 0.001f);
            Assert.AreEqual(1.25f, result.BleedAppliedMultiplier, 0.001f);
            Assert.AreEqual(18f, result.MaxHpBonus, 0.001f);
        }

        [Test]
        public void ApplyAllocatedEffects_LegacyAssetWithoutTypedEffect_UsesLabelFallback()
        {
            _cfg.TryFind("predator.0.0", out _, out _, out var node);
            node.Effect = ProgressionEffectType.None;
            var state = new PlayerProgressionState();
            state.AllocatedNodeIds.Add("predator.0.0");

            var result = ProgressionSystem.ApplyAllocatedEffects(_cfg, state);

            Assert.AreEqual(1.06f, result.WeaponDamageMultiplier, 0.001f);
        }

        [Test]
        public void ApplyAllocatedEffects_DoesNotApplySameLabelFromAnotherDiscipline()
        {
            var state = new PlayerProgressionState();
            state.AllocatedNodeIds.Add("warden.0.0"); // Max HP +10

            var result = ProgressionSystem.ApplyAllocatedEffects(_cfg, state);

            Assert.AreEqual(0f, result.MaxHpBonus, 0.001f);
        }

        [Test]
        public void SyncMaxHp_FullHealth_IncreasesCurrentAndMaximum()
        {
            var health = HealthState.Create(100f);

            bool changed = ProgressionSystem.SyncMaxHp(health, 108f);

            Assert.IsTrue(changed);
            Assert.AreEqual(108f, health.CurrentHp, 0.001f);
            Assert.AreEqual(108f, health.MaxHp, 0.001f);
        }

        [Test]
        public void SyncMaxHp_Damaged_PreservesHealthRatio()
        {
            var health = HealthState.Create(100f);
            health.CurrentHp = 50f;

            ProgressionSystem.SyncMaxHp(health, 108f);

            Assert.AreEqual(54f, health.CurrentHp, 0.001f);
            Assert.AreEqual(108f, health.MaxHp, 0.001f);
        }

        [Test]
        public void SyncMaxHp_Dead_RemainsDead()
        {
            var health = HealthState.Create(100f);
            health.CurrentHp = 0f;
            health.IsAlive = false;

            ProgressionSystem.SyncMaxHp(health, 108f);

            Assert.AreEqual(0f, health.CurrentHp, 0.001f);
            Assert.AreEqual(108f, health.MaxHp, 0.001f);
            Assert.IsFalse(health.IsAlive);
        }
    }
}
