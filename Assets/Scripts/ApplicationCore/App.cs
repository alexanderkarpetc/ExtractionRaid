using Adapters;
using Cysharp.Threading.Tasks;
using Quests;
using Save;
using Session;
using State;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using View;
using View.Audio;

namespace ApplicationCore
{
    public class App
    {
        static App _instance;

        public static bool IsInitialized => _instance != null;

        public static App Instance =>
            _instance ?? throw new System.InvalidOperationException(
                "App not initialized. Ensure AppBootstrap runs first.");

        public Player Player { get; private set; }
        public RaidSession RaidSession { get; private set; }
        public bool IsInHideout { get; private set; }
        public QuestDatabase QuestDatabase { get; private set; }
        public CoreDefinitionDatabase CoreDefinitionDatabase { get; private set; }
        public ICoreDefinitionRegistry CoreDefinitions { get; private set; }

        // Set when a raid ends (death from ProcessDeathEvents, or RequestExtraction).
        // Consumed by the end-of-raid screen; reset to None once the player returns
        // to the hideout via ReturnToHideout().
        public RaidOutcome LastRaidOutcome { get; internal set; }

        int _nextEIdValue;

        public EId AllocateEId()
        {
            _nextEIdValue++;
            return new EId(_nextEIdValue);
        }

        readonly ITimeAdapter _timeAdapter;
        readonly UnityInputAdapter _inputAdapter;
        readonly INavMeshAdapter _navMeshAdapter;
        readonly IPhysicsAdapter _physicsAdapter;
        readonly UnityCombatViewportAdapter _combatViewportAdapter;
        readonly GrenadePositionAdapter _grenadePositionAdapter;
        readonly PlayerPresenter _playerPresenter;
        readonly ProjectilePresenter _projectilePresenter;
        readonly DestructiblePresenter _destructiblePresenter;
        readonly GroundItemPresenter _groundItemPresenter;
        readonly BotPresenter _botPresenter;
        public BotPresenter BotPresenter => _botPresenter;
        readonly GrenadePresenter _grenadePresenter;
        readonly LootablePresenter _lootablePresenter;
        readonly HitPausePresenter _hitPausePresenter;
        readonly CameraShakePresenter _cameraShakePresenter;
        public CameraShakePresenter CameraShakePresenter => _cameraShakePresenter;
        readonly BloodDecalPresenter _bloodDecalPresenter;
        readonly BulletHoleDecalPresenter _bulletHoleDecalPresenter;
        readonly CasingEjectorPresenter _casingEjectorPresenter;
        readonly MagazineDropPresenter _magazineDropPresenter;
        readonly RagdollPresenter _ragdollPresenter;
        readonly FlinchPresenter _flinchPresenter;
        readonly BeamFlashPresenter _beamFlashPresenter;
        readonly DamageNumberPresenter _damageNumberPresenter;
        readonly CrosshairPresenter _crosshairPresenter;
        readonly HudDamagePresenter _hudDamagePresenter;
        readonly GameAudioPresenter _gameAudioPresenter;

