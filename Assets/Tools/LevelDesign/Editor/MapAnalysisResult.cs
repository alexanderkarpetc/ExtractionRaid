using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    [Flags]
    public enum AnalysisFlags { None = 0, Empty = 1, LowCover = 2, FarPOI = 4, LowDensity = 8, PotentialDeadEnd = 16, Corridor = 32, LongEmptyTraversal = 64 }
    [Serializable]
    public sealed class MapAnalysisSample
    {
        public Vector3 position;
        public int gridX, gridZ;
        public float interestingDistance = float.NaN, coverDistance = float.NaN, poiDistance = float.NaN;
        public int density = -1, directions = -1;
        public AnalysisFlags flags;
    }
    [Serializable]
    public sealed class MapAnalysisCluster
    {
        public Vector3 center;
        public int count;
        public float radius, coverDistance, poiDistance;
    }
    public sealed class MapAnalysisResult : ScriptableObject
    {
        public List<MapAnalysisSample> samples = new List<MapAnalysisSample>();
        public List<MapAnalysisCluster> clusters = new List<MapAnalysisCluster>();
        public float spacing;
        public bool complete;
        public string measurementSettings;
    }
}
