using System.Collections.Generic;
using System.IO;
using State;
using UnityEditor;
using UnityEngine;
using View;

namespace Game.Editor
{
    /// <summary>
    /// Tier 8 Wave E — drop-in path for new module content.
    ///
    /// Iterates the central <see cref="CoreDefinitionDatabase"/> and, for any
    /// <see cref="PayloadCoreDefinition"/> / <see cref="DeliveryCoreDefinition"/>
    /// without a wired visual prefab, creates a primitive placeholder prefab and
    /// wires the SO's reference. Idempotent — re-running with everything wired
    /// is a no-op.
    ///
    /// Workflow when adding a new module SO (e.g., FoamPayloadDefinition for Tier 3):
    ///   1. Create the SO asset under Resources/WeaponBuilder/Payloads/ or /Deliveries/.
    ///   2. Add it to <c>CoreDefinitionDatabase.asset</c>'s array.
    ///   3. Run <c>Tools → Weapon Builder → Create Module Prefabs</c>.
    ///   4. Primitive prefab is created at the canonical path and wired.
    ///   5. Replace the primitive with a real mesh (artist drop-in) without touching code/SO setup.
    ///
    /// See docs/ai/weapon-builder/README.md (Workflow section).
    /// </summary>
    public static class WeaponBuilderModulePrefabsUtility
    {
        const string PayloadFolder  = "Assets/Resources/Prefabs/Modules";
        const string DeliveryFolder = "Assets/Resources/Prefabs/Weapons";
        const string DatabasePath   = "Assets/Resources/WeaponBuilder/CoreDefinitionDatabase.asset";

        [MenuItem("Tools/Weapon Builder/Create Module Prefabs")]
        public static void CreateModulePrefabs()
        {
            var database = AssetDatabase.LoadAssetAtPath<CoreDefinitionDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"[Wave E] CoreDefinitionDatabase not found at {DatabasePath}. " +
                               "Run 'Tools → Weapon Builder → Create Stub Assets' first.");
                return;
            }

            EnsureFolder(PayloadFolder,  "Assets/Resources/Prefabs", "Modules");
            EnsureFolder(DeliveryFolder, "Assets/Resources/Prefabs", "Weapons");

            int payloadsCreated  = 0, payloadsWiredOnly  = 0, payloadsSkipped  = 0;
            int deliveriesCreated = 0, deliveriesWiredOnly = 0, deliveriesSkipped = 0;

            foreach (var payload in database.Payloads)
            {
                if (payload == null) continue;
                var result = EnsurePayloadPrefab(payload);
                if      (result == EnsureResult.Created)   payloadsCreated++;
                else if (result == EnsureResult.WiredOnly) payloadsWiredOnly++;
                else                                       payloadsSkipped++;
            }

            foreach (var delivery in database.Deliveries)
            {
                if (delivery == null) continue;
                var result = EnsureDeliveryPrefab(delivery);
                if      (result == EnsureResult.Created)   deliveriesCreated++;
                else if (result == EnsureResult.WiredOnly) deliveriesWiredOnly++;
                else                                       deliveriesSkipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Wave E] Done. Payloads: created {payloadsCreated}, wired-only {payloadsWiredOnly}, " +
                      $"skipped {payloadsSkipped}. Deliveries: created {deliveriesCreated}, " +
                      $"wired-only {deliveriesWiredOnly}, skipped {deliveriesSkipped}.");
        }

        // ── Per-SO logic ──────────────────────────────────────

        enum EnsureResult { Created, WiredOnly, Skipped }

        static EnsureResult EnsurePayloadPrefab(PayloadCoreDefinition payload)
        {
            // Skip if already wired — utility is non-destructive.
            if (payload.AttachmentPrefab != null) return EnsureResult.Skipped;

            string path = $"{PayloadFolder}/Module_Payload_{payload.Id}.prefab";

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EnsureResult outcome;

            if (prefabAsset == null)
            {
                prefabAsset = CreatePayloadPrimitive(payload.Id, path);
                outcome = EnsureResult.Created;
            }
            else
            {
                // Prefab exists at canonical path, SO just isn't pointing at it. Wire only.
                outcome = EnsureResult.WiredOnly;
            }

            WireObjectReference(payload, "_attachmentPrefab", prefabAsset);
            return outcome;
        }

        static EnsureResult EnsureDeliveryPrefab(DeliveryCoreDefinition delivery)
        {
            if (delivery.WeaponPrefab != null) return EnsureResult.Skipped;

            string path = $"{DeliveryFolder}/Weapon_{delivery.Id}.prefab";

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EnsureResult outcome;

            if (prefabAsset == null)
            {
                prefabAsset = CreateDeliveryPrimitive(delivery.Id, path);
                outcome = EnsureResult.Created;
            }
            else
            {
                outcome = EnsureResult.WiredOnly;
            }

            WireObjectReference(delivery, "_weaponPrefab", prefabAsset);
            return outcome;
        }

        // ── Primitive prefab creation ─────────────────────────

        static GameObject CreatePayloadPrimitive(string id, string path)
        {
            // Wrapper root + primitive cube child (skinny barrel-like). Matches the
            // structure of the Wave B/C real prefabs (Module_Payload_BallisticBarrel).
            var root = new GameObject($"Module_Payload_{id}");
            var meshGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshGO.name = "PrimitiveMesh";
            meshGO.transform.SetParent(root.transform, false);
            meshGO.transform.localScale = new Vector3(0.05f, 0.05f, 0.30f);
            Object.DestroyImmediate(meshGO.GetComponent<Collider>());

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static GameObject CreateDeliveryPrimitive(string id, string path)
        {
            var root = new GameObject($"Weapon_{id}");
            root.AddComponent<Animator>();
            var view = root.AddComponent<WeaponView>();

            var deliveryBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            deliveryBody.name = "DeliveryBody";
            deliveryBody.transform.SetParent(root.transform, false);
            deliveryBody.transform.localScale = new Vector3(0.05f, 0.10f, 0.40f);
            // Capsule is Y-up by default — rotate to point along +Z for a weapon-like silhouette.
            deliveryBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            deliveryBody.transform.localPosition = new Vector3(0f, 0f, 0.20f);
            Object.DestroyImmediate(deliveryBody.GetComponent<Collider>());

            var muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);

            var grip = new GameObject("RightHandGrip");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            var mount = new GameObject("PayloadMount");
            mount.transform.SetParent(root.transform, false);
            mount.transform.localPosition = new Vector3(0f, 0.05f, 0.40f);

            // Wire WeaponView serialized fields BEFORE saving the prefab.
            var so = new SerializedObject(view);
            so.FindProperty("_muzzlePoint").objectReferenceValue  = muzzle.transform;
            so.FindProperty("_payloadMount").objectReferenceValue = mount.transform;
            so.FindProperty("_deliveryBody").objectReferenceValue = deliveryBody.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ── Helpers ───────────────────────────────────────────

        static void EnsureFolder(string fullPath, string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, name);
        }

        static void WireObjectReference(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Wave E] Field '{fieldName}' not found on {target.name} ({target.GetType().Name}).");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
