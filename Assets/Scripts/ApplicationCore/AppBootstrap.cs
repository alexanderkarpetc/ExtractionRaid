using UnityEngine;
using View;
using View.UI;
using View.UI.CraftingMockup;
using View.UI.Death;
using View.UI.Dialogue;
using View.UI.Hotbar;
using View.UI.Tooltip;
using View.UI.WeaponBuilder;

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
            gameObject.AddComponent<NpcDialoguePresenter>();

            // Dialogue UIDocument host — must be its own GO because NpcDialogueWindow
            // requires a UIDocument component (panel settings owned by the window).
            var dialogueHost = new GameObject("NpcDialogueWindow");
            dialogueHost.transform.SetParent(transform, false);
            dialogueHost.AddComponent<NpcDialogueWindow>();
            gameObject.AddComponent<AimCursorOverlay>();
            gameObject.AddComponent<DamageNumberOverlay>();
            gameObject.AddComponent<StatusEffectOverlay>();
            gameObject.AddComponent<CraftPresenter>();
            gameObject.AddComponent<StaminaBarOverlay>();
            gameObject.AddComponent<DefenderArmorHUD>();
            gameObject.AddComponent<DeployUI>();

            // Tooltip overlay — runtime UI Toolkit panel that floats on top of everything.
            // Reachable via TooltipController.Instance from inventory hover handlers and
            // (later) Builder D&D cards.
            var tooltipHost = new GameObject("TooltipController");
            tooltipHost.transform.SetParent(transform, false);
            tooltipHost.AddComponent<TooltipController>();

            // Hotbar HUD strip — UI Toolkit replacement for the legacy uGUI hotbar.
            // Display-only; activation continues to flow through QuickSlotSystem keys 3-9.
            // Bind UX: right-click an inventory item → context menu offers "Bind to N".
            var hotbarHost = new GameObject("HotbarOverlay");
            hotbarHost.transform.SetParent(transform, false);
            hotbarHost.AddComponent<HotbarOverlay>();

            // Death screen — full-screen "You died" overlay, polls HealthMap each frame.
            // V1 ships with a single action (Respawn full equip) implemented as scene reload + StartRaid.
            var deathScreenHost = new GameObject("DeathScreen");
            deathScreenHost.transform.SetParent(transform, false);
            deathScreenHost.AddComponent<DeathScreen>();

            // Crafting UI Toolkit mockup — hidden by default, toggled via DevCheats or F10.
            var craftingMockupHost = new GameObject("CraftingMockupWindow");
            craftingMockupHost.transform.SetParent(transform, false);
            craftingMockupHost.AddComponent<CraftingMockupWindow>();
            craftingMockupHost.AddComponent<CraftingMockupHotkey>();

            // Weapon Builder modal — hidden by default, opened by Workbench or DevCheats.
            var weaponBuilderHost = new GameObject("WeaponBuilderWindow");
            weaponBuilderHost.transform.SetParent(transform, false);
            var weaponBuilderWindow = weaponBuilderHost.AddComponent<WeaponBuilderWindow>();
            if (App.Instance.CoreDefinitions != null)
            {
                var presenter = new WeaponBuilderPresenter(
                    App.Instance.CoreDefinitions,
                    App.Instance.Player.Inventory,
                    App.Instance.AllocateEId);
                weaponBuilderWindow.Initialize(presenter);
            }
            else
            {
                Debug.LogWarning("[AppBootstrap] Skipping WeaponBuilder initialization — " +
                                 "CoreDefinitionDatabase missing. Run Tools → Weapon Builder → Create Stub Assets.");
            }
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
