using System;
using System.Collections.Generic;
using NUnit.Framework;
using State;
using Session;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class DamageSystemTests
    {
        // ── Scenario DSL ──────────────────────────────────────
        //
        // Every test follows the same shape:
        //   1. Build a RaidState with owner + target + HP + (optional) armor.
        //   2. Spawn a projectile from owner with the hit's Damage/Pen/ArmorDamage/Bleed.
        //   3. Queue a HitSignal targeting either the target (bodyshot) or TargetedEntityId==targetId (headshot).
        //   4. Tick DamageSystem (optionally with a deterministic rand).
        //
        // `Scenario` encapsulates all of that; `Setup(...)` builds one ready to fire,
        // and `Scenario.Fire`/`Refire` drive the actual tick. Tests that need multiple
        // hits against the same target use `Refire(...)`.

        sealed class Scenario
        {
            public RaidState State;
            public EId OwnerId;
            public EId TargetId;
            public List<HitSignal> Hits;
            public RaidContext Context;
            public FakeRaidEvents Events;
            public ArmorState Helmet;
            public ArmorState Body;

            public float Hp => State.HealthMap[TargetId].CurrentHp;

            public void Fire(Func<float> rand = null)
            {
                if (rand == null) DamageSystem.Tick(State, Hits, in Context);
                else DamageSystem.Tick(State, Hits, in Context, rand);
            }

            public void Refire(float damage = 25f, float pen = 30f, float armorDmg = 10f,
                bool isHeadshot = false, float headshotMul = 1f, float bleedChance = 0f,
                Func<float> rand = null)
            {
                QueueHit(damage, pen, armorDmg, isHeadshot, headshotMul, bleedChance);
                Fire(rand);
            }

            internal void QueueHit(float damage, float pen, float armorDmg,
                bool isHeadshot, float headshotMul, float bleedChance)
            {
                var projId = State.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projId, OwnerId, Vector3.zero, Vector3.forward,
                    20f, 0f, 3f, damage,
                    headshotDamageMultiplier: headshotMul,
                    penetration: pen, armorDamage: armorDmg, bleedChance: bleedChance);
                State.Projectiles.Add(projectile);

                Hits.Clear();
                Hits.Add(new HitSignal
                {
                    ProjectileId     = projId,
                    TargetId         = TargetId,
                    Damage           = damage,
                    Penetration      = pen,
                    ArmorDamage      = armorDmg,
                    BleedChance      = bleedChance,
                    TargetedEntityId = isHeadshot ? TargetId : default,
                });
            }
        }

        /// <summary>
        /// Armor spec shortcut. Pass 0 to omit a piece. <paramref name="helmetDur"/> /
        /// <paramref name="bodyDur"/> default to the respective max (fresh armor).
        /// </summary>
        static ArmorSlotState Armor(
            float helmet = 0f, float helmetMaxDur = 100f, float helmetDur = -1f,
            float body   = 0f, float bodyMaxDur   = 100f, float bodyDur   = -1f)
        {
            var slot = new ArmorSlotState();
            if (helmet > 0f)
            {
                slot.Helmet = ArmorState.Create(helmet, helmetMaxDur);
                if (helmetDur >= 0f) slot.Helmet.CurrentDurability = helmetDur;
            }
            if (body > 0f)
            {
                slot.BodyArmor = ArmorState.Create(body, bodyMaxDur);
                if (bodyDur >= 0f) slot.BodyArmor.CurrentDurability = bodyDur;
            }
            return slot;
        }

        static Scenario Setup(
            float targetHp = 100f,
            ArmorSlotState armor = null,
            float damage = 25f,
            float pen = 30f,
            float armorDmg = 10f,
            bool isHeadshot = false,
            float headshotMul = 1f,
            float bleedChance = 0f,
            bool selfHit = false)
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var ownerId = state.AllocateEId();
            var targetId = selfHit ? ownerId : state.AllocateEId();
            state.HealthMap[targetId] = HealthState.Create(targetHp);

            ArmorState helmet = null, body = null;
            if (armor != null && (armor.Helmet != null || armor.BodyArmor != null))
            {
                state.ArmorMap[targetId] = armor;
                helmet = armor.Helmet;
                body   = armor.BodyArmor;
            }

            var events = new FakeRaidEvents();
            var scenario = new Scenario
            {
                State    = state,
                OwnerId  = ownerId,
                TargetId = targetId,
                Hits     = new List<HitSignal>(),
                Context  = TestContextFactory.Create(events: events),
                Events   = events,
                Helmet   = helmet,
                Body     = body,
            };
            scenario.QueueHit(damage, pen, armorDmg, isHeadshot, headshotMul, bleedChance);
            return scenario;
        }

        // ── Basic damage ──────────────────────────────────────

        [Test]
        public void Tick_SelfHit_IgnoresDamage()
        {
            var s = Setup(selfHit: true, damage: 25f, pen: 0f, armorDmg: 0f);
            s.Fire();
            Assert.AreEqual(100f, s.Hp, 0.001f, "Owner should not be damaged by own projectile");
        }

        [Test]
        public void Tick_EnemyHit_AppliesDamage()
        {
            var s = Setup(damage: 25f, pen: 0f, armorDmg: 0f);
            s.Fire();
            Assert.AreEqual(75f, s.Hp, 0.001f, "Enemy should be damaged by projectile");
        }

        [Test]
        public void ApplyPlayerTestDamage_DamagesPlayerAndPublishesHealth()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, Vector3.zero);
            state.HealthMap[playerId] = HealthState.Create(100f);
            var events = new FakeRaidEvents();

            bool applied = DamageSystem.ApplyPlayerTestDamage(state, 25f, events);

            Assert.IsTrue(applied);
            Assert.AreEqual(75f, state.HealthMap[playerId].CurrentHp, 0.001f);
            Assert.IsTrue(events.EntityDamagedCalled);
            Assert.AreEqual(playerId, events.EntityDamagedId);
        }

        [Test]
        public void ApplyPlayerTestDamage_ClampsAtOneHpAndNeverKillsPlayer()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, Vector3.zero);
            state.HealthMap[playerId] = HealthState.Create(10f);
            var events = new FakeRaidEvents();

            bool firstApplied = DamageSystem.ApplyPlayerTestDamage(state, 25f, events);
            bool secondApplied = DamageSystem.ApplyPlayerTestDamage(state, 25f, events);

            Assert.IsTrue(firstApplied);
            Assert.IsFalse(secondApplied);
            Assert.AreEqual(1f, state.HealthMap[playerId].CurrentHp, 0.001f);
            Assert.IsTrue(state.HealthMap[playerId].IsAlive);
            Assert.IsFalse(events.EntityDiedCalled);
        }

        // ── Armor Integration ─────────────────────────────────

        [Test]
        public void Tick_TargetWithArmor_DamageReduced()
        {
            // Pen=30 vs Armor=60 → diff=30 → multi=30/(30+30)=0.5 → 25dmg * 0.5 = 12.5
            var s = Setup(armor: Armor(body: 60f, bodyMaxDur: 200f));
            s.Fire();

            Assert.Less(s.Hp, 100f, "Should take some damage");
            Assert.Greater(s.Hp, 75f, "Should take less than unarmored");
            Assert.AreEqual(87.5f, s.Hp, 0.5f);
        }

        [Test]
        public void Tick_TargetWithArmor_DurabilityDecreased()
        {
            var s = Setup(armor: Armor(body: 60f, bodyMaxDur: 200f));
            s.Fire();

            Assert.Less(s.Body.CurrentDurability, 200f, "Armor durability should decrease");
        }

        [Test]
        public void Tick_HeadshotWithHelmet_UsesHelmetArmor()
        {
            // Headshot: 25*2=50 raw → helmet 80pts, pen 30 → diff=50, multi=30/80=0.375 → 50*0.375=18.75
            var s = Setup(
                armor:       Armor(helmet: 80f, body: 30f, bodyMaxDur: 120f),
                isHeadshot:  true, headshotMul: 2f);
            s.Fire(() => 1f); // roll=1 → no ricochet

            Assert.AreEqual(81.25f, s.Hp, 1f);
        }

        [Test]
        public void Tick_BodyshotWithVest_UsesVestArmor()
        {
            // Bodyshot: vest 30pts, pen 30 → diff=0 → multi=1.0 → full 25 damage
            var s = Setup(armor: Armor(helmet: 80f, body: 30f, bodyMaxDur: 120f));
            s.Fire();

            Assert.AreEqual(75f, s.Hp, 0.5f);
        }

        [Test]
        public void Tick_HeadshotNoHelmet_FullHeadshotDamage()
        {
            // Headshot 25*2=50, no helmet → full 50 damage, HP=50
            var s = Setup(
                armor:      Armor(body: 60f, bodyMaxDur: 200f),
                isHeadshot: true, headshotMul: 2f);
            s.Fire();

            Assert.AreEqual(50f, s.Hp, 0.5f,
                "Headshot without helmet should deal full headshot damage");
        }

        [Test]
        public void Tick_MultiHit_ArmorDegradesMakesSubsequentHitsStronger()
        {
            var s = Setup(
                targetHp: 200f,
                armor:    Armor(body: 60f, bodyMaxDur: 50f), // low durability
                damage:   30f, pen: 30f, armorDmg: 20f);

            float hpBefore = 200f;
            s.Fire();
            float firstHitDmg = hpBefore - s.Hp;
            hpBefore = s.Hp;

            s.Refire(damage: 30f, pen: 30f, armorDmg: 20f);
            float secondHitDmg = hpBefore - s.Hp;

            Assert.Greater(secondHitDmg, firstHitDmg,
                "Second hit should deal more damage as armor durability degraded");
        }

        [Test]
        public void Tick_BrokenArmor_FullDamage()
        {
            var s = Setup(armor: Armor(body: 60f, bodyMaxDur: 200f, bodyDur: 0f));
            s.Fire();

            Assert.AreEqual(75f, s.Hp, 0.001f, "Broken armor should provide no protection");
        }

        // ── Armor Break Events ────────────────────────────────

        [Test]
        public void Tick_ArmorBreaksDuringHit_EventFired()
        {
            var s = Setup(armor: Armor(body: 60f, bodyMaxDur: 200f, bodyDur: 1f)); // about to break
            s.Fire();

            Assert.IsTrue(s.Events.ArmorBrokenCalled, "ArmorBroken event should fire");
            Assert.AreEqual(s.TargetId, s.Events.ArmorBrokenEntityId);
            Assert.IsFalse(s.Events.ArmorBrokenIsHelmet, "Should be body armor, not helmet");
        }

        [Test]
        public void Tick_BodyshotNeverRicochets()
        {
            // Very strong body armor + low-pen ammo — ricochet must not fire for bodyshots.
            var s = Setup(
                armor:   Armor(body: 90f, bodyMaxDur: 200f),
                damage:  5f, pen: 10f, armorDmg: 2f);

            // Force ricochet-roll favourable every shot; still must be rejected because bodyshot.
            for (int i = 0; i < 5; i++)
                s.Refire(damage: 5f, pen: 10f, armorDmg: 2f, rand: () => 0.01f);

            Assert.IsFalse(s.Events.RicochetCalled, "Bodyshots should never ricochet");
            Assert.Less(s.Hp, 100f, "Should still deal some damage");
        }

        [Test]
        public void Tick_HeadshotHelmetBreakEvent_IsHelmetTrue()
        {
            var s = Setup(
                armor:       Armor(helmet: 30f, helmetMaxDur: 200f, helmetDur: 1f),
                pen:         50f, armorDmg: 10f,
                isHeadshot:  true, headshotMul: 2f);
            s.Fire();

            Assert.IsTrue(s.Events.ArmorBrokenCalled, "ArmorBroken should fire for helmet");
            Assert.IsTrue(s.Events.ArmorBrokenIsHelmet, "Should be helmet");
        }

        // ── Ricochet Integration (deterministic) ──────────────

        [Test]
        public void Tick_HeadshotRicochet_ZeroHpDamage()
        {
            // pen 20 < armor 60 → ricochet eligible; roll 0.1 < chance 0.4 → forced ricochet.
            var s = Setup(
                armor:       Armor(helmet: 60f, helmetMaxDur: 200f),
                pen:         20f, armorDmg: 10f,
                isHeadshot:  true, headshotMul: 2f);
            s.Fire(() => 0.1f);

            Assert.AreEqual(100f, s.Hp, 0.001f, "Ricochet should deal 0 HP damage");
            Assert.IsTrue(s.Events.RicochetCalled, "Ricochet event should fire");
        }

        [Test]
        public void Tick_HeadshotRicochet_DurabilityDamageApplied()
        {
            // ArmorDmg = 10 × (1 + 1.0) = 20 durability damage; helmet starts at 200.
            var s = Setup(
                armor:       Armor(helmet: 60f, helmetMaxDur: 200f),
                pen:         20f, armorDmg: 10f,
                isHeadshot:  true, headshotMul: 2f);
            s.Fire(() => 0.1f);

            Assert.AreEqual(180f, s.Helmet.CurrentDurability, 0.5f,
                "Ricochet should apply full durability damage (2x base)");
        }

        [Test]
        public void Tick_HeadshotRicochet_ProjectileRemoved()
        {
            var s = Setup(
                armor:       Armor(helmet: 60f, helmetMaxDur: 200f),
                pen:         20f, armorDmg: 10f,
                isHeadshot:  true, headshotMul: 2f);
            s.Fire(() => 0.1f);

            Assert.AreEqual(0, s.State.Projectiles.Count, "Projectile should be removed after ricochet");
        }

        [Test]
        public void Tick_RicochetBreaksHelmet_BothEventsFired()
        {
            // Helmet in safe zone (dur 83%) → effective armor = full 60 pts.
            // Low absolute dur means ArmorDmg (15 × 2 = 30) will break it (25 - 30 → 0).
            var s = Setup(
                armor:       Armor(helmet: 60f, helmetMaxDur: 30f, helmetDur: 25f),
                pen:         5f, armorDmg: 15f,
                isHeadshot:  true, headshotMul: 2f);
            s.Fire(() => 0.1f);

            Assert.AreEqual(100f, s.Hp, 0.001f,
                "Still 0 HP damage on ricochet even when helmet breaks");
            Assert.IsTrue(s.Events.ArmorBrokenCalled, "ArmorBroken should fire");
            Assert.IsTrue(s.Events.RicochetCalled, "Ricochet should fire");
            Assert.IsTrue(s.Helmet.IsBroken, "Helmet should be broken");
        }

        // ── Bleed Roll ────────────────────────────────────────

        [Test]
        public void Tick_WithBleedChance_RollBelow_AppliesBleeding()
        {
            var s = Setup(pen: 0f, armorDmg: 0f, bleedChance: 0.3f);
            s.Fire(() => 0.1f); // roll 0.1 < 0.3

            Assert.IsTrue(StatusEffectSystem.HasEffect(s.State, s.TargetId, StatusEffectType.Bleeding),
                "Should apply bleeding when roll < bleedChance");
        }

        [Test]
        public void Tick_WithBleedChance_RollAbove_NoBleeding()
        {
            var s = Setup(pen: 0f, armorDmg: 0f, bleedChance: 0.3f);
            s.Fire(() => 0.5f); // roll 0.5 > 0.3

            Assert.IsFalse(StatusEffectSystem.HasEffect(s.State, s.TargetId, StatusEffectType.Bleeding),
                "Should NOT apply bleeding when roll > bleedChance");
        }

        [Test]
        public void Tick_ZeroBleedChance_NoBleeding()
        {
            var s = Setup(pen: 0f, armorDmg: 0f, bleedChance: 0f);
            s.Fire(() => 0.01f); // very low roll

            Assert.IsFalse(StatusEffectSystem.HasEffect(s.State, s.TargetId, StatusEffectType.Bleeding),
                "Zero bleedChance should never cause bleeding");
        }
    }
}
