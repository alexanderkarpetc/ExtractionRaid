using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    public sealed class EnvironmentPrefabBuilderWindow : EditorWindow
    {
        enum ResourceKind { Environment, Characters }
        [SerializeField] ResourceKind _kind;
        [SerializeField] DefaultAsset _sourceFolder;
        string ModelRoot => _kind == ResourceKind.Environment
            ? "Assets/Resources/Environment/3D" : "Assets/Resources/Character";
        string PrefabRoot => _kind == ResourceKind.Environment
            ? "Assets/Resources/Environment/Prefabs" : "Assets/Resources/Character/Prefabs";
        string ScanRoot => _sourceFolder != null ? AssetDatabase.GetAssetPath(_sourceFolder) : ModelRoot;
        const string PresetPath = "Assets/Resources/FBXImporter.preset";
        const string CharacterMaterialTemplateGuid = "74cb09742c00eda41a79cd87d4452ccd";

        [Serializable]
        sealed class ModelEntry
        {
            public GameObject model;
            public List<Texture2D> textures = new();
            public Texture2D textureOverride;
            public Material materialOverride;
            public Vector2 tiling = Vector2.one;
            public Vector2 offset = Vector2.zero;
            public bool selected;
            public bool expanded;
        }

        readonly List<ModelEntry> _entries = new();
        Vector2 _scroll;
        string _status = "Scan to find FBX models without generated prefabs.";
        MessageType _statusType = MessageType.Info;
        bool _includeExisting;

        [MenuItem("Environment/Prefab Builder")]
        static void Open() => GetWindow<EnvironmentPrefabBuilderWindow>("Prefab Builder");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Resource Prefab Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag a folder from Project into Source folder, then Scan. An empty field scans the default folder. Existing prefabs are skipped.",
                MessageType.Info);
            var kind = (ResourceKind)EditorGUILayout.EnumPopup("Resource type", _kind);
            if (kind != _kind)
            {
                _kind = kind;
                _sourceFolder = null;
                _entries.Clear();
                _status = "Choose a folder or scan the default folder.";
                _statusType = MessageType.Info;
            }
            var folder = (DefaultAsset)EditorGUILayout.ObjectField("Source folder", _sourceFolder, typeof(DefaultAsset), false);
            if (folder != _sourceFolder)
            {
                string path = AssetDatabase.GetAssetPath(folder);
                if (folder == null || (path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(path)))
                {
                    _sourceFolder = folder;
                    _entries.Clear();
                    _status = "Folder selected. Click Scan.";
                    _statusType = MessageType.Info;
                }
                else
                {
                    _status = "Choose a folder inside Assets.";
                    _statusType = MessageType.Error;
                }
            }
            EditorGUILayout.LabelField("Scan path", ScanRoot);
            EditorGUILayout.LabelField("Prefab destination", PrefabRoot);
            if (_kind == ResourceKind.Characters)
                EditorGUILayout.HelpBox("Creates a model prefab with its rig and Animator. Character importer settings are preserved; gameplay components are not added. Choose an existing material if needed.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan", GUILayout.Height(26))) Scan();
            using (new EditorGUI.DisabledScope(!_entries.Any(e => e.selected)))
            {
                if (GUILayout.Button("Build Selected", GUILayout.Height(26))) BuildSelected();
            }
            EditorGUILayout.EndHorizontal();
            _includeExisting = EditorGUILayout.ToggleLeft("Show models that already have prefabs", _includeExisting);
            EditorGUILayout.HelpBox(_status, _statusType);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (ModelEntry entry in _entries)
            {
                if (entry.model == null) continue;
                string path = AssetDatabase.GetAssetPath(entry.model);
                string relative = path.StartsWith(ModelRoot + "/", StringComparison.Ordinal)
                    ? path.Substring(ModelRoot.Length + 1)
                    : path;
                bool hasPrefab = HasAllPrefabs(entry, path);
                if (!_includeExisting && hasPrefab) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
                entry.expanded = EditorGUILayout.Foldout(entry.expanded, relative, true);
                if (hasPrefab) GUILayout.Label("Exists", EditorStyles.miniLabel, GUILayout.Width(44));
                EditorGUILayout.EndHorizontal();
                if (entry.expanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.ObjectField("Model", entry.model, typeof(GameObject), false);
                    entry.materialOverride = (Material)EditorGUILayout.ObjectField(
                        "Use existing material", entry.materialOverride, typeof(Material), false);
                    entry.tiling = EditorGUILayout.Vector2Field("Texture tiling", entry.tiling);
                    entry.offset = EditorGUILayout.Vector2Field("Texture offset", entry.offset);
                    if (entry.textures.Count == 0)
                    {
                        entry.textureOverride = (Texture2D)EditorGUILayout.ObjectField(
                            "Texture (choose manually)", entry.textureOverride, typeof(Texture2D), false);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Detected textures", entry.textures.Count.ToString());
                        foreach (Texture2D texture in entry.textures)
                            EditorGUILayout.ObjectField(texture, typeof(Texture2D), false);
                        entry.textureOverride = (Texture2D)EditorGUILayout.ObjectField(
                            "Use one texture instead", entry.textureOverride, typeof(Texture2D), false);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        void Scan()
        {
            _entries.Clear();
            if (!AssetDatabase.IsValidFolder(ScanRoot))
            {
                _status = "Source folder does not exist: " + ScanRoot;
                _statusType = MessageType.Error;
                return;
            }
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ScanRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                var entry = new ModelEntry { model = model };
                entry.textures = FindTextures(path);
                entry.selected = !HasAllPrefabs(entry, path);
                _entries.Add(entry);
            }
            _entries.Sort((a, b) => string.Compare(AssetDatabase.GetAssetPath(a.model), AssetDatabase.GetAssetPath(b.model), StringComparison.OrdinalIgnoreCase));
            _status = $"Found {_entries.Count} FBX models. Select the rows to build.";
            _statusType = MessageType.Info;
        }

        static List<Texture2D> FindTextures(string modelPath)
        {
            string folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            string modelKey = NormalizeTextureStem(Path.GetFileNameWithoutExtension(modelPath));
            var matches = new List<Texture2D>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder, StringComparison.OrdinalIgnoreCase)) continue;
                string fileStem = Path.GetFileNameWithoutExtension(path);
                string stem = NormalizeTextureStem(fileStem);
                if ((stem == modelKey || stem.StartsWith(modelKey + "_", StringComparison.OrdinalIgnoreCase)) && IsSkinTexture(fileStem, modelKey))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null) matches.Add(texture);
                }
            }

            // Support the project's common T_<name>_Basic naming for multi-skin texture sets.
            if (matches.Count == 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder, StringComparison.OrdinalIgnoreCase)) continue;
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (!stem.StartsWith("T_", StringComparison.OrdinalIgnoreCase) ||
                        stem.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null) matches.Add(texture);
                }
            }
            return matches.Distinct().OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static string NormalizeTextureStem(string value)
        {
            if (value.StartsWith("SM_", StringComparison.OrdinalIgnoreCase)) value = value.Substring(3);
            if (value.StartsWith("SK_", StringComparison.OrdinalIgnoreCase)) value = value.Substring(3);
            if (value.StartsWith("T_", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);
            string[] suffixes = { "_BaseColor", "_Base_Color", "_Albedo", "_Color", "_Basic" };
            foreach (string suffix in suffixes)
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(0, value.Length - suffix.Length);
            return value;
        }

        static bool IsSkinTexture(string fileStem, string modelKey)
        {
            string stem = fileStem;
            if (stem.StartsWith("T_", StringComparison.OrdinalIgnoreCase)) stem = stem.Substring(2);
            if (!stem.StartsWith(modelKey, StringComparison.OrdinalIgnoreCase)) return false;
            string suffix = stem.Substring(modelKey.Length).TrimStart('_');
            if (string.IsNullOrEmpty(suffix)) return true;
            string[] parts = suffix.Split('_');
            return parts.All(part => part.Equals("Basic", StringComparison.OrdinalIgnoreCase) ||
                                     part.Equals("BaseColor", StringComparison.OrdinalIgnoreCase) ||
                                     part.Equals("Albedo", StringComparison.OrdinalIgnoreCase) ||
                                     part.Equals("Color", StringComparison.OrdinalIgnoreCase) ||
                                     int.TryParse(part, out _));
        }

        bool HasAllPrefabs(ModelEntry entry, string modelPath)
        {
            string outputFolder = GetPrefabFolder(modelPath);
            string stem = GetOutputStem(entry.model.name);
            int variants = Mathf.Max(1, entry.textures.Count);
            for (int i = 0; i < variants; i++)
            {
                string suffix = i == 0 ? "" : "_" + (i + 1).ToString("00");
                if (!File.Exists(outputFolder + "/ER_" + stem + suffix + ".prefab")) return false;
            }
            return true;
        }

        void BuildSelected()
        {
            int built = 0;
            int skipped = 0;
            var errors = new List<string>();
            foreach (ModelEntry entry in _entries.Where(e => e.selected && e.model != null))
            {
                try
                {
                    BuildEntry(entry, ref built, ref skipped);
                }
                catch (Exception exception)
                {
                    errors.Add(entry.model.name + ": " + exception.Message);
                    Debug.LogException(exception, entry.model);
                }
            }

            string buildSummary = errors.Count == 0
                ? $"Created {built} prefab(s); skipped {skipped} existing output(s)."
                : $"Created {built} prefab(s); skipped {skipped}; errors: {string.Join(" | ", errors)}";
            Scan();
            _status = buildSummary;
            _statusType = errors.Count == 0 ? MessageType.Info : MessageType.Error;
        }

        void BuildEntry(ModelEntry entry, ref int built, ref int skipped)
        {
            string modelPath = AssetDatabase.GetAssetPath(entry.model);
            if (_kind == ResourceKind.Environment) ApplyImporterPresetPreservingScale(modelPath);
            entry.model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            List<Texture2D> textures = entry.textureOverride != null
                ? new List<Texture2D> { entry.textureOverride }
                : entry.textures;
            if (textures.Count == 0) textures.Add(null);

            string materialFolder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            string prefabFolder = GetPrefabFolder(modelPath);
            EnsureAssetFolder(prefabFolder);
            string stem = GetOutputStem(entry.model.name);

            for (int i = 0; i < textures.Count; i++)
            {
                string suffix = i == 0 ? "" : "_" + (i + 1).ToString("00");
                string prefabPath = prefabFolder + "/ER_" + stem + suffix + ".prefab";
                if (File.Exists(prefabPath)) { skipped++; continue; }

                string materialPath = materialFolder + "/M_" + stem + suffix + ".mat";
                Material material = entry.materialOverride;
                if (material == null) material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    if (_kind == ResourceKind.Characters)
                    {
                        Material template = LoadCharacterMaterialTemplate();
                        if (textures[i] == null)
                            throw new InvalidOperationException("Choose a texture or an existing material for this character.");
                        material = new Material(template) { name = "M_" + stem + suffix };
                        SetCharacterTexture(material, textures[i], entry.tiling, entry.offset);
                    }
                    else
                    {
                        Shader shader = Shader.Find("EnvironmentPropDither");
                        if (shader == null) shader = Shader.Find("ExtractShaders/EnvironmentPropDither");
                        if (shader == null) throw new InvalidOperationException("EnvironmentPropDither shader was not found.");
                        material = new Material(shader) { name = "M_" + stem + suffix };
                        SetMaterialDefaults(material, textures[i], entry.tiling, entry.offset);
                    }
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else if (entry.materialOverride == null && textures[i] != null)
                {
                    if (_kind == ResourceKind.Characters)
                    {
                        // The template itself can also be the source model's assigned material.
                        if (material != LoadCharacterMaterialTemplate())
                        {
                            SetCharacterTexture(material, textures[i], entry.tiling, entry.offset);
                            EditorUtility.SetDirty(material);
                            AssetDatabase.SaveAssetIfDirty(material);
                        }
                    }
                    else
                    {
                        if (material.shader == null || material.shader.name.IndexOf("EnvironmentPropDither", StringComparison.OrdinalIgnoreCase) < 0)
                            throw new InvalidOperationException("Existing material has a different shader: " + materialPath);
                        SetMaterialDefaults(material, textures[i], entry.tiling, entry.offset);
                        EditorUtility.SetDirty(material);
                        AssetDatabase.SaveAssetIfDirty(material);
                    }
                }

                CreateRegularPrefab(entry.model, material, prefabPath);
                built++;
            }
        }

        static Material LoadCharacterMaterialTemplate()
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(CharacterMaterialTemplateGuid));
            if (template == null || template.shader == null || !template.HasProperty("_Texture"))
                throw new InvalidOperationException("M_MedicRobot_A template is missing or does not have a valid shader with _Texture.");
            return template;
        }

        static void SetCharacterTexture(Material material, Texture2D texture, Vector2 tiling, Vector2 offset)
        {
            if (!material.HasProperty("_Texture"))
                throw new InvalidOperationException("Character material has no _Texture property: " + material.name);
            material.SetTexture("_Texture", texture);
            material.SetTextureScale("_Texture", tiling);
            material.SetTextureOffset("_Texture", offset);
        }

        static void SetMaterialDefaults(Material material, Texture2D texture, Vector2 tiling, Vector2 offset)
        {
            if (material.HasProperty("_Texture")) material.SetTexture("_Texture", texture);
            if (material.HasProperty("_Texture")) material.SetTextureScale("_Texture", tiling);
            if (material.HasProperty("_Texture")) material.SetTextureOffset("_Texture", offset);
            if (material.HasProperty("_Color2")) material.SetColor("_Color2", Color.white);
            if (material.HasProperty("_ColorStreng")) material.SetFloat("_ColorStreng", 0f);
            if (material.HasProperty("_Saturate")) material.SetFloat("_Saturate", 0.5f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Occlusion")) material.SetFloat("_Occlusion", 1f);
            if (material.HasProperty("_ColorEmission")) material.SetColor("_ColorEmission", Color.white);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", 0f);
            if (material.HasProperty("_DitherEnabled")) material.SetFloat("_DitherEnabled", 0f);
            if (material.HasProperty("_Dither")) material.SetFloat("_Dither", 0f);
            if (material.HasProperty("Dither")) material.SetFloat("Dither", 0f);
            if (material.HasProperty("_DitherPatternScale")) material.SetFloat("_DitherPatternScale", 0.003f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 2f);
            material.renderQueue = -1;
            material.enableInstancing = false;
            material.doubleSidedGI = false;
        }

        static void CreateRegularPrefab(GameObject model, Material material, string prefabPath)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                if (root == null) throw new InvalidOperationException("Could not instantiate imported FBX.");
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = Path.GetFileNameWithoutExtension(prefabPath);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null) throw new InvalidOperationException("Unity did not save the prefab.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void ApplyImporterPresetPreservingScale(string modelPath)
        {
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(PresetPath);
            if (preset == null) throw new InvalidOperationException("Importer preset not found: " + PresetPath);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Model importer not found.");
            float scale = importer.globalScale;
            if (!preset.ApplyTo(importer)) throw new InvalidOperationException("Preset could not be applied to model importer.");
            importer.globalScale = scale;
            importer.SaveAndReimport();
        }

        string GetPrefabFolder(string modelPath)
        {
            string modelFolder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            string relative = modelFolder != null && modelFolder.StartsWith(ModelRoot + "/", StringComparison.Ordinal)
                ? modelFolder.Substring(ModelRoot.Length + 1)
                : modelFolder != null && modelFolder.StartsWith(ScanRoot + "/", StringComparison.Ordinal)
                    ? modelFolder.Substring(ScanRoot.Length + 1) : string.Empty;
            string category = Path.GetDirectoryName(relative)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(category) || category == ".") category = string.Empty;
            return string.IsNullOrEmpty(category) ? PrefabRoot : PrefabRoot + "/" + category;
        }

        static string GetOutputStem(string modelName)
        {
            return modelName.StartsWith("SM_", StringComparison.OrdinalIgnoreCase) || modelName.StartsWith("SK_", StringComparison.OrdinalIgnoreCase)
                ? modelName.Substring(3) : modelName;
        }

        static void EnsureAssetFolder(string folder)
        {
            string current = "Assets";
            foreach (string part in folder.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
