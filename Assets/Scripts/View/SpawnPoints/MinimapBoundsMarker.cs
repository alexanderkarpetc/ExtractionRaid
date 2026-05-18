using UnityEngine;

namespace View.SpawnPoints
{
    /// <summary>
    /// Designer-placed component that defines what the minimap capture camera sees.
    /// Place one in the level (typically at the geometric center of the playable area);
    /// MinimapPresenter finds it at raid start and uses Position + Size + LayerMask to
    /// configure the one-shot orthographic capture. Falls back to a centered default
    /// if missing (logs a warning so designers know to add one).
    /// </summary>
    public class MinimapBoundsMarker : MonoBehaviour
    {
        [Tooltip("When true, ignore Size/Position and compute the captured area from " +
                 "every renderer on the capture layers. Recommended for most levels — " +
                 "ensures the whole map is visible without manual tuning.")]
        public bool autoFit = true;

        [Tooltip("Manual XZ size of the captured rectangle (used only when AutoFit is off).")]
        public Vector2 size = new Vector2(80f, 80f);

        [Tooltip("Camera capture layers. Default = everything EXCEPT UI, Player, Bot, " +
                 "FOV and Ragdoll — i.e. env geometry only. Adjust if your level uses " +
                 "extra layers you want included.")]
        public LayerMask captureLayers = DefaultCaptureLayers;

        [Tooltip("Padding applied around the auto-fit bounds (in world units).")]
        public float autoFitPadding = 4f;

        [Tooltip("Camera height above the bounds when capturing. Auto-fit derives this " +
                 "from level Y bounds; this is the minimum.")]
        public float cameraHeight = 60f;

        [Tooltip("Background color filled before the env geometry renders. Default is " +
                 "a slightly bright slate so an empty capture is obvious.")]
        public Color clearColor = new Color(0.18f, 0.22f, 0.30f, 1f);

        [Tooltip("Capture texture resolution (square). 512 is a good default.")]
        public int textureSize = 512;

        // Excludes UI (5), Player (6), Bot (7), FOV (8), Ragdoll (9). Includes
        // Default (0), IgnoreRaycast (2), Water (4) and any custom env layers.
        public const int DefaultCaptureLayers = ~((1 << 5) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 9));

        public Bounds WorldBounds =>
            new Bounds(transform.position, new Vector3(size.x, 1f, size.y));

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.85f, 1f, 0.15f);
            Gizmos.DrawCube(transform.position, new Vector3(size.x, 0.1f, size.y));
            Gizmos.color = new Color(0.45f, 0.85f, 1f, 1f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.1f, size.y));
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"Minimap Bounds ({size.x:0}×{size.y:0})");
        }
#endif
    }
}