        App()
        {
            _timeAdapter = new UnityTimeAdapter();
            _inputAdapter = new UnityInputAdapter();
            _navMeshAdapter = new UnityNavMeshAdapter();
            _physicsAdapter = new UnityPhysicsAdapter();
            _combatViewportAdapter = new UnityCombatViewportAdapter();
            _grenadePositionAdapter = new GrenadePositionAdapter();
            _playerPresenter = new PlayerPresenter(_inputAdapter.SetMuzzlePoint);
            _projectilePresenter = new ProjectilePresenter();
            _destructiblePresenter = new DestructiblePresenter();
            _groundItemPresenter = new GroundItemPresenter();
            _botPresenter = new BotPresenter();
            _grenadePresenter = new GrenadePresenter(_grenadePositionAdapter);
            _lootablePresenter = new LootablePresenter();
            _hitPausePresenter = new HitPausePresenter();
            _cameraShakePresenter = new CameraShakePresenter();
            _bloodDecalPresenter = new BloodDecalPresenter();
            _bulletHoleDecalPresenter = new BulletHoleDecalPresenter();
            _casingEjectorPresenter = new CasingEjectorPresenter();
            _magazineDropPresenter = new MagazineDropPresenter();
            _ragdollPresenter = new RagdollPresenter();
            _flinchPresenter = new FlinchPresenter();
            _beamFlashPresenter = new BeamFlashPresenter();
            _damageNumberPresenter = new DamageNumberPresenter();
            _crosshairPresenter = new CrosshairPresenter();
            _hudDamagePresenter = new HudDamagePresenter();
            _gameAudioPresenter = new GameAudioPresenter();
            Player = new Player();

            QuestDatabase = Resources.Load<QuestDatabase>("Quests/QuestGraph");
            if (QuestDatabase == null)
                Debug.LogError("[App] QuestDatabase not found in Resources. Create a quest graph at Resources/QuestDatabase.questgraph.");

            CoreDefinitionDatabase = Resources.Load<CoreDefinitionDatabase>("WeaponBuilder/CoreDefinitionDatabase");
            if (CoreDefinitionDatabase != null)
            {
                CoreDefinitions = new DatabaseCoreDefinitionRegistry(CoreDefinitionDatabase);
            }
            else
            {
                // Database is not yet populated during Tier 0a. Registry stays null until
                // stub assets land in Cluster D. Runtime code that depends on the registry
                // arrives in Tier 0b; both sides tolerate null until then.
                Debug.LogWarning("[App] CoreDefinitionDatabase not found at Resources/WeaponBuilder/CoreDefinitionDatabase. " +
                                 "Weapon Builder registry will be null until the asset is created.");
            }
        }

        internal static void Initialize()
        {
            if (_instance != null)
            {
                Debug.LogWarning("[App] Already initialized.");
                return;
            }

            _instance = new App();

            LayerUtils.InitCollisionMatrix();

#if UNITY_EDITOR
            // Editor-only convenience: "Raid → Remove Save On Start" menu toggle wipes
            // the save file before load so Play Mode starts with a fresh player. Default
            // ON for dev velocity; toggle off when explicitly testing save persistence.
            // Pref key duplicated from Editor.RaidToolsMenu.RemoveSaveOnStartPrefKey
            // (Game asmdef can't reference Editor asmdef directly).
            const string removeSaveOnStartPrefKey = "ExtractionRaid.RemoveSaveOnStart";
            if (UnityEditor.EditorPrefs.GetBool(removeSaveOnStartPrefKey, false))
                SaveManager.Delete();
#endif

            var save = SaveManager.Load();
            if (save != null)
                _instance.Player.LoadFrom(save);

            Debug.Log("[App] Initialized.");
        }

        public void StartRaid(string levelId)
        {
            if (RaidSession != null && RaidSession.IsActive)
            {
                Debug.LogWarning("[App] Ending existing raid before starting new one.");
                EndRaid();
            }

            RaidSession = new RaidSession(levelId, AllocateEId, _timeAdapter, _inputAdapter,
                _navMeshAdapter, _physicsAdapter, _grenadePositionAdapter, CoreDefinitions,
                _combatViewportAdapter);
            RaidSession.Start();

            var cam = Camera.main;
            _combatViewportAdapter.SetCamera(cam);
            if (cam != null)
            {
                _inputAdapter.SetCamera(cam);
            }

            Debug.Log($"[App] Raid started on level '{levelId}'.");
        }

        public void EnterHideout()
        {
            StartRaid("hideout");
            IsInHideout = true;
            Debug.Log("[App] Entered hideout.");
        }

        public async UniTask DeployToRaid(string sceneName, string levelId)
        {
            EndRaid();
            DisposePresenters();
            await SceneManager.LoadSceneAsync(sceneName);

            var cam = Camera.main;
            _combatViewportAdapter.SetCamera(cam);
            if (cam != null)
            {
                _inputAdapter.SetCamera(cam);
            }

            StartRaid(levelId);
        }

