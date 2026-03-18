using Dev;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class DevCheatsWindow : EditorWindow
    {
        // Foldout states (persisted via EditorPrefs)
        static bool _foldWeapon;
        static bool _foldRecoil;
        static bool _foldPlayer;
        static bool _foldFOV;
        static bool _foldFoW;
        static bool _foldAim;

        Vector2 _scroll;

        [MenuItem("Window/Dev Cheats")]
        static void Open()
        {
            GetWindow<DevCheatsWindow>("Dev Cheats");
        }

        void OnEnable()
        {
            _foldWeapon = EditorPrefs.GetBool("DevCheats_foldWeapon", false);
            _foldRecoil = EditorPrefs.GetBool("DevCheats_foldRecoil", false);
            _foldPlayer = EditorPrefs.GetBool("DevCheats_foldPlayer", false);
            _foldFOV    = EditorPrefs.GetBool("DevCheats_foldFOV", false);
            _foldFoW    = EditorPrefs.GetBool("DevCheats_foldFoW", false);
            _foldAim    = EditorPrefs.GetBool("DevCheats_foldAim", false);
        }

        void SaveFoldouts()
        {
            EditorPrefs.SetBool("DevCheats_foldWeapon", _foldWeapon);
            EditorPrefs.SetBool("DevCheats_foldRecoil", _foldRecoil);
            EditorPrefs.SetBool("DevCheats_foldPlayer", _foldPlayer);
            EditorPrefs.SetBool("DevCheats_foldFOV", _foldFOV);
            EditorPrefs.SetBool("DevCheats_foldFoW", _foldFoW);
            EditorPrefs.SetBool("DevCheats_foldAim", _foldAim);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── Cheats ──────────────────────────────────────
            EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            DevCheats.GodMode      = EditorGUILayout.Toggle("God Mode", DevCheats.GodMode);
            DevCheats.InfiniteAmmo = EditorGUILayout.Toggle("Infinite Ammo", DevCheats.InfiniteAmmo);

            EditorGUILayout.Space(8);

            // ── Weapon Tweaks ───────────────────────────────
            DrawFoldout(ref _foldWeapon, "Weapon Tweaks", () =>
            {
                DevCheats.DamageMultiplier          = EditorGUILayout.Slider("Damage ×", DevCheats.DamageMultiplier, 0.1f, 50f);
                DevCheats.ProjectileSpeedMultiplier = EditorGUILayout.Slider("Projectile Speed ×", DevCheats.ProjectileSpeedMultiplier, 0.1f, 10f);
                DevCheats.FireRateMultiplier        = EditorGUILayout.Slider("Fire Rate ×", DevCheats.FireRateMultiplier, 0.1f, 10f);
            });

            // ── Recoil ───────────────────────────────────────
            // NoRecoil inverted: toggle ON = recoil enabled, OFF = no recoil
            bool recoilEnabled = !DevCheats.NoRecoil;
            DrawToggleFoldout(ref _foldRecoil, ref recoilEnabled, "Recoil", () =>
            {
                DevCheats.RecoilMultiplier          = EditorGUILayout.Slider("Kick ×", DevCheats.RecoilMultiplier, 0f, 5f);
                DevCheats.RecoilForwardMultiplier   = EditorGUILayout.Slider("  Forward ×", DevCheats.RecoilForwardMultiplier, 0f, 5f);
                DevCheats.RecoilSideMultiplier      = EditorGUILayout.Slider("  Side ×", DevCheats.RecoilSideMultiplier, 0f, 5f);
                DevCheats.RecoilRecoveryMultiplier  = EditorGUILayout.Slider("Recovery ×", DevCheats.RecoilRecoveryMultiplier, 0.1f, 5f);
            });
            DevCheats.NoRecoil = !recoilEnabled;

            // ── Player Tweaks ───────────────────────────────
            DrawFoldout(ref _foldPlayer, "Player Tweaks", () =>
            {
                DevCheats.MoveSpeedMultiplier = EditorGUILayout.Slider("Move Speed ×", DevCheats.MoveSpeedMultiplier, 0.1f, 10f);
            });

            // ── FOV ─────────────────────────────────────────
            DrawToggleFoldout(ref _foldFOV, ref DevCheats.FOVEnabled, "FOV", () =>
            {
                DevCheats.FOVNearRadius      = EditorGUILayout.Slider("Near Radius", DevCheats.FOVNearRadius, 1f, 15f);
                DevCheats.FOVFarRadius       = EditorGUILayout.Slider("Far Radius", DevCheats.FOVFarRadius, 10f, 100f);
                DevCheats.FOVAngle           = EditorGUILayout.Slider("FOV Angle", DevCheats.FOVAngle, 30f, 360f);
                DevCheats.ForceShowAllBots   = EditorGUILayout.Toggle("Force Show All Bots", DevCheats.ForceShowAllBots);
                DevCheats.FOVOcclusionEnabled = EditorGUILayout.Toggle("FOV Occlusion", DevCheats.FOVOcclusionEnabled);
            });

            // ── Fog of War ─────────────────────────────────────
            DrawToggleFoldout(ref _foldFoW, ref DevCheats.FogOfWarEnabled, "Fog of War", () =>
            {
                DevCheats.FogBlurRadius    = EditorGUILayout.Slider("Blur Radius", DevCheats.FogBlurRadius, 0f, 10f);
                DevCheats.FogBlurIterations = EditorGUILayout.IntSlider("Blur Iterations", DevCheats.FogBlurIterations, 1, 6);
                DevCheats.FogIntensity     = EditorGUILayout.Slider("Fog Intensity", DevCheats.FogIntensity, 0f, 1f);
                DevCheats.FogDesaturation  = EditorGUILayout.Slider("Desaturation", DevCheats.FogDesaturation, 0f, 1f);
                DevCheats.FogColor         = EditorGUILayout.ColorField("Fog Color", DevCheats.FogColor);
                DevCheats.FoWRTScale       = EditorGUILayout.IntSlider("RT Resolution", DevCheats.FoWRTScale, 64, 1024);
                DevCheats.FOVRayStep       = EditorGUILayout.Slider("Ray Step (°)", DevCheats.FOVRayStep, 0.5f, 5f);
                DevCheats.FogTemporalBlend = EditorGUILayout.Slider("Temporal Blend", DevCheats.FogTemporalBlend, 0.05f, 1f);
            });

            // ── Aim Split ─────────────────────────────────────
            DrawToggleFoldout(ref _foldAim, ref DevCheats.AimSplitEnabled, "Aim Split", () =>
            {
                DevCheats.AimFollowMultiplier = EditorGUILayout.Slider("Follow Speed ×", DevCheats.AimFollowMultiplier, 0.1f, 5f);
            });

            EditorGUILayout.Space(8);

            // ── Status Effects ───────────────────────────────
            EditorGUILayout.LabelField("Status Effects", EditorStyles.boldLabel);
            if (GUILayout.Button("Apply Bleed to Player"))
                DevCheats.ForceBleedPlayer = true;

            EditorGUILayout.Space(12);

            // ── Reset ───────────────────────────────────────
            if (GUILayout.Button("Reset All to Defaults"))
                DevCheats.Reset();

            EditorGUILayout.EndScrollView();
        }

        // ── Helpers ─────────────────────────────────────────

        /// <summary>Collapsible foldout group.</summary>
        void DrawFoldout(ref bool foldout, string title, System.Action drawContent)
        {
            EditorGUILayout.Space(4);
            var newFold = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (newFold != foldout)
            {
                foldout = newFold;
                SaveFoldouts();
            }

            if (foldout)
            {
                EditorGUI.indentLevel++;
                drawContent();
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>Foldout with enable toggle. Content grayed out when disabled.</summary>
        void DrawToggleFoldout(ref bool foldout, ref bool enabled, string title, System.Action drawContent)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            var newFold = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (newFold != foldout)
            {
                foldout = newFold;
                SaveFoldouts();
            }

            enabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(16));
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    EditorGUI.indentLevel++;
                    drawContent();
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
