using Dev;
using NUnit.Framework;
using State;
using UnityEditor;
using UnityEngine;
using View;

namespace Tests.EditMode
{
    /// <summary>
    /// Tier 8.x* — validate weapon prefab/SO wiring contract. Tests run у EditMode
    /// (load prefab assets via AssetDatabase, inspect Transform hierarchy + components).
    /// Catches regressions у prefab authoring що break runtime composition.
    ///
    /// Contract:
    /// • Payload prefabs are weapon roots — must have WeaponView, KickGroup, DeliverySocket, RightHandGrip.
    /// • Delivery prefabs are barrel inserts — must have MuzzlePoint.
    /// • SO assets (Payloads/*, Deliveries/*) must have BasePrefab/BarrelPrefab wired.
    /// </summary>
    [TestFixture]
    public class WeaponPrefabStructureTests
    {
        const string PayloadFolder  = "Assets/Resources/Prefabs/Modules";
        const string PayloadAssetsFolder  = "Assets/Resources/WeaponBuilder/Payloads";
        const string DeliveryAssetsFolder = "Assets/Resources/WeaponBuilder/Deliveries";

        // ── Payload (base) prefabs ────────────────────────────────

        [TestCase("Module_Payload_BallisticRound")]
        [TestCase("Module_Payload_LaserCharge")]
        public void PayloadPrefab_HasRequiredHierarchy(string prefabName)
        {
            var prefab = LoadPrefab($"{PayloadFolder}/{prefabName}.prefab");
            Assert.IsNotNull(prefab, $"Prefab not found: {prefabName}");

            // Top-level WeaponView component
            var view = prefab.GetComponent<WeaponView>();
            Assert.IsNotNull(view, $"{prefabName} must have WeaponView component на root");

            // Top-level Animator
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator, $"{prefabName} must have Animator component на root");

            // Required children
            AssertChildExists(prefab.transform, "KickGroup",       prefabName);
            AssertChildExists(prefab.transform, "RightHandGrip",   prefabName);

            // KickGroup must contain DeliverySocket (where delivery instantiates)
            var kickGroup = prefab.transform.Find("KickGroup");
            Assert.IsNotNull(kickGroup, $"{prefabName} → KickGroup missing");
            AssertChildExists(kickGroup, "DeliverySocket", prefabName);
        }

