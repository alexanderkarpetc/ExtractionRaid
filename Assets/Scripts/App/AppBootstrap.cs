using UnityEngine;
using View;

namespace App
{
    [DefaultExecutionOrder(1000)]
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] bool _autoLaunchRaid = true;
        [SerializeField] string _defaultLevelId = "test_level";

        void Awake()
        {
            App.Initialize();
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<HotbarDebugOverlay>();
            gameObject.AddComponent<InventoryUI>();
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

        void LateUpdate()
        {
            App.Instance.LateTick();
        }

        void OnDestroy()
        {
            App.Shutdown();
        }
    }
}
