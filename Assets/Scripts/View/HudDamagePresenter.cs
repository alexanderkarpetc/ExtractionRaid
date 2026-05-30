using Adapters;
using ApplicationCore;
using Dev;
using Session;
using State;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// HUD damage feedback — directional vignette pulse on hit + low-HP edge glow.
    /// One full-screen RawImage on a Screen-Space Overlay canvas, driven via Image.material
    /// per-frame writes (no MaterialPropertyBlock — UI elements don't support it).
    ///
    /// Pipeline:
    ///   - Consume <c>EntityHit</c> events targeted at the player → resolve sector angle
    ///     from <c>projectileDirection</c> via camera-local projection.
    ///   - Allocate hit slot (round-robin among 4) with intensity by hit kind
    ///     (Normal/Kill/Headshot/Ricochet — same priority order as crosshair hit pulse).
    ///   - Fade each slot out over <c>PulseDuration</c> seconds (quadratic ease-out).
    ///   - Low-HP layer: sine-modulated heartbeat when HP ratio ≤ threshold. Non-directional.
    ///   - Push everything to shader each frame.
    ///
    /// Lives as plain class in App; LateTick driven from <c>App.LateTick</c>. Same lifecycle
    /// pattern as <see cref="CrosshairPresenter"/>.
    /// </summary>
    public class HudDamagePresenter
    {
        const string PrefabPath = "Vfx/Prefabs/UI/HudDamageDirectional";
        const int MaxSlots = 4;

        // Shader prop IDs (cached)
        static readonly int _BaseColor          = Shader.PropertyToID("_BaseColor");
        static readonly int _InnerRadius        = Shader.PropertyToID("_InnerRadius");
        static readonly int _EdgeSoftness       = Shader.PropertyToID("_EdgeSoftness");
        static readonly int _SectorHalfWidthRad = Shader.PropertyToID("_SectorHalfWidthRad");
        static readonly int _AspectRatio        = Shader.PropertyToID("_AspectRatio");
        static readonly int _LowHpGlow          = Shader.PropertyToID("_LowHpGlow");
        static readonly int[] _HitSlotProps =
        {
            Shader.PropertyToID("_HitSlot0"),
            Shader.PropertyToID("_HitSlot1"),
            Shader.PropertyToID("_HitSlot2"),
            Shader.PropertyToID("_HitSlot3"),
        };

        struct HitSlot
        {
            public float StartTime;   // unscaledTime when allocated
            public float Duration;    // seconds — snapshotted at trigger (immune to mid-pulse cfg tweaks)
            public float Angle;       // screen-space radians [0..2PI]
            public float Intensity;   // peak intensity 0..1
            public bool  Active;
        }

        GameObject _prefab;
        Canvas _canvas;
        RawImage _overlay;
        Material _mat;
        bool _resourcesLoaded;
        bool _disabled;

        readonly HitSlot[] _slots = new HitSlot[MaxSlots];
        int _nextSlot;

        public HudDamagePresenter() { /* lazy init */ }

        void LoadResources()
        {
            if (_resourcesLoaded) return;
            _resourcesLoaded = true;
            _prefab = Resources.Load<GameObject>(PrefabPath);
            if (_prefab == null)
            {
                Debug.LogWarning($"[HudDamagePresenter] Prefab missing at Resources/{PrefabPath}");
                _disabled = true;
            }
        }

        void EnsureScene()
        {
            if (_canvas != null) return;
            if (_prefab == null) return;
            var go = Object.Instantiate(_prefab);
            go.name = "[HudDamage]";
            _canvas = go.GetComponentInChildren<Canvas>(true);
            _overlay = go.GetComponentInChildren<RawImage>(true);
            // Real material instance — uGUI Graphic.material does NOT auto-instance (returns the
            // assigned reference as-is), so per-frame SetColor/SetVector would otherwise mutate the
            // shared Resources material asset (persists on play-mode exit → git churn).
            if (_overlay != null && _overlay.material != null)
            {
                _mat = new Material(_overlay.material);
                _overlay.material = _mat;
            }
            _overlay.raycastTarget = false;
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.HudDamage;
            if (cfg == null || !cfg.Enabled)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                return;
            }

            LoadResources();
            if (_disabled) return;
            EnsureScene();
            if (_canvas == null || _mat == null) return;
            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

            var state = session.RaidState;
            var player = state?.PlayerEntity;
            if (player == null) return;

            // Consume EntityHit events targeting the player.
            var playerId = player.Id;
            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.EntityHit) continue;
                if (e.Id != playerId) continue;
                TriggerHit(e, cfg);
            }

            UpdateSlotsAndPush(state, player, cfg);
        }

        // Resolve sector angle from projectileDirection (which points FROM shooter TO player).
        // Negate to get player→shooter, then project into camera-local space and take XY angle.
        // Camera-local space avoids assumptions about world axes vs screen orientation.
        void TriggerHit(RaidEvent e, ViewCheatsHudDamageSection cfg)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var projectileDir = e.Direction;
            if (projectileDir.sqrMagnitude < 0.0001f) return;
            var dirToShooter = -projectileDir;
            var camLocal = cam.transform.InverseTransformDirection(dirToShooter);
            float angle = Mathf.Atan2(camLocal.y, camLocal.x);
            if (angle < 0f) angle += Mathf.PI * 2f;

            // Hit kind → intensity tier (priority Ricochet > Kill > Headshot > Normal — matches crosshair).
            bool isHeadshot = e.CurrentHp > 0.5f;
            bool isKill     = e.MaxHp     > 0.5f;
            bool isRicochet = e.KillerId.Value > 0;
            float intensity;
            if      (isRicochet) intensity = cfg.RicochetHitIntensity;
            else if (isKill)     intensity = cfg.KillHitIntensity;
            else if (isHeadshot) intensity = cfg.HeadshotHitIntensity;
            else                 intensity = cfg.NormalHitIntensity;

            _slots[_nextSlot] = new HitSlot
            {
                StartTime = Time.unscaledTime,
                Duration  = cfg.PulseDuration,
                Angle     = angle,
                Intensity = intensity,
                Active    = true,
            };
            _nextSlot = (_nextSlot + 1) % MaxSlots;
        }

        void UpdateSlotsAndPush(RaidState state, PlayerEntityState player, ViewCheatsHudDamageSection cfg)
        {
            float now = Time.unscaledTime;

            // Push static / cfg-derived props each frame so DevCheats edits land live.
            _mat.SetColor(_BaseColor, cfg.BaseColor);
            _mat.SetFloat(_InnerRadius, cfg.InnerRadius);
            _mat.SetFloat(_EdgeSoftness, cfg.EdgeSoftness);
            _mat.SetFloat(_SectorHalfWidthRad, cfg.SectorHalfWidthDeg * Mathf.Deg2Rad);
            _mat.SetFloat(_AspectRatio, (float)Screen.width / Mathf.Max(1, Screen.height));

            // Tick slots — quadratic ease-out fade. Push each slot's (angle, fadedIntensity) to its shader prop.
            for (int i = 0; i < MaxSlots; i++)
            {
                var s = _slots[i];
                float fadedIntensity = 0f;
                float angle = 0f;
                if (s.Active)
                {
                    float t = (now - s.StartTime) / Mathf.Max(0.001f, s.Duration);
                    if (t >= 1f)
                    {
                        s.Active = false;
                        _slots[i] = s;
                    }
                    else
                    {
                        float fade = 1f - t * t;
                        fadedIntensity = s.Intensity * fade;
                        angle = s.Angle;
                    }
                }
                _mat.SetVector(_HitSlotProps[i], new Vector4(angle, fadedIntensity, 0f, 0f));
            }

            // Low-HP heartbeat layer. Driven by player HP ratio if Health entry exists.
            float lowHp = 0f;
            if (cfg.LowHpGlowEnabled
                && state.HealthMap.TryGetValue(player.Id, out var hp)
                && hp.MaxHp > 0f
                && hp.IsAlive)
            {
                float hpRatio = hp.CurrentHp / hp.MaxHp;
                if (hpRatio <= cfg.LowHpThresholdRatio)
                {
                    float deficit = 1f - (hpRatio / Mathf.Max(0.001f, cfg.LowHpThresholdRatio));
                    float baseI = Mathf.Lerp(cfg.LowHpMinIntensity, cfg.LowHpMaxIntensity, Mathf.Clamp01(deficit));
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * cfg.LowHpPulseFreqHz * Mathf.PI * 2f);
                    lowHp = baseI * pulse;
                }
            }
            _mat.SetFloat(_LowHpGlow, lowHp);
        }

        public void Dispose()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }
}
