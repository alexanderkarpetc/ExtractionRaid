using System;
using System.Collections.Generic;
using System.Text;
using Adapters;
using Constants;
using Progression;
using State;
using Systems.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using View.SpawnPoints;

namespace Editor.Meta
{
    // ── Serializable snapshot cache (survives Play↔Edit + Unity restart via EditorPrefs) ──
    // Scanning needs the scene open (edit mode); looting needs a live Player (play mode).
    // We can't do both at once, so the scan bakes each region's spawns into asset
    // references (GUIDs) + plain values, and the loot step re-loads + re-rolls from that.

    [Serializable]
    public class RegionCache
    {
        public string mapPath;
        public string scannedAtUtc;
        public List<RegionSnapshot> regions = new();
    }

    [Serializable]
    public class RegionSnapshot
    {
        public string name;
        // Danger knob authored on the MapRegion — drives the survival roll (see RaidCombatSimulator).
        public float difficulty = 1f;
        public List<ContainerSpawn> containers = new();
        public List<LooseSpawn> loose = new();
        public List<BotSpawn> bots = new();
    }

    [Serializable] public class ContainerSpawn { public string configGuid; public string typeId; public float spawnChance; }
    [Serializable] public class LooseCustom { public string id; public int min; public int max; }
    [Serializable] public class LooseSpawn { public bool useItemGroup; public int itemGroup; public List<LooseCustom> custom = new(); public float spawnChance; }
    [Serializable] public class BotSpawn { public string configGuid; public string typeId; public float spawnChance; }

    /// <summary>
    /// Scan / cache / roll driver for the DevCheats <c>🌍 Meta → Region raid simulator</c>.
    /// See <see cref="RegionCache"/> for the two-phase (edit-scan / play-loot) rationale.
    /// </summary>
    public static class RaidSimRegions
    {
        const string PrefKey = "ExtractionRaid.Meta.RegionCache";
        const string DefaultMapPath = "Assets/Scenes/Test_Map.unity";

        // ─────────────────────────────────────────── Cache I/O ──

        public static RegionCache LoadCache()
        {
            var json = EditorPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<RegionCache>(json); }
            catch { return null; }
        }

