using System.Collections.Generic;
using Dev;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Editor window для <see cref="ViewCheatsConfig"/>. Mirrors <see cref="DevCheatsWindow"/>
    /// architecture — auto-rendered inline editors per section, foldouts persisted у
    /// EditorPrefs, "Create Section Assets" menu для bootstrap fresh clones.
    /// </summary>
    public class ViewCheatsWindow : EditorWindow
    {
        Vector2 _scroll;
        SerializedObject _so;
        ViewCheatsConfig _config;

        readonly Dictionary<string, bool> _foldouts = new();
        readonly Dictionary<string, UnityEditor.Editor> _sectionEditors = new();

        [MenuItem("Window/View Cheats")]
        static void Open()
        {
            GetWindow<ViewCheatsWindow>();
        }

        void OnEnable()
        {
            BindConfig();
            // Tab title з camera icon — better tab discoverability у crowded editor.
            var icon = EditorGUIUtility.IconContent("Camera Icon").image
                       ?? EditorGUIUtility.IconContent("SceneViewCamera").image;
            titleContent = new GUIContent("View Cheats", icon, "View-layer polish tweaks (camera shake, hit feedback, post-FX)");
        }
        void OnDisable() => ClearEditors();

        void BindConfig()
        {
            ClearEditors();
            _config = ViewCheats.Config;
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
            if (_foldouts.TryGetValue(key, out var v)) return v;
            v = EditorPrefs.GetBool($"ViewCheatsWindow.fold.{key}", true);
            _foldouts[key] = v;
            return v;
        }

        void SetFoldout(string key, bool value)
        {
            _foldouts[key] = value;
            EditorPrefs.SetBool($"ViewCheatsWindow.fold.{key}", value);
        }

        UnityEditor.Editor GetOrCreateEditor(string key, Object target)
        {
            if (_sectionEditors.TryGetValue(key, out var editor) && editor != null && editor.target == target)
                return editor;
            if (editor != null) DestroyImmediate(editor);
            editor = UnityEditor.Editor.CreateEditor(target);
            _sectionEditors[key] = editor;
            return editor;
        }

        void OnGUI()
        {
            DrawHeaderBanner();

            if (_so == null || _so.targetObject == null)
            {
                EditorGUILayout.HelpBox(
                    "ViewCheatsConfig asset not found.\nClick \"Create in Resources\" to materialize, " +
                    "or use Window → View Cheats — Create Section Assets.",
                    MessageType.Warning);

                if (GUILayout.Button("Create in Resources"))
                    CreateConfigAsset();

                return;
            }

            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── Sections (auto-rendered via inline editors) ──
            DrawSection("🎬 Camera Shake", _config.CameraShake);
            DrawSection("🩸 Blood Decals", _config.BloodDecal);
            DrawSection("🔫 Bullet Holes", _config.BulletHole);
            DrawSection("🥃 Casings", _config.Casings);
            DrawSection("💀 Ragdoll", _config.Ragdoll);
            DrawSection("🔻 Weapon Drop", _config.WeaponDrop);

            EditorGUILayout.EndScrollView();
            _so.ApplyModifiedProperties();
        }

        // ── Header banner ─────────────────────────────────────

        // Cool-blue tint distinguishes ViewCheats from DevCheats при погляді у crowded editor.
        static readonly Color HeaderColor = new(0.18f, 0.32f, 0.48f, 1f);
        static readonly Color HeaderAccent = new(0.36f, 0.62f, 0.92f, 1f);

        void DrawHeaderBanner()
        {
            const float bannerHeight = 56f;
            var rect = EditorGUILayout.GetControlRect(false, bannerHeight);

            // Background fill
            EditorGUI.DrawRect(rect, HeaderColor);
            // Bottom accent strip
            var accentRect = new Rect(rect.x, rect.yMax - 2, rect.width, 2);
            EditorGUI.DrawRect(accentRect, HeaderAccent);

            // Icon
            var iconImage = EditorGUIUtility.IconContent("Camera Icon").image
                            ?? EditorGUIUtility.IconContent("SceneViewCamera").image;
            if (iconImage != null)
            {
                var iconRect = new Rect(rect.x + 12, rect.y + 8, 40, 40);
                GUI.DrawTexture(iconRect, iconImage, ScaleMode.ScaleToFit);
            }

            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal   = { textColor = Color.white },
            };
            var titleRect = new Rect(rect.x + 60, rect.y + 6, rect.width - 70, 24);
            GUI.Label(titleRect, "View Cheats", titleStyle);

            // Tagline
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal     = { textColor = new Color(0.85f, 0.9f, 1f, 1f) },
                wordWrap   = true,
                fontStyle  = FontStyle.Italic,
            };
            var subRect = new Rect(rect.x + 60, rect.y + 28, rect.width - 70, 28);
            GUI.Label(subRect, "View-layer polish — shake, hit feedback, post-FX. Gameplay tuning lives у Dev Cheats.", subStyle);

            EditorGUILayout.Space(4);
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
                EditorGUILayout.HelpBox(
                    "Section asset missing. Run Window → View Cheats — Create Section Assets.",
                    MessageType.Warning);
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

        // ── Asset bootstrap ───────────────────────────────────

        const string ConfigAssetPath = "Assets/Resources/Configs/ViewCheatsConfig.asset";

        void CreateConfigAsset()
        {
            EnsureFolders();
            var inst = AssetDatabase.LoadAssetAtPath<ViewCheatsConfig>(ConfigAssetPath);
            if (inst == null)
            {
                inst = CreateInstance<ViewCheatsConfig>();
                AssetDatabase.CreateAsset(inst, ConfigAssetPath);
            }
            CreateSectionAssets(inst);
            EditorUtility.SetDirty(inst);
            AssetDatabase.SaveAssets();
            BindConfig();
        }

        [MenuItem("Window/View Cheats — Create Section Assets")]
        static void CreateSectionAssetsMenu()
        {
            var config = ViewCheats.Config;
            if (config == null)
            {
                Debug.LogError("[ViewCheats] No ViewCheatsConfig found. Create it first.");
                return;
            }
            // Persist root if it's an in-memory fallback (no asset on disk yet).
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(config)))
            {
                EnsureFolders();
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
                config = AssetDatabase.LoadAssetAtPath<ViewCheatsConfig>(ConfigAssetPath);
            }
            CreateSectionAssets(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ViewCheats] Section assets created/linked. Existing values preserved.");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Configs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Configs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Configs/ViewCheats"))
                AssetDatabase.CreateFolder("Assets/Resources/Configs", "ViewCheats");
        }

        static void CreateSectionAssets(ViewCheatsConfig config)
        {
            const string folder = "Assets/Resources/Configs/ViewCheats";
            EnsureFolders();

            var so = new SerializedObject(config);

            CreateSectionIfMissing<ViewCheatsCameraShakeSection>(so, "_cameraShake", folder, "CameraShake");
            CreateSectionIfMissing<ViewCheatsBloodDecalSection>(so, "_bloodDecal", folder, "BloodDecal");
            CreateSectionIfMissing<ViewCheatsBulletHoleSection>(so, "_bulletHole", folder, "BulletHole");
            CreateSectionIfMissing<ViewCheatsCasingsSection>(so, "_casings", folder, "Casings");
            CreateSectionIfMissing<ViewCheatsRagdollSection>(so, "_ragdoll", folder, "Ragdoll");
            CreateSectionIfMissing<ViewCheatsWeaponDropSection>(so, "_weaponDrop", folder, "WeaponDrop");

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
                Debug.Log($"[ViewCheats] Linked existing {path}");
                return;
            }

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            prop.objectReferenceValue = instance;
            Debug.Log($"[ViewCheats] Created {path}");
        }
    }
}
