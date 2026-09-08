using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Editor
{
    [Serializable]
    public sealed class ScatterPrefabEntry
    {
        public SceneReference prefab = new SceneReference();
        public bool enabled = true;
        [Min(0)] public float weight = 1;
        [Min(.01f)] public float scaleMin = 1, scaleMax = 1;
        public bool allowRotationY = true;
        [Tooltip("Negative uses global distance.")] public float minDistanceOverride = -1;
    }

    public sealed class ScatterSettings : ToolSettings
    {
        public List<ScatterPrefabEntry> prefabs = new List<ScatterPrefabEntry>();
        [Header("Placement")]
        [Min(1)] public int targetObjectCount = 500;
        public int seed = 1;
        [Min(0)] public float globalMinDistance = 1;
        [Header("Rotation and scale")]
        public bool randomRotationY = true;
        public float rotationMin, rotationMax = 360;
        [Min(.01f)] public float globalScaleMin = 1, globalScaleMax = 1;
        [Header("Surface")]
        [Min(.1f)] public float raycastHeight = 100;
        public LayerMask groundLayerMask = 1;
        [Range(0, 89)] public float maxGroundSlope = 35;
        public bool alignToGroundNormal;
        [Header("Collision")]
        public bool avoidCollisions = true;
        public LayerMask collisionLayerMask = ~0;
        [Min(.01f)] public float collisionRadiusMultiplier = 1;
        [Header("Exclusion and NavMesh")]
        public List<SceneReference> exclusionColliders = new List<SceneReference>();
        public LayerMask exclusionLayerMask;
        public bool avoidNavMesh;
        [Min(.01f)] public float navMeshAvoidanceRadius = .5f;
        [Header("Safety")]
        [Min(1)] public int maxPlacementAttempts = 50000;
        [Range(1, 10000)] public int maximumGeneratedObjects = 2000;
        [HideInInspector] public SceneReference preview = new SceneReference();
    }
}
