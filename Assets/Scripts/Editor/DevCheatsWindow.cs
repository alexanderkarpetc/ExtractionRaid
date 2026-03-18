using Dev;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class DevCheatsWindow : EditorWindow
    {
        [MenuItem("Window/Dev Cheats")]
        static void Open()
        {
            GetWindow<DevCheatsWindow>("Dev Cheats");
        }

        void OnGUI()
        {
            // ── Cheats ──────────────────────────────────────
            EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            DevCheats.GodMode      = EditorGUILayout.Toggle("God Mode", DevCheats.GodMode);
            DevCheats.InfiniteAmmo = EditorGUILayout.Toggle("Infinite Ammo", DevCheats.InfiniteAmmo);
            DevCheats.NoRecoil     = EditorGUILayout.Toggle("No Recoil", DevCheats.NoRecoil);

            EditorGUILayout.Space(8);

            // ── Weapon Tweaks ───────────────────────────────
            EditorGUILayout.LabelField("Weapon Tweaks", EditorStyles.boldLabel);
            DevCheats.DamageMultiplier          = EditorGUILayout.Slider("Damage Multiplier", DevCheats.DamageMultiplier, 0.1f, 50f);
            DevCheats.ProjectileSpeedMultiplier = EditorGUILayout.Slider("Projectile Speed", DevCheats.ProjectileSpeedMultiplier, 0.1f, 10f);
            DevCheats.FireRateMultiplier        = EditorGUILayout.Slider("Fire Rate", DevCheats.FireRateMultiplier, 0.1f, 10f);
            DevCheats.RecoilMultiplier          = EditorGUILayout.Slider("Recoil", DevCheats.RecoilMultiplier, 0f, 5f);

            EditorGUILayout.Space(8);

            // ── Player Tweaks ───────────────────────────────
            EditorGUILayout.LabelField("Player Tweaks", EditorStyles.boldLabel);
            DevCheats.MoveSpeedMultiplier = EditorGUILayout.Slider("Move Speed", DevCheats.MoveSpeedMultiplier, 0.1f, 10f);

            EditorGUILayout.Space(8);

            // ── FOV ─────────────────────────────────────────
            EditorGUILayout.LabelField("FOV", EditorStyles.boldLabel);
            DevCheats.FOVEnabled      = EditorGUILayout.Toggle("FOV Enabled", DevCheats.FOVEnabled);
            DevCheats.FOVNearRadius   = EditorGUILayout.Slider("Near Radius", DevCheats.FOVNearRadius, 1f, 15f);
            DevCheats.FOVFarRadius    = EditorGUILayout.Slider("Far Radius", DevCheats.FOVFarRadius, 10f, 100f);
            DevCheats.FOVAngle        = EditorGUILayout.Slider("FOV Angle", DevCheats.FOVAngle, 30f, 360f);
            DevCheats.ForceShowAllBots = EditorGUILayout.Toggle("Force Show All Bots", DevCheats.ForceShowAllBots);
            DevCheats.FOVOcclusionEnabled = EditorGUILayout.Toggle("FOV Occlusion", DevCheats.FOVOcclusionEnabled);

            EditorGUILayout.Space(8);

            // ── Fog of War ─────────────────────────────────────
            EditorGUILayout.LabelField("Fog of War", EditorStyles.boldLabel);
            DevCheats.FogOfWarEnabled  = EditorGUILayout.Toggle("FoW Enabled", DevCheats.FogOfWarEnabled);
            DevCheats.FogBlurRadius    = EditorGUILayout.Slider("Blur Radius", DevCheats.FogBlurRadius, 0f, 10f);
            DevCheats.FogBlurIterations = EditorGUILayout.IntSlider("Blur Iterations", DevCheats.FogBlurIterations, 1, 6);
            DevCheats.FogIntensity     = EditorGUILayout.Slider("Fog Intensity", DevCheats.FogIntensity, 0f, 1f);
            DevCheats.FogDesaturation  = EditorGUILayout.Slider("Desaturation", DevCheats.FogDesaturation, 0f, 1f);
            DevCheats.FogColor         = EditorGUILayout.ColorField("Fog Color", DevCheats.FogColor);
            DevCheats.FoWRTScale       = EditorGUILayout.IntSlider("RT Resolution", DevCheats.FoWRTScale, 64, 1024);
            DevCheats.FOVRayStep       = EditorGUILayout.Slider("Ray Step (°)", DevCheats.FOVRayStep, 0.5f, 5f);
            DevCheats.FogTemporalBlend = EditorGUILayout.Slider("Temporal Blend", DevCheats.FogTemporalBlend, 0.05f, 1f);
            DevCheats.FoWBypassBlur    = EditorGUILayout.Toggle("⚠ Bypass Blur (debug)", DevCheats.FoWBypassBlur);

            EditorGUILayout.Space(8);

            // ── Status Effects ───────────────────────────────
            EditorGUILayout.LabelField("Status Effects", EditorStyles.boldLabel);
            if (GUILayout.Button("Apply Bleed to Player"))
                DevCheats.ForceBleedPlayer = true;

            EditorGUILayout.Space(12);

            // ── Reset ───────────────────────────────────────
            if (GUILayout.Button("Reset All to Defaults"))
            {
                DevCheats.Reset();
            }
        }
    }
}
