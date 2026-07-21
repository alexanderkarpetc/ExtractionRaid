using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using View.UI.Attachments;
using View.UI.Controls;
using View.UI.EndOfRaid;
using View.UI.Inventory;
using View.UI.Progression;
using View.UI.WeaponBuilder;

namespace View.UI.PauseMenu
{
    /// <summary>
    /// In-raid pause overlay (UI Toolkit). Esc opens a centered popup with
    /// Resume / Settings / Exit-to-menu; Esc again (or Resume) closes it.
    ///
    /// While open the game is frozen (<see cref="Time.timeScale"/> = 0) and gameplay
    /// input is blocked. Own UIDocument host spawned by <see cref="AppBootstrap"/>,
    /// rendered above all HUD/modal panels (PanelSettings sort order 250).
    ///
    /// Esc is shared with every other modal/overlay, so the menu only *opens* when
    /// no other UI surface is up — those windows consume Esc to close themselves
    /// first. Visual language mirrors the MainMenu screen.
    /// </summary>
    // Runs BEFORE the modal/overlay presenters (which close themselves on Esc and may
    // flip their open-state the same frame). Reading their state first means one Esc
    // closes the top surface; a later Esc — with nothing else open — opens the menu.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuWindow : MonoBehaviour
    {
        public static PauseMenuWindow Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        Button _resumeBtn;
        Button _settingsBtn;
        Button _exitBtn;

        bool _isOpen;
        float _savedTimeScale = 1f;

        public bool IsOpen => _isOpen;

        void Awake()
        {
            Instance = this;
            BuildDocument();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Never leave the game frozen if we're torn down while paused.
            if (_isOpen) Time.timeScale = _savedTimeScale;
        }

        void Update()
        {
            if (_root == null) return;

            var kb = Keyboard.current;
            if (kb == null || !kb[Key.Escape].wasPressedThisFrame) return;

            if (_isOpen)
            {
                Resume();
            }
            else if (CanOpen())
            {
                Open();
            }
        }

        // ── Build ─────────────────────────────────────────────

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/PauseMenu/PauseMenu");
            var styles = Resources.Load<StyleSheet>("UI/PauseMenu/PauseMenu");
            var panel = Resources.Load<PanelSettings>("UI/PauseMenu/PauseMenuPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[PauseMenu] Missing UXML or PanelSettings in Resources/UI/PauseMenu/.");
                return;
            }

            // Re-apply scale config in code — Unity caches PanelSettings asset edits
            // unreliably across domain reloads (renders tiny on 4K otherwise).
            // Mirrors ControlsOverlay / docs/ai/ui-styling.md.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            if (styles != null && !_root.styleSheets.Contains(styles))
                _root.styleSheets.Add(styles);

            _root.style.flexGrow = 1;

            _resumeBtn = _root.Q<Button>("resumeBtn");
            _settingsBtn = _root.Q<Button>("settingsBtn");
            _exitBtn = _root.Q<Button>("exitBtn");

            if (_resumeBtn != null) _resumeBtn.clicked += Resume;
            if (_exitBtn != null) _exitBtn.clicked += OnExitClicked;
            if (_settingsBtn != null)
            {
                // Settings screen not built yet — visible but inert.
                _settingsBtn.SetEnabled(false);
                _settingsBtn.tooltip = "Coming soon";
            }

            // Hidden until Esc.
            _root.style.display = DisplayStyle.None;
        }

        // ── Open / close gating ───────────────────────────────

        /// <summary>
        /// True only when the pause menu may take Esc — there must be a live raid
        /// and no other modal/overlay open (those consume Esc to close themselves).
        /// </summary>
        static bool CanOpen()
        {
            if (!App.IsInitialized) return false;
            var player = App.Instance.RaidSession?.RaidState?.PlayerEntity;
            if (player == null) return false;

            if (player.IsInMenu) return false; // quests / notes / craft / deploy / npc
            if (InventoryWindow.Instance != null && InventoryWindow.Instance.IsOpen) return false;
            if (WeaponBuilderWindow.Instance != null && WeaponBuilderWindow.Instance.IsOpen) return false;
            if (AttachmentEditorWindow.Instance != null && AttachmentEditorWindow.Instance.IsOpen) return false;
            if (ControlsOverlay.Instance != null && ControlsOverlay.Instance.IsOpen) return false;
            if (EndOfRaidWindow.Instance != null && EndOfRaidWindow.Instance.IsVisible) return false;
            if (ProgressionWindow.Instance != null && ProgressionWindow.Instance.IsOpen) return false;

            return true;
        }

        public void Open()
        {
            if (_isOpen || _root == null) return;
            _isOpen = true;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (App.IsInitialized) App.Instance.SetGameplayInputBlocked(true);
            SetPausedFlag(true); // folds into IsInMenu → hotkeys can't open menus behind us

            _root.style.display = DisplayStyle.Flex;
        }

        public void Resume()
        {
            if (!_isOpen || _root == null) return;
            _isOpen = false;

            Time.timeScale = _savedTimeScale;
            if (App.IsInitialized) App.Instance.SetGameplayInputBlocked(false);
            SetPausedFlag(false);

            _root.style.display = DisplayStyle.None;
        }

        static void SetPausedFlag(bool paused)
        {
            if (!App.IsInitialized) return;
            var player = App.Instance.RaidSession?.RaidState?.PlayerEntity;
            if (player != null) player.IsPaused = paused;
        }

        void OnExitClicked()
        {
            // Restore time before tearing the app down so the menu scene runs normally.
            Time.timeScale = 1f;
            _isOpen = false;
            AppBootstrap.QuitToMainMenu();
        }
    }
}
