using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    public class MapDuplicateFinderWindow : EditorWindow
    {
        const float DefaultDistanceThreshold = 0.05f;

        readonly List<GameObject> _candidates = new();
        readonly List<DuplicateGroup> _groups = new();
        readonly HashSet<GameObject> _visited = new();

        Vector2 _scroll;
        float _distanceThreshold = DefaultDistanceThreshold;
        bool _scanSelectionOnly;
        bool _includeInactive = true;
        bool _collapsePrefabInstances = true;
        bool _sameNormalizedNameOnly = true;
        bool _ignoreChildrenOfDuplicateRoots = true;

        [MenuItem("Raid/Map Duplicate Finder")]
        static void Open()
        {
            GetWindow<MapDuplicateFinderWindow>("Map Duplicate Finder");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Map Duplicate Finder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Finds scene objects that have the same or very close world positions. The tool only selects objects; it does not delete anything.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            _distanceThreshold = Mathf.Max(0f, EditorGUILayout.FloatField("Distance Threshold", _distanceThreshold));
            _scanSelectionOnly = EditorGUILayout.ToggleLeft("Scan selected objects and their children only", _scanSelectionOnly);
            _includeInactive = EditorGUILayout.ToggleLeft("Include inactive objects", _includeInactive);
            _collapsePrefabInstances = EditorGUILayout.ToggleLeft("Collapse prefab instances to their root object", _collapsePrefabInstances);
            _sameNormalizedNameOnly = EditorGUILayout.ToggleLeft("Group only objects with the same normalized name", _sameNormalizedNameOnly);
            _ignoreChildrenOfDuplicateRoots = EditorGUILayout.ToggleLeft("Hide children when their parent is already in a duplicate group", _ignoreChildrenOfDuplicateRoots);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find Duplicates", GUILayout.Height(28)))
                FindDuplicates();

            using (new EditorGUI.DisabledScope(_groups.Count == 0))
            {
                if (GUILayout.Button("Select All Found", GUILayout.Height(28)))
                    SelectAllFound();
            }
            EditorGUILayout.EndHorizontal();

            DrawSummary();
            DrawResults();
        }

        void FindDuplicates()
        {
            _groups.Clear();
            GatherCandidates();

            float sqrThreshold = _distanceThreshold * _distanceThreshold;
            _visited.Clear();

            for (int i = 0; i < _candidates.Count; i++)
            {
                GameObject seed = _candidates[i];
                if (seed == null || _visited.Contains(seed))
                    continue;

                var group = new DuplicateGroup(seed.transform.position);
                group.Objects.Add(seed);

                for (int j = i + 1; j < _candidates.Count; j++)
                {
                    GameObject other = _candidates[j];
                    if (other == null || _visited.Contains(other))
                        continue;

                    if (_sameNormalizedNameOnly && NormalizeName(seed.name) != NormalizeName(other.name))
                        continue;

                    if ((seed.transform.position - other.transform.position).sqrMagnitude > sqrThreshold)
                        continue;

                    group.Objects.Add(other);
                }

                if (group.Objects.Count <= 1)
                    continue;

                foreach (GameObject obj in group.Objects)
                    _visited.Add(obj);

                if (_ignoreChildrenOfDuplicateRoots && HasDuplicateAncestor(group.Objects))
                    continue;

                _groups.Add(group);
            }

            _groups.Sort((a, b) => b.Objects.Count.CompareTo(a.Objects.Count));
            Repaint();
        }

        void GatherCandidates()
        {
            _candidates.Clear();

            if (_scanSelectionOnly)
            {
                foreach (GameObject selected in Selection.gameObjects)
                    AddHierarchy(selected);
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
                AddHierarchy(root);
        }

        void AddHierarchy(GameObject root)
        {
            if (root == null)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(_includeInactive);
            foreach (Transform tr in transforms)
            {
                if (tr == null || tr.gameObject == null)
                    continue;

                if (!_includeInactive && !tr.gameObject.activeInHierarchy)
                    continue;

                GameObject candidate = tr.gameObject;
                if (_collapsePrefabInstances)
                {
                    GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate);
                    if (prefabRoot != null)
                        candidate = prefabRoot;
                }

                if (PrefabUtility.IsPartOfPrefabAsset(candidate))
                    continue;

                if (!_candidates.Contains(candidate))
                    _candidates.Add(candidate);
            }
        }

        void DrawSummary()
        {
            EditorGUILayout.Space(8);

            int duplicateObjectCount = 0;
            foreach (DuplicateGroup group in _groups)
                duplicateObjectCount += group.Objects.Count;

            EditorGUILayout.LabelField("Scan Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Candidates", _candidates.Count.ToString());
            EditorGUILayout.LabelField("Duplicate Groups", _groups.Count.ToString());
            EditorGUILayout.LabelField("Objects In Groups", duplicateObjectCount.ToString());
        }

        void DrawResults()
        {
            if (_groups.Count == 0)
                return;

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _groups.Count; i++)
            {
                DuplicateGroup group = _groups[i];
                if (group == null || group.Objects.Count == 0)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Group {i + 1}: {group.Objects.Count} objects near {FormatPosition(group.Position)}",
                    EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Group"))
                    Selection.objects = group.Objects.ToArray();

                if (GUILayout.Button("Select Duplicates Only"))
                    SelectDuplicatesOnly(group);

                if (GUILayout.Button("Frame First"))
                    FrameObject(group.Objects[0]);
                EditorGUILayout.EndHorizontal();

                for (int j = 0; j < group.Objects.Count; j++)
                {
                    GameObject obj = group.Objects[j];
                    if (obj == null)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                    GUILayout.Label(FormatPosition(obj.transform.position), GUILayout.Width(170));
                    if (GUILayout.Button("Ping", GUILayout.Width(48)))
                        EditorGUIUtility.PingObject(obj);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        void SelectAllFound()
        {
            var objects = new List<UnityEngine.Object>();
            foreach (DuplicateGroup group in _groups)
            {
                foreach (GameObject obj in group.Objects)
                {
                    if (obj != null && !objects.Contains(obj))
                        objects.Add(obj);
                }
            }

            Selection.objects = objects.ToArray();
        }

        static void SelectDuplicatesOnly(DuplicateGroup group)
        {
            var objects = new List<UnityEngine.Object>();
            for (int i = 1; i < group.Objects.Count; i++)
            {
                if (group.Objects[i] != null)
                    objects.Add(group.Objects[i]);
            }

            Selection.objects = objects.ToArray();
        }

        static void FrameObject(GameObject obj)
        {
            if (obj == null)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static bool HasDuplicateAncestor(List<GameObject> objects)
        {
            var set = new HashSet<GameObject>(objects);
            foreach (GameObject obj in objects)
            {
                Transform parent = obj.transform.parent;
                while (parent != null)
                {
                    if (set.Contains(parent.gameObject))
                        return true;

                    parent = parent.parent;
                }
            }

            return false;
        }

        static string NormalizeName(string name)
        {
            name = name.Replace("(Clone)", string.Empty).Trim();

            int lastOpenParen = name.LastIndexOf(" (");
            if (lastOpenParen < 0 || !name.EndsWith(")"))
                return name;

            string suffix = name.Substring(lastOpenParen + 2, name.Length - lastOpenParen - 3);
            if (!int.TryParse(suffix, out _))
                return name;

            return name.Substring(0, lastOpenParen).Trim();
        }

        static string FormatPosition(Vector3 position)
        {
            return $"({position.x:F3}, {position.y:F3}, {position.z:F3})";
        }

        class DuplicateGroup
        {
            public readonly List<GameObject> Objects = new();
            public readonly Vector3 Position;

            public DuplicateGroup(Vector3 position)
            {
                Position = position;
            }
        }
    }
}



