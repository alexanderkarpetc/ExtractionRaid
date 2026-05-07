using System.Collections.Generic;
using System.Linq;
using ApplicationCore;
using Cysharp.Threading.Tasks;
using Dev;
using State;
using Systems;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using View.UI.CraftingMockup;
using View.UI.WeaponBuilder;

namespace Editor
{
    public class DevCheatsWindow : EditorWindow
    {
        Vector2 _scroll;
        SerializedObject _so;
        DevCheatsConfig _config;
        string _questIdInput = "";
        int _spawnModuleIndex;

        // Module catalog for "Spawn Module" devcheat (Tier 6 G3). Order:
        // payload modules first, then delivery — keeps related entries grouped
        // у dropdown.
        static readonly (string Id, string DisplayName)[] SpawnableModules =
        {
            ("BallisticRound", "Ballistic Round (Payload)"),
            ("LaserCharge",    "Laser Charge (Payload)"),
            ("SingleAction",   "Single-Action (Delivery)"),
            ("Auto",           "Auto (Delivery)"),
            ("Scatter",        "Scatter (Delivery)"),
        };

        // "Give Item" devcheat state — picker chooses an ItemDefinition.Id, qty
        // controls stack count. Lands directly in Player.Inventory.Backpack so it's
        // testable both in hideout and in raid.
        string _giveItemId;
        int _giveItemCount = 1;

        // Section foldout states (persisted via EditorPrefs)
        readonly Dictionary<string, bool> _foldouts = new();

        // Cached inline editors per section SO (recreated when config changes)
        readonly Dictionary<string, UnityEditor.Editor> _sectionEditors = new();

        [MenuItem("Window/Dev Cheats")]
        static void Open()
        {
            GetWindow<DevCheatsWindow>();
        }

        void OnEnable()
        {
            BindConfig();
            // Tab title з settings icon — distinguishes от ViewCheats у crowded editor.
            var icon = EditorGUIUtility.IconContent("SettingsIcon").image
                       ?? EditorGUIUtility.IconContent("_Help").image;
            titleContent = new GUIContent("Dev Cheats", icon, "Gameplay balance, cheats, runtime actions");
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
            DrawHeaderBanner();

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
            DrawSection("💀 Cheats", _config.Cheats);
            DrawSection("🔫 Weapon", _config.Weapon);
            DrawSection("💢 Recoil", _config.Recoil);
            DrawSection("🎯 Aim", _config.Aim);
            DrawSection("🏃 Player", _config.Player);
            DrawSection("👁 FOV", _config.FOV);
            DrawSection("🌫 Fog", _config.Fog);
            DrawSection("✛ Crosshair", _config.Crosshair);
            DrawSection("🔍 ADS", _config.ADS);
            DrawSection("❤ Health Bar", _config.HealthBar);
            DrawSection("🌐 Parallax", _config.Parallax);
            DrawSection("🔢 Damage Numbers", _config.DamageNumbers);
            DrawSection("🛡 Armor", _config.Armor);
            DrawSection("💉 Status Effects", _config.StatusEffects);
            DrawSection("⏸ Hit Pause", _config.HitPause);
            DrawSection("✨ Muzzle VFX", _config.MuzzleVfx);
            DrawSection("💥 Stagger / Hit Reaction", _config.Stagger);

            EditorGUILayout.Space(8);

            // ── Raid (custom — runtime actions) ───────────────
            DrawRaidSection();

            // ── Quests (custom — needs runtime App access) ────
            DrawQuestsSection();

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }

        // ── Header banner ─────────────────────────────────────

        // Warm orange tint розрізняє DevCheats від cool-blue ViewCheats.
        static readonly Color HeaderColor  = new(0.45f, 0.28f, 0.12f, 1f);
        static readonly Color HeaderAccent = new(0.92f, 0.55f, 0.18f, 1f);

