using System.Collections.Generic;
using System.Linq;
using ApplicationCore;
using Cysharp.Threading.Tasks;
using Dev;
using Editor.Meta;
using Progression;
using State;
using Systems.Meta;
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

        // "Give Weapon Preset" devcheat — designers author full builds (payload +
        // delivery + optional exotic/attachments) as WeaponPresetDefinition assets;
        // this picks one and spawns the assembled weapon into the backpack. Presets
        // are discovered via AssetDatabase (editor-only) so no Resources database is
        // needed.
        State.WeaponPresetDefinition[] _weaponPresets;
        int _weaponPresetIndex;

        int _giveCreditsAmount = 1000;
        State.BuildingKind _buildingKind = State.BuildingKind.WeaponBuilder;
        int _buildingLevel = 1;

        // Section foldout states (persisted via EditorPrefs)
        readonly Dictionary<string, bool> _foldouts = new();

        // Meta → Region raid simulator: cached scan of Test_Map's MapRegion polygons,
        // last scan / last loot status lines. Cache is loaded lazily (persists via
        // EditorPrefs across the Play↔Edit boundary — see RaidSimRegions).
        RegionCache _metaCache;
        bool _metaCacheLoaded;
        string _metaScanInfo = "";
        string _metaLootInfo = "";
        string _metaSellInfo = "";

        // Cached inline editors per section SO (recreated when config changes)
        readonly Dictionary<string, UnityEditor.Editor> _sectionEditors = new();

        [MenuItem("Raid/Dev Cheats")]
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

            // Sections are grouped into 5 macro buckets by topic, not by source. ViewCheats
            // entries (cosmetic polish) sit alongside DevCheats entries (gameplay tuning)
            // when they affect the same player-facing concern (e.g. Crosshair + Crosshair v2,
            // Damage Numbers v2 next to Combat). Underlying SO assets stay split on disk.
            var view = ViewCheats.Config;

            DrawMacroGroup("💀 Tools & Cheats", ToolsTone, defaultExpanded: true, () =>
            {
                DrawSection("💀 Cheats", _config.Cheats);
                DrawSection("⏱ Raid clock", _config.Raid);
                DrawRaidSection();
                DrawProgressionDebugSection();
                DrawNumericHealthBarToggle();
                DrawQuestsSection();
            });

            DrawMacroGroup("🌍 Meta", MetaTone, defaultExpanded: false, () =>
            {
                DrawMetaSection();
            });

            DrawMacroGroup("🎮 Combat", CombatTone, defaultExpanded: false, () =>
            {
                DrawSection("🔫 Weapon", _config.Weapon);
                DrawSection("💢 Recoil", _config.Recoil);
                DrawSection("🎯 Aim", _config.Aim);
                DrawSection("🔍 ADS", _config.ADS);
                DrawSection("🎯 Scope", _config.Scope);
                DrawSection("✛ Crosshair", _config.Crosshair);
                if (view != null) DrawSection("✛ Crosshair v2 (SDF)", view.CrosshairV2);
                if (view != null) DrawSection("🩸 HUD damage feedback", view.HudDamage);
                if (view != null) DrawSection("⚔ Battle HUD", view.BattleHud);
                DrawSection("🛡 Armor", _config.Armor);
                if (view != null) DrawSection("🔢 Damage Numbers v2 (TMP)", view.DamageNumberV2);
                DrawSection("⏸ Hit Pause", _config.HitPause);
                DrawSection("💥 Stagger / Hit Reaction", _config.Stagger);
                DrawSection("🔬 Laser (charge + shotgun)", _config.Laser);
                DrawSection("🔥 Barrel Heat (rifle)", _config.BarrelHeat);
            });

            DrawMacroGroup("🏃 Player & World", PlayerTone, defaultExpanded: false, () =>
            {
                DrawSection("🏃 Player", _config.Player);
                DrawSection("🏃 Stamina (sprint)", _config.Stamina);
                DrawSection("👁 FOV", _config.FOV);
                DrawSection("💉 Status Effects", _config.StatusEffects);
                DrawSection("❤ Health Bar", _config.HealthBar);
                DrawSection("🌫 Fog", _config.Fog);
                DrawSection("🌐 Parallax", _config.Parallax);
            });

            DrawMacroGroup("🧟 AI", AiTone, defaultExpanded: false, () =>
            {
                DrawSection("🧟 Horde", _config.Horde);
                DrawSection("🎯 Bot Engagement Gate", _config.BotEngagement);
                if (view != null) DrawSection("🤖 Bot Debug Overlay", view.BotDebug);
            });

            DrawMacroGroup("✨ FX & Feel", FxTone, defaultExpanded: false, () =>
            {
                if (view != null) DrawSection("🔊 Audio", view.Audio);
                DrawSection("✨ Muzzle VFX", _config.MuzzleVfx);
                if (view != null)
                {
                    DrawSection("🎬 Camera Shake", view.CameraShake);
                    DrawSection("⚡ Hit Flash", view.HitFlash);
                    DrawSection("💥 Impact VFX (per-archetype)", view.ImpactVfx);
                    DrawSection("🩸 Blood Decals", view.BloodDecal);
                    DrawSection("🔫 Bullet Holes", view.BulletHole);
                    DrawSection("🥃 Casings", view.Casings);
                    DrawSection("📦 Magazine Drop", view.Magazine);
                    DrawSection("🔻 Weapon Drop", view.WeaponDrop);
                    DrawSection("❗ Quest Marker", view.QuestMarker);
                    DrawSection("🧭 Deploy Marker", view.DeployMarker);
                    DrawSection("💀 Ragdoll", view.Ragdoll);
                }
            });

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }

        // ── Macro group banner palette ────────────────────────
        // Each tone is (bar background, bottom accent line). Picked to be distinct
        // at-a-glance without becoming a clown parade — warm hues for player-facing,
        // cool hues for systems and view polish.
        static readonly (Color bg, Color accent) ToolsTone  = (new(0.42f, 0.16f, 0.16f, 1f), new(0.92f, 0.32f, 0.32f, 1f));
        static readonly (Color bg, Color accent) CombatTone = (new(0.45f, 0.28f, 0.12f, 1f), new(0.92f, 0.55f, 0.18f, 1f));
        static readonly (Color bg, Color accent) PlayerTone = (new(0.16f, 0.34f, 0.22f, 1f), new(0.36f, 0.80f, 0.50f, 1f));
        static readonly (Color bg, Color accent) AiTone     = (new(0.28f, 0.20f, 0.44f, 1f), new(0.62f, 0.50f, 0.92f, 1f));
        static readonly (Color bg, Color accent) FxTone     = (new(0.18f, 0.32f, 0.48f, 1f), new(0.36f, 0.62f, 0.92f, 1f));
        static readonly (Color bg, Color accent) MetaTone    = (new(0.14f, 0.36f, 0.36f, 1f), new(0.30f, 0.82f, 0.78f, 1f));

        void DrawMacroGroup(string title, (Color bg, Color accent) tone, bool defaultExpanded,
            System.Action body)
        {
            EditorGUILayout.Space(10);

            // Foldout state — distinct key namespace from per-section foldouts so the
            // two layers don't collide. First-launch default is per-group.
            string key = "macro_" + title;
            if (!_foldouts.ContainsKey(key))
                _foldouts[key] = EditorPrefs.GetBool("DevCheats_fold_" + key, defaultExpanded);
            bool fold = _foldouts[key];

            const float bannerHeight = 30f;
            var rect = EditorGUILayout.GetControlRect(false, bannerHeight);
            EditorGUI.DrawRect(rect, tone.bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), tone.accent);

            // Whole bar is a click target — cheaper than a tiny foldout arrow.
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                fold = !fold;
                _foldouts[key] = fold;
                EditorPrefs.SetBool("DevCheats_fold_" + key, fold);
                Event.current.Use();
                Repaint();
            }
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal   = { textColor = Color.white },
            };
            string arrow = fold ? "▼" : "▶";
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, rect.width - 20, 20),
                $"{arrow}  {title}", labelStyle);

            if (!fold) return;

            EditorGUILayout.Space(2);
            EditorGUI.indentLevel++;
            body?.Invoke();
            EditorGUI.indentLevel--;
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
            GUI.Label(subRect, "Gameplay + view tuning, all in one place. Assets remain split on disk.", subStyle);

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
                EditorGUILayout.HelpBox($"Section asset missing. Run Raid → Dev Cheats — Create Section Assets.", MessageType.Warning);
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

        void DrawProgressionDebugSection()
        {
            const string key = "Progression test points";
            EditorGUILayout.Space(4);
            bool fold = GetFoldout(key);
            var newFold = EditorGUILayout.Foldout(
                fold, "🌟 Progression test points", true, EditorStyles.foldoutHeader);
            if (newFold != fold) SetFoldout(key, newFold);
            if (!newFold) return;

            EditorGUI.indentLevel++;
            bool appReady = Application.isPlaying && App.IsInitialized
                            && App.Instance.Player?.Progression != null;
            if (!appReady)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to grant temporary progression points.", MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            var progression = App.Instance.Player.Progression;
            EditorGUILayout.LabelField("Available", progression.DevUnlockPoints.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1"))
                    progression.DevUnlockPoints = Mathf.Min(9999, progression.DevUnlockPoints + 1);
                if (GUILayout.Button("+10"))
                    progression.DevUnlockPoints = Mathf.Min(9999, progression.DevUnlockPoints + 10);
                using (new EditorGUI.DisabledScope(progression.DevUnlockPoints == 0))
                    if (GUILayout.Button("Reset")) progression.DevUnlockPoints = 0;
            }

            EditorGUILayout.HelpBox(
                "One point unlocks one connected node without consuming its materials. " +
                "Dev points are not saved.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        void DrawNumericHealthBarToggle()
        {
            var healthBar = _config.HealthBar;
            if (healthBar == null) return;

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            bool showNumericHp = EditorGUILayout.ToggleLeft(
                "❤ Show numeric HP on health bars",
                healthBar.HBarShowNumericHp,
                EditorStyles.boldLabel);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(healthBar, "Toggle numeric HP on health bars");
            healthBar.HBarShowNumericHp = showNumericHp;
            EditorUtility.SetDirty(healthBar);
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
                if (GUILayout.Button("Damage Player (-25 HP, nonlethal)"))
                    DamagePlayerForMedicineTest();
            }

            EditorGUILayout.Space(4);
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

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Give All Mods") && appReady)
                    GiveAllMods();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Give Item", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                DrawGiveItemRow();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Give Weapon Preset", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                DrawGiveWeaponPresetRow();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Give Credits", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                DrawGiveCreditsRow();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Set Building Level", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!appReady))
            {
                DrawSetBuildingLevelRow();
            }

            if (!appReady)
                EditorGUILayout.HelpBox("Enter Play Mode to use raid cheats.", MessageType.Info);
            else if (App.Instance.IsInHideout)
                EditorGUILayout.HelpBox("Already in hideout.", MessageType.Info);

            EditorGUI.indentLevel--;
        }

        static void DamagePlayerForMedicineTest()
        {
            var session = App.Instance.RaidSession;
            bool damaged = DamageSystem.ApplyPlayerTestDamage(
                session.RaidState, 25f, session.ConsumeEvents());

            if (!damaged)
            {
                Debug.Log("[DevCheats] Player test damage skipped (already at 1 HP or no active player).");
                return;
            }

            var playerId = session.RaidState.PlayerEntity.Id;
            var health = session.RaidState.HealthMap[playerId];
            Debug.Log($"[DevCheats] Player damaged for medicine test: {health.CurrentHp:0}/{health.MaxHp:0} HP.");
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
                    // Weapon-category items (Weapon/Rifle/Pistol) can't be given raw — they
                    // need a WeaponConfiguration (payload + delivery). Use "Give Weapon
                    // Preset" for those; hide them here so the picker only lists items that
                    // actually work through the plain add-to-backpack path.
                    var dropdown = new ItemPickerDropdown(new AdvancedDropdownState(), id =>
                    {
                        _giveItemId = id;
                        Repaint();
                    }, filter: d => d.Category != State.ItemCategory.Weapon);
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

        void DrawGiveWeaponPresetRow()
        {
            // Lazy-load (and cache) the preset assets. Refresh button rescans after
            // new presets are authored without reopening the window.
            _weaponPresets ??= LoadWeaponPresets();

            if (_weaponPresets.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No WeaponPresetDefinition assets found.\n" +
                    "Create one via Assets → Create → Weapon Builder → Weapon Preset, " +
                    "then set its Payload + Delivery cores.",
                    MessageType.Info);
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                    _weaponPresets = LoadWeaponPresets();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var labels = new string[_weaponPresets.Length];
                for (int i = 0; i < _weaponPresets.Length; i++)
                    labels[i] = _weaponPresets[i].PresetName;

                _weaponPresetIndex = Mathf.Clamp(_weaponPresetIndex, 0, _weaponPresets.Length - 1);
                _weaponPresetIndex = EditorGUILayout.Popup(_weaponPresetIndex, labels,
                    GUILayout.MinWidth(220), GUILayout.ExpandWidth(true));

                var preset = _weaponPresets[_weaponPresetIndex];
                using (new EditorGUI.DisabledScope(preset == null || !preset.IsValid))
                {
                    if (GUILayout.Button("Give", GUILayout.Width(70)))
                        GiveWeaponPreset(preset);
                }

                if (GUILayout.Button("↻", GUILayout.Width(24)))
                    _weaponPresets = LoadWeaponPresets();
            }

            var selected = _weaponPresets[_weaponPresetIndex];
            if (selected != null && !selected.IsValid)
                EditorGUILayout.HelpBox(
                    $"'{selected.PresetName}' is missing a Payload or Delivery core.",
                    MessageType.Warning);
        }

        static State.WeaponPresetDefinition[] LoadWeaponPresets()
        {
            var guids = AssetDatabase.FindAssets("t:WeaponPresetDefinition");
            var list = new List<State.WeaponPresetDefinition>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<State.WeaponPresetDefinition>(path);
                if (asset != null) list.Add(asset);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.PresetName, b.PresetName));
            return list.ToArray();
        }

        static void GiveWeaponPreset(State.WeaponPresetDefinition preset)
        {
            var inventory = App.Instance?.Player?.Inventory;
            var session   = App.Instance?.RaidSession;
            if (inventory == null || session == null)
            {
                Debug.LogWarning("[DevCheats] Cannot give weapon preset — Player.Inventory / RaidSession not ready.");
                return;
            }
            if (preset == null || !preset.IsValid)
            {
                Debug.LogWarning("[DevCheats] Weapon preset is missing a Payload or Delivery core.");
                return;
            }

            int slot = inventory.FindFreeBackpackSlot();
            if (slot < 0)
            {
                Debug.LogWarning($"[DevCheats] Cannot give '{preset.PresetName}' — backpack is full.");
                return;
            }

            var config = preset.BuildConfiguration();
            var eid = session.RaidState.AllocateEId();
            inventory.Backpack[slot] = State.ItemState.CreateWeapon(eid, preset.WeaponDefinitionId, config);
            Debug.Log($"[DevCheats] Gave weapon preset '{preset.PresetName}' → backpack slot {slot}.");
        }

        void DrawGiveCreditsRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = App.IsInitialized ? (App.Instance.Player?.Credits ?? 0) : 0;
                EditorGUILayout.LabelField($"Balance: {current}¢", GUILayout.MinWidth(140));

                EditorGUILayout.LabelField("Amount", GUILayout.Width(54));
                _giveCreditsAmount = EditorGUILayout.IntField(_giveCreditsAmount, GUILayout.Width(80));

                if (GUILayout.Button("Give", GUILayout.Width(70)))
                    GiveCredits(_giveCreditsAmount);
                if (GUILayout.Button("Reset", GUILayout.Width(70)))
                    SetCredits(0);
            }
        }

        void DrawSetBuildingLevelRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = App.IsInitialized
                    ? (App.Instance.Player?.GetBuildingLevel(_buildingKind) ?? 0)
                    : 0;

                _buildingKind = (State.BuildingKind)EditorGUILayout.EnumPopup(
                    _buildingKind, GUILayout.Width(150));
                EditorGUILayout.LabelField($"current Lv. {current}", GUILayout.Width(90));
                EditorGUILayout.LabelField("→", GUILayout.Width(14));
                _buildingLevel = Mathf.Max(0, EditorGUILayout.IntField(
                    _buildingLevel, GUILayout.Width(50)));
                if (GUILayout.Button("Set", GUILayout.Width(60)))
                    SetBuildingLevel(_buildingKind, _buildingLevel);
            }
        }

        static void SetBuildingLevel(State.BuildingKind kind, int level)
        {
            var player = App.Instance?.Player;
            if (player == null)
            {
                Debug.LogWarning("[DevCheats] Cannot set building level — Player not ready.");
                return;
            }
            player.SetBuildingLevel(kind, Mathf.Max(0, level));
            // Tick any UpgradeBuildingTask that targets this kind so a quest waiting
            // for the upgrade doesn't sit stuck.
            Systems.QuestSystem.OnBuildingUpgraded(
                player.QuestProgress, App.Instance.QuestDatabase, kind, level);
            Debug.Log($"[DevCheats] {kind} set to Lv. {player.GetBuildingLevel(kind)}.");
        }

        static void GiveCredits(int amount)
        {
            var player = App.Instance?.Player;
            if (player == null)
            {
                Debug.LogWarning("[DevCheats] Cannot give credits — Player not ready.");
                return;
            }
            if (amount >= 0)
                player.Credit(amount);
            else
                player.TryDebit(-amount);
            Debug.Log($"[DevCheats] Credits {(amount >= 0 ? "+" : "")}{amount} → {player.Credits}¢.");
        }

        static void SetCredits(int amount)
        {
            var player = App.Instance?.Player;
            if (player == null) return;
            player.ProfileState.Credits = Mathf.Max(0, amount);
            Debug.Log($"[DevCheats] Credits set to {player.Credits}¢.");
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

            int added = InventorySystem.AddToBackpack(player.Inventory, defId, count, App.Instance.AllocateEId);
            if (added <= 0)
            {
                Debug.LogWarning($"[DevCheats] Cannot give '{defId}' — backpack is full.");
                return;
            }

            if (added < count)
                Debug.LogWarning($"[DevCheats] Gave {added}/{count}× {def.DisplayName} ({defId}) — backpack full, {count - added} dropped.");
            else
                Debug.Log($"[DevCheats] Gave {added}× {def.DisplayName} ({defId}).");
        }

        // Attachment mod ids — match the AttachmentDefinition SOs + ItemDefinition entries 1:1.
        static readonly string[] AttachmentModIds =
        {
            "PowerComp", "MuzzleBrake", "VerticalGrip", "AngledGrip", "HeavyStock",
            "SkeletonStock", "RedDot", "SniperScope", "ExtendedMag", "QuickMag",
            "LaserFocusing", "ScatterChoke", "AutoHeatSink", // unique (archetype-restricted)
        };

        // Drops a stack of every attachment mod into the backpack so the loot-gated
        // attachment editor can be exercised on an existing save.
        static void GiveAllMods()
        {
            var player = App.Instance?.Player;
            if (player?.Inventory == null)
            {
                Debug.LogWarning("[DevCheats] Cannot give mods — Player.Inventory not ready.");
                return;
            }

            int total = 0;
            foreach (var id in AttachmentModIds)
                total += InventorySystem.AddToBackpack(player.Inventory, id, 3, App.Instance.AllocateEId);
            Debug.Log($"[DevCheats] Gave {total} attachment mod units across {AttachmentModIds.Length} types.");
        }

        /// <summary>
        /// Builds a fully-assembled <see cref="State.ItemState"/> for every Payload × Delivery
        /// combination in <see cref="State.CoreDefinitionDatabase"/> and drops them into free
        /// backpack slots. Lets QA/dev test each archetype without going through the Builder UI.
        /// Each core gets a RANDOM rarity — exercises the dual-rarity inventory frame +
        /// tooltip colors. Stats fall back to Common until per-tier values are authored
        /// (Tier 4b), so rarity is visual-only for now; magazines start full.
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

                    var payloadRarity  = (State.RarityTier)Random.Range(0, 5);
                    var deliveryRarity = (State.RarityTier)Random.Range(0, 5);
                    var deliveryStats  = delivery.StatsByTier(deliveryRarity);
                    var config = new State.WeaponConfiguration(
                        payload:        new State.PayloadCoreInstance(payload.Id,   payloadRarity),
                        delivery:       new State.DeliveryCoreInstance(delivery.Id, deliveryRarity),
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

        // ── Meta → Region raid simulator ───────────────────────
        // Two-phase (see RaidSimRegions): SCAN Test_Map's MapRegion polygons in edit
        // mode → cache; then in Play Mode LOOT a region straight into the backpack,
        // grabbing the most valuable loot that fits. Lets you dry-run raids to feed
        // hideout / quest / craft / sell progression without playing them out.
        void DrawMetaSection()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                "Author MapRegion polygons in Test_Map, scan them (edit mode), then in " +
                "Play Mode loot a region into your backpack to test meta progression.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Scan Test_Map regions"))
                {
                    _metaCache = RaidSimRegions.Scan(out _metaScanInfo);
                    _metaCacheLoaded = true;
                    _metaLootInfo = "";
                }
            }
            if (Application.isPlaying)
                EditorGUILayout.LabelField("Re-scanning needs edit mode — exit Play Mode to scan.",
                    EditorStyles.miniLabel);

            if (!_metaCacheLoaded)
            {
                _metaCache = RaidSimRegions.LoadCache();
                _metaCacheLoaded = true;
            }

            if (!string.IsNullOrEmpty(_metaScanInfo))
                EditorGUILayout.HelpBox(_metaScanInfo, MessageType.Info);

            // Player-action sim: dump the backpack for credits (loot → sell → spend loop).
            {
                bool canSell = Application.isPlaying && App.IsInitialized && App.Instance.Player != null;
                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    int credits = canSell ? App.Instance.Player.Credits : 0;
                    EditorGUILayout.LabelField($"Balance: {credits}¢", GUILayout.MinWidth(120));
                    using (new EditorGUI.DisabledScope(!canSell))
                    {
                        if (GUILayout.Button("Sell Backpack → Credits", GUILayout.Width(190)))
                            SellBackpack();
                    }
                }
                if (!string.IsNullOrEmpty(_metaSellInfo))
                    EditorGUILayout.LabelField(_metaSellInfo, EditorStyles.miniLabel);

                // What a raid will prioritise over raw value — recomputed each repaint so
                // it tracks quest hand-ins / upgrades you do while the window is open.
                if (canSell)
                    EditorGUILayout.LabelField(MetaNeeds.Describe(CurrentNeeds()),
                        EditorStyles.wordWrappedMiniLabel);
            }

            if (_metaCache?.regions == null || _metaCache.regions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No scanned regions yet. Add MapRegion components to Test_Map (≥3 points " +
                    "each), then press Scan.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(
                $"Cached: {_metaCache.regions.Count} region(s)  ·  scanned {_metaCache.scannedAtUtc} UTC",
                EditorStyles.miniLabel);

            bool appReady = Application.isPlaying && App.IsInitialized && App.Instance.Player != null;

            EditorGUILayout.Space(2);
            foreach (var region in _metaCache.regions)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{region.name}  ×{region.difficulty:0.##}",
                            GUILayout.MinWidth(110));
                        EditorGUILayout.LabelField(
                            $"📦 {region.containers.Count}  ✦ {region.loose.Count}  💀 {region.bots.Count}",
                            EditorStyles.miniLabel, GUILayout.Width(140));
                        using (new EditorGUI.DisabledScope(!appReady))
                        {
                            if (GUILayout.Button("Raid → Backpack", GUILayout.Width(130)))
                            {
                                RaidSimRegions.RaidRegion(
                                    region, App.Instance.Player.Inventory, App.Instance.AllocateEId,
                                    App.Instance.CoreDefinitions, ProgressionTreeConfig.Instance,
                                    App.Instance.Player.Progression, out _metaLootInfo,
                                    MetaNeeds.ToQuotas(CurrentNeeds()));
                                Debug.Log($"[DevCheats] {_metaLootInfo}");
                            }
                        }
                    }

                    // Live bill + odds against the CURRENT kit, so you can see what a better
                    // gun / more armor / more skill nodes buys you before committing.
                    if (appReady)
                        EditorGUILayout.LabelField(DescribePlan(region), EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (!appReady)
                EditorGUILayout.HelpBox("Enter Play Mode (hideout is fine) to raid a region into the backpack.",
                    MessageType.Info);
            else
                EditorGUILayout.LabelField(
                    "Raiding burns real reserve ammo and rolls survival — dying forfeits the whole " +
                    "inventory, same as a live KIA.", EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(_metaLootInfo))
                EditorGUILayout.HelpBox(_metaLootInfo, MessageType.Info);
        }

        // The player's CURRENT shopping list: active-quest hand-ins, the next level of
        // each hideout building, and skill nodes that are unlockable right now. Deeper
        // levels / unreachable nodes are deliberately out — a sim raid should bring back
        // what unblocks the very next step, not everything the tree will ever want.
        static List<MetaNeeds.Need> CurrentNeeds()
        {
            var player = App.Instance?.Player;
            if (player == null) return new List<MetaNeeds.Need>();
            return MetaNeeds.Collect(player, App.Instance.QuestDatabase, ProgressionTreeConfig.Instance);
        }

        // One-line "what will this cost me and what are my odds" readout for a region row.
        static string DescribePlan(RegionSnapshot region)
        {
            var plan = RaidSimRegions.PlanRegion(
                region, App.Instance.Player.Inventory, App.Instance.CoreDefinitions,
                ProgressionTreeConfig.Instance, App.Instance.Player.Progression);

            if (plan.EnemyCount == 0) return "No enemies here — free loot, no ammo, no roll.";

            string gun, ammo;
            if (plan.HasWeapon)
            {
                ammo = $"{plan.RoundsNeeded} × {plan.AmmoType} (have {plan.RoundsAvailable})";
                if (plan.Shortfall > 0) ammo += $" ⚠ short {plan.Shortfall}";
                gun = $"{plan.WeaponName} — {plan.Dps:0} dps";
            }
            else
            {
                ammo = "none";
                gun = $"⚠ no gun (odds ×{RaidCombatSimulator.ImprovisedPenalty:0.00})";
            }

            return $"{plan.EnemyCount} enemy · {plan.TotalEnemyHp:0} HP · ammo {ammo}\n" +
                   $"{gun}, gear {plan.GearScore:P0}, " +
                   $"skills +{plan.SkillBonus:P0}  →  survive {plan.SurviveChance:P0}";
        }

        // Simulates the player vendoring everything in the backpack: each slot pays out
        // its sell value (real global sell price where the item is stocked; otherwise a
        // half-worth fallback so guns / uncatalogued loot still convert), credits the
        // player, ticks sell-quests, and empties the pack.
        void SellBackpack()
        {
            var player = App.Instance?.Player;
            if (player?.Inventory == null) return;

            var inv = player.Inventory;
            var db = App.Instance.QuestDatabase;
            long total = 0;
            int slotsSold = 0;

            for (int i = 0; i < inv.Backpack.Length; i++)
            {
                var it = inv.Backpack[i];
                if (it == null) continue;

                int price = SellValueOf(it);
                inv.Backpack[i] = null;
                slotsSold++;
                if (price > 0)
                {
                    player.Credit(price);
                    total += price;
                    if (db != null) QuestSystem.OnItemSold(player.QuestProgress, db, price);
                }
            }
            inv.Version++;

            _metaSellInfo = slotsSold == 0
                ? "Backpack already empty."
                : $"Sold {slotsSold} slot(s) for {total}¢ — balance {player.Credits}¢.";
            Debug.Log($"[DevCheats] {_metaSellInfo}");
        }

        static int SellValueOf(State.ItemState item)
        {
            // GetGlobalSellPrice already applies the durability discount (ShopSystem is the
            // single source of truth now).
            int p = ShopSystem.GetGlobalSellPrice(item);
            if (p > 0) return p;
            // No shop stocks it (weapons, some loot) → fall back to half its loot worth
            // (ValueOfItem also routes through the same durability multiplier).
            long worth = Systems.Meta.RegionLootSimulator.ValueOfItem(item);
            return Mathf.Max(1, Mathf.RoundToInt(worth * 0.5f));
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
                // Pick from currently-active quests so designers don't have to know /
                // paste raw ids. The text field below stays as a fallback for quests
                // that aren't active yet (or ids typed by hand).
                if (appReady)
                {
                    var db = App.Instance.QuestDatabase;
                    var activeIds = new List<string>();
                    var activeLabels = new List<string>();
                    foreach (var kvp in App.Instance.Player.QuestProgress.All)
                    {
                        if (kvp.Value.Status != State.QuestStatus.Active) continue;
                        string name = db != null && db.TryGet(kvp.Key, out var e) && e.Quest != null
                            ? e.Quest.DisplayName : null;
                        activeIds.Add(kvp.Key);
                        activeLabels.Add(string.IsNullOrEmpty(name) ? kvp.Key : $"{name} ({kvp.Key})");
                    }

                    if (activeIds.Count > 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                        int sel = Mathf.Clamp(activeIds.IndexOf(_questIdInput), 0, activeIds.Count - 1);
                        int newSel = EditorGUILayout.Popup("Active Quest", sel, activeLabels.ToArray());
                        _questIdInput = activeIds[newSel];

                        if (GUILayout.Button("Fulfill", GUILayout.Width(80)))
                            FulfillQuest(_questIdInput);
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No active quests.", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                _questIdInput = EditorGUILayout.TextField("Quest ID", _questIdInput);

                if (GUILayout.Button("Fulfill", GUILayout.Width(80)))
                {
                    if (appReady && !string.IsNullOrEmpty(_questIdInput))
                        FulfillQuest(_questIdInput);
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

        static void FulfillQuest(string questId)
        {
            var player = App.Instance.Player;
            var db = App.Instance.QuestDatabase;
            if (QuestSystem.TryFulfillTasks(player.QuestProgress, db, questId))
                Debug.Log($"[DevCheats] Fulfilled all tasks for quest '{questId}'. Claim reward at NPC.");
            else
                Debug.LogWarning($"[DevCheats] Quest '{questId}' is not active.");
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

        [MenuItem("Raid/Dev Cheats — Create Section Assets")]
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

            // ViewCheats sections live in the same window — bootstrap them here too
            // so a single menu materializes everything (the standalone ViewCheatsWindow
            // was retired when sections moved into this unified window).
            var viewConfig = ViewCheats.Config;
            if (viewConfig != null)
            {
                CreateViewSectionAssets(viewConfig);
                EditorUtility.SetDirty(viewConfig);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[DevCheats] Dev + View section assets created/linked. Existing values preserved.");
        }

        static void CreateViewSectionAssets(ViewCheatsConfig config)
        {
            const string folder = "Assets/Resources/Configs/ViewCheats";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/Configs"))
                    AssetDatabase.CreateFolder("Assets/Resources", "Configs");
                AssetDatabase.CreateFolder("Assets/Resources/Configs", "ViewCheats");
            }

            var so = new SerializedObject(config);

            CreateSectionIfMissing<ViewCheatsCameraShakeSection>(so, "_cameraShake", folder, "CameraShake");
            CreateSectionIfMissing<ViewCheatsBloodDecalSection>(so, "_bloodDecal", folder, "BloodDecal");
            CreateSectionIfMissing<ViewCheatsBulletHoleSection>(so, "_bulletHole", folder, "BulletHole");
            CreateSectionIfMissing<ViewCheatsCasingsSection>(so, "_casings", folder, "Casings");
            CreateSectionIfMissing<ViewCheatsMagazineSection>(so, "_magazine", folder, "Magazine");
            CreateSectionIfMissing<ViewCheatsRagdollSection>(so, "_ragdoll", folder, "Ragdoll");
            CreateSectionIfMissing<ViewCheatsWeaponDropSection>(so, "_weaponDrop", folder, "WeaponDrop");
            CreateSectionIfMissing<ViewCheatsHitFlashSection>(so, "_hitFlash", folder, "HitFlash");
            CreateSectionIfMissing<ViewCheatsImpactVfxSection>(so, "_impactVfx", folder, "ImpactVfx");
            CreateSectionIfMissing<ViewCheatsDamageNumberSection>(so, "_damageNumberV2", folder, "DamageNumberV2");
            CreateSectionIfMissing<ViewCheatsCrosshairV2Section>(so, "_crosshairV2", folder, "CrosshairV2");
            CreateSectionIfMissing<ViewCheatsHudDamageSection>(so, "_hudDamage", folder, "HudDamage");
            CreateSectionIfMissing<ViewCheatsBattleHudSection>(so, "_battleHud", folder, "BattleHud");
            CreateSectionIfMissing<ViewCheatsAudioSection>(so, "_audio", folder, "Audio");
            CreateSectionIfMissing<ViewCheatsBotDebugSection>(so, "_botDebug", folder, "BotDebug");
            CreateSectionIfMissing<ViewCheatsQuestMarkerSection>(so, "_questMarker", folder, "QuestMarker");
            CreateSectionIfMissing<ViewCheatsDeployMarkerSection>(so, "_deployMarker", folder, "DeployMarker");

            so.ApplyModifiedPropertiesWithoutUndo();
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
            CreateSectionIfMissing<DevCheatsScopeSection>(so, "_scope", folder, "Scope");
            CreateSectionIfMissing<DevCheatsHealthBarSection>(so, "_healthBar", folder, "HealthBar");
            CreateSectionIfMissing<DevCheatsParallaxSection>(so, "_parallax", folder, "Parallax");
            CreateSectionIfMissing<DevCheatsStatusEffectsSection>(so, "_statusEffects", folder, "StatusEffects");
            CreateSectionIfMissing<DevCheatsArmorSection>(so, "_armor", folder, "Armor");
            CreateSectionIfMissing<DevCheatsHitPauseSection>(so, "_hitPause", folder, "HitPause");
            CreateSectionIfMissing<DevCheatsMuzzleVfxSection>(so, "_muzzleVfx", folder, "MuzzleVfx");
            CreateSectionIfMissing<DevCheatsStaggerSection>(so, "_stagger", folder, "Stagger");
            CreateSectionIfMissing<DevCheatsHordeSection>(so, "_horde", folder, "Horde");
            CreateSectionIfMissing<DevCheatsBotEngagementSection>(so, "_botEngagement", folder, "BotEngagement");
            CreateSectionIfMissing<DevCheatsLaserSection>(so, "_laser", folder, "Laser");
            CreateSectionIfMissing<DevCheatsBarrelHeatSection>(so, "_barrelHeat", folder, "BarrelHeat");
            CreateSectionIfMissing<DevCheatsStaminaSection>(so, "_stamina", folder, "Stamina");
            CreateSectionIfMissing<DevCheatsRaidSection>(so, "_raid", folder, "Raid");

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