        /// <summary>
        /// Ends the current raid and marks the outcome as Extracted. Does NOT swap
        /// scenes — the end-of-raid screen takes over and routes the user to the
        /// hideout via <see cref="ReturnToHideout"/> when the player clicks Next.
        /// </summary>
        public void RequestExtraction()
        {
            LastRaidOutcome = RaidOutcome.Extracted;
            EndRaid();
        }

        /// <summary>
        /// Disposes raid presenters, loads the hideout scene, and re-enters hideout.
        /// Called by the end-of-raid screen's Next button after the player has seen
        /// the extraction/KIA result. Resets <see cref="LastRaidOutcome"/> on the
        /// way out so the screen doesn't re-trigger.
        /// </summary>
        public async UniTask ReturnToHideout()
        {
            // EndRaid is a no-op if RaidSession was already cleared (e.g. extraction
            // path nulls it; KIA path leaves the session live so the dead body still
            // ticks while the screen is up).
            EndRaid();
            DisposePresenters();
            await SceneManager.LoadSceneAsync("HideoutScene");

            var cam = Camera.main;
            _combatViewportAdapter.SetCamera(cam);
            if (cam != null)
            {
                _inputAdapter.SetCamera(cam);
            }

            EnterHideout();
            LastRaidOutcome = RaidOutcome.None;
        }

        void DisposePresenters()
        {
            _playerPresenter.Dispose();
            _projectilePresenter.Dispose();
            _destructiblePresenter.Dispose();
            _groundItemPresenter.Dispose();
            _botPresenter.Dispose();
            _grenadePresenter.Dispose();
            _lootablePresenter.Dispose();
            _hitPausePresenter.Dispose();
            _cameraShakePresenter.Dispose();
            _bloodDecalPresenter.Dispose();
            _bulletHoleDecalPresenter.Dispose();
            _casingEjectorPresenter.Dispose();
            _magazineDropPresenter.Dispose();
            _ragdollPresenter.Dispose();
            _flinchPresenter.Dispose();
            _beamFlashPresenter.Dispose();
            _damageNumberPresenter.Dispose();
            _crosshairPresenter.Dispose();
            _hudDamagePresenter.Dispose();
            _gameAudioPresenter.Dispose();
        }

        public void EndRaid()
        {
            if (RaidSession == null) return;

            string endedLevelId = RaidSession.LevelState?.LevelId;
            RaidSession.End();
            IsInHideout = false;
            if (LastRaidOutcome == RaidOutcome.Extracted)
                Systems.QuestSystem.OnPlayerExtracted(Player.QuestProgress, QuestDatabase, endedLevelId);
            Systems.QuestSystem.OnRaidEnded(Player.QuestProgress, QuestDatabase);

            // Risk loop (M1.1): dying forfeits everything carried into the raid —
            // equipped weapons + armor + backpack. Stash on base is a separate
            // collection and stays safe. Only KIA wipes; extraction (and the
            // outcome-None hideout-exit path) preserves the inventory.
            if (LastRaidOutcome == RaidOutcome.KIA)
                Player.Inventory.ClearAll();

            SavePlayer();
            Debug.Log("[App] Raid ended.");
            RaidSession = null;
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            _inputAdapter.BlockGameplayInput = blocked;
        }

        public void SetPointerOverUi(bool over)
        {
            _inputAdapter.IsPointerOverUi = over;
        }

        public bool IsPointerOverUi => _inputAdapter.IsPointerOverUi;

        public void Tick()
        {
            RaidSession?.Tick();
        }