        void DrawHeaderBanner()
        {
            const float bannerHeight = 56f;
            var rect = EditorGUILayout.GetControlRect(false, bannerHeight);

            EditorGUI.DrawRect(rect, HeaderColor);
            var accentRect = new Rect(rect.x, rect.yMax - 2, rect.width, 2);
            EditorGUI.DrawRect(accentRect, HeaderAccent);

            var iconImage = EditorGUIUtility.IconContent("SettingsIcon").image
                            ?? EditorGUIUtility.IconContent("_Help").image;
            if (iconImage != null)
            {
                var iconRect = new Rect(rect.x + 12, rect.y + 8, 40, 40);
                GUI.DrawTexture(iconRect, iconImage, ScaleMode.ScaleToFit);
            }

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal   = { textColor = Color.white },
            };
            var titleRect = new Rect(rect.x + 60, rect.y + 6, rect.width - 70, 24);
            GUI.Label(titleRect, "Dev Cheats", titleStyle);

            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(1f, 0.92f, 0.78f, 1f) },
                wordWrap  = true,
                fontStyle = FontStyle.Italic,
            };
            var subRect = new Rect(rect.x + 60, rect.y + 28, rect.width - 70, 28);
            GUI.Label(subRect, "Gameplay balance, cheats, runtime actions. View polish lives у View Cheats.", subStyle);

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
                        App.Instance.RequestExtraction();
                }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!appReady || CraftingMockupWindow.Instance == null))
            {
                if (GUILayout.Button("Toggle Crafting UI Mockup (F10)"))
                    CraftingMockupWindow.Instance?.Toggle();
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!appReady || WeaponBuilderWindow.Instance == null))
            {
                if (GUILayout.Button("Toggle Weapon Builder"))
                    WeaponBuilderWindow.Instance?.Toggle();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Spawn Module", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                EditorGUILayout.BeginHorizontal();
                var displayNames = new string[SpawnableModules.Length];
                for (int i = 0; i < SpawnableModules.Length; i++)
                    displayNames[i] = SpawnableModules[i].DisplayName;
                _spawnModuleIndex = EditorGUILayout.Popup(_spawnModuleIndex, displayNames);
                if (GUILayout.Button("Spawn", GUILayout.Width(80)) && appReady)
                    SpawnModuleIntoBackpack(SpawnableModules[_spawnModuleIndex].Id);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Spawn All Modules") && appReady)
                {
                    foreach (var (id, _) in SpawnableModules)
                        SpawnModuleIntoBackpack(id);
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Spawn All Weapon Variations") && appReady)
                    SpawnAllWeaponVariations();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Give Item", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                DrawGiveItemRow();
            }

            if (!appReady)
                EditorGUILayout.HelpBox("Enter Play Mode to use raid cheats.", MessageType.Info);
            else if (App.Instance.IsInHideout)
                EditorGUILayout.HelpBox("Already in hideout.", MessageType.Info);

            EditorGUI.indentLevel--;
        }

        void DrawGiveItemRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var def = string.IsNullOrEmpty(_giveItemId) ? null : ItemDefinition.Get(_giveItemId);
                string label = def != null
                    ? $"{def.DisplayName}  ({def.Id})"
                    : "Select item…";

                var btnRect = GUILayoutUtility.GetRect(new GUIContent(label), EditorStyles.popup,
                    GUILayout.MinWidth(220), GUILayout.ExpandWidth(true));
                if (EditorGUI.DropdownButton(btnRect, new GUIContent(label), FocusType.Keyboard))
                {
                    var dropdown = new ItemPickerDropdown(new AdvancedDropdownState(), id =>
                    {
                        _giveItemId = id;
                        Repaint();
                    });
                    dropdown.Show(btnRect);
                }

                EditorGUILayout.LabelField("Qty", GUILayout.Width(28));
                _giveItemCount = Mathf.Max(1, EditorGUILayout.IntField(_giveItemCount, GUILayout.Width(60)));

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_giveItemId)))
                {
                    if (GUILayout.Button("Give", GUILayout.Width(70)))
                        GiveItem(_giveItemId, _giveItemCount);
                }
            }
        }

        static void GiveItem(string defId, int count)
        {
            var player = App.Instance?.Player;
            if (player == null)
            {
                Debug.LogWarning("[DevCheats] Cannot give item — Player not ready.");
                return;
            }

            var def = ItemDefinition.Get(defId);
            if (def == null)
            {
                Debug.LogWarning($"[DevCheats] Unknown item id '{defId}'.");
                return;
            }

            int slot = player.Inventory.FindFreeBackpackSlot();
            if (slot < 0)
            {
                Debug.LogWarning($"[DevCheats] Cannot give '{defId}' — backpack is full.");
                return;
            }

            var eid = App.Instance.AllocateEId();
            player.Inventory.Backpack[slot] = ItemState.Create(eid, defId, count);
            Debug.Log($"[DevCheats] Gave {count}× {def.DisplayName} ({defId}) to backpack slot {slot}.");
        }

        // AdvancedDropdown picker over ItemDefinition.Registry, grouped by ItemCategory.
        // Built-in search bar handles fuzzy lookup so we never type/copy raw ids.
        class ItemPickerDropdown : AdvancedDropdown
        {
            readonly System.Action<string> _onPick;

            public ItemPickerDropdown(AdvancedDropdownState state, System.Action<string> onPick)
                : base(state)
            {
                _onPick = onPick;
                minimumSize = new Vector2(320, 420);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Items");

                var byCategory = ItemDefinition.Registry.Values
                    .GroupBy(d => d.Category)
                    .OrderBy(g => g.Key.ToString());

                foreach (var group in byCategory)
                {
                    var groupItem = new AdvancedDropdownItem(group.Key.ToString());
                    foreach (var def in group.OrderBy(d => d.DisplayName))
                        groupItem.AddChild(new ItemEntry(def));
                    root.AddChild(groupItem);
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is ItemEntry entry)
                    _onPick?.Invoke(entry.ItemId);
            }

            class ItemEntry : AdvancedDropdownItem
            {
                public string ItemId { get; }
                public ItemEntry(ItemDefinition def)
                    : base($"{def.DisplayName}  ({def.Id})")
                {
                    ItemId = def.Id;
                }
            }
        }

        /// <summary>
        /// Builds a fully-assembled <see cref="State.ItemState"/> for every Payload × Delivery
        /// combination in <see cref="State.CoreDefinitionDatabase"/> and drops them into free
        /// backpack slots. Lets QA/dev test each archetype without going through the Builder UI.
        /// All variants spawn at <see cref="State.RarityTier.Common"/>; magazines start full.
        /// </summary>
        static void SpawnAllWeaponVariations()
        {
            var inventory = App.Instance?.Player?.Inventory;
            var session   = App.Instance?.RaidSession;
            if (inventory == null || session == null)
            {
                Debug.LogWarning("[DevCheats] Cannot spawn weapon variations — Player.Inventory / RaidSession not ready.");
                return;
            }

            var db = Resources.Load<State.CoreDefinitionDatabase>("WeaponBuilder/CoreDefinitionDatabase");
            if (db == null)
            {
                Debug.LogWarning("[DevCheats] CoreDefinitionDatabase not found at Resources/WeaponBuilder/.");
                return;
            }

            int spawned = 0, skippedFull = 0;
            foreach (var payload in db.Payloads)
            {
                if (payload == null) continue;
                foreach (var delivery in db.Deliveries)
                {
                    if (delivery == null) continue;

                    int slot = inventory.FindFreeBackpackSlot();
                    if (slot < 0) { skippedFull++; continue; }

                    var deliveryStats = delivery.StatsByTier(State.RarityTier.Common);
                    var config = new State.WeaponConfiguration(
                        payload:        new State.PayloadCoreInstance(payload.Id,   State.RarityTier.Common),
                        delivery:       new State.DeliveryCoreInstance(delivery.Id, State.RarityTier.Common),
                        exotic:         null,
                        ammoInMagazine: deliveryStats.MagazineSize);

                    var eid = session.RaidState.AllocateEId();
                    inventory.Backpack[slot] = State.ItemState.CreateWeapon(eid, "Weapon", config);
                    spawned++;
                }
            }

            string skipNote = skippedFull > 0 ? $" Skipped {skippedFull} (backpack full)." : "";
            Debug.Log($"[DevCheats] Spawned {spawned} weapon variation(s).{skipNote}");
        }

        static void SpawnModuleIntoBackpack(string moduleDefinitionId)
        {
            var inventory = App.Instance?.Player?.Inventory;
            var session   = App.Instance?.RaidSession;
            if (inventory == null || session == null)
            {
                Debug.LogWarning("[DevCheats] Cannot spawn module — Player.Inventory / RaidSession not ready.");
                return;
            }

            int freeSlot = inventory.FindFreeBackpackSlot();
            if (freeSlot < 0)
            {
                Debug.LogWarning("[DevCheats] Cannot spawn module — backpack is full.");
                return;
            }

            var id = session.RaidState.AllocateEId();
            inventory.Backpack[freeSlot] = State.ItemState.Create(id, moduleDefinitionId);
            Debug.Log($"[DevCheats] Spawned {moduleDefinitionId} у backpack slot {freeSlot}.");
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
            CreateSectionIfMissing<DevCheatsHitPauseSection>(so, "_hitPause", folder, "HitPause");
            CreateSectionIfMissing<DevCheatsMuzzleVfxSection>(so, "_muzzleVfx", folder, "MuzzleVfx");
            CreateSectionIfMissing<DevCheatsStaggerSection>(so, "_stagger", folder, "Stagger");

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
