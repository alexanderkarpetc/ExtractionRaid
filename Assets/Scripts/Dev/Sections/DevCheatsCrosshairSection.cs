using UnityEngine;

namespace Dev
{
    public class DevCheatsCrosshairSection : ScriptableObject
    {
        // Crosshair
        public bool CrosshairEnabled = true;
        public float CrosshairLineLength = 24f;
        public float CrosshairLineThickness = 6f;
        public float CrosshairBaseGap = 15f;
        public float CrosshairCenterDotSize = 9f;
        public float CrosshairBloomExtraGap = 30f;
        public Color CrosshairNormalColor = new(0.2f, 1f, 0.3f, 0.9f);
        public Color CrosshairWarningColor = new(1f, 0.25f, 0.2f, 0.9f);
        public Color CrosshairBloomColor = new(1f, 1f, 1f, 0.95f);

        // Hit Markers
        public float HitMarkerScale = 1f;
        public float HitDuration = 0.3f;
        public float KillDuration = 0.5f;
        public float HitLineLength = 14f;
        public float KillLineLength = 18f;
        public float HitGapStart = 8f;
        public float HitGapExpand = 14f;
        public float HitMarkerThickness = 4f;
        public Color HitColor = Color.white;
        public Color KillColor = new(1f, 0.15f, 0.15f, 1f);

        // Headshot
        public float HeadshotOuterScale = 1.8f;
        public float HeadshotOuterExpandMul = 2f;
        public float HeadshotDuration = 0.5f;
        public Color HeadshotColor = new(1f, 0.85f, 0.2f, 1f);
    }
}
