using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Persistent dev-cheat configuration stored as a ScriptableObject asset.
    /// Lives in Resources/DevCheatsConfig.asset — loaded once via Resources.Load.
    /// Changes in Editor (including Play Mode) persist automatically.
    /// </summary>
    [CreateAssetMenu(fileName = "DevCheatsConfig", menuName = "Dev/Cheats Config")]
    public class DevCheatsConfig : ScriptableObject
    {
        // ── Cheats ──────────────────────────────────────────
        [Header("Cheats")]
        public bool GodMode;
        public bool InfiniteAmmo;

        // ── Weapon ──────────────────────────────────────────
        [Header("Weapon")]
        public float DamageMultiplier = 1f;
        public float ProjectileSpeedMultiplier = 1f;
        public float FireRateMultiplier = 1f;

        // ── Recoil ──────────────────────────────────────────
        [Header("Recoil")]
        public bool NoRecoil;
        public float RecoilMultiplier = .5f;
        public float RecoilForwardMultiplier = 1f;
        public float RecoilSideMultiplier = 1f;
        public float RecoilRecoveryMultiplier = 1f;

        // ── Aim Split ───────────────────────────────────────
        [Header("Aim Split")]
        public bool AimSplitEnabled;
        public float AimFollowMultiplier = 1f;

        // ── Player ──────────────────────────────────────────
        [Header("Player")]
        public float MoveSpeedMultiplier = 1f;

        // ── FOV ─────────────────────────────────────────────
        [Header("FOV")]
        public bool FOVEnabled = true;
        public float FOVNearRadius = 6f;
        public float FOVFarRadius = 25f;
        public float FOVAngle = 130f;
        public bool ForceShowAllBots;
        public bool FOVOcclusionEnabled = true;

        // ── Fog of War ──────────────────────────────────────
        [Header("Fog of War")]
        public bool FogOfWarEnabled = true;
        public float FogBlurRadius = 10f;
        public int FogBlurIterations = 3;
        public float FogIntensity = 0.6f;
        public float FogDesaturation;
        public Color FogColor = new(0.02f, 0.02f, 0.05f, 1f);
        public int FoWRTScale = 256;
        public float FOVRayStep = 2f;
        public float FogTemporalBlend = 0.2f;

        // ── Crosshair ─────────────────────────────────────
        [Header("Crosshair")]
        public bool CrosshairEnabled = true;
        public float CrosshairLineLength = 24f;
        public float CrosshairLineThickness = 6f;
        public float CrosshairBaseGap = 15f;
        public float CrosshairCenterDotSize = 9f;
        public float CrosshairBloomExtraGap = 30f;
        public Color CrosshairNormalColor = new(0.2f, 1f, 0.3f, 0.9f);
        public Color CrosshairWarningColor = new(1f, 0.25f, 0.2f, 0.9f);
        public Color CrosshairBloomColor = new(1f, 1f, 1f, 0.95f);

        // ── Status Effects ──────────────────────────────────
        [Header("Status Effects")]
        public bool ForceBleedPlayer;
    }
}
