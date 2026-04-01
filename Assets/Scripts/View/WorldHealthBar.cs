using Dev;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Dota 2-style health bar: fill + damage trail + white flash + HP segments.
    /// Flash expands beyond bar bounds (up/down/left/right).
    /// Uses a custom shader (UI/HealthBarFill) for per-pixel effects.
    /// All animation params tweakable via DevCheats → Health Bar section.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        // Padding ratio — how much extra space around the bar for flash expansion.
        // Image rect is larger than visual bar; shader handles the mapping.
        const float PaddingX = 0.02f;  // UV fraction
        const float PaddingY = 0.25f;  // UV fraction (generous for vertical flash)

        // 3-stop gradient: red (<30%) → yellow (30–70%) → green (>70%)
        static readonly Color ColorLow = new(0.8f, 0.2f, 0.15f, 1f);
        static readonly Color ColorMid = new(1f, 0.8f, 0f, 1f);
        static readonly Color ColorHigh = new(0.2f, 0.85f, 0.2f, 1f);
        const float StopLow = 0.3f;  // below this: red
        const float StopHigh = 0.7f; // above this: green

        // Shader property IDs (cached)
        static readonly int PropFill = Shader.PropertyToID("_Fill");
        static readonly int PropTrailFill = Shader.PropertyToID("_TrailFill");
        static readonly int PropFlashT = Shader.PropertyToID("_FlashT");
        static readonly int PropSegmentCount = Shader.PropertyToID("_SegmentCount");
        static readonly int PropBarColor = Shader.PropertyToID("_BarColor");
        static readonly int PropTrailColor = Shader.PropertyToID("_TrailColor");
        static readonly int PropFlashColor = Shader.PropertyToID("_FlashColor");
        static readonly int PropBgColor = Shader.PropertyToID("_BgColor");
        static readonly int PropFlashExpandX = Shader.PropertyToID("_FlashExpandX");
        static readonly int PropFlashExpandY = Shader.PropertyToID("_FlashExpandY");
        static readonly int PropFlashPower = Shader.PropertyToID("_FlashPower");
        static readonly int PropBorderSize = Shader.PropertyToID("_BorderSize");
        static readonly int PropSegmentLineColor = Shader.PropertyToID("_SegmentLineColor");
        static readonly int PropSegmentLineWidth = Shader.PropertyToID("_SegmentLineWidth");
        static readonly int PropPaddingX = Shader.PropertyToID("_PaddingX");
        static readonly int PropPaddingY = Shader.PropertyToID("_PaddingY");

        Image _image;
        Material _material;
        CanvasGroup _canvasGroup;
        RectTransform _canvasRect;
        RectTransform _fillRect;

        float _fill = 1f;
        float _trailFill = 1f;
        float _flashTimer = 1f; // 1 = flash finished
        float _trailDelayClock;
        float _maxHp;

        // Shake state
        float _shakeTimer;      // counts up from 0; shake active while < duration
        float _shakeMagnitude;  // base magnitude for current shake (proportional to damage)

        // Armor bar (thin stripe above health bar)
        GameObject _armorBarGo;
        RectTransform _helmetFillRect;
        RectTransform _vestFillRect;
        Image _helmetFillImage;
        Image _vestFillImage;
        Image _armorBgImage;

        public static WorldHealthBar Create(Transform parent, float maxHp)
        {
            float w = DevCheats.HBarWidth;
            float h = DevCheats.HBarHeight;
            float offsetY = DevCheats.HBarOffsetY;

            var go = new GameObject("HealthBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, offsetY, 0f);

            // Canvas (world-space)
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            // Canvas size = visual bar size
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;

            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // Fill Image — larger than canvas to allow flash expansion
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();

            // Expand rect beyond parent bounds by padding
            float extraX = w * PaddingX / (1f - 2f * PaddingX);
            float extraY = h * PaddingY / (1f - 2f * PaddingY);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(-extraX, -extraY);
            fillRect.offsetMax = new Vector2(extraX, extraY);

            var image = fillGo.AddComponent<Image>();

            // Create per-instance material from shader
            var shader = Shader.Find("UI/HealthBarFill");
            var mat = new Material(shader);
            mat.SetFloat(PropPaddingX, PaddingX);
            mat.SetFloat(PropPaddingY, PaddingY);
            image.material = mat;

            var bar = go.AddComponent<WorldHealthBar>();
            bar._image = image;
            bar._material = mat;
            bar._canvasGroup = canvasGroup;
            bar._canvasRect = rt;
            bar._fillRect = fillRect;
            bar._maxHp = maxHp;

            // Initialize shader with full-HP defaults so there's no visual pop on first damage
            mat.SetColor(PropBarColor, ColorHigh);
            mat.SetFloat(PropFill, 1f);
            mat.SetFloat(PropTrailFill, 1f);
            mat.SetFloat(PropFlashT, 1f);

            // Armor bar — thin stripe above health bar (hidden by default)
            bar.CreateArmorBar(go.transform, w, h);

            return bar;
        }

        void CreateArmorBar(Transform parent, float barWidth, float barHeight)
        {
            float armorH = barHeight * 0.35f;
            float gap = barHeight * 0.12f;

            _armorBarGo = new GameObject("ArmorBar");
            _armorBarGo.transform.SetParent(parent, false);
            var armorRect = _armorBarGo.AddComponent<RectTransform>();
            armorRect.anchorMin = new Vector2(0f, 1f);
            armorRect.anchorMax = new Vector2(1f, 1f);
            armorRect.pivot = new Vector2(0.5f, 0f);
            armorRect.anchoredPosition = new Vector2(0f, gap);
            armorRect.sizeDelta = new Vector2(0f, armorH);

            // Background
            var bgGo = new GameObject("ArmorBg");
            bgGo.transform.SetParent(_armorBarGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            _armorBgImage = bgGo.AddComponent<Image>();
            _armorBgImage.color = new Color(0.12f, 0.12f, 0.15f, 0.7f);

            // Helmet fill (left half) — uses anchor stretching for fill
            var helmetGo = new GameObject("HelmetFill");
            helmetGo.transform.SetParent(_armorBarGo.transform, false);
            _helmetFillRect = helmetGo.AddComponent<RectTransform>();
            _helmetFillRect.anchorMin = new Vector2(0f, 0f);
            _helmetFillRect.anchorMax = new Vector2(0f, 1f); // width = 0 initially
            _helmetFillRect.offsetMin = Vector2.zero;
            _helmetFillRect.offsetMax = Vector2.zero;
            _helmetFillImage = helmetGo.AddComponent<Image>();
            _helmetFillImage.color = new Color(0.3f, 0.7f, 1f, 0.9f);

            // 1px gap divider
            var divGo = new GameObject("Divider");
            divGo.transform.SetParent(_armorBarGo.transform, false);
            var divRect = divGo.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.498f, 0f);
            divRect.anchorMax = new Vector2(0.502f, 1f);
            divRect.offsetMin = Vector2.zero;
            divRect.offsetMax = Vector2.zero;
            var divImg = divGo.AddComponent<Image>();
            divImg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);

            // Vest fill (right half)
            var vestGo = new GameObject("VestFill");
            vestGo.transform.SetParent(_armorBarGo.transform, false);
            _vestFillRect = vestGo.AddComponent<RectTransform>();
            _vestFillRect.anchorMin = new Vector2(0.5f, 0f);
            _vestFillRect.anchorMax = new Vector2(0.5f, 1f); // width = 0 initially
            _vestFillRect.offsetMin = Vector2.zero;
            _vestFillRect.offsetMax = Vector2.zero;
            _vestFillImage = vestGo.AddComponent<Image>();
            _vestFillImage.color = new Color(0.3f, 0.7f, 1f, 0.9f);

            _armorBarGo.SetActive(false);
        }

        public void UpdateArmor(float helmetDurPercent, float vestDurPercent)
        {
            if (_armorBarGo == null) return;

            bool hasArmor = helmetDurPercent > 0f || vestDurPercent > 0f;
            _armorBarGo.SetActive(hasArmor);

            if (!hasArmor) return;

            // Helmet: stretch from left (anchor 0) to fill% of left half (0.5)
            if (_helmetFillRect != null)
            {
                _helmetFillRect.anchorMin = new Vector2(0f, 0f);
                _helmetFillRect.anchorMax = new Vector2(0.5f * helmetDurPercent, 1f);
            }

            // Vest: stretch from center (0.5) to fill% of right half
            if (_vestFillRect != null)
            {
                _vestFillRect.anchorMin = new Vector2(0.5f, 0f);
                _vestFillRect.anchorMax = new Vector2(0.5f + 0.5f * vestDurPercent, 1f);
            }
        }

        public void UpdateHealth(float current, float max)
        {
            float newFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _maxHp = max;

            // Detect damage: current fill decreased
            if (newFill < _fill - 0.001f)
            {
                _trailFill = Mathf.Max(_trailFill, _fill);
                _flashTimer = 0f;
                _trailDelayClock = 0f;

                // Shake proportional to damage ratio
                float damageRatio = _fill - newFill; // 0..1
                _shakeMagnitude = Mathf.Clamp01(damageRatio * 4f); // 25% HP hit = full shake
                _shakeTimer = 0f;
            }

            // Detect heal: snap trail forward
            if (newFill > _fill + 0.001f)
            {
                _trailFill = newFill;
                _flashTimer = 1f;
            }

            _fill = newFill;
            _canvasGroup.alpha = 1f;

            // 3-stop color gradient: green >70%, yellow 70–30%, red <30%
            // Pure green stays until StopHigh, then transitions
            Color barColor;
            if (_fill >= StopHigh)
                barColor = ColorHigh;
            else if (_fill > StopLow)
                barColor = Color.Lerp(ColorLow, ColorMid, (_fill - StopLow) / (StopHigh - StopLow));
            else
                barColor = ColorLow;

            _material.SetColor(PropBarColor, barColor);
        }

        void LateUpdate()
        {
            // Billboard
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            // Dynamic layout from DevCheats (real-time tweaking in editor)
            float w = DevCheats.HBarWidth;
            float h = DevCheats.HBarHeight;
            float baseY = DevCheats.HBarOffsetY;

            // Shake offset
            float shakeX = 0f, shakeY = 0f;
            float shakeDur = DevCheats.HBarShakeDuration;
            if (_shakeTimer < shakeDur)
            {
                _shakeTimer += Time.deltaTime;
                float decay = 1f - Mathf.Clamp01(_shakeTimer / shakeDur);
                float amplitude = _shakeMagnitude * DevCheats.HBarShakeIntensity * decay;
                float freq = DevCheats.HBarShakeFrequency;
                float t = _shakeTimer * freq;
                shakeX = Mathf.Sin(t * 6.2831853f) * amplitude;
                shakeY = Mathf.Cos(t * 4.1887902f) * amplitude; // different freq for Y
            }

            transform.localPosition = new Vector3(shakeX, baseY + shakeY, 0f);
            _canvasRect.sizeDelta = new Vector2(w, h);

            float extraX = w * PaddingX / (1f - 2f * PaddingX);
            float extraY = h * PaddingY / (1f - 2f * PaddingY);
            _fillRect.offsetMin = new Vector2(-extraX, -extraY);
            _fillRect.offsetMax = new Vector2(extraX, extraY);

            float dt = Time.deltaTime;

            // Flash phase advance
            float flashDur = Mathf.Max(DevCheats.HBarFlashDuration, 0.01f);
            if (_flashTimer < 1f)
                _flashTimer = Mathf.Clamp01(_flashTimer + dt / flashDur);

            // Trail delay → then lerp to fill
            _trailDelayClock += dt;
            if (_trailDelayClock > DevCheats.HBarTrailDelay && _trailFill > _fill)
                _trailFill = Mathf.MoveTowards(_trailFill, _fill, DevCheats.HBarTrailSpeed * dt);

            // Push uniforms — animation
            _material.SetFloat(PropFill, _fill);
            _material.SetFloat(PropTrailFill, _trailFill);
            _material.SetFloat(PropFlashT, _flashTimer);
            _material.SetFloat(PropFlashExpandX, DevCheats.HBarFlashExpandX);
            _material.SetFloat(PropFlashExpandY, DevCheats.HBarFlashExpandY);
            _material.SetFloat(PropFlashPower, Mathf.Max(DevCheats.HBarFlashPower, 0.1f));
            _material.SetFloat(PropBorderSize, DevCheats.HBarBorderSize);

            // Push uniforms — segments (always up-to-date, even before first damage)
            float hpPerSeg = DevCheats.HBarHpPerSegment;
            float segments = hpPerSeg > 0f ? _maxHp / hpPerSeg : 1f;
            _material.SetFloat(PropSegmentCount, segments);
            _material.SetFloat(PropSegmentLineWidth, DevCheats.HBarSegmentLineWidth);
            _material.SetColor(PropSegmentLineColor, DevCheats.HBarSegmentLineColor);

            // Push uniforms — colors
            _material.SetColor(PropTrailColor, DevCheats.HBarTrailColor);
            _material.SetColor(PropFlashColor, DevCheats.HBarFlashColor);
            _material.SetColor(PropBgColor, DevCheats.HBarBgColor);
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
