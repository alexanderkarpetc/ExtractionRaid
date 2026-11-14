using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using View.SpawnPoints;

namespace EditorTools
{
    [CustomEditor(typeof(WorkbenchSpawnPoint))]
    public class WorkbenchSpawnPointEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle _zone = new BoxBoundsHandle();

        void OnSceneGUI()
        {
            var point = (WorkbenchSpawnPoint)target;
            using (new Handles.DrawingScope(Color.cyan,
                Matrix4x4.TRS(point.transform.position, point.transform.rotation, Vector3.one)))
            {
                _zone.center = point.interactionOffset;
                _zone.size = point.interactionSize;
                EditorGUI.BeginChangeCheck();
                _zone.DrawHandle();
                if (!EditorGUI.EndChangeCheck()) return;

                serializedObject.Update();
                serializedObject.FindProperty("interactionOffset").vector3Value = _zone.center;
                serializedObject.FindProperty("interactionSize").vector3Value =
                    Vector3.Max(_zone.size, Vector3.one * 0.01f);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
