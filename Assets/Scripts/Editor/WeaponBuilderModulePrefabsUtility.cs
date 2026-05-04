using System.IO;
using State;
using UnityEditor;
using UnityEngine;
using View;

namespace Game.Editor
{
    /// <summary>
    /// Tier 8.x* — drop-in path for new module content.
    ///
    /// New asset architecture:
    ///   • Payload prefab = weapon BASE (root, hand-held). Owns WeaponView, Animator,
    ///     RightHandGrip (IK), DeliverySocket (where barrel mounts), KickGroup (recoil mesh).
    ///   • Delivery prefab = BARREL insert. Owns mesh + MuzzlePoint child. No MonoBehaviours.
    ///
    /// Iterates the central <see cref="CoreDefinitionDatabase"/> and, for any
    /// <see cref="PayloadCoreDefinition"/> / <see cref="DeliveryCoreDefinition"/>
    /// without a wired prefab, creates a primitive placeholder prefab and wires the SO.
    /// Idempotent — re-running with everything wired is a no-op.
    ///
    /// See docs/ai/weapon-builder/README.md (Workflow section).
    /// </summary>
    public static class WeaponBuilderModulePrefabsUtility
    {
        const string ModulesFolder    = "Assets/Resources/Prefabs/Modules";
        const string MaterialsFolder  = "Assets/Resources/Prefabs/Modules/Materials";
        const string DatabasePath     = "Assets/Resources/WeaponBuilder/CoreDefinitionDatabase.asset";
        const string AnimControllerPath = "Assets/Resources/Animation/Weapon_Base.controller";

        [MenuItem("Tools/Weapon Builder/Create Module Prefabs")]
        public static void CreateModulePrefabs()
        {
            var database = AssetDatabase.LoadAssetAtPath<CoreDefinitionDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"[Modules] CoreDefinitionDatabase not found at {DatabasePath}. " +
                               "Run 'Tools → Weapon Builder → Create Stub Assets' first.");
                return;
            }

            EnsureFolder(ModulesFolder,   "Assets/Resources/Prefabs", "Modules");
            EnsureFolder(MaterialsFolder, ModulesFolder,              "Materials");

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

