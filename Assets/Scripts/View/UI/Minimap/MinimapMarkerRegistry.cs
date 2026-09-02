using System;
using System.Collections.Generic;
using UnityEngine;

namespace View.UI.Minimap
{
    public enum MinimapMarkerType
    {
        Player,
        Npc,
        Extraction,
        Quest,
        Deploy,
        Custom,
    }

    /// <summary>
    /// Single marker entry. <see cref="LivePositionFn"/> is preferred when set — it's
    /// re-evaluated each frame so the marker tracks a moving entity. Otherwise the
    /// fixed <see cref="StaticPosition"/> is used.
    /// </summary>
    public class MinimapMarker
    {
        public string Id;
        public MinimapMarkerType Type;
        public Vector3 StaticPosition;
        public Func<Vector3> LivePositionFn;
        // Optional yaw provider in degrees, clockwise from minimap-up (world +Z). Only
        // honored by marker types that draw a directional indicator (Player arrow).
        public Func<float> LiveRotationFn;
        public string Tooltip;

        public Vector3 ResolvePosition()
        {
            return LivePositionFn != null ? LivePositionFn() : StaticPosition;
        }

        public float ResolveRotation()
        {
            return LiveRotationFn != null ? LiveRotationFn() : 0f;
        }
    }

    /// <summary>
    /// View-side stash of all minimap markers. Anyone — quest scaffolding, the
    /// presenter itself, custom subsystems — calls <see cref="Register"/> with a
    /// stable id; <c>MinimapWindow</c> queries this list each frame and never has to
    /// know who placed what. Cleared on raid start so stale entries from a previous
    /// run don't leak in.
    /// </summary>
    public static class MinimapMarkerRegistry
    {
        static readonly Dictionary<string, MinimapMarker> _markers = new();

        // Concrete ValueCollection rather than IReadOnlyCollection: MinimapWindow walks
        // this every frame, and an interface-typed foreach boxes the struct enumerator.
        public static Dictionary<string, MinimapMarker>.ValueCollection Markers => _markers.Values;

        public static void Register(string id, MinimapMarkerType type, Vector3 worldPos,
            string tooltip = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            _markers[id] = new MinimapMarker
            {
                Id = id,
                Type = type,
                StaticPosition = worldPos,
                LivePositionFn = null,
                Tooltip = tooltip,
            };
        }

        public static void Register(string id, MinimapMarkerType type,
            Func<Vector3> livePositionFn, string tooltip = null,
            Func<float> liveRotationFn = null)
        {
            if (string.IsNullOrEmpty(id) || livePositionFn == null) return;
            _markers[id] = new MinimapMarker
            {
                Id = id,
                Type = type,
                LivePositionFn = livePositionFn,
                LiveRotationFn = liveRotationFn,
                Tooltip = tooltip,
            };
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _markers.Remove(id);
        }

        public static void Clear() => _markers.Clear();
    }
}
