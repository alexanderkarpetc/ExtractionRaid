using System.Linq;
using Adapters;
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
        static RaidContext CreateContext(FakeInputAdapter input, float deltaTime = 1f / 60f,
            IRaidEvents events = null)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: input,
                navMesh: new FakeNavMeshAdapter()
            );
        }

        [Test]
        public void Tick_WithAttackPressedAndValidFacing_SpawnsProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_WithAttackNotPressed_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = false };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_InCooldownPhase_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Phase = WeaponPhase.Cooldown;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_InReadyPhase_SpawnsProjectile()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Phase = WeaponPhase.Ready;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count);
        }

        [Test]
        public void Tick_DuringEquipping_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Phase = WeaponPhase.Equipping;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_DuringUnequipping_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.Phase = WeaponPhase.Unequipping;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count);
        }

        [Test]
        public void Tick_ZeroAimDirection_DoesNotSpawn()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.AimDirection = Vector3.zero;
            state.PlayerEntity.WeaponAimPoint = Vector3.zero;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            var context = CreateContext(input);

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
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            Assert.AreEqual(muzzlePos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(muzzlePos.y, proj.Position.y, 0.001f);
            Assert.AreEqual(muzzlePos.z, proj.Position.z, 0.001f);
        }

        [Test]
        public void Tick_NullPlayer_DoesNotThrow()
        {
            var state = RaidState.Create();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            Assert.DoesNotThrow(() => ShootingSystem.Tick(state, in context));
        }

        [Test]
        public void Tick_SetsLastFireTimeAndFiringPhase()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.ElapsedTime = 2.5f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            var context = CreateContext(input);

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
            var context = CreateContext(input, events: eventBuffer);

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
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 7;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 30f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(7, state.Projectiles.Count);
        }

        [Test]
        public void Tick_SpreadWeapon_AllPelletsWithinSpreadAngle()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.AimDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 7;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 30f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 1;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 0f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 7;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 30f;
            state.PlayerEntity.EquippedWeapon.ProjectileSpeed = 25f;
            state.PlayerEntity.EquippedWeapon.ProjectileDamage = 8f;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 5;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 20f;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input, events: eventBuffer);

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
            var context = CreateContext(input, events: eventBuffer);

            ShootingSystem.Tick(state, in context);

            var fired = eventBuffer.All.Where(e => e.Type == RaidEventType.WeaponFired).ToList();
            Assert.AreEqual(1, fired.Count);
        }

        [Test]
        public void Tick_SpreadWeapon_EmitsOneWeaponFiredEvent()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.ProjectilesPerShot = 7;
            state.PlayerEntity.EquippedWeapon.SpreadAngle = 30f;
            var eventBuffer = new RaidEventBuffer();
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input, events: eventBuffer);

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
            var context = CreateContext(input);

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
            var context = CreateContext(input, events: eventBuffer);

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
            var context = CreateContext(input);

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
                state.Inventory.Backpack[i] = null;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

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
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(29, state.PlayerEntity.EquippedWeapon.AmmoInMagazine);
        }

        [Test]
        public void Tick_ShotgunFiringConsumesOneAmmo()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var weapon = state.PlayerEntity.EquippedWeapon;
            weapon.ProjectilesPerShot = 7;
            weapon.SpreadAngle = 30f;
            weapon.AmmoType = "Ammo_Shotgun";
            weapon.AmmoInMagazine = 5;
            weapon.MagazineSize = 5;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(7, state.Projectiles.Count, "7 pellets spawned");
            Assert.AreEqual(4, weapon.AmmoInMagazine, "Only 1 shell consumed");
        }

        [Test]
        public void Tick_NullAmmoType_InfiniteAmmo()
        {
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.EquippedWeapon.AmmoType = null;
            state.PlayerEntity.EquippedWeapon.AmmoInMagazine = 0;
            var input = new FakeInputAdapter { AttackPressed = true };
            var context = CreateContext(input);

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count, "Should fire with null AmmoType");
            Assert.AreEqual(0, state.PlayerEntity.EquippedWeapon.AmmoInMagazine,
                "Should not change AmmoInMagazine when AmmoType is null");
        }
    }
}
