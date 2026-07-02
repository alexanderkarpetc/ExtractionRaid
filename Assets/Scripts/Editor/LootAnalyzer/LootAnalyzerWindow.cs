using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Constants;
using Quests;
using State;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using View.SpawnPoints;

namespace Editor.LootAnalyzer
{
    /// <summary>
    /// Loot / progression analyzer.
    ///
    /// Answers: "how many containers / loose-loot pickups / kills — and roughly how
    /// many raids and hours — to finish the whole game (all quests + max hideout)?"
    ///
    /// Model
    ///   DEMAND  = full hideout (every BuildingKind 0→MaxLevel) + all quest item /
    ///             craft / build-weapon demands, netted against quest rewards.
    ///             Kill demand tracked separately (KillEnemyTask).
    ///   SUPPLY  = expected yield PER ACTION from each source:
    ///               container open → weighted pick, E[drops] = (min+max)/2
    ///               loose pickup   → uniform pick over the group (matches
    ///                                LooseLootSpawnPoint.RollItem — NOT weighted)
    ///               kill           → bot's medkits / grenades / ammo / armor
    ///   A RAID PROFILE (actions per successful raid) + RAID SUCCESS RATE turn
    ///   per-action supply into per-raid supply. Loot only banks on a successful
    ///   extract (÷ successRate → attempts); kills accrue every attempt.
    ///
    /// Two solvers:
    ///   • Analytic (approach A) — expected value, instant, no variance.
    ///   • Monte-Carlo (approach B) — simulates N playthroughs with real dice rolls
    ///     → median / P10 / P90 raid spread.
    ///
    /// The raid profile can be typed by hand OR derived from a real map via the
    /// Scene Scan (counts LootContainer / LooseLoot / Bot spawn points, weighting
    /// each by its spawnChance).
    /// </summary>
    public class LootAnalyzerWindow : EditorWindow
    {
        const string QuestDbPath = "Assets/Resources/Quests/QuestGraph.questgraph";
        const string PrefPrefix = "ExtractionRaid.LootAnalyzer.";

        // --- Config ---
        bool _completeHideout = true;
        bool _completeQuests = true;
        float _raidSuccessRate = 0.5f;
        float _raidMinutes = 20f;
        int _mcIterations = 2000;

        // Actions performed in a typical *successful* raid, keyed by source key.
        readonly Dictionary<string, float> _actionsPerRaid = new();

        // --- Output ---
        string _report = "";
        Vector2 _scroll;
        readonly List<SupplySource> _sources = new();

        // --- Scene scan ---
        readonly List<MapScan> _scans = new();
        readonly Dictionary<string, bool> _scanFoldout = new();
        [Range(0f, 1f)] float _engagement = 1f;

        // Sections
        bool _showProfile = true;
        bool _showScan;

        [MenuItem("Raid/Loot Analyzer")]
        static void Open()
        {
            var win = GetWindow<LootAnalyzerWindow>("Loot Analyzer");
            win.minSize = new Vector2(600, 460);
        }

        void OnEnable()
        {
            _completeHideout = EditorPrefs.GetBool(PrefPrefix + "hideout", true);
            _completeQuests = EditorPrefs.GetBool(PrefPrefix + "quests", true);
            _raidSuccessRate = EditorPrefs.GetFloat(PrefPrefix + "success", 0.5f);
            _raidMinutes = EditorPrefs.GetFloat(PrefPrefix + "minutes", 20f);
            _engagement = EditorPrefs.GetFloat(PrefPrefix + "engage", 1f);
            _mcIterations = EditorPrefs.GetInt(PrefPrefix + "mcIter", 2000);
            BuildSources();
            foreach (var s in _sources)
                _actionsPerRaid[s.Key] = EditorPrefs.GetFloat(PrefPrefix + "act." + s.Key, s.DefaultPerRaid);
            Analyze();
        }

        void OnDisable()
        {
            EditorPrefs.SetBool(PrefPrefix + "hideout", _completeHideout);
            EditorPrefs.SetBool(PrefPrefix + "quests", _completeQuests);
            EditorPrefs.SetFloat(PrefPrefix + "success", _raidSuccessRate);
            EditorPrefs.SetFloat(PrefPrefix + "minutes", _raidMinutes);
            EditorPrefs.SetFloat(PrefPrefix + "engage", _engagement);
            EditorPrefs.SetInt(PrefPrefix + "mcIter", _mcIterations);
            foreach (var kv in _actionsPerRaid)
                EditorPrefs.SetFloat(PrefPrefix + "act." + kv.Key, kv.Value);
        }

