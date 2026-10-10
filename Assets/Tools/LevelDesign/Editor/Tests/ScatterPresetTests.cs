using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor.Tests
{
    public sealed class ScatterPresetTests
    {
        ScatterTests.TestSceneScope scene;
        ScatterSettings source, target;
        ScatterPreset preset;
        GameObject rock, paper;
        string folder;

        [SetUp]
        public void SetUp()
        {
            scene = new ScatterTests.TestSceneScope();
            folder = System.IO.Path.GetDirectoryName(scene.PrefabPath).Replace('\\', '/');
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock = PrefabUtility.SaveAsPrefabAsset(cube, folder + "/Rock.prefab");
            paper = PrefabUtility.SaveAsPrefabAsset(cube, folder + "/Paper.prefab");
            Object.DestroyImmediate(cube);
            source = ScriptableObject.CreateInstance<ScatterSettings>();
            target = ScriptableObject.CreateInstance<ScatterSettings>();
            preset = ScriptableObject.CreateInstance<ScatterPreset>();
            source.prefabs.Add(Entry(rock, 7));
            source.targetObjectCount = 27;
            source.globalMinDistance = 2.4f;
            source.globalScaleMin = .4f;
            source.rotationMax = 70;
            source.groundLayerMask = 8;
            source.avoidNavMesh = true;
        }
        static ScatterPrefabEntry Entry(GameObject prefab, float weight)
        {
            var entry = new ScatterPrefabEntry { weight = weight, scaleMin = .6f, minDistanceOverride = 3 };
            entry.prefab.Set(prefab);
            return entry;
        }
        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Object.DestroyImmediate(source); Object.DestroyImmediate(target);
            if (preset && !AssetDatabase.Contains(preset)) Object.DestroyImmediate(preset);
            scene.Dispose();
        }
        [Test]
        public void ApplyRestoresIndependentRulesAndPreservesSceneContextWithUndo()
        {
            var parent = new GameObject("Existing preview");
            var zone = new GameObject("Exclusion").AddComponent<BoxCollider>();
            target.area.center = new Vector3(30, 8, -40); target.area.size = new Vector2(70, 60);
            target.area.rotationY = 35; target.showArea = false; target.editAreaHandles = true;
            target.preview.Set(parent);
            var exclusion = new SceneReference(); exclusion.Set(zone); target.exclusionColliders.Add(exclusion);
            target.prefabs.Add(Entry(paper, 2));
            source.preview.Set(parent); source.exclusionColliders.Add(exclusion);
            preset.Capture(source);
            source.prefabs[0].weight = 99;
            preset.Apply(target);
            Undo.FlushUndoRecordObjects();
            Assert.That(target.prefabs[0].prefab.Resolve(), Is.EqualTo(rock));
            Assert.That(target.prefabs[0].weight, Is.EqualTo(7));
            Assert.That(target.prefabs[0].scaleMin, Is.EqualTo(.6f));
            Assert.That(target.targetObjectCount, Is.EqualTo(27));
            Assert.That(target.globalMinDistance, Is.EqualTo(2.4f));
            Assert.That(target.groundLayerMask.value, Is.EqualTo(8));
            Assert.That(target.avoidNavMesh, Is.True);
            Assert.That(target.area.center, Is.EqualTo(new Vector3(30, 8, -40)));
            Assert.That(target.area.size, Is.EqualTo(new Vector2(70, 60)));
            Assert.That(target.area.rotationY, Is.EqualTo(35));
            Assert.That(target.showArea, Is.False); Assert.That(target.editAreaHandles, Is.True);
            Assert.That(target.preview.Resolve(), Is.EqualTo(parent));
            Assert.That(target.exclusionColliders.Count, Is.EqualTo(1));
            Assert.That(target.exclusionColliders[0].Resolve(), Is.EqualTo(zone));
            Undo.PerformUndo();
            Assert.That(target.prefabs[0].prefab.Resolve(), Is.EqualTo(paper));
            Assert.That(target.targetObjectCount, Is.EqualTo(500));
            Assert.That(target.preview.Resolve(), Is.EqualTo(parent));
        }
        [Test]
        public void AddPrefabsSkipsDuplicatesRetainsRulesAndCanBeUndone()
        {
            source.prefabs.Add(Entry(paper, 4)); preset.Capture(source);
            target.prefabs.Add(Entry(rock, 90)); target.targetObjectCount = 15;
            Assert.That(preset.AddPrefabs(target), Is.EqualTo(1));
            Undo.FlushUndoRecordObjects();
            Assert.That(target.prefabs.Count, Is.EqualTo(2));
            Assert.That(target.prefabs[0].weight, Is.EqualTo(90));
            Assert.That(target.prefabs[1].weight, Is.EqualTo(4));
            Assert.That(target.targetObjectCount, Is.EqualTo(15));
            Assert.That(preset.AddPrefabs(target), Is.EqualTo(0));
            Undo.PerformUndo();
            Assert.That(target.prefabs.Count, Is.EqualTo(1));
        }
        [Test]
        public void SavedAssetRestoresReferencesAndContainsNoSceneOwnership()
        {
            var preview = new GameObject("Source preview"); source.preview.Set(preview);
            var exclusion = new SceneReference(); exclusion.Set(preview); source.exclusionColliders.Add(exclusion);
            preset.Capture(source);
            string path = folder + "/Rocks.asset";
            AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssetIfDirty(preset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            preset = AssetDatabase.LoadAssetAtPath<ScatterPreset>(path);
            target.selectedPreset.Set(preset);
            preset.Apply(target);
            Assert.That(target.prefabs[0].prefab.Resolve(), Is.EqualTo(rock));
            Assert.That(target.preview.Resolve(), Is.Null);
            Assert.That(target.exclusionColliders, Is.Empty);
            Assert.That(target.selectedPreset.Resolve(), Is.EqualTo(preset));
            target.prefabs[0].weight = 123;
            preset.Apply(target);
            Assert.That(target.prefabs[0].weight, Is.EqualTo(7));
        }
        [Test]
        public void UpdatingPresetCanBeUndoneAndScenePrefabsAreRejected()
        {
            preset.Capture(source); Undo.FlushUndoRecordObjects();
            source.targetObjectCount = 88; preset.Capture(source); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); preset.Apply(target);
            Assert.That(target.targetObjectCount, Is.EqualTo(27));
            source.prefabs[0].prefab.Set(new GameObject("Scene object"));
            Assert.Throws<System.InvalidOperationException>(() => preset.Capture(source));
        }
    }
}
