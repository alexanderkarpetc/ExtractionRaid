using System.Reflection;
using State;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Menu action that creates (or refreshes) the stub core-definition assets for the
    /// Weapon Builder. Idempotent — running twice updates existing assets in place.
    ///
    /// Populated content (Common tier only; other tiers zero-filled until Tier 4):
    ///   Payloads:
    ///     - BallisticRound  — numeric parity with pre-migration Ballistic weapons
    ///     - LaserCharge     — charge-up energy payload (Tier 2)
    ///   Deliveries:
    ///     - SingleAction    — Pistol-like, 1 pellet, slower cadence
    ///     - Auto            — Rifle-like, sustained automatic
    ///     - Scatter         — Shotgun-like, 7 pellets with spread (Tier 2)
    ///   Exotics: none yet (Tier 5)
    ///   CoreDefinitionDatabase — aggregator referencing the above.
    ///
    /// See docs/ai/weapon-builder/plan/tasks.md T-0a.14/15 and T-2.06..T-2.09.
    /// </summary>
    public static class WeaponBuilderStubAssets
    {
        const string ResourcesFolder    = "Assets/Resources/WeaponBuilder";
        const string PayloadsFolder     = ResourcesFolder + "/Payloads";
        const string DeliveriesFolder   = ResourcesFolder + "/Deliveries";
        const string ExoticsFolder      = ResourcesFolder + "/Exotics";
        const string AttachmentsFolder  = ResourcesFolder + "/Attachments";

        const string BallisticPath    = PayloadsFolder   + "/BallisticRound.asset";
        const string LaserPath        = PayloadsFolder   + "/LaserCharge.asset";
        const string SingleActionPath = DeliveriesFolder + "/SingleAction.asset";
        const string AutoPath         = DeliveriesFolder + "/Auto.asset";
        const string ScatterPath      = DeliveriesFolder + "/Scatter.asset";
        const string DatabasePath     = ResourcesFolder  + "/CoreDefinitionDatabase.asset";

        // NOTE: weapon/barrel prefab wiring is NOT regenerated here. The old generator set
        // `_attachmentPrefab` (payload) / `_weaponPrefab` (delivery), but the Tier-8 visualization
        // refactor renamed those to `_basePrefab` / `_barrelPrefab` AND flipped the payload↔delivery
        // roles — so the old assignments were dead (SetField no-op + console spam) and wrong-model.
        // Prefab refs now live on the core assets directly (preserved across regen by GetOrCreate).
        // A correct Tier-8 prefab generator pass is a separate task.

        [MenuItem("Tools/Weapon Builder/Create Stub Assets")]
        public static void CreateStubAssets()
        {
            EnsureFolders();

            var ballistic = GetOrCreate<BallisticPayloadDefinition>(BallisticPath);
            PopulateBallistic(ballistic);

            var laser = GetOrCreate<LaserPayloadDefinition>(LaserPath);
            PopulateLaser(laser);

            var single = GetOrCreate<DeliveryCoreDefinition>(SingleActionPath);
            PopulateSingleAction(single);

            var auto = GetOrCreate<DeliveryCoreDefinition>(AutoPath);
            PopulateAuto(auto);

            var scatter = GetOrCreate<DeliveryCoreDefinition>(ScatterPath);
            PopulateScatter(scatter);

            var attachments = CreateAttachmentStubs();

            var database = GetOrCreate<CoreDefinitionDatabase>(DatabasePath);
            PopulateDatabase(database, ballistic, laser, single, auto, scatter, attachments);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[WeaponBuilderStubAssets] Stub assets created / refreshed:\n" +
                      $"  {BallisticPath}\n  {LaserPath}\n  {SingleActionPath}\n  " +
                      $"{AutoPath}\n  {ScatterPath}\n  {DatabasePath}\n" +
                      $"  + {attachments.Count} attachment stubs in {AttachmentsFolder}");
        }

        // ── Attachments (P2.3 base mods + P3 unique mods) ─────────────────
        // Numbers are catalog.md placeholders, tunable later. Universal mods leave
        // CompatibleArchetype empty; the 3 unique mods (P3) restrict to a payload archetype
        // ("Laser") or delivery firing pattern ("Scatter"/"Auto"). Unique-mod effects use
        // existing stat axes as proxies — the true charge/heat versions await P4 mechanics.

        static System.Collections.Generic.List<AttachmentDefinition> CreateAttachmentStubs()
        {
            return new System.Collections.Generic.List<AttachmentDefinition>
            {
                MakeAttachment("PowerComp",     "Power Compensator", AttachmentSlot.Muzzle,
                    (WeaponStatAxis.Damage, 12f), (WeaponStatAxis.Recoil, 15f), (WeaponStatAxis.Spread, 10f)),
                MakeAttachment("MuzzleBrake",   "Muzzle Brake",      AttachmentSlot.Muzzle,
                    (WeaponStatAxis.Recoil, -25f), (WeaponStatAxis.Ergonomics, -10f)),
                MakeAttachment("VerticalGrip",  "Vertical Grip",     AttachmentSlot.Grip,
                    (WeaponStatAxis.Recoil, -15f)),
                MakeAttachment("AngledGrip",    "Angled Grip",       AttachmentSlot.Grip,
                    (WeaponStatAxis.Ergonomics, 15f), (WeaponStatAxis.Recoil, -5f)),
                MakeAttachment("HeavyStock",    "Heavy Stock",       AttachmentSlot.Buttstock,
                    (WeaponStatAxis.Recoil, -25f), (WeaponStatAxis.Ergonomics, -20f)),
                MakeAttachment("SkeletonStock", "Skeleton Stock",    AttachmentSlot.Buttstock,
                    (WeaponStatAxis.Ergonomics, 20f), (WeaponStatAxis.Recoil, 10f)),
                MakeAttachment("RedDot",        "Red Dot Sight",     AttachmentSlot.Optic,
                    (WeaponStatAxis.Spread, -10f), (WeaponStatAxis.Ergonomics, 5f)),
                MakeAttachment("ExtendedMag",   "Extended Magazine", AttachmentSlot.Magazine,
                    (WeaponStatAxis.MagazineSize, 50f), (WeaponStatAxis.ReloadTime, 20f), (WeaponStatAxis.Ergonomics, -10f)),
                MakeAttachment("QuickMag",      "Quick Magazine",    AttachmentSlot.Magazine,
                    (WeaponStatAxis.ReloadTime, -25f), (WeaponStatAxis.Ergonomics, 5f), (WeaponStatAxis.MagazineSize, -20f)),

                // ── Unique (archetype-restricted) — P3. Existing-axis proxies for charge/heat. ──
                MakeUniqueAttachment("LaserFocusing", "Laser Focusing Optic", AttachmentSlot.Optic, "Laser",
                    (WeaponStatAxis.Damage, 12f), (WeaponStatAxis.Ergonomics, -12f)),
                MakeUniqueAttachment("ScatterChoke",  "Scatter Choke",        AttachmentSlot.Muzzle, "Scatter",
                    (WeaponStatAxis.Spread, -30f), (WeaponStatAxis.Recoil, 10f)),
                MakeUniqueAttachment("AutoHeatSink",  "Auto Heat-Sink",       AttachmentSlot.Muzzle, "Auto",
                    (WeaponStatAxis.Recoil, -20f), (WeaponStatAxis.Damage, -8f)),
            };
        }

        static AttachmentDefinition MakeAttachment(
            string id, string displayName, AttachmentSlot slot,
            params (WeaponStatAxis axis, float percent)[] deltas)
        {
            var def = GetOrCreate<AttachmentDefinition>($"{AttachmentsFolder}/{id}.asset");
            SetField(def, "_id",                  id);
            SetField(def, "_displayName",         displayName);
            SetField(def, "_slot",                slot);
            SetField(def, "_compatibleArchetype", string.Empty); // universal (MVP)

            var mods = new StatDelta[deltas.Length];
            for (int i = 0; i < deltas.Length; i++)
                mods[i] = new StatDelta { Axis = deltas[i].axis, Percent = deltas[i].percent };
            SetField(def, "_modifiers", mods);

            EditorUtility.SetDirty(def);
            return def;
        }

        // Unique (archetype-restricted) variant — builds like MakeAttachment, then stamps the
        // CompatibleArchetype token (payload archetype / delivery form-factor / firing pattern).
        static AttachmentDefinition MakeUniqueAttachment(
            string id, string displayName, AttachmentSlot slot, string archetype,
            params (WeaponStatAxis axis, float percent)[] deltas)
        {
            var def = MakeAttachment(id, displayName, slot, deltas);
            SetField(def, "_compatibleArchetype", archetype);
            EditorUtility.SetDirty(def);
            return def;
        }

        // ── Folder setup ──────────────────────────────────────

        static void EnsureFolders()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "WeaponBuilder");
            EnsureFolder(ResourcesFolder, "Payloads");
            EnsureFolder(ResourcesFolder, "Deliveries");
            EnsureFolder(ResourcesFolder, "Exotics");
            EnsureFolder(ResourcesFolder, "Attachments");
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

        // ── Payload: Ballistic ────────────────────────────────

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

        // ── Payload: Laser Charge (Tier 2) ────────────────────

        static void PopulateLaser(LaserPayloadDefinition def)
        {
            SetField(def, "_id",          "LaserCharge");
            SetField(def, "_archetype",   "Laser");
            SetField(def, "_displayName", "Laser");
            SetField(def, "_ammoType",    "Ammo_EnergyCell");

            // Common-tier payload stats: higher single-shot damage and projectile speed
            // than Ballistic — compensates for the charge-up overhead.
            var stats = new CommonPayloadStats[5];
            stats[(int)RarityTier.Common] = new CommonPayloadStats
            {
                Damage                   = 25f,
                ProjectileSpeed          = 50f,
                ProjectileLifetime       = 3f,
                HeadshotDamageMultiplier = 2.0f,
                BasePenetration          = 25f,
                BaseArmorDamage          = 8f,
                BaseBleedChance          = 0f, // energy — no bleed
            };
            SetField(def, "_statsByTier", stats);

            // Laser-specific: ChargeTime. 1s is a sensible starting point for MVP feel.
            var specific = new LaserSpecificStats[5];
            specific[(int)RarityTier.Common] = new LaserSpecificStats { ChargeTime = 1.0f };
            SetField(def, "_specificByTier", specific);

            EditorUtility.SetDirty(def);
        }

        // ── Delivery: Single-Action ───────────────────────────

        static void PopulateSingleAction(DeliveryCoreDefinition def)
        {
            SetField(def, "_id",         "SingleAction");
            SetField(def, "_formFactor", "Pistol");
            SetField(def, "_pattern",    FiringPattern.Single);

            var stats = new DeliveryStats[5];
            stats[(int)RarityTier.Common] = new DeliveryStats
            {
                // Values sourced from pre-migration Pistol stats (Ballistic + SingleAction, Common tier)
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

        // ── Delivery: Auto ────────────────────────────────────

        static void PopulateAuto(DeliveryCoreDefinition def)
        {
            SetField(def, "_id",         "Auto");
            SetField(def, "_formFactor", "Rifle");
            SetField(def, "_pattern",    FiringPattern.Auto);

            var stats = new DeliveryStats[5];
            stats[(int)RarityTier.Common] = new DeliveryStats
            {
                // Values sourced from pre-migration Rifle stats (Ballistic + Auto, Common tier)
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

        // ── Delivery: Scatter (Tier 2) ────────────────────────

        static void PopulateScatter(DeliveryCoreDefinition def)
        {
            SetField(def, "_id",         "Scatter");
            SetField(def, "_formFactor", "Shotgun");
            SetField(def, "_pattern",    FiringPattern.Scatter);

            var stats = new DeliveryStats[5];
            stats[(int)RarityTier.Common] = new DeliveryStats
            {
                // Shotgun-like: slow cadence, heavy recoil, multiple pellets in a wide cone.
                // Values mirror pre-migration Shotgun tuning where applicable.
                FireInterval        = 0.6f,
                ProjectilesPerShot  = 7,
                SpreadAngle         = 30f,
                ConeHalfAngle       = 20f,
                BodyRotationSpeed   = 180f,
                AimFollowSharpness  = 5f,
                RecoilKickForward   = 3f,
                RecoilKickSide      = 6f,
                RecoilRecoverySpeed = 3f,
                EquipTime           = 0.4f,
                UnequipTime         = 0.25f,
                MagazineSize        = 5,
                ReloadTime          = 2.5f,
            };
            SetField(def, "_statsByTier", stats);

            SetField(def, "_spinUpTime",     0f);
            SetField(def, "_spinDownTime",   0f);
            SetField(def, "_volleyCount",    0);
            SetField(def, "_volleyInterval", 0f);

            EditorUtility.SetDirty(def);
        }

        // ── Database aggregator ───────────────────────────────

        static void PopulateDatabase(
            CoreDefinitionDatabase db,
            BallisticPayloadDefinition ballistic,
            LaserPayloadDefinition laser,
            DeliveryCoreDefinition single,
            DeliveryCoreDefinition auto,
            DeliveryCoreDefinition scatter,
            System.Collections.Generic.List<AttachmentDefinition> attachments)
        {
            db.SetEntries(
                new System.Collections.Generic.List<PayloadCoreDefinition>  { ballistic, laser },
                new System.Collections.Generic.List<DeliveryCoreDefinition> { single, auto, scatter },
                new System.Collections.Generic.List<ExoticModDefinition>(),
                attachments);
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
