using ApplicationCore;
using Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace View.UI.Death
{
    /// <summary>
    /// Full-screen "You died" overlay. Polls
    /// <c>RaidState.HealthMap[player.Id].IsAlive</c> each frame; when the
    /// player transitions to dead, shows a centered panel with action buttons.
    ///
    /// V1 ships with a single action — Respawn (full equip) — implemented as a
    /// scene reload plus an explicit <c>App.StartRaid(levelId)</c> on
    /// <c>SceneManager.sceneLoaded</c>. This rebuilds the entire raid:
    /// PlayerView, BotViews, ground items, and (when the level is the test
    /// shooting range) clears + re-grants the cheat starting loadout via
    /// <c>PlayerSpawnSystem.SpawnPlayer</c>. Ragdoll on the dead player body
    /// is wiped because the old PlayerView GameObject is scene-bound and dies
    /// with the reload; the next <c>PlayerSpawned</c> event makes
    /// <c>PlayerPresenter</c> spawn a fresh body.
    ///
    /// Future actions (return to hideout, watch killcam, etc) plug into the
    /// .ds-actions container — same UXML, more buttons.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class DeathScreen : MonoBehaviour
    {
        public static DeathScreen Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        Button _respawnBtn;
        bool _isShowing;

        // Carried across the scene reload — the old DeathScreen instance is
        // DontDestroyOnLoad so this field survives until LaunchAfterReload runs.
        string _pendingLevelId;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= LaunchAfterReload;
        }

        void BuildDocument()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            var panel = Resources.Load<PanelSettings>("UI/Death/DeathScreenPanelSettings");
            if (panel != null)
            {
                // Re-apply scale fields — see docs/ai/ui-styling.md "Override
                // PanelSettings scale fields in code".
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                _doc.panelSettings = panel;
            }
            else
            {
                Debug.LogWarning("[DeathScreen] DeathScreenPanelSettings missing — " +
                                 "asset is auto-provisioned by DeathScreenAssetsBootstrap.");
            }

            var visualTree = Resources.Load<VisualTreeAsset>("UI/Death/DeathScreen");
            if (visualTree != null)
                _doc.visualTreeAsset = visualTree;
            else
                Debug.LogWarning("[DeathScreen] DeathScreen.uxml missing.");

            // Resolve to the actual .ds-root element from UXML, not the UIDocument
            // wrapper. The USS hides .ds-root by default (display:none) so the
            // overlay stays invisible in Edit mode; runtime Show/Hide must flip
            // inline display on the same element the rule targets, otherwise the
            // class-level rule keeps it hidden regardless of wrapper styling.
            var docRoot = _doc.rootVisualElement;
            _root = docRoot?.Q<VisualElement>("root") ?? docRoot;
            _respawnBtn = docRoot?.Q<Button>("respawn-btn");
            if (_respawnBtn != null)
                _respawnBtn.clicked += OnRespawnClicked;
        }

        void Update()
        {
            if (App.Instance == null)
            {
                if (_isShowing) HideImmediate();
                return;
            }

            var raid = App.Instance.RaidSession;
            var player = raid?.RaidState?.PlayerEntity;
            if (player == null)
            {
                if (_isShowing) HideImmediate();
                return;
            }

            // RaidSession.ProcessDeathEvents removes the HealthMap entry on EntityDied
            // and calls End() — so "dead" reads as either entry missing OR entry alive=false.
            // (Bots get the same treatment when they die; we don't care, we filter by player.Id.)
            bool playerDead = !raid.RaidState.HealthMap.TryGetValue(player.Id, out var hp)
                              || !hp.IsAlive;

            if (playerDead && !_isShowing)
                Show();
            else if (!playerDead && _isShowing)
                HideImmediate();
        }

        void Show()
        {
            if (_root == null)
            {
                Debug.LogWarning("[DeathScreen] Show requested but _root is null — UXML not loaded?");
                return;
            }
            _root.style.display = DisplayStyle.Flex;
            _isShowing = true;
            // Block gameplay input + flag IsInMenu so AimCursorOverlay frees the
            // cursor (the player can't shoot back from the dead anyway, and they
            // need the cursor to click Respawn).
            App.Instance?.SetGameplayInputBlocked(true);
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null) player.IsDeathScreenOpen = true;
            Debug.Log("[DeathScreen] Show — player died.");
        }

        void HideImmediate()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
            bool was = _isShowing;
            _isShowing = false;
            if (was)
            {
                App.Instance?.SetGameplayInputBlocked(false);
                var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
                if (player != null) player.IsDeathScreenOpen = false;
                Debug.Log("[DeathScreen] Hide — player alive again.");
            }
        }

        void OnRespawnClicked()
        {
            _pendingLevelId = App.Instance?.RaidSession?.LevelState?.LevelId;
            if (string.IsNullOrEmpty(_pendingLevelId))
            {
                Debug.LogWarning("[DeathScreen] Cannot respawn — no active raid level id.");
                return;
            }

            // "Full equip" semantics: wipe inventory before relaunching so
            // PlayerSpawnSystem.SpawnPlayer sees IsInventoryEmpty=true and
            // re-grants the cheat starting loadout — works on any map, not just
            // shooting_range / kill_feel_range that auto-fire the loadout path.
            var inventory = App.Instance?.Player?.Inventory;
            if (inventory != null)
                PlayerSpawnSystem.ClearInventory(inventory);

            // Scene reload tears down PlayerView/BotView/ProjectileView (scene-bound).
            // App + RaidSession are DontDestroyOnLoad-rooted, so we explicitly
            // restart the raid in the sceneLoaded callback. PlayerPresenter notices
            // its `_playerView == null` (Unity null-overload returns true for the
            // destroyed GO) and spawns a fresh body on the next PlayerSpawned event.
            HideImmediate();
            SceneManager.sceneLoaded += LaunchAfterReload;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void LaunchAfterReload(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= LaunchAfterReload;
            if (string.IsNullOrEmpty(_pendingLevelId)) return;
            App.Instance?.StartRaid(_pendingLevelId);
            _pendingLevelId = null;
        }
    }
}
