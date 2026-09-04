using Adapters;
using Session;

namespace Tests.EditMode.Fakes
{
    /// <summary>
    /// Single entry point for building <see cref="RaidContext"/> in EditMode tests.
    /// Consolidates the ~15 ad-hoc <c>CreateContext</c> helpers that used to live in
    /// individual test files.
    ///
    /// Defaults: fake adapters for input / nav / time, empty <see cref="RaidEventBuffer"/>
    /// for events, null physics (system decides how to handle), <see cref="AimConfig.Default"/>
    /// / <see cref="ShootingConfig.Default"/> configs. Any of these can be overridden per
    /// call — unspecified params keep their defaults.
    /// </summary>
    public static class TestContextFactory
    {
        public static RaidContext Create(
            FakeInputAdapter input = null,
            IRaidEvents events = null,
            IPhysicsAdapter physics = null,
            ICombatViewportAdapter combatViewport = null,
            ShootingConfig? shootingConfig = null,
            AimConfig? aimConfig = null,
            StaminaConfig? staminaConfig = null,
            ArmorConfig? armorConfig = null,
            FOVConfig? fovConfig = null,
            MovementConfig? movementConfig = null,
            BotEngagementConfig? botEngagementConfig = null,
            LaserConfig? laserConfig = null,
            BarrelHeatConfig? barrelHeatConfig = null,
            CheatsConfig? cheatsConfig = null,
            PlayerProgressionConfig? playerProgressionConfig = null,
            float deltaTime = 1f / 60f)
        {
            return new RaidContext(
                deltaTime: deltaTime,
                events: events ?? new RaidEventBuffer(),
                time: new FakeTimeAdapter { DeltaTime = deltaTime },
                input: input ?? new FakeInputAdapter(),
                navMesh: new FakeNavMeshAdapter(),
                physics: physics,
                combatViewport: combatViewport,
                shootingConfig: shootingConfig,
                aimConfig: aimConfig,
                staminaConfig: staminaConfig,
                armorConfig: armorConfig,
                fovConfig: fovConfig,
                movementConfig: movementConfig,
                botEngagementConfig: botEngagementConfig,
                laserConfig: laserConfig,
                barrelHeatConfig: barrelHeatConfig,
                cheatsConfig: cheatsConfig,
                playerProgressionConfig: playerProgressionConfig);
        }
    }
}