        [TestCase("Module_Payload_BallisticRound")]
        [TestCase("Module_Payload_LaserCharge")]
        public void PayloadPrefab_AnimatorHasControllerWired(string prefabName)
        {
            var prefab = LoadPrefab($"{PayloadFolder}/{prefabName}.prefab");
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator, $"{prefabName} → Animator component missing");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                $"{prefabName} → Animator.runtimeAnimatorController must be wired (Weapon_Base.controller). " +
                "Without controller, Equip/Unequip/Reload/Fire triggers no-op.");
        }

        [TestCase("Module_Payload_BallisticRound")]
        [TestCase("Module_Payload_LaserCharge")]
        public void PayloadPrefab_WeaponViewSerializedRefsWired(string prefabName)
        {
            var prefab = LoadPrefab($"{PayloadFolder}/{prefabName}.prefab");
            var view = prefab.GetComponent<WeaponView>();
            var so = new SerializedObject(view);

            var deliverySocket = so.FindProperty("_deliverySocket").objectReferenceValue;
            Assert.IsNotNull(deliverySocket, $"{prefabName} → WeaponView._deliverySocket must be wired");

            var recoilKickTarget = so.FindProperty("_recoilKickTarget").objectReferenceValue;
            Assert.IsNotNull(recoilKickTarget, $"{prefabName} → WeaponView._recoilKickTarget must be wired");

            var animatorRef = so.FindProperty("_animator").objectReferenceValue;
            Assert.IsNotNull(animatorRef, $"{prefabName} → WeaponView._animator must be wired (PlayClip routing)");

            // Wired references must point INTO the prefab hierarchy (not external).
            var deliverySocketTransform = deliverySocket as Transform;
            Assert.IsNotNull(deliverySocketTransform, "DeliverySocket reference is not a Transform");
            Assert.IsTrue(deliverySocketTransform.IsChildOf(prefab.transform),
                $"{prefabName} → _deliverySocket points outside the prefab");

            var kickTransform = recoilKickTarget as Transform;
            Assert.IsNotNull(kickTransform, "RecoilKickTarget reference is not a Transform");
            Assert.IsTrue(kickTransform.IsChildOf(prefab.transform),
                $"{prefabName} → _recoilKickTarget points outside the prefab");
        }

        // ── Delivery (barrel) prefabs ─────────────────────────────

        [TestCase("Module_Delivery_SingleAction")]
        [TestCase("Module_Delivery_Auto")]
        [TestCase("Module_Delivery_Scatter")]
        public void DeliveryPrefab_HasMuzzlePoint(string prefabName)
        {
            var prefab = LoadPrefab($"{PayloadFolder}/{prefabName}.prefab");
            Assert.IsNotNull(prefab, $"Prefab not found: {prefabName}");

            var muzzle = FindDeepChild(prefab.transform, "MuzzlePoint");
            Assert.IsNotNull(muzzle, $"{prefabName} must contain MuzzlePoint child Transform");
        }

        [TestCase("Module_Delivery_SingleAction")]
        [TestCase("Module_Delivery_Auto")]
        [TestCase("Module_Delivery_Scatter")]
        public void DeliveryPrefab_MuzzlePointAlignsWithSpawnHeight(string prefabName)
        {
            // Visual MuzzlePoint Y must align з projectile gameplay-spawn Y so flash + light
            // pulse + casing eject + tracer render at the actual bullet trajectory line.
            // Catches authoring drift коли DevCheats.Config.Parallax.ProjectileSpawnHeight
            // вupdated але delivery prefabs не regenerated. Tolerance 0.1m — accommodates
            // minor per-archetype asthetic offsets без losing visual coherence.
            var prefab = LoadPrefab($"{PayloadFolder}/{prefabName}.prefab");
            var muzzle = FindDeepChild(prefab.transform, "MuzzlePoint");
            Assert.IsNotNull(muzzle, $"{prefabName} → MuzzlePoint missing");

            var config = DevCheats.Config;
            Assert.IsNotNull(config, "DevCheats.Config not loaded — Resources/Configs/DevCheatsConfig missing?");
            float expectedY = config.Parallax.ProjectileSpawnHeight;

            // Delivery prefab is a root asset → muzzle.position.y == its accumulated local Y
            // у the prefab subtree (no external parents у asset context).
            float actualY = muzzle.position.y;
            float diff = Mathf.Abs(actualY - expectedY);
            Assert.LessOrEqual(diff, 0.1f,
                $"{prefabName} → MuzzlePoint Y ({actualY:F3}) drifts {diff:F3}m from " +
                $"ProjectileSpawnHeight ({expectedY:F3}). Run 'Tools → Weapon Builder → Create Module Prefabs' to regenerate.");
        }

        // ── SO asset wiring ───────────────────────────────────────

        [TestCase("BallisticRound")]
        [TestCase("LaserCharge")]
        public void PayloadAsset_HasBasePrefabWired(string assetName)
        {
            var path = $"{PayloadAssetsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PayloadCoreDefinition>(path);
            Assert.IsNotNull(asset, $"Asset not found: {path}");
            Assert.IsNotNull(asset.BasePrefab,
                $"{assetName}.asset must have BasePrefab wired (run 'Tools → Weapon Builder → Create Module Prefabs').");
        }

        [TestCase("SingleAction")]
        [TestCase("Auto")]
        [TestCase("Scatter")]
        public void DeliveryAsset_HasBarrelPrefabWired(string assetName)
        {
            var path = $"{DeliveryAssetsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DeliveryCoreDefinition>(path);
            Assert.IsNotNull(asset, $"Asset not found: {path}");
            Assert.IsNotNull(asset.BarrelPrefab,
                $"{assetName}.asset must have BarrelPrefab wired.");
        }

        [TestCase("BallisticRound")]
        [TestCase("LaserCharge")]
        public void PayloadAsset_BasePrefabContainsRequiredChildren(string assetName)
        {
            var path = $"{PayloadAssetsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PayloadCoreDefinition>(path);
            var prefab = asset.BasePrefab;
            Assert.IsNotNull(prefab, $"{assetName}.BasePrefab not wired");

            Assert.IsNotNull(prefab.GetComponent<WeaponView>(), "BasePrefab must have WeaponView");
            Assert.IsNotNull(prefab.transform.Find("KickGroup"), "BasePrefab must have KickGroup child");
            Assert.IsNotNull(prefab.transform.Find("RightHandGrip"), "BasePrefab must have RightHandGrip child");
        }

        [TestCase("SingleAction")]
        [TestCase("Auto")]
        [TestCase("Scatter")]
        public void DeliveryAsset_BarrelPrefabHasMuzzlePoint(string assetName)
        {
            var path = $"{DeliveryAssetsFolder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DeliveryCoreDefinition>(path);
            var prefab = asset.BarrelPrefab;
            Assert.IsNotNull(prefab, $"{assetName}.BarrelPrefab not wired");

            var muzzle = FindDeepChild(prefab.transform, "MuzzlePoint");
            Assert.IsNotNull(muzzle, $"{assetName}.BarrelPrefab must contain MuzzlePoint child");
        }

        // ── Helpers ───────────────────────────────────────────────

        static GameObject LoadPrefab(string path)
            => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        static void AssertChildExists(Transform parent, string childName, string prefabName)
        {
            var child = parent.Find(childName);
            Assert.IsNotNull(child, $"{prefabName} must have child '{childName}'");
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
