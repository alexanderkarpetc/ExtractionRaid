using UnityEngine;
using View;
using View.UI;
using View.UI.CraftingMockup;

namespace ApplicationCore
{
    [DefaultExecutionOrder(1000)]
    public class AppBootstrap : MonoBehaviour
    {
        [SerializeField] LaunchMode _launchMode = LaunchMode.Raid;
        [SerializeField] string _defaultLevelId = "test_level";
        [SerializeField] PopupManager _popupManagerPrefab;

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
            if (_popupManagerPrefab != null)
            {
                var pm = Instantiate(_popupManagerPrefab);
                DontDestroyOnLoad(pm.gameObject);
            }
            gameObject.AddComponent<InventoryUI>();
            gameObject.AddComponent<QuestPresenter>();
            gameObject.AddComponent<AimCursorOverlay>();
            gameObject.AddComponent<DamageNumberOverlay>();
            gameObject.AddComponent<StatusEffectOverlay>();
            gameObject.AddComponent<CraftingUI>();
            gameObject.AddComponent<StaminaBarOverlay>();
            gameObject.AddComponent<DefenderArmorHUD>();
            gameObject.AddComponent<DeployUI>();

            // Crafting UI Toolkit mockup — hidden by default, toggled via DevCheats or F10.
            var craftingMockupHost = new GameObject("CraftingMockupWindow");
            craftingMockupHost.transform.SetParent(transform, false);
            craftingMockupHost.AddComponent<CraftingMockupWindow>();
            craftingMockupHost.AddComponent<CraftingMockupHotkey>();
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
