using Adapters;
using Session;
using UnityEngine;

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

        App()
        {
            _timeAdapter = new UnityTimeAdapter();
            _inputAdapter = new UnityInputAdapter();
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

            RaidSession = new RaidSession(levelId, _timeAdapter, _inputAdapter);
            RaidSession.Start();
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
            RaidSession?.ClearEvents();
        }

        internal static void Shutdown()
        {
            if (_instance == null) return;

            _instance.EndRaid();
            _instance._inputAdapter?.Dispose();
            _instance = null;
            Debug.Log("[App] Shutdown.");
        }
    }
}