        // ---------------------------------------------------------------- GUI

        void OnGUI()
        {
            EditorGUILayout.LabelField("Completion target", EditorStyles.boldLabel);
            _completeHideout = EditorGUILayout.Toggle("Max out hideout (all buildings → L" + BuildingConstants.MaxLevel + ")", _completeHideout);
            _completeQuests = EditorGUILayout.Toggle("Complete all quests", _completeQuests);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Raid model", EditorStyles.boldLabel);
            _raidSuccessRate = EditorGUILayout.Slider(
                new GUIContent("Raid success rate", "Fraction of raids you extract alive. Loot only banks on success; kills accrue every attempt."),
                _raidSuccessRate, 0.05f, 1f);
            _raidMinutes = EditorGUILayout.Slider(new GUIContent("Minutes / raid attempt"), _raidMinutes, 3f, 60f);

            DrawScanSection();
            DrawProfileSection();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analyze (expected)", GUILayout.Height(26))) Analyze();
                if (GUILayout.Button($"Monte-Carlo ×{_mcIterations}", GUILayout.Height(26))) Analyze(runMonteCarlo: true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _mcIterations = Mathf.Clamp(EditorGUILayout.IntField("MC iterations", _mcIterations), 100, 50000);
                if (GUILayout.Button("Copy report", GUILayout.Width(110))) EditorGUIUtility.systemCopyBuffer = _report;
                if (GUILayout.Button("Export .md", GUILayout.Width(90))) ExportMarkdown();
            }

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void DrawProfileSection()
        {
            EditorGUILayout.Space(6);
            _showProfile = EditorGUILayout.Foldout(_showProfile, "Actions per successful raid", true, EditorStyles.foldoutHeader);
            if (!_showProfile) return;
            EditorGUI.indentLevel++;
            foreach (var s in _sources)
            {
                float cur = _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : s.DefaultPerRaid;
                _actionsPerRaid[s.Key] = Mathf.Max(0f, EditorGUILayout.FloatField(s.Display, cur));
            }
            EditorGUI.indentLevel--;
        }

        void DrawScanSection()
        {
            EditorGUILayout.Space(6);
            _showScan = EditorGUILayout.Foldout(_showScan, "Scene scan (real per-map spawn counts)", true, EditorStyles.foldoutHeader);
            if (!_showScan) return;

            EditorGUI.indentLevel++;
            _engagement = EditorGUILayout.Slider(
                new GUIContent("Engagement fraction", "Share of a map's spawns you actually interact with per raid. Applied when you press 'Use as profile'."),
                _engagement, 0.05f, 1f);
            if (GUILayout.Button("Scan raid scenes"))
                ScanScenes();

            foreach (var scan in _scans)
            {
                _scanFoldout.TryGetValue(scan.SceneName, out var open);
                _scanFoldout[scan.SceneName] = EditorGUILayout.Foldout(open, $"{scan.SceneName}  (C {scan.ContainerTotal():0.#} / L {scan.LooseTotal():0.#} / K {scan.KillTotal():0.#})", true);
                if (!_scanFoldout[scan.SceneName]) continue;

                EditorGUI.indentLevel++;
                foreach (var kv in scan.Containers) EditorGUILayout.LabelField($"Container {kv.Key}", $"{kv.Value:0.##} expected");
                foreach (var kv in scan.Loose) EditorGUILayout.LabelField($"Loose {kv.Key}", $"{kv.Value:0.##} expected");
                foreach (var kv in scan.Kills) EditorGUILayout.LabelField($"Bot {kv.Key}", $"{kv.Value:0.##} expected");
                if (GUILayout.Button($"Use '{scan.SceneName}' as raid profile (×{_engagement:0.##})"))
                    ApplyScanToProfile(scan);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }

        void ExportMarkdown()
        {
            var path = EditorUtility.SaveFilePanel("Export loot analysis", "", "loot-analysis.md", "md");
            if (string.IsNullOrEmpty(path)) return;
            System.IO.File.WriteAllText(path, _report);
            EditorUtility.RevealInFinder(path);
        }

        // ------------------------------------------------------------ Sources

        /// <summary>Expected yield per single action, for every loot source.</summary>
        void BuildSources()
        {
            _sources.Clear();

            foreach (var c in new[]
                     {
                         ContainerConstants.MedContainer, ContainerConstants.AmmoBox,
                         ContainerConstants.RandomLootBox, ContainerConstants.ModuleCache,
                     })
            {
                _sources.Add(new SupplySource(
                    key: "cont." + c.TypeId,
                    display: $"Open: {c.DisplayName}",
                    kind: "container",
                    yield: ExpectedContainerYield(c),
                    defaultPerRaid: c.TypeId == "RandomLootBox" ? 3f : 2f)
                { ContainerConfig = c });
            }

            // Project container assets whose TypeId isn't a built-in preset (scene variants).
            foreach (var guid in AssetDatabase.FindAssets("t:ContainerTypeConfigAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ContainerTypeConfigAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || string.IsNullOrEmpty(asset.TypeId)) continue;
                var key = "cont." + asset.TypeId;
                if (_sources.Any(s => s.Key == key)) continue;
                var cfg = asset.ToContainerTypeConfig();
                _sources.Add(new SupplySource(key, $"Open: {asset.DisplayName}", "container",
                    ExpectedContainerYield(cfg), 0f) { ContainerConfig = cfg });
            }

            foreach (ItemGroup g in Enum.GetValues(typeof(ItemGroup)))
            {
                _sources.Add(new SupplySource(
                    key: "loose." + g,
                    display: $"Loose pickup: {g}",
                    kind: "loose",
                    yield: ExpectedLooseYield(g),
                    defaultPerRaid: g == ItemGroup.Mixed ? 5f : 0f)
                { LooseGroup = g });
            }

            foreach (var typeId in new[] { "Scav", "PMC", "Boss" })
            {
                if (!BotConstants.TryGetConfig(typeId, out var cfg)) continue;
                _sources.Add(new SupplySource(
                    key: "kill." + typeId,
                    display: $"Kill: {typeId}",
                    kind: "kill",
                    yield: ExpectedBodyYield(cfg),
                    defaultPerRaid: typeId == "Scav" ? 4f : typeId == "PMC" ? 2f : 0f)
                { BotTypeId = typeId });
            }
        }

        SupplySource FindSource(string key) => _sources.FirstOrDefault(s => s.Key == key);

        static Dictionary<string, double> ExpectedContainerYield(ContainerTypeConfig c)
        {
            var yield = new Dictionary<string, double>();
            if (c.PossibleDrops == null || c.PossibleDrops.Length == 0) return yield;
            double eDrops = (c.MinDrops + c.MaxDrops) / 2.0;
            double totalW = c.PossibleDrops.Sum(d => (double)d.Weight);
            if (totalW <= 0) return yield;
            foreach (var d in c.PossibleDrops)
                Add(yield, d.DefinitionId, eDrops * (d.Weight / totalW) * (d.MinCount + d.MaxCount) / 2.0);
            return yield;
        }

        static Dictionary<string, double> ExpectedLooseYield(ItemGroup group)
        {
            var yield = new Dictionary<string, double>();
            var drops = ItemGroups.GetDrops(group);
            if (drops == null || drops.Length == 0) return yield;
            double p = 1.0 / drops.Length; // uniform — RollItem picks Random.Range(0, len)
            foreach (var d in drops)
                Add(yield, d.DefinitionId, p * (d.MinCount + d.MaxCount) / 2.0);
            return yield;
        }

        static Dictionary<string, double> ExpectedBodyYield(BotTypeConfig cfg)
        {
            var yield = new Dictionary<string, double>();
            // All current bot presets fire the BallisticRound payload → Ammo_Rifle,
            // capped at 30 (see LootSystem.CreateLootable). Mirror that here.
            Add(yield, "Ammo_Rifle", 30);
            if (cfg.MedkitCount > 0) Add(yield, "Medkit", cfg.MedkitCount);
            if (cfg.GrenadeCount > 0) Add(yield, "Grenade", cfg.GrenadeCount);
            if (!string.IsNullOrEmpty(cfg.HelmetDefinitionId)) Add(yield, cfg.HelmetDefinitionId, 1);
            if (!string.IsNullOrEmpty(cfg.BodyArmorDefinitionId)) Add(yield, cfg.BodyArmorDefinitionId, 1);
            return yield;
        }

        // -------------------------------------------------------- Scene scan

        void ScanScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var setup = EditorSceneManager.GetSceneManagerSetup();
            _scans.Clear();
            try
            {
                var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets/Scenes" });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    // Skip non-raid scenes: menu, hideout, and the isolated shooting-range test scenes.
                    if (name == "MainMenu" || name == "HideoutScene" || path.Contains("ShootingScenes")) continue;

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    var scan = new MapScan { SceneName = name };
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var cp in root.GetComponentsInChildren<LootContainerSpawnPoint>(true))
                            if (cp.ContainerTypeId != null)
                                AddF(scan.Containers, cp.ContainerTypeId, Mathf.Clamp01(cp.spawnChance));
                        foreach (var lp in root.GetComponentsInChildren<LooseLootSpawnPoint>(true))
                        {
                            string k = lp.useItemGroup ? lp.itemGroup.ToString() : "Custom";
                            AddF(scan.Loose, k, Mathf.Clamp01(lp.spawnChance));
                        }
                        foreach (var bp in root.GetComponentsInChildren<BotSpawnPoint>(true))
                            if (bp.config != null)
                                AddF(scan.Kills, bp.config.TypeId, Mathf.Clamp01(bp.spawnChance));
                    }
                    _scans.Add(scan);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        void ApplyScanToProfile(MapScan scan)
        {
            // Zero every source first so the profile reflects only this map.
            foreach (var s in _sources) _actionsPerRaid[s.Key] = 0f;

            foreach (var kv in scan.Containers)
            {
                var key = "cont." + kv.Key;
                if (FindSource(key) == null && Constants.ContainerConstants.TryGetConfig(kv.Key, out var cfg))
                    _sources.Add(new SupplySource(key, $"Open: {cfg.DisplayName}", "container",
                        ExpectedContainerYield(cfg), 0f) { ContainerConfig = cfg });
                _actionsPerRaid[key] = kv.Value * _engagement;
            }
            foreach (var kv in scan.Loose)
            {
                if (kv.Key == "Custom") continue; // custom loose pools aren't in the shared groups
                _actionsPerRaid["loose." + kv.Key] = kv.Value * _engagement;
            }
            foreach (var kv in scan.Kills)
            {
                var key = "kill." + kv.Key;
                if (FindSource(key) != null) _actionsPerRaid[key] = kv.Value * _engagement;
                // Non enemy-type bots (test targets) are ignored — they don't satisfy kill quests.
            }
            _showProfile = true;
            Analyze();
        }

        // ----------------------------------------------------------- Analysis

        void Analyze(bool runMonteCarlo = false)
        {
            BuildSources();
            var sb = new StringBuilder();
            var itemDemand = new Dictionary<string, double>();
            var rewards = new Dictionary<string, double>();
            var killDemand = new Dictionary<string, double>();
            var inOneRaidKills = new List<(string type, int count, string quest)>();

            if (_completeHideout)
                foreach (var kv in BuildingConstants.UpgradeRecipes)
                    foreach (var step in kv.Value)
                        foreach (var ing in step)
                            Add(itemDemand, ing.ItemId, ing.Count);

            var db = LoadQuestDb();
            var entries = db != null ? db.Entries.ToList() : new List<QuestDatabaseEntry>();
            if (_completeQuests && db != null)
            {
                foreach (var e in entries)
                {
                    var q = e.Quest;
                    if (q == null) continue;
                    foreach (var task in q.Tasks)
                    {
                        switch (task)
                        {
                            case FindAndTransferTask t: Add(itemDemand, t.QuestItemId, Math.Max(1, t.RequiredCount)); break;
                            case CraftTask t: Add(itemDemand, t.ItemId, Math.Max(1, t.RequiredCount)); break;
                            case BuildWeaponTask t:
                                if (!string.IsNullOrEmpty(t.PayloadId)) Add(itemDemand, t.PayloadId, 1);
                                if (!string.IsNullOrEmpty(t.DeliveryId)) Add(itemDemand, t.DeliveryId, 1);
                                break;
                            case KillEnemyTask t:
                                string key = t.EnemyType == EnemyType.Any ? "Any" : t.EnemyType.ToBotTypeId();
                                Add(killDemand, key, Math.Max(1, t.RequiredCount));
                                if (t.InOneRaid) inOneRaidKills.Add((key, Math.Max(1, t.RequiredCount), q.DisplayName));
                                break;
                        }
                    }
                    foreach (var r in q.Rewards)
                        if (!string.IsNullOrEmpty(r.ItemId)) Add(rewards, r.ItemId, r.Count);
                }
            }

            var netDemand = new Dictionary<string, double>();
            foreach (var kv in itemDemand)
            {
                double net = kv.Value - (rewards.TryGetValue(kv.Key, out var r) ? r : 0);
                if (net > 0.0001) netDemand[kv.Key] = net;
            }

            // Per-successful-raid supply.
            var perRaidItem = new Dictionary<string, double>();
            var perAttemptKills = new Dictionary<string, double>();
            foreach (var s in _sources)
            {
                float n = _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : 0f;
                if (n <= 0) continue;
                foreach (var y in s.Yield) Add(perRaidItem, y.Key, y.Value * n);
                if (s.Kind == "kill" && s.BotTypeId != null) Add(perAttemptKills, s.BotTypeId, n);
            }
            double totalKillsPerAttempt = perAttemptKills.Values.Sum();

            // Header.
            sb.AppendLine("# Loot / Progression Analysis");
            sb.AppendLine();
            sb.AppendLine($"Target: {(_completeHideout ? "max hideout" : "—")}{(_completeHideout && _completeQuests ? " + " : "")}{(_completeQuests ? "all quests" : "")}");
            sb.AppendLine($"Raid success rate: {_raidSuccessRate:P0}   |   {_raidMinutes:0} min / attempt");
            if (db == null) sb.AppendLine($"⚠ Quest database not found at {QuestDbPath} — quest demand skipped.");
            sb.AppendLine();

            // Reachability + analytic raids-per-item.
            var reachable = new List<(string item, double demand, double perRaid, double raids)>();
            var unreachable = new List<(string item, double demand)>();
            foreach (var kv in netDemand)
            {
                double supply = perRaidItem.TryGetValue(kv.Key, out var sup) ? sup : 0;
                if (supply <= 0) unreachable.Add((kv.Key, kv.Value));
                else reachable.Add((kv.Key, kv.Value, supply, kv.Value / supply));
            }
            reachable.Sort((a, b) => b.raids.CompareTo(a.raids));
            unreachable.Sort((a, b) => b.demand.CompareTo(a.demand));

            double successfulRaidsForLoot = reachable.Count > 0 ? reachable[0].raids : 0;
            double attemptsForLoot = _raidSuccessRate > 0 ? successfulRaidsForLoot / _raidSuccessRate : double.PositiveInfinity;

            var killLines = new List<(string type, double demand, double perAttempt, double attempts)>();
            foreach (var kv in killDemand)
            {
                double perAtt = kv.Key == "Any" ? totalKillsPerAttempt
                    : perAttemptKills.TryGetValue(kv.Key, out var pa) ? pa : 0;
                killLines.Add((kv.Key, kv.Value, perAtt, perAtt > 0 ? kv.Value / perAtt : double.PositiveInfinity));
            }
            killLines.Sort((a, b) => b.attempts.CompareTo(a.attempts));
            double attemptsForKills = killLines.Count > 0 ? killLines[0].attempts : 0;
            double totalAttempts = Math.Max(attemptsForLoot, attemptsForKills);

            // Bottom line (analytic).
            sb.AppendLine("## Bottom line (expected value)");
            if (unreachable.Count > 0)
                sb.AppendLine($"❌ **{unreachable.Count} required items have NO loot source** — un-completable from loot alone (listed below). Numbers cover reachable demand only.");
            if (double.IsInfinity(totalAttempts))
                sb.AppendLine("Estimated raids: **∞** (a kill target has no matching enemy in your raid profile).");
            else
            {
                double hours = totalAttempts * _raidMinutes / 60.0;
                sb.AppendLine($"Estimated **{Math.Ceiling(totalAttempts):0} raid attempts** (~{Math.Ceiling(successfulRaidsForLoot):0} successful) → **~{hours:0.0} hours** at {_raidMinutes:0} min/raid.");
                string driver = attemptsForLoot >= attemptsForKills
                    ? (reachable.Count > 0 ? $"loot item '{Name(reachable[0].item)}'" : "loot")
                    : $"kill target '{killLines[0].type}'";
                sb.AppendLine($"Bottleneck: **{driver}**.");
            }
            sb.AppendLine();

            // Monte-Carlo.
            if (runMonteCarlo)
                AppendMonteCarlo(sb, netDemand, killDemand, perRaidItem, perAttemptKills, totalKillsPerAttempt, unreachable);

            // Loot bottlenecks.
            sb.AppendLine("## Top loot bottlenecks (reachable)");
            sb.AppendLine("| Item | Need | Per raid | Raids |");
            sb.AppendLine("|------|-----:|---------:|------:|");
            foreach (var r in reachable.Take(15))
                sb.AppendLine($"| {Name(r.item)} | {r.demand:0} | {r.perRaid:0.00} | {Math.Ceiling(r.raids):0} |");
            if (reachable.Count == 0) sb.AppendLine("| (none) | | | |");
            sb.AppendLine();

            if (unreachable.Count > 0)
            {
                sb.AppendLine("## ❌ Unreachable demand (no loot source produces these)");
                sb.AppendLine("Required by hideout upgrades / quests but present in **no** container, loose-loot group, or body drop. Add them to a loot table, gate behind crafting, or drop the requirement.");
                sb.AppendLine();
                sb.AppendLine("| Item | Need |");
                sb.AppendLine("|------|-----:|");
                foreach (var u in unreachable) sb.AppendLine($"| {Name(u.item)} | {u.demand:0} |");
                sb.AppendLine();
            }

            if (killDemand.Count > 0)
            {
                sb.AppendLine("## Kill targets");
                sb.AppendLine("| Enemy | Need | Per attempt | Attempts |");
                sb.AppendLine("|-------|-----:|------------:|---------:|");
                foreach (var k in killLines)
                    sb.AppendLine($"| {k.type} | {k.demand:0} | {k.perAttempt:0.00} | {(double.IsInfinity(k.attempts) ? "∞" : Math.Ceiling(k.attempts).ToString("0"))} |");
                foreach (var io in inOneRaidKills)
                {
                    double perAtt = io.type == "Any" ? totalKillsPerAttempt
                        : perAttemptKills.TryGetValue(io.type, out var pa) ? pa : 0;
                    if (perAtt < io.count)
                        sb.AppendLine($"> ⚠ '{io.quest}' needs {io.count}× {io.type} **in one raid**, but the profile kills only {perAtt:0.0}/raid — infeasible as configured.");
                }
                sb.AppendLine();
            }

            if (_completeQuests && db != null && entries.Count > 0)
            {
                sb.AppendLine("## Quest critical path (prerequisite order)");
                var ordered = TopoSortQuests(entries, out var cycle);
                if (cycle) sb.AppendLine("> ⚠ Prerequisite cycle detected — order below is partial.");
                int i = 1;
                foreach (var e in ordered)
                {
                    var reqs = e.RequiredQuestIds != null && e.RequiredQuestIds.Length > 0 ? " ← " + string.Join(", ", e.RequiredQuestIds) : "";
                    sb.AppendLine($"{i++}. **{e.Quest.DisplayName}** (L{e.Quest.RequiredLevel}){reqs}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Source yields (expected items per action)");
            foreach (var s in _sources)
            {
                if (s.Yield.Count == 0) continue;
                var parts = s.Yield.OrderByDescending(y => y.Value).Select(y => $"{Name(y.Key)} {y.Value:0.00}");
                sb.AppendLine($"- **{s.Display}**: {string.Join(", ", parts)}");
            }
            sb.AppendLine();
            sb.AppendLine("_Buying is excluded by design (does not advance progression). Loose-loot uses the current raid profile; press Scan → 'Use as profile' for map-accurate counts._");

            _report = sb.ToString();
            Repaint();
        }

        // ----------------------------------------------------- Monte-Carlo (B)

        void AppendMonteCarlo(StringBuilder sb,
            Dictionary<string, double> netDemand, Dictionary<string, double> killDemand,
            Dictionary<string, double> perRaidItem, Dictionary<string, double> perAttemptKills,
            double totalKillsPerAttempt, List<(string item, double demand)> unreachable)
        {
            // Only simulate reachable demand — unreachable items would loop forever.
            var targets = netDemand.Where(kv => perRaidItem.TryGetValue(kv.Key, out var s) && s > 0)
                                   .ToDictionary(kv => kv.Key, kv => (int)Math.Ceiling(kv.Value));
            bool killsFeasible = killDemand.All(kv =>
                kv.Key == "Any" ? totalKillsPerAttempt > 0 : perAttemptKills.TryGetValue(kv.Key, out var p) && p > 0);

            sb.AppendLine("## Monte-Carlo (simulated dice)");
            if (!killsFeasible)
            {
                sb.AppendLine("> ⚠ A kill target can't be met with the current profile — skipping simulation.");
                sb.AppendLine();
                return;
            }

            const int CapRaids = 200000;
            var rng = new System.Random(12345); // fixed seed → reproducible report
            var containerActions = _sources.Where(s => s.Kind == "container")
                .Select(s => (cfg: s.ContainerConfig, n: _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : 0f))
                .Where(x => x.n > 0).ToArray();
            var looseActions = _sources.Where(s => s.Kind == "loose")
                .Select(s => (drops: ItemGroups.GetDrops(s.LooseGroup), n: _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : 0f))
                .Where(x => x.n > 0 && x.drops != null && x.drops.Length > 0).ToArray();
            var killActions = _sources.Where(s => s.Kind == "kill")
                .Select(s => (type: s.BotTypeId, yield: s.Yield, n: _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : 0f))
                .Where(x => x.n > 0).ToArray();

            var results = new int[_mcIterations];
            for (int sim = 0; sim < _mcIterations; sim++)
            {
                var banked = new Dictionary<string, int>();
                var kills = new Dictionary<string, int>();
                int totalKills = 0;
                int raids = 0;
                while (raids < CapRaids && !Complete(banked, kills, totalKills, targets, killDemand, totalKillsPerAttempt))
                {
                    raids++;
                    bool success = rng.NextDouble() < _raidSuccessRate;

                    // Kills accrue every attempt.
                    foreach (var (type, yield, n) in killActions)
                    {
                        int c = RollCount(rng, n);
                        if (c == 0) continue;
                        kills[type] = (kills.TryGetValue(type, out var k) ? k : 0) + c;
                        totalKills += c;
                        if (success)
                            foreach (var y in yield) AddI(banked, y.Key, (int)Math.Round(y.Value * c));
                    }
                    if (!success) continue;

                    foreach (var (cfg, n) in containerActions)
                        for (int a = RollCount(rng, n); a > 0; a--)
                            RollContainer(rng, cfg, banked);
                    foreach (var (drops, n) in looseActions)
                        for (int a = RollCount(rng, n); a > 0; a--)
                        {
                            var d = drops[rng.Next(drops.Length)];
                            AddI(banked, d.DefinitionId, RollRange(rng, d.MinCount, d.MaxCount));
                        }
                }
                results[sim] = raids;
            }

            Array.Sort(results);
            int median = results[_mcIterations / 2];
            int p10 = results[(int)(_mcIterations * 0.10)];
            int p90 = results[(int)(_mcIterations * 0.90)];
            double mean = results.Average();
            bool hitCap = results[_mcIterations - 1] >= CapRaids;

            sb.AppendLine($"{_mcIterations} simulated playthroughs (raid attempts to completion):");
            sb.AppendLine();
            sb.AppendLine("| Percentile | Raids | Hours |");
            sb.AppendLine("|-----------|------:|------:|");
            sb.AppendLine($"| P10 (lucky) | {p10} | {p10 * _raidMinutes / 60.0:0.0} |");
            sb.AppendLine($"| Median | {median} | {median * _raidMinutes / 60.0:0.0} |");
            sb.AppendLine($"| Mean | {mean:0.0} | {mean * _raidMinutes / 60.0:0.0} |");
            sb.AppendLine($"| P90 (unlucky) | {p90} | {p90 * _raidMinutes / 60.0:0.0} |");
            if (hitCap) sb.AppendLine($"> ⚠ Some sims hit the {CapRaids}-raid cap — supply is barely above demand for some item.");
            if (unreachable.Count > 0) sb.AppendLine($"> Note: {unreachable.Count} unreachable items excluded from the simulation.");
            sb.AppendLine();
        }

        static bool Complete(Dictionary<string, int> banked, Dictionary<string, int> kills, int totalKills,
            Dictionary<string, int> targets, Dictionary<string, double> killDemand, double totalKillsPerAttempt)
        {
            foreach (var t in targets)
                if ((banked.TryGetValue(t.Key, out var b) ? b : 0) < t.Value) return false;
            foreach (var kv in killDemand)
            {
                int have = kv.Key == "Any" ? totalKills : (kills.TryGetValue(kv.Key, out var k) ? k : 0);
                if (have < kv.Value) return false;
            }
            return true;
        }

        static void RollContainer(System.Random rng, ContainerTypeConfig c, Dictionary<string, int> banked)
        {
            if (c.PossibleDrops == null || c.PossibleDrops.Length == 0) return;
            int drops = RollRange(rng, c.MinDrops, c.MaxDrops);
            double totalW = c.PossibleDrops.Sum(d => (double)d.Weight);
            for (int i = 0; i < drops; i++)
            {
                double roll = rng.NextDouble() * totalW, acc = 0;
                foreach (var d in c.PossibleDrops)
                {
                    acc += d.Weight;
                    if (roll <= acc) { AddI(banked, d.DefinitionId, RollRange(rng, d.MinCount, d.MaxCount)); break; }
                }
            }
        }

        // Fractional action count → floor + Bernoulli(remainder).
        static int RollCount(System.Random rng, float n)
        {
            int whole = (int)n;
            return whole + (rng.NextDouble() < n - whole ? 1 : 0);
        }

        static int RollRange(System.Random rng, int min, int max) => min >= max ? min : rng.Next(min, max + 1);

        // -------------------------------------------------------- Quest utils

        static QuestDatabase LoadQuestDb()
        {
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(QuestDbPath);
            if (db != null) return db;
            var guids = AssetDatabase.FindAssets("t:QuestDatabase");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<QuestDatabase>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        static List<QuestDatabaseEntry> TopoSortQuests(List<QuestDatabaseEntry> entries, out bool cycle)
        {
            var byId = new Dictionary<string, QuestDatabaseEntry>();
            foreach (var e in entries)
                if (e.Quest != null && !string.IsNullOrEmpty(e.Quest.Id)) byId[e.Quest.Id] = e;

            var indeg = byId.Keys.ToDictionary(id => id, _ => 0);
            var dependents = byId.Keys.ToDictionary(id => id, _ => new List<string>());
            foreach (var e in byId.Values)
            {
                if (e.RequiredQuestIds == null) continue;
                foreach (var req in e.RequiredQuestIds)
                    if (byId.ContainsKey(req)) { indeg[e.Quest.Id]++; dependents[req].Add(e.Quest.Id); }
            }

            var ready = new List<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var result = new List<QuestDatabaseEntry>();
            Comparison<string> byLevel = (a, b) =>
            {
                int c = byId[a].Quest.RequiredLevel.CompareTo(byId[b].Quest.RequiredLevel);
                return c != 0 ? c : string.Compare(byId[a].Quest.DisplayName, byId[b].Quest.DisplayName, StringComparison.Ordinal);
            };
            while (ready.Count > 0)
            {
                ready.Sort(byLevel);
                var id = ready[0]; ready.RemoveAt(0);
                result.Add(byId[id]);
                foreach (var dep in dependents[id]) if (--indeg[dep] == 0) ready.Add(dep);
            }
            cycle = result.Count < byId.Count;
            if (cycle) foreach (var e in byId.Values) if (!result.Contains(e)) result.Add(e);
            return result;
        }

        // ------------------------------------------------------------- helpers

        static void Add(Dictionary<string, double> d, string key, double v)
        {
            if (string.IsNullOrEmpty(key)) return;
            d[key] = (d.TryGetValue(key, out var cur) ? cur : 0) + v;
        }

        static void AddF(Dictionary<string, float> d, string key, float v)
        {
            if (string.IsNullOrEmpty(key)) return;
            d[key] = (d.TryGetValue(key, out var cur) ? cur : 0) + v;
        }

        static void AddI(Dictionary<string, int> d, string key, int v)
        {
            if (string.IsNullOrEmpty(key) || v == 0) return;
            d[key] = (d.TryGetValue(key, out var cur) ? cur : 0) + v;
        }

        static string Name(string id)
        {
            var def = ItemDefinition.Get(id);
            return def != null ? def.DisplayName : id;
        }

        class SupplySource
        {
            public readonly string Key;
            public readonly string Display;
            public readonly string Kind; // container | loose | kill
            public readonly Dictionary<string, double> Yield;
            public readonly float DefaultPerRaid;
            public string BotTypeId;
            public ItemGroup LooseGroup;
            public ContainerTypeConfig ContainerConfig;

            public SupplySource(string key, string display, string kind,
                Dictionary<string, double> yield, float defaultPerRaid)
            {
                Key = key; Display = display; Kind = kind; Yield = yield; DefaultPerRaid = defaultPerRaid;
            }
        }

        class MapScan
        {
            public string SceneName;
            public readonly Dictionary<string, float> Containers = new();
            public readonly Dictionary<string, float> Loose = new();
            public readonly Dictionary<string, float> Kills = new();
            public float ContainerTotal() => Containers.Values.Sum();
            public float LooseTotal() => Loose.Values.Sum();
            public float KillTotal() => Kills.Values.Sum();
        }
    }
}
