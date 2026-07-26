using Systems.Meta;
using UnityEditor;
using UnityEngine;
using View.SpawnPoints;

namespace Editor
{
    /// <summary>
    /// Collider-style authoring for <see cref="MapRegion"/>. Toggle <b>Edit Region</b>, then
    /// in the Scene view: <b>click the ground</b> to drop connected points, <b>drag</b> the
    /// dots to move them, <b>Shift-click</b> a dot to delete it. Points auto-connect into a
    /// closed outline. Backs the DevCheats <c>🌍 Meta</c> region simulator.
    /// </summary>
    [CustomEditor(typeof(MapRegion))]
    public class MapRegionEditor : UnityEditor.Editor
    {
        bool _editing = true;

        static readonly Color EdgeColor = new(0.30f, 0.82f, 0.78f, 1f);
        static readonly Color FillColor = new(0.30f, 0.82f, 0.78f, 0.10f);

        public override void OnInspectorGUI()
        {
            var region = (MapRegion)target;

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("regionName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("difficultyMultiplier"),
                new GUIContent("Difficulty ×"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));
            serializedObject.ApplyModifiedProperties();

            // What this multiplier actually buys, at the two ends of the gear curve.
            float diff = Mathf.Max(0.1f, region.difficultyMultiplier);
            EditorGUILayout.LabelField(
                $"Survive: worst kit {RaidCombatSimulator.SurviveChance(0f, diff, 0f, 1f):P0} · " +
                $"best kit {RaidCombatSimulator.SurviveChance(1f, diff, 0f, 1f):P0} · " +
                $"full skill tree adds +{RaidCombatSimulator.MaxSkillBonus:P0} flat.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            _editing = GUILayout.Toggle(_editing, _editing ? "● Editing — click the map to add points" : "Edit Region",
                "Button", GUILayout.Height(26));

            EditorGUILayout.HelpBox(
                "Click the ground to add a point.\n" +
                "Drag a dot to move it.  Shift-click a dot to delete it.\n" +
                $"Points: {region.PointCount}{(region.IsValid ? "  ✓ valid" : "  (need ≥3)")}",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!region.IsValid))
            {
                if (GUILayout.Button("Count Contents Inside"))
                    RecountInside(region);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(region.PointCount == 0))
                {
                    if (GUILayout.Button("Remove Last"))
                    {
                        Undo.RecordObject(region, "Remove Region Point");
                        region.points.RemoveAt(region.PointCount - 1);
                        EditorUtility.SetDirty(region);
                    }
                    if (GUILayout.Button("Clear All"))
                    {
                        Undo.RecordObject(region, "Clear Region");
                        region.points.Clear();
                        EditorUtility.SetDirty(region);
                    }
                }
            }

            // Raw coordinates tucked behind a foldout — the scene handles are the primary UI.
            EditorGUILayout.Space(4);
            var listProp = serializedObject.FindProperty("points");
            listProp.isExpanded = EditorGUILayout.Foldout(listProp.isExpanded, "Raw points", true);
            if (listProp.isExpanded)
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(listProp, GUIContent.none, true);
                serializedObject.ApplyModifiedProperties();
            }
        }

        void OnSceneGUI()
        {
            var region = (MapRegion)target;
            var t = region.transform;

            DrawOutline(region);
            if (!_editing) return;

            var e = Event.current;
            float planeY = t.position.y;

            // Shift-click a vertex → delete it (checked before handles so it wins the click).
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                int hit = NearestVertexToMouse(region, e.mousePosition, 14f);
                if (hit >= 0)
                {
                    Undo.RecordObject(region, "Delete Region Point");
                    region.points.RemoveAt(hit);
                    EditorUtility.SetDirty(region);
                    e.Use();
                    return;
                }
            }

            // Draggable dots.
            for (int i = 0; i < region.PointCount; i++)
            {
                Vector3 world = region.WorldPoint(i);
                float size = HandleUtility.GetHandleSize(world) * 0.09f;

                Handles.color = EdgeColor;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(region, "Move Region Point");
                    region.points[i] = t.InverseTransformPoint(new Vector3(moved.x, world.y, moved.z));
                    EditorUtility.SetDirty(region);
                }
                Handles.Label(world + Vector3.up * (size * 3f), i.ToString());
            }

            // Background click (not on a dot) → append a point on the ground plane.
            int bg = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(bg);
            if (e.type == EventType.MouseDown && e.button == 0 && !e.shift && !e.alt
                && HandleUtility.nearestControl == bg)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    Undo.RecordObject(region, "Add Region Point");
                    region.points.Add(t.InverseTransformPoint(ray.GetPoint(enter)));
                    EditorUtility.SetDirty(region);
                    e.Use();
                }
            }
        }

        // Tallies the loot / enemy spawn points that fall inside this polygon and stamps
        // the counts into regionName (📦 containers · ✦ loose · 💀 bots) — the same numbers
        // the DevCheats 🌍 Meta scan will bucket for this region.
        static void RecountInside(MapRegion region)
        {
            var containers = Object.FindObjectsByType<LootContainerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var loose = Object.FindObjectsByType<LooseLootSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var bots = Object.FindObjectsByType<BotSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int c = 0, l = 0, b = 0;
            foreach (var x in containers) if (x.config != null && region.ContainsXZ(x.transform.position)) c++;
            foreach (var x in loose) if (region.ContainsXZ(x.transform.position)) l++;
            foreach (var x in bots) if (x.config != null && region.ContainsXZ(x.transform.position)) b++;

            Undo.RecordObject(region, "Count Region Contents");
            region.regionName = $"{BaseName(region.regionName)} (📦{c} ✦{l} 💀{b})";
            EditorUtility.SetDirty(region);
            Debug.Log($"[Meta] '{region.regionName}' — {c} container(s), {l} loose, {b} enemy spawn(s) inside.");
        }

        // Strips a trailing " (…)" count suffix so repeated recounts don't stack.
        static string BaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Region";
            int idx = name.LastIndexOf(" (", System.StringComparison.Ordinal);
            return idx > 0 ? name.Substring(0, idx) : name;
        }

        static void DrawOutline(MapRegion region)
        {
            if (region.PointCount < 2) return;
            var verts = new Vector3[region.PointCount];
            for (int i = 0; i < region.PointCount; i++) verts[i] = region.WorldPoint(i);

            if (region.IsValid)
            {
                Handles.color = FillColor;
                Handles.DrawAAConvexPolygon(verts); // visual fill only; true shape is the edge loop
            }
            Handles.color = EdgeColor;
            for (int i = 0; i < verts.Length; i++)
                Handles.DrawLine(verts[i], verts[(i + 1) % verts.Length], 2f);
        }

        static int NearestVertexToMouse(MapRegion region, Vector2 mouse, float maxPixels)
        {
            int best = -1;
            float bestDist = maxPixels;
            for (int i = 0; i < region.PointCount; i++)
            {
                Vector2 gp = HandleUtility.WorldToGUIPoint(region.WorldPoint(i));
                float d = Vector2.Distance(gp, mouse);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }
    }
}
