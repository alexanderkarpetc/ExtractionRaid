using Constants;
using UnityEngine;
using UnityEngine.SceneManagement;
using View;
using View.UI;
using View.UI.CraftingMockup;
using View.UI.Dialogue;
using View.UI.EndOfRaid;
using View.UI.Extraction;
using View.UI.Hotbar;
using View.UI.Minimap;
using View.UI.Inventory;
using View.UI.Tooltip;
using View.UI.WeaponBuilder;

namespace ApplicationCore
{
    [DefaultExecutionOrder(1000)]
    public class AppBootstrap : MonoBehaviour
    {
        const string MainMenuSceneName = "MainMenu";

        /// <summary>The owning bootstrap (the one that initialized <see cref="App"/>).</summary>
        public static AppBootstrap Instance { get; private set; }

        [SerializeField] LaunchMode _launchMode = LaunchMode.Raid;
        [SerializeField] string _defaultLevelId = "test_level";
        [SerializeField] PopupManager _popupManagerPrefab;
        [SerializeField] ItemIconRegistryAsset _itemIconRegistry;

        bool _isOwner;

        void Awake()
        {
            if (App.IsInitialized)
            {
                Destroy(gameObject);
                return;
            }

            _isOwner = true;
            Instance = this;
            App.Initialize();
            DontDestroyOnLoad(gameObject);

            // Most test/raid scenes own their own AppBootstrap and do not serialize
            // this optional reference. Keep the Resources asset as the shared default
            // so inventory icons work regardless of which scene is launched directly.
            if (_itemIconRegistry == null)
                _itemIconRegistry = Resources.Load<ItemIconRegistryAsset>("Configs/ItemIconRegistry");

            if (_popupManagerPrefab != null)
            {
                var pm = Instantiate(_popupManagerPrefab);
                DontDestroyOnLoad(pm.gameObject);
            }
            gameObject.AddComponent<InventoryUI>();
            gameObject.AddComponent<QuestPresenter>();
            gameObject.AddComponent<NotesPresenter>();
            gameObject.AddComponent<NpcDialoguePresenter>();
            gameObject.AddComponent<BuildingDialoguePresenter>();

            // Dialogue UIDocument host — must be its own GO because NpcDialogueWindow
            // requires a UIDocument component (panel settings owned by the window).
            var dialogueHost = new GameObject("NpcDialogueWindow");
            dialogueHost.transform.SetParent(transform, false);
            dialogueHost.AddComponent<NpcDialogueWindow>();

            // Quests modal — UI Toolkit quest popup.
            // Hidden by default; opened by QuestPresenter (journal, Key.I) or
            // NpcDialoguePresenter ("Open Quests" choice). Own UIDocument host.
            var questsWindowHost = new GameObject("QuestsWindow");
            questsWindowHost.transform.SetParent(transform, false);
            questsWindowHost.AddComponent<View.UI.Quests.QuestsWindow>();

            // Field Notes modal — UI Toolkit tutorial / field guide popup.
            // Hidden by default; toggled by NotesPresenter (Key.N). Own UIDocument host.
            var notesWindowHost = new GameObject("NotesWindow");
            notesWindowHost.transform.SetParent(transform, false);
            notesWindowHost.AddComponent<View.UI.Notes.NotesWindow>();

            // End-of-raid result screen — separate UIDocument host (own panel settings,
            // own sort order so it lands above HUD overlays).
            gameObject.AddComponent<EndOfRaidPresenter>();
            var endOfRaidHost = new GameObject("EndOfRaidWindow");
            endOfRaidHost.transform.SetParent(transform, false);
            endOfRaidHost.AddComponent<EndOfRaidWindow>();

            // Extraction HUD — radial timer widget driven by PlayerEntityState
            // extraction fields; ExtractionSystem ticks the progress, this presenter
            // shows the visual state and triggers RequestExtraction on completion.
            gameObject.AddComponent<ExtractionHudPresenter>();
            var extractionHudHost = new GameObject("ExtractionHudWindow");
            extractionHudHost.transform.SetParent(transform, false);
            extractionHudHost.AddComponent<ExtractionHudWindow>();

            // Deploy wayfinding (hideout) — world beacon on the exit-to-raid deploy point
            // + a screen-edge direction arrow to it (new-player "where do I leave?" cue).
            gameObject.AddComponent<DeployBeaconPresenter>();
            gameObject.AddComponent<DeployArrowPresenter>();

            // Notification banners — bottom-right toast stack. The presenter listens to
            // QuestSystem.TaskCompleted (only event wired in v1) and pushes banners to
            // the overlay window, which owns the UI Toolkit panel + animations.
            gameObject.AddComponent<NotificationPresenter>();
            var notificationHost = new GameObject("NotificationOverlay");
            notificationHost.transform.SetParent(transform, false);
            notificationHost.AddComponent<View.UI.Notifications.NotificationOverlay>();

            // Minimap — env-only screenshot captured once at raid start; markers are
            // contributed by the presenter (player/npc/extract/quest) and any external
            // caller via MinimapMarkerRegistry. Press M to expand from corner → fullscreen.
            gameObject.AddComponent<MinimapPresenter>();
            var minimapHost = new GameObject("MinimapWindow");
            minimapHost.transform.SetParent(transform, false);
            minimapHost.AddComponent<MinimapWindow>();
            gameObject.AddComponent<PointerOverUiTracker>();
            gameObject.AddComponent<DeployUI>();

            // Tooltip overlay — runtime UI Toolkit panel that floats on top of everything.
            // Reachable via TooltipController.Instance from inventory hover handlers and
            // (later) Builder D&D cards.
            var tooltipHost = new GameObject("TooltipController");
            tooltipHost.transform.SetParent(transform, false);
            tooltipHost.AddComponent<TooltipController>();

            // Weapon comparison panel — floating two-column compare shown on inventory hover of
            // a weapon (hovered vs equipped baseline). Reachable via WeaponComparePanel.Instance.
            var compareHost = new GameObject("WeaponComparePanel");
            compareHost.transform.SetParent(transform, false);
            compareHost.AddComponent<View.UI.Compare.WeaponComparePanel>();

            // Hotbar HUD strip — UI Toolkit replacement for the legacy uGUI hotbar.
            // Display-only; activation continues to flow through QuickSlotSystem keys 3-9.
            // Bind UX: right-click an inventory item → context menu offers "Bind to N".
            var hotbarHost = new GameObject("HotbarOverlay");
            hotbarHost.transform.SetParent(transform, false);
            var hotbar = hotbarHost.AddComponent<HotbarOverlay>();
            hotbar.SetIconRegistry(_itemIconRegistry);

            // Controls legend — top-right "[O] Controls" pill that expands into a
            // keybinding list. Passive HUD; toggled by the O key (handled in the
            // overlay itself). Own UIDocument host.
            var controlsHost = new GameObject("ControlsOverlay");
            controlsHost.transform.SetParent(transform, false);
            controlsHost.AddComponent<View.UI.Controls.ControlsOverlay>();

            // Pause menu — Esc-driven overlay (Resume / Settings / Exit to menu). Own
            // UIDocument host, rendered above all HUD/modals. Handles its own Esc input
            // and open-gating (only opens when no other modal/overlay is up).
            var pauseMenuHost = new GameObject("PauseMenuWindow");
            pauseMenuHost.transform.SetParent(transform, false);
            pauseMenuHost.AddComponent<View.UI.PauseMenu.PauseMenuWindow>();

            // Battle HUD overlay — UI Toolkit panel hosting status effect row (Stage 3+).
            // Replaces legacy IMGUI StatusEffectOverlay. Reuses TooltipController.ShowFromPanel
            // for hover tooltips. See docs/ai/gunplay/battle-hud.md.
            var battleHudHost = new GameObject("BattleHudOverlay");
            battleHudHost.transform.SetParent(transform, false);
            battleHudHost.AddComponent<View.UI.BattleHud.BattleHudOverlay>();

            // Inventory modal — UI Toolkit replacement for the legacy uGUI LootPopupView.
            // Hidden by default; InventoryUI drives Open/Close when
            // DevCheats.UseUiToolkitInventory is on (legacy popup is gated off in
            // that path). See docs/ai/ui-styling.md and the migration plan.
            var inventoryWindowHost = new GameObject("InventoryWindow");
            inventoryWindowHost.transform.SetParent(transform, false);
            inventoryWindowHost.AddComponent<InventoryWindow>();
            InventoryWindow.Instance?.SetIconRegistry(_itemIconRegistry);

            // Crafting UI Toolkit mockup — hidden by default, toggled via DevCheats or F10.
            var craftingMockupHost = new GameObject("CraftingMockupWindow");
            craftingMockupHost.transform.SetParent(transform, false);
            craftingMockupHost.AddComponent<CraftingMockupWindow>();
            craftingMockupHost.AddComponent<CraftingMockupHotkey>();

            // Character progression skill tree — hidden by default, toggled with K.
            var progressionHost = new GameObject("ProgressionWindow");
            progressionHost.transform.SetParent(transform, false);
            progressionHost.AddComponent<View.UI.Progression.ProgressionWindow>();
            progressionHost.AddComponent<View.UI.Progression.ProgressionHotkey>();

            // Attachment editor modal — edit a weapon's attachments anywhere (P2.2). Hidden
            // by default; opened via AttachmentEditorWindow.Instance.Open(weaponItem). Lazily
            // builds its own presenter. See docs/ai/weapon-builder/attachments/.
            var attachmentEditorHost = new GameObject("AttachmentEditorWindow");
            attachmentEditorHost.transform.SetParent(transform, false);
            attachmentEditorHost.AddComponent<View.UI.Attachments.AttachmentEditorWindow>();

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

        /// <summary>
        /// Abandons the current session and returns to the main menu. Tears down the
        /// persistent <see cref="App"/> and all DontDestroyOnLoad UI hosts (children of
        /// this GameObject) by destroying the owning bootstrap, then loads the menu
        /// scene. Called from the pause menu's Exit button.
        /// </summary>
        public static void QuitToMainMenu()
        {
            Time.timeScale = 1f;

            var owner = Instance;
            // Destroying the bootstrap GO triggers OnDestroy → App.Shutdown() and takes
            // every child UI host down with it, so nothing leaks into the menu scene.
            if (owner != null)
                Destroy(owner.gameObject);

            SceneManager.LoadScene(MainMenuSceneName);
        }

        void OnApplicationQuit()
        {
            if (_isOwner)
                App.Shutdown();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_isOwner)
                App.Shutdown();
        }
    }
}
