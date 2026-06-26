using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace View
{
    /// <summary>
    /// Dissolves tagged renderers that stand between the camera and its follow target.
    /// Colliders stay enabled so the same object can be detected while dissolved.
    /// </summary>
    public class CameraObstacleHider : MonoBehaviour
    {
        const int HitBufferSize = 64;
        static readonly int FoliageDitherPositionId = Shader.PropertyToID("_PlayerFoliageDitherPosition");
        static readonly int FoliageDitherParamsId = Shader.PropertyToID("_PlayerFoliageDitherParams");
        static readonly int CursorDitherPositionId = Shader.PropertyToID("_CursorFoliageDitherPosition");
        static readonly int CursorDitherParamsId = Shader.PropertyToID("_CursorFoliageDitherParams");

        [SerializeField] string _hideTag = "CameraHide";
        [SerializeField] Vector3 _targetOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] LayerMask _raycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] CameraObstacleHiderSettings _settings;

        readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
        readonly Dictionary<Transform, DitherState> _tracked = new();
        readonly HashSet<Transform> _blockedThisFrame = new();
        readonly List<Transform> _removeBuffer = new();
        MaterialPropertyBlock _propertyBlock;

        Transform _target;

        CameraObstacleHiderSettings Settings
        {
            get
            {
                if (_settings == null)
                    _settings = Resources.Load<CameraObstacleHiderSettings>(CameraObstacleHiderSettings.ResourcePath);

                return _settings;
            }
        }

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
                ResetGlobalFoliageDither();
                return;
            }

            UpdateGlobalFoliageDither();
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

            var settings = Settings;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var state = new DitherState(renderers, VisibleDither(settings));
            state.SetTarget(HiddenDither(settings), DissolveDuration(settings));
            ApplyDither(state, state.Current);
            _tracked.Add(root, state);
        }

        void UpdateDither(float deltaTime)
        {
            _removeBuffer.Clear();
            foreach (var kvp in _tracked)
            {
                var state = kvp.Value;
                var settings = Settings;
                float visibleDither = VisibleDither(settings);
                float hiddenDither = HiddenDither(settings);
                float desiredTarget = _blockedThisFrame.Contains(kvp.Key) ? hiddenDither : visibleDither;
                float duration = Mathf.Approximately(desiredTarget, hiddenDither)
                    ? DissolveDuration(settings)
                    : RestoreDuration(settings);

                state.SetTarget(desiredTarget, duration);
                state.Tick(deltaTime, CurveFor(settings, desiredTarget, hiddenDither));
                ApplyDither(state, state.Current);

                if (kvp.Key == null ||
                    Mathf.Approximately(state.Target, visibleDither) &&
                    Mathf.Approximately(state.Current, visibleDither))
                {
                    _removeBuffer.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                var root = _removeBuffer[i];
                if (root != null && _tracked.TryGetValue(root, out var state))
                    ApplyDither(state, VisibleDither(Settings));

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

                var settings = Settings;
                SetDitherFloat(_propertyBlock, settings != null ? settings.DitherPropertyName : "Dither", dither);
                SetDitherFloat(_propertyBlock, settings != null ? settings.FallbackDitherPropertyName : "_Dither", dither);

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
                    ApplyDither(state, VisibleDither(Settings));
            }

            _tracked.Clear();
        }

        void OnDisable()
        {
            ResetAll();
            ResetGlobalFoliageDither();
        }

        void OnDestroy()
        {
            ResetAll();
            ResetGlobalFoliageDither();
        }

        void UpdateGlobalFoliageDither()
        {
            var settings = Settings;
            if (settings == null || !settings.PlayerFoliageZoneEnabled || _target == null)
            {
                ResetGlobalPlayerFoliageDither();
            }
            else
            {
                var position = _target.position;
                Shader.SetGlobalVector(FoliageDitherPositionId, new Vector4(position.x, position.y, position.z, 1f));
                Shader.SetGlobalVector(
                    FoliageDitherParamsId,
                    new Vector4(
                        Mathf.Max(0f, settings.PlayerFoliageZoneRadius),
                        Mathf.Max(0.01f, settings.PlayerFoliageZoneSoftness),
                        Mathf.Clamp01(settings.PlayerFoliageZoneDither),
                        0f));
            }

            UpdateGlobalCursorFoliageDither(settings);
        }

        static void ResetGlobalFoliageDither()
        {
            ResetGlobalPlayerFoliageDither();
            ResetGlobalCursorFoliageDither();
        }

        static void ResetGlobalPlayerFoliageDither()
        {
            Shader.SetGlobalVector(FoliageDitherParamsId, Vector4.zero);
        }

        static void ResetGlobalCursorFoliageDither()
        {
            Shader.SetGlobalVector(CursorDitherParamsId, Vector4.zero);
        }

        static void UpdateGlobalCursorFoliageDither(CameraObstacleHiderSettings settings)
        {
            var mouse = Mouse.current;
            if (settings == null || !settings.CursorFoliageZoneEnabled || mouse == null)
            {
                ResetGlobalCursorFoliageDither();
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            Shader.SetGlobalVector(CursorDitherPositionId, new Vector4(position.x, position.y, 0f, 1f));
            Shader.SetGlobalVector(
                CursorDitherParamsId,
                new Vector4(
                    Mathf.Max(0f, settings.CursorFoliageZoneRadius),
                    Mathf.Max(0.01f, settings.CursorFoliageZoneSoftness),
                    Mathf.Clamp01(settings.CursorFoliageZoneDither),
                    0f));
        }

        static void SetDitherFloat(MaterialPropertyBlock block, string propertyName, float value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return;

            block.SetFloat(Shader.PropertyToID(propertyName), value);
        }

        static float VisibleDither(CameraObstacleHiderSettings settings)
        {
            return settings != null ? settings.VisibleDither : 0f;
        }

        static float HiddenDither(CameraObstacleHiderSettings settings)
        {
            return settings != null ? settings.HiddenDither : 1f;
        }

        static float DissolveDuration(CameraObstacleHiderSettings settings)
        {
            return settings != null ? Mathf.Max(0f, settings.DissolveDuration) : 0.25f;
        }

        static float RestoreDuration(CameraObstacleHiderSettings settings)
        {
            return settings != null ? Mathf.Max(0f, settings.RestoreDuration) : 0.2f;
        }

        static AnimationCurve CurveFor(CameraObstacleHiderSettings settings, float target, float hiddenDither)
        {
            if (settings == null)
                return null;

            return Mathf.Approximately(target, hiddenDither)
                ? settings.DissolveCurve
                : settings.RestoreCurve;
        }

        sealed class DitherState
        {
            public readonly Renderer[] Renderers;
            public float Current;
            public float Target;
            float _start;
            float _elapsed;
            float _duration;

            public DitherState(Renderer[] renderers, float current)
            {
                Renderers = renderers;
                Current = current;
                Target = current;
                _start = current;
            }

            public void SetTarget(float target, float duration)
            {
                if (Mathf.Approximately(Target, target))
                    return;

                _start = Current;
                Target = target;
                _elapsed = 0f;
                _duration = duration;
            }

            public void Tick(float deltaTime, AnimationCurve curve)
            {
                if (Mathf.Approximately(Current, Target))
                    return;

                if (_duration <= 0f)
                {
                    Current = Target;
                    return;
                }

                _elapsed += deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float shapedT = curve != null ? curve.Evaluate(t) : t;
                Current = Mathf.LerpUnclamped(_start, Target, shapedT);

                if (t >= 1f)
                    Current = Target;
            }
        }
    }
}
