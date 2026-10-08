using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtractionRaid.View.Authoring
{
    [Serializable]
    public sealed class RoadIntersectionEnd
    {
        public SplatRoad road;
        public bool start = true;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RoadIntersection : MonoBehaviour
    {
        public List<RoadIntersectionEnd> approaches = new List<RoadIntersectionEnd>();
        [Min(0.1f)] public float cutback = 5;
        [Range(0, 1)] public float cornerRounding = 0.75f;
        [Range(1, 32)] public int cornerSegments = 8;
        [Range(1, 32)] public int surfaceSegments = 8;
        public Material materialTemplate;
        public int mapResolution = 512;
        [HideInInspector] public Mesh generatedMesh;
        [HideInInspector] public Material generatedMaterial;
        [HideInInspector] public string assetFolder;
        [HideInInspector] public List<SplatRoad> builtRoads = new List<SplatRoad>();
    }
}