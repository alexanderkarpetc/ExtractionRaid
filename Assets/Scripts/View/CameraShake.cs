using UnityEngine;

namespace View
{
    /// <summary>
    /// Gunplay A.3 — additive position-only camera shake. Sits next to
    /// <see cref="RaidCameraController"/> on Main Camera and exposes a small API
    /// (Kick / Tremor) called by <see cref="CameraShakePresenter"/> on game events.
    ///
    /// <para>Two simultaneous channels:</para>
    /// <list type="bullet">
    /// <item><b>Kick</b> — directional impulse в одну сторону, eases out to zero.
    /// Used for fire recoil (push camera back along weapon direction).</item>
    /// <item><b>Tremor</b> — omnidirectional noise, eases out. Used for damage taken,
    /// fire jitter on top of kick, future explosions.</item>
    /// </list>
    ///
    /// <para>Output: <see cref="GetCurrentOffset"/> returns kick + tremor offset у
    /// world space; <see cref="RaidCameraController"/> adds it to camera position
    /// AFTER its main follow lerp. Position-only — no rotation shake (top-down camera
    /// holds fixed pitch; rotation would break aim alignment).</para>
    ///
    /// <para>All time tracking via <c>Time.unscaledDeltaTime</c> so shake remains crisp
    /// during hit pause (Time.timeScale slowdown).</para>
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        // ── Kick state (single active impulse — latest call replaces previous) ──
        Vector3 _kickDirection;
        float   _kickMagnitude;
        float   _kickElapsedUnscaled;
        float   _kickDuration;

        // ── Tremor state (single active accumulator) ─────────────────────────
        float _tremorMagnitude;
        float _tremorElapsedUnscaled;
        float _tremorDuration;
        float _tremorFrequency = 25f; // Hz, set by Tremor() caller

        // Per-axis perlin seeds — distinct so X/Z noise не correlated.
        readonly Vector2 _noiseSeedX = new(0.13f, 7.91f);
        readonly Vector2 _noiseSeedZ = new(5.27f, 2.84f);

        /// <summary>
        /// Apply a directional kick (impulse) along <paramref name="direction"/> for
        /// <paramref name="durationUnscaled"/> seconds. Latest call replaces previous —
        /// rapid fire produces overlapping kicks (re-set timer each shot).
        /// </summary>
        public void Kick(Vector3 direction, float magnitude, float durationUnscaled)
        {
            if (magnitude <= 0f || durationUnscaled <= 0f) return;
            direction.y = 0f; // top-down — no vertical kick
            if (direction.sqrMagnitude < 0.0001f) return;
            _kickDirection      = direction.normalized;
            _kickMagnitude      = magnitude;
            _kickDuration       = durationUnscaled;
            _kickElapsedUnscaled = 0f;
        }

        /// <summary>
        /// Apply omnidirectional tremor at given intensity (peak displacement) for
        /// <paramref name="durationUnscaled"/> seconds. Latest call replaces previous;
        /// peak is taken as <c>Max(currentRemaining, newMagnitude)</c> so multiple
        /// concurrent damage hits don't reduce shake mid-decay.
        /// </summary>
        public void Tremor(float magnitude, float durationUnscaled, float frequency)
        {
            if (magnitude <= 0f || durationUnscaled <= 0f) return;

            // Preserve in-progress tremor that is currently louder than the new request.
            float currentLoudness = ResolveTremorLoudness();
            float incoming        = magnitude;
            if (incoming >= currentLoudness)
            {
                _tremorMagnitude = incoming;
                _tremorDuration  = durationUnscaled;
                _tremorElapsedUnscaled = 0f;
            }
            // else: keep current tremor — quieter incoming would make shake feel weaker, undesired.

            if (frequency > 0f) _tremorFrequency = frequency;
        }

        /// <summary>
        /// Current world-space offset to add to camera position after follow lerp.
        /// Computed each frame — ease-out-quad decay on both kick and tremor.
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            // Advance unscaled time + decay both channels.
            float dt = Time.unscaledDeltaTime;

            Vector3 offset = Vector3.zero;

            if (_kickDuration > 0f)
            {
                _kickElapsedUnscaled += dt;
                float kt = _kickElapsedUnscaled / _kickDuration;
                if (kt >= 1f)
                {
                    _kickDuration = 0f;
                }
                else
                {
                    float kEased = (1f - kt) * (1f - kt); // ease-out quad
                    offset += _kickDirection * (_kickMagnitude * kEased);
                }
            }

            if (_tremorDuration > 0f)
            {
                _tremorElapsedUnscaled += dt;
                float tt = _tremorElapsedUnscaled / _tremorDuration;
                if (tt >= 1f)
                {
                    _tremorDuration = 0f;
                }
                else
                {
                    float tEased = (1f - tt) * (1f - tt);
                    float t      = Time.unscaledTime * _tremorFrequency;
                    // Perlin gives [0,1] — map to [-1,1] для bidirectional shake.
                    float nx = Mathf.PerlinNoise(_noiseSeedX.x + t, _noiseSeedX.y) * 2f - 1f;
                    float nz = Mathf.PerlinNoise(_noiseSeedZ.x + t, _noiseSeedZ.y) * 2f - 1f;
                    offset += new Vector3(nx, 0f, nz) * (_tremorMagnitude * tEased);
                }
            }

            return offset;
        }

        float ResolveTremorLoudness()
        {
            if (_tremorDuration <= 0f) return 0f;
            float tt = _tremorElapsedUnscaled / _tremorDuration;
            if (tt >= 1f) return 0f;
            float eased = (1f - tt) * (1f - tt);
            return _tremorMagnitude * eased;
        }
    }
}
