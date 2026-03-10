using State;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Hides the system cursor and draws two dots on screen:
    /// 1. Raw aim point (white) — instant mouse position on ground
    /// 2. Weapon aim point (green) — smoothed weapon tracking point
    /// </summary>
    public class AimCursorOverlay : MonoBehaviour
    {
        Texture2D _rawDotTex;
        Texture2D _weaponDotTex;

        const float RawDotSize = 6f;
        const float WeaponDotSize = 10f;

        void Awake()
        {
            _rawDotTex = MakeTex(new Color(1f, 1f, 1f, 0.6f));
            _weaponDotTex = MakeTex(new Color(0.2f, 1f, 0.3f, 0.9f));
        }

        void Update()
        {
            // Force-hide system cursor every frame during active gameplay.
            // Unity resets Cursor.visible when editor regains focus.
            var session = App.App.Instance?.RaidSession;
            bool inGameplay = session?.RaidState?.PlayerEntity != null;
            Cursor.visible = !inGameplay;
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var player = session.RaidState?.PlayerEntity;
            if (player == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Raw aim dot (white) — player intent
            DrawDot(cam, player.RawAimPoint, _rawDotTex, RawDotSize);

            // Weapon aim dot (green) — where weapon actually points
            DrawDot(cam, player.WeaponAimPoint, _weaponDotTex, WeaponDotSize);
        }

        void DrawDot(Camera cam, Vector3 worldPoint, Texture2D tex, float size)
        {
            var screenPos = cam.WorldToScreenPoint(worldPoint);

            // Behind camera check
            if (screenPos.z < 0f) return;

            // Unity GUI Y is flipped (0 = top)
            float guiY = Screen.height - screenPos.y;
            float half = size * 0.5f;

            var rect = new Rect(screenPos.x - half, guiY - half, size, size);
            GUI.DrawTexture(rect, tex);
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
            Cursor.visible = true;
            if (_rawDotTex != null) Destroy(_rawDotTex);
            if (_weaponDotTex != null) Destroy(_weaponDotTex);
        }
    }
}
