using System.Reflection;
using System.Runtime.Serialization;
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
            var appType = typeof(App.App);
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
            var instanceField = typeof(App.App).GetField("_instance",
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
            var weapon = WeaponEntityState.CreateRifle(weaponId);

            weapon.Phase = WeaponPhase.Ready;

            state.PlayerEntity.Hotbar[0] = weapon;
            state.PlayerEntity.SelectedHotbarSlot = 0;
            state.PlayerEntity.EquippedWeapon = weapon;
            state.PlayerEntity.PendingHotbarSlot = -1;

            var weapon2Id = state.AllocateEId();
            state.PlayerEntity.Hotbar[1] = WeaponEntityState.CreateShotgun(weapon2Id);

            // Starting reserve ammo for tests
            var rifleAmmoId = state.AllocateEId();
            App.App.Instance.Player.Inventory.Backpack[0] = ItemState.Create(rifleAmmoId, "Ammo_Rifle", 60);
            var shotgunAmmoId = state.AllocateEId();
            App.App.Instance.Player.Inventory.Backpack[1] = ItemState.Create(shotgunAmmoId, "Ammo_Shotgun", 15);

            return state;
        }

        public static System.Func<EId> NewAllocator()
        {
            int counter = 0;
            return () => new EId(++counter);
        }
    }
}