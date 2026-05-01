using Adapters;
using Cysharp.Threading.Tasks;
using Quests;
using Save;
using Session;
using State;
using UnityEngine;
using UnityEngine.SceneManagement;
using View;

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
        readonly GrenadePositionAdapter _grenadePositionAdapter;
        readonly PlayerPresenter _playerPresenter;
        readonly ProjectilePresenter _projectilePresenter;
        readonly DestructiblePresenter _destructiblePresenter;
        readonly GroundItemPresenter _groundItemPresenter;
        readonly BotPresenter _botPresenter;
        readonly GrenadePresenter _grenadePresenter;
        readonly CorpsePresenter _corpsePresenter;
        readonly HitPausePresenter _hitPausePresenter;

        App()
        {
            _timeAdapter = new UnityTimeAdapter();
            _inputAdapter = new UnityInputAdapter();
            _navMeshAdapter = new UnityNavMeshAdapter();
            _physicsAdapter = new UnityPhysicsAdapter();
            _grenadePositionAdapter = new GrenadePositionAdapter();
            _playerPresenter = new PlayerPresenter(_inputAdapter.SetMuzzlePoint);
            _projectilePresenter = new ProjectilePresenter();
            _destructiblePresenter = new DestructiblePresenter();
            _groundItemPresenter = new GroundItemPresenter();
            _botPresenter = new BotPresenter();
            _grenadePresenter = new GrenadePresenter(_grenadePositionAdapter);
            _corpsePresenter = new CorpsePresenter();
            _hitPausePresenter = new HitPausePresenter();
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
                _navMeshAdapter, _physicsAdapter, _grenadePositionAdapter, CoreDefinitions);
            RaidSession.Start();

            var cam = Camera.main;
            if (cam != null)
                _inputAdapter.SetCamera(cam);

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
            if (cam != null)
                _inputAdapter.SetCamera(cam);

            StartRaid(levelId);
        }

        /// <summary>
        /// Ends the current raid and returns the player to the hideout scene.
        /// Used by both cheat-extract and real extraction flows.
        /// </summary>
        public async UniTask ExtractToHideout()
        {
            EndRaid();
            DisposePresenters();
            await SceneManager.LoadSceneAsync("HideoutScene");

            var cam = Camera.main;
            if (cam != null)
                _inputAdapter.SetCamera(cam);

            EnterHideout();
        }

        void DisposePresenters()
        {
            _playerPresenter.Dispose();
            _projectilePresenter.Dispose();
            _destructiblePresenter.Dispose();
            _groundItemPresenter.Dispose();
            _botPresenter.Dispose();
            _grenadePresenter.Dispose();
            _corpsePresenter.Dispose();
            _hitPausePresenter.Dispose();
        }

        public void EndRaid()
        {
            if (RaidSession == null) return;

            RaidSession.End();
            IsInHideout = false;
            SavePlayer();
            Debug.Log("[App] Raid ended.");
            RaidSession = null;
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            _inputAdapter.BlockGameplayInput = blocked;
        }

        public void Tick()
        {
            RaidSession?.Tick();
        }

        public void LateTick()
        {
            _destructiblePresenter.LateTick(RaidSession);
            _playerPresenter.LateTick(RaidSession);
            _botPresenter.LateTick(RaidSession);
            _projectilePresenter.LateTick(RaidSession);
            _grenadePresenter.LateTick(RaidSession);
            _groundItemPresenter.LateTick(RaidSession);
            _corpsePresenter.LateTick(RaidSession);
            _hitPausePresenter.LateTick(RaidSession);
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
