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

        [Header("Player Foliage Zone")]
        [SerializeField] bool _playerFoliageZoneEnabled = true;
        [Tooltip("Screen-space radius in pixels around the player.")]
        [SerializeField, Min(0f)] float _playerFoliageZoneRadius = 110f;
        [Tooltip("Screen-space fade width in pixels outside the radius.")]
        [SerializeField, Min(0.01f)] float _playerFoliageZoneSoftness = 70f;
        [SerializeField, Range(0f, 1f)] float _playerFoliageZoneDither = 1f;

        [Header("Cursor Foliage Zone")]
        [SerializeField] bool _cursorFoliageZoneEnabled = true;
        [Tooltip("Screen-space radius in pixels around the cursor.")]
        [SerializeField, Min(0f)] float _cursorFoliageZoneRadius = 90f;
        [Tooltip("Screen-space fade width in pixels outside the cursor radius.")]
        [SerializeField, Min(0.01f)] float _cursorFoliageZoneSoftness = 60f;
        [SerializeField, Range(0f, 1f)] float _cursorFoliageZoneDither = 1f;

        public string DitherPropertyName => _ditherPropertyName;
        public string FallbackDitherPropertyName => _fallbackDitherPropertyName;
        public float VisibleDither => _visibleDither;
        public float HiddenDither => _hiddenDither;
        public float DissolveDuration => _dissolveDuration;
        public float RestoreDuration => _restoreDuration;
        public AnimationCurve DissolveCurve => _dissolveCurve;
        public AnimationCurve RestoreCurve => _restoreCurve;
        public bool PlayerFoliageZoneEnabled => _playerFoliageZoneEnabled;
        public float PlayerFoliageZoneRadius => _playerFoliageZoneRadius;
        public float PlayerFoliageZoneSoftness => _playerFoliageZoneSoftness;
        public float PlayerFoliageZoneDither => _playerFoliageZoneDither;
        public bool CursorFoliageZoneEnabled => _cursorFoliageZoneEnabled;
        public float CursorFoliageZoneRadius => _cursorFoliageZoneRadius;
        public float CursorFoliageZoneSoftness => _cursorFoliageZoneSoftness;
        public float CursorFoliageZoneDither => _cursorFoliageZoneDither;
    }
}
