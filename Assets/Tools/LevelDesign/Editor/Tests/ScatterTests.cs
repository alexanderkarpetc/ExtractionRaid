using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesign.Editor.Tests
{
    public sealed class ScatterTests
    {
        [Test]
        public void WeightedSelectionIsRepeatableAndSkipsZeroWeights()
        {
            var a = new System.Random(123); var b = new System.Random(123);
            float[] weights = { 0, 1, 3 };
            int last = 0;
            for (int i = 0; i < 1000; i++)
            {
                int selected = ScatterGeometry.WeightedIndex(weights, a);
                Assert.That(selected, Is.EqualTo(ScatterGeometry.WeightedIndex(weights, b)));
                Assert.That(selected, Is.Not.Zero);
                if (selected == 2) last++;
            }
            Assert.That(last, Is.InRange(680, 820));
        }
        [Test]
        public void DistanceUsesLargerOverrideAcrossNegativeCellBoundary()
        {
            var validator = new ScatterPlacementValidator(5);
            validator.Add(new Vector3(-.1f, 0, 0), 5, new Bounds(Vector3.zero, Vector3.one));
            Assert.That(validator.Separated(new Vector3(4, 0, 0), .5f), Is.False);
            Assert.That(validator.Separated(new Vector3(5, 0, 0), .5f), Is.True);
            Assert.That(validator.Overlaps(new Bounds(Vector3.zero, Vector3.one)), Is.True);
        }
        [Test]
        public void BoundsHonorOffsetPivotRotationAndScale()
        {
            var result = ScatterGeometry.TransformBounds(new Bounds(new Vector3(0, 1, 0), new Vector3(2, 2, 4)),
                Matrix4x4.TRS(new Vector3(10, 0, 0), Quaternion.Euler(0, 90, 0), Vector3.one * 2));
            Assert.That(Vector3.Distance(result.center, new Vector3(10, 2, 0)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(result.size, new Vector3(8, 4, 4)), Is.LessThan(.001f));
            Assert.That(result.min.y, Is.EqualTo(0).Within(.001f));
        }
        [Test]
        public void BakeAndClearCanBeUndone()
        {
            using var testScene = new TestSceneScope();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, testScene.ScenePath);
            var settings = ScriptableObject.CreateInstance<ScatterSettings>();
            var parent = new GameObject("EnvironmentScatter_PREVIEW");
            SceneManager.MoveGameObjectToScene(parent, scene);
            try
            {
                settings.preview.Set(parent);
                ScatterGenerator.Bake(settings);
                Undo.FlushUndoRecordObjects();
                Assert.That(parent.name, Is.EqualTo("EnvironmentScatter_BAKED"));
                Undo.PerformUndo();
                Assert.That(parent.name, Is.EqualTo("EnvironmentScatter_PREVIEW"));
                Assert.That(settings.preview.Resolve(), Is.EqualTo(parent));
                ScatterGenerator.Clear(settings);
                Undo.FlushUndoRecordObjects();
                Assert.That(parent == null, Is.True);
                Undo.PerformUndo();
                parent = settings.preview.Resolve() as GameObject;
                Assert.That(parent, Is.Not.Null);
            }
            finally
            {
                if (parent) UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(settings);
                Undo.ClearAll();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [TestCase(12)]
        [TestCase(500)]
        [TestCase(2000)]
        public void GenerationGroundsObjectsAndRepeatsSeedWithoutChangingPrefab(int objectCount)
        {
            using var testScene = new TestSceneScope();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            EditorSceneManager.SaveScene(scene, testScene.ScenePath);
            var settings = ScriptableObject.CreateInstance<ScatterSettings>();
            GameObject prefab = null;
            try
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0, -.5f, 0);
                ground.transform.localScale = new Vector3(1000, 1, 1000);
                var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
                source.layer = 2;
                prefab = PrefabUtility.SaveAsPrefabAsset(source, testScene.PrefabPath);
                UnityEngine.Object.DestroyImmediate(source);
                var entry = new ScatterPrefabEntry(); entry.prefab.Set(prefab);
                settings.prefabs.Add(entry); settings.targetObjectCount = objectCount;
                settings.area.size = Vector2.one * Mathf.Max(30, Mathf.Sqrt(objectCount) * 4);
                settings.globalScaleMin = .5f; settings.globalScaleMax = 1.2f;
                Assert.That(ScatterGenerator.Validate(settings), Is.Null);
                var routine = new ScatterGenerator().Generate(settings, _ => { });
                while (routine.MoveNext()) { }
                var parent = (GameObject)settings.preview.Resolve();
                Assert.That(parent.transform.childCount, Is.EqualTo(objectCount));
                var positions = new Vector3[objectCount];
                var rotations = new Quaternion[objectCount];
                var scales = new Vector3[objectCount];
                for (int i = 0; i < objectCount; i++)
                {
                    positions[i] = parent.transform.GetChild(i).position;
                    rotations[i] = parent.transform.GetChild(i).rotation;
                    scales[i] = parent.transform.GetChild(i).localScale;
                    Assert.That(positions[i].y, Is.EqualTo(scales[i].y * .5f).Within(.001f));
                }
                routine = new ScatterGenerator().Generate(settings, _ => { });
                while (routine.MoveNext()) { }
                parent = (GameObject)settings.preview.Resolve();
                for (int i = 0; i < objectCount; i++)
                {
                    Assert.That(parent.transform.GetChild(i).position, Is.EqualTo(positions[i]));
                    Assert.That(parent.transform.GetChild(i).rotation, Is.EqualTo(rotations[i]));
                    Assert.That(parent.transform.GetChild(i).localScale, Is.EqualTo(scales[i]));
                }
                EditorSceneManager.SaveScene(scene);
                var restored = ScriptableObject.CreateInstance<ScatterSettings>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings), restored);
                    Assert.That(restored.preview.Resolve(), Is.EqualTo(parent));
                    Assert.That(restored.prefabs[0].prefab.Resolve(), Is.EqualTo(prefab));
                }
                finally { UnityEngine.Object.DestroyImmediate(restored); }
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
                ScatterGenerator.Clear(settings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings); Undo.ClearAll();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        internal sealed class TestSceneScope : IDisposable
        {
            readonly SceneSetup[] previous;
            readonly string folder;
            public string ScenePath => folder + "/Scene.unity";
            public string PrefabPath => folder + "/Prop.prefab";
            public TestSceneScope()
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) Assert.Ignore("Save scene edits before running scene integration tests.");
                previous = Array.FindAll(EditorSceneManager.GetSceneManagerSetup(), scene => !string.IsNullOrEmpty(scene.path));
                string name = "LevelDesignTest_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", name);
                folder = "Assets/" + name;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            public void Dispose()
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ExclusionsIncludeTriggersAndIgnorePointsOutside()
        {
            using var testScene = new TestSceneScope();
            var zone = new GameObject("Exclusion test");
            try
            {
                var collider = zone.AddComponent<BoxCollider>(); collider.isTrigger = true;
                collider.size = new Vector3(4, 4, 4); zone.transform.rotation = Quaternion.Euler(0, 30, 0);
                Physics.SyncTransforms();
                Assert.That(ScatterAvoidance.Excluded(Vector3.zero, new[] { collider }, Physics.defaultPhysicsScene, 0, new Collider[8]), Is.True);
                Assert.That(ScatterAvoidance.Excluded(new Vector3(10, 0, 0), new[] { collider }, Physics.defaultPhysicsScene, 0, new Collider[8]), Is.False);
                Assert.That(ScatterAvoidance.Excluded(Vector3.zero, new Collider[0], Physics.defaultPhysicsScene, 1, new Collider[8]), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(zone); }
        }
    }
}


