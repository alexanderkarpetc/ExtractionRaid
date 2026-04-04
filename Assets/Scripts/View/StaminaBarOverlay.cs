using ApplicationCore;
using UnityEngine;

namespace View
{
    public class StaminaBarOverlay : MonoBehaviour
    {
        const float BarWidth = 200f;
        const float BarHeight = 16f;
        const float MarginX = 16f;
        const float MarginY = 16f;

        static readonly Color BgColor = new(0.1f, 0.1f, 0.1f, 0.7f);
        static readonly Color FillColor = new(0.2f, 0.7f, 1f, 0.9f);
        static readonly Color LowFillColor = new(1f, 0.4f, 0.2f, 0.9f);
        static readonly Color BorderColor = new(0.3f, 0.3f, 0.3f, 0.8f);

        const float LowThreshold = 0.25f;

        Texture2D _whiteTex;

        void Awake()
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        void OnGUI()
        {
            var session = App.Instance?.RaidSession;
            if (session == null) return;

            var player = session.RaidState?.PlayerEntity;
            if (player == null) return;

            if (player.MaxStamina <= 0f) return;

            float ratio = player.Stamina / player.MaxStamina;

            float x = MarginX;
            float y = MarginY;

            var bgRect = new Rect(x - 1f, y - 1f, BarWidth + 2f, BarHeight + 2f);
            DrawRect(bgRect, BorderColor);

            var barBg = new Rect(x, y, BarWidth, BarHeight);
            DrawRect(barBg, BgColor);

            if (ratio > 0f)
            {
                var fillColor = ratio <= LowThreshold ? LowFillColor : FillColor;
                var fillRect = new Rect(x, y, BarWidth * ratio, BarHeight);
                DrawRect(fillRect, fillColor);
            }
        }

        void DrawRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = prev;
        }

        void OnDestroy()
        {
            if (_whiteTex != null) Destroy(_whiteTex);
        }
    }
}
