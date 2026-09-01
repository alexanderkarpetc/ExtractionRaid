using System.Collections.Generic;
using Adapters;
using ApplicationCore;
using Constants;
using Dev;
using Progression;
using Quests;
using Systems;
using Systems.Bot;
using State;
using UnityEngine;
using View.SpawnPoints;

namespace Session
{
    public class RaidSession
    {
        public RaidState RaidState { get; private set; }
        public LevelState LevelState { get; private set; }
        public bool IsActive => RaidState.IsRunning;

        readonly RaidEventBuffer _eventBuffer;
        readonly ITimeAdapter _timeAdapter;
        readonly IInputAdapter _inputAdapter;
        readonly INavMeshAdapter _navMeshAdapter;
        readonly IPhysicsAdapter _physicsAdapter;
        readonly IGrenadePositionAdapter _grenadePositionAdapter;
        readonly ICoreDefinitionRegistry _coreDefinitions;
        readonly System.Func<EId> _allocateEId;
        readonly List<HitSignal> _hitInbox = new();
        readonly List<CollisionSignal> _collisionInbox = new();

        public RaidSession(string levelId, System.Func<EId> allocateEId,
            ITimeAdapter timeAdapter, IInputAdapter inputAdapter, INavMeshAdapter navMeshAdapter,
            IPhysicsAdapter physicsAdapter = null,
            IGrenadePositionAdapter grenadePositionAdapter = null,
            ICoreDefinitionRegistry coreDefinitions = null)
        {
            _timeAdapter = timeAdapter;
            _inputAdapter = inputAdapter;
            _navMeshAdapter = navMeshAdapter;
            _physicsAdapter = physicsAdapter;
            _grenadePositionAdapter = grenadePositionAdapter;
            _coreDefinitions = coreDefinitions;
            _allocateEId = allocateEId;
            _eventBuffer = new RaidEventBuffer();
            RaidState = RaidState.Create(allocateEId);
            LevelState = LevelState.Create(levelId);
        }

        public void Start()
        {
            var spawnPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            var selectedSpawnPoint = PlayerSpawnPointSelector.Pick(spawnPoints, UnityEngine.Random.value);
            if (selectedSpawnPoint == null)
            {
                Debug.LogWarning("No PlayerSpawnPoint found in the scene. Player will spawn at world origin.");
            }
            else if (System.Array.TrueForAll(spawnPoints, point => point == null || point.weight <= 0f))
            {
                Debug.LogWarning("All PlayerSpawnPoint weights are 0. Falling back to an equal random choice among available points.");
            }

            RaidState.RaidDurationSeconds = ResolveRaidDuration(LevelState.LevelId);

            var spawnPos = selectedSpawnPoint != null ? selectedSpawnPoint.transform.position : Vector3.zero;
            var progression = ProgressionSystem.ApplyAllocatedEffects(
                ProgressionTreeConfig.Instance, App.Instance.Player.Progression);
            float playerMaxHp = BotConstants.PlayerMaxHp + progression.MaxHpBonus;
            PlayerSpawnSystem.SpawnPlayer(
                RaidState, spawnPos, _eventBuffer, LevelState.LevelId, playerMaxHp);
            SpawnFromScenePoints();
            SpawnActiveQuestItems();
            // SpawnTestGroundItems();
            // SpawnTestBots();
            if (LevelState.LevelId == "shooting_range")
                SpawnShootingRangeTargets();
            else if (LevelState.LevelId == "kill_feel_range")
                SpawnKillFeelTargets();
            else if (LevelState.LevelId == "ranged_range")
                SpawnRangedRangeTargets();
            else if (LevelState.LevelId == "feedback_range")
                SpawnFeedbackRangeTargets();
            // horde_range: no static spawn — HordeSpawnSystem.Tick drives waves.

            _eventBuffer.RaidStarted();
        }

        /// <summary>
        /// Raid clock length for a level, or 0 for "no clock". The hideout is a safe hub, the
        /// shooting ranges are combat labs, and `test_level` (TestScene) is the dev sandbox —
        /// none of them should ever KIA the player on a timer. Real levels take the DevCheats
        /// value; per-map authoring lands with map #2 (roadmap M3.1).
        ///
        /// `test_level` is checked here rather than folded into <c>IsTestRange</c> on purpose:
        /// that predicate also decides who gets the 6-weapon cheat loadout, and TestScene is
        /// meant to play with the normal one.
        /// </summary>
        static float ResolveRaidDuration(string levelId)
        {
            if (levelId == "hideout" || levelId == "test_level" || PlayerSpawnSystem.IsTestRange(levelId))
                return Constants.RaidTimerConstants.NoLimit;

            return DevCheats.RaidDurationSeconds;
        }

