using Adapters;
using NUnit.Framework;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class BotCombatSystemTests
    {

        static RaidState CreateStateWithBotWantingToFire(string typeId = "Scav")
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, typeId, new Vector3(0, 0, 10f),
                new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            bot.WantsToFire = true;
            bot.DesiredAimPoint = state.PlayerEntity.Position;
            bot.FacingDirection = -Vector3.forward;
            return state;
        }

        [Test]
        public void Tick_BotWantsToFire_SpawnsProjectile()
        {
            var state = CreateStateWithBotWantingToFire();
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.GreaterOrEqual(state.Projectiles.Count, 1);
        }

        [Test]
        public void Tick_BotDoesNotWantToFire_NoProjectile()
        {
            var state = CreateStateWithBotWantingToFire();
            state.Bots[0].WantsToFire = false;
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_BotWantsToHeal_StartsCastThenHealsByAmount()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            BotSpawnSystem.SpawnBot(state, "PMC", Vector3.zero, new[] { Vector3.zero }, events);

            var bot = state.Bots[0];
            var hp = state.HealthMap[bot.Id];
            hp.CurrentHp = 50f;
            bot.WantsToHeal = true;
            bot.Blackboard.TimeSinceTargetSeen = 5f;
            int medkitsBefore = bot.Blackboard.MedkitsRemaining;
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            // Cast started: medkit committed, HP not applied yet — vulnerability window.
            Assert.AreEqual(medkitsBefore - 1, bot.Blackboard.MedkitsRemaining);
            Assert.GreaterOrEqual(bot.Blackboard.HealCastEndTime, 0f, "Heal cast should be running");
            Assert.AreEqual(50f, hp.CurrentHp, 0.01f, "HP applies at cast end, not instantly");

            // Cast completes: heals by HealAmount (30), not to full.
            bot.WantsToHeal = false;
            state.ElapsedTime = bot.Blackboard.HealCastEndTime + 0.1f;
            BotCombatSystem.Tick(state, in ctx);

            var config = Constants.BotConstants.GetConfig("PMC");
            Assert.AreEqual(50f + config.HealAmount, hp.CurrentHp, 0.01f,
                "Should heal by HealAmount, not snap to full");
            Assert.Less(bot.Blackboard.HealCastEndTime, 0f, "Cast state should clear");
            Assert.AreEqual(0f, bot.Blackboard.TimeSinceTargetSeen,
                "Should reset target memory so bot doesn't lose target after healing");
        }

        [Test]
        public void Tick_EmptyMagazine_StartsReloadInsteadOfFiring()
        {
            var state = CreateStateWithBotWantingToFire();
            var bot = state.Bots[0];
            bot.Weapon.AmmoInMagazine = 0;
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(0, state.Projectiles.Count, "Empty mag must not fire");
            Assert.AreEqual(WeaponPhase.Reloading, bot.Weapon.Phase);

            // Reload completes after ReloadTime → mag refilled, firing resumes.
            state.ElapsedTime = bot.Weapon.PhaseStartTime + bot.Weapon.Stats.ReloadTime + 0.1f;
            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(WeaponPhase.Ready, bot.Weapon.Phase);
            Assert.AreEqual(bot.Weapon.Stats.MagazineSize, bot.Weapon.AmmoInMagazine + state.Projectiles.Count,
                "Mag refills to full, then the queued shot fires");
            Assert.GreaterOrEqual(state.Projectiles.Count, 1, "Bot fires again after reloading");
        }

        [Test]
        public void Tick_FiringConsumesAmmo_AndCountsDownBurst()
        {
            var state = CreateStateWithBotWantingToFire();
            var bot = state.Bots[0];
            int ammoBefore = bot.Weapon.AmmoInMagazine;
            bot.Blackboard.BurstShotsLeft = 1;
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(ammoBefore - 1, bot.Weapon.AmmoInMagazine);
            Assert.AreEqual(0, bot.Blackboard.BurstShotsLeft);
            Assert.Greater(bot.Blackboard.NextBurstTime, state.ElapsedTime,
                "Burst spent → pause before the next burst");
        }

        [Test]
        public void Tick_FireRespectsCooldown()
        {
            var state = CreateStateWithBotWantingToFire();
            state.Bots[0].Weapon.LastFireTime = 0f;
            state.ElapsedTime = 0.1f;
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(0, state.Projectiles.Count,
                "Should not fire during weapon cooldown");
        }

        [Test]
        public void Tick_BossSpawnsMultiplePellets()
        {
            var state = CreateStateWithBotWantingToFire("Boss");
            var ctx = TestContextFactory.Create();

            BotCombatSystem.Tick(state, in ctx);

            Assert.AreEqual(7, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmitsProjectileSpawnedEvents()
        {
            var state = CreateStateWithBotWantingToFire();
            var eventBuffer = new RaidEventBuffer();
            var ctx = TestContextFactory.Create(events: eventBuffer);

            BotCombatSystem.Tick(state, in ctx);

            int count = 0;
            foreach (var e in eventBuffer.All)
                if (e.Type == RaidEventType.ProjectileSpawned) count++;

            Assert.GreaterOrEqual(count, 1);
        }
    }
}