            Debug.Log($"[Modules] Done. Payloads: created {payloadsCreated}, wired-only {payloadsWiredOnly}, " +
                      $"skipped {payloadsSkipped}. Deliveries: created {deliveriesCreated}, " +
                      $"wired-only {deliveriesWiredOnly}, skipped {deliveriesSkipped}.");
        }

        // ── Per-SO logic ──────────────────────────────────────

        enum EnsureResult { Created, WiredOnly, Skipped }

        static EnsureResult EnsurePayloadPrefab(PayloadCoreDefinition payload)
        {
            if (payload.BasePrefab != null) return EnsureResult.Skipped;

            string path = $"{ModulesFolder}/Module_Payload_{payload.Id}.prefab";

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EnsureResult outcome;

            if (prefabAsset == null)
            {
                prefabAsset = CreatePayloadBasePrimitive(payload.Id, path);
                outcome = EnsureResult.Created;
            }
            else
            {
                outcome = EnsureResult.WiredOnly;
            }

            WireObjectReference(payload, "_basePrefab", prefabAsset);
            return outcome;
        }

        static EnsureResult EnsureDeliveryPrefab(DeliveryCoreDefinition delivery)
        {
            if (delivery.BarrelPrefab != null) return EnsureResult.Skipped;

            string path = $"{ModulesFolder}/Module_Delivery_{delivery.Id}.prefab";

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EnsureResult outcome;

            if (prefabAsset == null)
            {
                prefabAsset = CreateDeliveryBarrelPrimitive(delivery.Id, path);
                outcome = EnsureResult.Created;
            }
            else
            {
                outcome = EnsureResult.WiredOnly;
            }

            WireObjectReference(delivery, "_barrelPrefab", prefabAsset);
            return outcome;
        }

        // ── Primitive prefab creation ─────────────────────────

        /// <summary>
        /// Authors payload BASE prefab (weapon root). Structure:
        ///   Module_Payload_{id} [Animator, WeaponView]
        ///   ├── KickGroup (recoil kick target)
        ///   │   ├── PayloadBaseMesh (cube — handle/receiver/magazine placeholder)
        ///   │   └── DeliverySocket (Transform — where delivery barrel attaches)
        ///   └── RightHandGrip (Transform — IK target)
        /// </summary>
        static GameObject CreatePayloadBasePrimitive(string id, string path)
        {
            var root = new GameObject($"Module_Payload_{id}");
            var animator = root.AddComponent<Animator>();
            // Wire shared base controller (Equip/Unequip/Reload/Fire/DryFire clips animate
            // root localEulerAngles via empty paths — works regardless of mesh hierarchy).
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimControllerPath);
            if (controller != null) animator.runtimeAnimatorController = controller;
            else Debug.LogWarning($"[Modules] Animator controller not found at {AnimControllerPath}");
            var view = root.AddComponent<WeaponView>();

            // KickGroup — kicks back on Fire (visual feedback). Contains all visible mesh.
            var kickGroup = new GameObject("KickGroup");
            kickGroup.transform.SetParent(root.transform, false);

            // PayloadBaseMesh — cube placeholder. Sized як "weapon body" (handle+receiver).
            var baseMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMesh.name = "PayloadBaseMesh";
            baseMesh.transform.SetParent(kickGroup.transform, false);
            baseMesh.transform.localScale    = new Vector3(0.06f, 0.10f, 0.20f);
            baseMesh.transform.localPosition = new Vector3(0f, 0f, 0f);
            Object.DestroyImmediate(baseMesh.GetComponent<Collider>());
            // Slight color difference per payload — Ballistic gray, Laser blue tint
            ApplyPlaceholderTint(baseMesh, id);

            // DeliverySocket — Transform marker, local position at front of base.
            // Delivery barrel instantiates here at runtime.
            var socket = new GameObject("DeliverySocket");
            socket.transform.SetParent(kickGroup.transform, false);
            socket.transform.localPosition = new Vector3(0f, 0f, 0.10f); // front face of base mesh

            // RightHandGrip — IK target. Sibling of KickGroup so doesn't kick з recoil.
            var grip = new GameObject("RightHandGrip");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.05f, 0.05f);

            // Wire WeaponView serialized fields BEFORE saving prefab.
            var so = new SerializedObject(view);
            so.FindProperty("_deliverySocket").objectReferenceValue   = socket.transform;
            so.FindProperty("_recoilKickTarget").objectReferenceValue = kickGroup.transform;
            so.FindProperty("_animator").objectReferenceValue         = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// Authors delivery BARREL prefab. Structure:
        ///   Module_Delivery_{id}
        ///   ├── DeliveryBarrelMesh (capsule/cylinder — barrel placeholder, length по id)
        ///   └── MuzzlePoint (Transform at barrel tip)
        /// No MonoBehaviours — purely visual.
        /// </summary>
        static GameObject CreateDeliveryBarrelPrimitive(string id, string path)
        {
            var root = new GameObject($"Module_Delivery_{id}");

            float barrelLength = id switch
            {
                "SingleAction" => 0.12f, // pistol
                "Auto"         => 0.30f, // rifle
                "Scatter"      => 0.40f, // shotgun
                _              => 0.20f, // default
            };

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barrel.name = "DeliveryBarrelMesh";
            barrel.transform.SetParent(root.transform, false);
            // Capsule Y-up by default — rotate so length points along +Z.
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale    = new Vector3(0.04f, barrelLength * 0.5f, 0.04f);
            barrel.transform.localPosition = new Vector3(0f, 0f, barrelLength * 0.5f);
            Object.DestroyImmediate(barrel.GetComponent<Collider>());
            ApplyPlaceholderTint(barrel, id);

            // MuzzlePoint at barrel tip — Y synced з DevCheats.Config.Parallax.ProjectileSpawnHeight
            // so visual flash/light/casing align з actual projectile spawn position. Test
            // DeliveryPrefab_MuzzlePointAlignsWithSpawnHeight catches drift якщо config changes
            // або prefab manually edited.
            float spawnY = Dev.DevCheats.Config?.Parallax?.ProjectileSpawnHeight ?? 0f;
            var muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, spawnY, barrelLength + 0.01f);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static void ApplyPlaceholderTint(GameObject go, string id)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            renderer.sharedMaterial = GetOrCreatePlaceholderMaterial(id);
        }

        static Material GetOrCreatePlaceholderMaterial(string id)
        {
            // Persistent .mat asset — runtime-allocated `new Material(...)` gets GC'd
            // when prefab saved → magenta on load. Save як asset, prefab references stable.
            string matPath = $"{MaterialsFolder}/Module_Mat_{id}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            Color tint = id switch
            {
                "BallisticRound" => new Color(0.45f, 0.40f, 0.35f),  // gunmetal brown
                "LaserCharge"    => new Color(0.30f, 0.50f, 0.70f),  // cool blue
                "SingleAction"   => new Color(0.30f, 0.30f, 0.30f),
                "Auto"           => new Color(0.25f, 0.25f, 0.30f),
                "Scatter"        => new Color(0.20f, 0.20f, 0.20f),
                _                => Color.gray,
            };

            if (existing != null)
            {
                existing.color = tint;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("[Modules] No Lit/Standard shader found — placeholder will be magenta.");
                return null;
            }

            var mat = new Material(shader) { color = tint };
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
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
                Debug.LogError($"[Modules] Field '{fieldName}' not found on {target.name} ({target.GetType().Name}).");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
