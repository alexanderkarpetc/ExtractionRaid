using System.Collections.Generic;
using Adapters;
using NUnit.Framework;
using Session;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class DamageSystemTests
    {
        static RaidContext CreateContext(IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: 1f / 60f,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = 1f / 60f },
                input: new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter()
            );
        }

        [Test]
        public void Tick_SelfHit_IgnoresDamage()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();

            state.HealthMap[ownerId] = HealthState.Create(100f);

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal { ProjectileId = projId, TargetId = ownerId, Damage = 25f }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            Assert.AreEqual(100f, state.HealthMap[ownerId].CurrentHp, 0.001f,
                "Owner should not be damaged by own projectile");
        }

        [Test]
        public void Tick_EnemyHit_AppliesDamage()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal { ProjectileId = projId, TargetId = targetId, Damage = 25f }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            Assert.AreEqual(75f, state.HealthMap[targetId].CurrentHp, 0.001f,
                "Enemy should be damaged by projectile");
        }

        // ── Armor Integration ─────────────────────────────────

        [Test]
        public void Tick_TargetWithArmor_DamageReduced()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            state.ArmorMap[targetId] = new ArmorSlotState
            {
                BodyArmor = ArmorState.Create(60f, 200f), // 60 armor pts
            };

            var projId = state.AllocateEId();
            // Pen=30 vs Armor=60 → diff=30 → multi=30/(30+30)=0.5 → 25dmg * 0.5 = 12.5
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            Assert.Less(state.HealthMap[targetId].CurrentHp, 100f, "Should take some damage");
            Assert.Greater(state.HealthMap[targetId].CurrentHp, 75f, "Should take less than unarmored");
            Assert.AreEqual(87.5f, state.HealthMap[targetId].CurrentHp, 0.5f);
        }

        [Test]
        public void Tick_TargetWithArmor_DurabilityDecreased()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            var bodyArmor = ArmorState.Create(60f, 200f);
            state.ArmorMap[targetId] = new ArmorSlotState { BodyArmor = bodyArmor };

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            Assert.Less(bodyArmor.CurrentDurability, 200f, "Armor durability should decrease");
        }

        [Test]
        public void Tick_HeadshotWithHelmet_UsesHelmetArmor()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            state.ArmorMap[targetId] = new ArmorSlotState
            {
                Helmet = ArmorState.Create(80f, 100f),    // high helmet
                BodyArmor = ArmorState.Create(30f, 120f), // low vest
            };

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, headshotDamageMultiplier: 2f,
                penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            // Headshot: TargetedEntityId == TargetId
            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                    TargetedEntityId = targetId, // headshot
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            // Headshot: 25*2=50 raw → helmet 80pts, pen 30 → diff=50, multi=30/80=0.375 → 50*0.375=18.75
            Assert.AreEqual(81.25f, state.HealthMap[targetId].CurrentHp, 1f);
        }

        [Test]
        public void Tick_BodyshotWithVest_UsesVestArmor()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            state.ArmorMap[targetId] = new ArmorSlotState
            {
                Helmet = ArmorState.Create(80f, 100f),
                BodyArmor = ArmorState.Create(30f, 120f), // low vest
            };

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            // Bodyshot: TargetedEntityId != TargetId (default EId)
            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            // Bodyshot: vest 30pts, pen 30 → diff=0 → multi=1.0 → full 25 damage
            Assert.AreEqual(75f, state.HealthMap[targetId].CurrentHp, 0.5f);
        }

        [Test]
        public void Tick_HeadshotNoHelmet_FullHeadshotDamage()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            state.ArmorMap[targetId] = new ArmorSlotState
            {
                Helmet = null,                             // no helmet!
                BodyArmor = ArmorState.Create(60f, 200f),  // has vest
            };

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, headshotDamageMultiplier: 2f,
                penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                    TargetedEntityId = targetId, // headshot
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            // Headshot 25*2=50, no helmet → full 50 damage, HP=50
            Assert.AreEqual(50f, state.HealthMap[targetId].CurrentHp, 0.5f,
                "Headshot without helmet should deal full headshot damage");
        }

        [Test]
        public void Tick_MultiHit_ArmorDegradesMakesSubsequentHitsStronger()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(200f);
            var bodyArmor = ArmorState.Create(60f, 50f); // low durability!
            state.ArmorMap[targetId] = new ArmorSlotState { BodyArmor = bodyArmor };

            float hpBefore = 200f;
            float firstHitDmg = 0f;

            // Fire 2 identical shots
            for (int i = 0; i < 2; i++)
            {
                var projId = state.AllocateEId();
                var projectile = ProjectileEntityState.Create(
                    projId, ownerId, Vector3.zero, Vector3.forward,
                    20f, 0f, 3f, 30f, penetration: 30f, armorDamage: 20f);
                state.Projectiles.Add(projectile);

                var hits = new List<HitSignal>
                {
                    new HitSignal
                    {
                        ProjectileId = projId, TargetId = targetId,
                        Damage = 30f, Penetration = 30f, ArmorDamage = 20f,
                    }
                };

                var context = CreateContext();
                DamageSystem.Tick(state, hits, in context);

                float hpAfter = state.HealthMap[targetId].CurrentHp;
                float dmgThisHit = hpBefore - hpAfter;

                if (i == 0) firstHitDmg = dmgThisHit;
                if (i == 1)
                {
                    Assert.Greater(dmgThisHit, firstHitDmg,
                        "Second hit should deal more damage as armor durability degraded");
                }

                hpBefore = hpAfter;
            }
        }

        [Test]
        public void Tick_BrokenArmor_FullDamage()
        {
            var state = RaidState.Create();
            var ownerId = state.AllocateEId();
            var targetId = state.AllocateEId();

            state.HealthMap[targetId] = HealthState.Create(100f);
            var bodyArmor = ArmorState.Create(60f, 200f);
            bodyArmor.CurrentDurability = 0f; // broken!
            state.ArmorMap[targetId] = new ArmorSlotState { BodyArmor = bodyArmor };

            var projId = state.AllocateEId();
            var projectile = ProjectileEntityState.Create(
                projId, ownerId, Vector3.zero, Vector3.forward,
                20f, 0f, 3f, 25f, penetration: 30f, armorDamage: 10f);
            state.Projectiles.Add(projectile);

            var hits = new List<HitSignal>
            {
                new HitSignal
                {
                    ProjectileId = projId, TargetId = targetId,
                    Damage = 25f, Penetration = 30f, ArmorDamage = 10f,
                }
            };

            var context = CreateContext();
            DamageSystem.Tick(state, hits, in context);

            Assert.AreEqual(75f, state.HealthMap[targetId].CurrentHp, 0.001f,
                "Broken armor should provide no protection");
        }
    }
}
