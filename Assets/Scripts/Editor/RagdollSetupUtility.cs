using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Gunplay A.9 — programmatic ragdoll authoring on `CharacterBody.prefab`.
    /// Idempotent: re-runs strip existing Rigidbody/Collider/CharacterJoint components
    /// from ragdoll bones, then re-add fresh з current parameters.
    ///
    /// Bone layout (Character01 chibi rig):
    ///   Hips (root) → Spine → UpperChest → Head (skip Neck — too small)
    ///                                   → UpperArm_L/R → LowerArm_L/R
    ///                       → UpperLeg_L/R → LowerLeg_L/R
    ///
    /// All Rigidbodies start with isKinematic = true — Animator drives bones nominally.
    /// Runtime <see cref="View.RagdollController"/> toggles them на active when entity dies.
    /// </summary>
    public static class RagdollSetupUtility
    {
        const string CharacterBodyPath = "Assets/Resources/Prefabs/Bodies/CharacterBody.prefab";
        const string SkeletonRoot      = "Character01/Character01MeshSkinned/Root";

        [MenuItem("Tools/Gunplay/Build Ragdoll on CharacterBody")]
        public static void BuildOnCharacterBody()
        {
            var contents = PrefabUtility.LoadPrefabContents(CharacterBodyPath);
            try
            {
                BuildRagdoll(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, CharacterBodyPath);
                Debug.Log("[RagdollSetup] Ragdoll авторено на " + CharacterBodyPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void BuildRagdoll(GameObject root)
        {
            var t = root.transform;
            var hips       = t.Find(SkeletonRoot + "/Hips");
            var spine      = hips.Find("Spine");
            var upperChest = spine.Find("UpperChest");
            var head       = upperChest.Find("Neck/Head");
            var shoulderL  = upperChest.Find("Shoulder_L");
            var shoulderR  = upperChest.Find("Shoulder_R");
            var upperArmL  = shoulderL.Find("UpperArm_L");
            var upperArmR  = shoulderR.Find("UpperArm_R");
            var lowerArmL  = upperArmL.Find("LowerArm_L");
            var lowerArmR  = upperArmR.Find("LowerArm_R");
            var upperLegL  = hips.Find("UpperLeg_L");
            var upperLegR  = hips.Find("UpperLeg_R");
            var lowerLegL  = upperLegL.Find("LowerLeg_L");
            var lowerLegR  = upperLegR.Find("LowerLeg_R");

            var allBones = new[]
            {
                hips, spine, upperChest, head,
                upperArmL, upperArmR, lowerArmL, lowerArmR,
                upperLegL, upperLegR, lowerLegL, lowerLegR,
            };

            // Idempotency: strip prior ragdoll components.
            foreach (var b in allBones)
            {
                if (b == null) continue;
                StripExisting(b.gameObject);
            }

            // Hips — Box collider, root Rigidbody (no joint).
            AddBoxBody(hips, size: new Vector3(0.22f, 0.18f, 0.18f), center: Vector3.zero, mass: 4f);

            // Torso chain. Local +X axis points from parent → child у this rig (negative X
            // у child localPosition = bone extends to child along -X). Capsule center у -X
            // direction matches mesh visual.
            AddCapsuleBody(spine,      radius: 0.10f, height: 0.22f, dir: 0,
                                       center: new Vector3(-0.10f, 0f, 0f), mass: 2f, parent: hips);
            AddCapsuleBody(upperChest, radius: 0.13f, height: 0.22f, dir: 0,
                                       center: new Vector3(-0.10f, 0f, 0f), mass: 3f, parent: spine);
            AddSphereBody (head,       radius: 0.14f,
                                       center: new Vector3(-0.18f, 0f, 0f), mass: 1.5f, parent: upperChest);

            // Arms — joint скипає Shoulder bone (it's a 33mm offset, not a separate physics body).
            AddCapsuleBody(upperArmL, radius: 0.05f, height: 0.32f, dir: 0,
                                      center: new Vector3(-0.14f, 0f, 0f), mass: 1f, parent: upperChest);
            AddCapsuleBody(upperArmR, radius: 0.05f, height: 0.32f, dir: 0,
                                      center: new Vector3(-0.14f, 0f, 0f), mass: 1f, parent: upperChest);
            AddCapsuleBody(lowerArmL, radius: 0.045f, height: 0.24f, dir: 0,
                                      center: new Vector3(-0.10f, 0f, 0f), mass: 0.7f, parent: upperArmL);
            AddCapsuleBody(lowerArmR, radius: 0.045f, height: 0.24f, dir: 0,
                                      center: new Vector3(-0.10f, 0f, 0f), mass: 0.7f, parent: upperArmR);

            // Legs.
            AddCapsuleBody(upperLegL, radius: 0.07f, height: 0.22f, dir: 0,
                                      center: new Vector3(-0.09f, 0f, 0f), mass: 2f, parent: hips);
            AddCapsuleBody(upperLegR, radius: 0.07f, height: 0.22f, dir: 0,
                                      center: new Vector3(-0.09f, 0f, 0f), mass: 2f, parent: hips);
            AddCapsuleBody(lowerLegL, radius: 0.06f, height: 0.14f, dir: 0,
                                      center: new Vector3(-0.05f, 0f, 0f), mass: 1.5f, parent: upperLegL);
            AddCapsuleBody(lowerLegR, radius: 0.06f, height: 0.14f, dir: 0,
                                      center: new Vector3(-0.05f, 0f, 0f), mass: 1.5f, parent: upperLegR);
        }

        static void StripExisting(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) Object.DestroyImmediate(rb, true);
            var cj = go.GetComponent<CharacterJoint>();
            if (cj != null) Object.DestroyImmediate(cj, true);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col, true);
        }

        static Rigidbody AddBoxBody(Transform bone, Vector3 size, Vector3 center, float mass)
        {
            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true; // активується runtime при death
            var col = bone.gameObject.AddComponent<BoxCollider>();
            col.size = size;
            col.center = center;
            return rb;
        }

        static Rigidbody AddCapsuleBody(Transform bone, float radius, float height, int dir,
                                         Vector3 center, float mass, Transform parent)
        {
            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true;
            var col = bone.gameObject.AddComponent<CapsuleCollider>();
            col.direction = dir;
            col.radius = radius;
            col.height = height;
            col.center = center;

            if (parent != null)
                AttachJoint(bone, parent);
            return rb;
        }

        static Rigidbody AddSphereBody(Transform bone, float radius, Vector3 center, float mass, Transform parent)
        {
            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = true;
            var col = bone.gameObject.AddComponent<SphereCollider>();
            col.radius = radius;
            col.center = center;

            if (parent != null)
                AttachJoint(bone, parent);
            return rb;
        }

        static void AttachJoint(Transform bone, Transform parent)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.GetComponent<Rigidbody>();
            joint.enableProjection = true; // helps keep joints aligned during fast motion
            // Default limits — Unity defaults are reasonable: ±20° twist, ±40° swing 1, ±40° swing 2.
            // Tunable per playtest якщо чібі-flop виглядає кривувато.
        }
    }
}
