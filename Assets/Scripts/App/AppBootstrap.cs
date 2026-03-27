using UnityEngine;
using View;

namespace App
{
    [DefaultExecutionOrder(1000)]
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] LaunchMode _launchMode = LaunchMode.Raid;
        [SerializeField] string _defaultLevelId = "test_level";

        bool _isOwner;

        void Awake()
        {
            if (App.IsInitialized)
            {
                Destroy(gameObject);
                return;
            }

            _isOwner = true;
            App.Initialize();
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<HotbarDebugOverlay>();
            gameObject.AddComponent<InventoryUI>();
            gameObject.AddComponent<AimCursorOverlay>();
            gameObject.AddComponent<DamageNumberOverlay>();
            gameObject.AddComponent<StatusEffectOverlay>();
            gameObject.AddComponent<CraftingUI>();
            gameObject.AddComponent<StaminaBarOverlay>();
            gameObject.AddComponent<DeployUI>();
        }

        void Start()
        {
            LaunchOptions options;
            switch (_launchMode)
            {
                case LaunchMode.Hideout:
                    options = LaunchOptions.Hideout();
                    break;
                case LaunchMode.Raid:
                    options = LaunchOptions.DefaultRaid(_defaultLevelId);
                    break;
                case LaunchMode.TestScenario:
                    options = LaunchOptions.DefaultRaid(_defaultLevelId);
                    break;
                default:
                    options = LaunchOptions.Menu();
                    break;
            }
            GameLauncher.Launch(options).Forget();
        }

        void Update()
        {
            App.Instance.Tick();
        }

        void LateUpdate()
        {
            App.Instance.LateTick();
        }

        void OnApplicationQuit()
        {
            if (_isOwner)
                App.Shutdown();
        }

        void OnDestroy()
        {
            if (_isOwner)
                App.Shutdown();
        }
    }
}
