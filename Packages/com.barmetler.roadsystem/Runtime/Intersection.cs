using System;
using System.Linq;
using UnityEngine;

namespace Barmetler.RoadSystem
{
    [SelectionBase]
    public class Intersection : MonoBehaviour
    {
        [Serializable]
        public class IntersectionSettings
        {
            [Min(0.01f)]
            [Tooltip("Multiplier applied to the width and length of the intersection.")]
            public float widthMultiplier = 1f;
        }

        [Tooltip("Settings regarding intersection sizing")]
        public IntersectionSettings settings = new IntersectionSettings();

        [SerializeField, HideInInspector]
        private RoadAnchor[] anchorPoints = Array.Empty<RoadAnchor>();

        [SerializeField, HideInInspector]
        private float radius;

        [SerializeField, HideInInspector]
        private MeshFilter[] meshFilters = Array.Empty<MeshFilter>();

        [SerializeField, HideInInspector]
        private Mesh[] sourceMeshes = Array.Empty<Mesh>();

        [SerializeField, HideInInspector]
        private Vector3[] sourceAnchorPositions = Array.Empty<Vector3>();

        public RoadAnchor[] AnchorPoints => anchorPoints;

        private void OnValidate()
        {
            RefreshData();
            GenerateMesh();
        }

        private void RefreshData()
        {
            anchorPoints = GetComponentsInChildren<RoadAnchor>();
            if (sourceAnchorPositions.Length != anchorPoints.Length)
                sourceAnchorPositions = anchorPoints
                    .Select(anchor => transform.InverseTransformPoint(anchor.transform.position))
                    .ToArray();

            meshFilters = GetComponentsInChildren<MeshFilter>();
            if (sourceMeshes.Length != meshFilters.Length)
                sourceMeshes = meshFilters.Select(filter => filter.sharedMesh).ToArray();

            radius = Mathf.Sqrt(anchorPoints.Length > 0
                ? anchorPoints.Select(e => (e.transform.position - transform.position).sqrMagnitude).Max()
                : 0);
        }

        private void Awake()
        {
            RefreshData();
        }

        public void Invalidate(bool updateMesh = true)
        {
            RefreshData();
            foreach (var p in anchorPoints)
            {
                p.Invalidate();
            }
        }

        public float Radius => radius;

        public void GenerateMesh()
        {
            var widthMultiplier = Mathf.Max(settings?.widthMultiplier ?? 1f, 0.01f);
            var planeScale = new Vector3(widthMultiplier, 1f, widthMultiplier);

            for (var i = 0; i < anchorPoints.Length && i < sourceAnchorPositions.Length; ++i)
            {
                anchorPoints[i].transform.position =
                    transform.TransformPoint(Vector3.Scale(sourceAnchorPositions[i], planeScale));
            }

            for (var i = 0; i < meshFilters.Length && i < sourceMeshes.Length; ++i)
            {
                var filter = meshFilters[i];
                var sourceMesh = sourceMeshes[i];
                if (!filter || !sourceMesh) continue;
                if (!sourceMesh.isReadable)
                {
                    Debug.LogError($"Intersection source mesh '{sourceMesh.name}' must have Read/Write enabled.", this);
                    continue;
                }

                var previousMesh = filter.sharedMesh;
                var generatedMesh = Instantiate(sourceMesh);
                generatedMesh.name = "Intersection Mesh";

                var vertices = generatedMesh.vertices;
                for (var vertexIndex = 0; vertexIndex < vertices.Length; ++vertexIndex)
                {
                    var rootPosition = transform.InverseTransformPoint(
                        filter.transform.TransformPoint(vertices[vertexIndex]));
                    rootPosition = Vector3.Scale(rootPosition, planeScale);
                    vertices[vertexIndex] = filter.transform.InverseTransformPoint(
                        transform.TransformPoint(rootPosition));
                }

                generatedMesh.vertices = vertices;
                generatedMesh.RecalculateBounds();
                generatedMesh.RecalculateNormals();
                generatedMesh.RecalculateTangents();
                filter.sharedMesh = generatedMesh;

                var collider = filter.GetComponent<MeshCollider>();
                if (collider) collider.sharedMesh = generatedMesh;

                if (previousMesh && previousMesh != sourceMesh && previousMesh.name == "Intersection Mesh")
                {
                    if (Application.isPlaying) Destroy(previousMesh);
                    else DestroyImmediate(previousMesh);
                }
            }

            radius = Mathf.Sqrt(anchorPoints.Length > 0
                ? anchorPoints.Select(e => (e.transform.position - transform.position).sqrMagnitude).Max()
                : 0);
        }
    }
}
