using System;
using System.Collections.Generic;
using System.IO;
using ExtractionRaid.Editor.SplatMap;
using ExtractionRaid.View.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads
{
    public static class IntersectionAssets
    {
        public static RoadIntersection Owner(SplatRoad road)
        {
            foreach (var node in Resources.FindObjectsOfTypeAll<RoadIntersection>())
                if (node.gameObject.scene.IsValid() && node.builtRoads.Contains(road)) return node;
            return null;
        }

        static void ValidateScene(Component component)
        {
            if (!component || EditorUtility.IsPersistent(component) || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetPrefabStage(component.gameObject) != null || string.IsNullOrEmpty(component.gameObject.scene.path))
                throw new InvalidOperationException("Use a saved regular scene outside Play/Prefab Mode.");
            if (component.transform.localToWorldMatrix.determinant <= 0.000001f)
                throw new InvalidOperationException("Use a positive, non-zero transform scale.");
            if (SplatMapPainterWindow.HasOpenMapFor(component.GetComponent<MeshRenderer>()))
                throw new InvalidOperationException("Save maps and close Painter for all affected roads and the intersection.");
        }

        static void ValidateRoad(SplatRoad road, RoadIntersection node)
        {
            ValidateScene(road);
            if (road.gameObject.scene != node.gameObject.scene || !road.generatedMesh || !road.generatedMaterial ||
                !AssetDatabase.Contains(road.generatedMesh) || road.points.Count < 2 || !road.useSplatMaps ||
                road.GetComponent<MeshFilter>().sharedMesh != road.generatedMesh ||
                road.generatedMaterial.shader.name != "ExtractShaders/SplatRGBA")
                throw new InvalidOperationException("Connect built Splat roads in the same scene (not marking overlays).");
            var owner = Owner(road);
            if (owner && owner != node)
                throw new InvalidOperationException("An approach already belongs to another intersection. This version supports one intersection per road object; split long roads into sections.");
            foreach (var other in Resources.FindObjectsOfTypeAll<SplatRoad>())
                if (other != road && other.gameObject.scene.IsValid() && other.generatedMesh == road.generatedMesh)
                    throw new InvalidOperationException("An approach shares its mesh with a duplicate. Use independently built roads.");
        }

        public static bool Build(RoadIntersection node, bool regenerateMask = false)
        {
            ValidateScene(node);
            if (node.approaches.Count < 3 || node.approaches.Count > 4)
                throw new ArgumentException("Connect exactly 3 (T) or 4 (X) separate road ends.");
            if (node.mapResolution != 256 && node.mapResolution != 512 && node.mapResolution != 1024 && node.mapResolution != 2048)
                throw new ArgumentException("Map Resolution must be 256, 512, 1024 or 2048.");
            if (node.generatedMesh && !node.generatedMaterial) throw new InvalidOperationException("Restore the intersection material before rebuilding.");
            foreach (var other in Resources.FindObjectsOfTypeAll<RoadIntersection>())
                if (other != node && node.generatedMesh && other.generatedMesh == node.generatedMesh)
                    throw new InvalidOperationException("This intersection shares assets with a duplicate.");
            var roads = new List<SplatRoad>();
            var prepared = new Dictionary<SplatRoad, RoadMeshData>();
            var mouths = new List<Vector3[]>();
            Vector3 center = Vector3.zero;
            foreach (var end in node.approaches)
            {
                if (end == null || !end.road || roads.Contains(end.road)) throw new ArgumentException("Assign distinct roads to every approach.");
                var road = end.road;
                ValidateRoad(road, node);
                roads.Add(road);
                var full = RoadAssets.Generate(road);
                int stride = road.widthSegments + 1;
                int first = end.start ? 0 : full.vertices.Length - stride;
                center += (full.vertices[first] + full.vertices[first + stride - 1]) * 0.5f;
                prepared.Add(road, IntersectionGeometry.Trim(full, stride, end.start, node.cutback, out var mouth));
                mouths.Add(mouth);
            }
            center /= roads.Count;
            var reference = roads[0];
            foreach (var road in roads)
                if (road.snapToGround != reference.snapToGround || road.groundLayers.value != reference.groundLayers.value ||
                    Mathf.Abs(road.surfaceLift - reference.surfaceLift) > 0.0001f)
                    throw new ArgumentException("Approaches must use the same Snap To Ground, Ground Layers and Surface Lift.");
            foreach (var end in node.approaches)
            {
                var point = end.road.transform.TransformPoint(end.road.points[end.start ? 0 : end.road.points.Count - 1]);
                if (new Vector2(point.x - center.x, point.z - center.z).magnitude > node.cutback * 0.5f)
                    throw new ArgumentException("Move the selected road ends close to the same junction center before building.");
            }
            var ground = new RoadGroundQuery(reference);
            RoadMeshData data = IntersectionGeometry.Build(mouths, center, node.cornerRounding, node.cornerSegments,
                node.surfaceSegments, reference.textureTileSize, reference.snapToGround ? ground.Project : null, reference.surfaceLift);
            // Removed approaches are restored in the same operation.
            foreach (var previous in node.builtRoads)
                if (previous && !prepared.ContainsKey(previous))
                { ValidateRoad(previous, node); prepared.Add(previous, RoadAssets.Generate(previous)); }
            bool firstBuild = !node.generatedMesh;
            Color[] maskPixels = firstBuild || regenerateMask ? IntersectionGeometry.InitialMask(data, node.mapResolution, node.edgeFade) : null;
            Shader shader = Shader.Find("ExtractShaders/SplatRGBA");
            Material template = node.materialTemplate ? node.materialTemplate : reference.generatedMaterial;
            if (!shader || (firstBuild ? template.shader != shader : node.generatedMaterial.shader != shader))
                throw new ArgumentException("Use an ExtractShaders/SplatRGBA material.");
            string folder = node.assetFolder;
            if (regenerateMask && !firstBuild && (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/") || !AssetDatabase.IsValidFolder(folder)))
                throw new InvalidOperationException("Restore the intersection asset folder before creating a new mask.");
            if (firstBuild)
            {
                string chosen = EditorUtility.SaveFilePanelInProject("Intersection Asset Location", "Intersection", "asset", "Create a separate mesh, material and paint maps.");
                if (string.IsNullOrEmpty(chosen)) return false;
                folder = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(chosen).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(chosen) + "_Data");
                string guid = AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
                if (string.IsNullOrEmpty(guid)) throw new IOException("Could not create intersection folder.");
                folder = AssetDatabase.GUIDToAssetPath(guid);
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Road Intersection");
            Mesh mesh = null;
            Material material = null;
            string createdMaskPath = null;
            try
            {
                mesh = firstBuild ? new Mesh { name = "Intersection Mesh" } : node.generatedMesh;
                material = firstBuild ? new Material(template) { name = "Intersection Material" } : node.generatedMaterial;
                if (!firstBuild) Undo.RegisterCompleteObjectUndo(mesh, "Rebuild intersection");
                RoadAssets.WriteMesh(mesh, data, node.transform, true);
                if (firstBuild)
                {
                    AssetDatabase.CreateAsset(mesh, folder + "/IntersectionMesh.asset");
                    var pixels = new Color[node.mapResolution * node.mapResolution];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 0, 0, 0);
                    material.SetTexture("_SplatMap", RoadAssets.SaveMap(folder + "/Splat.png", node.mapResolution, pixels));
                    pixels = maskPixels;
                    material.SetTexture("_MaskMap", RoadAssets.SaveMap(folder + "/Mask.png", node.mapResolution, pixels));
                    foreach (string property in new[] { "_SplatMap", "_MaskMap" })
                    { material.SetTextureScale(property, Vector2.one); material.SetTextureOffset(property, Vector2.zero); }
                    material.SetFloat("_UseRoadDetailUV", 1);
                    material.SetFloat("_UseOpacityMask", 1);
                    SplatShaderGUI.Configure(material);
                    AssetDatabase.CreateAsset(material, folder + "/IntersectionMaterial.mat");
                }
                if (!firstBuild && regenerateMask)
                {
                    createdMaskPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Mask_EdgeFade.png");
                    var mask = RoadAssets.SaveMap(createdMaskPath, node.mapResolution, maskPixels);
                    Undo.RegisterCompleteObjectUndo(material, "Assign edge fade mask");
                    material.SetTexture("_MaskMap", mask);
                    material.SetTextureScale("_MaskMap", Vector2.one);
                    material.SetTextureOffset("_MaskMap", Vector2.zero);
                    material.SetFloat("_UseOpacityMask", 1);
                    SplatShaderGUI.Configure(material);
                }                foreach (var pair in prepared)
                {
                    Undo.RegisterCompleteObjectUndo(pair.Key.generatedMesh, "Trim / restore road");
                    RoadAssets.WriteMesh(pair.Key.generatedMesh, pair.Value, pair.Key.transform, pair.Key.useSplatMaps);
                }
                var filter = node.GetComponent<MeshFilter>();
                var renderer = node.GetComponent<MeshRenderer>();
                Undo.RecordObjects(new UnityEngine.Object[] { node, filter, renderer }, "Assign intersection");
                node.generatedMesh = mesh; node.generatedMaterial = material; node.assetFolder = folder;
                node.builtRoads = new List<SplatRoad>(roads);
                filter.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.enabled = true;
                foreach (var changed in new UnityEngine.Object[] { node, filter, renderer })
                { EditorUtility.SetDirty(changed); if (PrefabUtility.IsPartOfPrefabInstance(changed)) PrefabUtility.RecordPrefabInstancePropertyModifications(changed); }
                foreach (var pair in prepared) { EditorUtility.SetDirty(pair.Key.generatedMesh); AssetDatabase.SaveAssetIfDirty(pair.Key.generatedMesh); }
                EditorUtility.SetDirty(mesh); EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(mesh); AssetDatabase.SaveAssetIfDirty(material);
                EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if (createdMaskPath != null)
                {
                    AssetDatabase.DeleteAsset(createdMaskPath);
                    if (material) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
                }
                foreach (var pair in prepared) if (pair.Key.generatedMesh) { EditorUtility.SetDirty(pair.Key.generatedMesh); AssetDatabase.SaveAssetIfDirty(pair.Key.generatedMesh); }
                if (firstBuild)
                {
                    AssetDatabase.DeleteAsset(folder);
                    if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                    if (material && !AssetDatabase.Contains(material)) UnityEngine.Object.DestroyImmediate(material);
                }
                throw;
            }
        }

        public static void Detach(RoadIntersection node)
        {
            ValidateScene(node);
            var prepared = new Dictionary<SplatRoad, RoadMeshData>();
            foreach (var road in node.builtRoads)
                if (road) { ValidateRoad(road, node); prepared[road] = RoadAssets.Generate(road); }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Detach Road Intersection");
            try
            {
                foreach (var pair in prepared)
                {
                    Undo.RegisterCompleteObjectUndo(pair.Key.generatedMesh, "Restore full road");
                    RoadAssets.WriteMesh(pair.Key.generatedMesh, pair.Value, pair.Key.transform, pair.Key.useSplatMaps);
                }
                var renderer = node.GetComponent<MeshRenderer>();
                Undo.RecordObjects(new UnityEngine.Object[] { node, renderer }, "Detach intersection");
                node.builtRoads.Clear(); renderer.enabled = false;
                EditorUtility.SetDirty(node); EditorUtility.SetDirty(renderer);
                if (PrefabUtility.IsPartOfPrefabInstance(node)) PrefabUtility.RecordPrefabInstancePropertyModifications(node);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                foreach (var pair in prepared) { EditorUtility.SetDirty(pair.Key.generatedMesh); AssetDatabase.SaveAssetIfDirty(pair.Key.generatedMesh); }
                EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}