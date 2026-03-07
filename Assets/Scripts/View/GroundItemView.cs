using State;
using UnityEngine;

namespace View
{
    public class GroundItemView : MonoBehaviour
    {
        public EId EntityId { get; private set; }

        TextMesh _label;

        public void Initialize(EId id, string displayName)
        {
            EntityId = id;
            gameObject.name = $"GroundItem_{displayName}_{id}";
            CreateLabel(displayName);
        }

        void CreateLabel(string text)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            _label = labelGo.AddComponent<TextMesh>();
            _label.text = text;
            _label.characterSize = 0.15f;
            _label.fontSize = 48;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Color.white;

            var billboardGo = labelGo;
            billboardGo.AddComponent<BillboardText>();
        }
    }

    public class BillboardText : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
