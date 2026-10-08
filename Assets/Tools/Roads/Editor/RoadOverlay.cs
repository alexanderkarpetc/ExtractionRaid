using System;
using System.Collections.Generic;
using ExtractionRaid.View.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExtractionRaid.Editor.Roads
{
    public static class RoadOverlay
    {
        public static SplatRoad Create(SplatRoad source)
        {
            if (!source || !source.gameObject.scene.IsValid() || EditorUtility.IsPersistent(source) ||
                EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetPrefabStage(source.gameObject) != null)
                throw new InvalidOperationException("Select a road in a regular scene outside Play Mode.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var go = new GameObject(source.name + " Markings");
            SceneManager.MoveGameObjectToScene(go, source.gameObject.scene);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            var overlay = go.AddComponent<SplatRoad>();
            // Copy authoring settings only: generated asset references must never be shared.
            overlay.points = new List<Vector3>(source.points);
            overlay.width = source.width;
            overlay.lateralOffset = source.lateralOffset;
            overlay.sampleSpacing = source.sampleSpacing;
            overlay.smooth = source.smooth;
            overlay.widthSegments = source.widthSegments;
            overlay.snapToGround = source.snapToGround;
            overlay.groundLayers = source.groundLayers;
            overlay.rayHeight = source.rayHeight;
            overlay.rayDistance = source.rayDistance;
            overlay.surfaceLift = source.surfaceLift + 0.01f;
            overlay.textureTileSize = source.textureTileSize;
            overlay.depthOffset = source.depthOffset;
            overlay.useSplatMaps = false;
            Undo.RegisterCreatedObjectUndo(go, "Create Road Overlay");
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return overlay;
        }
    }
}