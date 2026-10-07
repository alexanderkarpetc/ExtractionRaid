using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ExtractionRaid.Editor.SplatMap
{
    public sealed class SplatMapPainterWindow : EditorWindow
    {
        const string ShaderName = "ExtractShaders/SplatRGBA";
        [SerializeField] MeshRenderer target;
        [SerializeField] Material original;
        [SerializeField] SplatPaintBuffer buffer;
        [SerializeField] string savePath;
        [SerializeField] int resolution = 1024;
        [SerializeField] int channel;
        [SerializeField] float brushRadius = 0.5f;
        [SerializeField] float strength = 0.3f;
        [SerializeField] bool soft = true;
        [SerializeField] bool paintMask;
        [SerializeField] bool hideMask = true;
        bool painting, stroke, lastValid;
        int control, undoGroup;
        Vector3 lastPoint, lastNormal;
        SplatSurfaceBrush surfaceBrush;
        Texture2D working;
        Material preview;
        Mesh rayMesh;
        MeshCollider rayCollider;
        Vector3[] sourceVertices;
        Vector2[] meshUV;
        int[] triangles;
        Matrix4x4 bakedMatrix;
        Image image;
        Label status;
        Toggle paintToggle;
        string MapProperty => paintMask ? "_MaskMap" : "_SplatMap";

        [MenuItem("Tools/Level Design/Splat Map Painter")]
        public static void Open() => GetWindow<SplatMapPainterWindow>("Splat Map Painter");

        public static void OpenFor(MeshRenderer renderer)
        {
            var window = GetWindow<SplatMapPainterWindow>("Splat Map Painter");
            window.SetTarget(renderer);
            window.CreateGUI();
        }

        public static bool HasOpenMapFor(MeshRenderer renderer)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SplatMapPainterWindow>())
                if (window.target == renderer && window.buffer) return true;
            return false;
        }

        void OnEnable()
        {
            minSize = new Vector2(420, 520);
            saveChangesMessage = "Save map changes to PNG?";
            SceneView.duringSceneGui += DuringSceneGUI;
            Undo.undoRedoPerformed += OnUndo;
            EditorSceneManager.sceneSaving += BeforeSceneSave;
            EditorSceneManager.sceneSaved += AfterSceneSave;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (buffer && target && original) RestorePreview();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Undo.undoRedoPerformed -= OnUndo;
            EditorSceneManager.sceneSaving -= BeforeSceneSave;
            EditorSceneManager.sceneSaved -= AfterSceneSave;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            ReleasePreview();
        }

        void OnDestroy()
        {
            if (buffer) { Undo.ClearUndo(buffer); DestroyImmediate(buffer); }
        }

        public override void SaveChanges()
        {
            if (SaveMap()) base.SaveChanges();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var root = new ScrollView();
            rootVisualElement.Add(root);
            root.Add(new HelpBox("Requires a mesh with UV0 in the 0–1 range and one SplatRGBA material. Overlapping UV islands are painted together.", HelpBoxMessageType.Info));
            var targetField = new ObjectField("Model") { objectType = typeof(MeshRenderer), allowSceneObjects = true, value = target };
            targetField.RegisterValueChangedCallback(e =>
            {
                SetTarget(e.newValue as MeshRenderer);
                targetField.SetValueWithoutNotify(target);
            });
            root.Add(targetField);
            root.Add(new Button(() =>
            {
                SetTarget(Selection.activeGameObject ? Selection.activeGameObject.GetComponent<MeshRenderer>() : null);
                targetField.SetValueWithoutNotify(target);
            }) { text = "Use Selected Model" });
            var mode = new PopupField<string>("Map", new System.Collections.Generic.List<string> { "Splat RGBA", "Visibility Mask" }, paintMask ? 1 : 0);
            mode.RegisterValueChangedCallback(e =>
            {
                bool next = mode.index == 1;
                if (next == paintMask) return;
                if (!ResolvePending()) { mode.SetValueWithoutNotify(paintMask ? "Visibility Mask" : "Splat RGBA"); return; }
                ReleasePreview();
                if (buffer) { Undo.ClearUndo(buffer); DestroyImmediate(buffer); }
                buffer = null;
                savePath = null;
                paintMask = next;
                painting = false;
                CreateGUI();
            });
            root.Add(mode);
            var sizes = new System.Collections.Generic.List<int> { 256, 512, 1024, 2048 };
            var sizeField = new PopupField<int>("New Map Resolution", sizes, Mathf.Max(0, sizes.IndexOf(resolution)));
            sizeField.RegisterValueChangedCallback(e => resolution = e.newValue);
            root.Add(sizeField);
            root.Add(new Button(() => BeginMap(false)) { text = paintMask ? "Create White Mask (Fully Visible)" : "Create New Splat Map (Base Layer R)" });
            root.Add(new Button(() => BeginMap(true)) { text = "Edit Map from Material" });
            var layers = new PopupField<string>("Layer", new System.Collections.Generic.List<string> { "R — Base", "G — Layer 1", "B — Layer 2", "A — Layer 3" }, channel);
            layers.RegisterValueChangedCallback(e => channel = layers.index);
            root.Add(layers);
            layers.EnableInClassList("splat-hidden", paintMask);
            var maskAction = new PopupField<string>("Mask Action", new System.Collections.Generic.List<string> { "Hide — Black", "Show — White" }, hideMask ? 0 : 1);
            maskAction.RegisterValueChangedCallback(e => hideMask = maskAction.index == 0);
            maskAction.EnableInClassList("splat-hidden", !paintMask);
            root.Add(maskAction);
            var brush = new PopupField<string>("Brush", new System.Collections.Generic.List<string> { "Soft", "Hard" }, soft ? 0 : 1);
            brush.RegisterValueChangedCallback(e => soft = brush.index == 0);
            root.Add(brush);
            var radiusField = new Slider("Radius (m)", 0.01f, 10f) { value = brushRadius, showInputField = true };
            radiusField.RegisterValueChangedCallback(e => brushRadius = e.newValue);
            root.Add(radiusField);
            var strengthField = new Slider("Strength", 0.01f, 1) { value = strength, showInputField = true };
            strengthField.RegisterValueChangedCallback(e => strength = e.newValue);
            root.Add(strengthField);
            paintToggle = new Toggle("Paint in Scene View") { value = painting };
            paintToggle.RegisterValueChangedCallback(e => { painting = e.newValue; EndStroke(); SceneView.RepaintAll(); });
            root.Add(paintToggle);
            root.Add(new HelpBox(paintMask
                ? "Left mouse: paint mask. Shift: reverse action (show / hide). Soft brush creates smooth transparency. Alt: navigate. Ctrl+Z: undo. Radius is measured in world metres."
                : "Left mouse / drag: paint. Shift: restore base layer R. Alt: navigate. Ctrl+Z: undo. Radius is measured in world metres and is independent of UV stretching and object scale.", HelpBoxMessageType.Info));
            root.Add(new Button(() => SaveMap()) { text = "Save PNG and Assign to Material" });
            root.Add(new Button(() =>
            {
                string previous = savePath;
                savePath = null;
                if (!SaveMap()) savePath = previous;
            }) { text = "Save PNG As…" });
            status = new Label(buffer ? "Map ready for editing." : "Select a model and create or open a map.");
            root.Add(status);
            image = new Image { image = working, scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("splat-map-preview");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Tools/SplatMap/Editor/SplatMapPainter.uss");
            if (styles) root.styleSheets.Add(styles);
            root.Add(image);
        }

        bool ResolvePending()
        {
            if (!hasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Splat Map Painter", "The map has unsaved changes.", "Save", "Cancel", "Discard");
            if (choice == 1 || (choice == 0 && !SaveMap())) return false;
            hasUnsavedChanges = false;
            return true;
        }

        void SetTarget(MeshRenderer next)
        {
            if (next == target || !ResolvePending()) return;
            ReleasePreview();
            if (buffer) { Undo.ClearUndo(buffer); DestroyImmediate(buffer); }
            buffer = null;
            target = next;
            original = null;
            savePath = null;
            painting = false;
            paintToggle?.SetValueWithoutNotify(false);
            if (image != null) image.image = null;
            Message("Model selected. Create or open a map.");
        }

        bool ValidateTarget(out Mesh mesh)
        {
            mesh = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || !target || EditorUtility.IsPersistent(target))
            { Message("Select a MeshRenderer in a scene outside Play Mode."); return false; }
            if (PrefabStageUtility.GetPrefabStage(target.gameObject) != null)
            { Message("Paint on a model instance in a regular scene, outside Prefab Mode."); return false; }
            var filter = target.GetComponent<MeshFilter>();
            mesh = filter ? filter.sharedMesh : null;
            Material material = original ? original : target.sharedMaterial;
            if (!mesh || target.sharedMaterials.Length != 1 || !material || material.shader.name != ShaderName)
            { Message("Requires a MeshFilter and one ExtractShaders/SplatRGBA material."); return false; }
            if (material.GetTextureScale(MapProperty) != Vector2.one || material.GetTextureOffset(MapProperty) != Vector2.zero)
            { Message("Set the painted map to Tiling (1,1) and Offset (0,0). Layer textures can use any tiling."); return false; }
            return true;
        }

        void BeginMap(bool loadExisting)
        {
            if (!ValidateTarget(out Mesh mesh) || !ResolvePending()) return;
            Texture2D source = loadExisting ? (original ? original : target.sharedMaterial).GetTexture(MapProperty) as Texture2D : null;
            if (loadExisting && (!source || source.width != source.height || source.width > 2048))
            { Message("Assign a square map up to 2048×2048 to the matching material field."); return; }
            var sourceImporter = source ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as TextureImporter : null;
            if (sourceImporter && (sourceImporter.sRGBTexture || sourceImporter.alphaIsTransparency))
            { Message("Disable sRGB and Alpha Is Transparency in Import Settings for the existing map first."); return; }
            Color[] pixels;
            try
            {
                // Probe mesh readability before replacing the current painting session.
                _ = mesh.vertices;
                Vector2[] uv = mesh.uv;
                if (uv.Length != mesh.vertexCount) throw new InvalidOperationException("The model has no UV0.");
                foreach (Vector2 p in uv)
                    if (p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1)
                        throw new InvalidOperationException("UV0 extends outside 0–1. A unique UV layout is required.");
                pixels = source ? ReadPixels(source) : new Color[resolution * resolution];
                if (!source) for (int i = 0; i < pixels.Length; i++) pixels[i] = paintMask ? Color.white : new Color(1, 0, 0, 0);
                if (paintMask) for (int i = 0; i < pixels.Length; i++) pixels[i] = SplatBrush.BlendMask(pixels[i], 1, 0);
            }
            catch (Exception e) { Message(e.Message); return; }
            ReleasePreview();
            if (buffer) { Undo.ClearUndo(buffer); DestroyImmediate(buffer); }
            original = target.sharedMaterial;
            buffer = CreateInstance<SplatPaintBuffer>();
            buffer.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            buffer.size = source ? source.width : resolution;
            buffer.pixels = pixels;
            string sourcePath = source ? AssetDatabase.GetAssetPath(source) : null;
            savePath = sourcePath != null && sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? sourcePath : null;
            hasUnsavedChanges = !source;
            RestorePreview();
            Message("Map ready. Enable Paint in Scene View.");
        }

        static Color[] ReadPixels(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy.GetPixels();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); DestroyImmediate(copy); }
        }

        void RestorePreview()
        {
            if (!buffer || !target || !original || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                working = new Texture2D(buffer.size, buffer.size, TextureFormat.RGBA32, false, true)
                { name = "Splat painting preview", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                preview = new Material(original) { hideFlags = HideFlags.HideAndDontSave };
                preview.SetTexture(MapProperty, working);
                if (paintMask) preview.SetFloat("_UseOpacityMask", 1);
                SplatShaderGUI.Configure(preview);
                target.sharedMaterial = preview;
                var source = target.GetComponent<MeshFilter>().sharedMesh;
                sourceVertices = source.vertices;
                meshUV = source.uv;
                triangles = source.triangles;
                rayMesh = Instantiate(source);
                rayMesh.hideFlags = HideFlags.HideAndDontSave;
                var go = new GameObject("Splat painter raycast") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
                rayCollider = go.AddComponent<MeshCollider>();
                UpdateRayMesh();
                UpdateTexture();
            }
            catch (Exception e) { ReleasePreview(); Message("Could not prepare the mesh: " + e.Message); }
        }

        void UpdateRayMesh()
        {
            bakedMatrix = target.transform.localToWorldMatrix;
            var vertices = new Vector3[sourceVertices.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = bakedMatrix.MultiplyPoint3x4(sourceVertices[i]);
            rayMesh.vertices = vertices;
            // Keep outward winding under mirrored transforms.
            var indices = (int[])triangles.Clone();
            if (bakedMatrix.determinant < 0)
                for (int i = 0; i < indices.Length; i += 3) (indices[i], indices[i + 1]) = (indices[i + 1], indices[i]);
            rayMesh.triangles = indices;
            surfaceBrush = new SplatSurfaceBrush(vertices, meshUV, indices, buffer.size);
            rayMesh.RecalculateBounds();
            rayCollider.sharedMesh = null;
            rayCollider.sharedMesh = rayMesh;
        }

        void ReleasePreview()
        {
            EndStroke();
            if (target && preview && target.sharedMaterial == preview) target.sharedMaterial = original;
            if (preview) DestroyImmediate(preview);
            if (working) DestroyImmediate(working);
            if (rayCollider) DestroyImmediate(rayCollider.gameObject);
            if (rayMesh) DestroyImmediate(rayMesh);
            preview = null; working = null; rayCollider = null; rayMesh = null;
            surfaceBrush = null;
        }

        void BeforeSceneSave(Scene scene, string path)
        {
            if (target && preview && target.sharedMaterial == preview) target.sharedMaterial = original;
        }
        void AfterSceneSave(Scene scene)
        {
            if (target && preview && target.sharedMaterial == original) target.sharedMaterial = preview;
        }
        void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { painting = false; paintToggle?.SetValueWithoutNotify(false); ReleasePreview(); }
            if (state == PlayModeStateChange.EnteredEditMode) RestorePreview();
        }
        void OnUndo() { if (buffer) { hasUnsavedChanges = true; UpdateTexture(); } }
        void UpdateTexture()
        {
            if (!working || !buffer) return;
            working.SetPixels(buffer.pixels);
            working.Apply(false);
            if (image != null) { image.image = working; image.MarkDirtyRepaint(); }
            SceneView.RepaintAll();
        }
        void Message(string text) { if (status != null) status.text = text; }

        void DuringSceneGUI(SceneView scene)
        {
            if (!painting || !buffer || !target || !rayCollider || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(id);
            if (e.alt) { EndStroke(); return; }
            if (e.type == EventType.MouseUp && e.button == 0 && stroke) { EndStroke(); e.Use(); return; }
            if (target.transform.localToWorldMatrix != bakedMatrix) UpdateRayMesh();
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!rayCollider.Raycast(ray, out RaycastHit hit, float.MaxValue)) { lastValid = false; return; }
            if (e.type == EventType.Repaint) DrawBrush(hit, e.shift ? 0 : channel);
            if (e.type == EventType.MouseMove) scene.Repaint();
            if (e.button != 0 || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag)) return;
            if (e.type == EventType.MouseDown)
            {
                if (HandleUtility.nearestControl != id) return;
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Paint splat map");
                Undo.RegisterCompleteObjectUndo(buffer, "Paint splat map");
                GUIUtility.hotControl = control = id;
                stroke = true;
                lastValid = false;
            }
            if (!stroke || GUIUtility.hotControl != control) return;
            int paintChannel = e.shift ? 0 : channel;
            float visibility = hideMask != e.shift ? 0 : 1;
            float distance = lastValid ? Vector3.Distance(lastPoint, hit.point) : 0;
            // Interpolate short surface strokes, but do not bridge large jumps or opposite-facing surfaces.
            int steps = lastValid && distance < brushRadius * 4 && Vector3.Dot(lastNormal, hit.normal) > 0.5f
                ? Mathf.Max(1, Mathf.CeilToInt(distance / (brushRadius * 0.35f))) : 1;
            bool painted = false;
            for (int i = 1; i <= steps; i++)
                painted |= surfaceBrush.Stamp(buffer.pixels,
                    steps > 1 ? Vector3.Lerp(lastPoint, hit.point, (float)i / steps) : hit.point,
                    steps > 1 ? Vector3.Lerp(lastNormal, hit.normal, (float)i / steps).normalized : hit.normal,
                    brushRadius, strength, soft, paintChannel, paintMask, visibility);
            lastPoint = hit.point; lastNormal = hit.normal; lastValid = true;
            if (painted)
            {
                hasUnsavedChanges = true;
                EditorUtility.SetDirty(buffer);
                UpdateTexture();
            }
            else Message("No texels inside the brush. Increase Radius or use a higher-resolution map.");
            e.Use();
        }

        void EndStroke()
        {
            if (stroke) Undo.CollapseUndoOperations(undoGroup);
            if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            stroke = false; lastValid = false; control = 0;
        }

        void DrawBrush(RaycastHit hit, int selectedChannel)
        {
            Color previous = Handles.color;
            Handles.color = paintMask ? Color.white : selectedChannel == 0 ? Color.red : selectedChannel == 1 ? Color.green : selectedChannel == 2 ? Color.cyan : Color.yellow;
            Handles.DrawWireDisc(hit.point + hit.normal * 0.002f, hit.normal, brushRadius);
            Handles.color = previous;
        }

        bool SaveMap()
        {
            EndStroke();
            if (!buffer || !target || !original || !working) { Message("Create or open a map first."); return false; }
            string path = savePath;
            if (string.IsNullOrEmpty(path))
                path = EditorUtility.SaveFilePanelInProject("Save Map", target.name + (paintMask ? "_Mask" : "_Splat"), "png", "Choose a location inside Assets.");
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string absolute = Path.GetFullPath(path);
                string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
                if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The map must be saved inside Assets.");
                File.WriteAllBytes(absolute, working.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(buffer.size));
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Undo.RecordObject(original, "Assign painted map");
                original.SetTexture(MapProperty, saved);
                if (paintMask) original.SetFloat("_UseOpacityMask", 1);
                SplatShaderGUI.Configure(original);
                EditorUtility.SetDirty(original);
                AssetDatabase.SaveAssetIfDirty(original);
                savePath = path;
                hasUnsavedChanges = false;
                Message("Saved: " + path + ". Materials using this asset will also update.");
                return true;
            }
            catch (Exception e) { Message("Save failed: " + e.Message); return false; }
        }
    }
}
