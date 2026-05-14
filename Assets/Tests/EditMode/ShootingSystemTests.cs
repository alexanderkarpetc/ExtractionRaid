using System.Linq;
using Adapters;
using ApplicationCore;
using Systems;
using NUnit.Framework;
using Session;
using State;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ShootingSystemTests
    {

        [Test]
        public void Tick_WithAttackPressedAndValidFacing_SpawnsProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithAttackNotPressed_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = false };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        // Ready is the only phase that allows the ballistic fire path to spawn a projectile.
        [TestCase(WeaponPhase.Ready,       1)]
        [TestCase(WeaponPhase.Cooldown,    0)]
        [TestCase(WeaponPhase.Equipping,   0)]
        [TestCase(WeaponPhase.Unequipping, 0)]
        public void Tick_PhaseGate_ControlsProjectileSpawn(WeaponPhase phase, int expectedProjectiles)
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Phase = phase;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(expectedProjectiles, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ZeroAimDirection_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.AimDirection = Vector3.zero;
            state.PlayerEntity.WeaponAimPoint = Vector3.zero;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ProjectileDirectionMatchesAimDirection()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var aimDir = new Vector3(1f, 0f, 1f).normalized;
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.AimDirection = aimDir;
            state.PlayerEntity.WeaponAimPoint = aimDir * 10f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(aimDir.x, state.Projectiles[0].Direction.x, 0.001f);
            Assert.AreEqual(aimDir.z, state.Projectiles[0].Direction.z, 0.001f);
        }

        [Test]
        public void Tick_ProjectileSpawnsAtMuzzleWorldPoint()
        {
            var muzzlePos = new Vector3(2f, 0.5f, 4.2f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(new Vector3(2f, 0f, 3f));
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter
            {
                AttackPressed = true,
                MuzzleWorldPoint = muzzlePos,
            };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            Assert.AreEqual(muzzlePos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(ShootingConfig.Default.ProjectileSpawnHeight, proj.Position.y, 0.001f);
            Assert.AreEqual(muzzlePos.z, proj.Position.z, 0.001f);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            Assert.DoesNotThrow(() => ShootingSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_SetsLastFireTimeAndFiringPhase()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 2.5f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(2.5f, state.PlayerEntity.EquippedWeapon.LastFireTime, 0.001f);
            Assert.AreEqual(WeaponPhase.Firing, state.PlayerEntity.EquippedWeapon.Phase);
            Assert.AreEqual(2.5f, state.PlayerEntity.EquippedWeapon.PhaseStartTime, 0.001f);
        }

        [Test]
        public void Tick_NoEquippedWeapon_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.EquippedWeapon = null;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            Assert.DoesNotThrow(() => ShootingSystem.Tick(state, in context));
            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmitsProjectileSpawnedEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var spawned = eventBuffer.All.Where(e => e.Type == RaidEventType.ProjectileSpawned).ToList();
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(state.Projectiles[0].Id, spawned[0].Id);
        }

        [Test]
        public void Tick_SpreadWeapon_SpawnsCorrectCount()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =7;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =30f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(7, state.Projectiles.Count);
        }

        [Test]
        public void Tick_SpreadWeapon_AllPelletsWithinSpreadAngle()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.AimDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =7;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =30f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            foreach (var proj in state.Projectiles)
            {
                float angle = Vector3.Angle(Vector3.forward, proj.Direction);
                Assert.LessOrEqual(angle, 15f + 0.01f,
                    $"Pellet direction angle {angle}° exceeds half spread 15°");
            }
        }

        [Test]
        public void Tick_ZeroSpread_ExactAimDirection()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            var aimDir = new Vector3(1f, 0f, 1f).normalized;
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.AimDirection = aimDir;
            state.PlayerEntity.WeaponAimPoint = aimDir * 10f;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =1;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =0f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.AreEqual(aimDir.x, state.Projectiles[0].Direction.x, 0.001f);
            Assert.AreEqual(aimDir.z, state.Projectiles[0].Direction.z, 0.001f);
        }

        [Test]
        public void Tick_SpreadWeapon_AllPelletsHaveSameSpeedAndDamage()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =7;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =30f;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectileSpeed = 25f;
            state.PlayerEntity.EquippedWeapon.Stats.Damage = 8f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            foreach (var proj in state.Projectiles)
            {
                Assert.AreEqual(25f, proj.Speed, 0.001f);
                Assert.AreEqual(8f, proj.Damage, 0.001f);
            }
        }

        [Test]
        public void Tick_SpreadWeapon_EmitsEventPerPellet()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =5;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =20f;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var spawned = eventBuffer.All.Where(e => e.Type == RaidEventType.ProjectileSpawned).ToList();
            Assert.AreEqual(5, spawned.Count);
        }

        [Test]
        public void Tick_Fires_EmitsWeaponFiredEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var fired = eventBuffer.All.Where(e => e.Type == RaidEventType.WeaponFired).ToList();
            Assert.AreEqual(1, fired.Count);
        }

        [Test]
        public void Tick_SpreadWeapon_EmitsOneWeaponFiredEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Stats.ProjectilesPerShot =7;
            state.PlayerEntity.EquippedWeapon.Stats.SpreadAngle =30f;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var fired = eventBuffer.All.Where(e => e.Type == RaidEventType.WeaponFired).ToList();
            Assert.AreEqual(1, fired.Count, "Spread weapon should emit exactly 1 WeaponFired event per volley");
        }

        // ── Ammo tests ─────────────────────────────────────────

        [Test]
        public void Tick_EmptyMagazine_DoesNotSpawnProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_EmptyMagazine_EmitsDryFireEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var dryFires = eventBuffer.All.Where(e => e.Type == RaidEventType.WeaponDryFired).ToList();
            Assert.AreEqual(1, dryFires.Count);
        }

        [Test]
        public void Tick_EmptyMagazine_AutoReloadsIfReserveAvailable()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            // Reserve ammo already in backpack from CreateStateWithPlayer
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Reloading, state.PlayerEntity.EquippedWeapon.Phase);
        }

        [Test]
        public void Tick_EmptyMagazine_NoReserve_StaysReady()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            // Clear all reserve ammo
            for (int i = 0; i < InventoryState.BackpackSize; i++)
                App.Instance.Player.Inventory.Backpack[i] = null;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Ready, state.PlayerEntity.EquippedWeapon.Phase);
        }

        [Test]
        public void Tick_FiringConsumesOneAmmo()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 30;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(29, state.PlayerEntity.EquippedWeapon.AmmoInMagazine);
        }

        [Test]
        public void Tick_MultiPelletFiringConsumesOneAmmo()
        {
            // Scatter-pattern weapon (N pellets per trigger pull, 1 shell consumed).
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.Stats.ProjectilesPerShot = 7;
            weapon.Stats.SpreadAngle = 30f;
            weapon.AmmoType = "Ammo_Rifle";
            weapon.AmmoInMagazine = 5;
            weapon.Stats.MagazineSize = 5;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(7, state.Projectiles.Count, "7 pellets spawned");
            Assert.AreEqual(4, weapon.AmmoInMagazine, "Only 1 round consumed per trigger pull");
        }

        [Test]
        public void Tick_NullAmmoType_InfiniteAmmo()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoType = null;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count, "Should fire with null AmmoType");
            Assert.AreEqual(0, state.PlayerEntity.EquippedWeapon.AmmoInMagazine,
                "Should not change AmmoInMagazine when AmmoType is null");
        }

        // ── Charge-up gate (Tier 2) ──────────────────────────

        [Test]
        public void Tick_LaserPayload_AttackPressed_TransitionsToChargingNotFiring()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                state.ElapsedTime = 0f;
                var events = new RaidEventBuffer();
                var input = new FakeInputAdapter { AttackPressed = true };
                var context = TestContextFactory.Create(input, events: events);

                ShootingSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);
                Assert.AreEqual(0, state.Projectiles.Count, "No fire until charge completes");
                Assert.AreEqual(0f, weapon.ChargeStartTime);
                Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeStarted));
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_LaserPayload_ChargingWithTimeRemaining_DoesNotFire()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                state.ElapsedTime = 0.5f; // half way
                var input = new FakeInputAdapter { AttackPressed = true };
                var context = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Charging, weapon.Phase);
                Assert.AreEqual(0, state.Projectiles.Count);
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_LaserPayload_ChargingWithTimeElapsed_StaysChargedUntilRelease()
        {
            // Tau-cannon mechanic (2026-05-06): full charge no longer auto-fires —
            // user must release. Charging + AttackPressed → still waiting.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                state.ElapsedTime = 1.1f;
                var events = new RaidEventBuffer();
                var input = new FakeInputAdapter { AttackPressed = true };
                var context = TestContextFactory.Create(input, events: events);

                ShootingSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Charging, weapon.Phase, "Stays charging until release");
                Assert.AreEqual(0, state.Projectiles.Count, "No fire while still holding");
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_LaserPayload_ChargingWithAttackJustReleased_FiresAtCurrentChargeAndEmitsCompleted()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                state.ElapsedTime = 1.1f; // past charge time → ratio clamped to 1.0
                var events = new RaidEventBuffer();
                var input = new FakeInputAdapter
                {
                    AttackPressed = false,
                    AttackJustReleased = true,
                };
                var context = TestContextFactory.Create(input, events: events);

                ShootingSystem.Tick(state, in context);

                Assert.AreEqual(WeaponPhase.Firing, weapon.Phase, "Release fires the charged shot");
                Assert.AreEqual(1, state.Projectiles.Count);
                Assert.IsTrue(events.All.Any(e => e.Type == RaidEventType.WeaponChargeCompleted));
            }
            finally { Object.DestroyImmediate(laserSO); }
        }

        [Test]
        public void Tick_NonLaserPayload_AttackPressed_FiresImmediatelyBypassingCharge()
        {
            // Default weapon (rifle-like) has no PayloadDefinition — charge gate bypassed.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            Assert.IsNull(weapon.PayloadDefinition, "Precondition: non-laser weapon");

            var events = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = TestContextFactory.Create(input, events: events);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(WeaponPhase.Firing, weapon.Phase);
            Assert.AreEqual(1, state.Projectiles.Count);
            Assert.IsFalse(events.All.Any(e => e.Type == RaidEventType.WeaponChargeStarted),
                "Non-laser must not emit charge events");
        }

        static LaserPayloadDefinition MakeLaserPayloadSO(float chargeTime)
            => WeaponBuilderTestFactory.MakeLaser(chargeTime: chargeTime);

        // ── Parabolic charge → damage curve (B3) ─────────────────

        [Test]
        public void LaserConfig_ChargeDamageMultiplier_ZeroCharge_ReturnsMin()
        {
            var cfg = LaserConfig.Default; // Min=0.1, Power=2
            Assert.AreEqual(0.1f, cfg.ChargeDamageMultiplier(0f), 1e-4f);
        }

        [Test]
        public void LaserConfig_ChargeDamageMultiplier_FullCharge_ReturnsOne()
        {
            var cfg = LaserConfig.Default;
            Assert.AreEqual(1f, cfg.ChargeDamageMultiplier(1f), 1e-4f);
        }

        [Test]
        public void LaserConfig_ChargeDamageMultiplier_HalfCharge_IsParabolic()
        {
            // min + (1-min) * 0.5² = 0.1 + 0.9 * 0.25 = 0.325 (vs old linear 0.65)
            var cfg = LaserConfig.Default;
            Assert.AreEqual(0.325f, cfg.ChargeDamageMultiplier(0.5f), 1e-4f);
        }

        [Test]
        public void LaserConfig_ChargeDamageMultiplier_LinearPower_MatchesLerp()
        {
            // Power=1 → reproduces linear behavior. Validates curve gen.
            var cfg = new LaserConfig
            {
                ChargeDamageMin = 0.3f,
                ChargeDamagePower = 1f,
            };
            Assert.AreEqual(0.65f, cfg.ChargeDamageMultiplier(0.5f), 1e-4f);
        }

        // ── Laser Shotgun: charge controls projectile lifetime (B2) ──

        [Test]
        public void Tick_LaserShotgun_FullCharge_FiresLongerLifetimeProjectiles()
        {
            // Laser+Scatter with full charge — projectile lifetime should be base × ShotgunMaxLifetimeMult.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            var scatterSO = WeaponBuilderTestFactory.MakeDelivery(id: "Scatter",
                pattern: FiringPattern.Scatter);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.DeliveryDefinition = scatterSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                // Full charge for Scatter: base 1s × ScatterChargeMult 1.5 = 1.5s effective.
                // ElapsedTime must exceed it for clamp(1.0).
                state.ElapsedTime = 2f;
                var input = new FakeInputAdapter
                {
                    AttackPressed = false,
                    AttackJustReleased = true,
                };
                var ctx = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in ctx);

                Assert.Greater(state.Projectiles.Count, 0, "Laser shotgun should fire pellets");
                float baseLifetime = weapon.Stats.ProjectileLifetime;
                float maxLifetime  = baseLifetime * LaserConfig.Default.ShotgunMaxLifetimeMult;
                Assert.AreEqual(maxLifetime, state.Projectiles[0].Lifetime, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(laserSO);
                Object.DestroyImmediate(scatterSO);
            }
        }

        [Test]
        public void Tick_LaserShotgun_ZeroCharge_FiresShorterLifetimeProjectiles()
        {
            // Quick tap (chargeRatio ≈ 0) → projectile lifetime shrinks to base × ShotgunMinLifetimeMult.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var laserSO = MakeLaserPayloadSO(chargeTime: 1f);
            var scatterSO = WeaponBuilderTestFactory.MakeDelivery(id: "Scatter",
                pattern: FiringPattern.Scatter);
            try
            {
                weapon.PayloadDefinition = laserSO;
                weapon.DeliveryDefinition = scatterSO;
                weapon.Phase = WeaponPhase.Charging;
                weapon.ChargeStartTime = 0f;
                state.ElapsedTime = 0f; // ratio = 0
                var input = new FakeInputAdapter
                {
                    AttackPressed = false,
                    AttackJustReleased = true,
                };
                var ctx = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in ctx);

                Assert.Greater(state.Projectiles.Count, 0);
                float baseLifetime = weapon.Stats.ProjectileLifetime;
                float minLifetime  = baseLifetime * LaserConfig.Default.ShotgunMinLifetimeMult;
                Assert.AreEqual(minLifetime, state.Projectiles[0].Lifetime, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(laserSO);
                Object.DestroyImmediate(scatterSO);
            }
        }

        [Test]
        public void Tick_NonLaserShotgun_NotAffectedByShotgunMultipliers()
        {
            // Ballistic+Scatter (regular shotgun) — must not have lifetime modulation.
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            var scatterSO = WeaponBuilderTestFactory.MakeDelivery(id: "Scatter",
                pattern: FiringPattern.Scatter);
            try
            {
                weapon.DeliveryDefinition = scatterSO; // payload stays default (non-laser)
                var input = new FakeInputAdapter
                {
                    AttackPressed = true,
                    AttackJustPressed = true, // Scatter is semi-auto gate
                };
                var ctx = TestContextFactory.Create(input);

                ShootingSystem.Tick(state, in ctx);

                Assert.Greater(state.Projectiles.Count, 0);
                Assert.AreEqual(weapon.Stats.ProjectileLifetime,
                    state.Projectiles[0].Lifetime, 1e-3f,
                    "Ballistic shotgun must retain baseline lifetime");
            }
            finally { Object.DestroyImmediate(scatterSO); }
        }
    }
}
