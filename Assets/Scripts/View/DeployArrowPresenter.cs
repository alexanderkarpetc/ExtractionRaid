using ApplicationCore;
using Dev;
using Systems;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Screen-edge direction arrow to the NEAREST deploy point in the HIDEOUT, shown only
    /// while that exit is off-screen (when on-screen the world beacon speaks for itself).
    /// New-player wayfinding: where to leave the bunker and head out on a raid. Procedural
    /// (generated arrow sprite, no art), tunable via ViewCheats → 🧭 Deploy Marker.
    /// View-only MonoBehaviour on the App GO.
    /// </summary>
    public class DeployArrowPresenter : MonoBehaviour
    {
        static Sprite s_arrowSprite;

        Canvas _canvas;
        Image _arrow;
        RectTransform _arrowRt;

        void Update()
        {
            if (!EnsureUi()) return;

            if (!App.IsInitialized) { Hide(); return; }
            var app = App.Instance;
            var session = app.RaidSession;
            if (session == null || !app.IsInHideout
                || !QuestSystem.HasAcceptedAnyQuest(app.Player?.QuestProgress)) { Hide(); return; }

            var state = session.RaidState;
            var player = state?.PlayerEntity;
            if (player == null || state.DeployPoints.Count == 0) { Hide(); return; }

            // Arrow knobs live in the Quest Marker section (all on-screen marker guidance
            // grouped there).
            var cfg = ViewCheats.Config?.QuestMarker;
            if (cfg != null && !cfg.ArrowEnabled) { Hide(); return; }

            var cam = Camera.main;
            if (cam == null) { Hide(); return; }

            // Nearest deploy point (XZ distance).
            Vector3 target = default;
            float best = float.MaxValue;
            for (int i = 0; i < state.DeployPoints.Count; i++)
            {
                var p = state.DeployPoints[i].Position;
                float dx = p.x - player.Position.x, dz = p.z - player.Position.z;
                float d = dx * dx + dz * dz;
                if (d < best) { best = d; target = p; }
            }

            Vector3 sp = cam.WorldToScreenPoint(target + Vector3.up * 1.2f);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            float inset = cfg != null ? cfg.ArrowEdgeInsetPx : 64f;
            bool onScreen = !behind
                            && sp.x >= inset && sp.x <= Screen.width - inset
                            && sp.y >= inset && sp.y <= Screen.height - inset;
            if (onScreen) { Hide(); return; }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var dir = new Vector2(sp.x, sp.y) - center;
            if (dir.sqrMagnitude < 1e-3f) dir = Vector2.up;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            float x = Mathf.Clamp(sp.x, inset, Screen.width - inset);
            float y = Mathf.Clamp(sp.y, inset, Screen.height - inset);
            float size = cfg != null ? cfg.ArrowSizePx : 64f;
            // Subtle attention pulse while shown (±ArrowPulseAmount of size).
            if (cfg != null && cfg.ArrowPulseAmount > 0f)
                size *= 1f + cfg.ArrowPulseAmount * Mathf.Sin(Time.time * cfg.ArrowPulseHz * Mathf.PI * 2f);

            _arrowRt.sizeDelta = new Vector2(size, size);
            _arrowRt.position = new Vector3(x, y, 0f);
            _arrowRt.rotation = Quaternion.Euler(0f, 0f, angle - 90f); // sprite points up at 0°
            _arrow.color = cfg != null ? cfg.ArrowColor : new Color(0.35f, 0.92f, 0.55f, 1f);
            if (!_arrow.enabled) _arrow.enabled = true;
        }

        void Hide()
        {
            if (_arrow != null && _arrow.enabled) _arrow.enabled = false;
        }

        bool EnsureUi()
        {
            if (_canvas != null) return true;

            var canvasGo = new GameObject("DeployArrowCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // above HUD

            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(canvasGo.transform, false);
            _arrow = arrowGo.AddComponent<Image>();
            _arrow.raycastTarget = false;
            _arrow.sprite = ArrowSprite();
            _arrowRt = _arrow.rectTransform;
            _arrowRt.sizeDelta = new Vector2(64f, 64f);
            _arrow.enabled = false;
            return true;
        }

        // Solid triangle pointing up (+Y) with a thin dark outline. White fill so the
        // Image.color tint (cfg.Color) shows; outline tints to a dark shade of the color.
        static Sprite ArrowSprite()
        {
            if (s_arrowSprite != null) return s_arrowSprite;

            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, mipChain: false)
            {
                name = "DeployArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var fill    = new Color32(255, 255, 255, 255);
            var outline = new Color32(20, 30, 34, 235);
            var clear   = new Color32(0, 0, 0, 0);

            var apex  = new Vector2(0.50f, 0.10f);
            var left  = new Vector2(0.14f, 0.82f);
            var right = new Vector2(0.86f, 0.82f);

            var px = new Color32[S * S];
            for (int yy = 0; yy < S; yy++)
            for (int xx = 0; xx < S; xx++)
            {
                var p = new Vector2((xx + 0.5f) / S, 1f - (yy + 0.5f) / S);
                px[yy * S + xx] = PointInTriangle(p, apex, left, right) ? fill : clear;
            }

            var outlined = new Color32[S * S];
            for (int yy = 0; yy < S; yy++)
            for (int xx = 0; xx < S; xx++)
            {
                int idx = yy * S + xx;
                if (px[idx].a > 0) { outlined[idx] = fill; continue; }
                bool near =
                    (xx > 0     && px[idx - 1].a > 0) ||
                    (xx < S - 1 && px[idx + 1].a > 0) ||
                    (yy > 0     && px[idx - S].a > 0) ||
                    (yy < S - 1 && px[idx + S].a > 0);
                outlined[idx] = near ? outline : clear;
            }

            tex.SetPixels32(outlined);
            tex.Apply(false, false);
            s_arrowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return s_arrowSprite;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
