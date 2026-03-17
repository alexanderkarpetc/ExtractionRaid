using Adapters;
using Constants;
using NUnit.Framework;
using Session;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class BotHealTests
    {
        static RaidContext CreateContext(float dt = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: dt,
                events: new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = dt },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        static (RaidState state, BotEntityState bot) CreatePMCSafe(
            float hpRatio, float elapsedTime, float lastDamageTime = -999f)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "PMC", new Vector3(0, 0, 20f),
                new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            var hp = state.HealthMap[bot.Id];
            hp.CurrentHp = hp.MaxHp * hpRatio;

            bot.Blackboard.HasTarget = true;
            bot.Blackboard.CanSeeTarget = false;
            bot.Blackboard.DistanceToTarget = 15f;
            bot.Blackboard.LastDamageTime = lastDamageTime;
            state.ElapsedTime = elapsedTime;

            return (state, bot);
        }

        // ── Emergency heal ────────────────────────────────────────

        [Test]
        public void EmergencyHeal_CriticalHp_NoDamageRecently_Heals()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToHeal);
            Assert.AreEqual("Emergency Heal", bot.Blackboard.DebugStatus);
        }

        [Test]
        public void EmergencyHeal_DamageTooRecent_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 9.5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not emergency heal if damage was less than 1.5s ago");
        }

        // ── Normal heal ──────────────────────────────────────────

        [Test]
        public void NormalHeal_ModerateHp_SafeWindow_Heals()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToHeal);
            Assert.AreEqual("Heal", bot.Blackboard.DebugStatus);
        }

        [Test]
        public void NormalHeal_DamageTooRecent_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 8f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not normal heal if damage was less than 3s ago");
        }

        [Test]
        public void NormalHeal_CanSeeTarget_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Blackboard.CanSeeTarget = true;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not normal heal with visible enemy");
        }

        [Test]
        public void NormalHeal_EnemyTooClose_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Blackboard.DistanceToTarget = 5f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not heal when enemy is within 10m");
        }

        [Test]
        public void NormalHeal_Reloading_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Weapon.Phase = WeaponPhase.Reloading;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not heal while reloading");
        }

        [Test]
        public void NormalHeal_HpAboveThreshold_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.6f, elapsedTime: 10f, lastDamageTime: 5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not heal when HP is above threshold");
        }

        // ── No target = safe ─────────────────────────────────────

        [Test]
        public void Heal_NoTarget_SafeToHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Blackboard.HasTarget = false;
            bot.Blackboard.DistanceToTarget = float.MaxValue;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsTrue(bot.WantsToHeal,
                "Bot with no target should be considered safe to heal");
        }

        // ── Cooldown ─────────────────────────────────────────────

        [Test]
        public void Heal_OnCooldown_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Blackboard.HealCooldownTimer = 5f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not heal while on cooldown");
        }

        [Test]
        public void EmergencyHeal_SetsShorterCooldown()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            var config = BotConstants.GetConfig("PMC");
            Assert.AreEqual(config.EmergencyHealCooldown, bot.Blackboard.HealCooldownTimer, 0.01f,
                "Emergency heal should set shorter cooldown");
        }

        [Test]
        public void NormalHeal_SetsFullCooldown()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.4f, elapsedTime: 10f, lastDamageTime: 5f);
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            var config = BotConstants.GetConfig("PMC");
            Assert.AreEqual(config.HealCooldown, bot.Blackboard.HealCooldownTimer, 0.01f,
                "Normal heal should set full cooldown");
        }

        // ── HP alone is not enough ───────────────────────────────

        [Test]
        public void LowHp_ActiveCombat_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 9.9f);
            bot.Blackboard.CanSeeTarget = true;
            bot.Blackboard.DistanceToTarget = 5f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Low HP alone should never trigger healing during active combat");
        }

        // ── Medkit requirement ────────────────────────────────────

        [Test]
        public void Heal_NoMedkitsRemaining_DoesNotHeal()
        {
            var (state, bot) = CreatePMCSafe(hpRatio: 0.2f, elapsedTime: 10f, lastDamageTime: 5f);
            bot.Blackboard.MedkitsRemaining = 0;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal,
                "Should not heal without medkits");
        }

        // ── Scav cannot heal ─────────────────────────────────────

        [Test]
        public void Scav_CannotHeal()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, new[] { Vector3.zero }, events);
            var bot = state.Bots[0];
            state.HealthMap[bot.Id].CurrentHp = 10f;
            state.ElapsedTime = 100f;
            var ctx = CreateContext();

            BotBrainSystem.Tick(state, in ctx);

            Assert.IsFalse(bot.WantsToHeal, "Scav should not be able to heal");
        }
    }
}
