using State;
using UnityEngine;

namespace View
{
    public class HotbarDebugOverlay : MonoBehaviour
    {
        Texture2D _selectedTex;
        Texture2D _occupiedTex;
        Texture2D _emptyTex;
        GUIStyle _slotStyle;

        void Awake()
        {
            _selectedTex = MakeTex(Color.green);
            _occupiedTex = MakeTex(new Color(0.3f, 0.3f, 0.3f, 0.9f));
            _emptyTex = MakeTex(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var player = session.RaidState?.PlayerEntity;
            if (player == null) return;

            if (_slotStyle == null)
            {
                _slotStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 25,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                };
                _slotStyle.normal.textColor = Color.white;
            }

            const float slotW = 200f;
            const float slotH = 140f;
            const float gap = 6f;
            float totalW = PlayerEntityState.HotbarSize * slotW + (PlayerEntityState.HotbarSize - 1) * gap;
            float startX = (Screen.width - totalW) / 2f;
            float startY = Screen.height - slotH - 16f;

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                var rect = new Rect(startX + i * (slotW + gap), startY, slotW, slotH);
                bool isSelected = i == player.SelectedHotbarSlot;
                var weapon = player.Hotbar[i];

                _slotStyle.normal.background = isSelected
                    ? _selectedTex
                    : weapon != null
                        ? _occupiedTex
                        : _emptyTex;

                _slotStyle.normal.textColor = isSelected ? Color.black : Color.white;

                string label = weapon != null
                    ? $"[{i + 1}]\n{weapon.PrefabId}"
                    : $"[{i + 1}]";

                GUI.Box(rect, label, _slotStyle);
            }
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (_selectedTex != null) Destroy(_selectedTex);
            if (_occupiedTex != null) Destroy(_occupiedTex);
            if (_emptyTex != null) Destroy(_emptyTex);
        }
    }
}
