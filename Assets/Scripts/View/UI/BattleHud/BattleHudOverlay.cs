using System.Collections.Generic;
using ApplicationCore;
using Dev;
using State;
using Systems;
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
        VisualElement _ammoBlock;
        Label _ammoMag;
        Label _ammoReserve;
        Label _ammoType;
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
            _ammoBlock = _root.Q<VisualElement>("ammo-block");
            _ammoMag = _root.Q<Label>("ammo-mag");
            _ammoReserve = _root.Q<Label>("ammo-reserve");
            _ammoType = _root.Q<Label>("ammo-type");
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
            RefreshAmmo(cfg);
        }

        // ── Ammo counter (Stage 7) ─────────────────────────────────────────
        void RefreshAmmo(ViewCheatsBattleHudSection cfg)
        {
            if (_ammoBlock == null) return;

            // Hidden when disabled, holstered (no equipped weapon), or weapon has no ammo
            // concept (null AmmoType — e.g. melee). Otherwise: mag / reserve / type name.
            var weapon    = App.Instance?.RaidSession?.RaidState?.PlayerEntity?.EquippedWeapon;
            var inventory = App.Instance?.Player?.Inventory;
            bool show = cfg.AmmoEnabled && weapon != null && !string.IsNullOrEmpty(weapon.AmmoType);

            if (!show)
            {
                if (_ammoBlock.style.display != DisplayStyle.None) _ammoBlock.style.display = DisplayStyle.None;
                return;
            }

            if (_ammoBlock.style.display != DisplayStyle.Flex) _ammoBlock.style.display = DisplayStyle.Flex;
            ApplyCornerAnchor(_ammoBlock, cfg.AmmoCorner, cfg.AmmoOffset);

            int mag     = weapon.AmmoInMagazine;
            int reserve = inventory != null ? AmmoSystem.CountReserve(inventory, weapon.AmmoType) : 0;
            int magSize = weapon.Stats.MagazineSize;

            if (_ammoMag != null)     _ammoMag.text = mag.ToString();
            if (_ammoReserve != null) _ammoReserve.text = reserve.ToString();
            if (_ammoType != null)
            {
                var def = ItemDefinition.Get(weapon.AmmoType);
                _ammoType.text = def != null ? def.DisplayName : weapon.AmmoType;
            }

            // Low/empty warning recolors the mag number (USS classes).
            float ratio = magSize > 0 ? (float)mag / magSize : 1f;
            bool empty = mag <= 0;
            bool low   = !empty && ratio <= cfg.AmmoLowThreshold;
            _ammoBlock.EnableInClassList("is-low", low);
            _ammoBlock.EnableInClassList("is-empty", empty);
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
                    var key = StatusEffectVisualMap.KeyFor(e);
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
            tile.AddToClassList(StatusEffectVisualMap.UssClassFor(e));
            tile.userData = e;

            var icon = new Label(StatusEffectVisualMap.EmojiFor(e));
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

        // Visual mapping (status type/level → USS class + emoji + color) lives у
        // `StatusEffectVisualMap` — shared with Stage 4 worldspace mini-icons.

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