        // The whole presenter fan-out shows up in the profiler as a single opaque
        // AppBootstrap.LateUpdate self-time entry, because none of these calls carry a
        // marker of their own. One marker per presenter makes that column readable without
        // needing Deep Profile. ProfilerMarker compiles out of non-development builds.
        static readonly ProfilerMarker MkDestructible     = new("App.LateTick.Destructible");
        static readonly ProfilerMarker MkPlayer           = new("App.LateTick.Player");
        static readonly ProfilerMarker MkRagdoll          = new("App.LateTick.Ragdoll");
        static readonly ProfilerMarker MkFlinch           = new("App.LateTick.Flinch");
        static readonly ProfilerMarker MkBot              = new("App.LateTick.Bot");
        static readonly ProfilerMarker MkProjectile       = new("App.LateTick.Projectile");
        static readonly ProfilerMarker MkGrenade          = new("App.LateTick.Grenade");
        static readonly ProfilerMarker MkGroundItem       = new("App.LateTick.GroundItem");
        static readonly ProfilerMarker MkLootable         = new("App.LateTick.Lootable");
        static readonly ProfilerMarker MkHitPause         = new("App.LateTick.HitPause");
        static readonly ProfilerMarker MkCameraShake      = new("App.LateTick.CameraShake");
        static readonly ProfilerMarker MkBloodDecal       = new("App.LateTick.BloodDecal");
        static readonly ProfilerMarker MkBulletHoleDecal  = new("App.LateTick.BulletHoleDecal");
        static readonly ProfilerMarker MkCasingEjector    = new("App.LateTick.CasingEjector");
        static readonly ProfilerMarker MkMagazineDrop     = new("App.LateTick.MagazineDrop");
        static readonly ProfilerMarker MkBeamFlash        = new("App.LateTick.BeamFlash");
        static readonly ProfilerMarker MkDamageNumber     = new("App.LateTick.DamageNumber");
        static readonly ProfilerMarker MkCrosshair        = new("App.LateTick.Crosshair");
        static readonly ProfilerMarker MkHudDamage        = new("App.LateTick.HudDamage");
        static readonly ProfilerMarker MkGameAudio        = new("App.LateTick.GameAudio");

        public void LateTick()
        {
            using (MkDestructible.Auto()) _destructiblePresenter.LateTick(RaidSession);
            using (MkPlayer.Auto()) _playerPresenter.LateTick(RaidSession);
            // RagdollPresenter MUST run before BotPresenter — it grabs character body GO
            // from the bot view before BotDespawned destroys the shell + body together.
            using (MkRagdoll.Auto()) _ragdollPresenter.LateTick(RaidSession);
            // FlinchPresenter consumes EntityHit events to drive spine lean; needs to run
            // while bot views still exist. Order before BotPresenter (which clears views).
            using (MkFlinch.Auto()) _flinchPresenter.LateTick(RaidSession);
            using (MkBot.Auto()) _botPresenter.LateTick(RaidSession);
            using (MkProjectile.Auto()) _projectilePresenter.LateTick(RaidSession);
            using (MkGrenade.Auto()) _grenadePresenter.LateTick(RaidSession);
            using (MkGroundItem.Auto()) _groundItemPresenter.LateTick(RaidSession);
            using (MkLootable.Auto()) _lootablePresenter.LateTick(RaidSession);
            using (MkHitPause.Auto()) _hitPausePresenter.LateTick(RaidSession);
            using (MkCameraShake.Auto()) _cameraShakePresenter.LateTick(RaidSession);
            using (MkBloodDecal.Auto()) _bloodDecalPresenter.LateTick(RaidSession);
            using (MkBulletHoleDecal.Auto()) _bulletHoleDecalPresenter.LateTick(RaidSession);
            using (MkCasingEjector.Auto()) _casingEjectorPresenter.LateTick(RaidSession);
            using (MkMagazineDrop.Auto()) _magazineDropPresenter.LateTick(RaidSession);
            using (MkBeamFlash.Auto()) _beamFlashPresenter.LateTick(RaidSession);
            using (MkDamageNumber.Auto()) _damageNumberPresenter.LateTick(RaidSession);
            using (MkCrosshair.Auto()) _crosshairPresenter.LateTick(RaidSession);
            using (MkHudDamage.Auto()) _hudDamagePresenter.LateTick(RaidSession);
            using (MkGameAudio.Auto()) _gameAudioPresenter.LateTick(RaidSession);
            RaidSession?.ClearEvents();
        }

        void SavePlayer()
        {
            SaveManager.Save(Player.ToSaveData());
        }

        internal static void Shutdown()
        {
            if (_instance == null) return;

            _instance.DisposePresenters();
            _instance.EndRaid();
            _instance._inputAdapter?.Dispose();
            _instance = null;
            Debug.Log("[App] Shutdown.");
        }
    }
}
