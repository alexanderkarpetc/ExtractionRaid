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
        [SerializeField] float radius = 0.025f;
        [SerializeField] float strength = 0.3f;
        [SerializeField] bool soft = true;
        bool painting, stroke, lastValid;
        int control, undoGroup;
        Vector2 lastUV;
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

        [MenuItem("Tools/Level Design/Splat Map Painter")]
        public static void Open() => GetWindow<SplatMapPainterWindow>("Splat Map Painter");

        void OnEnable()
        {
            minSize = new Vector2(420, 520);
            saveChangesMessage = "Зберегти зміни сплат-карти у PNG?";
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
            root.Add(new HelpBox("Меш з UV0 у межах 0–1 та одним матеріалом SplatRGBA. UV-острови, що перекриваються, фарбуються разом.", HelpBoxMessageType.Info));
            var targetField = new ObjectField("Модель") { objectType = typeof(MeshRenderer), allowSceneObjects = true, value = target };
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
            }) { text = "Використати вибрану модель" });
            var sizes = new System.Collections.Generic.List<int> { 256, 512, 1024, 2048 };
            var sizeField = new PopupField<int>("Роздільність нової карти", sizes, Mathf.Max(0, sizes.IndexOf(resolution)));
            sizeField.RegisterValueChangedCallback(e => resolution = e.newValue);
            root.Add(sizeField);
            root.Add(new Button(() => BeginMap(false)) { text = "Створити нову карту (основний шар R)" });
            root.Add(new Button(() => BeginMap(true)) { text = "Редагувати карту з матеріалу" });
            var layers = new PopupField<string>("Шар", new System.Collections.Generic.List<string> { "R — основний", "G — шар 1", "B — шар 2", "A — шар 3" }, channel);
            layers.RegisterValueChangedCallback(e => channel = layers.index);
            root.Add(layers);
            var brush = new PopupField<string>("Кисть", new System.Collections.Generic.List<string> { "М’яка", "Тверда" }, soft ? 0 : 1);
            brush.RegisterValueChangedCallback(e => soft = brush.index == 0);
            root.Add(brush);
            var radiusField = new Slider("Радіус (частка UV-карти)", 0.002f, 0.2f) { value = radius, showInputField = true };
            radiusField.RegisterValueChangedCallback(e => radius = e.newValue);
            root.Add(radiusField);
            var strengthField = new Slider("Сила", 0.01f, 1) { value = strength, showInputField = true };
            strengthField.RegisterValueChangedCallback(e => strength = e.newValue);
            root.Add(strengthField);
            var paintToggle = new Toggle("Малювати у Scene View") { value = painting };
            paintToggle.RegisterValueChangedCallback(e => { painting = e.newValue; EndStroke(); SceneView.RepaintAll(); });
            root.Add(paintToggle);
            root.Add(new HelpBox("ЛКМ / перетягування — малювати. Shift — повернути основний шар R. Alt — навігація. Ctrl+Z — Undo. Радіус заданий у UV, тому масштаб кисті на моделі залежить від розгортки.", HelpBoxMessageType.Info));
            root.Add(new Button(() => SaveMap()) { text = "Зберегти PNG і призначити матеріалу" });
            root.Add(new Button(() =>
            {
                string previous = savePath;
                savePath = null;
                if (!SaveMap()) savePath = previous;
            }) { text = "Зберегти PNG як…" });
            status = new Label(buffer ? "Карта готова до редагування." : "Виберіть модель і створіть або відкрийте карту.");
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
            int choice = EditorUtility.DisplayDialogComplex("Splat Map Painter", "Є незбережені зміни карти.", "Зберегти", "Скасувати", "Відкинути");
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
            if (image != null) image.image = null;
            Message("Модель вибрано. Створіть або відкрийте карту.");
        }

        bool ValidateTarget(out Mesh mesh)
        {
            mesh = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || !target || EditorUtility.IsPersistent(target))
            { Message("Потрібен MeshRenderer у сцені, поза Play Mode."); return false; }
            if (PrefabStageUtility.GetPrefabStage(target.gameObject) != null)
            { Message("Малюйте на екземплярі моделі у звичайній сцені, поза Prefab Mode."); return false; }
            var filter = target.GetComponent<MeshFilter>();
            mesh = filter ? filter.sharedMesh : null;
            Material material = original ? original : target.sharedMaterial;
            if (!mesh || target.sharedMaterials.Length != 1 || !material || material.shader.name != ShaderName)
            { Message("Потрібні MeshFilter і один матеріал ExtractShaders/SplatRGBA."); return false; }
            if (material.GetTextureScale("_SplatMap") != Vector2.one || material.GetTextureOffset("_SplatMap") != Vector2.zero)
            { Message("Для Splat Map задайте Tiling (1,1) та Offset (0,0). Tiling текстур шарів може бути довільним."); return false; }
            return true;
        }

        void BeginMap(bool loadExisting)
        {
            if (!ValidateTarget(out Mesh mesh) || !ResolvePending()) return;
            Texture2D source = loadExisting ? (original ? original : target.sharedMaterial).GetTexture("_SplatMap") as Texture2D : null;
            if (loadExisting && (!source || source.width != source.height || source.width > 2048))
            { Message("Призначте квадратну сплат-карту до 2048×2048 у матеріалі."); return; }
            var sourceImporter = source ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as TextureImporter : null;
            if (sourceImporter && (sourceImporter.sRGBTexture || sourceImporter.alphaIsTransparency))
            { Message("Для наявної сплат-карти спочатку вимкніть sRGB та Alpha Is Transparency в Import Settings."); return; }
            Color[] pixels;
            try
            {
                // Probe mesh readability before replacing the current painting session.
                _ = mesh.vertices;
                Vector2[] uv = mesh.uv;
                if (uv.Length != mesh.vertexCount) throw new InvalidOperationException("Модель не має UV0.");
                foreach (Vector2 p in uv)
                    if (p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1)
                        throw new InvalidOperationException("UV0 виходять за 0–1. Потрібна унікальна UV-розгортка.");
                pixels = source ? ReadPixels(source) : new Color[resolution * resolution];
                if (!source) for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 0, 0, 0);
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
            Message("Карта готова. Увімкніть малювання у Scene View.");
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
                preview.SetTexture("_SplatMap", working);
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
            catch (Exception e) { ReleasePreview(); Message("Не вдалося підготувати меш: " + e.Message); }
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
            if (state == PlayModeStateChange.ExitingEditMode) { painting = false; ReleasePreview(); }
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
            Vector2 uv = hit.textureCoord;
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
            float distance = lastValid ? Vector2.Distance(lastUV, uv) : 0;
            // Avoid bridging distant UV islands when the pointer crosses a seam.
            int steps = lastValid && distance < radius * 4 ? Mathf.Max(1, Mathf.CeilToInt(distance / (radius * 0.35f))) : 1;
            for (int i = 1; i <= steps; i++)
                SplatBrush.Stamp(buffer.pixels, buffer.size, steps > 1 ? Vector2.Lerp(lastUV, uv, (float)i / steps) : uv, radius, strength, soft, paintChannel);
            lastUV = uv; lastValid = true;
            hasUnsavedChanges = true;
            EditorUtility.SetDirty(buffer);
            UpdateTexture();
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
            int index = hit.triangleIndex * 3;
            if (index < 0 || index + 2 >= triangles.Length) return;
            int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
            Vector2 uv1 = meshUV[b] - meshUV[a], uv2 = meshUV[c] - meshUV[a];
            float determinant = uv1.x * uv2.y - uv1.y * uv2.x;
            if (Mathf.Abs(determinant) < 0.000001f) return;
            Vector3 edge1 = bakedMatrix.MultiplyVector(sourceVertices[b] - sourceVertices[a]);
            Vector3 edge2 = bakedMatrix.MultiplyVector(sourceVertices[c] - sourceVertices[a]);
            Vector3 axisU = (edge1 * uv2.y - edge2 * uv1.y) / determinant;
            Vector3 axisV = (-edge1 * uv2.x + edge2 * uv1.x) / determinant;
            var points = new Vector3[49];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / (points.Length - 1);
                points[i] = hit.point + hit.normal * 0.002f + radius * (axisU * Mathf.Cos(angle) + axisV * Mathf.Sin(angle));
            }
            Color previous = Handles.color;
            Handles.color = selectedChannel == 0 ? Color.red : selectedChannel == 1 ? Color.green : selectedChannel == 2 ? Color.cyan : Color.yellow;
            Handles.DrawAAPolyLine(2, points);
            Handles.color = previous;
        }

        bool SaveMap()
        {
            EndStroke();
            if (!buffer || !target || !original || !working) { Message("Спочатку створіть або відкрийте карту."); return false; }
            string path = savePath;
            if (string.IsNullOrEmpty(path))
                path = EditorUtility.SaveFilePanelInProject("Зберегти сплат-карту", target.name + "_Splat", "png", "Виберіть місце у Assets.");
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string absolute = Path.GetFullPath(path);
                string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
                if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Карту потрібно зберегти всередині Assets.");
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
                Undo.RecordObject(original, "Assign splat map");
                original.SetTexture("_SplatMap", saved);
                EditorUtility.SetDirty(original);
                AssetDatabase.SaveAssetIfDirty(original);
                savePath = path;
                hasUnsavedChanges = false;
                Message("Збережено: " + path + ". Матеріали, що використовують цей asset, теж оновляться.");
                return true;
            }
            catch (Exception e) { Message("Помилка збереження: " + e.Message); return false; }
        }
    }
}
