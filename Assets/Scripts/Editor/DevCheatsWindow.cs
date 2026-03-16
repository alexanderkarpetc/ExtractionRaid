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
