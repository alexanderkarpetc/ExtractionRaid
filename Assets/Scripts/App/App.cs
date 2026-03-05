using Adapters;
using Session;
using UnityEngine;
using View;

namespace App
{
    public class App
    {
        static App _instance;

        public static App Instance =>
            _instance ?? throw new System.InvalidOperationException(
                "App not initialized. Ensure AppBootstrap runs first.");

        public Player Player { get; private set; }
        public RaidSession RaidSession { get; private set; }

        readonly ITimeAdapter _timeAdapter;
        readonly UnityInputAdapter _inputAdapter;
        readonly INavMeshAdapter _navMeshAdapter;
        readonly PlayerPresenter _playerPresenter;
        readonly ProjectilePresenter _projectilePresenter;

        App()
        {
            _timeAdapter = new UnityTimeAdapter();
            _inputAdapter = new UnityInputAdapter();
            _navMeshAdapter = new UnityNavMeshAdapter();
            _playerPresenter = new PlayerPresenter();
            _projectilePresenter = new ProjectilePresenter();
            Player = new Player();
        }

        internal static void Initialize()
        {
            if (_instance != null)
            {
                Debug.LogWarning("[App] Already initialized.");
                return;
            }

            _instance = new App();
            Debug.Log("[App] Initialized.");
        }

        public void StartRaid(string levelId)
        {
            if (RaidSession != null && RaidSession.IsActive)
            {
                Debug.LogWarning("[App] Ending existing raid before starting new one.");
                EndRaid();
            }

            RaidSession = new RaidSession(levelId, _timeAdapter, _inputAdapter, _navMeshAdapter);
            RaidSession.Start();

            var cam = Camera.main;
            if (cam != null)
                _inputAdapter.SetCamera(cam);

            Debug.Log($"[App] Raid started on level '{levelId}'.");
        }

        public void EndRaid()
        {
            if (RaidSession == null) return;

            RaidSession.End();
            Debug.Log("[App] Raid ended.");
            RaidSession = null;
        }

        public void Tick()
        {
            RaidSession?.Tick();
        }

        public void LateTick()
        {
            _playerPresenter.LateTick(RaidSession);
            _projectilePresenter.LateTick(RaidSession);
            RaidSession?.ClearEvents();
        }

        internal static void Shutdown()
        {
            if (_instance == null) return;

            _instance._playerPresenter?.Dispose();
            _instance._projectilePresenter?.Dispose();
            _instance.EndRaid();
            _instance._inputAdapter?.Dispose();
            _instance = null;
            Debug.Log("[App] Shutdown.");
        }
    }
}
