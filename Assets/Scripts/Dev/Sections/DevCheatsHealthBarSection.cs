using UnityEngine;

namespace Dev
{
    public class DevCheatsHealthBarSection : ScriptableObject
    {
        // Layout
        public float HBarWidth = 1f;
        public float HBarHeight = 0.12f;
        public float HBarOffsetY = 2.4f;
        public float HBarBorderSize = 0.04f;

        // Animation
        public float HBarTrailDelay = 0.35f;
        public float HBarTrailSpeed = 1.2f;
        public float HBarFlashDuration = 0.4f;
        public float HBarFlashExpandX = 0.015f;
        public float HBarFlashExpandY = 0.2f;
        public float HBarFlashPower = 2f;
        public float HBarShakeIntensity = 0.05f;
        public float HBarShakeDuration = 0.3f;
        public float HBarShakeFrequency = 30f;
        public float HBarHpPerSegment = 25f;
        public float HBarSegmentLineWidth = 0.012f;
        public Color HBarSegmentLineColor = new(0f, 0f, 0f, 0.4f);

        // Colors
        public Color HBarTrailColor = new(0.8f, 0.15f, 0.1f, 1f);
        public Color HBarFlashColor = new(1f, 1f, 1f, 1f);
        public Color HBarBgColor = new(0.12f, 0.12f, 0.12f, 0.85f);
    }
}
