using State;
using UnityEngine;

namespace View
{
    public class BotDebugLabel : MonoBehaviour
    {
        const float VerticalOffset = 2.8f;
        const float CharSize = 0.08f;
        const int FontSize = 32;

        public static bool Enabled = true;

        TextMesh _textMesh;
        MeshRenderer _renderer;

        public static BotDebugLabel Create(Transform parent)
        {
            var go = new GameObject("DebugLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            var label = go.AddComponent<BotDebugLabel>();

            label._textMesh = go.AddComponent<TextMesh>();
            label._textMesh.alignment = TextAlignment.Center;
            label._textMesh.anchor = TextAnchor.LowerCenter;
            label._textMesh.characterSize = CharSize;
            label._textMesh.fontSize = FontSize;
            label._textMesh.color = Color.white;

            label._renderer = go.GetComponent<MeshRenderer>();

            return label;
        }

        public void UpdateLabel(BotEntityState bot, float currentHp, float maxHp)
        {
            if (!Enabled)
            {
                if (_renderer.enabled) _renderer.enabled = false;
                return;
            }

            if (!_renderer.enabled) _renderer.enabled = true;

            var bb = bot.Blackboard;
            var status = bb.DebugStatus ?? "Idle";
            var distText = bb.HasTarget ? $"Dist: {bb.DistanceToTarget:F1}" : "";
            var seeText = bb.CanSeeTarget ? " [SEE]" : "";

            _textMesh.text = $"[{bot.TypeId}] {status}{seeText}\nHP: {currentHp:F0}/{maxHp:F0}  {distText}";
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }
}
