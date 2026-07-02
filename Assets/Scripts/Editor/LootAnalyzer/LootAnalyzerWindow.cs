using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Constants;
using Quests;
using State;
using UnityEditor;
using UnityEngine;

namespace Editor.LootAnalyzer
{
    /// <summary>
    /// Loot / progression analyzer (approach A — analytic expected value).
    ///
    /// Reads the game's static loot + upgrade + quest tables and answers:
    /// "how many containers / loose-loot pickups / kills — and roughly how many
    /// raids and hours — to finish the whole game (all quests + max hideout)?"
    ///
    /// Model
    ///   DEMAND  = full hideout (every BuildingKind 0→MaxLevel) + all quest item /
    ///             craft / build-weapon demands, netted against quest rewards.
    ///             Kill demand is tracked separately (KillEnemyTask).
    ///   SUPPLY  = expected yield PER ACTION from each source, computed analytically:
    ///               container open  → weighted pick, E[drops] = (min+max)/2
    ///               loose pickup    → uniform pick over the group (matches
    ///                                 LooseLootSpawnPoint.RollItem — NOT weighted)
    ///               kill            → bot's medkits / grenades / ammo / armor
    ///   A configurable RAID PROFILE (actions per successful raid) + RAID SUCCESS
    ///   RATE turn per-action supply into per-raid supply. Loot only banks on a
    ///   successful extract (÷ successRate → attempts); kills accrue every attempt.
    ///
    /// Everything here is expected-value only — no variance. A Monte-Carlo pass
    /// (median / P90 spread) is the natural follow-up (approach B).
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

        // Actions performed in a typical *successful* raid, keyed by source key.
        readonly Dictionary<string, float> _actionsPerRaid = new();

        // --- Output ---
        string _report = "";
        Vector2 _scroll;
        readonly List<SupplySource> _sources = new();

        [MenuItem("Raid/Loot Analyzer")]
        static void Open()
        {
            var win = GetWindow<LootAnalyzerWindow>("Loot Analyzer");
            win.minSize = new Vector2(560, 400);
        }

