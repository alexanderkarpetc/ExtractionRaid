using System.Collections.Generic;
using Dev;
using Systems;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class DevCheatsWindow : EditorWindow
    {
        Vector2 _scroll;
        SerializedObject _so;
        DevCheatsConfig _config;
        string _questIdInput = "";

        // Section foldout states (persisted via EditorPrefs)
        readonly Dictionary<string, bool> _foldouts = new();

        // Cached inline editors per section SO (recreated when config changes)
        readonly Dictionary<string, UnityEditor.Editor> _sectionEditors = new();

        [MenuItem("Window/Dev Cheats")]
        static void Open()
        {
            GetWindow<DevCheatsWindow>("Dev Cheats");
        }

        void OnEnable()
        {
            BindConfig();
        }

        void OnDisable()
        {
            ClearEditors();
        }

        void BindConfig()
        {
            ClearEditors();
            _config = DevCheats.Config;
            if (_config != null)
                _so = new SerializedObject(_config);
        }

        void ClearEditors()
        {
            foreach (var e in _sectionEditors.Values)
                if (e != null) DestroyImmediate(e);
            _sectionEditors.Clear();
        }

        bool GetFoldout(string key)
        {
            if (!_foldouts.ContainsKey(key))
                _foldouts[key] = EditorPrefs.GetBool("DevCheats_fold_" + key, false);
            return _foldouts[key];
        }

        void SetFoldout(string key, bool value)
        {
            _foldouts[key] = value;
            EditorPrefs.SetBool("DevCheats_fold_" + key, value);
        }

        UnityEditor.Editor GetOrCreateEditor(string key, ScriptableObject target)
        {
            if (target == null) return null;
            if (_sectionEditors.TryGetValue(key, out var editor) && editor != null && editor.target == target)
                return editor;

            if (editor != null) DestroyImmediate(editor);
            editor = UnityEditor.Editor.CreateEditor(target);
            _sectionEditors[key] = editor;
            return editor;
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

            // ── Sections (auto-rendered via inline editors) ──
            DrawSection("Cheats", _config.Cheats);
            DrawSection("Weapon", _config.Weapon);
            DrawSection("Recoil", _config.Recoil);
            DrawSection("Aim", _config.Aim);
            DrawSection("Player", _config.Player);
            DrawSection("FOV", _config.FOV);
            DrawSection("Fog", _config.Fog);
            DrawSection("Crosshair", _config.Crosshair);
            DrawSection("ADS", _config.ADS);
            DrawSection("Health Bar", _config.HealthBar);
            DrawSection("Parallax", _config.Parallax);
            DrawSection("Damage Numbers", _config.DamageNumbers);
            DrawSection("Armor", _config.Armor);
            DrawSection("Status Effects", _config.StatusEffects);

            EditorGUILayout.Space(8);

            // ── Quests (custom — needs runtime App access) ────
            DrawQuestsSection();

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }

        void DrawSection(string title, ScriptableObject section)
        {
            EditorGUILayout.Space(4);
            bool fold = GetFoldout(title);
            var newFold = EditorGUILayout.Foldout(fold, title, true, EditorStyles.foldoutHeader);
            if (newFold != fold) SetFoldout(title, newFold);

            if (!newFold) return;

            if (section == null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox($"Section asset missing. Run Window → Dev Cheats — Create Section Assets.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            var editor = GetOrCreateEditor(title, section);
            if (editor == null) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(section);
            EditorGUI.indentLevel--;
        }

        void DrawQuestsSection()
        {
            EditorGUILayout.Space(4);
            bool fold = GetFoldout("Quests");
            var newFold = EditorGUILayout.Foldout(fold, "Quests", true, EditorStyles.foldoutHeader);
            if (newFold != fold) SetFoldout("Quests", newFold);
            if (!newFold) return;

            EditorGUI.indentLevel++;
            bool appReady = Application.isPlaying && App.App.IsInitialized;

            using (new EditorGUI.DisabledScope(!appReady))
            {
                EditorGUILayout.BeginHorizontal();
                _questIdInput = EditorGUILayout.TextField("Quest ID", _questIdInput);

                if (GUILayout.Button("Fulfill", GUILayout.Width(80)))
                {
                    if (appReady && !string.IsNullOrEmpty(_questIdInput))
                    {
                        var player = App.App.Instance.Player;
                        var db = App.App.Instance.QuestDatabase;
                        if (QuestSystem.TryFulfillTasks(player.QuestProgress, db, _questIdInput))
                            Debug.Log($"[DevCheats] Fulfilled all tasks for quest '{_questIdInput}'. Claim reward at NPC.");
                        else
                            Debug.LogWarning($"[DevCheats] Quest '{_questIdInput}' is not active.");
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

            EditorGUI.indentLevel--;
        }

        // ── Asset creation helpers ─────────────────────────────

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
            config.Cheats.GodMode = false;
            config.Cheats.InfiniteAmmo = true;
            EditorUtility.SetDirty(config.Cheats);

            config.Weapon.DamageMultiplier = 1f;
            config.Weapon.ProjectileSpeedMultiplier = 5.5f;
            config.Weapon.FireRateMultiplier = 1f;
            EditorUtility.SetDirty(config.Weapon);

            config.Recoil.NoRecoil = false;
            config.Recoil.RecoilMultiplier = 3f;
            config.Recoil.RecoilForwardMultiplier = 1f;
            config.Recoil.RecoilSideMultiplier = 1f;
            config.Recoil.RecoilRecoveryMultiplier = 3f;
            EditorUtility.SetDirty(config.Recoil);

            config.Aim.AimSplitEnabled = true;
            config.Aim.AimFollowMultiplier = 1f;
            EditorUtility.SetDirty(config.Aim);

            config.Player.MoveSpeedMultiplier = 1f;
            EditorUtility.SetDirty(config.Player);

            config.FOV.FOVEnabled = true;
            config.FOV.FOVNearRadius = 3.5f;
            config.FOV.FOVFarRadius = 33.1f;
            config.FOV.FOVAngle = 95f;
            config.FOV.ForceShowAllBots = false;
            config.FOV.FOVOcclusionEnabled = true;
            EditorUtility.SetDirty(config.FOV);

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

            config.Parallax.ProjectileSpawnHeight = 0.606f;
            config.Parallax.ParallaxCorrection = true;
            config.Parallax.ConvergenceBlend = 0.317f;
            config.Parallax.ConvergenceAimUp = true;
            config.Parallax.AimUpHeightRatio = 0.833f;
            config.Parallax.ProjectileHitRadius = 0f;
            EditorUtility.SetDirty(config.Parallax);

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
            CreateSectionIfMissing<DevCheatsArmorSection>(so, "_armor", folder, "Armor");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateSectionIfMissing<T>(SerializedObject so, string propName, string folder, string assetName) where T : ScriptableObject
        {
            var prop = so.FindProperty(propName);
            var path = $"{folder}/{assetName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                prop.objectReferenceValue = existing;
                Debug.Log($"[DevCheats] Linked existing {path}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            prop.objectReferenceValue = instance;
            Debug.Log($"[DevCheats] Created {path}");
        }
    }
}
