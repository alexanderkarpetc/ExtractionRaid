using ApplicationCore;
using Dev;
using State;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Worldspace radial stamina gauge — player only (NOT universal, unlike
    /// <see cref="WorldStatusIcons"/>). A donut ring offset to the side of the player
    /// (configurable) that "rubber-bands" toward the player via a critically-damped
    /// spring so it trails then catches up while sprinting.
    ///
    /// Procedural SDF (<c>StaminaRing.shader</c>): gray track + filled arc growing clockwise
    /// from 12 o'clock, fill color green→orange→red by ratio, blink while exhausted.
    /// Fade-out when stamina full (after a delay) keeps the HUD clean.
    ///
    /// NOT parented to the player (parenting = rigid follow, no spring). Lives top-level and
    /// SmoothDamps its world position toward (playerPos + offset). Self-destroys when its
    /// follow target is gone (player despawn), so PlayerView needs no teardown hook.
    /// </summary>
    public class WorldStaminaRing : MonoBehaviour
    {
        const string MaterialPath = "Vfx/Materials/StaminaRing";
        const string ShaderName   = "BattleHud/StaminaRing";

        static readonly int _FillRatioProp   = Shader.PropertyToID("_FillRatio");
        static readonly int _ThicknessProp   = Shader.PropertyToID("_Thickness");
        static readonly int _TrackColorProp  = Shader.PropertyToID("_TrackColor");
        static readonly int _ColorHighProp   = Shader.PropertyToID("_ColorHigh");
        static readonly int _ColorMidProp    = Shader.PropertyToID("_ColorMid");
        static readonly int _ColorLowProp     = Shader.PropertyToID("_ColorLow");
        static readonly int _FillIntensityProp = Shader.PropertyToID("_FillIntensity");
        static readonly int _OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        static readonly int _OutlineWidthProp = Shader.PropertyToID("_OutlineWidth");
        static readonly int _BlinkProp         = Shader.PropertyToID("_Blink");
        static readonly int _BlinkMinAlphaProp = Shader.PropertyToID("_BlinkMinAlpha");
        static readonly int _GlobalAlphaProp   = Shader.PropertyToID("_GlobalAlpha");

        Transform _followTarget;
        Canvas _canvas;
        RectTransform _rect;
        Material _material;

        // Per-frame state fed by PlayerView.SyncFromState.
        float _ratio = 1f;
        bool _isExhausted;

        // Spring + fade runtime state.
        Vector3 _springVel;
        bool _posInitialized;
        float _alpha;       // current fade alpha
        // Seconds stamina has been "full". Seeded large so the ring starts HIDDEN at level
        // start (full stamina from spawn) instead of fading in then hiding by timeout. Resets
        // to 0 the moment stamina depletes, so the "show topping-off after a refill" behavior
        // is preserved for in-game regen.
        float _fullTimer = 9999f;

        public static WorldStaminaRing Create(Transform followTarget)
        {
            var go = new GameObject("StaminaRing");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 102; // above WorldHealthBar (100) + WorldStatusIcons (101)

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1f, 1f);

            var imgGo = new GameObject("Fill");
            imgGo.transform.SetParent(go.transform, false);
            var imgRect = imgGo.AddComponent<RectTransform>();
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.offsetMin = Vector2.zero;
            imgRect.offsetMax = Vector2.zero;
            var img = imgGo.AddComponent<Image>();
            img.raycastTarget = false;

            var comp = go.AddComponent<WorldStaminaRing>();
            comp._followTarget = followTarget;
            comp._canvas = canvas;
            comp._rect = rect;

            // Per-instance material. NOTE: uGUI Image.material does NOT auto-instance (unlike
            // Renderer.material) — it returns the assigned reference as-is. So we must `new
            // Material(...)` an OWNED copy here, otherwise (a) per-frame SetColor would mutate the
            // shared Resources .mat at runtime, and (b) OnDestroy's Destroy() would hit the asset
            // ("Destroying assets is not permitted"). Resources asset preferred (build-safe);
            // shader fallback so the ring works before the .mat is authored.
            var shared = Resources.Load<Material>(MaterialPath);
            if (shared != null)
            {
                comp._material = new Material(shared);
                img.material = comp._material;
            }
            else
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    comp._material = new Material(shader);
                    img.material = comp._material;
                }
                else
                {
                    Debug.LogWarning($"[WorldStaminaRing] Shader '{ShaderName}' not found and no material at Resources/{MaterialPath}");
                }
            }

            comp._canvas.enabled = false;
            return comp;
        }

        /// <summary>Fed each frame by PlayerView. Ring consumes in LateUpdate.</summary>
        public void UpdateStamina(float ratio, bool isExhausted)
        {
            _ratio = Mathf.Clamp01(ratio);
            _isExhausted = isExhausted;
        }

        void LateUpdate()
        {
            // Self-cleanup when the player view is gone.
            if (_followTarget == null) { Destroy(gameObject); return; }

            var cfg = ViewCheats.Config?.BattleHud;
            bool enabled = cfg == null || (cfg.Enabled && cfg.StaminaRingEnabled);
            if (!enabled) { if (_canvas.enabled) _canvas.enabled = false; return; }

            float dt = Time.deltaTime;

            // ── Spring follow ────────────────────────────────────────────────
            Vector3 offset = cfg != null ? cfg.StaminaRingOffset : new Vector3(-0.65f, 0.05f, 0f);
            Vector3 target = _followTarget.position + offset;
            float springTime = cfg != null ? cfg.StaminaRingSpringTime : 0.13f;
            if (!_posInitialized)
            {
                transform.position = target;
                _posInitialized = true;
            }
            else if (springTime <= 0.0001f)
            {
                transform.position = target;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, target,
                    ref _springVel, springTime, Mathf.Infinity, dt);
            }

            // Billboard to camera.
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;

            // ── Size ─────────────────────────────────────────────────────────
            float worldSize = cfg != null ? cfg.StaminaRingWorldSize : 0.55f;
            _rect.sizeDelta = new Vector2(worldSize, worldSize);

            // ── Visibility / fade (hide-when-full after delay) ───────────────
            bool alwaysVisible = cfg != null && cfg.StaminaRingAlwaysVisible;
            float hideThreshold = cfg != null ? cfg.StaminaRingHideThreshold : 0.999f;
            float hideDelay = cfg != null ? cfg.StaminaRingHideDelay : 0.8f;
            float fadeTime = cfg != null ? cfg.StaminaRingFadeTime : 0.3f;

            bool isFull = _ratio >= hideThreshold && !_isExhausted;
            if (isFull) _fullTimer += dt; else _fullTimer = 0f;

            float targetAlpha = 1f;
            if (!alwaysVisible && isFull && _fullTimer >= hideDelay)
                targetAlpha = 0f;

            float fadeStep = fadeTime > 0.0001f ? dt / fadeTime : 1f;
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeStep);

            if (_alpha <= 0.001f)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }
            if (!_canvas.enabled) _canvas.enabled = true;

            // ── Material push ────────────────────────────────────────────────
            if (_material != null)
            {
                _material.SetFloat(_FillRatioProp, _ratio);
                _material.SetFloat(_ThicknessProp, cfg != null ? cfg.StaminaRingThickness : 0.11f);
                _material.SetColor(_TrackColorProp, cfg != null ? cfg.StaminaRingTrackColor : new Color(0.22f, 0.22f, 0.24f, 0.65f));
                _material.SetColor(_ColorHighProp, cfg != null ? cfg.StaminaRingColorHigh : new Color(0.30f, 0.85f, 0.35f, 1f));
                _material.SetColor(_ColorMidProp,  cfg != null ? cfg.StaminaRingColorMid  : new Color(0.95f, 0.60f, 0.15f, 1f));
                _material.SetColor(_ColorLowProp,  cfg != null ? cfg.StaminaRingColorLow  : new Color(1.00f, 0.18f, 0.14f, 1f));
                _material.SetFloat(_FillIntensityProp, cfg != null ? cfg.StaminaRingFillIntensity : 1.5f);
                _material.SetColor(_OutlineColorProp, cfg != null ? cfg.StaminaRingOutlineColor : new Color(0.02f, 0.02f, 0.03f, 0.95f));
                _material.SetFloat(_OutlineWidthProp, cfg != null ? cfg.StaminaRingOutlineWidth : 0.028f);
                _material.SetFloat(_BlinkMinAlphaProp, cfg != null ? cfg.StaminaRingBlinkMinAlpha : 0.25f);

                // Blink only while exhausted — sine pulse at configured frequency.
                float blink = 0f;
                if (_isExhausted)
                {
                    float freq = cfg != null ? cfg.StaminaRingBlinkFrequency : 3f;
                    blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * freq);
                }
                _material.SetFloat(_BlinkProp, blink);
                _material.SetFloat(_GlobalAlphaProp, _alpha);
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
