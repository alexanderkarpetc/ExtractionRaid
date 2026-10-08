using System;
using ExtractionRaid.Editor.SplatMap;
using ExtractionRaid.View.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ExtractionRaid.Editor.Roads
{
    public sealed class RoadBuilderWindow : EditorWindow
    {
        [SerializeField] SplatRoad road;
        [SerializeField] int selectedPoint = -1;
        bool placePoints;
        bool editPoints = true;
        SerializedObject serializedRoad;
        HelpBox status;
        string message = "Create a road or select an existing Splat Road.";

        [MenuItem("Tools/Level Design/Road Builder")]
        public static void Open() => GetWindow<RoadBuilderWindow>("Road Builder");

        public static void OpenFor(SplatRoad target)
        {
            var window = GetWindow<RoadBuilderWindow>("Road Builder");
            window.SetRoad(target);
        }

        void OnEnable()
        {
            minSize = new Vector2(420, 520);
            SceneView.duringSceneGui += DuringSceneGUI;
            Selection.selectionChanged += OnSelection;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (!road && Selection.activeGameObject) road = Selection.activeGameObject.GetComponent<SplatRoad>();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Selection.selectionChanged -= OnSelection;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            rootVisualElement.Unbind();
            serializedRoad?.Dispose();
            serializedRoad = null;
        }

        public void CreateGUI()
        {
            rootVisualElement.Unbind();
            serializedRoad?.Dispose();
            serializedRoad = null;
            rootVisualElement.Clear();
            var root = new ScrollView();
            rootVisualElement.Add(root);
            root.Add(new HelpBox("Place road points on ground colliders, then build a mesh and paint it with Splat Map Painter. The scene must be saved first.", HelpBoxMessageType.Info));
            root.Add(new Button(CreateRoad) { text = "Create New Road" });
            root.Add(new Button(RoadIntersectionEditor.CreateFromSelection) { text = "Create Intersection from Selected Roads" });
            var roadField = new ObjectField("Road") { objectType = typeof(SplatRoad), allowSceneObjects = true, value = road };
            roadField.RegisterValueChangedCallback(e => SetRoad(e.newValue as SplatRoad));
            root.Add(roadField);
            if (road)
            {
                var owner = IntersectionAssets.Owner(road);
                if (owner)
                {
                    root.Add(new HelpBox("This road is connected to an intersection. Rebuild the intersection after changing road settings or points.", HelpBoxMessageType.Info));
                    root.Add(new Button(() => Selection.activeGameObject = owner.gameObject) { text = "Select Connected Intersection" });
                }
                var addToggle = new Toggle("Place Points (Left Click)") { value = placePoints };
                addToggle.RegisterValueChangedCallback(e => { placePoints = e.newValue; SceneView.RepaintAll(); });
                root.Add(addToggle);
                var editToggle = new Toggle("Edit Points in Scene View") { value = editPoints };
                editToggle.RegisterValueChangedCallback(e => { editPoints = e.newValue; SceneView.RepaintAll(); });
                root.Add(editToggle);
                root.Add(new HelpBox("Enable Place Points and left-click the ground. Disable it to select point spheres and drag the position handle. Alt navigates the camera. Rebuild after editing. Tight bends and intersections may overlap.", HelpBoxMessageType.Info));
                root.Add(new Button(() => RemovePoint(selectedPoint)) { text = "Remove Selected Point" });
                root.Add(new Button(() => RemovePoint(road.points.Count - 1)) { text = "Remove Last Point" });
                root.Add(new Button(CreateOverlay) { text = "Create Overlay from Current Path" });
                serializedRoad = new SerializedObject(road);
                var splatToggle = new Toggle("Use Splat Maps") { value = road.useSplatMaps };
                splatToggle.SetEnabled(!road.generatedMesh);
                splatToggle.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(road, "Set road material mode");
                    road.useSplatMaps = e.newValue;
                    Changed();
                    rootVisualElement.schedule.Execute(CreateGUI);
                });
                root.Add(splatToggle);
                root.Add(new HelpBox("Material Template is copied on the first build. Afterwards edit the generated material. Regular mode fits the texture once across the width and repeats it along the road.", HelpBoxMessageType.Info));
                foreach (string property in new[] { "points", "width", "lateralOffset", "sampleSpacing", "smooth", "widthSegments", "snapToGround", "groundLayers", "rayHeight", "rayDistance", "surfaceLift", "textureTileSize", "depthOffset", "materialTemplate" })
                {
                    var field = new PropertyField(serializedRoad.FindProperty(property));
                    if (property == "lateralOffset") field.label = "Lateral Offset (m)";
                    field.RegisterCallback<SerializedPropertyChangeEvent>(_ => { UpdateStatus(); SceneView.RepaintAll(); });
                    root.Add(field);
                }
                if (road.useSplatMaps)
                {
                root.Add(new HelpBox("The following settings are used only when creating the initial maps. Rebuild keeps all painted PNGs. Layer texture Wrap Mode should be Repeat.", HelpBoxMessageType.Info));
                var sizes = new System.Collections.Generic.List<int> { 256, 512, 1024, 2048 };
                var resolution = new PopupField<int>("Initial Map Resolution", sizes, Mathf.Max(0, sizes.IndexOf(road.mapResolution)));
                resolution.RegisterValueChangedCallback(e => { Undo.RecordObject(road, "Set road map resolution"); road.mapResolution = e.newValue; Changed(); });
                root.Add(resolution);
                root.Add(new PropertyField(serializedRoad.FindProperty("edgeFade")));
                root.Add(new PropertyField(serializedRoad.FindProperty("fadeEnds")));
                }
                root.Bind(serializedRoad);
                root.Add(new Button(BuildRoad) { text = road.useSplatMaps ? (road.generatedMesh ? "Rebuild Road (Keep Paint Maps)" : "Build Road and Create Paint Maps") : (road.generatedMesh ? "Rebuild Mesh" : "Build Mesh") });
                if (road.useSplatMaps || (road.generatedMaterial && road.generatedMaterial.shader.name == "ExtractionRaid/Road Marking Unlit")) root.Add(new Button(OpenPainter) { text = "Open Splat Map Painter" });
                root.Add(new Button(() => { Selection.activeGameObject = road.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }) { text = "Select and Frame Road" });
            }
            status = new HelpBox("", HelpBoxMessageType.Info);
            root.Add(status);
            UpdateStatus();
        }

        void SetRoad(SplatRoad next)
        {
            if (next && (EditorUtility.IsPersistent(next) || PrefabStageUtility.GetPrefabStage(next.gameObject) != null))
            { message = "Use a road instance in a regular scene, outside Prefab Mode."; UpdateStatus(); return; }
            road = next;
            selectedPoint = -1;
            placePoints = false;
            CreateGUI();
            SceneView.RepaintAll();
        }

        void OnSelection()
        {
            var next = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<SplatRoad>() : null;
            if (next && next != road) SetRoad(next);
        }

        void OnUndo() { selectedPoint = road ? Mathf.Min(selectedPoint, road.points.Count - 1) : -1; CreateGUI(); SceneView.RepaintAll(); }
        void OnPlayMode(PlayModeStateChange _) { placePoints = false; CreateGUI(); }

        void CreateRoad()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || PrefabStageUtility.GetCurrentPrefabStage() != null)
            { message = "Save a regular scene first and leave Play/Prefab Mode."; UpdateStatus(); return; }
            var go = new GameObject("Splat Road");
            Undo.RegisterCreatedObjectUndo(go, "Create Splat Road");
            road = Undo.AddComponent<SplatRoad>(go);
            selectedPoint = -1;
            Selection.activeGameObject = go;
            placePoints = true;
            message = "Click the ground to place at least two points.";
            EditorSceneManager.MarkSceneDirty(scene);
            CreateGUI();
        }

        void CreateOverlay()
        {
            if (!CanEdit()) return;
            if (road.points.Count < 2 || string.IsNullOrEmpty(road.gameObject.scene.path))
            { message = "Save the scene and add at least two points first."; UpdateStatus(); return; }
            var overlay = RoadOverlay.Create(road);
            SetRoad(overlay);
            Selection.activeGameObject = overlay.gameObject;
            message = "Independent path copied. Assign Material Template, adjust Width if needed, then click Build Mesh.";
            UpdateStatus();
        }

        void Changed()
        {
            EditorUtility.SetDirty(road);
            if (PrefabUtility.IsPartOfPrefabInstance(road)) PrefabUtility.RecordPrefabInstancePropertyModifications(road);
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            UpdateStatus();
            SceneView.RepaintAll();
        }

        void RemovePoint(int index)
        {
            if (!road || index < 0 || index >= road.points.Count || !CanEdit()) return;
            Undo.RecordObject(road, "Remove road point");
            road.points.RemoveAt(index);
            selectedPoint = Mathf.Min(index, road.points.Count - 1);
            message = "Point removed. Rebuild to update the mesh.";
            Changed();
        }

        bool CanEdit()
        {
            if (!road || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(road) || PrefabStageUtility.GetPrefabStage(road.gameObject) != null) return false;
            if (SplatMapPainterWindow.HasOpenMapFor(road.GetComponent<MeshRenderer>()))
            { message = "Save maps and close Splat Map Painter before editing this road."; UpdateStatus(); return false; }
            return true;
        }

        void BuildRoad()
        {
            if (!CanEdit()) return;
            try
            {
                if (RoadAssets.Build(road, out string report)) { placePoints = false; message = report; CreateGUI(); }
                else { message = report; UpdateStatus(); }
            }
            catch (Exception e) { message = "Build failed: " + e.Message; UpdateStatus(); }
            SceneView.RepaintAll();
        }

        void OpenPainter()
        {
            if (!road || !road.generatedMesh) { message = "Build the road first."; UpdateStatus(); return; }
            placePoints = false;
            editPoints = false;
            CreateGUI();
            SplatMapPainterWindow.OpenFor(road.GetComponent<MeshRenderer>());
        }

        void UpdateStatus()
        {
            if (status != null) status.text = road ? $"Points: {road.points.Count}. Selected: {(selectedPoint >= 0 ? (selectedPoint + 1).ToString() : "none")}. {message}" : message;
        }

        void DuringSceneGUI(SceneView scene)
        {
            if (!road || !CanEdit()) return;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (placePoints && !e.alt && e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            var world = new Vector3[road.points.Count];
            for (int i = 0; i < world.Length; i++) world[i] = road.transform.TransformPoint(road.points[i]);
            Color previous = Handles.color;
            Handles.color = new Color(1, 0.75f, 0.15f);
            if (world.Length >= 2)
            {
                try { Handles.DrawAAPolyLine(3, RoadGeometry.SamplePath(world, Mathf.Max(road.sampleSpacing, 0.5f), road.smooth).ToArray()); }
                catch (ArgumentException) { Handles.DrawAAPolyLine(2, world); }
            }
            for (int i = 0; i < world.Length; i++)
            {
                float size = HandleUtility.GetHandleSize(world[i]) * 0.065f;
                Handles.Label(world[i] + Vector3.up * size * 2, (i + 1).ToString());
                if (!placePoints && editPoints)
                {
                    if (Handles.Button(world[i], Quaternion.identity, size, size, Handles.SphereHandleCap))
                    { selectedPoint = i; UpdateStatus(); Repaint(); }
                }
                else if (e.type == EventType.Repaint) Handles.SphereHandleCap(0, world[i], Quaternion.identity, size, EventType.Repaint);
            }
            Handles.color = previous;
            if (!placePoints && editPoints && selectedPoint >= 0 && selectedPoint < world.Length)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world[selectedPoint], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    try
                    {
                        Physics.SyncTransforms();
                        if (road.snapToGround && new RoadGroundQuery(road).Project(moved, out Vector3 ground)) moved = ground;
                        Undo.RecordObject(road, "Move road point");
                        road.points[selectedPoint] = road.transform.InverseTransformPoint(moved);
                        message = "Point moved. Rebuild to update the mesh.";
                        Changed();
                    }
                    catch (Exception ex) { message = ex.Message; UpdateStatus(); }
                }
            }
            if (!placePoints || e.alt || e.type != EventType.MouseDown || e.button != 0 || HandleUtility.nearestControl != control) return;
            try
            {
                if (road.points.Count >= 256) { message = "Limit: 256 points per road. Split the road into sections."; UpdateStatus(); return; }
                Physics.SyncTransforms();
                if (!new RoadGroundQuery(road).Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), float.MaxValue, out RaycastHit hit))
                { message = "No ground hit. Check colliders and Ground Layers."; UpdateStatus(); return; }
                Undo.RecordObject(road, "Add road point");
                road.points.Add(road.transform.InverseTransformPoint(hit.point));
                selectedPoint = road.points.Count - 1;
                message = "Point added. Build or rebuild when ready.";
                Changed();
                e.Use();
            }
            catch (Exception ex) { message = ex.Message; UpdateStatus(); }
        }
    }
}
