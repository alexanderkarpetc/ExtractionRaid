using System.Collections.Generic;
using State;
using Systems;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class RaidStateDebuggerWindow : EditorWindow
    {
        Vector2 _scrollPos;

        bool _foldPlayer = true;
        bool _foldPlayerWeapon;
        bool _foldPlayerHotbar;
        bool _foldBots = true;
        bool _foldProjectiles;
        bool _foldGrenades;
        bool _foldGroundItems;
        bool _foldInventory;
        bool _foldHealthMap;

        readonly Dictionary<int, bool> _botFolds = new();
        readonly Dictionary<int, bool> _projFolds = new();
        readonly Dictionary<int, bool> _grenadeFolds = new();

        [MenuItem("Window/Raid State Debugger")]
        static void Open()
        {
            GetWindow<RaidStateDebuggerWindow>("Raid State Debugger");
        }

        void Update()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect Raid State.", MessageType.Info);
                return;
            }

            RaidState state;
            try
            {
                var session = App.App.Instance?.RaidSession;
                if (session == null)
                {
                    EditorGUILayout.HelpBox("No active raid session.", MessageType.Warning);
                    return;
                }

                state = session.RaidState;
            }
            catch
            {
                EditorGUILayout.HelpBox("App not initialized.", MessageType.Warning);
                return;
            }

            if (state == null)
            {
                EditorGUILayout.HelpBox("RaidState is null.", MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawGeneral(state);
            DrawPlayer(state);
            DrawBots(state);
            DrawProjectiles(state);
            DrawGrenades(state);
            DrawGroundItems(state);
            DrawInventory(state);
            DrawHealthMap(state);

            EditorGUILayout.EndScrollView();
        }

        // ── General ──────────────────────────────────────────────

        void DrawGeneral(RaidState state)
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            Field("Elapsed Time", $"{state.ElapsedTime:F2}s");
            Field("Is Running", state.IsRunning);
            Field("Projectiles", state.Projectiles.Count);
            Field("Grenades", state.Grenades.Count);
            Field("Bots", state.Bots.Count);
            Field("Ground Items", state.GroundItems.Count);
            EditorGUILayout.Space(4);
        }

        // ── Player ───────────────────────────────────────────────

        void DrawPlayer(RaidState state)
        {
            var p = state.PlayerEntity;
            _foldPlayer = EditorGUILayout.Foldout(_foldPlayer, "Player", true, EditorStyles.foldoutHeader);
            if (!_foldPlayer) return;

            EditorGUI.indentLevel++;

            if (p == null)
            {
                EditorGUILayout.LabelField("(no player)");
                EditorGUI.indentLevel--;
                return;
            }

            Field("Id", p.Id);
            Field("Position", p.Position);
            Field("Velocity", p.Velocity);
            Field("Facing", p.FacingDirection);
            Field("Aim", p.AimDirection);
            Field("Raw Aim Point", p.RawAimPoint);
            Field("Weapon Aim Point", p.WeaponAimPoint);
            Field("Selected Slot", p.SelectedHotbarSlot);
            Field("Pending Slot", p.PendingHotbarSlot);
            Field("Is Rolling", p.IsRolling);
            if (p.IsRolling)
            {
                Field("Roll Direction", p.RollDirection);
                Field("Roll Start", $"{p.RollStartTime:F2}s");
            }

            Field("Roll Cooldown End", $"{p.RollCooldownEndTime:F2}s");
            Field("Grenade Mode", p.IsInGrenadeMode);
            Field("Grenade Charging", p.GrenadeThrowCharging);
            Field("Grenade Target Dist", p.GrenadeTargetDistance);
            Field("Grenade Count", InventorySystem.CountGrenades(state.Inventory));

            DrawHealth(p.Id, state.HealthMap);

            // Equipped weapon
            _foldPlayerWeapon = EditorGUILayout.Foldout(_foldPlayerWeapon,
                p.EquippedWeapon != null
                    ? $"Equipped Weapon [{p.EquippedWeapon.PrefabId}]"
                    : "Equipped Weapon [none]",
                true);
            if (_foldPlayerWeapon && p.EquippedWeapon != null)
            {
                EditorGUI.indentLevel++;
                DrawWeapon(p.EquippedWeapon, state.ElapsedTime);
                EditorGUI.indentLevel--;
            }

            // Hotbar
            _foldPlayerHotbar = EditorGUILayout.Foldout(_foldPlayerHotbar, "Hotbar", true);
            if (_foldPlayerHotbar)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
                {
                    var w = p.Hotbar[i];
                    string sel = i == p.SelectedHotbarSlot ? " ◄" : "";
                    Field($"[{i + 1}]", w != null ? $"{w.PrefabId}{sel}" : $"[empty]{sel}");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Bots ─────────────────────────────────────────────────

        void DrawBots(RaidState state)
        {
            _foldBots = EditorGUILayout.Foldout(_foldBots,
                $"Bots ({state.Bots.Count})", true, EditorStyles.foldoutHeader);
            if (!_foldBots) return;

            EditorGUI.indentLevel++;

            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bot = state.Bots[i];
                int key = bot.Id.Value;

                if (!_botFolds.ContainsKey(key)) _botFolds[key] = false;
                _botFolds[key] = EditorGUILayout.Foldout(_botFolds[key],
                    $"[{bot.TypeId}] {bot.Id}", true);

                if (!_botFolds[key]) continue;

                EditorGUI.indentLevel++;

                Field("Position", bot.Position);
                Field("Velocity", bot.Velocity);
                Field("Aim Dir", bot.AimDirection);
                Field("Is Rolling", bot.IsRolling);
                if (bot.IsRolling)
                {
                    Field("Roll Direction", bot.RollDirection);
                    Field("Roll Start", $"{bot.RollStartTime:F2}s");
                }

                Field("Roll CD End", $"{bot.RollCooldownEndTime:F2}s");

                DrawHealth(bot.Id, state.HealthMap);

                if (bot.Weapon != null)
                {
                    Field("Weapon", bot.Weapon.PrefabId);
                }

                // Intents
                EditorGUILayout.LabelField("Intents", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                Field("WantsToFire", bot.WantsToFire);
                Field("WantsToDodge", bot.WantsToDodge);
                Field("WantsToHeal", bot.WantsToHeal);
                EditorGUI.indentLevel--;

                // Blackboard
                var bb = bot.Blackboard;
                if (bb != null)
                {
                    EditorGUILayout.LabelField("Blackboard", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    Field("Status", bb.DebugStatus ?? "—");
                    Field("HasTarget", bb.HasTarget);
                    Field("CanSeeTarget", bb.CanSeeTarget);
                    if (bb.HasTarget)
                    {
                        Field("Distance", $"{bb.DistanceToTarget:F1}");
                        Field("Last Known Pos", bb.LastKnownTargetPos);
                    }

                    Field("IsDodging", bb.IsDodging);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Projectiles ──────────────────────────────────────────

        void DrawProjectiles(RaidState state)
        {
            _foldProjectiles = EditorGUILayout.Foldout(_foldProjectiles,
                $"Projectiles ({state.Projectiles.Count})", true, EditorStyles.foldoutHeader);
            if (!_foldProjectiles) return;

            EditorGUI.indentLevel++;

            for (int i = 0; i < state.Projectiles.Count; i++)
            {
                var proj = state.Projectiles[i];
                int key = proj.Id.Value;

                if (!_projFolds.ContainsKey(key)) _projFolds[key] = false;

                float age = state.ElapsedTime - proj.SpawnTime;
                string header = $"{proj.Id} → Owner({proj.OwnerId.Value})  [{age:F1}s / {proj.Lifetime:F1}s]";

                _projFolds[key] = EditorGUILayout.Foldout(_projFolds[key], header, true);
                if (!_projFolds[key]) continue;

                EditorGUI.indentLevel++;
                Field("Position", proj.Position);
                Field("Direction", proj.Direction);
                Field("Speed", proj.Speed);
                Field("Damage", proj.Damage);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Grenades ──────────────────────────────────────────────

        void DrawGrenades(RaidState state)
        {
            _foldGrenades = EditorGUILayout.Foldout(_foldGrenades,
                $"Grenades ({state.Grenades.Count})", true, EditorStyles.foldoutHeader);
            if (!_foldGrenades) return;

            EditorGUI.indentLevel++;

            for (int i = 0; i < state.Grenades.Count; i++)
            {
                var g = state.Grenades[i];
                int key = g.Id.Value;

                if (!_grenadeFolds.ContainsKey(key)) _grenadeFolds[key] = false;

                float fuseRemaining = Mathf.Max(0f, g.FuseTime - (state.ElapsedTime - g.SpawnTime));
                string header = $"{g.Id} → Owner({g.OwnerId.Value})  [fuse {fuseRemaining:F1}s]";

                _grenadeFolds[key] = EditorGUILayout.Foldout(_grenadeFolds[key], header, true);
                if (!_grenadeFolds[key]) continue;

            EditorGUI.indentLevel++;
            Field("Damage", g.Damage);
                Field("Explosion Radius", g.ExplosionRadius);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Ground Items ─────────────────────────────────────────

        void DrawGroundItems(RaidState state)
        {
            _foldGroundItems = EditorGUILayout.Foldout(_foldGroundItems,
                $"Ground Items ({state.GroundItems.Count})", true, EditorStyles.foldoutHeader);
            if (!_foldGroundItems) return;

            EditorGUI.indentLevel++;

            foreach (var item in state.GroundItems)
            {
                Field(item.DisplayName, item.Position);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Inventory ────────────────────────────────────────────

        void DrawInventory(RaidState state)
        {
            var inv = state.Inventory;
            _foldInventory = EditorGUILayout.Foldout(_foldInventory, "Inventory", true, EditorStyles.foldoutHeader);
            if (!_foldInventory || inv == null) return;

            EditorGUI.indentLevel++;

            for (int i = 0; i < InventoryState.WeaponSlotCount; i++)
            {
                var item = inv.WeaponSlots[i];
                Field($"Weapon [{i}]", item != null ? item.DisplayName : "[empty]");
            }

            Field("Helmet", inv.HelmetSlot != null ? inv.HelmetSlot.DisplayName : "[empty]");
            Field("Body Armor", inv.BodyArmorSlot != null ? inv.BodyArmorSlot.DisplayName : "[empty]");

            int backpackCount = 0;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                if (inv.Backpack[i] != null) backpackCount++;
            }

            EditorGUILayout.LabelField($"Backpack ({backpackCount}/{InventoryState.BackpackSize})",
                EditorStyles.miniLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                var item = inv.Backpack[i];
                if (item != null)
                    Field($"[{i}]", item.DisplayName);
            }

            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Health Map ───────────────────────────────────────────

        void DrawHealthMap(RaidState state)
        {
            _foldHealthMap = EditorGUILayout.Foldout(_foldHealthMap,
                $"Health Map ({state.HealthMap.Count})", true, EditorStyles.foldoutHeader);
            if (!_foldHealthMap) return;

            EditorGUI.indentLevel++;

            foreach (var kvp in state.HealthMap)
            {
                var h = kvp.Value;
                string status = h.IsAlive ? "Alive" : "Dead";
                Field($"{kvp.Key}", $"{h.CurrentHp:F0} / {h.MaxHp:F0}  [{status}]");
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ── Shared helpers ───────────────────────────────────────

        void DrawWeapon(WeaponEntityState w, float elapsedTime)
        {
            Field("Id", w.Id);
            Field("PrefabId", w.PrefabId);

            // Phase status with remaining timer
            string phaseStatus;
            switch (w.Phase)
            {
                case State.WeaponPhase.Cooldown:
                    float cdRemaining = Mathf.Max(0f, w.FireInterval - (elapsedTime - w.PhaseStartTime));
                    phaseStatus = cdRemaining > 0.001f ? $"Cooldown ({cdRemaining:F2}s)" : "Ready";
                    break;
                case State.WeaponPhase.Equipping:
                    float eqRemaining = Mathf.Max(0f, w.EquipTime - (elapsedTime - w.PhaseStartTime));
                    phaseStatus = $"Equipping ({eqRemaining:F2}s)";
                    break;
                case State.WeaponPhase.Unequipping:
                    float uqRemaining = Mathf.Max(0f, w.UnequipTime - (elapsedTime - w.PhaseStartTime));
                    phaseStatus = $"Unequipping ({uqRemaining:F2}s)";
                    break;
                case State.WeaponPhase.Reloading:
                    float rlRemaining = Mathf.Max(0f, w.ReloadTime - (elapsedTime - w.PhaseStartTime));
                    phaseStatus = $"Reloading ({rlRemaining:F2}s)";
                    break;
                default:
                    phaseStatus = w.Phase.ToString();
                    break;
            }

            Field("Phase", phaseStatus);
            Field("EquipTime", w.EquipTime);
            Field("UnequipTime", w.UnequipTime);

            if (!string.IsNullOrEmpty(w.AmmoType))
            {
                Field("AmmoType", w.AmmoType);
                Field("Magazine", $"{w.AmmoInMagazine} / {w.MagazineSize}");
                Field("ReloadTime", w.ReloadTime);
            }

            Field("FireInterval", w.FireInterval);
            Field("Projectiles/Shot", w.ProjectilesPerShot);
            Field("SpreadAngle", $"{w.SpreadAngle}°");
            Field("Proj Speed", w.ProjectileSpeed);
            Field("Proj Lifetime", w.ProjectileLifetime);
            Field("Proj Damage", w.ProjectileDamage);
            Field("ConeHalfAngle", $"{w.ConeHalfAngle}°");
            Field("BodyRotSpeed", w.BodyRotationSpeed);
            Field("AimFollowSharpness", w.AimFollowSharpness);
        }

        void DrawHealth(EId id, Dictionary<EId, HealthState> healthMap)
        {
            if (!healthMap.TryGetValue(id, out var h)) return;

            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float labelW = EditorGUIUtility.labelWidth;

            var labelRect = new Rect(rect.x, rect.y, labelW, rect.height);
            var barRect = new Rect(rect.x + labelW, rect.y, rect.width - labelW, rect.height);

            EditorGUI.LabelField(labelRect, "Health");

            float ratio = h.MaxHp > 0f ? h.CurrentHp / h.MaxHp : 0f;
            EditorGUI.ProgressBar(barRect, ratio,
                $"{h.CurrentHp:F0} / {h.MaxHp:F0}  [{(h.IsAlive ? "Alive" : "Dead")}]");
        }

        static void Field(string label, object value)
        {
            EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
        }

        static void Field(string label, Vector3 v)
        {
            EditorGUILayout.LabelField(label, $"({v.x:F2}, {v.y:F2}, {v.z:F2})");
        }

        static void Field(string label, bool value)
        {
            EditorGUILayout.LabelField(label, value ? "✓" : "✗");
        }

        static void Field(string label, float value)
        {
            EditorGUILayout.LabelField(label, $"{value:F2}");
        }

        static void Field(string label, int value)
        {
            EditorGUILayout.LabelField(label, value.ToString());
        }
    }
}