using System.Collections.Generic;
using Adapters;
using ApplicationCore;
using Dev;
using Session;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Floating damage numbers presenter (uGUI + TextMeshPro, World Space Canvas, pool).
    ///
    /// Pipeline:
    ///   RaidEvent.DamageNumber → ResolveTier → look up pool instance → either MERGE з existing
    ///   same-target popup (within consolidation window) OR spawn new з materialPreset for tier.
    ///
    /// Self-contained — runtime-creates own Canvas + pool on first use. Constructed у App ctor.
    /// </summary>
    public class DamageNumberPresenter
    {
        const string PrefabResourcePath = "Vfx/Prefabs/UI/DamageNumber";
        const string MaterialFolder     = "Vfx/Materials/DamageNumber_";

        enum Tier { Normal, Headshot, Kill, Bleed, Absorbed, Ricochet }

        readonly List<DamageNumberInstance> _pool = new();
        readonly Dictionary<int, ActiveSlot> _consolidationMap = new();

        struct ActiveSlot
        {
            public DamageNumberInstance Instance;
            public float LastUpdateUnscaled;
        }

        Canvas _canvas;
        GameObject _prefab;
        Material _matNormal, _matHeadshot, _matKill, _matBleed, _matAbsorbed, _matRicochet;
        Camera _camera;
        bool _resourcesLoaded;
        bool _disabled;

        public DamageNumberPresenter()
        {
            // Lazy load — Resources.Load called once.
        }

        void LoadResources()
        {
            if (_resourcesLoaded) return;
            _prefab = Resources.Load<GameObject>(PrefabResourcePath);
            _matNormal   = Resources.Load<Material>(MaterialFolder + "Normal");
            _matHeadshot = Resources.Load<Material>(MaterialFolder + "Headshot");
            _matKill     = Resources.Load<Material>(MaterialFolder + "Kill");
            _matBleed    = Resources.Load<Material>(MaterialFolder + "Bleed");
            _matAbsorbed = Resources.Load<Material>(MaterialFolder + "Absorbed");
            _matRicochet = Resources.Load<Material>(MaterialFolder + "Ricochet");

            if (_prefab == null)
            {
                Debug.LogWarning($"[DamageNumberPresenter] Prefab not found at Resources/{PrefabResourcePath}");
                _disabled = true;
            }
            _resourcesLoaded = true;
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            var canvasGo = new GameObject("[DamageNumberCanvas]");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            // Sort order high so it draws on top of in-world geometry. Adjust if depth/occlusion required.
            _canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;
            scaler.referencePixelsPerUnit = 100f;
            // Canvas RectTransform at origin — children position у world via DamageNumberInstance.
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1, 1);
            // World scale driven by ViewCheats; 0.03 default — readable at top-down distance.
            var cfg = ViewCheats.Config?.DamageNumberV2;
            float ws = cfg != null ? cfg.WorldScale : 0.03f;
            rt.localScale = Vector3.one * ws;
        }

        void UpdateCanvasScale()
        {
            if (_canvas == null) return;
            var cfg = ViewCheats.Config?.DamageNumberV2;
            if (cfg == null) return;
            float ws = cfg.WorldScale;
            var rt = _canvas.transform as RectTransform;
            if (rt != null && Mathf.Abs(rt.localScale.x - ws) > 0.0001f)
                rt.localScale = Vector3.one * ws;
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.DamageNumberV2;
            if (cfg == null || !cfg.Enabled) return;

            LoadResources();
            if (_disabled) return;

            if (_camera == null) _camera = Camera.main;

            EnsureCanvas();
            UpdateCanvasScale(); // pick up runtime WorldScale tunes
            EnsurePool(cfg.PoolSize);

            float nowUnscaled = Time.unscaledTime;

            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type != RaidEventType.DamageNumber) continue;
                ProcessEvent(e, cfg, nowUnscaled);
            }
        }

        void ProcessEvent(RaidEvent e, ViewCheatsDamageNumberSection cfg, float nowUnscaled)
        {
            bool isHeadshot = e.CurrentHp > 0.5f;
            bool isKill     = e.MaxHp     > 0.5f;
            float absorption = e.Id.Value / 1000f;
            int   flags    = e.KillerId.Value;
            bool isRicochet = (flags & 1) != 0;
            bool isBleed    = (flags & 2) != 0;

            var tier = ResolveTier(isHeadshot, isKill, isRicochet, isBleed, absorption);
            int targetKey = ResolveConsolidationKey(e, tier);

            // Try consolidation: same-target hit within window → MergeAdd to existing popup.
            if (targetKey != -1 && cfg.ConsolidationWindowMs > 0f
                && _consolidationMap.TryGetValue(targetKey, out var slot)
                && slot.Instance != null && slot.Instance.IsActive
                && (nowUnscaled - slot.LastUpdateUnscaled) * 1000f <= cfg.ConsolidationWindowMs)
            {
                slot.Instance.MergeAdd(e.Damage);
                slot.LastUpdateUnscaled = nowUnscaled;
                _consolidationMap[targetKey] = slot;
                return;
            }

            // Otherwise spawn fresh from pool.
            var inst = AcquireInstance();
            if (inst == null) return; // pool exhausted, skip

            ConfigureForTier(inst, tier, e, cfg, targetKey);

            if (targetKey != -1)
            {
                _consolidationMap[targetKey] = new ActiveSlot { Instance = inst, LastUpdateUnscaled = nowUnscaled };
            }
        }

        static Tier ResolveTier(bool isHeadshot, bool isKill, bool isRicochet, bool isBleed, float absorption)
        {
            if (isRicochet) return Tier.Ricochet;
            if (isBleed)    return Tier.Bleed;
            if (isKill)     return Tier.Kill;
            if (isHeadshot) return Tier.Headshot;
            if (absorption > 0.5f) return Tier.Absorbed;
            return Tier.Normal;
        }

        static int ResolveConsolidationKey(RaidEvent e, Tier tier)
        {
            // No consolidation for ricochet/bleed/kill (each event distinctive).
            // Normal/headshot/absorbed → key by hit world position (rounded) — proxy for "same target".
            // True target EId would be cleaner — DamageNumberSpawned didn't carry it; rounded pos is close-enough.
            if (tier == Tier.Ricochet || tier == Tier.Bleed || tier == Tier.Kill) return -1;
            int gx = Mathf.RoundToInt(e.Position.x * 2f);
            int gz = Mathf.RoundToInt(e.Position.z * 2f);
            return (gx * 73856093) ^ (gz * 19349663);
        }

        void ConfigureForTier(DamageNumberInstance inst, Tier tier, RaidEvent e,
            ViewCheatsDamageNumberSection cfg, int consolidationKey)
        {
            // Per-tier params
            Material mat;
            float sizeMul, holdMs, decayMs, totalMsOverride = -1f;
            string text;
            float numericDamage = e.Damage;

            switch (tier)
            {
                case Tier.Kill:
                    mat     = _matKill;
                    sizeMul = cfg.KillSize;
                    holdMs  = cfg.HoldMsKill;
                    decayMs = cfg.DecayMs;
                    // <line-height> compresses gap between digit baseline and sub-label baseline.
                    text    = $"<line-height={cfg.SubLabelLineHeight}%>{Mathf.RoundToInt(e.Damage)}\n<size={cfg.SubLabelSizePct}%>{cfg.KillLabel}</size>";
                    break;
                case Tier.Headshot:
                    mat     = _matHeadshot;
                    sizeMul = cfg.HeadshotSize;
                    holdMs  = cfg.HoldMs;
                    decayMs = cfg.DecayMs;
                    text    = string.IsNullOrEmpty(cfg.HeadshotLabel)
                              ? Mathf.RoundToInt(e.Damage).ToString()
                              : $"<line-height={cfg.SubLabelLineHeight}%>{Mathf.RoundToInt(e.Damage)}\n<size={cfg.SubLabelSizePct}%>{cfg.HeadshotLabel}</size>";
                    break;
                case Tier.Bleed:
                    mat     = _matBleed;
                    sizeMul = cfg.BleedSize;
                    holdMs  = cfg.HoldMs;
                    decayMs = cfg.DecayMs;
                    totalMsOverride = cfg.BleedTotalMs;
                    text    = Mathf.RoundToInt(e.Damage).ToString();
                    break;
                case Tier.Absorbed:
                    mat     = _matAbsorbed;
                    sizeMul = cfg.AbsorbedSize;
                    holdMs  = cfg.HoldMs;
                    decayMs = cfg.DecayMs;
                    text    = Mathf.RoundToInt(e.Damage).ToString();
                    break;
                case Tier.Ricochet:
                    mat     = _matRicochet;
                    sizeMul = cfg.RicochetSize;
                    holdMs  = 0f;
                    decayMs = cfg.RicochetTotalMs - cfg.SpawnMs;
                    totalMsOverride = cfg.RicochetTotalMs;
                    text    = cfg.RicochetLabel;
                    numericDamage = -1f; // text-only — never merges
                    break;
                default: // Normal
                    mat     = _matNormal;
                    sizeMul = cfg.NormalSize;
                    holdMs  = cfg.HoldMs;
                    decayMs = cfg.DecayMs;
                    text    = Mathf.RoundToInt(e.Damage).ToString();
                    break;
            }

            // If totalMsOverride is set, stretch decay component to hit total.
            if (totalMsOverride > 0f)
            {
                decayMs = Mathf.Max(0.001f, totalMsOverride - cfg.SpawnMs - holdMs);
            }

            // World base size: Canvas localScale is 0.01 → text at fontSize 36 reads about correctly.
            float baseSize = sizeMul * (cfg.FontSize / 36f);
            inst.transform.SetParent(_canvas.transform, false);
            // Lift anchor above character head — avoids body occlusion (TMP shader uses ZTest LEqual,
            // no shader-level override; positional offset is the reliable fix).
            var anchor = e.Position + Vector3.up * cfg.SpawnYOffset;
            var kind   = TrajectoryFor(tier, cfg);
            // bullet direction packed у event.Direction. Used by Knockback/ArcGravity/FloatUpDrift.
            inst.Activate(
                text: text,
                materialPreset: mat,
                worldAnchor: anchor,
                bulletDir: e.Direction,
                kind: kind,
                driftBiasFactor: cfg.DriftBiasFactor,
                knockbackDistance: cfg.KnockbackDistance,
                knockbackUpRatio: cfg.KnockbackUpRatio,
                arcInitH: cfg.ArcInitialHorizontal,
                arcInitUp: cfg.ArcInitialUp,
                arcGravity: cfg.ArcGravity,
                baseSize: baseSize,
                spawnMs: cfg.SpawnMs,
                holdMs: holdMs,
                decayMs: decayMs,
                driftWorldY: cfg.DriftUpWorld,
                endScale: cfg.EndScale,
                consolidationKey: consolidationKey,
                numericDamage: numericDamage,
                camera: _camera,
                onComplete: HandleInstanceComplete);
        }

        static DamageNumberTrajectory TrajectoryFor(Tier tier, ViewCheatsDamageNumberSection cfg) => tier switch
        {
            Tier.Headshot => cfg.HeadshotTrajectory,
            Tier.Kill     => cfg.KillTrajectory,
            Tier.Bleed    => cfg.BleedTrajectory,
            Tier.Absorbed => cfg.AbsorbedTrajectory,
            Tier.Ricochet => cfg.RicochetTrajectory,
            _             => cfg.NormalTrajectory,
        };

        DamageNumberInstance AcquireInstance()
        {
            // Look for inactive у pool.
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null && !_pool[i].IsActive) return _pool[i];

            // Pool exhausted: drop oldest by deactivating it.
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                {
                    _pool[i].Deactivate();
                    return _pool[i];
                }
            }
            return null;
        }

        void EnsurePool(int targetSize)
        {
            while (_pool.Count < targetSize)
            {
                var go = Object.Instantiate(_prefab, _canvas.transform);
                go.SetActive(false);
                var inst = go.GetComponent<DamageNumberInstance>();
                _pool.Add(inst);
            }
        }

        void HandleInstanceComplete(DamageNumberInstance inst)
        {
            // Clear any consolidation map entries pointing to this instance.
            int keyToRemove = -1;
            foreach (var kvp in _consolidationMap)
            {
                if (kvp.Value.Instance == inst) { keyToRemove = kvp.Key; break; }
            }
            if (keyToRemove != -1) _consolidationMap.Remove(keyToRemove);
        }

        public void Dispose()
        {
            foreach (var inst in _pool)
                if (inst != null) Object.Destroy(inst.gameObject);
            _pool.Clear();
            _consolidationMap.Clear();
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }
}
