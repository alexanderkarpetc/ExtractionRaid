using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Dissolves tagged renderers that stand between the camera and its follow target.
    /// Colliders stay enabled so the same object can be detected while dissolved.
    /// </summary>
    public class CameraObstacleHider : MonoBehaviour
    {
        const int HitBufferSize = 64;
        static readonly int DitherId = Shader.PropertyToID("Dither");
        static readonly int UnderscoreDitherId = Shader.PropertyToID("_Dither");

        [SerializeField] string _hideTag = "CameraHide";
        [SerializeField] Vector3 _targetOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] LayerMask _raycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Range(0f, 1f)] float _visibleDither = 0f;
        [SerializeField, Range(0f, 1f)] float _hiddenDither = 1f;
        [SerializeField, Min(0.01f)] float _fadeSpeed = 4f;

        readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
        readonly Dictionary<Transform, DitherState> _tracked = new();
        readonly HashSet<Transform> _blockedThisFrame = new();
        readonly List<Transform> _removeBuffer = new();
        MaterialPropertyBlock _propertyBlock;

        Transform _target;

        public void SetTarget(Transform target)
        {
            if (_target == target)
                return;

            ResetAll();
            _target = target;
        }

        public void Refresh()
        {
            if (_target == null)
            {
                ResetAll();
                return;
            }

            _blockedThisFrame.Clear();

            var from = transform.position;
            var to = _target.position + _targetOffset;
            var delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                ResetAll();
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(
                from,
                delta / distance,
                _hitBuffer,
                distance,
                _raycastMask,
                _triggerInteraction);

            for (int i = 0; i < hitCount; i++)
            {
                var hitTransform = _hitBuffer[i].transform;
                var hideRoot = FindTaggedRoot(hitTransform);
                if (hideRoot == null)
                    continue;

                _blockedThisFrame.Add(hideRoot);
                Track(hideRoot);
            }

            foreach (var kvp in _tracked)
            {
                kvp.Value.Target = _blockedThisFrame.Contains(kvp.Key)
                    ? _hiddenDither
                    : _visibleDither;
            }

            UpdateDither(Time.deltaTime);
        }

        Transform FindTaggedRoot(Transform start)
        {
            var current = start;
            while (current != null)
            {
                if (current.gameObject.tag == _hideTag)
                    return current;

                current = current.parent;
            }

            return null;
        }

        void Track(Transform root)
        {
            if (_tracked.ContainsKey(root))
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var state = new DitherState(renderers, _visibleDither, _hiddenDither);
            ApplyDither(state, state.Current);
            _tracked.Add(root, state);
        }

        void UpdateDither(float deltaTime)
        {
            float step = Mathf.Max(0.01f, _fadeSpeed) * deltaTime;

            _removeBuffer.Clear();
            foreach (var kvp in _tracked)
            {
                var state = kvp.Value;
                state.Current = Mathf.MoveTowards(state.Current, state.Target, step);
                ApplyDither(state, state.Current);

                if (kvp.Key == null ||
                    Mathf.Approximately(state.Target, _visibleDither) &&
                    Mathf.Approximately(state.Current, _visibleDither))
                {
                    _removeBuffer.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                var root = _removeBuffer[i];
                if (root != null && _tracked.TryGetValue(root, out var state))
                    ApplyDither(state, _visibleDither);

                _tracked.Remove(root);
            }
        }

        void ApplyDither(DitherState state, float dither)
        {
            if (state.Renderers == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < state.Renderers.Length; i++)
            {
                var renderer = state.Renderers[i];
                if (renderer == null)
                    continue;

                _propertyBlock.Clear();
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(DitherId, dither);
                _propertyBlock.SetFloat(UnderscoreDitherId, dither);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        void ResetAll()
        {
            _removeBuffer.Clear();
            foreach (var kvp in _tracked)
                _removeBuffer.Add(kvp.Key);

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                var root = _removeBuffer[i];
                if (root != null && _tracked.TryGetValue(root, out var state))
                    ApplyDither(state, _visibleDither);
            }

            _tracked.Clear();
        }

        void OnDisable()
        {
            ResetAll();
        }

        void OnDestroy()
        {
            ResetAll();
        }

        sealed class DitherState
        {
            public readonly Renderer[] Renderers;
            public float Current;
            public float Target;

            public DitherState(Renderer[] renderers, float current, float target)
            {
                Renderers = renderers;
                Current = current;
                Target = target;
            }
        }
    }
}
