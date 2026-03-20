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
        static bool _foldCrosshair;
        static bool _foldHealthBar;

        Vector2 _scroll;
        SerializedObject _so;
        DevCheatsConfig _config;

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
            _foldCrosshair = EditorPrefs.GetBool("DevCheats_foldCrosshair", false);
            _foldHealthBar = EditorPrefs.GetBool("DevCheats_foldHealthBar", false);

            BindConfig();
        }

        void BindConfig()
        {
            _config = DevCheats.Config;
            if (_config != null)
                _so = new SerializedObject(_config);
        }

        void SaveFoldouts()
        {
            EditorPrefs.SetBool("DevCheats_foldWeapon", _foldWeapon);
            EditorPrefs.SetBool("DevCheats_foldRecoil", _foldRecoil);
            EditorPrefs.SetBool("DevCheats_foldPlayer", _foldPlayer);
            EditorPrefs.SetBool("DevCheats_foldFOV", _foldFOV);
            EditorPrefs.SetBool("DevCheats_foldFoW", _foldFoW);
            EditorPrefs.SetBool("DevCheats_foldAim", _foldAim);
            EditorPrefs.SetBool("DevCheats_foldCrosshair", _foldCrosshair);
            EditorPrefs.SetBool("DevCheats_foldHealthBar", _foldHealthBar);
        }

        void MarkDirty()
        {
            if (_config != null)
                EditorUtility.SetDirty(_config);
        }

        void OnGUI()
        {
            if (_so == null || _so.targetObject == null)
            {
                EditorGUILayout.HelpBox(
                    "DevCheatsConfig asset not found.\nCreate it via Assets → Create → Dev → Cheats Config\nand place in a Resources folder.",
                    MessageType.Warning);

                if (GUILayout.Button("Create in Resources"))
                    CreateConfigAsset();

                return;
            }

            _so.Update();

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
            bool fovEnabled = DevCheats.FOVEnabled;
            DrawToggleFoldout(ref _foldFOV, ref fovEnabled, "FOV", () =>
            {
                DevCheats.FOVNearRadius      = EditorGUILayout.Slider("Near Radius", DevCheats.FOVNearRadius, 1f, 15f);
                DevCheats.FOVFarRadius       = EditorGUILayout.Slider("Far Radius", DevCheats.FOVFarRadius, 10f, 100f);
                DevCheats.FOVAngle           = EditorGUILayout.Slider("FOV Angle", DevCheats.FOVAngle, 30f, 360f);
                DevCheats.ForceShowAllBots   = EditorGUILayout.Toggle("Force Show All Bots", DevCheats.ForceShowAllBots);
                DevCheats.FOVOcclusionEnabled = EditorGUILayout.Toggle("FOV Occlusion", DevCheats.FOVOcclusionEnabled);
            });
            DevCheats.FOVEnabled = fovEnabled;

            // ── Fog of War ─────────────────────────────────────
            bool fowEnabled = DevCheats.FogOfWarEnabled;
            DrawToggleFoldout(ref _foldFoW, ref fowEnabled, "Fog of War", () =>
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
            DevCheats.FogOfWarEnabled = fowEnabled;

            // ── Aim Split ─────────────────────────────────────
            bool aimEnabled = DevCheats.AimSplitEnabled;
            DrawToggleFoldout(ref _foldAim, ref aimEnabled, "Aim Split", () =>
            {
                DevCheats.AimFollowMultiplier = EditorGUILayout.Slider("Follow Speed ×", DevCheats.AimFollowMultiplier, 0.1f, 5f);
            });
            DevCheats.AimSplitEnabled = aimEnabled;

            // ── Crosshair ─────────────────────────────────────
            bool crosshairEnabled = DevCheats.CrosshairEnabled;
            DrawToggleFoldout(ref _foldCrosshair, ref crosshairEnabled, "Crosshair", () =>
            {
                DevCheats.CrosshairLineLength    = EditorGUILayout.Slider("Line Length", DevCheats.CrosshairLineLength, 4f, 60f);
                DevCheats.CrosshairLineThickness = EditorGUILayout.Slider("Line Thickness", DevCheats.CrosshairLineThickness, 1f, 16f);
                DevCheats.CrosshairBaseGap       = EditorGUILayout.Slider("Base Gap", DevCheats.CrosshairBaseGap, 0f, 40f);
                DevCheats.CrosshairCenterDotSize  = EditorGUILayout.Slider("Center Dot Size", DevCheats.CrosshairCenterDotSize, 0f, 20f);
                DevCheats.CrosshairBloomExtraGap = EditorGUILayout.Slider("Bloom Extra Gap", DevCheats.CrosshairBloomExtraGap, 0f, 80f);
                DevCheats.CrosshairNormalColor   = EditorGUILayout.ColorField("Normal Color", DevCheats.CrosshairNormalColor);
                DevCheats.CrosshairWarningColor  = EditorGUILayout.ColorField("Warning Color", DevCheats.CrosshairWarningColor);
                DevCheats.CrosshairBloomColor    = EditorGUILayout.ColorField("Bloom Color", DevCheats.CrosshairBloomColor);
            });
            DevCheats.CrosshairEnabled = crosshairEnabled;

            // ── Health Bar ─────────────────────────────────────
            DrawFoldout(ref _foldHealthBar, "Health Bar", () =>
            {
                EditorGUILayout.LabelField("Layout", EditorStyles.miniLabel);
                DevCheats.HBarWidth          = EditorGUILayout.Slider("Width", DevCheats.HBarWidth, 0.2f, 3f);
                DevCheats.HBarHeight         = EditorGUILayout.Slider("Height", DevCheats.HBarHeight, 0.02f, 0.5f);
                DevCheats.HBarOffsetY        = EditorGUILayout.Slider("Offset Y", DevCheats.HBarOffsetY, 0f, 5f);
                DevCheats.HBarBorderSize     = EditorGUILayout.Slider("Border Size", DevCheats.HBarBorderSize, 0f, 0.15f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Animation", EditorStyles.miniLabel);
                DevCheats.HBarTrailDelay     = EditorGUILayout.Slider("Trail Delay", DevCheats.HBarTrailDelay, 0f, 1f);
                DevCheats.HBarTrailSpeed     = EditorGUILayout.Slider("Trail Speed", DevCheats.HBarTrailSpeed, 0.1f, 10f);
                DevCheats.HBarFlashDuration  = EditorGUILayout.Slider("Flash Duration", DevCheats.HBarFlashDuration, 0.1f, 2f);
                DevCheats.HBarFlashExpandX   = EditorGUILayout.Slider("Flash Expand X", DevCheats.HBarFlashExpandX, 0f, 10f);
                DevCheats.HBarFlashExpandY   = EditorGUILayout.Slider("Flash Expand Y", DevCheats.HBarFlashExpandY, 0f, 10f);
                DevCheats.HBarFlashPower     = EditorGUILayout.Slider("Flash Power", DevCheats.HBarFlashPower, 0.5f, 10f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Shake", EditorStyles.miniLabel);
                DevCheats.HBarShakeIntensity = EditorGUILayout.Slider("Shake Intensity", DevCheats.HBarShakeIntensity, 0f, 0.3f);
                DevCheats.HBarShakeDuration  = EditorGUILayout.Slider("Shake Duration", DevCheats.HBarShakeDuration, 0.05f, 1f);
                DevCheats.HBarShakeFrequency = EditorGUILayout.Slider("Shake Frequency", DevCheats.HBarShakeFrequency, 5f, 60f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Segments", EditorStyles.miniLabel);
                DevCheats.HBarHpPerSegment      = EditorGUILayout.Slider("HP per Segment", DevCheats.HBarHpPerSegment, 5f, 100f);
                DevCheats.HBarSegmentLineWidth  = EditorGUILayout.Slider("Segment Line Width", DevCheats.HBarSegmentLineWidth, 0.001f, 0.05f);
                DevCheats.HBarSegmentLineColor  = EditorGUILayout.ColorField("Segment Line Color", DevCheats.HBarSegmentLineColor);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Colors", EditorStyles.miniLabel);
                DevCheats.HBarTrailColor     = EditorGUILayout.ColorField("Trail Color", DevCheats.HBarTrailColor);
                DevCheats.HBarFlashColor     = EditorGUILayout.ColorField("Flash Color", DevCheats.HBarFlashColor);
                DevCheats.HBarBgColor        = EditorGUILayout.ColorField("Background Color", DevCheats.HBarBgColor);
            });

            EditorGUILayout.Space(8);

            // ── Status Effects ───────────────────────────────
            EditorGUILayout.LabelField("Status Effects", EditorStyles.boldLabel);
            if (GUILayout.Button("Apply Bleed to Player"))
                DevCheats.ForceBleedPlayer = true;

            EditorGUILayout.EndScrollView();

            // Auto-save: if anything changed, mark dirty
            if (GUI.changed)
                MarkDirty();

            _so.ApplyModifiedProperties();
        }

        // ── Helpers ─────────────────────────────────────────

        void CreateConfigAsset()
        {
            const string folder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<DevCheatsConfig>();
            AssetDatabase.CreateAsset(asset, folder + "/DevCheatsConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-bind to the newly created asset
            BindConfig();
            Debug.Log("[DevCheats] Created config asset at " + folder + "/DevCheatsConfig.asset");
        }

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
