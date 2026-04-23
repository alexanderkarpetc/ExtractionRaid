using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Adapters;
using ApplicationCore;
using Session;
using State;
using UnityEngine;

namespace Tests.EditMode
{
    public static class EditModeTestsUtils
    {
        static readonly List<Object> _ownedScriptableObjects = new();

        /// <summary>
        /// Injects a minimal App instance via reflection so edit-mode tests can access
        /// App.Instance.Player / CoreDefinitions without running AppBootstrap or any
        /// Unity adapters. The injected <see cref="ICoreDefinitionRegistry"/> knows
        /// about BallisticRound / Auto / SingleAction — enough for
        /// <see cref="Systems.WeaponItemFactory"/> default configurations to assemble.
        /// Call ResetApp() in [TearDown] to clean up static state between test runs.
        /// </summary>
        public static void EnsureAppForTests()
        {
            // Destroy any SOs we own from a previous invocation — many test fixtures
            // don't call ResetApp() between tests, so destroy-then-create keeps the
            // leak bounded to one batch.
            DestroyOwnedScriptableObjects();

            var appType = typeof(App);
            var instanceField = appType.GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Always create a fresh minimal App so tests don't share Player state.
            var appInstance = FormatterServices.GetUninitializedObject(appType);

            var playerProp = appType.GetProperty("Player");
            playerProp.SetValue(appInstance, new Player());

            var coreDefsProp = appType.GetProperty("CoreDefinitions");
            coreDefsProp.SetValue(appInstance, BuildDefaultCoreRegistry());

            instanceField.SetValue(null, appInstance);
        }

        /// <summary>
        /// Clears the injected App instance and destroys any ScriptableObjects that
        /// <see cref="EnsureAppForTests"/> created. Call in [TearDown] to avoid state
        /// leaking between test runs.
        /// </summary>
        public static void ResetApp()
        {
            var instanceField = typeof(App).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            instanceField.SetValue(null, null);

            DestroyOwnedScriptableObjects();
        }

        static void DestroyOwnedScriptableObjects()
        {
            for (int i = 0; i < _ownedScriptableObjects.Count; i++)
            {
                if (_ownedScriptableObjects[i] != null)
                    Object.DestroyImmediate(_ownedScriptableObjects[i]);
            }
            _ownedScriptableObjects.Clear();
        }

        static ICoreDefinitionRegistry BuildDefaultCoreRegistry()
        {
            var ballistic = MakeBallisticPayload("BallisticRound", "Ammo_Rifle");
            var single    = MakeDelivery("SingleAction", FiringPattern.Single);
            var auto      = MakeDelivery("Auto",         FiringPattern.Auto);

            var db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            db.SetEntries(
                new List<PayloadCoreDefinition>  { ballistic },
                new List<DeliveryCoreDefinition> { single, auto },
                new List<ExoticModDefinition>());

            _ownedScriptableObjects.Add(ballistic);
            _ownedScriptableObjects.Add(single);
            _ownedScriptableObjects.Add(auto);
            _ownedScriptableObjects.Add(db);

            return new DatabaseCoreDefinitionRegistry(db);
        }

        static BallisticPayloadDefinition MakeBallisticPayload(string id, string ammoType)
        {
            var def = ScriptableObject.CreateInstance<BallisticPayloadDefinition>();
            SetPrivateField(def, "_id", id);
            SetPrivateField(def, "_ammoType", ammoType);
            var array = new CommonPayloadStats[5];
            array[(int)RarityTier.Common] = default;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        static DeliveryCoreDefinition MakeDelivery(string id, FiringPattern pattern)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_id", id);
            SetPrivateField(def, "_pattern", pattern);
            var array = new DeliveryStats[5];
            array[(int)RarityTier.Common] = default;
            SetPrivateField(def, "_statsByTier", array);
            return def;
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
            throw new System.InvalidOperationException(
                $"Field '{fieldName}' not found on {target.GetType()}.");
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