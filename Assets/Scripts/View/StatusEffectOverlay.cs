using State;
using Systems;
using UnityEngine;

namespace View
{
    public class StatusEffectOverlay : MonoBehaviour
    {
        Texture2D _bleedBgTex;
        GUIStyle _effectStyle;

        void Awake()
        {
            _bleedBgTex = MakeTex(new Color(0.6f, 0f, 0f, 0.85f));
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var state = session.RaidState;
            var player = state?.PlayerEntity;
            if (player == null) return;

            if (!state.StatusEffects.TryGetValue(player.Id, out var effects))
                return;
            if (effects.Count == 0) return;

            EnsureStyles();

            const float boxW = 180f;
            const float boxH = 40f;
            const float gap = 6f;
            const float marginX = 16f;
            const float marginY = 16f;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                float y = marginY + i * (boxH + gap);
                var rect = new Rect(marginX, y, boxW, boxH);

                DrawEffect(rect, effect, state.ElapsedTime);
            }
        }

        void DrawEffect(Rect rect, StatusEffectInstance effect, float elapsedTime)
        {
            switch (effect.Type)
            {
                case StatusEffectType.Bleeding:
                    DrawBleed(rect, elapsedTime);
                    break;
                default:
                    _effectStyle.normal.background = _bleedBgTex;
                    GUI.Box(rect, effect.Type.ToString(), _effectStyle);
                    break;
            }
        }

        void DrawBleed(Rect rect, float elapsedTime)
        {
            float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(elapsedTime * 3f));
            GUI.color = new Color(1f, pulse * 0.3f, pulse * 0.3f, 0.9f);
            GUI.DrawTexture(rect, _bleedBgTex);
            GUI.color = Color.white;

            var labelRect = new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height);
            GUI.Label(labelRect, "BLEEDING", _effectStyle);
        }

        void EnsureStyles()
        {
            if (_effectStyle != null) return;

            _effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _effectStyle.normal.textColor = Color.white;
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
            if (_bleedBgTex != null) Destroy(_bleedBgTex);
        }
    }
}
