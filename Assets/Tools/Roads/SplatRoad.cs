using System.Collections.Generic;
using UnityEngine;

namespace ExtractionRaid.View.Authoring
{
    // Authoring data only. Mesh generation and asset creation live in the Editor assembly.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SplatRoad : MonoBehaviour
    {
        [Tooltip("Control points in this object's local space.")]
        public List<Vector3> points = new List<Vector3>();
        [Min(0.1f)] public float width = 4;
        [Min(0.05f)] public float sampleSpacing = 0.5f;
        public bool smooth = true;
        [Range(1, 16)] public int widthSegments = 4;
        public bool snapToGround = true;
        public LayerMask groundLayers = ~4;
        [Min(0.1f)] public float rayHeight = 10;
        [Min(0.1f)] public float rayDistance = 30;
        [Min(0)] public float surfaceLift = 0.02f;
        [Min(0.1f)] public float textureTileSize = 2;
        [Range(-5, 0)] public float depthOffset = -1;
        [Tooltip("World metres: positive right, negative left, following point order.")]
        public float lateralOffset;
        public Material materialTemplate;
        [Tooltip("Disable for a regular material such as Road Marking Unlit. Set before the first build.")]
        public bool useSplatMaps = true;
        [Tooltip("Resolution used only when first creating this road's paint maps.")]
        public int mapResolution = 1024;
        [Tooltip("Initial fade width in metres; rebuilding preserves the painted mask.")]
        [Min(0)] public float edgeFade = 0.25f;
        public bool fadeEnds = true;
        [HideInInspector] public Mesh generatedMesh;
        [HideInInspector] public Material generatedMaterial;
        [HideInInspector] public string assetFolder;
    }
}
