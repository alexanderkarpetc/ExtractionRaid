using System.Collections.Generic;
using ApplicationCore;
using Cysharp.Threading.Tasks;
using Dev;
using Systems;
using UnityEditor;
using UnityEngine;
using View.UI.CraftingMockup;

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

            // ── Raid (custom — runtime actions) ───────────────
            DrawRaidSection();

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

        void DrawRaidSection()
        {
            EditorGUILayout.Space(4);
            bool fold = GetFoldout("Raid");
            var newFold = EditorGUILayout.Foldout(fold, "Raid", true, EditorStyles.foldoutHeader);
            if (newFold != fold) SetFoldout("Raid", newFold);
            if (!newFold) return;

            EditorGUI.indentLevel++;
            bool appReady = Application.isPlaying && App.IsInitialized;

            using (new EditorGUI.DisabledScope(!appReady || App.Instance.IsInHideout))
            {
                if (GUILayout.Button("Extract to Hideout"))
                {
                    if (appReady)
                        App.Instance.ExtractToHideout().Forget();
                }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!appReady || CraftingMockupWindow.Instance == null))
            {
                if (GUILayout.Button("Toggle Crafting UI Mockup (F10)"))
                    CraftingMockupWindow.Instance?.Toggle();
            }

            if (!appReady)
                EditorGUILayout.HelpBox("Enter Play Mode to use raid cheats.", MessageType.Info);
            else if (App.Instance.IsInHideout)
                EditorGUILayout.HelpBox("Already in hideout.", MessageType.Info);

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
            bool appReady = Application.isPlaying && App.IsInitialized;

            using (new EditorGUI.DisabledScope(!appReady))
            {
                EditorGUILayout.BeginHorizontal();
                _questIdInput = EditorGUILayout.TextField("Quest ID", _questIdInput);

                if (GUILayout.Button("Fulfill", GUILayout.Width(80)))
                {
                    if (appReady && !string.IsNullOrEmpty(_questIdInput))
                    {
                        var player = App.Instance.Player;
                        var db = App.Instance.QuestDatabase;
                        if (QuestSystem.TryFulfillTasks(player.QuestProgress, db, _questIdInput))
                            Debug.Log($"[DevCheats] Fulfilled all tasks for quest '{_questIdInput}'. Claim reward at NPC.");
                        else
                            Debug.LogWarning($"[DevCheats] Quest '{_questIdInput}' is not active.");
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (appReady)
                {
                    var progress = App.Instance.Player.QuestProgress;
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
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[DevCheats] Section assets created/linked. Existing values preserved.");
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
