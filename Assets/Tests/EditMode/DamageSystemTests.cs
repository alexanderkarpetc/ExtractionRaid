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
    }
}
