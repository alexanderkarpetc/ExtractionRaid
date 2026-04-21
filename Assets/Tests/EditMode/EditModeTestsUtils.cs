using System.Reflection;
using System.Runtime.Serialization;
using ApplicationCore;
using Session;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    public static class EditModeTestsUtils
    {
        /// <summary>
        /// Injects a minimal App instance via reflection so edit-mode tests can access
        /// App.Instance.Player without running AppBootstrap or any Unity adapters.
        /// Call ResetApp() in [TearDown] to clean up static state between test runs.
        /// </summary>
        public static void EnsureAppForTests()
        {
            var appType = typeof(App);
            var instanceField = appType.GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Always create a fresh minimal App so tests don't share Player state.
            var appInstance = FormatterServices.GetUninitializedObject(appType);

            var playerProp = appType.GetProperty("Player");
            playerProp.SetValue(appInstance, new Player());

            instanceField.SetValue(null, appInstance);
        }

        /// <summary>
        /// Clears the injected App instance. Call in [TearDown] to avoid state leaking
        /// between test runs.
        /// </summary>
        public static void ResetApp()
        {
            var instanceField = typeof(App).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            instanceField.SetValue(null, null);
        }

        public static RaidState CreateStateWithPlayer(Vector3 startPos)
        {
            EnsureAppForTests();

            var state = RaidState.Create(EditModeTestsUtils.NewAllocator());
            var playerId = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(playerId, startPos);

            var weaponId = state.AllocateEId();
            var weapon = NewRifleLikeWeapon(weaponId);

            weapon.Phase = WeaponPhase.Ready;

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;
            state.PlayerEntity.PendingHotbarSlot = -1;

            var weapon2Id = state.AllocateEId();
            state.PlayerEntity.Hotbar[1] = NewPistolLikeWeapon(weapon2Id);

            // Starting reserve ammo for tests
            var rifleAmmoId = state.AllocateEId();
            App.Instance.Player.Inventory.Backpack[0] = ItemState.Create(rifleAmmoId, "Ammo_Rifle", 60);
            var pistolAmmoId = state.AllocateEId();
            App.Instance.Player.Inventory.Backpack[1] = ItemState.Create(pistolAmmoId, "Ammo_Pistol", 36);

            return state;
        }

        /// <summary>
        /// Builds a rifle-equivalent WeaponEntityState for tests — stats parity with
        /// pre-migration Ballistic+Auto combo.
        /// </summary>
        public static WeaponEntityState NewRifleLikeWeapon(EId id) =>
            new()
            {
                Id             = id,
                PrefabId       = "Weapon_Rifle",
                PayloadCore    = new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                DeliveryCore   = new DeliveryCoreInstance("Auto",          RarityTier.Common),
                AmmoType       = "Ammo_Rifle",
                AmmoInMagazine = 30,
                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
                Stats = new WeaponStats
                {
                    Damage                   = 10f,
                    ProjectileSpeed          = 20f,
                    ProjectileLifetime       = 3f,
                    HeadshotDamageMultiplier = 2f,
                    BasePenetration          = 20f,
                    BaseArmorDamage          = 5f,
                    ProjectilesPerShot       = 1,
                    ConeHalfAngle            = 45f,
                    BodyRotationSpeed        = 270f,
                    AimFollowSharpness       = 10f,
                    RecoilKickForward        = 2f,
                    RecoilKickSide           = 1.5f,
                    RecoilRecoverySpeed      = 2f,
                    EquipTime                = 0.3f,
                    UnequipTime              = 0.2f,
                    MagazineSize             = 30,
                    ReloadTime               = 2.0f,
                    FireInterval             = 0.2f,
                },
            };

        /// <summary>
        /// Builds a pistol-equivalent WeaponEntityState for tests — stats parity with
        /// pre-migration Ballistic+SingleAction combo.
        /// </summary>
        public static WeaponEntityState NewPistolLikeWeapon(EId id) =>
            new()
            {
                Id             = id,
                PrefabId       = "Weapon_Pistol",
                PayloadCore    = new PayloadCoreInstance("BallisticRound", RarityTier.Common),
                DeliveryCore   = new DeliveryCoreInstance("SingleAction",  RarityTier.Common),
                AmmoType       = "Ammo_Pistol",
                AmmoInMagazine = 12,
                LastFireTime   = -999f,
                Phase          = WeaponPhase.Ready,
                PhaseStartTime = 0f,
                Stats = new WeaponStats
                {
                    Damage                   = 15f,
                    ProjectileSpeed          = 25f,
                    ProjectileLifetime       = 2.5f,
                    HeadshotDamageMultiplier = 2.5f,
                    BasePenetration          = 15f,
                    BaseArmorDamage          = 6f,
                    ProjectilesPerShot       = 1,
                    ConeHalfAngle            = 35f,
                    BodyRotationSpeed        = 300f,
                    AimFollowSharpness       = 15f,
                    RecoilKickForward        = 1.5f,
                    RecoilKickSide           = 1f,
                    RecoilRecoverySpeed      = 4f,
                    EquipTime                = 0.2f,
                    UnequipTime              = 0.15f,
                    MagazineSize             = 12,
                    ReloadTime               = 1.5f,
                    FireInterval             = 0.4f,
                },
            };

        public static System.Func<EId> NewAllocator()
        {
            int counter = 0;
            return () => new EId(++counter);
        }
    }
}