        static void SaveCache(RegionCache cache)
            => EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(cache));

        // ────────────────────────────────────── Scan (edit mode) ──

        static string ResolveMapPath()
        {
            if (System.IO.File.Exists(DefaultMapPath)) return DefaultMapPath;
            var guids = AssetDatabase.FindAssets("Test_Map t:SceneAsset");
            return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
        }

        /// <summary>Opens Test_Map additively, buckets every loot/enemy spawn into the
        /// region polygon that contains it, and caches the result. Edit mode only.</summary>
        public static RegionCache Scan(out string status)
        {
            if (Application.isPlaying)
            {
                status = "Exit Play Mode to scan — loading the map scene needs edit mode.";
                return null;
            }

            var path = ResolveMapPath();
            if (string.IsNullOrEmpty(path))
            {
                status = "Test_Map.unity not found under the project.";
                return null;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                status = "Scan cancelled (unsaved scenes).";
                return null;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var cache = new RegionCache { mapPath = path, scannedAtUtc = DateTime.UtcNow.ToString("u") };
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                var regions = new List<MapRegion>();
                var containers = new List<LootContainerSpawnPoint>();
                var loosePts = new List<LooseLootSpawnPoint>();
                var botPts = new List<BotSpawnPoint>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    regions.AddRange(root.GetComponentsInChildren<MapRegion>(true));
                    containers.AddRange(root.GetComponentsInChildren<LootContainerSpawnPoint>(true));
                    loosePts.AddRange(root.GetComponentsInChildren<LooseLootSpawnPoint>(true));
                    botPts.AddRange(root.GetComponentsInChildren<BotSpawnPoint>(true));
                }

                foreach (var region in regions)
                {
                    if (!region.IsValid) continue;
                    var snap = new RegionSnapshot
                    {
                        name = string.IsNullOrEmpty(region.regionName) ? region.name : region.regionName,
                        difficulty = Mathf.Max(0.1f, region.difficultyMultiplier),
                    };

                    foreach (var c in containers)
                    {
                        if (c.config == null || !region.ContainsXZ(c.transform.position)) continue;
                        snap.containers.Add(new ContainerSpawn
                        {
                            configGuid = GuidOf(c.config),
                            typeId = c.ContainerTypeId,
                            spawnChance = Mathf.Clamp01(c.spawnChance),
                        });
                    }

                    foreach (var l in loosePts)
                    {
                        if (!region.ContainsXZ(l.transform.position)) continue;
                        var ls = new LooseSpawn
                        {
                            useItemGroup = l.useItemGroup,
                            itemGroup = (int)l.itemGroup,
                            spawnChance = Mathf.Clamp01(l.spawnChance),
                        };
                        if (!l.useItemGroup && l.customItems != null)
                            foreach (var ci in l.customItems)
                                ls.custom.Add(new LooseCustom { id = ci.definitionId, min = ci.minCount, max = ci.maxCount });
                        snap.loose.Add(ls);
                    }

                    foreach (var b in botPts)
                    {
                        if (b.config == null || !region.ContainsXZ(b.transform.position)) continue;
                        snap.bots.Add(new BotSpawn
                        {
                            configGuid = GuidOf(b.config),
                            typeId = b.config.TypeId,
                            spawnChance = Mathf.Clamp01(b.spawnChance),
                        });
                    }

                    cache.regions.Add(snap);
                }
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            SaveCache(cache);
            status = cache.regions.Count == 0
                ? "Scan complete — no MapRegion polygons found in Test_Map. Add some (≥3 points each)."
                : $"Scanned {cache.regions.Count} region(s) from {System.IO.Path.GetFileName(path)}.";
            return cache;
        }

        static string GuidOf(UnityEngine.Object asset)
        {
            var p = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(p) ? "" : AssetDatabase.AssetPathToGUID(p);
        }

        // ───────────────────────────────── Threat (play mode) ──

        /// <summary>Total HP the region's garrison fields — the ammo bill is priced off this.</summary>
        public static void MeasureThreat(RegionSnapshot snap, out int enemyCount, out float totalHp)
        {
            enemyCount = 0; totalHp = 0f;
            if (snap?.bots == null) return;
            foreach (var b in snap.bots)
            {
                if (!TryLoadBotConfig(b, out var cfg)) continue;
                enemyCount++;
                totalHp += Mathf.Max(1f, cfg.MaxHp);
            }
        }

        /// <summary>Prices the fight (ammo + survival odds) without committing to it.</summary>
        public static RaidCombatSimulator.Plan PlanRegion(
            RegionSnapshot snap, InventoryState inv, ICoreDefinitionRegistry registry,
            ProgressionTreeConfig tree, PlayerProgressionState progress)
        {
            MeasureThreat(snap, out var enemyCount, out var totalHp);
            return RaidCombatSimulator.BuildPlan(
                enemyCount, totalHp, snap?.difficulty ?? 1f, inv, registry, tree, progress);
        }

        // ────────────────────────────────────── Loot (play mode) ──

        /// <summary>Result of a full sim run: the fight, then (if you lived) the haul.</summary>
        public struct RaidResult
        {
            public RaidCombatSimulator.Plan Plan;
            public RaidCombatSimulator.Outcome Outcome;
            public RegionLootSimulator.FillResult Fill;
            public bool LootTaken;      // false = died
        }

        /// <summary>
        /// Full risk/reward run at a region: clear the garrison (burns reserve ammo, rolls
        /// survival), and only on a survived roll pour the most valuable loot into the backpack.
        /// Dying mirrors the live KIA path — the whole inventory is forfeit (App.EndRaid).
        /// </summary>
        public static RaidResult RaidRegion(
            RegionSnapshot snap, InventoryState inv, Func<EId> alloc,
            ICoreDefinitionRegistry registry, ProgressionTreeConfig tree,
            PlayerProgressionState progress, out string log)
        {
            var res = new RaidResult
            {
                Plan = PlanRegion(snap, inv, registry, tree, progress),
            };

            res.Outcome = RaidCombatSimulator.Resolve(res.Plan, inv);

            var sb = new StringBuilder();
            if (res.Plan.EnemyCount == 0)
            {
                sb.AppendLine($"'{snap.name}' is undefended — walked in for free.");
            }
            else
            {
                string bill = res.Plan.HasWeapon
                    ? $"spent {res.Outcome.RoundsSpent}/{res.Plan.RoundsNeeded} {res.Plan.AmmoType}"
                    : "no gun — fought it out bare-handed";
                sb.AppendLine($"Fought '{snap.name}' (×{res.Plan.Difficulty:0.##} difficulty): " +
                              $"{res.Plan.EnemyCount} enemy(ies), {res.Plan.TotalEnemyHp:0} HP → {bill}.");
                sb.AppendLine($"Survival {res.Plan.SurviveChance:P0} " +
                              $"(gear {res.Plan.GearScore:P0}, skills +{res.Plan.SkillBonus:P0}" +
                              (res.Plan.Shortfall > 0 ? $", ammo short ×{res.Plan.AmmoPenalty:0.00}" : "") +
                              (res.Plan.WeaponPenalty < 1f ? $", no gun ×{res.Plan.WeaponPenalty:0.00}" : "") +
                              $") · rolled {res.Outcome.Roll:0.00}.");
            }

            if (!res.Outcome.Survived)
            {
                inv.ClearAll();
                sb.Append("☠ KIA — no loot, and everything you carried is gone (weapons, armor, backpack).");
                log = sb.ToString();
                return res;
            }

            res.LootTaken = true;
            res.Fill = LootRegion(snap, inv, alloc, out var lootLog);
            sb.AppendLine("✔ Extracted.");
            sb.Append(lootLog);
            log = sb.ToString();
            return res;
        }

        /// <summary>Rolls every spawn in <paramref name="snap"/> (respecting spawn
        /// chance) and pours the most valuable loot into the backpack.</summary>
        public static RegionLootSimulator.FillResult LootRegion(
            RegionSnapshot snap, InventoryState inv, Func<EId> alloc, out string log)
        {
            // Loot EVERYTHING in the region — spawn chance is ignored on purpose: the
            // fantasy is "you just cleared this whole area". What actually drops inside
            // each container / body is still rolled (min-max drops, weighted picks), and
            // the backpack capacity is the only real limit (FillBackpackByValue).
            var rolled = new List<RegionLootSimulator.Rolled>();
            int containersHit = 0, botsHit = 0, looseHit = 0;

            foreach (var c in snap.containers)
            {
                var cfg = LoadContainerConfig(c);
                if (cfg == null) continue;
                RegionLootSimulator.RollContainer(cfg.Value, rolled);
                containersHit++;
            }

            foreach (var l in snap.loose)
            {
                if (l.useItemGroup)
                    RegionLootSimulator.RollLooseGroup((ItemGroup)l.itemGroup, rolled);
                else
                {
                    var custom = new List<(string, int, int)>();
                    if (l.custom != null)
                        foreach (var ci in l.custom) custom.Add((ci.id, ci.min, ci.max));
                    RegionLootSimulator.RollLooseCustom(custom, rolled);
                }
                looseHit++;
            }

            foreach (var b in snap.bots)
            {
                if (!TryLoadBotConfig(b, out var cfg)) continue;
                RegionLootSimulator.RollBot(cfg, rolled);
                botsHit++;
            }

            var result = RegionLootSimulator.FillBackpackByValue(inv, rolled, alloc);

            var sb = new StringBuilder();
            sb.AppendLine($"Looted region '{snap.name}': {containersHit} container(s), {looseHit} loose, {botsHit} enemy body(ies).");
            sb.AppendLine($"Kept {result.UnitsBanked} unit(s) across {result.DistinctBanked} slot(s) " +
                          $"({result.WeaponsBanked} gun(s)), worth {result.ValueBanked}¢.");
            sb.Append($"Backpack {result.SlotsUsed}/{result.SlotsCapacity} slots (most valuable kept).");
            if (result.Skipped != null && result.Skipped.Count > 0)
            {
                int skippedUnits = 0;
                foreach (var s in result.Skipped) skippedUnits += s.Count;
                sb.Append($"  ⚠ {skippedUnits} unit(s) left behind — backpack full.");
            }
            log = sb.ToString();
            return result;
        }

        static ContainerTypeConfig? LoadContainerConfig(ContainerSpawn c)
        {
            if (!string.IsNullOrEmpty(c.configGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(c.configGuid);
                var asset = AssetDatabase.LoadAssetAtPath<ContainerTypeConfigAsset>(path);
                if (asset != null) return asset.ToContainerTypeConfig();
            }
            // Fallback: built-in preset by type id.
            if (!string.IsNullOrEmpty(c.typeId) && ContainerConstants.TryGetConfig(c.typeId, out var cfg))
                return cfg;
            return null;
        }

        static bool TryLoadBotConfig(BotSpawn b, out BotTypeConfig cfg)
        {
            cfg = default;
            if (!string.IsNullOrEmpty(b.configGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(b.configGuid);
                var asset = AssetDatabase.LoadAssetAtPath<BotTypeConfigAsset>(path);
                if (asset != null)
                {
                    asset.ApplyToRegistry(); // bake loot table + stats into BotConstants
                    return BotConstants.TryGetConfig(asset.TypeId, out cfg);
                }
            }
            return !string.IsNullOrEmpty(b.typeId) && BotConstants.TryGetConfig(b.typeId, out cfg);
        }
    }
}
