using System;
using System.Collections.Generic;
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
    [CustomEditor(typeof(RoadIntersection))]
    public sealed class RoadIntersectionEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Level Design/Create Intersection from Selected Roads")]
        public static void CreateFromSelection()
        {
            try
            {
                var roads = new List<SplatRoad>();
                foreach (var go in Selection.gameObjects)
                {
                    var road = go.GetComponent<SplatRoad>();
                    if (road && !roads.Contains(road)) roads.Add(road);
                }
                if (roads.Count < 3 || roads.Count > 4) throw new ArgumentException("Select 3 (T) or 4 (X) road objects in the Hierarchy.");
                var scene = roads[0].gameObject.scene;
                foreach (var road in roads)
                    if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || string.IsNullOrEmpty(scene.path) ||
                        road.gameObject.scene != scene || PrefabStageUtility.GetPrefabStage(road.gameObject) != null ||
                        road.points.Count < 2 || !road.generatedMesh || !road.useSplatMaps || IntersectionAssets.Owner(road))
                        throw new ArgumentException("Select independently built Splat roads in one saved regular scene, outside Play Mode. Roads already attached to intersections are not supported.");
                // Pick the combination of ends forming the tightest cluster; editable in the Inspector.
                float best = float.PositiveInfinity;
                int selected = 0;
                Vector3 center = Vector3.zero;
                for (int mask = 0; mask < (1 << roads.Count); mask++)
                {
                    Vector3 average = Vector3.zero;
                    for (int i = 0; i < roads.Count; i++) average += End(roads[i], (mask & (1 << i)) == 0);
                    average /= roads.Count;
                    float score = 0;
                    for (int i = 0; i < roads.Count; i++) score += (End(roads[i], (mask & (1 << i)) == 0) - average).sqrMagnitude;
                    if (score < best) { best = score; selected = mask; center = average; }
                }
                var goNode = new GameObject(roads.Count == 3 ? "Road T Intersection" : "Road X Intersection");
                SceneManager.MoveGameObjectToScene(goNode, scene);
                goNode.transform.position = center;
                goNode.layer = roads[0].gameObject.layer;
                var node = goNode.AddComponent<RoadIntersection>();
                foreach (var road in roads) node.cutback = Mathf.Max(node.cutback, road.width * 1.5f);
                for (int i = 0; i < roads.Count; i++)
                    node.approaches.Add(new RoadIntersectionEnd { road = roads[i], start = (selected & (1 << i)) == 0 });
                node.materialTemplate = roads[0].generatedMaterial;
                Undo.RegisterCreatedObjectUndo(goNode, "Create Road Intersection");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = goNode;
            }
            catch (Exception e) { EditorUtility.DisplayDialog("Create Intersection", e.Message, "OK"); }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var node = (RoadIntersection)target;
            var root = new VisualElement();
            root.Add(new HelpBox("Start selects the first point; unchecked selects the last. Place connected ends near the same center. Cutback is measured along each road and snaps to its mesh rows. Rebuild here after editing approaches. Existing PNGs are kept. One intersection per road object.", HelpBoxMessageType.Info));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var status = new HelpBox("Build to trim approaches and create the intersection.", HelpBoxMessageType.Info);
            root.Add(new Button(() =>
            {
                try
                {
                    serializedObject.ApplyModifiedProperties();
                    if (IntersectionAssets.Build(node)) status.text = "Intersection built. Existing road maps were preserved.";
                }
                catch (Exception e) { status.text = e.Message; }
            }) { text = "Build / Rebuild Intersection" });
            root.Add(new Button(() =>
            {
                if (node.generatedMesh) SplatMapPainterWindow.OpenFor(node.GetComponent<MeshRenderer>());
                else status.text = "Build the intersection first.";
            }) { text = "Open Splat Map Painter" });
            root.Add(new Button(() =>
            {
                try { IntersectionAssets.Detach(node); status.text = "Roads restored. Intersection renderer disabled; assets kept."; }
                catch (Exception e) { status.text = e.Message; }
            }) { text = "Detach and Restore Roads" });
            root.Add(status);
            return root;
        }

        void OnSceneGUI()
        {
            var node = (RoadIntersection)target;
            foreach (var end in node.approaches)
            {
                if (end == null || !end.road || end.road.points.Count < 2) continue;
                Vector3 p = End(end.road, end.start);
                Handles.Label(p, end.road.name + (end.start ? " — Start" : " — End"));
                Handles.DrawDottedLine(node.transform.position, p, 5);
            }
        }

        static Vector3 End(SplatRoad road, bool start) =>
            road.transform.TransformPoint(road.points[start ? 0 : road.points.Count - 1]);
    }
}