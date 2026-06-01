using Adapters;
using Constants;
using NUnit.Framework;
using Session;
using State;
using Systems;
using Systems.Bot;
using Systems.Bot.BT;
using Systems.Bot.Nodes;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Coverage for Horde-mode melee path: <see cref="MeleeAttackNode"/> gating +
    /// <see cref="DamageSystem.ApplyMeleeDamage"/> direct-damage application.
    /// </summary>
    [TestFixture]
    public class MeleeAttackTests
    {
        // ── MeleeAttackNode ──────────────────────────────────────

        [Test]
        public void MeleeAttackNode_NoTarget_Failure()
        {
            var bot = MakeBot();
            bot.Blackboard.HasTarget = false;

            var node = new MeleeAttackNode();
            var ctx  = TestContextFactory.Create();
            var status = node.Tick(bot, new RaidState(), in ctx, in BotConstants.Zombie);

            Assert.AreEqual(BTStatus.Failure, status);
            Assert.IsFalse(bot.WantsToMeleeAttack);
        }

        [Test]
        public void MeleeAttackNode_OutOfRange_Failure()
        {
            var bot = MakeBot();
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.LastKnownTargetPos = new Vector3(5f, 0f, 0f);
            bot.Blackboard.DistanceToTarget = 5f; // > Zombie.MeleeAttackRadius (1.6)

            var node = new MeleeAttackNode();
            var ctx  = TestContextFactory.Create();
            var status = node.Tick(bot, new RaidState(), in ctx, in BotConstants.Zombie);

            Assert.AreEqual(BTStatus.Failure, status);
            Assert.IsFalse(bot.WantsToMeleeAttack);
        }

        [Test]
        public void MeleeAttackNode_InRange_SuccessAndSetsIntent()
        {
            var bot = MakeBot();
            bot.Blackboard.HasTarget = true;
            bot.Blackboard.LastKnownTargetPos = new Vector3(1f, 0f, 0f);
            bot.Blackboard.DistanceToTarget = 1f; // < Zombie.MeleeAttackRadius (1.6)

            var node = new MeleeAttackNode();
            var ctx  = TestContextFactory.Create();
            var status = node.Tick(bot, new RaidState(), in ctx, in BotConstants.Zombie);

            Assert.AreEqual(BTStatus.Success, status);
            Assert.IsTrue(bot.WantsToMeleeAttack);
            Assert.AreEqual(Vector3.zero, bot.DesiredVelocity); // plant feet to swing
        }

        // ── DamageSystem.ApplyMeleeDamage ───────────────────────

        [Test]
        public void ApplyMeleeDamage_AlivePlayer_DropsHpAndEmitsEvents()
        {
            var state  = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            var ctx    = TestContextFactory.Create(events: events);
            var pid    = state.PlayerEntity.Id;
            state.HealthMap[pid] = HealthState.Create(BotConstants.PlayerMaxHp);
            var hp     = state.HealthMap[pid];
            float startHp = hp.CurrentHp;

            DamageSystem.ApplyMeleeDamage(state, pid, damage: 25f,
                attackerId: default, hitPoint: Vector3.zero,
                hitDirection: Vector3.forward, in ctx);

            Assert.AreEqual(startHp - 25f, hp.CurrentHp, 0.001f);
            Assert.IsTrue(hp.IsAlive);
            Assert.AreEqual(1, events.EntityHits.Count);
            Assert.IsFalse(events.EntityDiedCalled);
        }

        [Test]
        public void ApplyMeleeDamage_GodModePlayer_ZeroHpButStillEmitsFeedback()
        {
            // Visual passthrough: GodMode zeroes HP loss on the player but melee must still emit
            // EntityHit so horde-zombie feedback (flash / vignette / flinch) shows during GodMode
            // playtest — mirrors the projectile branch. Regression guard for the 2026-06-01 fix.
            var state  = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            var cheats = CheatsConfig.Default;
            cheats.GodMode = true;
            var ctx    = TestContextFactory.Create(events: events, cheatsConfig: cheats);
            var pid    = state.PlayerEntity.Id;
            state.HealthMap[pid] = HealthState.Create(BotConstants.PlayerMaxHp);
            var hp     = state.HealthMap[pid];
            float startHp = hp.CurrentHp;

            DamageSystem.ApplyMeleeDamage(state, pid, damage: 25f,
                attackerId: default, hitPoint: Vector3.zero,
                hitDirection: Vector3.forward, in ctx);

            Assert.AreEqual(startHp, hp.CurrentHp, 0.001f); // GodMode → no HP loss
            Assert.IsTrue(hp.IsAlive);
            Assert.AreEqual(1, events.EntityHits.Count);    // …but feedback still fires
        }

        [Test]
        public void ApplyMeleeDamage_LethalDamage_EmitsDied()
        {
            var state  = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            var ctx    = TestContextFactory.Create(events: events);
            var pid    = state.PlayerEntity.Id;
            state.HealthMap[pid] = HealthState.Create(BotConstants.PlayerMaxHp);

            DamageSystem.ApplyMeleeDamage(state, pid, damage: 99999f,
                attackerId: default, hitPoint: Vector3.zero,
                hitDirection: Vector3.forward, in ctx);

            Assert.IsFalse(state.HealthMap[pid].IsAlive);
            Assert.IsTrue(events.EntityDiedCalled);
            Assert.AreEqual(pid, events.EntityDiedId);
        }

        [Test]
        public void ApplyMeleeDamage_AlreadyDead_NoOp()
        {
            var state  = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var events = new FakeRaidEvents();
            var ctx    = TestContextFactory.Create(events: events);
            var pid    = state.PlayerEntity.Id;
            var hp     = HealthState.Create(BotConstants.PlayerMaxHp);
            hp.CurrentHp = 0f;
            hp.IsAlive   = false;
            state.HealthMap[pid] = hp;

            DamageSystem.ApplyMeleeDamage(state, pid, damage: 10f,
                attackerId: default, hitPoint: Vector3.zero,
                hitDirection: Vector3.forward, in ctx);

            Assert.AreEqual(0, events.EntityHits.Count);
        }

        // ── helpers ─────────────────────────────────────────────

        static BotEntityState MakeBot()
        {
            return BotEntityState.Create(new EId(1), "Zombie", Vector3.zero,
                new[] { Vector3.zero });
        }
    }
}
