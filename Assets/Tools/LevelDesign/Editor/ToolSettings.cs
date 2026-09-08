using System;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Editor
{
    // Global IDs survive editor restarts; unloaded scenes are deliberately not opened.
    [Serializable]
    public sealed class SceneReference
    {
        public string id = "";
        [SerializeField] UnityEngine.Object cached;
        public UnityEngine.Object Resolve()
        {
            if (cached && GlobalObjectId.GetGlobalObjectIdSlow(cached).ToString() != id) cached = null;
            if (!cached && !string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out var value))
                cached = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(value);
            return cached;
        }
        public void Set(UnityEngine.Object value)
        {
            cached = value;
            id = value ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : "";
        }
    }

    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative("id");
            var cached = property.FindPropertyRelative("cached");
            UnityEngine.Object current = null;
            if (!string.IsNullOrEmpty(id.stringValue) && GlobalObjectId.TryParse(id.stringValue, out var value))
                current = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(value);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.ObjectField(position, label, current, typeof(UnityEngine.Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                id.stringValue = next ? GlobalObjectId.GetGlobalObjectIdSlow(next).ToString() : "";
                cached.objectReferenceValue = next;
            }
        }
    }

    [Serializable]
    public sealed class BoxArea
    {
        public Vector3 center;
        public Vector2 size = new Vector2(30, 30);
        public float rotationY;
        public Vector3 Point(float x, float z) => center + Quaternion.Euler(0, rotationY, 0) *
            new Vector3(x * size.x, 0, z * size.y);
        public bool Valid => size.x > 0 && size.y > 0 && float.IsFinite(size.x) && float.IsFinite(size.y);
    }

    public abstract class ToolSettings : ScriptableObject
    {
        public BoxArea area = new BoxArea();
        public bool showArea = true;
        public bool editAreaHandles;
    }

    public abstract class ToolWindow<T> : EditorWindow where T : ToolSettings
    {
        [SerializeField] protected T settings;
        protected EditorBatch job;
        protected string status = "Ready";
        Vector2 scroll;
        string Key => "ExtractionRaid.LevelDesign." + typeof(T).Name + "." + Application.dataPath;
        protected virtual void OnEnable()
        {
            if (!settings)
            {
                settings = CreateInstance<T>();
                if (EditorPrefs.HasKey(Key)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(Key), settings);
            }
            // HideAndDontSave includes NotEditable, which locks SerializedProperty controls.
            // Apply after deserialization, including settings retained across a domain reload.
            settings.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            SceneView.duringSceneGui += SceneGUI;
            Undo.undoRedoPerformed += UndoChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        protected virtual void OnDisable()
        {
            job?.Dispose();
            Save();
            SceneView.duringSceneGui -= SceneGUI;
            Undo.undoRedoPerformed -= UndoChanged;
            EditorApplication.playModeStateChanged -= PlayChanged;
        }
        void PlayChanged(PlayModeStateChange state) { job?.Dispose(); Repaint(); }
        void UndoChanged() { job?.Dispose(); Save(); Repaint(); SceneView.RepaintAll(); }
        protected void Save() { if (settings) EditorPrefs.SetString(Key, JsonUtility.ToJson(settings)); }
        protected bool Busy => job != null && job.Running;
        protected virtual void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || Busy))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                using var serialized = new SerializedObject(settings);
                serialized.Update();
                var property = serialized.GetIterator();
                bool children = true;
                while (property.NextVisible(children))
                {
                    children = false;
                    if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
                }
                if (serialized.ApplyModifiedProperties()) { Save(); SceneView.RepaintAll(); }
                EditorGUILayout.EndScrollView();
                Buttons();
            }
            if (Busy && GUILayout.Button("Cancel (keep partial result)")) job.Dispose();
            string message = EditorApplication.isPlaying ? "Stop Play Mode to edit level design settings."
                : EditorApplication.isPlayingOrWillChangePlaymode ? "Unity is entering Play Mode; editing is temporarily disabled."
                : Busy ? "Working: " + status : status;
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }
        protected abstract void Buttons();
        protected void StartJob(System.Collections.IEnumerator routine)
        {
            job?.Dispose();
            job = new EditorBatch(routine, () => { if (!Busy) Save(); Repaint(); SceneView.RepaintAll(); },
                error => { status = error.Message; Debug.LogException(error); });
        }
        protected virtual void SceneGUI(SceneView view)
        {
            if (!settings || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!settings.showArea) return;
            var area = settings.area;
            using (new Handles.DrawingScope(Color.cyan, Matrix4x4.TRS(area.center,
                       Quaternion.Euler(0, area.rotationY, 0), Vector3.one)))
                Handles.DrawWireCube(Vector3.zero, new Vector3(area.size.x, 0, area.size.y));
            if (!settings.editAreaHandles || Busy) return;
            EditorGUI.BeginChangeCheck();
            var center = Handles.PositionHandle(area.center, Quaternion.identity);
            var rotation = Handles.RotationHandle(Quaternion.Euler(0, area.rotationY, 0), center);
            var size = Handles.ScaleHandle(new Vector3(area.size.x, 1, area.size.y), center, rotation,
                HandleUtility.GetHandleSize(center));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Edit level design area");
                area.center = center; area.rotationY = rotation.eulerAngles.y;
                area.size = new Vector2(Mathf.Max(.1f, size.x), Mathf.Max(.1f, size.z));
                Save(); Repaint();
            }
        }
    }
}
