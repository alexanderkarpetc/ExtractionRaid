using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    public enum AnalysisOverlay { Traversal, CoverDistance, POIDistance, ObjectDensity, Connectivity }
    public enum POIType { Major, Minor, Extraction, Spawn, Other }
    [Serializable]
    public sealed class MapAnalysisPOI
    {
        public SceneReference sceneObject = new SceneReference();
        public string poiName = "POI";
        [Min(0)] public float radius = 5;
        public POIType type;
    }
    [Serializable]
    public sealed class DistanceThresholds
    {
        [Min(0)] public float warning = 8;
        [Min(0)] public float bad = 15;
        public int Classify(float distance) => distance >= bad ? 2 : distance >= warning ? 1 : 0;
        public bool Valid => float.IsFinite(warning) && float.IsFinite(bad) && warning >= 0 && bad > warning;
    }
    public sealed class MapAnalysisSettings : ToolSettings
    {
        [Header("NavMesh")]
        [Min(.1f)] public float sampleSpacing = 3;
        [Tooltip("Maximum vertical offset from the area plane; analyse floors separately.")]
        [Min(.01f)] public float sampleHeightTolerance = 2;
        [Tooltip("Agent type ID from Navigation settings.")] public int agentTypeId;
        public int navMeshAreaMask = -1;
        [Range(1, 100000)] public int maximumSamples = 20000;
        [Header("Modules")]
        public bool emptyTraversal = true;
        public bool cover = true;
        public bool poiDistance = true;
        public bool connectivity = true;
        public bool objectDensity = true;
        [Header("Empty traversal")]
        public LayerMask interestingLayerMask;
        public DistanceThresholds traversalThresholds = new DistanceThresholds();
        [Header("Cover")]
        public LayerMask coverLayerMask;
        public DistanceThresholds coverThresholds = new DistanceThresholds();
        [Header("POI")]
        public List<MapAnalysisPOI> pois = new List<MapAnalysisPOI>();
        public DistanceThresholds poiThresholds = new DistanceThresholds { warning = 30, bad = 60 };
        [Header("Density")]
        public LayerMask densityLayerMask;
        [Min(.1f)] public float densityRadius = 10;
        [Min(0)] public int lowDensityThreshold = 2;
        [Min(1)] public int highDensityThreshold = 9;
        [Header("Connectivity")]
        [Min(.1f)] public float connectivityRadius = 3;
        [Range(0, 4)] public int deadEndMaxDirections = 1;
        [Min(1)] public int minimumClusterSamples = 4;
        [Header("Display (cached results; rerun after measurement changes)")]
        public bool showVisualization = true;
        public AnalysisOverlay overlay;
        public bool showSamples = true, showProblemAreas = true, showLabels = true;
        [Range(100, 20000)] public int maximumDrawnSamples = 5000;
        [Range(1, 200)] public int maximumDrawnMarkers = 50;
        public Color goodColor = new Color(.2f, .85f, .3f, .7f);
        public Color warningColor = new Color(1, .7f, .1f, .8f);
        public Color badColor = new Color(1, .15f, .15f, .85f);
    }
}
