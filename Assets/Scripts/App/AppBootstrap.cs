using UnityEngine;

namespace App
{
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] bool _autoLaunchRaid = true;
        [SerializeField] string _defaultLevelId = "test_level";

        void Awake()
        {
            App.Initialize();
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_autoLaunchRaid)
            {
                var options = LaunchOptions.DefaultRaid(_defaultLevelId);
                GameLauncher.Launch(options).Forget();
            }
        }

        void Update()
        {
            App.Instance.Tick();
        }

        void OnDestroy()
        {
            App.Shutdown();
        }
    }
}
