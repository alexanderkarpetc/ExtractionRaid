using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    public class MaterialPropertyTweener : MonoBehaviour
    {
        public enum PropertyType
        {
            Float,
            Color,
            Vector
        }

        [System.Serializable]
        public class Track
        {
            public string PropertyName = "_Alpha";
            public PropertyType Type = PropertyType.Float;
            public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            public float FloatFrom;
            public float FloatTo = 1f;
            public Color ColorFrom = Color.clear;
            public Color ColorTo = Color.white;
            public Vector4 VectorFrom;
            public Vector4 VectorTo = Vector4.one;

            [System.NonSerialized] public int PropertyId;
        }

        [Header("Target")]
        [SerializeField] Renderer _targetRenderer;
        [SerializeField] int _materialIndex;

        [Header("Timing")]
        [SerializeField, Min(0f)] float _duration = 0.2f;
        [SerializeField] bool _playOnEnable;
        [SerializeField] bool _useUnscaledTime;

        [Header("Tracks")]
        [SerializeField] Track[] _tracks =
        {
            new()
        };

        MaterialPropertyBlock _propertyBlock;
        float _time;
        float _direction = 1f;
        bool _isPlaying;

        public bool IsPlaying => _isPlaying;
        public float NormalizedTime => _duration <= 0f ? (_direction >= 0f ? 1f : 0f) : Mathf.Clamp01(_time / _duration);

        void Awake()
        {
            if (_targetRenderer == null)
                _targetRenderer = GetComponentInChildren<Renderer>();

            _propertyBlock = new MaterialPropertyBlock();
            CachePropertyIds();
        }

        void OnEnable()
        {
            if (_playOnEnable)
                PlayForward();
            else
                Apply();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _duration = Mathf.Max(0f, _duration);
            _materialIndex = Mathf.Max(0, _materialIndex);
            if (!Application.isPlaying) return;

            CachePropertyIds();
            Apply();
        }
#endif

        void Update()
        {
            if (!_isPlaying) return;

            float delta = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _time = Mathf.Clamp(_time + delta * _direction, 0f, _duration);
            Apply();

            if (_time <= 0f || _time >= _duration)
                _isPlaying = false;
        }

        public void PlayForward()
        {
            _direction = 1f;
            _isPlaying = true;
            if (_duration <= 0f)
            {
                _time = 1f;
                Apply();
                _isPlaying = false;
            }
        }

        public void RestartForward()
        {
            _time = 0f;
            _direction = 1f;
            _isPlaying = true;
            Apply();

            if (_duration <= 0f)
            {
                _time = 1f;
                Apply();
                _isPlaying = false;
            }
        }

        public void PlayReverse()
        {
            _direction = -1f;
            _isPlaying = true;
            if (_duration <= 0f)
            {
                _time = 0f;
                Apply();
                _isPlaying = false;
            }
        }

        public void RestartReverse()
        {
            _time = _duration;
            _direction = -1f;
            _isPlaying = true;
            Apply();

            if (_duration <= 0f)
            {
                _time = 0f;
                Apply();
                _isPlaying = false;
            }
        }

        public void SetActive(bool active)
        {
            if (active)
                PlayForward();
            else
                PlayReverse();
        }

        public void Stop()
        {
            _isPlaying = false;
        }

        public void SetNormalized(float normalized)
        {
            _time = Mathf.Clamp01(normalized) * Mathf.Max(_duration, 0f);
            Apply();
        }

        public void SetFloatRange(int trackIndex, float from, float to)
        {
            if (!TryGetTrack(trackIndex, out var track)) return;
            track.FloatFrom = from;
            track.FloatTo = to;
            Apply();
        }

        public void SetColorRange(int trackIndex, Color from, Color to)
        {
            if (!TryGetTrack(trackIndex, out var track)) return;
            track.ColorFrom = from;
            track.ColorTo = to;
            Apply();
        }

        public void SetVectorRange(int trackIndex, Vector4 from, Vector4 to)
        {
            if (!TryGetTrack(trackIndex, out var track)) return;
            track.VectorFrom = from;
            track.VectorTo = to;
            Apply();
        }

        void CachePropertyIds()
        {
            if (_tracks == null) return;

            for (int i = 0; i < _tracks.Length; i++)
            {
                var track = _tracks[i];
                if (track == null || string.IsNullOrEmpty(track.PropertyName)) continue;
                track.PropertyId = Shader.PropertyToID(track.PropertyName);
            }
        }

        bool TryGetTrack(int index, out Track track)
        {
            track = null;
            if (_tracks == null || index < 0 || index >= _tracks.Length) return false;
            track = _tracks[index];
            return track != null;
        }

        void Apply()
        {
            if (_targetRenderer == null || _tracks == null || _tracks.Length == 0) return;
            int materialCount = _targetRenderer.sharedMaterials != null ? _targetRenderer.sharedMaterials.Length : 0;
            if (materialCount == 0) return;
            int materialIndex = Mathf.Clamp(_materialIndex, 0, materialCount - 1);

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            float normalized = _duration <= 0f ? (_direction >= 0f ? 1f : 0f) : Mathf.Clamp01(_time / _duration);

            _targetRenderer.GetPropertyBlock(_propertyBlock, materialIndex);
            for (int i = 0; i < _tracks.Length; i++)
            {
                var track = _tracks[i];
                if (track == null || string.IsNullOrEmpty(track.PropertyName)) continue;
                if (track.PropertyId == 0)
                    track.PropertyId = Shader.PropertyToID(track.PropertyName);

                float t = track.Curve != null ? Mathf.Clamp01(track.Curve.Evaluate(normalized)) : normalized;
                switch (track.Type)
                {
                    case PropertyType.Float:
                        _propertyBlock.SetFloat(track.PropertyId, Mathf.Lerp(track.FloatFrom, track.FloatTo, t));
                        break;
                    case PropertyType.Color:
                        _propertyBlock.SetColor(track.PropertyId, Color.Lerp(track.ColorFrom, track.ColorTo, t));
                        break;
                    case PropertyType.Vector:
                        _propertyBlock.SetVector(track.PropertyId, Vector4.Lerp(track.VectorFrom, track.VectorTo, t));
                        break;
                }
            }

            _targetRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
        }
    }
}
