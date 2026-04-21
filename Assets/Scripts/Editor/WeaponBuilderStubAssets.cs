using System.IO;
using System.Reflection;
using State;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Menu action that creates (or refreshes) the Tier 0a stub assets for Weapon Builder.
    /// Idempotent — running twice updates existing assets in place.
    ///
    /// Tier 0a scope:
    ///   - BallisticRound (Common) — numeric parity with pre-migration Ballistic payload
    ///   - SingleAction (Common)   — delivery slice matching pre-migration Pistol
    ///   - Auto (Common)           — delivery slice matching pre-migration Rifle
    ///   - CoreDefinitionDatabase  — aggregator referencing the above three
    ///
    /// Only Common-tier values are populated; other tiers are left at default (zero)
    /// until the full StatsByTier table is filled in Tier 4.
    ///
    /// See docs/ai/weapon-builder/plan/tasks.md T-0a.14 / T-0a.15.
    /// </summary>
    public static class WeaponBuilderStubAssets
    {
        const string ResourcesFolder = "Assets/Resources/WeaponBuilder";
        const string PayloadsFolder  = ResourcesFolder + "/Payloads";
        const string DeliveriesFolder = ResourcesFolder + "/Deliveries";
        const string ExoticsFolder   = ResourcesFolder + "/Exotics";

        const string BallisticPath   = PayloadsFolder  + "/BallisticRound.asset";
        const string SingleActionPath = DeliveriesFolder + "/SingleAction.asset";
        const string AutoPath        = DeliveriesFolder + "/Auto.asset";
        const string DatabasePath    = ResourcesFolder + "/CoreDefinitionDatabase.asset";

        [MenuItem("Tools/Weapon Builder/Create Stub Assets")]
        public static void CreateStubAssets()
        {
            EnsureFolders();

            var ballistic = GetOrCreate<BallisticPayloadDefinition>(BallisticPath);
            PopulateBallistic(ballistic);

            var single = GetOrCreate<DeliveryCoreDefinition>(SingleActionPath);
            PopulateSingleAction(single);

            var auto = GetOrCreate<DeliveryCoreDefinition>(AutoPath);
            PopulateAuto(auto);

            var database = GetOrCreate<CoreDefinitionDatabase>(DatabasePath);
            PopulateDatabase(database, ballistic, single, auto);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[WeaponBuilderStubAssets] Stub assets created / refreshed:\n" +
                      $"  {BallisticPath}\n  {SingleActionPath}\n  {AutoPath}\n  {DatabasePath}");
        }

        // ── Folder setup ──────────────────────────────────────

        static void EnsureFolders()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "WeaponBuilder");
            EnsureFolder(ResourcesFolder, "Payloads");
            EnsureFolder(ResourcesFolder, "Deliveries");
            EnsureFolder(ResourcesFolder, "Exotics");
        }

        static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ── Asset resolve-or-create ───────────────────────────

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ── Population ────────────────────────────────────────

        static void PopulateBallistic(BallisticPayloadDefinition def)
        {
            SetField(def, "_id",          "BallisticRound");
            SetField(def, "_archetype",   "Ballistic");
            SetField(def, "_displayName", "Ballistic");
            SetField(def, "_ammoType",    "Ammo_Rifle");

            var stats = new CommonPayloadStats[5];
            stats[(int)RarityTier.Common] = new CommonPayloadStats
            {
                Damage                   = 15f,
                ProjectileSpeed          = 25f,
                ProjectileLifetime       = 2.5f,
                HeadshotDamageMultiplier = 2.0f,
                BasePenetration          = 15f,
                BaseArmorDamage          = 5f,
                BaseBleedChance          = 0f,
            };
            SetField(def, "_statsByTier", stats);

            EditorUtility.SetDirty(def);
        }

        static void PopulateSingleAction(DeliveryCoreDefinition def)
        {
            SetField(def, "_id",         "SingleAction");
            SetField(def, "_formFactor", "Pistol");
            SetField(def, "_pattern",    FiringPattern.Single);

            var stats = new DeliveryStats[5];
            stats[(int)RarityTier.Common] = new DeliveryStats
            {
                // Values sourced from WeaponEntityState.CreatePistol
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
            };
            SetField(def, "_statsByTier", stats);

            // Pattern-specific params (Single uses none)
            SetField(def, "_spinUpTime",     0f);
            SetField(def, "_spinDownTime",   0f);
            SetField(def, "_volleyCount",    0);
            SetField(def, "_volleyInterval", 0f);

            EditorUtility.SetDirty(def);
        }

        static void PopulateAuto(DeliveryCoreDefinition def)
        {
            SetField(def, "_id",         "Auto");
            SetField(def, "_formFactor", "Rifle");
            SetField(def, "_pattern",    FiringPattern.Auto);

            var stats = new DeliveryStats[5];
            stats[(int)RarityTier.Common] = new DeliveryStats
            {
                // Values sourced from WeaponEntityState.CreateRifle
                FireInterval        = 0.2f,
                ProjectilesPerShot  = 1,
                SpreadAngle         = 0f,
                ConeHalfAngle       = 45f,
                BodyRotationSpeed   = 270f,
                AimFollowSharpness  = 10f,
                RecoilKickForward   = 2f,
                RecoilKickSide      = 1.5f,
                RecoilRecoverySpeed = 2f,
                EquipTime           = 0.3f,
                UnequipTime         = 0.2f,
                MagazineSize        = 30,
                ReloadTime          = 2.0f,
            };
            SetField(def, "_statsByTier", stats);

            SetField(def, "_spinUpTime",     0f);
            SetField(def, "_spinDownTime",   0f);
            SetField(def, "_volleyCount",    0);
            SetField(def, "_volleyInterval", 0f);

            EditorUtility.SetDirty(def);
        }

        static void PopulateDatabase(
            CoreDefinitionDatabase db,
            BallisticPayloadDefinition ballistic,
            DeliveryCoreDefinition single,
            DeliveryCoreDefinition auto)
        {
            db.SetEntries(
                new System.Collections.Generic.List<PayloadCoreDefinition>  { ballistic },
                new System.Collections.Generic.List<DeliveryCoreDefinition> { single, auto },
                new System.Collections.Generic.List<ExoticModDefinition>());
            EditorUtility.SetDirty(db);
        }

        // ── Reflection helper ─────────────────────────────────

        static void SetField(object target, string fieldName, object value)
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
            Debug.LogError($"[WeaponBuilderStubAssets] Field '{fieldName}' not found on {target.GetType().Name}.");
        }
    }
}
