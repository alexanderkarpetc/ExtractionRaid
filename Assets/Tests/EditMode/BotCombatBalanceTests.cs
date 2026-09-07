using System.Collections.Generic;
using Constants;
using NUnit.Framework;
using State;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class BotCombatBalanceTests
    {
        [TestCase("Scav", 6, 3)]
        [TestCase("RegularBots", 6, 3)]
        public void StarterPistol_KillsRegularScavInExpectedHits(
            string configName, int expectedBodyHits, int expectedHeadHits)
        {
            var configAsset = Resources.Load<BotTypeConfigAsset>($"Configs/Bots/{configName}");
            var payload = Resources.Load<PayloadCoreDefinition>(
                "WeaponBuilder/Payloads/BallisticRound");
            var delivery = Resources.Load<DeliveryCoreDefinition>(
                "WeaponBuilder/Deliveries/SingleAction");

            Assert.NotNull(configAsset);
            Assert.NotNull(payload);
            Assert.NotNull(delivery);

            var stats = WeaponStatComposer.Compose(
                payload, RarityTier.Common, delivery, RarityTier.Common);
            var ammo = ItemDefinition.Get(payload.AmmoType);
            float damage = stats.Damage + (ammo?.DamageModifier ?? 0f);
            float penetration = stats.BasePenetration + (ammo?.Penetration ?? 0f);
            float armorDamage = stats.BaseArmorDamage + (ammo?.ArmorDamage ?? 0f);

            Assert.AreEqual(expectedBodyHits, HitsToKill(
                configAsset.ToBotTypeConfig(), stats, damage, penetration, armorDamage,
                isHeadshot: false));
            Assert.AreEqual(expectedHeadHits, HitsToKill(
                configAsset.ToBotTypeConfig(), stats, damage, penetration, armorDamage,
                isHeadshot: true));
        }

        static int HitsToKill(BotTypeConfig config, WeaponStats stats,
            float damage, float penetration, float armorDamage, bool isHeadshot)
        {
            int nextId = 1;
            var state = RaidState.Create(() => new EId(nextId++));
            var playerId = state.AllocateEId();
            var botId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, Vector3.zero);
            state.HealthMap[botId] = HealthState.Create(config.MaxHp);
            var context = TestContextFactory.Create();

            for (int hits = 1; hits <= 100; hits++)
            {
                var projectileId = state.AllocateEId();
                state.Projectiles.Add(ProjectileEntityState.Create(
                    projectileId, playerId, Vector3.zero, Vector3.forward,
                    stats.ProjectileSpeed, 0f, stats.ProjectileLifetime, damage,
                    stats.HeadshotDamageMultiplier,
                    penetration: penetration,
                    armorDamage: armorDamage));

                var hit = new HitSignal
                {
                    ProjectileId = projectileId,
                    TargetId = botId,
                    TargetedEntityId = isHeadshot ? botId : default,
                    Damage = damage,
                    Penetration = penetration,
                    ArmorDamage = armorDamage,
                };
                DamageSystem.Tick(state, new List<HitSignal> { hit }, in context,
                    randomProvider: () => 1f);

                if (!state.HealthMap[botId].IsAlive)
                    return hits;
            }

            Assert.Fail("Regular Scav did not die within 100 hits.");
            return -1;
        }
    }
}
