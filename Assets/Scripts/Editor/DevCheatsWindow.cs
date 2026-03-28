using Dev;
using Systems;
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
        static bool _foldADS;
        static bool _foldHealthBar;
        static bool _foldParallax;
        static bool _foldDamageNumbers;
        static bool _foldQuests;

        Vector2 _scroll;
        SerializedObject _so;
        DevCheatsConfig _config;
        string _questIdInput = "";

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
            _foldADS       = EditorPrefs.GetBool("DevCheats_foldADS", false);
            _foldHealthBar = EditorPrefs.GetBool("DevCheats_foldHealthBar", false);
            _foldParallax  = EditorPrefs.GetBool("DevCheats_foldParallax", false);
            _foldDamageNumbers = EditorPrefs.GetBool("DevCheats_foldDamageNumbers", false);
            _foldQuests = EditorPrefs.GetBool("DevCheats_foldQuests", false);

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
            EditorPrefs.SetBool("DevCheats_foldADS", _foldADS);
            EditorPrefs.SetBool("DevCheats_foldHealthBar", _foldHealthBar);
            EditorPrefs.SetBool("DevCheats_foldParallax", _foldParallax);
            EditorPrefs.SetBool("DevCheats_foldDamageNumbers", _foldDamageNumbers);
            EditorPrefs.SetBool("DevCheats_foldQuests", _foldQuests);
        }

        void MarkDirty()
        {
            if (_config == null) return;
            // Mark all section assets dirty so changes persist independently
            EditorUtility.SetDirty(_config);
            if (_config.Cheats) EditorUtility.SetDirty(_config.Cheats);
            if (_config.Weapon) EditorUtility.SetDirty(_config.Weapon);
            if (_config.Recoil) EditorUtility.SetDirty(_config.Recoil);
            if (_config.Aim) EditorUtility.SetDirty(_config.Aim);
            if (_config.Player) EditorUtility.SetDirty(_config.Player);
            if (_config.FOV) EditorUtility.SetDirty(_config.FOV);
            if (_config.Fog) EditorUtility.SetDirty(_config.Fog);
            if (_config.Crosshair) EditorUtility.SetDirty(_config.Crosshair);
            if (_config.ADS) EditorUtility.SetDirty(_config.ADS);
            if (_config.HealthBar) EditorUtility.SetDirty(_config.HealthBar);
            if (_config.Parallax) EditorUtility.SetDirty(_config.Parallax);
            if (_config.StatusEffects) EditorUtility.SetDirty(_config.StatusEffects);
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

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Hit Markers", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "Scale: global size multiplier for all hit markers.\n" +
                    "Duration: how long hit/kill markers stay visible.\n" +
                    "Line Length / Gap / Expand: X-shape geometry.",
                    MessageType.None);
                DevCheats.HitMarkerScale      = EditorGUILayout.Slider("Scale ×", DevCheats.HitMarkerScale, 0.5f, 3f);
                DevCheats.HitDuration         = EditorGUILayout.Slider("Hit Duration", DevCheats.HitDuration, 0.1f, 1f);
                DevCheats.KillDuration        = EditorGUILayout.Slider("Kill Duration", DevCheats.KillDuration, 0.1f, 2f);
                DevCheats.HitLineLength       = EditorGUILayout.Slider("Hit Line Length", DevCheats.HitLineLength, 4f, 40f);
                DevCheats.KillLineLength      = EditorGUILayout.Slider("Kill Line Length", DevCheats.KillLineLength, 4f, 40f);
                DevCheats.HitGapStart         = EditorGUILayout.Slider("Gap Start", DevCheats.HitGapStart, 0f, 30f);
                DevCheats.HitGapExpand        = EditorGUILayout.Slider("Gap Expand", DevCheats.HitGapExpand, 0f, 40f);
                DevCheats.HitMarkerThickness  = EditorGUILayout.Slider("Thickness", DevCheats.HitMarkerThickness, 1f, 10f);
                DevCheats.HitColor            = EditorGUILayout.ColorField("Hit Color", DevCheats.HitColor);
                DevCheats.KillColor           = EditorGUILayout.ColorField("Kill Color", DevCheats.KillColor);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Headshot (Targeted Hit)", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "When player aims cursor directly at a target and hits it.\n" +
                    "Shows double X-marker: inner (normal) + outer (expanding faster).\n" +
                    "Outer Scale: size multiplier vs inner. Expand Mul: speed of outer X.",
                    MessageType.None);
                DevCheats.HeadshotDuration       = EditorGUILayout.Slider("Duration", DevCheats.HeadshotDuration, 0.1f, 2f);
                DevCheats.HeadshotOuterScale     = EditorGUILayout.Slider("Outer Scale ×", DevCheats.HeadshotOuterScale, 1f, 4f);
                DevCheats.HeadshotOuterExpandMul = EditorGUILayout.Slider("Outer Expand ×", DevCheats.HeadshotOuterExpandMul, 1f, 5f);
                DevCheats.HeadshotColor          = EditorGUILayout.ColorField("Color", DevCheats.HeadshotColor);
            });
            DevCheats.CrosshairEnabled = crosshairEnabled;

            // ── ADS ──────────────────────────────────────────
            DrawFoldout(ref _foldADS, "ADS (Aim Down Sights)", () =>
            {
                EditorGUILayout.LabelField("Transition", EditorStyles.miniLabel);
                DevCheats.AdsTransitionTime             = EditorGUILayout.Slider("Transition Time", DevCheats.AdsTransitionTime, 0.05f, 1f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Gameplay", EditorStyles.miniLabel);
                DevCheats.AdsMoveSpeedMultiplier        = EditorGUILayout.Slider("Move Speed ×", DevCheats.AdsMoveSpeedMultiplier, 0.1f, 1f);
                DevCheats.AdsAimFollowMultiplier        = EditorGUILayout.Slider("Aim Follow ×", DevCheats.AdsAimFollowMultiplier, 0.5f, 5f);
                DevCheats.AdsRecoilMultiplier           = EditorGUILayout.Slider("Recoil Kick ×", DevCheats.AdsRecoilMultiplier, 0f, 2f);
                DevCheats.AdsRecoilRecoveryMultiplier   = EditorGUILayout.Slider("Recoil Recovery ×", DevCheats.AdsRecoilRecoveryMultiplier, 0.5f, 5f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Camera", EditorStyles.miniLabel);
                DevCheats.AdsZoomFactor                 = EditorGUILayout.Slider("Zoom Factor", DevCheats.AdsZoomFactor, 0.5f, 1f);
                DevCheats.AdsCursorInfluenceMultiplier  = EditorGUILayout.Slider("Cursor Influence ×", DevCheats.AdsCursorInfluenceMultiplier, 0.5f, 3f);
                DevCheats.AdsVignetteIntensity          = EditorGUILayout.Slider("Vignette Intensity", DevCheats.AdsVignetteIntensity, 0f, 1f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Crosshair", EditorStyles.miniLabel);
                DevCheats.AdsBaseGap                    = EditorGUILayout.Slider("Base Gap", DevCheats.AdsBaseGap, 0f, 30f);
                DevCheats.AdsBloomExtraGap              = EditorGUILayout.Slider("Bloom Extra Gap", DevCheats.AdsBloomExtraGap, 0f, 50f);
            });

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

            // ── Parallax / Projectile ─────────────────────────
            DrawFoldout(ref _foldParallax, "Parallax / Projectile", () =>
            {
                EditorGUILayout.HelpBox(
                    "Spawn Height: Y position where bullet spawns (0=ground, 1.5=gun barrel).\n" +
                    "Lower = less visual offset from crosshair, but trail starts from feet area.",
                    MessageType.None);
                DevCheats.ProjectileSpawnHeight = EditorGUILayout.Slider("Spawn Height", DevCheats.ProjectileSpawnHeight, 0f, 2f);

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Parallax Correction: rotates bullet XZ direction so the screen-space trail\n" +
                    "passes through the crosshair. Small correction at low Spawn Height (~2%).\n" +
                    "Used as base direction, blended with Convergence independently.",
                    MessageType.None);
                DevCheats.ParallaxCorrection = EditorGUILayout.Toggle("Parallax Correction", DevCheats.ParallaxCorrection);

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Convergence Blend: blends between parallax direction (visual) and\n" +
                    "convergence direction (accuracy toward 3D collider under cursor).\n" +
                    "0 = full parallax (trail through crosshair).\n" +
                    "1 = full convergence (bullet exactly toward target).",
                    MessageType.None);
                DevCheats.ConvergenceBlend = EditorGUILayout.Slider("Convergence Blend", DevCheats.ConvergenceBlend, 0f, 1f);

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Aim Up on Hit: when cursor hits a character, angles bullet upward toward\n" +
                    "their upper body. Height Ratio: 0=feet, 0.5=center, 1=head.",
                    MessageType.None);
                DevCheats.ConvergenceAimUp = EditorGUILayout.Toggle("Aim Up on Hit", DevCheats.ConvergenceAimUp);
                DevCheats.AimUpHeightRatio = EditorGUILayout.Slider("  Height Ratio", DevCheats.AimUpHeightRatio, 0f, 1f);

                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Hit Radius: SphereCast radius for bullet collision. Wider = more forgiving\n" +
                    "hits, compensates for remaining parallax error.",
                    MessageType.None);
                DevCheats.ProjectileHitRadius = EditorGUILayout.Slider("Hit Radius", DevCheats.ProjectileHitRadius, 0f, 0.5f);
            });

            // ── Damage Numbers ─────────────────────────────────
            bool dmgEnabled = DevCheats.DmgNumEnabled;
            DrawToggleFoldout(ref _foldDamageNumbers, ref dmgEnabled, "Damage Numbers", () =>
            {
                EditorGUILayout.HelpBox(
                    "Floating numbers on hit. Pop animation + selectable trajectory.\n" +
                    "0=FloatUp (straight up), 1=Knockback (opposite to bullet),\n" +
                    "2=ArcGravity (knockback + gravity), 3=Scatter (random directions).",
                    MessageType.None);
                DevCheats.DmgNumTrajectoryMode = EditorGUILayout.IntSlider("Trajectory Mode", DevCheats.DmgNumTrajectoryMode, 0, 3);
                string[] modeNames = { "Float Up", "Knockback", "Arc + Gravity", "Scatter" };
                int m = Mathf.Clamp(DevCheats.DmgNumTrajectoryMode, 0, 3);
                EditorGUILayout.LabelField("  → " + modeNames[m], EditorStyles.miniLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Timing", EditorStyles.miniLabel);
                DevCheats.DmgNumDuration        = EditorGUILayout.Slider("Duration (sec)", DevCheats.DmgNumDuration, 0.3f, 3f);
                DevCheats.DmgNumFlySpeed        = EditorGUILayout.Slider("Fly Speed (px/s)", DevCheats.DmgNumFlySpeed, 10f, 300f);
                DevCheats.DmgNumGravityAccel    = EditorGUILayout.Slider("Gravity (Arc mode)", DevCheats.DmgNumGravityAccel, 0f, 500f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Pop Animation", EditorStyles.miniLabel);
                DevCheats.DmgNumPopDuration     = EditorGUILayout.Slider("Pop Duration", DevCheats.DmgNumPopDuration, 0.05f, 0.5f);
                DevCheats.DmgNumPopOvershoot    = EditorGUILayout.Slider("Pop Overshoot", DevCheats.DmgNumPopOvershoot, 1f, 2f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Size", EditorStyles.miniLabel);
                DevCheats.DmgNumBaseFontSize    = EditorGUILayout.Slider("Base Font Size", DevCheats.DmgNumBaseFontSize, 8f, 40f);
                DevCheats.DmgNumDamageScaleFactor = EditorGUILayout.Slider("Damage Scale Factor", DevCheats.DmgNumDamageScaleFactor, 1f, 50f);
                DevCheats.DmgNumRandomSpread    = EditorGUILayout.Slider("Random Spread (deg)", DevCheats.DmgNumRandomSpread, 0f, 90f);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Colors", EditorStyles.miniLabel);
                DevCheats.DmgNumNormalColor     = EditorGUILayout.ColorField("Normal", DevCheats.DmgNumNormalColor);
                DevCheats.DmgNumHeadshotColor   = EditorGUILayout.ColorField("Headshot", DevCheats.DmgNumHeadshotColor);
                DevCheats.DmgNumKillColor       = EditorGUILayout.ColorField("Kill", DevCheats.DmgNumKillColor);
            });
            DevCheats.DmgNumEnabled = dmgEnabled;

            // ── Quests ──────────────────────────────────────
            DrawFoldout(ref _foldQuests, "Quests", () =>
            {
                bool appReady = Application.isPlaying && App.App.IsInitialized;

                using (new EditorGUI.DisabledScope(!appReady))
                {
                    EditorGUILayout.BeginHorizontal();
                    _questIdInput = EditorGUILayout.TextField("Quest ID", _questIdInput);

                    if (GUILayout.Button("Complete", GUILayout.Width(80)))
                    {
                        if (appReady && !string.IsNullOrEmpty(_questIdInput))
                        {
                            var player = App.App.Instance.Player;
                            if (QuestSystem.TryComplete(player.QuestProgress, _questIdInput))
                            {
                                Debug.Log($"[DevCheats] Completed quest '{_questIdInput}'.");
                            }
                            else
                            {
                                Debug.LogWarning($"[DevCheats] Quest '{_questIdInput}' is not active.");
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (appReady)
                    {
                        var progress = App.App.Instance.Player.QuestProgress;
                        int active = 0, completed = 0;
                        foreach (var kvp in progress.All)
                        {
                            if (kvp.Value.Status == State.QuestStatus.Active) active++;
                            else if (kvp.Value.Status == State.QuestStatus.Completed) completed++;
                        }
                        EditorGUILayout.LabelField($"Active: {active}  |  Completed: {completed}",
                            EditorStyles.miniLabel);
                    }
                }

                if (!appReady)
                    EditorGUILayout.HelpBox("Enter Play Mode to use quest cheats.", MessageType.Info);
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
            const string folder = "Assets/Resources/Configs";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Resources", "Configs");

            var asset = ScriptableObject.CreateInstance<DevCheatsConfig>();
            AssetDatabase.CreateAsset(asset, folder + "/DevCheatsConfig.asset");
            CreateSectionAssets(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BindConfig();
            Debug.Log("[DevCheats] Created config + section assets");
        }

        [MenuItem("Window/Dev Cheats — Create Section Assets")]
        static void CreateSectionAssetsMenu()
        {
            var config = DevCheats.Config;
            if (config == null)
            {
                Debug.LogError("[DevCheats] No DevCheatsConfig found. Create it first.");
                return;
            }
            CreateSectionAssets(config);
            ApplyMigratedValues(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[DevCheats] Section assets created with migrated values.");
        }

        /// <summary>Apply values from the old monolithic DevCheatsConfig.asset (pre-refactor).</summary>
        static void ApplyMigratedValues(DevCheatsConfig config)
        {
            // Cheats
            config.Cheats.GodMode = false;
            config.Cheats.InfiniteAmmo = true;
            EditorUtility.SetDirty(config.Cheats);

            // Weapon
            config.Weapon.DamageMultiplier = 1f;
            config.Weapon.ProjectileSpeedMultiplier = 5.5f;
            config.Weapon.FireRateMultiplier = 1f;
            EditorUtility.SetDirty(config.Weapon);

            // Recoil
            config.Recoil.NoRecoil = false;
            config.Recoil.RecoilMultiplier = 3f;
            config.Recoil.RecoilForwardMultiplier = 1f;
            config.Recoil.RecoilSideMultiplier = 1f;
            config.Recoil.RecoilRecoveryMultiplier = 3f;
            EditorUtility.SetDirty(config.Recoil);

            // Aim
            config.Aim.AimSplitEnabled = true;
            config.Aim.AimFollowMultiplier = 1f;
            EditorUtility.SetDirty(config.Aim);

            // Player
            config.Player.MoveSpeedMultiplier = 1f;
            EditorUtility.SetDirty(config.Player);

            // FOV
            config.FOV.FOVEnabled = true;
            config.FOV.FOVNearRadius = 3.5f;
            config.FOV.FOVFarRadius = 33.1f;
            config.FOV.FOVAngle = 95f;
            config.FOV.ForceShowAllBots = false;
            config.FOV.FOVOcclusionEnabled = true;
            EditorUtility.SetDirty(config.FOV);

            // Fog
            config.Fog.FogOfWarEnabled = true;
            config.Fog.FogBlurRadius = 3.31f;
            config.Fog.FogBlurIterations = 3;
            config.Fog.FogIntensity = 0.6f;
            config.Fog.FogDesaturation = 0f;
            config.Fog.FogColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            config.Fog.FoWRTScale = 256;
            config.Fog.FOVRayStep = 2f;
            config.Fog.FogTemporalBlend = 0.2f;
            EditorUtility.SetDirty(config.Fog);

            // Crosshair
            config.Crosshair.CrosshairEnabled = true;
            config.Crosshair.CrosshairLineLength = 24f;
            config.Crosshair.CrosshairLineThickness = 6f;
            config.Crosshair.CrosshairBaseGap = 15f;
            config.Crosshair.CrosshairCenterDotSize = 9f;
            config.Crosshair.CrosshairBloomExtraGap = 30f;
            config.Crosshair.CrosshairNormalColor = new Color(0.2f, 1f, 0.3f, 0.9f);
            config.Crosshair.CrosshairWarningColor = new Color(1f, 0.25f, 0.2f, 0.9f);
            config.Crosshair.CrosshairBloomColor = new Color(1f, 1f, 1f, 0.95f);
            config.Crosshair.HitMarkerScale = 1.49f;
            config.Crosshair.HitDuration = 0.3f;
            config.Crosshair.KillDuration = 0.5f;
            config.Crosshair.HitLineLength = 14f;
            config.Crosshair.KillLineLength = 18f;
            config.Crosshair.HitGapStart = 8f;
            config.Crosshair.HitGapExpand = 14f;
            config.Crosshair.HitMarkerThickness = 4f;
            config.Crosshair.HitColor = Color.white;
            config.Crosshair.KillColor = new Color(1f, 0.15f, 0.15f, 1f);
            config.Crosshair.HeadshotOuterScale = 1.25f;
            config.Crosshair.HeadshotOuterExpandMul = 1.62f;
            config.Crosshair.HeadshotDuration = 0.5f;
            config.Crosshair.HeadshotColor = new Color(1f, 0.85f, 0.2f, 1f);
            EditorUtility.SetDirty(config.Crosshair);

            // ADS
            config.ADS.AdsTransitionTime = 0.18f;
            config.ADS.AdsMoveSpeedMultiplier = 0.7f;
            config.ADS.AdsAimFollowMultiplier = 1.5f;
            config.ADS.AdsRecoilMultiplier = 0.6f;
            config.ADS.AdsRecoilRecoveryMultiplier = 1.5f;
            config.ADS.AdsZoomFactor = 0.947f;
            config.ADS.AdsCursorInfluenceMultiplier = 2.36f;
            config.ADS.AdsBaseGap = 10.4f;
            config.ADS.AdsBloomExtraGap = 7.8f;
            config.ADS.AdsVignetteIntensity = 0.471f;
            EditorUtility.SetDirty(config.ADS);

            // Health Bar
            config.HealthBar.HBarWidth = 1.4f;
            config.HealthBar.HBarHeight = 0.181f;
            config.HealthBar.HBarOffsetY = 2.48f;
            config.HealthBar.HBarBorderSize = 0.1182f;
            config.HealthBar.HBarTrailDelay = 0.25f;
            config.HealthBar.HBarTrailSpeed = 2f;
            config.HealthBar.HBarFlashDuration = 0.62f;
            config.HealthBar.HBarFlashExpandX = 1f;
            config.HealthBar.HBarFlashExpandY = 2f;
            config.HealthBar.HBarFlashPower = 6.73f;
            config.HealthBar.HBarShakeIntensity = 0.1f;
            config.HealthBar.HBarShakeDuration = 0.25f;
            config.HealthBar.HBarShakeFrequency = 19.5f;
            config.HealthBar.HBarHpPerSegment = 5f;
            config.HealthBar.HBarSegmentLineWidth = 0.012f;
            config.HealthBar.HBarSegmentLineColor = new Color(0f, 0f, 0f, 0.4f);
            config.HealthBar.HBarTrailColor = Color.white;
            config.HealthBar.HBarFlashColor = Color.white;
            config.HealthBar.HBarBgColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            EditorUtility.SetDirty(config.HealthBar);

            // Parallax
            config.Parallax.ProjectileSpawnHeight = 0.606f;
            config.Parallax.ParallaxCorrection = true;
            config.Parallax.ConvergenceBlend = 0.317f;
            config.Parallax.ConvergenceAimUp = true;
            config.Parallax.AimUpHeightRatio = 0.833f;
            config.Parallax.ProjectileHitRadius = 0f;
            EditorUtility.SetDirty(config.Parallax);

            // Status Effects
            config.StatusEffects.ForceBleedPlayer = false;
            EditorUtility.SetDirty(config.StatusEffects);
        }

        static void CreateSectionAssets(DevCheatsConfig config)
        {
            const string folder = "Assets/Resources/Configs/DevCheats";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/Configs"))
                    AssetDatabase.CreateFolder("Assets/Resources", "Configs");
                AssetDatabase.CreateFolder("Assets/Resources/Configs", "DevCheats");
            }

            // Use SerializedObject to set references on config
            var so = new SerializedObject(config);

            CreateSectionIfMissing<DevCheatsCheatsSection>(so, "_cheats", folder, "Cheats");
            CreateSectionIfMissing<DevCheatsWeaponSection>(so, "_weapon", folder, "Weapon");
            CreateSectionIfMissing<DevCheatsRecoilSection>(so, "_recoil", folder, "Recoil");
            CreateSectionIfMissing<DevCheatsAimSection>(so, "_aim", folder, "Aim");
            CreateSectionIfMissing<DevCheatsPlayerSection>(so, "_player", folder, "Player");
            CreateSectionIfMissing<DevCheatsFOVSection>(so, "_fov", folder, "FOV");
            CreateSectionIfMissing<DevCheatsFogSection>(so, "_fog", folder, "Fog");
            CreateSectionIfMissing<DevCheatsCrosshairSection>(so, "_crosshair", folder, "Crosshair");
            CreateSectionIfMissing<DevCheatsADSSection>(so, "_ads", folder, "ADS");
            CreateSectionIfMissing<DevCheatsHealthBarSection>(so, "_healthBar", folder, "HealthBar");
            CreateSectionIfMissing<DevCheatsParallaxSection>(so, "_parallax", folder, "Parallax");
            CreateSectionIfMissing<DevCheatsDamageNumberSection>(so, "_damageNumbers", folder, "DamageNumbers");
            CreateSectionIfMissing<DevCheatsStatusEffectsSection>(so, "_statusEffects", folder, "StatusEffects");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateSectionIfMissing<T>(SerializedObject so, string propName, string folder, string assetName) where T : ScriptableObject
        {
            var prop = so.FindProperty(propName);
            var path = $"{folder}/{assetName}.asset";

            // Check if a persisted asset already exists on disk
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                // Always re-assign (in case the reference was lost or replaced by in-memory fallback)
                prop.objectReferenceValue = existing;
                Debug.Log($"[DevCheats] Linked existing {path}");
                return;
            }

            // Create new asset
            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            prop.objectReferenceValue = instance;
            Debug.Log($"[DevCheats] Created {path}");
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