        void SpawnFromScenePoints()
        {
            var botPoints = Object.FindObjectsByType<BotSpawnPoint>(FindObjectsSortMode.None);
            var lootPoints = Object.FindObjectsByType<LooseLootSpawnPoint>(FindObjectsSortMode.None);
            var containerPoints = Object.FindObjectsByType<LootContainerSpawnPoint>(FindObjectsSortMode.None);

            foreach (var sp in botPoints)
            {
                if (UnityEngine.Random.value > sp.spawnChance) continue;
                if (sp.config == null)
                {
                    UnityEngine.Debug.LogWarning($"BotSpawnPoint '{sp.name}' has no config asset — skipping.", sp);
                    continue;
                }
                sp.config.ApplyToRegistry();
                BotSpawnSystem.SpawnBot(RaidState, sp.config.TypeId,
                    sp.transform.position, sp.GetPatrolPositions(), _eventBuffer, _coreDefinitions);
            }

            foreach (var sp in lootPoints)
            {
                if (UnityEngine.Random.value > sp.spawnChance) continue;
                var (defId, count) = sp.RollItem();
                if (defId == null) continue;
                var id = RaidState.AllocateEId();
                var groundItem = GroundItemState.Create(id, defId, sp.transform.position, count);
                RaidState.GroundItems.Add(groundItem);
                _eventBuffer.GroundItemSpawned(id, sp.transform.position, defId);
            }

            // Clear any leftover visual overrides from a previous raid before re-registering.
            ContainerVisualRegistry.Clear();
            foreach (var sp in containerPoints)
            {
                if (UnityEngine.Random.value > sp.spawnChance) continue;
                if (sp.config == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"LootContainerSpawnPoint '{sp.name}' has no config asset — skipping.", sp);
                    continue;
                }
                sp.config.ApplyToRegistry();
                var config = sp.config.ToContainerTypeConfig();
                // Register the per-instance visual override (if any) BEFORE firing the
                // LootableSpawned event so LootablePresenter can consume it in the same tick.
                if (sp.visualPrefab != null)
                    ContainerVisualRegistry.Register(sp.transform.position, sp.visualPrefab);
                LootSystem.CreateContainer(RaidState, in config, sp.transform.position, _eventBuffer);
            }

            var workbenchPoints = Object.FindObjectsByType<WorkbenchSpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in workbenchPoints)
            {
                var id = RaidState.AllocateEId();
                var wb = WorkbenchState.Create(id, sp.transform.position, sp.kind);
                RaidState.Workbenches.Add(wb);
            }

