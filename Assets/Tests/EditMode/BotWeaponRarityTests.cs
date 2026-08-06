using System.Collections.Generic;
using Adapters;
using Constants;
using NUnit.Framework;
using State;
using Systems.Bot;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Bot weapon module rarity. Loadouts in <see cref="BotConstants"/> are authored Common/Common,
    /// so without a roll every corpse dropped the same grey gun; payload and delivery are now rolled
    /// independently at spawn (50/40/10 Common/Uncommon/Rare).
    ///
    /// The distribution itself is tested on the pure mapping rather than by sampling RNG — the
    /// boundaries are the contract, and a sampled test is either flaky or slow.
    /// </summary>
    [TestFixture]
    public class BotWeaponRarityTests
    {
        BallisticPayloadDefinition  _payload;
        DeliveryCoreDefinition      _delivery;
        CoreDefinitionDatabase      _db;
        ICoreDefinitionRegistry     _registry;

        [SetUp]
        public void SetUp()
        {
            // Scav carries BallisticRound + SingleAction; the registry has to answer for exactly
            // those ids or WeaponSyncSystem refuses to assemble and bot.Weapon comes back null.
            _payload  = WeaponBuilderTestFactory.MakeBallistic("BallisticRound");
            _delivery = WeaponBuilderTestFactory.MakeDelivery("SingleAction");
            _db = WeaponBuilderTestFactory.MakeDatabase(
                payloads:   new PayloadCoreDefinition[]  { _payload },
                deliveries: new DeliveryCoreDefinition[] { _delivery });
            _registry = WeaponBuilderTestFactory.MakeRegistry(_db);
        }

        [TearDown]
        public void TearDown() =>
            WeaponBuilderTestFactory.DestroyAll(_payload, _delivery, _db);

        [TestCase(0f,     ExpectedResult = RarityTier.Common)]
        [TestCase(0.25f,  ExpectedResult = RarityTier.Common)]
        [TestCase(0.499f, ExpectedResult = RarityTier.Common)]
        [TestCase(0.5f,   ExpectedResult = RarityTier.Uncommon)]  // 50% mark: first Uncommon roll
        [TestCase(0.7f,   ExpectedResult = RarityTier.Uncommon)]
        [TestCase(0.899f, ExpectedResult = RarityTier.Uncommon)]
        [TestCase(0.9f,   ExpectedResult = RarityTier.Rare)]      // 90% mark: first Rare roll
        [TestCase(1f,     ExpectedResult = RarityTier.Rare)]
        public RarityTier PickRarity_MapsRollOntoTheWeights(float roll01)
            => BotSpawnSystem.PickRarity(roll01);

        [TestCase(-5f, ExpectedResult = RarityTier.Common)]
        [TestCase(42f, ExpectedResult = RarityTier.Rare)]
        public RarityTier PickRarity_ClampsOutOfRangeRolls(float roll01)
            => BotSpawnSystem.PickRarity(roll01);

        [Test]
        public void SpawnBot_RollsWeaponModulesWithinTheAllowedTiers()
        {
            var seen = new HashSet<RarityTier>();

            // Enough spawns that a stuck roll (always Common) shows up as a single-tier set, without
            // asserting proportions — that is the pure test's job above.
            for (int i = 0; i < 60; i++)
            {
                var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
                BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, new[] { Vector3.zero },
                    new FakeRaidEvents(), _registry);

                var weapon = state.Bots[0].Weapon;
                Assert.IsNotNull(weapon, "Bot must spawn with an assembled weapon.");

                foreach (var tier in new[] { weapon.PayloadCore.Rarity, weapon.DeliveryCore.Rarity })
                {
                    Assert.IsTrue(tier == RarityTier.Common || tier == RarityTier.Uncommon
                                  || tier == RarityTier.Rare,
                        $"Bot weapon rolled {tier}, which is outside the 50/40/10 table.");
                    seen.Add(tier);
                }
            }

            Assert.Greater(seen.Count, 1,
                "60 spawns produced a single rarity — the roll is not being applied.");
        }

        [Test]
        public void SpawnBot_RollsRarityOnly_NotTheModuleIdentity()
        {
            // Rarity is the only thing the roll may touch: swapping a bot's payload or delivery id
            // would silently change its archetype — and the caliber it drops.
            var authored = BotConstants.GetConfig("Scav").WeaponConfig;

            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            BotSpawnSystem.SpawnBot(state, "Scav", Vector3.zero, new[] { Vector3.zero },
                new FakeRaidEvents(), _registry);

            var weapon = state.Bots[0].Weapon;
            Assert.AreEqual(authored.Payload.DefinitionId,  weapon.PayloadCore.DefinitionId);
            Assert.AreEqual(authored.Delivery.DefinitionId, weapon.DeliveryCore.DefinitionId);
            Assert.AreEqual(authored.AmmoInMagazine, weapon.AmmoInMagazine,
                "Magazine load is carried over untouched — rarity only ever grows the capacity.");
        }
    }
}
