using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>Feeds the FullOpaqueGrass five-point shader with the local player's trail.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class GrassInteractionTrail : MonoBehaviour
    {
        const int PointCount = 5;
        const float StrengthDepth = 0.75f;

        static readonly int[] TargetPositionIds =
        {
            Shader.PropertyToID("_TargetTurbulencePose1"),
            Shader.PropertyToID("_TargetTurbulencePose2"),
            Shader.PropertyToID("_TargetTurbulencePose3"),
            Shader.PropertyToID("_TargetTurbulencePose4"),
            Shader.PropertyToID("_TargetTurbulencePose5")
        };

        [Header("Full Opaque Grass Interaction")]
        [SerializeField, Min(0.05f)]
        [Tooltip("How many seconds the five-point trail remains behind the player.")]
        float _recoveryTime = 1.5f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("0 disables bending, 1 is the addon's default depth, 2 presses the grass more strongly.")]
        float _bendStrength = 1f;

        readonly List<PositionSample> _history = new List<PositionSample>(64);

        public float RecoveryTime
        {
            get => _recoveryTime;
            set => _recoveryTime = Mathf.Max(0.05f, value);
        }

        public float BendStrength
        {
            get => _bendStrength;
            set => _bendStrength = Mathf.Clamp(value, 0f, 2f);
        }

        void OnEnable()
        {
            _history.Clear();
            _history.Add(new PositionSample(Time.unscaledTime, transform.position));
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            _history.Add(new PositionSample(now, transform.position));
            RemoveExpiredSamples(now);

            if (_bendStrength <= 0.001f)
            {
                SetAllShaderPositions(new Vector3(0f, -10000f, 0f));
                return;
            }

            for (int i = 0; i < PointCount; i++)
            {
                float age = _recoveryTime * i / (PointCount - 1f);
                Vector3 position = FindPositionAt(now - age);

                // This addon bends blades toward its target. Raising the target weakens the
                // bend; lowering it makes blades lie flatter, without modifying vendor shaders.
                position.y += (1f - _bendStrength) * StrengthDepth;
                Shader.SetGlobalVector(TargetPositionIds[i], position);
            }
        }

        void OnDisable()
        {
            SetAllShaderPositions(new Vector3(0f, -10000f, 0f));
            _history.Clear();
        }

        void RemoveExpiredSamples(float now)
        {
            float oldestRequiredTime = now - _recoveryTime;
            int removeCount = 0;

            // Keep one sample before the window so interpolation remains continuous.
            while (removeCount + 1 < _history.Count
                   && _history[removeCount + 1].Time < oldestRequiredTime)
            {
                removeCount++;
            }

            if (removeCount > 0)
                _history.RemoveRange(0, removeCount);
        }

        Vector3 FindPositionAt(float targetTime)
        {
            if (_history.Count == 0)
                return transform.position;

            for (int i = 1; i < _history.Count; i++)
            {
                PositionSample next = _history[i];
                if (next.Time < targetTime)
                    continue;

                PositionSample previous = _history[i - 1];
                float duration = next.Time - previous.Time;
                float t = duration > 0.0001f
                    ? Mathf.Clamp01((targetTime - previous.Time) / duration)
                    : 1f;
                return Vector3.Lerp(previous.Position, next.Position, t);
            }

            return targetTime <= _history[0].Time
                ? _history[0].Position
                : _history[_history.Count - 1].Position;
        }

        static void SetAllShaderPositions(Vector3 position)
        {
            for (int i = 0; i < TargetPositionIds.Length; i++)
                Shader.SetGlobalVector(TargetPositionIds[i], position);
        }

        readonly struct PositionSample
        {
            public readonly float Time;
            public readonly Vector3 Position;

            public PositionSample(float time, Vector3 position)
            {
                Time = time;
                Position = position;
            }
        }
    }
}
