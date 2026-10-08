using System;
using System.IO;
using ExtractionRaid.Editor.SplatMap;
using ExtractionRaid.View.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionRaid.Editor.Roads
{
    public static class RoadAssets
    {
        public static bool Build(SplatRoad road, out string report)
        {
            report = "Build cancelled.";
            if (!road || EditorUtility.IsPersistent(road) || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetPrefabStage(road.gameObject) != null || string.IsNullOrEmpty(road.gameObject.scene.path))
                throw new InvalidOperationException("Build a road in a saved regular scene, outside Play/Prefab Mode.");
            if (IntersectionAssets.Owner(road))
                throw new InvalidOperationException("This road belongs to an intersection. Select the intersection and use Build / Rebuild Intersection.");
            var renderer = road.GetComponent<MeshRenderer>();
            if (SplatMapPainterWindow.HasOpenMapFor(renderer))
                throw new InvalidOperationException("Save your maps and close Splat Map Painter before rebuilding this road.");
            if (road.transform.localToWorldMatrix.determinant <= 0.000001f)
                throw new InvalidOperationException("The road transform must have a positive, non-zero scale.");
            if (road.rayHeight <= 0 || road.rayDistance <= 0) throw new ArgumentException("Ray Height and Ray Distance must be positive.");
            if (road.generatedMesh && !road.generatedMaterial)
                throw new InvalidOperationException("The generated road material is missing. Restore it before rebuilding.");
            if (road.generatedMesh)
                foreach (SplatRoad other in Resources.FindObjectsOfTypeAll<SplatRoad>())
                    if (other != road && other.gameObject.scene.IsValid() && other.generatedMesh == road.generatedMesh)
                        throw new InvalidOperationException("This road shares its generated mesh with another instance. Use Create New Road for an independently editable road.");
            Shader shader = Shader.Find("ExtractShaders/SplatRGBA");
            Material chosenMaterial = road.generatedMesh ? road.generatedMaterial : road.materialTemplate;
            if (road.useSplatMaps && (!shader || (chosenMaterial && chosenMaterial.shader != shader)))
                throw new InvalidOperationException("Use an ExtractShaders/SplatRGBA material template.");
            if (!road.useSplatMaps && (!chosenMaterial || chosenMaterial.shader == shader))
                throw new InvalidOperationException("Assign a regular material template, for example ExtractionRaid/Road Marking Unlit. Enable Use Splat Maps for SplatRGBA.");
            if (road.useSplatMaps && road.mapResolution != 256 && road.mapResolution != 512 && road.mapResolution != 1024 && road.mapResolution != 2048)
                throw new ArgumentException("Map Resolution must be 256, 512, 1024 or 2048.");
            RoadMeshData data = Generate(road);
            bool firstBuild = !road.generatedMesh;
            if (!firstBuild && !EditorUtility.DisplayDialog("Rebuild Road", road.useSplatMaps ? "Rebuild the geometry? Existing Splat and Mask PNGs will be kept, but paint positions can shift when the shape or length changes." : "Rebuild the mesh? The generated material will be kept.", "Rebuild", "Cancel")) return false;
            string folder = road.assetFolder;
            if (firstBuild)
            {
                string chosen = EditorUtility.SaveFilePanelInProject("Choose Road Asset Location", road.name + "_Road", "asset", "A unique folder containing this road's mesh, material and paint maps will be created here.");
                if (string.IsNullOrEmpty(chosen)) return false;
                string parent = Path.GetDirectoryName(chosen).Replace('\\', '/');
                string proposed = parent + "/" + Path.GetFileNameWithoutExtension(chosen) + "_Data";
                folder = AssetDatabase.GenerateUniqueAssetPath(proposed);
                string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
                if (string.IsNullOrEmpty(guid)) throw new IOException("Could not create the road asset folder.");
                folder = AssetDatabase.GUIDToAssetPath(guid);
            }
            Mesh mesh = firstBuild ? new Mesh { name = road.name + " Road Mesh" } : road.generatedMesh;
            Material material = firstBuild ? (road.materialTemplate ? new Material(road.materialTemplate) : new Material(shader)) : road.generatedMaterial;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Splat Road");
            try
            {
                if (!firstBuild) Undo.RegisterCompleteObjectUndo(mesh, "Rebuild road mesh");
                WriteMesh(mesh, data, road.transform, road.useSplatMaps);
                if (firstBuild)
                {
                    AssetDatabase.CreateAsset(mesh, folder + "/RoadMesh.asset");
                    if (road.useSplatMaps)
                    {
                        var splatPixels = new Color[road.mapResolution * road.mapResolution];
                        for (int i = 0; i < splatPixels.Length; i++) splatPixels[i] = new Color(1, 0, 0, 0);
                        Texture2D splat = SaveMap(folder + "/Splat.png", road.mapResolution, splatPixels);
                        Texture2D mask = SaveMap(folder + "/Mask.png", road.mapResolution,
                        RoadGeometry.InitialMask(road.mapResolution, road.width, data.length, road.edgeFade, road.fadeEnds));
                        material.name = road.name + " Road Material";
                        material.SetTexture("_SplatMap", splat);
                        material.SetTexture("_MaskMap", mask);
                        material.SetTextureScale("_SplatMap", Vector2.one);
                        material.SetTextureOffset("_SplatMap", Vector2.zero);
                        material.SetTextureScale("_MaskMap", Vector2.one);
                        material.SetTextureOffset("_MaskMap", Vector2.zero);
                        material.SetFloat("_UseOpacityMask", 1);
                    }
                    material.name = road.name + " Road Material";
                    AssetDatabase.CreateAsset(material, folder + "/RoadMaterial.mat");
                }
                Undo.RecordObject(material, "Configure road material");
                if (firstBuild && material.HasProperty("_UseRoadMaskUV") && !material.GetTexture("_MaskMap"))
                    material.SetFloat("_UseRoadMaskUV", 1);
                if (material.HasProperty("_DepthOffset")) material.SetFloat("_DepthOffset", road.depthOffset);
                if (road.useSplatMaps)
                {
                    material.SetFloat("_UseRoadDetailUV", 1);

                    SplatShaderGUI.Configure(material);
                }
                var filter = road.GetComponent<MeshFilter>();
                Undo.RecordObjects(new UnityEngine.Object[] { road, filter, renderer }, "Assign road assets");
                road.generatedMesh = mesh;
                road.generatedMaterial = material;
                road.assetFolder = folder;
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                foreach (UnityEngine.Object changed in new UnityEngine.Object[] { road, filter, renderer })
                    if (PrefabUtility.IsPartOfPrefabInstance(changed)) PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                EditorUtility.SetDirty(mesh);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(road);
                AssetDatabase.SaveAssetIfDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(material);
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                report = $"Road built: {data.length:F1} m, {data.vertices.Length} vertices. Assets: {folder}";
                return true;
            }
            catch
            {
                if (firstBuild)
                {
                    // This folder was uniquely created by this operation; it contains no pre-existing assets.
                    AssetDatabase.DeleteAsset(folder);
                    if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                    if (material && !AssetDatabase.Contains(material)) UnityEngine.Object.DestroyImmediate(material);
                }
                throw;
            }
        }

        public static RoadMeshData Generate(SplatRoad road)
        {
            Physics.SyncTransforms();
            var points = new Vector3[road.points.Count];
            for (int i = 0; i < points.Length; i++) points[i] = road.transform.TransformPoint(road.points[i]);
            var ground = new RoadGroundQuery(road);
            return RoadGeometry.Build(points, road.width, road.sampleSpacing, road.smooth,
                road.widthSegments, road.textureTileSize, road.surfaceLift,
                road.snapToGround ? ground.Project : null, road.lateralOffset);
        }

        public static void WriteMesh(Mesh mesh, RoadMeshData data, Transform transform, bool splat)
        {
            var vertices = new Vector3[data.vertices.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = transform.InverseTransformPoint(data.vertices[i]);
            mesh.Clear();
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = splat ? data.paintUV : data.markingUV;
            mesh.uv2 = data.paintUV;
            mesh.SetUVs(3, data.detailUV);
            mesh.triangles = data.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public static Texture2D SaveMap(string path, int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = size;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
