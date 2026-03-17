using System.Collections.Generic;
using UnityEngine;

namespace View.FogOfWar
{
    /// <summary>
    /// Builds a triangle-fan mesh from visibility polygon endpoints.
    /// Lives on a child GO of the player, on the "FOV" layer.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FOVMeshBuilder : MonoBehaviour
    {
        Mesh _mesh;
        MeshFilter _filter;
        Vector3[] _verts;
        int[] _tris;

        void Awake()
        {
            _mesh = new Mesh { name = "FOV_VisibilityMesh" };
            _mesh.MarkDynamic();
            _filter = GetComponent<MeshFilter>();
            _filter.mesh = _mesh;
        }

        /// <summary>
        /// Rebuild mesh from endpoints. endpoints[0] = fan center, endpoints[1..N] = perimeter.
        /// </summary>
        public void RebuildMesh(List<Vector3> endpoints)
        {
            int count = endpoints.Count;
            int perimeterCount = count - 1;
            if (perimeterCount < 3) return;

            int triCount = (perimeterCount - 1) * 3;
            EnsureArrays(count, triCount);

            // Convert world positions to local space
            for (int i = 0; i < count; i++)
                _verts[i] = transform.InverseTransformPoint(endpoints[i]);

            // Triangle fan: center(0) → pairs of consecutive perimeter verts
            int idx = 0;
            for (int i = 1; i < perimeterCount; i++)
            {
                _tris[idx++] = 0;
                _tris[idx++] = i;
                _tris[idx++] = i + 1;
            }

            // Close the fan: last perimeter → first perimeter
            _tris[idx++] = 0;
            _tris[idx++] = perimeterCount;
            _tris[idx++] = 1;

            _mesh.Clear();
            _mesh.SetVertices(_verts, 0, count);
            _mesh.SetTriangles(_tris, 0, idx, 0);
            _mesh.RecalculateBounds();
        }

        void EnsureArrays(int vertCount, int triCount)
        {
            // +3 for the closing triangle
            int totalTris = triCount + 3;
            if (_verts == null || _verts.Length < vertCount)
                _verts = new Vector3[vertCount * 2];
            if (_tris == null || _tris.Length < totalTris)
                _tris = new int[totalTris * 2];
        }

        void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
