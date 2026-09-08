using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

namespace LevelDesign.Editor
{
    public sealed class ScatterGenerator
    {
        sealed class Candidate
        {
            public ScatterPrefabEntry entry;
            public GameObject prefab;
            public Bounds bounds;
        }
        public static string Validate(ScatterSettings settings)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return "Use a normal scene in Edit Mode.";
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return "Save the scene first (persistent object references).";
            if (!settings.area.Valid) return "Area sizes must be positive and finite.";
            if (settings.targetObjectCount < 1 || settings.targetObjectCount > Mathf.Clamp(settings.maximumGeneratedObjects, 1, 10000))
                return "Target count exceeds Maximum Generated Objects (hard cap: 10,000).";
            if (settings.maxPlacementAttempts < 1 || settings.maxPlacementAttempts > 1000000) return "Attempts must be between 1 and 1,000,000.";
            if (settings.raycastHeight <= 0 || settings.globalScaleMin <= 0 || settings.globalScaleMax <= 0 ||
                settings.globalMinDistance < 0 || settings.collisionRadiusMultiplier <= 0) return "Check positive scale, ray height, collision multiplier and distance.";
            if (settings.prefabs.Count == 0) return "Add at least one enabled prefab.";
            bool any = false;
            foreach (var entry in settings.prefabs)
            {
                if (!entry.enabled || entry.weight <= 0) continue;
                var prefab = entry.prefab.Resolve() as GameObject;
                if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.transform.parent != null || !prefab.activeSelf)
                    return "Each enabled entry must reference an active prefab asset root.";
                if (entry.scaleMin <= 0 || entry.scaleMax <= 0 || !float.IsFinite(entry.weight)) return "Check prefab scales and weights.";
                if (!ScatterGeometry.TryPrefabBounds(prefab, out _)) return "Each prefab needs active renderer bounds for grounding and collision.";
                any = true;
            }
            return any ? null : "No enabled prefab with positive weight.";
        }

        public IEnumerator Generate(ScatterSettings settings, Action<string> report)
        {
            var candidates = new List<Candidate>();
            var weights = new List<float>();
            float maxDistance = settings.globalMinDistance;
            foreach (var entry in settings.prefabs)
            {
                if (!entry.enabled || entry.weight <= 0) continue;
                var prefab = (GameObject)entry.prefab.Resolve();
                ScatterGeometry.TryPrefabBounds(prefab, out var bounds);
                candidates.Add(new Candidate { entry = entry, prefab = prefab, bounds = bounds });
                weights.Add(entry.weight);
                maxDistance = Mathf.Max(maxDistance, entry.minDistanceOverride);
            }
            Clear(settings);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Scatter Preview");
            var parent = new GameObject("EnvironmentScatter_PREVIEW");
            Undo.RegisterCreatedObjectUndo(parent, "Create Scatter Preview");
            Undo.RecordObject(settings, "Track Scatter Preview");
            settings.preview.Set(parent);
            var random = new System.Random(settings.seed);
            var validator = new ScatterPlacementValidator(maxDistance);
            var physics = parent.scene.GetPhysicsScene();
            var hits = new Collider[256];
            var groundHits = new RaycastHit[256];
            var exclusions = new List<Collider>();
            foreach (var reference in settings.exclusionColliders)
            {
                var obj = reference.Resolve();
                var collider = obj as Collider;
                if (!collider && obj is GameObject go) collider = go.GetComponent<Collider>();
                if (collider) exclusions.Add(collider);
            }
            Physics.SyncTransforms();
            int count = 0;
            for (int attempt = 0; attempt < settings.maxPlacementAttempts && count < settings.targetObjectCount; attempt++)
            {
                yield return null;
                if (!parent) yield break;
                var candidate = candidates[ScatterGeometry.WeightedIndex(weights, random)];
                var entry = candidate.entry;
                var point = settings.area.Point(ScatterGeometry.Range(random, -.5f, .5f), ScatterGeometry.Range(random, -.5f, .5f));
                float yaw = settings.randomRotationY && entry.allowRotationY
                    ? ScatterGeometry.Range(random, settings.rotationMin, settings.rotationMax) : 0;
                float scale = ScatterGeometry.Range(random, entry.scaleMin, entry.scaleMax) *
                    ScatterGeometry.Range(random, settings.globalScaleMin, settings.globalScaleMax);
                int groundCount = physics.Raycast(point + Vector3.up * settings.raycastHeight, Vector3.down, groundHits,
                    settings.raycastHeight * 2, settings.groundLayerMask, QueryTriggerInteraction.Ignore);
                if (groundCount == groundHits.Length) continue;
                RaycastHit ground = default;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < groundCount; i++)
                    if (!groundHits[i].transform.IsChildOf(parent.transform) && groundHits[i].distance < nearest)
                    { ground = groundHits[i]; nearest = ground.distance; }
                if (!ground.collider) continue;
                if (Vector3.Angle(ground.normal, Vector3.up) > settings.maxGroundSlope) continue;
                if (ScatterAvoidance.Excluded(ground.point, exclusions, physics, settings.exclusionLayerMask, hits)) continue;
                if (settings.avoidNavMesh && NavMesh.SamplePosition(ground.point, out _,
                    Mathf.Max(.01f, settings.navMeshAvoidanceRadius), NavMesh.AllAreas)) continue;
                float distance = entry.minDistanceOverride >= 0 ? entry.minDistanceOverride : settings.globalMinDistance;
                if (!validator.Separated(ground.point, distance)) continue;
                var rotation = (settings.alignToGroundNormal ? Quaternion.FromToRotation(Vector3.up, ground.normal) : Quaternion.identity)
                    * Quaternion.Euler(0, yaw, 0) * candidate.prefab.transform.localRotation;
                var objectScale = candidate.prefab.transform.localScale * scale;
                var relative = ScatterGeometry.TransformBounds(candidate.bounds, Matrix4x4.TRS(Vector3.zero, rotation, objectScale));
                // Support the transformed box against the local ground plane, including off-centre pivots.
                var localNormal = Matrix4x4.TRS(Vector3.zero, rotation, objectScale).transpose.MultiplyVector(ground.normal);
                float support = Vector3.Dot(ground.normal, relative.center) - Vector3.Dot(
                    new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z)), candidate.bounds.extents);
                var position = ground.point - Vector3.up * (support / Mathf.Max(.01f, ground.normal.y));
                var collisionScale = Vector3.Scale(objectScale, new Vector3(settings.collisionRadiusMultiplier, 1, settings.collisionRadiusMultiplier));
                var collisionLocal = new Bounds(candidate.bounds.center, Vector3.Scale(candidate.bounds.size,
                    new Vector3(settings.collisionRadiusMultiplier, 1, settings.collisionRadiusMultiplier)));
                var occupied = ScatterGeometry.TransformBounds(collisionLocal, Matrix4x4.TRS(position, rotation, objectScale));
                bool blocked = false;
                if (settings.avoidCollisions)
                {
                    blocked = validator.Overlaps(occupied);
                    var extents = Vector3.Scale(candidate.bounds.extents, new Vector3(Mathf.Abs(collisionScale.x), Mathf.Abs(collisionScale.y), Mathf.Abs(collisionScale.z)));
                    int n = physics.OverlapBox(occupied.center, Vector3.Max(extents - Vector3.one * .01f, Vector3.one * .001f),
                        hits, rotation, settings.collisionLayerMask, QueryTriggerInteraction.Ignore);
                    if (n == hits.Length) blocked = true; // Saturated queries fail closed.
                    for (int i = 0; i < n && !blocked; i++)
                        if (!hits[i].transform.IsChildOf(parent.transform)) blocked = true;
                }
                if (blocked) continue;
                // Separate groups avoid absorbing unrelated designer edits while this job yields.
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Scatter placement");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(candidate.prefab, parent.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Scatter placement");
                Undo.SetTransformParent(instance.transform, parent.transform, "Scatter placement");
                Undo.RecordObject(instance.transform, "Scatter placement");
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = objectScale;
                // Queries must see the same transforms regardless of editor update timing.
                Physics.SyncTransforms();
                validator.Add(ground.point, distance, occupied);
                count++;
                report($"Preview: {count}/{settings.targetObjectCount}; attempts: {attempt + 1}/{settings.maxPlacementAttempts}");
            }
            report($"Preview ready: {count}/{settings.targetObjectCount}." + (count < settings.targetObjectCount ? " Placement attempts exhausted; relax constraints or enlarge area." : ""));
        }

        public static void Clear(ScatterSettings settings)
        {
            var parent = settings.preview.Resolve() as GameObject;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Scatter Preview");
            Undo.RecordObject(settings, "Clear Scatter Preview");
            settings.preview.Set(null);
            Undo.FlushUndoRecordObjects();
            if (parent) Undo.DestroyObjectImmediate(parent);
        }
        public static void Bake(ScatterSettings settings)
        {
            var parent = settings.preview.Resolve() as GameObject;
            if (!parent) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Bake Scatter");
            Undo.RecordObjects(new UnityEngine.Object[] { parent, settings }, "Bake Scatter");
            parent.name = "EnvironmentScatter_BAKED";
            settings.preview.Set(null);
            EditorSceneManager.MarkSceneDirty(parent.scene);
        }
    }
}
