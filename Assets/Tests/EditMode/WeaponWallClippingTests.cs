using NUnit.Framework;
using Session;
using Systems;
using Tests.EditMode.Fakes;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Tests for Solution 2 (pre-fire muzzle raycast / wall clamp) in ShootingSystem.
    /// Solution 3a (weapon pullback in CharacterBody.LateUpdate) is view-layer and
    /// validated manually in play mode; its pure math is tested in WeaponPullbackMathTests.
    /// </summary>
    [TestFixture]
    public class WeaponWallClippingTests
    {
        static ShootingConfig ConfigWithMuzzleBlock(bool enabled, float backoff = 0.1f)
        {
            var cfg = ShootingConfig.Default;
            cfg.MuzzleBlockEnabled = enabled;
            cfg.MuzzleBlockBackoff = backoff;
            return cfg;
        }

        [Test]
        public void Tick_MuzzleBlockDisabled_SpawnsAtMuzzle()
        {
            var muzzlePos = new Vector3(2f, 0.5f, 4f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = true, MuzzleWorldPoint = muzzlePos };
            var physics = new FakePhysicsAdapter { WallHit = true, WallHitPoint = new Vector3(1f, 0.5f, 2f) };
            var context = TestContextFactory.Create(input, physics: physics, shootingConfig: ConfigWithMuzzleBlock(false));

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            // Disabled → spawn is the unaltered muzzle XZ with y=ProjectileSpawnHeight
            Assert.AreEqual(muzzlePos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(ShootingConfig.Default.ProjectileSpawnHeight, proj.Position.y, 0.001f);
            Assert.AreEqual(muzzlePos.z, proj.Position.z, 0.001f);
        }

        [Test]
        public void Tick_MuzzleBlockEnabled_NoHit_SpawnsAtMuzzle()
        {
            var muzzlePos = new Vector3(2f, 0.5f, 4f);
            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            var input = new FakeInputAdapter { AttackPressed = true, MuzzleWorldPoint = muzzlePos };
            var physics = new FakePhysicsAdapter { WallHit = false };
            var context = TestContextFactory.Create(input, physics: physics, shootingConfig: ConfigWithMuzzleBlock(true));

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            Assert.AreEqual(muzzlePos.x, proj.Position.x, 0.001f);
            Assert.AreEqual(ShootingConfig.Default.ProjectileSpawnHeight, proj.Position.y, 0.001f);
            Assert.AreEqual(muzzlePos.z, proj.Position.z, 0.001f);
        }

        [Test]
        public void Tick_MuzzleBlockEnabled_Hit_ClampsSpawnBeforeWall()
        {
            // Player at origin, muzzle at (2, 0.5, 0). Wall hit at (1, 0.3, 0) — between player and muzzle.
            var muzzlePos = new Vector3(2f, 0.5f, 0f);
            var wallHitPoint = new Vector3(1f, 0.3f, 0f);

            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.WeaponAimPoint = new Vector3(10f, 0f, 0f);
            var input = new FakeInputAdapter { AttackPressed = true, MuzzleWorldPoint = muzzlePos };
            var physics = new FakePhysicsAdapter { WallHit = true, WallHitPoint = wallHitPoint };
            var context = TestContextFactory.Create(input, physics: physics, shootingConfig: ConfigWithMuzzleBlock(true));

            ShootingSystem.Tick(state, in context);

            var proj = state.Projectiles[0];
            Assert.Less(proj.Position.x, muzzlePos.x,
                "Spawn X must be clamped closer to player than the original muzzle.");
            Assert.Less(proj.Position.x, wallHitPoint.x,
                "Spawn X must be on the player-side of the wall.");
            Assert.AreEqual(ShootingConfig.Default.ProjectileSpawnHeight, proj.Position.y, 0.001f,
                "Y must remain at the configured spawn height.");
        }

        [Test]
        public void Tick_ClampBoundedBehindChest_DoesNotSpawnBehindPlayer()
        {
            // Wall hit extremely close to player (5 cm ahead). With backoff=0.5, naive clamp
            // would put spawn at -0.45 X (behind player). Bound must clamp to just before wall.
            var muzzlePos = new Vector3(2f, 0.5f, 0f);
            var wallHitPoint = new Vector3(0.05f, 0.3f, 0f);

            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.WeaponAimPoint = new Vector3(10f, 0f, 0f);
            var input = new FakeInputAdapter { AttackPressed = true, MuzzleWorldPoint = muzzlePos };
            var physics = new FakePhysicsAdapter { WallHit = true, WallHitPoint = wallHitPoint };
            var context = TestContextFactory.Create(input, physics: physics, shootingConfig: ConfigWithMuzzleBlock(true, backoff: 0.5f));

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(1, state.Projectiles.Count,
                "Should fire — wall is ahead of player with minimum clearance.");
            var proj = state.Projectiles[0];
            Assert.GreaterOrEqual(proj.Position.x, 0f,
                "Spawn X must NOT be behind player origin (X=0).");
            Assert.LessOrEqual(proj.Position.x, wallHitPoint.x,
                "Spawn X must not be past the wall.");
        }

        [Test]
        public void Tick_WallFlushWithPlayer_SkipsFire()
        {
            // Wall touching the player (1 cm distance) — no clearance for a bullet.
            // System should silently skip firing (no projectile spawned).
            var muzzlePos = new Vector3(2f, 0.5f, 0f);
            var wallHitPoint = new Vector3(0.01f, 0.3f, 0f);

            var state = EditModeTestsUtils.CreateStateWithPlayer(Vector3.zero);
            state.PlayerEntity.FacingDirection = Vector3.forward;
            state.PlayerEntity.WeaponAimPoint = new Vector3(10f, 0f, 0f);
            var input = new FakeInputAdapter { AttackPressed = true, MuzzleWorldPoint = muzzlePos };
            var physics = new FakePhysicsAdapter { WallHit = true, WallHitPoint = wallHitPoint };
            var context = TestContextFactory.Create(input, physics: physics, shootingConfig: ConfigWithMuzzleBlock(true));

            ShootingSystem.Tick(state, in context);

            Assert.AreEqual(0, state.Projectiles.Count,
                "No projectile should spawn when the player is flush with a wall.");
        }
    }
}