            var deployPoints = Object.FindObjectsByType<DeploySpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in deployPoints)
            {
                var id = RaidState.AllocateEId();
                var dp = DeployPointState.Create(id, sp.transform.position);
                RaidState.DeployPoints.Add(dp);
            }

            var npcPoints = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in npcPoints)
            {
                var id = RaidState.AllocateEId();
                var npc = NpcState.Create(id, sp.transform.position, sp.npcId);
                RaidState.Npcs.Add(npc);
            }

            var extractionPoints = Object.FindObjectsByType<ExtractionPointSpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in extractionPoints)
            {
                var id = RaidState.AllocateEId();
                var ep = ExtractionPointState.Create(id, sp.transform.position, sp.radius, sp.label);
                RaidState.ExtractionPoints.Add(ep);
            }
        }

        void SpawnActiveQuestItems()
        {
            if (!App.IsInitialized) return;
            var db = App.Instance.QuestDatabase;
            var progress = App.Instance.Player?.QuestProgress;
            if (db == null || progress == null) return;

            var currentMap = MapIds.FromLevelId(LevelState.LevelId);
            if (currentMap == MapId.None) return;

            var activeQuests = QuestSystem.GetAllActiveQuests(progress, db);
            foreach (var quest in activeQuests)
            {
                if (quest?.Tasks == null) continue;
                foreach (var task in quest.Tasks)
                {
                    if (task is not FindItemTask findItem) continue;
                    if (findItem.Map != currentMap) continue;
                    if (string.IsNullOrEmpty(findItem.ItemId)) continue;

                    var id = RaidState.AllocateEId();
                    var groundItem = GroundItemState.Create(id, findItem.ItemId, findItem.Coordinates, 1);
                    RaidState.GroundItems.Add(groundItem);
                    _eventBuffer.GroundItemSpawned(id, findItem.Coordinates, findItem.ItemId);
                }
            }
        }

        void SpawnTestGroundItems()
        {
            // Non-weapon ground items (legacy "Rifle" entry retired у Cluster A 2026-05-01).
            var testItems = new (string defId, UnityEngine.Vector3 pos, int count)[]
            {
                ("Medkit",       new UnityEngine.Vector3( 3f, 0f,  2f), 1),
                ("Helmet_Basic", new UnityEngine.Vector3(-2f, 0f,  4f), 1),
                ("Armor_Basic",  new UnityEngine.Vector3( 5f, 0f, -1f), 1),
                ("Ammo_Rifle",   new UnityEngine.Vector3(-3f, 0f, -3f), 30),
            };

            foreach (var (defId, pos, count) in testItems)
            {
                var id = RaidState.AllocateEId();
                var groundItem = State.GroundItemState.Create(id, defId, pos, count);
                RaidState.GroundItems.Add(groundItem);
                _eventBuffer.GroundItemSpawned(id, pos, defId);
            }

            // Test Builder-assembled weapon as ground item — confirms WeaponConfiguration
            // travels intact ground ↔ inventory.
            var weaponId = RaidState.AllocateEId();
            var weaponPos = new UnityEngine.Vector3(4f, 0f, 4f);
            var testWeaponConfig = new State.WeaponConfiguration(
                payload:        new State.PayloadCoreInstance("BallisticRound", State.RarityTier.Common),
                delivery:       new State.DeliveryCoreInstance("Auto",          State.RarityTier.Common),
                exotic:         null,
                ammoInMagazine: 30);
            var weaponGroundItem = State.GroundItemState.CreateWeapon(
                weaponId, "Weapon", weaponPos, testWeaponConfig);
            RaidState.GroundItems.Add(weaponGroundItem);
            _eventBuffer.GroundItemSpawned(weaponId, weaponPos, "Weapon");
        }

        void SpawnTestBots()
        {
            BotSpawnSystem.SpawnBot(RaidState, "Scav",
                new UnityEngine.Vector3(10f, 0f, 10f),
                new[]
                {
                    new UnityEngine.Vector3(10f, 0f, 10f),
                    new UnityEngine.Vector3(15f, 0f, 5f),
                    new UnityEngine.Vector3(20f, 0f, 10f),
                },
                _eventBuffer, _coreDefinitions);

            // BotSpawnSystem.SpawnBot(RaidState, "PMC",
            //     new UnityEngine.Vector3(-10f, 0f, 15f),
            //     new[]
            //     {
            //         new UnityEngine.Vector3(-10f, 0f, 15f),
            //         new UnityEngine.Vector3(-5f, 0f, 20f),
            //         new UnityEngine.Vector3(-15f, 0f, 20f),
            //     },
            //     _eventBuffer, _coreDefinitions);
        }

        void SpawnTestContainers()
        {
            var spawns = new (ContainerTypeConfig config, UnityEngine.Vector3 pos)[]
            {
                (ContainerConstants.MedContainer, new UnityEngine.Vector3(6f, 0f, 6f)),
                (ContainerConstants.AmmoBox, new UnityEngine.Vector3(-6f, 0f, 6f)),
                (ContainerConstants.RandomLootBox, new UnityEngine.Vector3(0f, 0f, -6f)),
            };

            foreach (var (config, pos) in spawns)
                LootSystem.CreateContainer(RaidState, in config, pos, _eventBuffer);
        }

        void SpawnShootingRangeTargets()
        {
            // ── Row 1: Static close (z=8) ─────────────────────
            for (int i = 0; i < 5; i++)
            {
                float x = -8f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 8f);
                BotSpawnSystem.SpawnBot(RaidState, "Target", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 2: Static medium (z=16) ───────────────────
            for (int i = 0; i < 5; i++)
            {
                float x = -8f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 16f);
                BotSpawnSystem.SpawnBot(RaidState, "Target", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 3: Static far (z=24) ──────────────────────
            for (int i = 0; i < 5; i++)
            {
                float x = -8f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 24f);
                BotSpawnSystem.SpawnBot(RaidState, "Target", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 4: Horizontal patrol (z=20) ───────────────
            for (int i = 0; i < 3; i++)
            {
                float cx = 14f + i * 6f;
                var pos = new UnityEngine.Vector3(cx, 0f, 20f);
                var wpA = new UnityEngine.Vector3(cx - 4f, 0f, 20f);
                var wpB = new UnityEngine.Vector3(cx + 4f, 0f, 20f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetPatrol", pos, new[] { wpA, wpB }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 5: Vertical patrol (x=-16, various z) ────
            for (int i = 0; i < 3; i++)
            {
                float z = 10f + i * 6f;
                var pos = new UnityEngine.Vector3(-16f, 0f, z);
                var wpA = new UnityEngine.Vector3(-16f, 0f, 6f);
                var wpB = new UnityEngine.Vector3(-16f, 0f, 26f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetPatrol", pos, new[] { wpA, wpB }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 6: Fast targets (z=12) ────────────────────
            for (int i = 0; i < 2; i++)
            {
                float cx = 20f + i * 8f;
                var pos = new UnityEngine.Vector3(cx, 0f, 12f);
                var wpA = new UnityEngine.Vector3(cx - 6f, 0f, 12f);
                var wpB = new UnityEngine.Vector3(cx + 6f, 0f, 12f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetFast", pos, new[] { wpA, wpB }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 7: Dodge targets (x=-12) ──────────────────
            for (int i = 0; i < 2; i++)
            {
                float z = 12f + i * 4f;
                var pos = new UnityEngine.Vector3(-12f, 0f, z);
                BotSpawnSystem.SpawnBot(RaidState, "TargetDodge", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 8: Weak/killable (x=36, z=16) ────────────
            for (int i = 0; i < 5; i++)
            {
                float x = 36f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 16f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetWeak", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 9: Armored targets (z=28) ────────────────
            {
                // Helmet only — test ricochet
                var p1 = new UnityEngine.Vector3(-8f, 0f, 28f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetLightArmor", p1, new[] { p1 }, _eventBuffer, _coreDefinitions);

                // Full armor — test pen vs protection
                var p2 = new UnityEngine.Vector3(-4f, 0f, 28f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetHeavyArmor", p2, new[] { p2 }, _eventBuffer, _coreDefinitions);

                // Glass cannon — armor breaks fast, then dies
                var p3 = new UnityEngine.Vector3(0f, 0f, 28f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetGlassCannon", p3, new[] { p3 }, _eventBuffer, _coreDefinitions);

                // Tank — no helmet, body armor only, 200 HP
                var p4 = new UnityEngine.Vector3(4f, 0f, 28f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetTank", p4, new[] { p4 }, _eventBuffer, _coreDefinitions);

                // Heavy armor + patrol — moving armored target
                var p5 = new UnityEngine.Vector3(8f, 0f, 28f);
                var wp5 = new[] { new UnityEngine.Vector3(4f, 0f, 28f), new UnityEngine.Vector3(12f, 0f, 28f) };
                BotSpawnSystem.SpawnBot(RaidState, "TargetHeavyArmor", p5, wp5, _eventBuffer, _coreDefinitions);
            }
        }

        // Kill-feel test layout: many low-HP targets для iterating on death feel + ragdoll.
        // Targets spawn in groups by HP tier so player can directly compare 1-shot/2-shot/3-shot
        // kill feedback. Patrol/fast row tests moving-target ragdoll. Helmet row tests headshot
        // discrimination.
        void SpawnKillFeelTargets()
        {
            // ── Row 1: HP=10 (one-shot pistol/rifle) — close ──
            for (int i = 0; i < 8; i++)
            {
                float x = -14f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 8f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel10", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 2: HP=25 ──────────────────────────────────
            for (int i = 0; i < 8; i++)
            {
                float x = -14f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 12f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel25", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 3: HP=50 ──────────────────────────────────
            for (int i = 0; i < 8; i++)
            {
                float x = -14f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 16f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel50", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 4: HP=75 ──────────────────────────────────
            for (int i = 0; i < 6; i++)
            {
                float x = -10f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 20f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel75", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 5: HP=100 (multi-shot rifle, low-end LMG) ─
            for (int i = 0; i < 6; i++)
            {
                float x = -10f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 24f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel100", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 6: Patrol (moving target ragdoll) ─────────
            for (int i = 0; i < 4; i++)
            {
                float cx = 16f + i * 5f;
                var pos = new UnityEngine.Vector3(cx, 0f, 14f);
                var wpA = new UnityEngine.Vector3(cx - 3f, 0f, 14f);
                var wpB = new UnityEngine.Vector3(cx + 3f, 0f, 14f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeelPatrol", pos, new[] { wpA, wpB }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 7: Fast (running target — knockback test) ─
            for (int i = 0; i < 3; i++)
            {
                float cx = 16f + i * 6f;
                var pos = new UnityEngine.Vector3(cx, 0f, 22f);
                var wpA = new UnityEngine.Vector3(cx - 5f, 0f, 22f);
                var wpB = new UnityEngine.Vector3(cx + 5f, 0f, 22f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeelFast", pos, new[] { wpA, wpB }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 8: Helmet (headshot vs bodyshot test) ────
            // Same HP, different death silhouettes depending on aim zone — use to verify
            // headshot/bodyshot ragdoll profiles diverge visibly.
            for (int i = 0; i < 6; i++)
            {
                float x = -10f + i * 4f;
                var pos = new UnityEngine.Vector3(x, 0f, 28f);
                BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeelHelmet", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
            }

            // ── Row 9: Cluster (test multi-kill chain) ────────
            // Tightly packed — for spray/burst kill chains where multiple ragdolls fire
            // back-to-back. Tests pool overflow + visual chaos handling.
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float x = -22f + i * 1.5f;
                    float z = 14f + j * 1.5f;
                    var pos = new UnityEngine.Vector3(x, 0f, z);
                    BotSpawnSystem.SpawnBot(RaidState, "TargetKillFeel10", pos, new[] { pos }, _eventBuffer, _coreDefinitions);
                }
            }
        }

        // Ranged-combat playtest layout — 7 RangedTarget bots arranged in 4 zones at
        // escalating distance from player spawn (0,0,0). Each zone has its own cover
        // configuration so we can iterate on engagement scenarios:
        //   Zone A (z~12-14): close — minimal cover, bots in open
        //   Zone B (z~30):    mid — walls per side, central pillar splits lanes
        //   Zone C (z~50):    mid-far — L-corner + wall, cover-vs-cover trades
        //   Zone D (z~75):    long range — long central wall forces flank
        // Positions deliberately offset from the static cover cubes authored у scene
        // (ShootingScene_RangedRange.unity) so spawners never sit inside colliders.
        void SpawnRangedRangeTargets()
        {
            // Zone A — close (instant aggro on raid start)
            var a1 = new UnityEngine.Vector3(-3f, 0f, 13f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", a1, new[] { a1 }, _eventBuffer, _coreDefinitions);

            var a2 = new UnityEngine.Vector3(5f, 0f, 12f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", a2, new[] { a2 }, _eventBuffer, _coreDefinitions);

            // Zone B — mid range, lane-split layout
            var b1 = new UnityEngine.Vector3(-9f, 0f, 30f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", b1, new[] { b1 }, _eventBuffer, _coreDefinitions);

            var b2 = new UnityEngine.Vector3(10f, 0f, 30f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", b2, new[] { b2 }, _eventBuffer, _coreDefinitions);

            // Zone C — mid-far with corner cover
            var c1 = new UnityEngine.Vector3(-11f, 0f, 50f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", c1, new[] { c1 }, _eventBuffer, _coreDefinitions);

            var c2 = new UnityEngine.Vector3(13f, 0f, 50f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", c2, new[] { c2 }, _eventBuffer, _coreDefinitions);

            // Zone D — long range, behind big wall
            var d1 = new UnityEngine.Vector3(0f, 0f, 75f);
            BotSpawnSystem.SpawnBot(RaidState, "RangedTarget", d1, new[] { d1 }, _eventBuffer, _coreDefinitions);
        }

        // Damage-feedback playtest layout — 6 stationary turret bots (one per weapon archetype)
        // arranged in a row ahead of the player spawn. Each bot uses FireForward behavior
        // (continuous fire in fixed facing direction, no targeting). Player walks into the firing
        // lanes to sample hit reactions / VFX / HUD damage indicators. Pairs з GodMode visual
        // passthrough (DamageSystem zeroes player HP damage but fires all VFX events) — see
        // ShootingScene_Feedback.unity + level id "feedback_range".
        //
        // Row layout: x = -25…+25 step 10m, z = 10 (ahead of player), y = 0.
        // Bot order left→right: Ballistic Pistol / Rifle / Shotgun, Laser Pistol / Rifle / Shotgun.
        // Bots face -Z (back toward the player area) so the firing lanes intersect the player's walk path.
        void SpawnFeedbackRangeTargets()
        {
            const float z = 10f;
            const float step = 10f;
            string[] typeIds =
            {
                "FeedbackTarget_BPistol",
                "FeedbackTarget_BRifle",
                "FeedbackTarget_BShotgun",
                "FeedbackTarget_LPistol",
                "FeedbackTarget_LRifle",
                "FeedbackTarget_LShotgun",
            };
            // Center the row around x=0 — first bot at -((N-1)/2 × step).
            float startX = -((typeIds.Length - 1) * step) * 0.5f;
            for (int i = 0; i < typeIds.Length; i++)
            {
                var pos = new UnityEngine.Vector3(startX + i * step, 0f, z);
                BotSpawnSystem.SpawnBot(RaidState, typeIds[i], pos, new[] { pos }, _eventBuffer, _coreDefinitions);
                // Last added bot — face -Z so firing lane points back to the player area.
                var spawned = RaidState.Bots[RaidState.Bots.Count - 1];
                spawned.FacingDirection = -UnityEngine.Vector3.forward;
            }

            // Side column on the right edge — varied archetypes, facing -X (right→left fire).
            // Player walks vertically through the horizontal lanes, intercepting hit-vignette
            // direction-sector tests from a different axis (orthogonal to the top row).
            // x kept clear of top row's x=25 firing lane (top bot at x=25 fires straight -Z and
            // would shred this column if collinear — push sideX further out by ~10m).
            const float sideX = 35f;
            const float sideStartZ = 0f;
            const float sideStep = 4f;
            string[] sideTypeIds =
            {
                "FeedbackTarget_BRifle",
                "FeedbackTarget_BShotgun",
                "FeedbackTarget_LRifle",
            };
            for (int i = 0; i < sideTypeIds.Length; i++)
            {
                var pos = new UnityEngine.Vector3(sideX, 0f, sideStartZ + i * sideStep);
                BotSpawnSystem.SpawnBot(RaidState, sideTypeIds[i], pos, new[] { pos }, _eventBuffer, _coreDefinitions);
                var spawned = RaidState.Bots[RaidState.Bots.Count - 1];
                spawned.FacingDirection = -UnityEngine.Vector3.right;
            }
        }

        public void Tick()
        {
            if (!RaidState.IsRunning) return;

            var progression = ProgressionSystem.ApplyAllocatedEffects(
                ProgressionTreeConfig.Instance, App.Instance.Player.Progression);

            var progressionPlayer = RaidState.PlayerEntity;
            if (progressionPlayer != null
                && RaidState.HealthMap.TryGetValue(progressionPlayer.Id, out var playerHealth))
                ProgressionSystem.SyncMaxHp(
                    playerHealth, BotConstants.PlayerMaxHp + progression.MaxHpBonus);

            var context = new RaidContext(
                deltaTime: _timeAdapter.DeltaTime,
                events: _eventBuffer,
                time: _timeAdapter,
                input: _inputAdapter,
                navMesh: _navMeshAdapter,
                physics: _physicsAdapter,
                grenadePositions: _grenadePositionAdapter,
                coreDefinitions: _coreDefinitions,
                aimConfig: new AimConfig
                {
                    AimSplitEnabled = DevCheats.AimSplitEnabled,
                    AimFollowMultiplier = DevCheats.AimFollowMultiplier,
                    AdsAimFollowMultiplier = DevCheats.AdsAimFollowMultiplier,
                    RecoilRecoveryMultiplier = DevCheats.RecoilRecoveryMultiplier,
                    AdsRecoilRecoveryMultiplier = DevCheats.AdsRecoilRecoveryMultiplier,
                    MinAimDistance = DevCheats.MinAimDistance,
                    ScopeSpringStiffnessLow = DevCheats.ScopeSpringStiffnessLow,
                    ScopeSpringStiffnessHigh = DevCheats.ScopeSpringStiffnessHigh,
                    ScopeSpringDampingLow = DevCheats.ScopeSpringDampingLow,
                    ScopeSpringDampingHigh = DevCheats.ScopeSpringDampingHigh,
                    ScopeErgoImpact = DevCheats.ScopeErgoImpact,
                    ScopeNearDistance = DevCheats.ScopeNearDistance,
                    ScopeFarDistance = DevCheats.ScopeFarDistance,
                },
                shootingConfig: new ShootingConfig
                {
                    ProjectileSpawnHeight = DevCheats.ProjectileSpawnHeight,
                    ParallaxCorrection = DevCheats.ParallaxCorrection,
                    ConvergenceBlend = DevCheats.ConvergenceBlend,
                    ConvergenceAimUp = DevCheats.ConvergenceAimUp,
                    AimUpHeightRatio = DevCheats.AimUpHeightRatio,
                    ProjectileSpeedMultiplier = DevCheats.ProjectileSpeedMultiplier,
                    DamageMultiplier = DevCheats.DamageMultiplier,
                    NoRecoil = DevCheats.NoRecoil,
                    RecoilMultiplier = DevCheats.RecoilMultiplier,
                    AdsRecoilMultiplier = DevCheats.AdsRecoilMultiplier,
                    RecoilForwardMultiplier = DevCheats.RecoilForwardMultiplier,
                    RecoilSideMultiplier = DevCheats.RecoilSideMultiplier,
                    InfiniteAmmo = DevCheats.InfiniteAmmo,
                    MuzzleBlockEnabled = DevCheats.MuzzleBlockEnabled,
                    MuzzleBlockBackoff = DevCheats.MuzzleBlockBackoff,
                },
                staggerConfig: new StaggerConfig
                {
                    Enabled              = DevCheats.Config.Stagger.Enabled,
                    DurationLight        = DevCheats.Config.Stagger.DurationLight,
                    DurationHeavy        = DevCheats.Config.Stagger.DurationHeavy,
                    DurationHeadshot     = DevCheats.Config.Stagger.DurationHeadshot,
                    HeavyDamageThreshold = DevCheats.Config.Stagger.HeavyDamageThreshold,
                    AIShootingLockout    = DevCheats.Config.Stagger.AIShootingLockout,
                },
                armorConfig: new ArmorConfig
                {
                    ForceNoArmor     = DevCheats.ForceNoArmor,
                    ForceMaxArmor    = DevCheats.ForceMaxArmor,
                    DamageReductionK = DevCheats.ArmorK,
                    RicochetChance   = DevCheats.ArmorRicochetChance,
                },
                fovConfig: new FOVConfig
                {
                    Enabled          = DevCheats.FOVEnabled,
                    ForceShowAllBots = DevCheats.ForceShowAllBots,
                    NearRadius       = DevCheats.FOVNearRadius,
                    FarRadius        = DevCheats.FOVFarRadius,
                    Angle            = DevCheats.FOVAngle,
                    OcclusionEnabled = DevCheats.FOVOcclusionEnabled,
                },
                movementConfig: new MovementConfig
                {
                    MoveSpeedMultiplier    = DevCheats.MoveSpeedMultiplier,
                    AdsMoveSpeedMultiplier = DevCheats.AdsMoveSpeedMultiplier,
                },
                botEngagementConfig: new BotEngagementConfig
                {
                    Enabled             = DevCheats.Config.BotEngagement.Enabled,
                    MaxEngagementRadius = DevCheats.Config.BotEngagement.MaxEngagementRadius,
                },
                laserConfig: new LaserConfig
                {
                    ChargeTimeOverrideSeconds = DevCheats.Config.Laser.ChargeTimeOverrideSeconds,
                    ChargeRatioPower       = DevCheats.Config.Laser.ChargeRatioPower,
                    ChargeDamageMin        = DevCheats.Config.Laser.ChargeDamageMin,
                    ChargeDamagePower      = DevCheats.Config.Laser.ChargeDamagePower,
                    ShotgunMinSpreadMult   = DevCheats.Config.Laser.ShotgunMinSpreadMult,
                    ShotgunMaxSpreadMult   = DevCheats.Config.Laser.ShotgunMaxSpreadMult,
                    ShotgunMinLifetimeMult = DevCheats.Config.Laser.ShotgunMinLifetimeMult,
                    ShotgunMaxLifetimeMult = DevCheats.Config.Laser.ShotgunMaxLifetimeMult,
                    SingleActionChargeMult = DevCheats.Config.Laser.SingleActionChargeMult,
                    AutoChargeMult         = DevCheats.Config.Laser.AutoChargeMult,
                    ScatterChargeMult      = DevCheats.Config.Laser.ScatterChargeMult,
                },
                barrelHeatConfig: new BarrelHeatConfig
                {
                    Enabled             = DevCheats.Config.BarrelHeat.Enabled,
                    MaxHeatShots        = DevCheats.Config.BarrelHeat.MaxHeatShots,
                    DecayPerSecond      = DevCheats.Config.BarrelHeat.DecayPerSecond,
                    HeatCurvePower      = DevCheats.Config.BarrelHeat.HeatCurvePower,
                    MaxSpreadMultiplier = DevCheats.Config.BarrelHeat.MaxSpreadMultiplier,
                },
                cheatsConfig: new CheatsConfig
                {
                    GodMode = DevCheats.GodMode,
                    IgnoreBleedOnPlayer = DevCheats.IgnoreBleed,
                },
                raidTimerConfig: new RaidTimerConfig
                {
                    // Duration lives on RaidState (resolved per level at Start); the config carries
                    // it too so the HUD reads thresholds and length from one place.
                    DurationSeconds   = RaidState.RaidDurationSeconds,
                    WarnAtSeconds     = DevCheats.RaidWarnAtSeconds,
                    CriticalAtSeconds = DevCheats.RaidCriticalAtSeconds,
                },
                staminaConfig: new StaminaConfig
                {
                    MaxStamina              = DevCheats.Config.Stamina.MaxStamina,
                    SprintDrainRate         = DevCheats.Config.Stamina.SprintDrainRate,
                    RegenRate               = DevCheats.Config.Stamina.RegenRate,
                    RegenDelay              = DevCheats.Config.Stamina.RegenDelay,
                    SprintSpeedMultiplier   = DevCheats.Config.Stamina.SprintSpeedMultiplier,
                    ExhaustionRecoveryRatio = DevCheats.Config.Stamina.ExhaustionRecoveryRatio,
                },
                playerProgressionConfig: new PlayerProgressionConfig
                {
                    WeaponDamageMultiplier = progression.WeaponDamageMultiplier,
                    PenetrationMultiplier = progression.PenetrationMultiplier,
                    ArmorDamageMultiplier = progression.ArmorDamageMultiplier,
                    HeadshotDamageMultiplier = progression.HeadshotDamageMultiplier,
                    RecoilMultiplier = progression.RecoilMultiplier,
                    RecoilRecoveryMultiplier = progression.RecoilRecoveryMultiplier,
                    ReloadTimeMultiplier = progression.ReloadTimeMultiplier,
                    EquipTimeMultiplier = progression.EquipTimeMultiplier,
                    HeatBuildupMultiplier = progression.HeatBuildupMultiplier,
                    BleedAppliedMultiplier = progression.BleedAppliedMultiplier,
                }
            );

            // ADS state + blend (before Movement so speed is affected this frame)
            {
                var player = RaidState.PlayerEntity;
                if (player != null)
                {
                    player.IsADS = context.Input.AdsPressed
                                   && !player.IsRolling
                                   && !player.AreHandsBusy
                                   && !player.IsInMenu;
                    float adsTarget = player.IsADS ? 1f : 0f;
                    float adsSpeed = 1f / Mathf.Max(0.01f, DevCheats.AdsTransitionTime);
                    player.AdsBlend = Mathf.MoveTowards(player.AdsBlend, adsTarget,
                        context.DeltaTime * adsSpeed);
                }
            }

            StaminaSystem.Tick(RaidState, in context);
            RollSystem.Tick(RaidState, in context);
            MovementSystem.Tick(RaidState, in context);
            WeaponSyncSystem.Tick(RaidState, in context);
            WeaponEquipSystem.Tick(RaidState, in context);
            WeaponStateMachineSystem.Tick(RaidState, in context);
            AimingSystem.Tick(RaidState, in context);
            QuickSlotSystem.Tick(RaidState, in context);
            GrenadeSystem.Tick(RaidState, in context);
            MedkitSystem.Tick(RaidState, in context);
            StatusEffectSystem.Tick(RaidState, in context);
            BandageSystem.Tick(RaidState, in context);
            // Heat decay BEFORE ShootingSystem — same-frame increment net positive під час
            // sustained fire, decay leads коли player не стріляє.
            WeaponHeatSystem.Tick(RaidState, in context);
            ShootingSystem.Tick(RaidState, in context);

            PlayerVisionSystem.Tick(RaidState, in context); // resolve sniper-scope reveal before FOV
            PlayerFOVSystem.Tick(RaidState, in context);
            BotPerceptionSystem.Tick(RaidState, in context);
            BotBrainSystem.Tick(RaidState, in context);
            BotMovementSystem.Tick(RaidState, in context);
            BotCombatSystem.Tick(RaidState, in context);

            if (LevelState.LevelId == "horde_range")
                HordeSpawnSystem.Tick(RaidState, in context, _eventBuffer, _coreDefinitions);

            ProjectileSystem.Tick(RaidState, in context);
            GrenadeSystem.TickExplosions(RaidState, in context);
            ExtractionSystem.Tick(RaidState, in context);
            // After extraction (a run that just completed wins the tie), before DamageSystem so the
            // timeout death lands in the same frame's death processing as any other kill.
            RaidTimerSystem.Tick(RaidState, in context);
            DamageSystem.Tick(RaidState, _hitInbox, in context);
            _hitInbox.Clear();
            ProcessCollisions(in context);
            _collisionInbox.Clear();
            ProcessDamageAlerts();
            ProcessDeathEvents();

            if (context.Input.PickUpPressed && RaidState.PlayerEntity != null && !RaidState.PlayerEntity.IsInventoryOpen)
            {
                var player = RaidState.PlayerEntity;
                if (player.NpcTargetId != EId.None)
                {
                    player.NpcTargetId = EId.None;
                }
                else if (player.DeployTargetId != EId.None)
                {
                    player.DeployTargetId = EId.None;
                }
                else if (player.CraftTargetId != EId.None)
                {
                    player.CraftTargetId = EId.None;
                }
                else if (player.LootTargetId != EId.None)
                {
                    player.LootTargetId = EId.None;
                }
                    else
                    {
                        var nearest = LootSystem.FindNearestInteractable(
                            RaidState, player.Position, player.FacingDirection);
                        if (nearest.Type == InteractableType.Lootable)
                            player.LootTargetId = nearest.Id;
                        else if (nearest.Type == InteractableType.Workbench)
                            player.CraftTargetId = nearest.Id;
                        else if (nearest.Type == InteractableType.DeployPoint
                                 && QuestSystem.HasAcceptedAnyQuest(App.Instance.Player?.QuestProgress))
                            player.DeployTargetId = nearest.Id;
                        else if (nearest.Type == InteractableType.Npc)
                            player.NpcTargetId = nearest.Id;
                        else if (nearest.Type == InteractableType.GroundItem)
                            InventorySystem.TryPickUp(RaidState, nearest.Id, _eventBuffer);
                    }
            }

            RaidState.ElapsedTime += context.DeltaTime;
        }

        void ProcessCollisions(in RaidContext context)
        {
            foreach (var col in _collisionInbox)
            {
                for (int i = RaidState.Projectiles.Count - 1; i >= 0; i--)
                {
                    if (RaidState.Projectiles[i].Id == col.ProjectileId)
                    {
                        context.Events.ProjectileHit(col.ProjectileId, col.Position, col.Normal,
                            col.SurfaceType ?? "surface");
                        context.Events.ProjectileDespawned(col.ProjectileId);
                        RaidState.Projectiles.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        void ProcessDamageAlerts()
        {
            foreach (var e in _eventBuffer.All)
            {
                if (e.Type != RaidEventType.EntityDamaged) continue;

                for (int i = 0; i < RaidState.Bots.Count; i++)
                {
                    if (RaidState.Bots[i].Id == e.Id)
                    {
                        RaidState.Bots[i].Blackboard.WasDamaged = true;
                        RaidState.Bots[i].Blackboard.LastDamageTime = RaidState.ElapsedTime;
                        break;
                    }
                }
            }
        }

        void ProcessDeathEvents()
        {
            int count = _eventBuffer.All.Count;
            for (int idx = 0; idx < count; idx++)
            {
                var e = _eventBuffer.All[idx];
                if (e.Type != RaidEventType.EntityDied) continue;

                for (int i = RaidState.Bots.Count - 1; i >= 0; i--)
                {
                    if (RaidState.Bots[i].Id == e.Id)
                    {
                        var deadBot = RaidState.Bots[i];
                        if (BotConstants.TryGetConfig(deadBot.TypeId, out var cfg))
                            LootSystem.CreateLootable(RaidState, deadBot, in cfg, _eventBuffer);

                        if (App.IsInitialized
                            && RaidState.PlayerEntity != null
                            && e.KillerId == RaidState.PlayerEntity.Id)
                        {
                            var db = App.Instance.QuestDatabase;
                            var progress = App.Instance.Player?.QuestProgress;
                            if (db != null && progress != null)
                                QuestSystem.OnEnemyKilled(progress, db, deadBot.TypeId);
                        }

                        RaidState.Bots.RemoveAt(i);
                        _eventBuffer.BotDespawned(e.Id);
                        break;
                    }
                }

                if (RaidState.PlayerEntity != null && RaidState.PlayerEntity.Id == e.Id)
                {
                    if (App.IsInitialized)
                        App.Instance.LastRaidOutcome = RaidOutcome.KIA;
                    End();
                }

                RaidState.HealthMap.Remove(e.Id);
            }
        }

        public RaidEventBuffer ConsumeEvents() => _eventBuffer;

        public void ClearEvents() => _eventBuffer.Clear();

        public void ReportHit(HitSignal signal)
        {
            _hitInbox.Add(signal);
        }

        public void ReportCollision(CollisionSignal signal)
        {
            _collisionInbox.Add(signal);
        }

        public bool RequestCraft(string recipeId)
        {
            return CraftingSystem.TryCraft(RaidState, App.Instance.Player.Inventory, recipeId);
        }

        public void End()
        {
            RaidState.IsRunning = false;
            _eventBuffer.RaidEnded();
        }
    }
}
