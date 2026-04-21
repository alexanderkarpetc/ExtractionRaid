using System.Reflection;
using NUnit.Framework;
using State;
using Systems;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class WeaponStatComposerTests
    {
        // ── Basic compose ─────────────────────────────────────

        [Test]
        public void Compose_CommonTier_PopulatesAllPayloadFields()
        {
            var payload = MakePayloadWithStats(RarityTier.Common, new CommonPayloadStats
            {
                Damage                   = 15f,
                ProjectileSpeed          = 25f,
                ProjectileLifetime       = 2.5f,
                HeadshotDamageMultiplier = 2.0f,
                BasePenetration          = 15f,
                BaseArmorDamage          = 5f,
                BaseBleedChance          = 0.1f,
            });
            var delivery = MakeDeliveryWithStats(RarityTier.Common, default);
            try
            {
                var stats = WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common);

                Assert.AreEqual(15f,  stats.Damage);
                Assert.AreEqual(25f,  stats.ProjectileSpeed);
                Assert.AreEqual(2.5f, stats.ProjectileLifetime);
                Assert.AreEqual(2.0f, stats.HeadshotDamageMultiplier);
                Assert.AreEqual(15f,  stats.BasePenetration);
                Assert.AreEqual(5f,   stats.BaseArmorDamage);
                Assert.AreEqual(0.1f, stats.BaseBleedChance);
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_CommonTier_PopulatesAllDeliveryFields()
        {
            var payload = MakePayloadWithStats(RarityTier.Common, default);
            var delivery = MakeDeliveryWithStats(RarityTier.Common, new DeliveryStats
            {
                FireInterval        = 0.4f,
                ProjectilesPerShot  = 1,
                SpreadAngle         = 0f,
                ConeHalfAngle       = 35f,
                BodyRotationSpeed   = 300f,
                AimFollowSharpness  = 15f,
                RecoilKickForward   = 1.5f,
                RecoilKickSide      = 1f,
                RecoilRecoverySpeed = 4f,
                EquipTime           = 0.2f,
                UnequipTime         = 0.15f,
                MagazineSize        = 12,
                ReloadTime          = 1.5f,
            });
            try
            {
                var stats = WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common);

                Assert.AreEqual(0.4f,  stats.FireInterval);
                Assert.AreEqual(1,     stats.ProjectilesPerShot);
                Assert.AreEqual(0f,    stats.SpreadAngle);
                Assert.AreEqual(35f,   stats.ConeHalfAngle);
                Assert.AreEqual(300f,  stats.BodyRotationSpeed);
                Assert.AreEqual(15f,   stats.AimFollowSharpness);
                Assert.AreEqual(1.5f,  stats.RecoilKickForward);
                Assert.AreEqual(1f,    stats.RecoilKickSide);
                Assert.AreEqual(4f,    stats.RecoilRecoverySpeed);
                Assert.AreEqual(0.2f,  stats.EquipTime);
                Assert.AreEqual(0.15f, stats.UnequipTime);
                Assert.AreEqual(12,    stats.MagazineSize);
                Assert.AreEqual(1.5f,  stats.ReloadTime);
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Rarity selection ──────────────────────────────────

        [Test]
        public void Compose_PicksCorrectPayloadTier()
        {
            var payload = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            var statsArray = new CommonPayloadStats[5];
            statsArray[(int)RarityTier.Common]    = new CommonPayloadStats { Damage = 10f };
            statsArray[(int)RarityTier.Rare]      = new CommonPayloadStats { Damage = 20f };
            statsArray[(int)RarityTier.Legendary] = new CommonPayloadStats { Damage = 40f };
            SetPrivateField(payload, "_statsByTier", statsArray);

            var delivery = MakeDeliveryWithStats(RarityTier.Common, default);
            try
            {
                Assert.AreEqual(10f, WeaponStatComposer.Compose(payload, RarityTier.Common,    delivery, RarityTier.Common).Damage);
                Assert.AreEqual(20f, WeaponStatComposer.Compose(payload, RarityTier.Rare,      delivery, RarityTier.Common).Damage);
                Assert.AreEqual(40f, WeaponStatComposer.Compose(payload, RarityTier.Legendary, delivery, RarityTier.Common).Damage);
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_PicksCorrectDeliveryTier()
        {
            var payload = MakePayloadWithStats(RarityTier.Common, default);
            var delivery = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            var statsArray = new DeliveryStats[5];
            statsArray[(int)RarityTier.Common] = new DeliveryStats { FireInterval = 0.4f };
            statsArray[(int)RarityTier.Epic]   = new DeliveryStats { FireInterval = 0.2f };
            SetPrivateField(delivery, "_statsByTier", statsArray);

            try
            {
                Assert.AreEqual(0.4f, WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common).FireInterval);
                Assert.AreEqual(0.2f, WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Epic).FireInterval);
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Independence of sides ─────────────────────────────

        [Test]
        public void Compose_DeliveryDoesNotOverridePayloadFields()
        {
            // Delivery's stats have no Damage field, so Damage must come from Payload only.
            var payload  = MakePayloadWithStats(RarityTier.Common, new CommonPayloadStats { Damage = 42f });
            var delivery = MakeDeliveryWithStats(RarityTier.Common, default); // empty delivery stats
            try
            {
                var stats = WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common);
                Assert.AreEqual(42f, stats.Damage);
            }
            finally { Cleanup(payload, delivery); }
        }

        [Test]
        public void Compose_PayloadDoesNotOverrideDeliveryFields()
        {
            var payload  = MakePayloadWithStats(RarityTier.Common, default);
            var delivery = MakeDeliveryWithStats(RarityTier.Common, new DeliveryStats { MagazineSize = 99 });
            try
            {
                var stats = WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common);
                Assert.AreEqual(99, stats.MagazineSize);
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Exotic null / ignored ─────────────────────────────

        [Test]
        public void Compose_ExoticNull_UsesBaseStats()
        {
            var payload  = MakePayloadWithStats(RarityTier.Common, new CommonPayloadStats { Damage = 10f });
            var delivery = MakeDeliveryWithStats(RarityTier.Common, default);
            try
            {
                var stats = WeaponStatComposer.Compose(payload, RarityTier.Common, delivery, RarityTier.Common, exotic: null);
                Assert.AreEqual(10f, stats.Damage);
            }
            finally { Cleanup(payload, delivery); }
        }

        // ── Helpers ───────────────────────────────────────────

        static BallisticPayloadDefinition MakePayloadWithStats(RarityTier tier, CommonPayloadStats stats)
        {
            var def = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            var array = new CommonPayloadStats[5];
            array[(int)tier] = stats;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static DeliveryCoreDefinition MakeDeliveryWithStats(RarityTier tier, DeliveryStats stats)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            var array = new DeliveryStats[5];
            array[(int)tier] = stats;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static void Cleanup(Object a, Object b)
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Assert.Fail($"Field '{fieldName}' not found on {target.GetType()}.");
        }
    }
}
