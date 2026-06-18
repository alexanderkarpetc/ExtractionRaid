using Constants;
using UnityEngine;
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
            App.Initialize();
            DontDestroyOnLoad(gameObject);
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

            // Hotbar HUD strip — UI Toolkit replacement for the legacy uGUI hotbar.
            // Display-only; activation continues to flow through QuickSlotSystem keys 3-9.
            // Bind UX: right-click an inventory item → context menu offers "Bind to N".
            var hotbarHost = new GameObject("HotbarOverlay");
            hotbarHost.transform.SetParent(transform, false);
            hotbarHost.AddComponent<HotbarOverlay>();

            // Controls legend — top-right "[O] Controls" pill that expands into a
            // keybinding list. Passive HUD; toggled by the O key (handled in the
            // overlay itself). Own UIDocument host.
            var controlsHost = new GameObject("ControlsOverlay");
            controlsHost.transform.SetParent(transform, false);
            controlsHost.AddComponent<View.UI.Controls.ControlsOverlay>();

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
