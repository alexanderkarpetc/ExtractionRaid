using UnityEngine;

namespace View
{
    [CreateAssetMenu(fileName = "CameraObstacleHiderSettings", menuName = "View/Camera Obstacle Hider Settings")]
    public class CameraObstacleHiderSettings : ScriptableObject
    {
        public const string ResourcePath = "Configs/CameraObstacleHiderSettings";

        [Header("Shader Properties")]
        [SerializeField] string _ditherPropertyName = "Dither";
        [SerializeField] string _fallbackDitherPropertyName = "_Dither";

        [Header("Dither Range")]
        [Tooltip("Value used when the object is fully visible. Texture-based dissolve often needs values below 0.")]
        [SerializeField] float _visibleDither = 0f;

        [Tooltip("Value used when the object is fully dissolved. Texture-based dissolve often needs values above 1.")]
        [SerializeField] float _hiddenDither = 1f;

        [Header("Timing")]
        [SerializeField, Min(0f)] float _dissolveDuration = 0.25f;
        [SerializeField, Min(0f)] float _restoreDuration = 0.2f;

        [Header("Curves")]
        [SerializeField] AnimationCurve _dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] AnimationCurve _restoreCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public string DitherPropertyName => _ditherPropertyName;
        public string FallbackDitherPropertyName => _fallbackDitherPropertyName;
        public float VisibleDither => _visibleDither;
        public float HiddenDither => _hiddenDither;
        public float DissolveDuration => _dissolveDuration;
        public float RestoreDuration => _restoreDuration;
        public AnimationCurve DissolveCurve => _dissolveCurve;
        public AnimationCurve RestoreCurve => _restoreCurve;
    }
}