        void OnEnable()
        {
            _completeHideout = EditorPrefs.GetBool(PrefPrefix + "hideout", true);
            _completeQuests = EditorPrefs.GetBool(PrefPrefix + "quests", true);
            _raidSuccessRate = EditorPrefs.GetFloat(PrefPrefix + "success", 0.5f);
            _raidMinutes = EditorPrefs.GetFloat(PrefPrefix + "minutes", 20f);
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

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Actions per successful raid", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var s in _sources)
            {
                float cur = _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : s.DefaultPerRaid;
                _actionsPerRaid[s.Key] = Mathf.Max(0f, EditorGUILayout.FloatField(s.Display, cur));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Analyze", GUILayout.Height(26))) Analyze();
                if (GUILayout.Button("Copy report", GUILayout.Height(26), GUILayout.Width(110)))
                    EditorGUIUtility.systemCopyBuffer = _report;
                if (GUILayout.Button("Export .md", GUILayout.Height(26), GUILayout.Width(90)))
                    ExportMarkdown();
            }

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
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

            // Containers — weighted pick, E[drops] = (min+max)/2.
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
                    defaultPerRaid: c.TypeId == "RandomLootBox" ? 3f : 2f));
            }

            // Loose loot — abstract rate per group. Uniform pick (matches RollItem).
            foreach (ItemGroup g in Enum.GetValues(typeof(ItemGroup)))
            {
                _sources.Add(new SupplySource(
                    key: "loose." + g,
                    display: $"Loose pickup: {g}",
                    kind: "loose",
                    yield: ExpectedLooseYield(g),
                    defaultPerRaid: g == ItemGroup.Mixed ? 5f : 0f));
            }

            // Bodies — the three real enemy types (Scav / PMC / Boss).
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

        static Dictionary<string, double> ExpectedContainerYield(ContainerTypeConfig c)
        {
            var yield = new Dictionary<string, double>();
            if (c.PossibleDrops == null || c.PossibleDrops.Length == 0) return yield;
            double eDrops = (c.MinDrops + c.MaxDrops) / 2.0;
            double totalW = c.PossibleDrops.Sum(d => (double)d.Weight);
            if (totalW <= 0) return yield;
            foreach (var d in c.PossibleDrops)
            {
                double p = d.Weight / totalW;
                double eCount = (d.MinCount + d.MaxCount) / 2.0;
                Add(yield, d.DefinitionId, eDrops * p * eCount);
            }
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
            // Ammo — all current bot presets fire the BallisticRound payload → Ammo_Rifle,
            // capped at 30 (see LootSystem.CreateLootable). Mirror that here.
            Add(yield, "Ammo_Rifle", 30);
            if (cfg.MedkitCount > 0) Add(yield, "Medkit", cfg.MedkitCount);
            if (cfg.GrenadeCount > 0) Add(yield, "Grenade", cfg.GrenadeCount);
            if (!string.IsNullOrEmpty(cfg.HelmetDefinitionId)) Add(yield, cfg.HelmetDefinitionId, 1);
            if (!string.IsNullOrEmpty(cfg.BodyArmorDefinitionId)) Add(yield, cfg.BodyArmorDefinitionId, 1);
            return yield;
        }

        // ----------------------------------------------------------- Analysis

        void Analyze()
        {
            BuildSources();
            var sb = new StringBuilder();
            var itemDemand = new Dictionary<string, double>();
            var rewards = new Dictionary<string, double>();
            var killDemand = new Dictionary<string, double>(); // botTypeId or "Any"
            var inOneRaidKills = new List<(string type, int count, string quest)>();

            // ---- Demand: hideout ----
            if (_completeHideout)
            {
                foreach (var kv in BuildingConstants.UpgradeRecipes)
                    foreach (var levelStep in kv.Value)
                        foreach (var ing in levelStep)
                            Add(itemDemand, ing.ItemId, ing.Count);
            }

            // ---- Demand: quests ----
            var db = LoadQuestDb();
            List<QuestDatabaseEntry> entries = db != null ? db.Entries.ToList() : new List<QuestDatabaseEntry>();
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
                            case FindAndTransferTask t:
                                Add(itemDemand, t.QuestItemId, Math.Max(1, t.RequiredCount));
                                break;
                            case CraftTask t:
                                Add(itemDemand, t.ItemId, Math.Max(1, t.RequiredCount));
                                break;
                            case BuildWeaponTask t:
                                if (!string.IsNullOrEmpty(t.PayloadId)) Add(itemDemand, t.PayloadId, 1);
                                if (!string.IsNullOrEmpty(t.DeliveryId)) Add(itemDemand, t.DeliveryId, 1);
                                break;
                            case KillEnemyTask t:
                                string key = t.EnemyType == EnemyType.Any ? "Any" : t.EnemyType.ToBotTypeId();
                                Add(killDemand, key, Math.Max(1, t.RequiredCount));
                                if (t.InOneRaid)
                                    inOneRaidKills.Add((key, Math.Max(1, t.RequiredCount), q.DisplayName));
                                break;
                        }
                    }
                    foreach (var r in q.Rewards)
                        if (!string.IsNullOrEmpty(r.ItemId)) Add(rewards, r.ItemId, r.Count);
                }
            }

            // Net demand after quest-chain rewards (buying is out of scope by design).
            var netDemand = new Dictionary<string, double>();
            foreach (var kv in itemDemand)
            {
                double net = kv.Value - (rewards.TryGetValue(kv.Key, out var r) ? r : 0);
                if (net > 0.0001) netDemand[kv.Key] = net;
            }

            // ---- Per-successful-raid supply ----
            var perRaidItem = new Dictionary<string, double>();   // items banked per successful raid
            var perAttemptKills = new Dictionary<string, double>(); // kills per raid attempt, by botTypeId
            foreach (var s in _sources)
            {
                float n = _actionsPerRaid.TryGetValue(s.Key, out var v) ? v : 0f;
                if (n <= 0) continue;
                foreach (var y in s.Yield) Add(perRaidItem, y.Key, y.Value * n);
                if (s.Kind == "kill" && s.BotTypeId != null) Add(perAttemptKills, s.BotTypeId, n);
            }
            double totalKillsPerAttempt = perAttemptKills.Values.Sum();

            // ---- Report header ----
            sb.AppendLine("# Loot / Progression Analysis (expected value)");
            sb.AppendLine();
            sb.AppendLine($"Target: {(_completeHideout ? "max hideout" : "—")}{(_completeHideout && _completeQuests ? " + " : "")}{(_completeQuests ? "all quests" : "")}");
            sb.AppendLine($"Raid success rate: {_raidSuccessRate:P0}   |   {_raidMinutes:0} min / attempt");
            if (db == null)
                sb.AppendLine($"⚠ Quest database not found at {QuestDbPath} — quest demand skipped.");
            sb.AppendLine();

            // ---- Reachability + raids-per-item ----
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

            // ---- Kills ----
            var killLines = new List<(string type, double demand, double perAttempt, double attempts)>();
            foreach (var kv in killDemand)
            {
                double perAtt = kv.Key == "Any" ? totalKillsPerAttempt
                    : perAttemptKills.TryGetValue(kv.Key, out var pa) ? pa : 0;
                double attempts = perAtt > 0 ? kv.Value / perAtt : double.PositiveInfinity;
                killLines.Add((kv.Key, kv.Value, perAtt, attempts));
            }
            killLines.Sort((a, b) => b.attempts.CompareTo(a.attempts));
            double attemptsForKills = killLines.Count > 0 ? killLines[0].attempts : 0;

            double totalAttempts = Math.Max(attemptsForLoot, attemptsForKills);

            // ---- Headline ----
            sb.AppendLine("## Bottom line");
            if (unreachable.Count > 0)
            {
                sb.AppendLine($"❌ **{unreachable.Count} required items have NO loot source** — the game is currently un-completable from loot alone (see below). Numbers below cover only reachable demand.");
            }
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

            // ---- Loot bottlenecks ----
            sb.AppendLine("## Top loot bottlenecks (reachable)");
            sb.AppendLine("| Item | Need | Per raid | Raids |");
            sb.AppendLine("|------|-----:|---------:|------:|");
            foreach (var r in reachable.Take(15))
                sb.AppendLine($"| {Name(r.item)} | {r.demand:0} | {r.perRaid:0.00} | {Math.Ceiling(r.raids):0} |");
            if (reachable.Count == 0) sb.AppendLine("| (none) | | | |");
            sb.AppendLine();

            // ---- Unreachable ----
            if (unreachable.Count > 0)
            {
                sb.AppendLine("## ❌ Unreachable demand (no loot source produces these)");
                sb.AppendLine("These items are required by hideout upgrades / quests but appear in **no** container, loose-loot group, or body drop. Either add them to a loot table, gate them behind crafting, or remove the requirement.");
                sb.AppendLine();
                sb.AppendLine("| Item | Need |");
                sb.AppendLine("|------|-----:|");
                foreach (var u in unreachable)
                    sb.AppendLine($"| {Name(u.item)} | {u.demand:0} |");
                sb.AppendLine();
            }

            // ---- Kills ----
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
                        sb.AppendLine($"> ⚠ '{io.quest}' needs {io.count}× {io.type} **in one raid**, but your profile kills only {perAtt:0.0}/raid — infeasible as configured.");
                }
                sb.AppendLine();
            }

            // ---- Critical path ----
            if (_completeQuests && db != null && entries.Count > 0)
            {
                sb.AppendLine("## Quest critical path (prerequisite order)");
                var ordered = TopoSortQuests(entries, out var cycle);
                if (cycle)
                    sb.AppendLine("> ⚠ Prerequisite cycle detected — order below is partial.");
                int i = 1;
                foreach (var e in ordered)
                {
                    var q = e.Quest;
                    var reqs = e.RequiredQuestIds != null && e.RequiredQuestIds.Length > 0
                        ? " ← " + string.Join(", ", e.RequiredQuestIds) : "";
                    sb.AppendLine($"{i++}. **{q.DisplayName}** (L{q.RequiredLevel}){reqs}");
                }
                sb.AppendLine();
            }

            // ---- Source reference ----
            sb.AppendLine("## Source yields (expected items per action)");
            foreach (var s in _sources)
            {
                if (s.Yield.Count == 0) continue;
                var parts = s.Yield.OrderByDescending(y => y.Value)
                    .Select(y => $"{Name(y.Key)} {y.Value:0.00}");
                sb.AppendLine($"- **{s.Display}**: {string.Join(", ", parts)}");
            }
            sb.AppendLine();
            sb.AppendLine("_Expected-value model (no variance). Add a Monte-Carlo pass for median / P90 spread. Buying is intentionally excluded (does not advance progression). Loose-loot uses an abstract per-raid rate; wire it to scene LooseLootSpawnPoint counts for map-accurate numbers._");

            _report = sb.ToString();
            Repaint();
        }

        // -------------------------------------------------------- Quest utils

        static QuestDatabase LoadQuestDb()
        {
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(QuestDbPath);
            if (db != null) return db;
            var guids = AssetDatabase.FindAssets("t:QuestDatabase");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<QuestDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        /// <summary>Kahn topological sort by RequiredQuestIds, tie-broken by RequiredLevel then name.</summary>
        static List<QuestDatabaseEntry> TopoSortQuests(List<QuestDatabaseEntry> entries, out bool cycle)
        {
            var byId = new Dictionary<string, QuestDatabaseEntry>();
            foreach (var e in entries)
                if (e.Quest != null && !string.IsNullOrEmpty(e.Quest.Id))
                    byId[e.Quest.Id] = e;

            var indeg = byId.Keys.ToDictionary(id => id, _ => 0);
            var dependents = byId.Keys.ToDictionary(id => id, _ => new List<string>());
            foreach (var e in byId.Values)
            {
                if (e.RequiredQuestIds == null) continue;
                foreach (var req in e.RequiredQuestIds)
                    if (byId.ContainsKey(req))
                    {
                        indeg[e.Quest.Id]++;
                        dependents[req].Add(e.Quest.Id);
                    }
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
                var id = ready[0];
                ready.RemoveAt(0);
                result.Add(byId[id]);
                foreach (var dep in dependents[id])
                    if (--indeg[dep] == 0) ready.Add(dep);
            }
            cycle = result.Count < byId.Count;
            if (cycle) // append leftovers so nothing is silently dropped
                foreach (var e in byId.Values)
                    if (!result.Contains(e)) result.Add(e);
            return result;
        }

        // ------------------------------------------------------------- helpers

        static void Add(Dictionary<string, double> d, string key, double v)
        {
            if (string.IsNullOrEmpty(key)) return;
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

            public SupplySource(string key, string display, string kind,
                Dictionary<string, double> yield, float defaultPerRaid)
            {
                Key = key; Display = display; Kind = kind; Yield = yield; DefaultPerRaid = defaultPerRaid;
            }
        }
    }
}
