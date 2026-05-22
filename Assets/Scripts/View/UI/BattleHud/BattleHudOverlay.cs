using System.Collections.Generic;
using ApplicationCore;
using Dev;
using State;
using UnityEngine;
using UnityEngine.UIElements;
using View.UI.Tooltip;
using View.UI.Tooltip.Builders;

namespace View.UI.BattleHud
{
    /// <summary>
    /// Battle HUD overlay — UI Toolkit panel hosting status effect row (Stage 3),
    /// future surfaces: armor display, stamina ring placeholder, etc.
    ///
    /// Replaces the legacy IMGUI <c>StatusEffectOverlay</c> + the Stage 1/2 uGUI
    /// <c>BattleHudPresenter</c> attempt. Mirrors <see cref="View.UI.Hotbar.HotbarOverlay"/>
    /// pattern (UIDocument + dynamically-spawned tiles + tooltip-via-ShowFromPanel).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class BattleHudOverlay : MonoBehaviour
    {
        public static BattleHudOverlay Instance { get; private set; }

        const string PanelSettingsPath = "UI/BattleHud/BattleHudPanelSettings";
        const string UxmlPath          = "UI/BattleHud/BattleHudOverlay";

        UIDocument _doc;
        VisualElement _root;
        VisualElement _statusRow;
        // Track tiles by composite key (Type + Level) so we can in-place update
        // when bleed escalates L1 → L2 without losing tooltip hover state.
        readonly Dictionary<string, VisualElement> _tiles = new();
        readonly HashSet<string> _seenThisFrame = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _doc = gameObject.GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Debug.LogError($"[BattleHudOverlay] PanelSettings missing at Resources/{PanelSettingsPath}");
                enabled = false;
                return;
            }
            _doc.panelSettings = panelSettings;

            var visualTree = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[BattleHudOverlay] UXML missing at Resources/{UxmlPath}");
                enabled = false;
                return;
            }
            _doc.visualTreeAsset = visualTree;
        }

        void OnEnable()
        {
            if (_doc == null) return;
            _root = _doc.rootVisualElement;
            if (_root == null) return;
            _statusRow = _root.Q<VisualElement>("status-row");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TooltipController.Instance?.Hide();
        }

        void LateUpdate()
        {
            if (_root == null || _statusRow == null) return;

            var cfg = ViewCheats.Config?.BattleHud;
            if (cfg == null || !cfg.Enabled)
            {
                if (_root.style.display != DisplayStyle.None) _root.style.display = DisplayStyle.None;
                return;
            }

            // BattleHud is itself a UI panel (it CAUSES IsPointerOverUi when cursor sits over
            // a tile). Hiding on IsPointerOverUi would create a feedback loop:
            //   cursor over tile → flag true → hide root → cursor over nothing → flag false → show → repeat.
            // Same approach as HotbarOverlay / InventoryWindow — always visible while Enabled.
            if (_root.style.display != DisplayStyle.Flex) _root.style.display = DisplayStyle.Flex;

            ApplyCornerAnchor(_statusRow, cfg.StatusRowCorner, cfg.StatusRowOffset);
            SyncStatusTiles(cfg);
        }

        void SyncStatusTiles(ViewCheatsBattleHudSection cfg)
        {
            var session = App.Instance?.RaidSession;
            var state = session?.RaidState;
            var player = state?.PlayerEntity;
            if (player == null)
            {
                ClearAllTiles();
                return;
            }

            _seenThisFrame.Clear();

            if (state.StatusEffects.TryGetValue(player.Id, out var effects))
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var e = effects[i];
                    var key = $"{e.Type}-L{e.Level}";
                    _seenThisFrame.Add(key);

                    if (!_tiles.TryGetValue(key, out var tile))
                    {
                        tile = CreateTile(e);
                        _tiles[key] = tile;
                        _statusRow.Add(tile);
                    }
                    else
                    {
                        // Refresh data ref so tooltip rebuild reads current instance state.
                        tile.userData = e;
                    }
                }
            }

            // Remove stale tiles (status expired) — iterate copy bo ми мутуємо dict.
            if (_tiles.Count > _seenThisFrame.Count)
            {
                var stale = new List<string>();
                foreach (var kvp in _tiles)
                    if (!_seenThisFrame.Contains(kvp.Key)) stale.Add(kvp.Key);
                foreach (var key in stale)
                {
                    _tiles[key].RemoveFromHierarchy();
                    _tiles.Remove(key);
                }
            }
        }

        VisualElement CreateTile(StatusEffectInstance e)
        {
            var tile = new VisualElement();
            tile.AddToClassList("bh-status-tile");
            tile.AddToClassList(VisualClassFor(e));
            tile.userData = e;

            var icon = new Label(EmojiFor(e));
            icon.AddToClassList("bh-status-tile__icon");
            icon.pickingMode = PickingMode.Ignore;
            tile.Add(icon);

            tile.RegisterCallback<PointerEnterEvent>(OnTilePointerEnter);
            tile.RegisterCallback<PointerLeaveEvent>(OnTilePointerLeave);
            return tile;
        }

        static void OnTilePointerEnter(PointerEnterEvent evt)
        {
            if (TooltipController.Instance == null) return;
            if (evt.target is not VisualElement tile) return;
            if (tile.userData is not StatusEffectInstance e) return;
            var model = StatusEffectTooltipBuilder.For(e);
            TooltipController.Instance.ShowFromPanel(model, evt.position);
        }

        static void OnTilePointerLeave(PointerLeaveEvent _)
        {
            TooltipController.Instance?.Hide();
        }

        void ClearAllTiles()
        {
            if (_tiles.Count == 0) return;
            foreach (var kvp in _tiles) kvp.Value.RemoveFromHierarchy();
            _tiles.Clear();
        }

        // ── Visual mapping (status type/level → USS class + emoji) ──────────
        // Inlined for now (2 cases). Move to a static helper if it grows.
        static string VisualClassFor(StatusEffectInstance e) => e.Type switch
        {
            StatusEffectType.Bleeding when e.Level >= 2 => "bh-status-tile--bleed-heavy",
            StatusEffectType.Bleeding                   => "bh-status-tile--bleed-light",
            _ => "bh-status-tile--bleed-light", // fallback
        };

        static string EmojiFor(StatusEffectInstance e) => e.Type switch
        {
            StatusEffectType.Bleeding => "🩸",
            _ => "?",
        };

        // ── Corner anchor ──────────────────────────────────────────────────
        // Offset = padding-inward from chosen corner. Positive values always push
        // toward center. Mirrors the same approach we'd planned for the paper-doll.
        static void ApplyCornerAnchor(VisualElement row, HudCorner corner, Vector2 offset)
        {
            // Reset all four edges first — only the chosen corner's two edges get set.
            row.style.left   = new StyleLength(StyleKeyword.Auto);
            row.style.right  = new StyleLength(StyleKeyword.Auto);
            row.style.top    = new StyleLength(StyleKeyword.Auto);
            row.style.bottom = new StyleLength(StyleKeyword.Auto);

            switch (corner)
            {
                case HudCorner.TopLeft:
                    row.style.left = offset.x;
                    row.style.top  = offset.y;
                    break;
                case HudCorner.TopRight:
                    row.style.right = offset.x;
                    row.style.top   = offset.y;
                    break;
                case HudCorner.BottomLeft:
                    row.style.left   = offset.x;
                    row.style.bottom = offset.y;
                    break;
                case HudCorner.BottomRight:
                default:
                    row.style.right  = offset.x;
                    row.style.bottom = offset.y;
                    break;
            }
        }
    }
}
