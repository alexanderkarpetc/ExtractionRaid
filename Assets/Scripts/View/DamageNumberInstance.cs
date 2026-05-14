using System.Collections;
using Dev;
using TMPro;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Per-popup MonoBehaviour для v2 damage-numbers (lives on DamageNumber prefab).
    /// Owns animation lifecycle + recycle via owning pool. Stateless between activations —
    /// <see cref="Activate"/> resets all internal animation state.
    ///
    /// Animation envelope (per tier configurable):
    ///   t = 0           spawn at world hit point + ±horizontal jitter
    ///   t = 0..spawnMs  ease-out scale 0 → 1
    ///   t = spawn..hold peak scale 1.0
    ///   t = hold..end   fade alpha + scale → endScale + drift up worldUnits
    /// Same-target consolidation: <see cref="MergeAdd"/> updates value text + restarts decay.
    /// </summary>
    public class DamageNumberInstance : MonoBehaviour
    {
        [SerializeField] TMP_Text _text;

        // Configuration captured at Activate() — used by Update tick.
        Vector3 _worldAnchor;
        Vector3 _spawnOffsetWorld;
        Vector3 _bulletDirHoriz;       // horizontal-only bullet direction (Y zeroed, normalized)
        DamageNumberTrajectory _kind;
        // Trajectory params (baked from section at Activate)
        float   _driftBiasFactor;
        float   _knockbackDistance;
        float   _knockbackUpRatio;
        float   _arcInitH;
        float   _arcInitUp;
        float   _arcGravity;
        float   _baseSize;
        float   _spawnMs;
        float   _holdMs;
        float   _decayMs;
        float   _driftWorldY;
        float   _endScale;
        float   _elapsedUnscaled;
        Camera  _camera;
        bool    _active;
        System.Action<DamageNumberInstance> _onComplete;

        /// <summary>Consolidation key — null = no consolidation, else <see cref="State.EId"/>.Value (target).</summary>
        public int ConsolidationKey { get; private set; } = -1;

        /// <summary>True while playing the animation. Pool checks this before reuse.</summary>
        public bool IsActive => _active;

        /// <summary>Numeric value for merge-add. <-1 = text-only (ricochet word).</summary>
        public float AccumulatedDamage { get; private set; } = -1f;

        public TMP_Text Text => _text;

        void Reset()
        {
            _text = GetComponentInChildren<TMP_Text>(true);
        }

        public void Activate(
            string text,
            Material materialPreset,
            Vector3 worldAnchor,
            Vector3 bulletDir,
            DamageNumberTrajectory kind,
            float driftBiasFactor,
            float knockbackDistance,
            float knockbackUpRatio,
            float arcInitH,
            float arcInitUp,
            float arcGravity,
            float baseSize,
            float spawnMs,
            float holdMs,
            float decayMs,
            float driftWorldY,
            float endScale,
            int consolidationKey,
            float numericDamage,
            Camera camera,
            System.Action<DamageNumberInstance> onComplete)
        {
            _worldAnchor      = worldAnchor;
            _spawnOffsetWorld = new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.05f, 0.05f));
            // Project bullet direction onto XZ plane — trajectories work у camera-relative top-down space.
            var horiz = bulletDir; horiz.y = 0f;
            _bulletDirHoriz   = horiz.sqrMagnitude > 0.001f ? horiz.normalized : Vector3.forward;
            _kind             = kind;
            _driftBiasFactor  = driftBiasFactor;
            _knockbackDistance = knockbackDistance;
            _knockbackUpRatio = knockbackUpRatio;
            _arcInitH         = arcInitH;
            _arcInitUp        = arcInitUp;
            _arcGravity       = arcGravity;
            _baseSize         = baseSize;
            _spawnMs          = Mathf.Max(0.001f, spawnMs);
            _holdMs           = Mathf.Max(0f, holdMs);
            _decayMs          = Mathf.Max(0.001f, decayMs);
            _driftWorldY      = driftWorldY;
            _endScale         = endScale;
            _elapsedUnscaled  = 0f;
            _camera           = camera;
            _onComplete       = onComplete;
            ConsolidationKey  = consolidationKey;
            AccumulatedDamage = numericDamage;

            if (_text != null)
            {
                _text.text = text;
                _text.fontSharedMaterial = materialPreset;
            }
            transform.localScale = Vector3.zero;
            _active = true;
            gameObject.SetActive(true);
        }

        /// <summary>Consolidation hook — adds extra damage to running total and restarts decay timer.</summary>
        public void MergeAdd(float extraDamage)
        {
            if (AccumulatedDamage < 0f) return; // word-only popups can't merge
            AccumulatedDamage += extraDamage;
            if (_text != null) _text.text = Mathf.RoundToInt(AccumulatedDamage).ToString();
            // Reset timeline back до spawn-completion so popup feels "alive" again без full re-pop.
            _elapsedUnscaled = Mathf.Min(_elapsedUnscaled, _spawnMs);
        }

        public void Deactivate()
        {
            _active = false;
            ConsolidationKey = -1;
            AccumulatedDamage = -1f;
            gameObject.SetActive(false);
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(this);
        }

        void Update()
        {
            if (!_active) return;
            _elapsedUnscaled += Time.unscaledDeltaTime * 1000f; // store у ms

            float totalLifetime = _spawnMs + _holdMs + _decayMs;
            if (_elapsedUnscaled >= totalLifetime)
            {
                Deactivate();
                return;
            }

            // ── Scale envelope
            float scale;
            if (_elapsedUnscaled < _spawnMs)
            {
                float t = _elapsedUnscaled / _spawnMs;
                // Ease-out cubic (snappy spawn pop)
                scale = 1f - Mathf.Pow(1f - t, 3f);
            }
            else if (_elapsedUnscaled < _spawnMs + _holdMs)
            {
                scale = 1f;
            }
            else
            {
                float t = (_elapsedUnscaled - _spawnMs - _holdMs) / _decayMs;
                scale = Mathf.Lerp(1f, _endScale, t);
            }
            transform.localScale = Vector3.one * scale * _baseSize;

            // ── Alpha envelope (fade тільки у decay)
            float alpha;
            if (_elapsedUnscaled < _spawnMs + _holdMs)
            {
                alpha = 1f;
            }
            else
            {
                float t = (_elapsedUnscaled - _spawnMs - _holdMs) / _decayMs;
                alpha = 1f - t;
            }
            if (_text != null)
            {
                var c = _text.color;
                c.a = alpha;
                _text.color = c;
            }

            // ── Position envelope — per-trajectory math.
            // _elapsedUnscaled is у ms; convert to seconds for physics (arc, knockback).
            float tSec    = _elapsedUnscaled * 0.001f;
            float overallT = _elapsedUnscaled / totalLifetime;
            var offset    = ComputeTrajectoryOffset(_kind, overallT, tSec);
            var pos       = _worldAnchor + _spawnOffsetWorld + offset;
            transform.position = pos;
            // Ensure billboard-facing camera для World Space Canvas
            if (_camera != null)
                transform.rotation = _camera.transform.rotation;
        }

        Vector3 ComputeTrajectoryOffset(DamageNumberTrajectory kind, float overallT, float tSec)
        {
            // overallT ∈ [0..1] over total popup lifetime — used by drift/bias modes (linear).
            // tSec — physics time (s) for ballistic arc.
            switch (kind)
            {
                case DamageNumberTrajectory.FloatUp:
                    return Vector3.up * (_driftWorldY * overallT);

                case DamageNumberTrajectory.FloatUpDrift:
                    // FloatUp + small bullet-dir bias (telegraph deflection / direction-sensitive feedback).
                    return Vector3.up * (_driftWorldY * overallT)
                         + _bulletDirHoriz * (_driftWorldY * _driftBiasFactor * overallT);

                case DamageNumberTrajectory.Knockback:
                    // Pure bullet-dir push з slight upward component.
                    return _bulletDirHoriz * (_knockbackDistance * overallT)
                         + Vector3.up * (_driftWorldY * _knockbackUpRatio * overallT);

                case DamageNumberTrajectory.ArcGravity:
                    // Ballistic: x(t) = vx·t, y(t) = vy·t - 0.5·g·t². Time у seconds.
                    return _bulletDirHoriz * (_arcInitH * tSec)
                         + Vector3.up * (_arcInitUp * tSec - 0.5f * _arcGravity * tSec * tSec);

                default:
                    return Vector3.up * (_driftWorldY * overallT);
            }
        }
    }
}
