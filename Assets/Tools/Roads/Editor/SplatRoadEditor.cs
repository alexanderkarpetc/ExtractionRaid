using ExtractionRaid.View.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ExtractionRaid.Editor.Roads
{
    [CustomEditor(typeof(SplatRoad))]
    public sealed class SplatRoadEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Button(() => RoadBuilderWindow.OpenFor((SplatRoad)target)) { text = "Open Road Builder" });
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }
}
